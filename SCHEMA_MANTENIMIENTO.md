# Mantenimiento del Schema SOPRO

## Regla fundamental

Todo cambio de schema debe tocar **3 artefactos en orden**:

| # | Artefacto | Ubicación |
|---|-----------|-----------|
| 1 | Entidad | `SOPRO.Core/Entities/` |
| 2 | Mapeo EF | `SOPRO.Data/Context/SOPROContext.cs` |
| 3 | Migración en SchemaManager | `SOPRO.Application/Services/SchemaManager.cs` |

Si tocas (1) y (2) sin tocar (3), los DBs viejos fallarán al abrirse con errores como:
- `SQLite Error 1: 'no such column: X'`
- `The data is NULL at ordinal N`

---

## Cómo agregar una migración nueva

1. **Crear el método** `M0XX_NombreDescriptivo(DbConnection conn, DbTransaction tx)` en `SchemaManager.cs`

2. **Registrarlo** en `EnsureCurrentSchema` dentro del bloque de la transacción principal:
   ```csharp
   M010_ConfigColumnasReporte(conn, tx);
   M011_SanitizarNullsLegacy(conn, tx);
   M012_TuNuevaMigracion(conn, tx);   // ← aquí
   tx.Commit();
   ```
   `RegistrarVersion` no va en ese bloque: se ejecuta en su propia transacción
   después de `RepararActividadesProgramadas`.

3. **Incrementar** `VersionActual`:
   ```csharp
   public const string VersionActual = "2026.04.5";  // de .4 a .5
   ```

4. **Regla para columnas string NOT NULL nuevas**: si agregas una columna `string`
   (sin `?`) a una entidad, agrégala también en `M011_SanitizarNullsLegacy` para
   limpiar NULLs en DBs viejos que puedan existir.

5. **Manejo de errores**: usa `Exec`; **no** envuelvas migraciones en un catch
   genérico. `CREATE ... IF NOT EXISTS` ya es idempotente en SQLite. El único
   duplicado tolerado es la carrera de `ALTER TABLE ... ADD COLUMN`
   (`SqliteException` con código 1 y mensaje `duplicate column name`).
   Cualquier otro fallo debe propagarse.

---

## Arquitectura del SchemaManager

```
EnsureCurrentSchema(context)
│
├── ValidarVersionRegistrada(conn)   ← ANTES de cualquier escritura (fail-closed)
│     └── si la versión es más nueva o ilegible: lanza y no toca el archivo
│
├── EnsureMigracionesTable(conn)     ← crea __MigracionesCustom si no existe
│
├── TRANSACCIÓN ──────────────────────────────────────────────────────
│   ├── M001_Herramientas(conn, tx)
│   ├── M002_ProyectosColumnas(conn, tx)        ← ModoCalculoPorcentajes, ParametrosFSR, etc.
│   ├── M003_ProgramacionObra(conn, tx)         ← 7 tablas de programación
│   ├── M004_ColumnasTablas(conn, tx)           ← Columnas* catálogos, Rendimiento, Modulo
│   ├── M005_Financiamiento(conn, tx)
│   ├── M006_TitulosReporte(conn, tx)
│   ├── M007_WrapTextoAlineacion(conn, tx)
│   ├── M008_MaquinariaColumnas(conn, tx)       ← TipoCombustible, NumeroLlantas, etc.
│   ├── M009_ConceptosPresupuesto(conn, tx)     ← CargosAdicionales, Nivel, Fechas
│   ├── M010_ConfigColumnasReporte(conn, tx)
│   ├── M011_SanitizarNullsLegacy(conn, tx)     ← NULLs en columnas string NOT NULL
│   ├── M012_DisenadorEncabezadoPdf(conn, tx)   ← PlantillasReporteElementos
│   ├── M013_TablasIndirectosVistas(conn, tx)   ← Grupos/Conceptos/ConfiguracionesIndirectos, VistasPresupuesto
│   ├── M014_TablasFormatoReporte(conn, tx)     ← FormatosCeldaReporte, ConfiguracionesEstiloFilaReporte
│   └── M015_VinculosCostoHorarioMaquinaria(conn, tx)
│
├── RepararActividadesProgramadas(conn, context)
│   └── TRANSACCIÓN PROPIA ── fuera de la principal porque necesita
│                             PRAGMA foreign_keys=OFF (no permitido en tx)
│
└── TRANSACCIÓN ── RegistrarVersion(conn, tx)   ← sella solo si todo lo anterior terminó bien
```

Contrato de fallo:

- La transacción principal es atómica: si una migración falla, se revierte y el
  error se propaga. `RegistrarVersion` **no** se ejecuta.
- `RepararActividadesProgramadas` corre después del commit de las migraciones y
  antes del sello de versión. Si falla, sus cambios se revierten, el error se
  propaga y **no** se registra versión. Las migraciones ya confirmadas son
  idempotentes y se reintentan en la siguiente apertura, que también reintenta
  la reparación.
- No se toleran fallos genéricos. Solo la carrera de columna duplicada en
  `AgregarColumna` se ignora mediante `EsDuplicadoIgnorable`.
- El gating de versión ocurre antes de crear/modificar cualquier tabla. Una
  versión registrada más nueva que `VersionActual` o con formato no reconocido
  rechaza la operación y conserva la fila original. La clave debe ser
  `SchemaVersion_` seguida de una versión estricta; un sufijo malformado no se
  normaliza.
