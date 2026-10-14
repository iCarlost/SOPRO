# Personalizacion de formato en reportes

SOPRO permite personalizar el formato de las celdas de los reportes al estilo Excel:
fuente, tamano, negrita, cursiva, alineacion horizontal y vertical, color de texto y
de fondo, ajuste de texto y formato numerico. Los cambios se aplican en pantalla y
se reflejan igual en los reportes PDF y Excel, porque los tres consumen el mismo
snapshot neutral de reporte.

## Alcance

### Formato por celda desde el ribbon (seleccion tipo Excel)

Disponible en los modulos que implementan seleccion de celdas:

- Presupuesto
- Calculo de Indirectos
- Matrices / APU
- Explosion de Insumos
- Programa de Obra y Programa de Insumos
- Analisis de Financiamiento
- Catalogos de Materiales, Mano de Obra, Herramientas y Maquinaria

El modulo de Factor de Salario Real (FSR) en pantalla no admite formato desde el
ribbon; sus reportes PDF y Excel si aplican los estilos por rol de fila
(personalizados o predeterminados).

### Estilos por rol de fila (predeterminados y personalizados)

Presupuesto, Indirectos, Explosion, Programas, Financiamiento, Utilidad y FSR
(AE-2(A) y AE-2(C)) definen estilos por rol de fila (capitulo, subtotal, total,
seccion, etc.) que se aplican de forma consistente en el modulo y en los reportes
PDF y Excel. Los colores por defecto reproducen el pintado historico de cada
modulo.

## Como usarla

1. Abre un proyecto y ve a un modulo con reporte (por ejemplo, Presupuesto).
2. Elige el DESTINO del formato:
   - **Columna completa**: haz clic en el encabezado de la columna.
   - **Celdas**: selecciona una o varias celdas en el grid.
3. Selecciona el alcance de la seleccion:
   - **Shift** + clic extiende un rango contiguo.
   - **Ctrl** + clic agrega celdas o rangos discontinuos.
4. Aplica el formato con los controles del ribbon (fuente, tamano, negrita, cursiva,
   alineacion, colores, ajustar texto). El cambio se pinta de inmediato.
5. El indicador del ribbon muestra sobre que se aplicara: `columna <Nombre>` o
   `selección de N celda(s)`. Si las celdas seleccionadas no comparten el mismo
   formato, los tooltips avisan: `las celdas tienen formatos distintos` (estado
   mixto).
6. Deshaz o rehaz con **Ctrl+Z** / **Ctrl+Y**. Un rango aplicado se deshace como
   **UNA sola accion**, no celda por celda.

Notas:

- Los cambios se guardan automaticamente en el archivo del proyecto (`.soproproj`);
  no hay que pulsar un boton de guardar.
- Las filas vacias y las columnas no exportables se ignoran al aplicar formato.
- Si una propiedad no se configura (o se vacia), la celda hereda ese aspecto del
  nivel inferior de la jerarquia de estilos.
- Al formatear CELDAS, cada comando del ribbon (fuente, tamano, negrita, cursiva,
  alineacion, color de texto, color de fondo) aplica SOLO esa propiedad sobre la
  seleccion: el resto del formato de cada celda se conserva. Asi una edicion (p.
  ej. alinear) NO arrastra ni pisa otra propiedad (p. ej. el fondo) de otra celda.

## Precedencia de estilos

El estilo final de cada celda se resuelve como una cascada, de mayor a menor
precedencia (`ReportCellStyleResolver`):

| Nivel | Fuente | Descripcion |
|-------|--------|-------------|
| 1. Override de celda | `FormatosCeldaReporte` (persistido) | Formato aplicado a UNA celda concreta. Maxima precedencia. |
| 2. Estilo semantico de fila (rol) | `ConfiguracionesEstiloFilaReporte` + defaults del modulo | Aplica a todas las filas con ese rol (Capitulo, Subtotal, Total, Seccion, ...). |
| 3. Estilo de columna | Configuracion de columna del reporte | Estilo del contenido de la columna completa. |
| 4. Fila alterna (banding) | Estilo de tabla | Solo aplica en la fila alterna, si la tabla la define. |
| 5. Estilo de tabla | Estilo de contenido de la tabla | Base sobre la que se superponen los demas niveles. |

Reglas de la cascada:

- En cada nivel, una propiedad **nula hereda** del nivel inferior y un valor **no
  nulo gana**. Se combina propiedad a propiedad (no se reemplaza el estilo completo).
- Los colores se normalizan a `#RRGGBB` o `#AARRGGBB`; los valores invalidos se
  descartan y se comportan como heredar.
- El estilo de rol efectivo (nivel 2) es la combinacion del default del modulo
  (`EstilosFilaPredeterminados`) con el override del usuario: si solo personalizas,
  por ejemplo, el color de fondo de "Subtotal", la negrita predeterminada se
  conserva.

## Claves estables

Los formatos no se guardan por posicion visual (fila/columna visible), sino por
**claves estables** del dominio, de modo que sobreviven a reordenamientos, filtros
y paginacion:

- **Direccion de celda** = vista + fila + columna. La vista vacia (`""`) es la
  vista por defecto y actua como comodin.
