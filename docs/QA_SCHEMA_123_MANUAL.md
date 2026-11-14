# Suite manual — SchemaManager (#123)

Valida el cambio fusionado en `81cd936` sin tocar proyectos reales. Ejecutar
siempre sobre copias y con SOPRO/DB Browser cerrados cuando se edite SQLite.

## 0. Preparación

Requisitos: Windows, `main` en `81cd936`, DB Browser for SQLite (o equivalente)
y una carpeta temporal.

```powershell
$qa = Join-Path $env:TEMP 'SOPRO-QA-123'
Remove-Item $qa -Recurse -Force -ErrorAction SilentlyContinue
New-Item $qa -ItemType Directory | Out-Null
dotnet build SOPRO.sln -c Release --nologo
Copy-Item SOPRO.Tests\TestData\proyecto-sintetico-vial-demo.db `
  (Join-Path $qa 'base.soproproj')
```

Abrir una copia desde el código compilado:

```powershell
dotnet run --project SOPRO.WinForms\SOPRO.WinForms.csproj -c Release --no-build -- `
  "$qa\base.soproproj"
```

Con cualquier caso, tomar hash antes/después y revisar que no quede candado:

```powershell
Get-FileHash "$qa\caso.soproproj" -Algorithm SHA256
Test-Path "$qa\caso.soproproj.lock"
```

Consultas de inspección:

```sql
SELECT Clave, FechaAplicacion FROM __MigracionesCustom
WHERE Clave LIKE 'SchemaVersion_%';

SELECT type, name FROM sqlite_master
WHERE type IN ('table', 'index') ORDER BY type, name;
```

## 1. Humo: proyecto válido

Abrir `base.soproproj`, comprobar que carga el proyecto, cerrarlo y abrirlo de
nuevo.

Esperado:

- Abre sin error en ambas ocasiones y conserva nombre/datos.
- La versión es `SchemaVersion_2026.04.5`.
- No queda `.lock` al cerrar.

## 2. Compatibilidad de proyecto sin versión

Copiar la base como `sin-version.soproproj`. Con SOPRO cerrado, ejecutar:

```sql
DROP TABLE IF EXISTS __MigracionesCustom;
```

Abrir la copia.

Esperado: abre normalmente, recrea `__MigracionesCustom`, registra
`SchemaVersion_2026.04.5`, conserva los datos y permite cerrar/reabrir.

## 3. Esquema más nuevo: rechazo sin escritura

Copiar la base como `futuro.soproproj`, guardar el hash y ejecutar:

```sql
DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';
INSERT INTO __MigracionesCustom (Clave, FechaAplicacion)
VALUES ('SchemaVersion_2999.01.1', '2999-01-01T00:00:00.0000000');
```

Cerrar el editor SQLite y abrir con SOPRO.

Esperado:

- Diálogo con `más reciente` y `Actualice SOPRO`.
- La fila sigue siendo `SchemaVersion_2999.01.1` y sigue siendo única.
- No se crean tablas/índices, el hash no cambia y no queda `.lock`.

## 4. Versión no reconocida y clave malformada

Repetir el caso 3 con copias distintas.

### 4A. Sufijo no reconocible

```sql
DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';
INSERT INTO __MigracionesCustom (Clave, FechaAplicacion)
VALUES ('SchemaVersion_vNext', '2026-01-01T00:00:00.0000000');
```

Esperado: diálogo con `no reconocida`; no hay migración, modificación de hash,
cambio de clave ni `.lock` residual.

### 4B. Prefijo repetido

```sql
DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';
INSERT INTO __MigracionesCustom (Clave, FechaAplicacion)
VALUES ('SchemaVersion_SchemaVersion_2026.04.5', '2026-01-01T00:00:00.0000000');
```

Esperado: rechazo con `no reconocida`; la clave permanece literalmente igual y
no se convierte en `2026.04.5`.

## 5. Fallo real de migración: índice único

Este caso comprueba que un error real no se oculta ni reemplaza la versión
anterior. Copiar `base.soproproj` como `fallo-indice.soproproj` y ejecutar:

```sql
SELECT Id, ProgramaObraId, NumeroPeriodo
FROM PeriodosPrograma LIMIT 1;

DROP INDEX IF EXISTS IX_PeriodosPrograma_ProgramaObraId_NumeroPeriodo;

INSERT INTO PeriodosPrograma
    (ProgramaObraId, NumeroPeriodo, Etiqueta, FechaInicio, FechaFin, EsCerrado)
SELECT ProgramaObraId, NumeroPeriodo, Etiqueta, FechaInicio, FechaFin, EsCerrado
FROM PeriodosPrograma LIMIT 1;

DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';
INSERT INTO __MigracionesCustom (Clave, FechaAplicacion)
VALUES ('SchemaVersion_2026.04.4', '2026-04-04T00:00:00.0000000');
```

Abrir la copia y guardar antes el número de filas de `PeriodosPrograma`.

Esperado:

- Diálogo con `UNIQUE constraint failed`.
- Permanece `SchemaVersion_2026.04.4`; no aparece `.5`.
- Las filas duplicadas permanecen intactas y no queda `.lock`.

Recuperar la copia eliminando el duplicado y recreando el índice:

```sql
DELETE FROM PeriodosPrograma
WHERE Id = (
    SELECT MAX(Id) FROM PeriodosPrograma
    GROUP BY ProgramaObraId, NumeroPeriodo
    HAVING COUNT(*) > 1
);
CREATE UNIQUE INDEX IX_PeriodosPrograma_ProgramaObraId_NumeroPeriodo
    ON PeriodosPrograma(ProgramaObraId, NumeroPeriodo);
```

Al reabrir, debe abrir normalmente y registrar `2026.04.5`.

## 6. Fallo de `RepararActividadesProgramadas` (avanzada)

Ejecutar solo sobre una copia. Copiar `base.soproproj` como
`fallo-reparacion.soproproj`. Con SOPRO cerrado, forzar temporalmente una FK
legacy y eliminar una columna que la reparación necesita:

```sql
PRAGMA writable_schema = ON;
UPDATE sqlite_schema
SET sql = replace(sql, 'ON DELETE SET NULL', 'ON DELETE RESTRICT')
WHERE type = 'table' AND name = 'ActividadesProgramadas';
PRAGMA writable_schema = OFF;
```

Cerrar y volver a abrir la copia en el editor SQLite para recargar el esquema;
después ejecutar:

```sql
ALTER TABLE ActividadesProgramadas DROP COLUMN FechaModificacion;

DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';
INSERT INTO __MigracionesCustom (Clave, FechaAplicacion)
VALUES ('SchemaVersion_2026.04.4', '2026-04-04T00:00:00.0000000');

SELECT sql FROM sqlite_schema
WHERE type = 'table' AND name = 'ActividadesProgramadas';
```

Confirmar que contiene `ON DELETE RESTRICT` y no contiene
`FechaModificacion`. Si el editor no permite `DROP COLUMN`, restaurar la copia
y marcar este caso como no ejecutado; no improvisar sobre un proyecto real.

Al abrir con SOPRO se espera:

- Error SQLite con `no such column: FechaModificacion`.
- Permanece `SchemaVersion_2026.04.4`; no aparece `.5`.
- No queda `ActividadesProgramadas_FIX`.
- La tabla original permanece con `ON DELETE RESTRICT` y no queda `.lock`.

Reparar y repetir la apertura:

```sql
ALTER TABLE ActividadesProgramadas
ADD COLUMN FechaModificacion TEXT NOT NULL DEFAULT '';
```

Ahora debe reconstruir la tabla con `ON DELETE SET NULL` y registrar
`SchemaVersion_2026.04.5`.

## 7. Evidencia y criterio de aceptación

Marcar como aprobado solo si pasan 1–5 y, si se ejecuta, el caso 6. Para cada
fallo conservar: copia exacta, hash antes/después, SQL ejecutado, captura y
texto completo del diálogo, existencia de `.lock`, existencia de
`ActividadesProgramadas_FIX` y consulta posterior de `__MigracionesCustom`.

Los dos hashes PDF legacy conocidos no forman parte de esta suite.