- `SchemaVersion.Compare` es el único parser/comparador del contrato
  `AAAA.MM.revision`; lo comparten `SchemaManager`, `ExternalProjectSource` y
  `ProjectDuplicationService`.

---

## Verificar versión de schema de un DB

```csharp
string? version = SchemaManager.GetVersionRegistrada(context);
// "2026.04.5" si ya pasó por SchemaManager
// null si es un DB muy viejo (antes del SchemaManager)
```

`SchemaVersion.Compare` devuelve `null` si alguna de las dos versiones no cumple
el contrato `AAAA.MM.revision`, negativo si la primera es anterior y positivo si
es posterior. Hay que comprobar `version is not null` antes de llamarlo:
`EnsureCurrentSchema` rechaza (`InvalidOperationException`) las versiones
posteriores o ilegibles; los callers externos aplican la misma regla antes de
copiar/migrar.

---

## M014 — Tablas de formato de reporte (esquema `2026.04.4`)

La migración `M014_TablasFormatoReporte` crea, dentro de la transacción principal
(idempotente, `CREATE TABLE IF NOT EXISTS` + índices `CREATE UNIQUE INDEX IF NOT
EXISTS`), las dos tablas de la feature de formato de celdas/estilos de reporte:

| Tabla | Propósito | Índice único |
|-------|-----------|--------------|
| `FormatosCeldaReporte` | Override de estilo de UNA celda, por dirección estable (vista + fila + columna) | `IX_FormatosCeldaReporte_ProyectoId_TipoReporte_ClaveVista_ClaveFila_NombreColumna` |
| `ConfiguracionesEstiloFilaReporte` | Estilo semántico de fila por ROL (`RolFila`) | `IX_ConfiguracionesEstiloFilaReporte_ProyectoId_TipoReporte_RolFila` |

Detalles:

- Ambas tienen `FOREIGN KEY (ProyectoId) REFERENCES Proyectos(Id) ON DELETE
  CASCADE`: borrar el proyecto elimina sus formatos (mismo patrón que
  M003/M004/M005/M010/M013).
- Las propiedades de estilo (`Fuente`, `TamanoFuente`, `Negrita`, `Cursiva`,
  `ColorFuente`, `ColorFondo`, `AlineacionHorizontal`, `AlineacionVertical`,
  `Wrap`, `FormatoNumerico`) son **NULLables**: `NULL` = heredar el estilo del
  nivel inferior de la cascada, no "sin estilo".
- `ClaveVista TEXT NOT NULL DEFAULT ''`: `''` = vista por defecto. SQLite permite
  duplicados cuando una columna de un índice único es NULL; por eso `''` (y no
  NULL) es la clave canónica y evita ese agujero del índice único. En el resolver
  de estilos, una clave de vista vacía actúa como comodín.
- `TipoReporte` guarda el identificador estable del reporte ("Presupuesto",
  "APU", "Indirectos", "Explosion", "ProgramaObra", "ProgramaInsumos",
  "Financiamiento", "Materiales", "ManoDeObra", "Herramientas", "Maquinaria",
  "Utilidad", "FactorSalarioReal", "TabuladorFsr").
- Entidades espejadas en `SOPRO.Core/Entities/FormatoCeldaReporte.cs` y
  `SOPRO.Core/Entities/ConfiguracionEstiloFilaReporte.cs`; acceso centralizado
  via `SOPRO.Application/Services/Reporting/FormatoReporteService.cs` (upsert
  parcial; una fila sin ninguna propiedad de estilo se elimina en lugar de
  persistirse vacía).
- Con esta migración `VersionActual` pasó de `2026.04.3` a `2026.04.4`.

Uso del formato y precedencia de estilos: ver
[docs/formato-celdas-reportes.md](docs/formato-celdas-reportes.md).

---

## Puntos donde se llama SchemaManager

Callers vigentes de `EnsureCurrentSchema(context)`:

- `ProjectLifecycleService.CreateProject` — proyectos nuevos
- `ProjectLifecycleService.OpenProject` — apertura normal (con gating de versión)
- `ProjectLifecycleService.ApplyProjectUpgrades` — upgrade manual
- `ExternalProjectSource.OpenSnapshot` — copia temporal de una fuente externa
- `ProjectDuplicationService` — borrador de duplicación antes de migrarlo
- `FormCatalogoMaestro` — apertura del catálogo maestro en UI
- `FormImportarMaestro` — importación desde catálogo maestro

Patrón: crear el contexto, llamar `EnsureCurrentSchema(context)` y, solo si no
lanza, usar la base. En fuentes externas la versión se valida antes de copiar;
en duplicación se valida sobre el borrador ya copiado, antes de migrarlo; en la
apertura propia el gating ocurre dentro de `EnsureCurrentSchema` antes de
cualquier escritura.

---

## Deuda técnica pendiente

- [ ] Mover `SchemaManager` de `SOPRO.Application` a `SOPRO.Data` (es infraestructura pura)
- [ ] Tests automáticos: abrir DB nueva, DB vieja sin programación, DB vieja con programación conflictiva (en progreso — ver `SOPRO.Tests/Services/Schema/SchemaManagerMigrationTests.cs`)