- **Claves de fila** (`ClaveFilaReporte`), por reporte:

  | Reporte | Token | Ejemplo |
  |---------|-------|---------|
  | Presupuesto | `C:{Id}` (conceptos), `SUB:{Id}` (agrupadores), `SUB:` (cierre), `TOTAL`, `IVA` | `C:15`, `SUB:42` |
  | Indirectos | `G:{Id}`, `IC:{Id}` | `G:2`, `IC:7` |
  | APU / Matrices | `C:{Id}`, `COMP:{Id}`, `PU:{ConceptoId}` | `COMP:42` |
  | Programa de Obra | `ACT:{Id}`, `RES:{Clave}` | `ACT:103` |
  | Programa de Insumos | `INS:{InsumoId}` | `INS:8` |

  Las claves de fila de los AGRUPADORES del Presupuesto se derivan del Id de la
  entidad (`SUB:{Id}`), no de la `Clave` visible: dos agrupadores (Capítulo/
  Subcapítulo/Nivel) que compartan la misma `Clave` —o la tengan vacía— siguen
  teniendo claves DISTINTAS, de modo que editar la celda de uno NO cambia la de
  otro. Un estilo por ROL de fila sigue existiendo, pero solo se aplica cuando no
  hay override de celda para esa posición (precedencia celda &gt; rol).

- **Roles de fila** (`RolFilaReporte`): `Capitulo`, `Subcapitulo`, `Nivel1`,
  `Nivel2`, `Nivel3`, `Concepto`, `Subtotal`, `Total`, `Grupo`, `Resumen`,
  `Encabezado`, `TotalGeneral`, `Referencia`, `Seccion`.

## Persistencia y migracion

- El formato se guarda en el archivo del proyecto (SQLite `.soproproj`) en dos
  tablas creadas por la migracion **M014** del `SchemaManager` (esquema
  **`2026.04.4`**):
  - `FormatosCeldaReporte` — override por celda (indice unico por proyecto, tipo
    de reporte, vista, fila y columna).
  - `ConfiguracionesEstiloFilaReporte` — estilo semantico por rol (indice unico por
    proyecto, tipo de reporte y rol).
- Ambas tablas se eliminan en cascada si se borra el proyecto.
- Las propiedades de estilo son anulables: `NULL` significa heredar.
- `ClaveVista` es `NOT NULL` con valor `""` por defecto: SQLite permite duplicados
  cuando una columna del indice unico es NULL, por lo que `""` es la clave
  canonica de la vista por defecto.
- La escritura es un **upsert parcial**: solo se guardan las propiedades aplicadas;
  si tras un cambio la fila queda sin ninguna propiedad de estilo, se elimina.
- Los overrides se cargan con **una sola consulta** por (proyecto, tipo de reporte)
  al abrir el modulo y en cada recarga; nunca celda por celda.

La migracion es idempotente (`CREATE TABLE IF NOT EXISTS` / `CREATE INDEX IF NOT
EXISTS`) y corre dentro de la transaccion principal de `SchemaManager`, junto con
el resto de migraciones (M001-M014). Ver [SCHEMA_MANTENIMIENTO.md](../SCHEMA_MANTENIMIENTO.md).

## PDF y Excel

Los generadores de PDF y Excel **nunca leen el DataGridView**. Consumen un
`ReportColumnSnapshot` neutral (columnas + estilo de tabla + `EstilosCelda` +
`EstilosFila`) construido por `ReportStyleSnapshotBuilder` a partir de la
configuracion persistida. `ReportCellStyleResolver` resuelve el estilo final de
cada celda con la cascada anterior y los renderers de `SOPRO.Reporting` lo aplican.
Resultado: lo que ves en pantalla es lo que sale en el PDF y en el Excel.

## Notas de mantenimiento (desarrolladores)

Piezas clave del sistema:

| Pieza | Ubicacion |
|-------|-----------|
| Resolucion de cascada | `SOPRO.Reporting/Formatting/ReportCellStyleResolver.cs` |
| Persistencia (celda y rol) | `SOPRO.Application/Services/Reporting/FormatoReporteService.cs` |
| Defaults por rol y modulo | `SOPRO.Application/Services/Reporting/EstilosFilaPredeterminados.cs` |
| Claves estables de fila | `SOPRO.Application/Services/Reporting/ClaveFilaReporte.cs` |
| Roles semanticos | `SOPRO.Application/Services/Reporting/RolFilaReporte.cs` |
| Snapshot neutral + estilos | `SOPRO.Application/UseCases/Reporting/ReportStyleSnapshotBuilder.cs` |
| Modelos neutrales | `SOPRO.Application/Models/Reporting/ReportColumns/` |
| Entidades persistidas | `SOPRO.Core/Entities/FormatoCeldaReporte.cs`, `SOPRO.Core/Entities/ConfiguracionEstiloFilaReporte.cs` |
| Coordinador ribbon → celdas | `SOPRO.WinForms/Helpers/FormatoSeleccionCoordinator.cs`, `IGridSeleccionFormato.cs` |
| Undo/redo compuesto | `SOPRO.WinForms/Undo/UndoInfrastructure.cs` (`CompositeUndoableAction` + `UndoManager.Execute`) |
| Destino del ribbon | `SOPRO.WinForms/Forms/FormProyecto.Ribbon.cs`, `FormProyecto.Formato.cs` |
| Migracion M014 | `SOPRO.Application/Services/SchemaManager.cs` |

Tests relevantes: `SOPRO.Reporting.Tests` (resolver, defaults y snapshots por
modulo), `SOPRO.Tests` (`ClaveFilaReporteTests`, `FormatoReporteServiceTests`,
`ReportStyleSnapshotBuilderTests`), `SOPRO.Tests.WinForms` (seleccion, formato de
celdas por modulo y generadores con estilos).

Al agregar un modulo nuevo con formato por celda:

1. Implementar `IGridSeleccionFormato` en el form del modulo.
2. Definir el `TipoReporte` estable y, si aplica, defaults de rol en
   `EstilosFilaPredeterminados`.
3. Integrar el snapshot con `ReportStyleSnapshotBuilder` (o el builder `ConEstilos`
   del modulo) antes de renderizar PDF/Excel.
4. Agrupar los pares antes/después en una `CompositeUndoableAction` ejecutada con
   `UndoManager.Execute` (un rango = UNA accion).
