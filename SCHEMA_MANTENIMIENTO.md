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

1. **Crear el método** `M0XX_NombreDescriptivo(DbConnection conn)` en `SchemaManager.cs`

2. **Registrarlo** en `EnsureCurrentSchema` dentro del bloque de la transacción:
   ```csharp
   M010_ConfigColumnasReporte(conn);
   M011_SanitizarNullsLegacy(conn);
   M012_TuNuevaMigracion(conn);   // ← aquí
   RegistrarVersion(conn);
   tx.Commit();
   ```

3. **Incrementar** `VersionActual`:
   ```csharp
   public const string VersionActual = "2026.04.2";  // de .1 a .2
   ```

4. **Regla para columnas string NOT NULL nuevas**: si agregas una columna `string`
   (sin `?`) a una entidad, agrégala también en `M011_SanitizarNullsLegacy` para
   limpiar NULLs en DBs viejos que puedan existir.

---

## Arquitectura del SchemaManager

```
EnsureCurrentSchema(context)
│
├── EnsureMigracionesTable(conn)     ← crea __MigracionesCustom si no existe
│
├── TRANSACCIÓN ──────────────────────────────────────────────────────
│   ├── M001_Herramientas
│   ├── M002_ProyectosColumnas        ← ModoCalculoPorcentajes, ParametrosFSR, etc.
│   ├── M003_ProgramacionObra         ← 7 tablas de programación
│   ├── M004_ColumnasTablas           ← Columnas* catálogos, Rendimiento, Modulo
│   ├── M005_Financiamiento
│   ├── M006_TitulosReporte
│   ├── M007_WrapTextoAlineacion
│   ├── M008_MaquinariaColumnas       ← TipoCombustible, NumeroLlantas, etc.
│   ├── M009_ConceptosPresupuesto     ← CargosAdicionales, Nivel, Fechas
│   ├── M010_ConfigColumnasReporte
│   ├── M011_SanitizarNullsLegacy    ← NULLs en columnas string NOT NULL
│   └── RegistrarVersion             ← sella __MigracionesCustom con versión
│
└── RepararActividadesProgramadas(conn, context)
    └── TRANSACCIÓN PROPIA ── fuera de la principal porque necesita
                              PRAGMA foreign_keys=OFF (no permitido en tx)
```

---

## Verificar versión de schema de un DB

```csharp
string? version = SchemaManager.GetVersionRegistrada(context);
// "2026.04.1" si ya pasó por SchemaManager
// null si es un DB muy viejo (antes del SchemaManager)
```

---

## Puntos donde se llama SchemaManager

Todo `new SOPROContext(path)` en el sistema llama `SchemaManager.EnsureCurrentSchema(context)` inmediatamente después de `EnsureCreated()`:

- `ProjectLifecycleService.CreateProject` — proyectos nuevos
- `ProjectLifecycleService.OpenProject` — apertura normal
- `ProjectLifecycleService.ApplyProjectUpgrades` — upgrade manual
- `DatabaseInitializer.InitializeMasterCatalog` — catálogo maestro nuevo
- `DatabaseInitializer.CreateNewProject` — nuevo proyecto desde initializer
- `DatabaseInitializer.UpgradeSchema` — upgrade manual desde initializer
- `DatabaseMigrationHelper.EnsureTablesExist` — helper legacy (wrapper)
- `CatalogSearchService.BuildProjectCatalogIndex` — indexado de proyectos externos
- `ExternalMatrixImportService` — importación de matrices (3 métodos)
- `ExternalInsumoImportService` — importación de insumos (2 métodos)
- `FormCatalogoMaestro` — apertura del catálogo maestro en UI
- `FormImportarMaestro` — importación desde catálogo maestro

---

## Deuda técnica pendiente

- [ ] Mover `SchemaManager` de `SOPRO.Application` a `SOPRO.Data` (es infraestructura pura)
- [ ] Tests automáticos: abrir DB nueva, DB vieja sin programación, DB vieja con programación conflictiva
