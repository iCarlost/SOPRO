# Plan 01: Desacoplamiento del Núcleo y la Aplicación

**Estado:** N0 cerrado (PR #2, `508188a`); N1 cerrado (PR #3, `b9a4576`+`eaa2f67`); N2 cerrado; N3 (slice inicial de Application) cerrado; N4 cerrado (PRs N4-1..N4-5 integrados, Gate N4 operativo cerrado); N5 en curso (16/17 grupos completos, 0 parciales, 1 pendiente; rama `feat/application-n5-20-financiamiento` en validación); N6 scaffold parcial (PackageId/versión 0.1.0/README presentes; falta CI, doc XML, changelog, global.json, consumidor externo, baseline API); N7 no iniciado.
**Decisión arquitectónica:** [ADR-001](ADR-001-ARQUITECTURA-OBJETIVO.md)  
**Plan dependiente:** [Plan 02: Migración WinForms a WPF](PLAN-02-MIGRACION-WINFORMS-WPF.md)  
**Distribución actual:** repositorio y paquete privados; sin publicación en NuGet.org

## 1. Objetivo

Separar los algoritmos de cálculo, los casos de uso y la infraestructura para lograr que:

- El motor pueda consumirse como biblioteca independiente y paquete en formato NuGet.
- SOPRO use exactamente el mismo motor que un consumidor de validación fuera de la solución.
- WinForms y WPF consuman los mismos casos de uso.
- La UI no conozca EF Core, SQLite ni entidades rastreadas.
- Los cambios estructurales no alteren inicialmente los resultados contables.

Este plan no pretende corregir todas las divergencias funcionales actuales. Primero establece una única ruta comprobable; las correcciones se harán después mediante cambios explícitos.

## 2. Estado actual relevante

- `MotorCalculoSopro` vive en `SOPRO.Application`; desde N2 es una fachada que delega la aritmética en `SOPRO.Calculation`.
- `SOPRO.Application` referencia Core, Data, ClosedXML y `SOPRO.Calculation`.
- El motor recibe `Proyecto`, `ConceptoPresupuesto` y `BudgetPercentageInput`.
- Existen consumidores directos en Application, WinForms y tests.
- Existen rutas de cálculo paralelas en preview, APU, FSR, financiamiento, programación y reportes.
- WinForms conserva y comparte instancias de `SOPROContext`.
- Application y WinForms mezclan consultas, mutaciones, cálculo y `SaveChanges`.

## 3. Alcance

### Incluido

- Caracterización del comportamiento legacy.
- Proyecto `SOPRO.Calculation`.
- Fachada compatible `MotorCalculoSopro`.
- Migración completa de consumidores del motor base.
- Casos de uso headless para desacoplar la UI.
- Ciclo de vida de contexto por operación.
- Inversión progresiva entre Application y Data.
- Empaquetado y validación privada del preview.
- Extracción posterior de módulos avanzados por contratos independientes.

### No incluido

- Reescritura visual de formularios.
- Correcciones contables mezcladas con la extracción.
- Cambio del esquema SQLite por necesidades del frontend.
- Soporte Linux o macOS.
- Reemplazo inmediato de PDFsharp, ClosedXML o Inno Setup.
- Creación anticipada de capas genéricas.
- Publicación en NuGet.org o apertura del repositorio fuente.

### Terminología de distribución

- **API pública:** miembros `public` de C#; no significa código fuente público.
- **Consumidor externo:** proyecto de validación fuera de `SOPRO.sln`; puede usar un feed local o privado.
- **Paquete NuGet:** formato `.nupkg`; no implica publicación en NuGet.org.
- **Release de SOPRO:** instalador de la aplicación; es independiente de la distribución del código fuente y del paquete de cálculo.

## 4. Definición de paridad

Durante las fases N0 a N5, paridad significa:

- Mismo valor `decimal` exacto.
- Mismo orden de operaciones observable.
- Misma secuencia y orden de elementos.
- Mismo elemento receptor del residuo.
- Mismo string cuando forma parte de la API legacy.
- Misma cultura efectiva en la fachada.
- Mismo fallback de modo y valores desconocidos.
- Mismo tipo y momento observable de excepción cuando esté caracterizado.
- Misma mutación o ausencia de mutación contractual.

"Mismo centavo" no es una definición suficiente.

## 5. Contrato legacy que debe preservarse

La fachada debe conservar dos constructores:

```text
MotorCalculoSopro(Proyecto proyecto)
MotorCalculoSopro(int decimalesCantidad, int decimalesImporte, int decimalesPorcentaje)
```

Debe conservar estos 15 métodos:

```text
RedondearCantidad
RedondearImporte
RedondearPorcentaje
Multiplicar
CalcularImporteSobreBase
CalcularPrecioUnitario
DistribuirImporte
DistribuirCantidad
SumarImportes
SumarCantidades
SumarCostoDirecto
FormatCantidad
FormatImporte
FormatPorcentaje
FormatNumero
```

También debe conservar `DesglosePrecios`, sus propiedades posicionales, propiedades calculadas, igualdad, deconstrucción y uso con `with`.

## 6. Comportamientos legacy conocidos

Estos comportamientos se caracterizan antes de decidir si son defectos:

- Las precisiones negativas se normalizan a cero.
- Las precisiones mayores de 28 fallan al redondear, no necesariamente al construir.
- `Multiplicar` redondea el precio unitario, pero no la cantidad.
- `DecimalesPorcentaje` no participa actualmente en la cascada de precio.
- Cualquier modo distinto de `"SobreCD"` cae en acumulables.
- Los indirectos central y campo se suman antes del redondeo monetario del motor.
- `DesglosePrecios` separa central/campo con una precisión fija de seis decimales.
- Distribución vacía devuelve vacío.
- Suma de pesos cero devuelve ceros.
- Los pesos negativos se aceptan actualmente.
- El cierre normal de distribución es contra el total redondeado.
- El último elemento absorbe el residuo y puede quedar negativo.
- Los formatos consultan `CurrentCulture` en cada llamada.

La API nueva puede ser más estricta. La fachada legacy debe adaptar, normalizar o diferir errores para preservar el contrato caracterizado.

## 7. Restricciones no negociables

1. La extracción básica no corrige resultados contables.
2. Fase N0 se entrega y aprueba antes de mover la implementación.
3. La fachada conserva toda la API legacy, no solo los tres tipos acoplados.
4. `SOPRO.Calculation` no referencia ningún proyecto SOPRO.
5. Los contratos del paquete son inmutables.
6. El formateo nuevo exige cultura explícita o queda fuera del paquete.
7. WinForms no referencia directamente el paquete.
8. No se introduce cálculo alternativo en adaptadores.
9. Un módulo avanzado no se incorpora al paquete hasta eliminar sus implementaciones paralelas.
10. Una corrección funcional requiere su propio caso dorado y cambio documentado.
11. Ningún pipeline publica fuera de fuentes privadas sin aprobación explícita.

## 8. Resumen de fases

| Fase | Resultado | Dependencia | Esfuerzo relativo |
|---|---|---|---:|
| N0 | Baseline, goldens y decisiones de compatibilidad | Ninguna | M |
| N1 | `SOPRO.Calculation` con motor base | N0 | M |
| N2 | Fachada legacy completa | N1 | M |
| N3 | Frontera inicial de casos de uso | N0 | L |
| N4 | Contexto por operación y persistencia encapsulada | N3 | L |
| N5 | Consumidores migrados al motor y casos de uso | N2-N4 | XL |
| N6 | Paquete preview validado en fuente local o privada | N2 y gates de paquete | M |
| N7 | Módulos avanzados extraídos individualmente | N5 | XL |

N3 y N4 pueden avanzar en paralelo con N1 y N2 siempre que no alteren los mismos comportamientos sin pruebas compartidas.

## 9. Fase N0: Congelar comportamiento

### Acciones

1. Confirmar y conservar el descubrimiento de los 63 tests actuales.
2. Crear un manifiesto de escenarios dorados.
3. Congelar el escenario sintético oficial.
4. Congelar el proyecto real `***REMOVED***.db` mediante snapshot canónico.
5. Caracterizar los dos constructores, 15 métodos y `DesglosePrecios`.
6. Añadir pruebas directas para `RecalculoGlobalService` y `PricePropagationService`.
7. Caracterizar preview con conceptos y preview de referencia con y sin `Proyecto`.
8. Caracterizar APU, FSR, Costo Horario e Indirectos antes de centralizarlos.
9. Registrar divergencias en una tabla de decisiones.
10. Fijar cultura, reloj, orden y precisión en los fixtures.

### Casos mínimos del motor

- Precisiones 0, 1, 2, 4 y 28.
- Precisión negativa y mayor de 28.
- Midpoints positivos y negativos.
- Cero, negativos y overflow decimal.
- Null y colecciones vacías.
- Pesos cero, negativos y mezclados.
- Residuo positivo y negativo.
- Modo acumulable, `SobreCD`, casing distinto, null y desconocido.
- Indirectos central/campo con importes que divergen al redondear por separado.
- Culturas `es-MX`, `en-US` e invariant.

### Gate N0

- Todos los tests existentes pasan.
- No existen fallos sin clasificar.
- Los goldens oficial y real están revisados.
- Las divergencias conocidas tienen propietario y decisión temporal.
- Diez ejecuciones consecutivas del conjunto crítico producen los mismos resultados.
- N0 se integra en un PR independiente.

## 10. Fase N1: Crear `SOPRO.Calculation`

### Estructura inicial

```text
SOPRO.Calculation/
├── CalculationPrecision.cs
├── SoproCalculationEngine.cs
├── DirectCostLine.cs
├── Pricing/
│   ├── PricePercentageInput.cs
│   ├── PercentageCalculationMode.cs
│   ├── PriceBreakdown.cs
│   └── UnitPriceCalculator.cs
├── Distribution/
│   └── AmountDistributor.cs
├── README.md
└── SOPRO.Calculation.csproj
```

La superficie `public` de C# debe mantenerse pequeña. Helpers e implementaciones auxiliares serán `internal` salvo que exista un caso de uso autorizado concreto.

### Contratos

```text
Proyecto                -> CalculationPrecision
BudgetPercentageInput   -> PricePercentageInput
"SobreCD"               -> PercentageCalculationMode
ConceptoPresupuesto     -> DirectCostLine
DesglosePrecios         -> PriceBreakdown
```

`DirectCostLine.HasMatrix` debe mapear exactamente `MatrizId.HasValue` durante compatibilidad, no la navegación cargada ni un ID mayor que cero.

### Gate N1

- Compila copiando solo la carpeta del proyecto.
- Cero tipos SOPRO en la API pública.
- Cero dependencias runtime.
- Pruebas unitarias y diferenciales exactas.
- Consumidor fuera de la solución compila y ejecuta desde una fuente local o privada.
- Objetivo de cobertura: 95% líneas y 90% ramas del núcleo.

## 11. Fase N2: Fachada compatible

### Acciones

1. Mantener `MotorCalculoSopro` en el assembly y namespace actuales.
2. Delegar las operaciones numéricas al motor nuevo.
3. Mantener normalizaciones y fallbacks legacy en el adaptador.
4. Reconstruir `DesglosePrecios` desde `PriceBreakdown`.
5. Mantener los cuatro métodos de formato con cultura actual.
6. Mantener temporalmente las propiedades internas de precisión.
7. Crear pruebas por reflexión de la API pública.
8. Crear pruebas diferenciales legacy, motor nuevo y fachada.

### Gate N2

- Los callers actuales compilan sin cambios masivos.
- Los nombres de parámetros y retornos se conservan.
- No hay diferencias en casos aceptados.
- Los defectos preservados están documentados.
- La fachada no contiene una segunda cascada de cálculo.

### Resultado de la implementación (PR N2)

- `MotorCalculoSopro` conserva namespace, assembly, sellado, ambos constructores y los 15 métodos públicos (verificado por reflexión en `MotorFacadeN2Tests`).
- Los resultados aritméticos delegan en `SoproCalculationEngine`; en la fachada solo viven: formato con cultura actual (N0 fila 9), guards/null y forma concreta de colecciones legacy (N0 filas 7, 11, 12, 14), el mapeo `"SobreCD"` → `OverDirectCost` (N0 fila 6) y el mapeo `ConceptoPresupuesto` → `DirectCostLine` (`HasMatrix == MatrizId.HasValue`).
- `DesglosePrecios` se reconstruye desde `PriceBreakdown` sin cambios en su contrato público.
- Excepciones preservadas: `ArgumentNullException` con `ParamName` `"pct"` y `"proyecto"`; `OverflowException` y `ArgumentOutOfRangeException` (precisión > 28) propagadas desde el paquete (N1 filas 8 y 10 de la tabla N0).
- Defectos preservados y blindados con goldens: residuo de distribución en el último periodo (puede quedar negativo), `MatrizId = 0` participa en `SumarCostoDirecto`, agrupadores omitidos, excepciones de precisión excesiva.
- El oráculo independiente (`LegacyOracleGoldenTests`) es el guardián: las comparaciones legacy-vs-paquete son tautológicas por diseño y el golden congelado detecta cualquier deriva del mapeo o la aritmética.

## 12. Fase N3: Frontera de Application

### Contratos iniciales

```text
ProjectRef
ProjectSessionInfo
Result<T>
AppError
AppErrorCode
OperationProgress
```

### Reglas de caso de uso

- Recibe request inmutable.
- Devuelve result o error tipado.
- Acepta `CancellationToken` si puede bloquear o tardar.
- Acepta progreso en operaciones largas.
- No devuelve entidades rastreadas.
- No expone `DbContext`, `DbSet`, `IQueryable` o expresiones EF.
- Define si es consulta o comando.
- Define su frontera transaccional.

### Primer slice

```text
ListMaterials
SaveMaterial
DeleteMaterial
PreviewMaterialDeletion
```

WinForms debe adoptar primero estos casos de uso. Solo después se construye la vista WPF correspondiente.

### Gate N3

- `FormCatalogoMateriales` no calcula ni persiste directamente.
- La UI recibe DTOs y claves de negocio.
- Validaciones y errores se pueden probar sin crear formularios.
- Guardado y propagación son una operación lógica única.

### Resultado de la implementación (PR N3 — slice materiales)

- Contratos en `SOPRO.Application/Contracts`: `ProjectRef`, `ProjectSessionInfo`, `Result<T>` (con `Error` tipado y anotación `MemberNotNullWhen` para flujo de nulabilidad), `AppError`, `AppErrorCode`, `OperationProgress`, `WorkspacePaths`.
- Casos de uso en `SOPRO.Application/UseCases/Materials`: `ListMaterials`, `SaveMaterial`, `DeleteMaterial`, `PreviewMaterialDeletion`, `FindMaterialByKey` (autocompletado por clave headless, solo en alcance del proyecto). Request inmutables; nunca devuelven entidades rastreadas (`AsNoTracking` y proyección a DTOs).
- `ProjectSessionInfo` no expone `SOPROContext` en su superficie pública: lo resuelve interno a partir de `DatabasePath` (`Create`); el puente legacy `LegacySessionBridge.FromLegacy(SOPROContext, proyectoId)` queda fuera de la superficie pública de la sesión (N4 sustituirá el ciclo de vida del contexto).
- Correcciones del dictamen NO-GO (revisión `4b9b7b5`):
  - Guardado y eliminación con transacción única (`BeginTransactionAsync` + `CommitAsync`): persistencia + propagación del recálculo son una operación lógica atómica; ante fallo o cancelación hay rollback completo (`SaveMaterial.GuardarEnProyectoAsync`, `DeleteMaterial`).
  - Todos los comandos validan el alcance de la sesión (`MaterialScope.IsInSessionScope`): operar sobre un material de otro proyecto devuelve `NotFound`; el request debe corresponder al proyecto de la sesión.
  - La propagación headless (`PricePropagationService.ActualizarConceptos`) también actualiza `PrecioUnitario` y `ImporteTotal` del concepto (paridad con `FormPresupuesto.RefrescarPreciosDesdeDB`).
  - `SaveMaterial.SaveToMaster` escribe en la base del catálogo maestro (`CatalogoMaestro.db` según `WorkspacePaths`, inyectable por prueba), no en la base del proyecto; la fila maestra queda con `ProyectoId = null` y `Origen = Maestro`.
  - `KeyValidationService` corrige el chequeo de clave única al editar (los `if` que saltaban el tipo excluido eliminados; self-exclusión por Id).
  - Cargas de catálogo serializadas (`SemaphoreSlim` + `CancellationTokenSource` por carga) y eliminación con `materialId` capturado antes del primer `await` (sin TOCTOU en `FormCatalogoMateriales.CargaCrud`).
  - Exportación PDF con `MaterialListItem` y `MaterialCatalogExportResolver` en Application (el generador de MigraDoc solo renderiza); ancho y formato de columnas persistidos vía `CatalogColumnLayoutService` (Application), no con EF en los formularios.
  - Errores de persistencia tipados como `AppErrorCode.Database` (las excepciones EF/SQLite no escapan).
- Correcciones del dictamen NO-GO (revisión `8d06aa0`, fixes pendientes de revisión):
  - `Result<T>` es una clase sellada con `Value` nullable (`T?`): no existe un estado "inválido" por defecto ni un estado de error sin mensaje; los consumidores deben comprobar `IsSuccess` antes de leer `Value`/`Error`. `Ok(null)` está permitido cuando el tipo lo admite (consultas que distinguen "no encontrado" de error, p. ej. `FindMaterialByKey`).
  - `ProjectRef` transporta los metadatos del encabezado (Nombre, Descripcion, Ubicacion, Convocante, Contratista, ApoderadoLegal, FechaInicio, FechaTermino, PlazoEjecucion) vía `ProjectRef.FromEntity`; el PDF (`FormCatalogoMateriales.ReportesPdf`) reconstruye el `Proyecto` para el renderizado sin abrir contexto ni adivinar campos.
  - Identidad de la fila maestra por `Material.MaterialMaestroId` (lo escribe `FormImportarMaestro`): al guardar "en maestro" desde un proyecto se edita la fila maestra asociada del material de origen; sin fila asociada se crea una fila nueva. Los Ids no se comparten entre bases: nunca se sobrescribe un registro maestro por colisión numérica de Ids (test de regresión con Id colisionado).
  - `MaterialScope` aísla únicamente por `ProyectoId`: las copias importadas del maestro viven en el proyecto (`ProyectoId = proyecto`, `Origen = Maestro`) y son editables/usables desde él; los maestros puros solo desde la sesión del catálogo maestro.
  - Unicidad de clave en el catálogo maestro case-insensitive sobre los datos almacenados (`m.Clave.ToUpper() == claveNormalizada`; SQLite compara en binario) y con self-exclusión al editar.
  - `ChangeTracker.Clear()` en los handlers de excepción/cancelación de guardado y eliminación: una operación fallida no deja entidades mutadas en el contexto compartido que una operación posterior persista (test de regresión).
  - `FindMatricesUsingMaterial` y `PreviewMaterialDeletion` restringen las matrices al proyecto de la sesión (`m.ProyectoId == session.Project.ProjectId`): una colisión numérica de Ids no cruza proyectos.
  - "Dónde se usa" headless: `MenuContextual` usa `FindMatricesUsingMaterial` con DTOs (`MaterialUsageInMatrix`), sin contexto legacy.
  - `PricePropagationService.ActualizarAgrupadores` recalcula también los agrupadores sin hijos a cero (paridad `RecalculoGlobalService`): una jerarquía vaciada no conserva totales obsoletos.
  - El selector de insumos (`FormSeleccionarInsumo.Cierre`) ignora el Id devuelto cuando el guardado fue en el catálogo maestro (`UltimoGuardadoEnMaestro`): el Id maestro no existe en el proyecto y no debe compararse contra sus filas.
- Correcciones del dictamen NO-GO (revisión `8a8d1b0`, fixes pendientes de revisión):
  - Los agrupadores del presupuesto se recalculan con la jerarquía que infiere la UI (Orden/Nivel, paridad `BudgetHierarchyService.CalculateAggregatorTotal`), NO por `PadreId`: la persistencia legacy (`FormPresupuesto.Persistencia`) nunca establece `PadreId`, así que los agrupadores reales del fixture dorado pasaban a cero. Cada agrupador suma las hojas de su bloque (hasta la primera fila con `Nivel` menor o igual al suyo); sin hojas → cero.
  - La propagación de precios y la eliminación nunca cruzan proyectos: `PricePropagationService` (incluidos los auxiliares) filtra por `Matriz.ProyectoId == proyecto de la sesión`, y `CatalogItemService` propaga con el `ProyectoId` de la fila guardada. Un material de P1 referenciado por una matriz de P2 ya no recalcula ni elimina nada de P2.
  - `DeleteMaterial` rechaza con `Conflict` (sin tocar nada, rollback) cuando el material está referenciado por matrices de otro proyecto (FK `MaterialId` es `Restrict`: eliminarlo sería imposible sin borrar datos ajenos).
  - `PreviewMaterialDeletion` reporta solo el alcance real (proyecto de la sesión) y advierte con `CrossProjectReferenceCount` las referencias externas que bloquean la eliminación; la UI lo muestra antes de confirmar.
  - Al crear una fila maestra nueva desde un material de proyecto se persiste la asociación (`Material.MaterialMaestroId`): dos guardados con claves distintas editan la MISMA fila maestra (resuelve la pregunta abierta del dictamen; probado).
- Correcciones del dictamen NO-GO (revisión `c3f990b`, fixes pendientes de revisión):
  - Total de agrupador ÚNICO y con paridad exacta de la UI (`GridTotales.cs:313-314`): suma del `Importe` de las hojas del bloque, asignada IGUAL a `CostoDirectoTotal` e `ImporteTotal` (la pantalla muestra `CostoDirectoTotal` vía `BudgetLoadService`). Ya no se suman CD e Importe por separado (divergía: ***REMOVED*** → ***REMOVED***/***REMOVED***).
  - Nivel semántico de una hoja: toda fila no agrupadora es un concepto de nivel 5 ("Concepto"), sea cual sea su `Nivel` almacenado: una hoja legacy con Nivel 1 no corta el bloque de un subcapítulo de nivel 1 (el test ya no fuerza `Nivel = 5`).
  - La propagación filtra los conceptos por el proyecto de las matrices afectadas (`ConceptoPresupuesto.ProyectoId`): un concepto de P2 asociado a una matriz de P1 ya no se recalcula (ni se pone en cero) al guardar/eliminar desde P1.
  - La asociación maestra es reintentable: la fila maestra creada se revierte (compensación) si la escritura local de `MaterialMaestroId` falla (dos bases → dos commits; SQLite no admite transacciones distribuidas). La operación queda sin efectos visibles: reintentar funciona en lugar de devolver `Conflict` por una fila huérfana (probado con trigger de fallo).
- `FormCatalogoMateriales` y `FormEditarMaterial` consumen los casos de uso; el formulario no calcula ni persiste (el Grid solo lee DTOs ya recalculados por el motor en el guardado). Formato de columnas, decimales de importe y título del PDF provienen de la sesión neutral.
- Buscar/eliminar no mutan: previews y consultas con `AsNoTracking`.
- Validaciones (campos requeridos, clave única en alcance del proyecto) y errores tipados viven en `SaveMaterial`: probados sin formularios en `MaterialsUseCasesTests` (46 pruebas, SQLite real del mismo esquema), incluyendo rollback ante fallo de propagación (overflow), cancelación sin persistencia parcial, reapertura SQLite verificando `PrecioUnitario`/`ImporteTotal`, aislamiento entre proyectos y catálogo maestro, clave duplicada al editar, identidad maestra por `MaterialMaestroId` (colisión de Ids entre bases, edición de la fila maestra de un importado, reutilización de la misma fila maestra en guardados consecutivos, compensación si la asociación falla con reintento exitoso), unicidad de clave maestra sin importar capitalización (exacta y mixta), limpieza del tracker tras rollback, agrupadores por Orden/Nivel (total único desde Importe en ambos campos, hoja con Nivel legacy sin cortar el bloque, agrupador vacío en cero, agrupadores legacy con totales previos), propagación y eliminación que no cruzan proyectos (Save/Delete/Preview con material de P1 en matriz de P2, y concepto de P2 sobre matriz de P1), cancelación antes del commit (durante SaveChangesAsync, entre SaveChanges y Commit, compensación del maestro ANTES de la asociación y operación completa DESPUÉS, rollback de eliminación) y contexto por operación (la fábrica se invoca una vez por comando), y resolución de valores de exportación con `MaterialListItem`.

## 13. Fase N4: Persistencia y ciclo de vida

### PR N4-1 (frontera de sesión + cancelación + servicios de búsqueda) — integrado; Gate N4 cerrado

- `ProjectSessionInfo` es datos puros: ya no posee ni expone `SOPROContext` (ni es `IDisposable`); la identidad del proyecto viaja como `ProjectRef` resuelto por el puente legacy (`LegacySessionBridge.FromLegacy`). `grep "session\.Context"` en `SOPRO.Application` → 0.
- Puerto `IProjectDbContextFactory` (transient por uso, decisión confirmada) con implementación `ProjectDbContextFactory` en `SOPRO.Data/Factories`. El puerto vive en Data (junto al tipo que produce): ubicarlo en Application exigiría que Data referencie Application (ciclo; hoy Application → Data). Desviación técnica documentada del plan.
- Los 6 casos de uso de materiales construyen y liberan su contexto dentro del método (`await using var context = await _factory.CreateAsync(...)`) y comprueban `ThrowIfCancellationRequested()` después de cada paso de I/O (FindAsync, ToListAsync, AnyAsync, SaveChangesAsync, BeginTransactionAsync, antes de CommitAsync).
- `CatalogSearchService` e `InsumoSearchService` migrados a la fábrica (ya no construyen `SOPROContext`); sus constructores reciben la fábrica inyectada (WinForms la compone al instanciar los servicios).
- Cancelación del flujo maestro: la compensación de N3 sigue ejecutándose cuando la cancelación llega ANTES del segundo `SaveChangesAsync` (la compensación guarda sin token: debe completarse aunque la operación se haya cancelado); cuando llega DESPUÉS, la operación ya quedó completa y devuelve éxito.
- El catch de `OperationCanceledException` ya no limpia `ChangeTracker` (no hay estado compartido: el contexto es por operación y se descarta con `await using`); se eliminó el `using System.Transactions` sobrante de `SaveMaterial`.
- Tests N4-1 (6 nuevos, deterministas vía `SaveHookContext`/`TestDbContextFactory` en `MaterialsUseCasesTests`): cancelación durante SaveChangesAsync (rollback), entre SaveChanges y Commit (rollback), compensación del maestro antes de la asociación (0 filas maestras, asociación nula), operación completa después de la asociación, rollback de `DeleteMaterial` y una invocación de la fábrica por comando.
- Pendiente para PRs N4 posteriores (orden del plan): `ExternalMatrixImportService`/`ExternalInsumoImportService` (contexto externo), `MatrixConsolidationService`, `SchemaManager`; prohibición de `Task.Run` con contexto (8 ocurrencias en `FormProgramaObra.*`/`FormProyecto.Formato.cs`); snapshots para cálculo en segundo plano; pruebas de hilo con contador de accesos concurrentes.

### PR N4-2 (ciclo de vida + bloqueo de workspace + hallazgo 1) — integrado; Gate N4 cerrado

- Hallazgo 1 del dictamen N4-1: documentado en los 6 casos de uso — el `catch (OperationCanceledException)` no filtra con `when (cancellationToken.IsCancellationRequested)`: cualquier OCE interrumpe por diseño (la cancelación es un abandono, no un fallo recuperable); el contexto es por operación, no hay estado que limpiar.
- `ProjectLifecycleService` migrado: el constructor recibe `IProjectDbContextFactory` opcional (por defecto `ProjectDbContextFactory`); `CreateProject`/`OpenProject` ya no construyen `SOPROContext` directamente. `CloseProjectSession` ya no accede a `session.Context`: la sesión es la dueña de su contexto y de su candado y se cierra con `Dispose` (`grep session\.Context` en Application → 0).
- `ProjectSession` ahora es `IDisposable` (dispone el contexto y el candado de workspace); `Dispose` es idempotente (doble cierre sin efecto).
- `IWorkspaceLock` (decisión confirmada: archivo `.lock` + `FileShare.None`): el candado se adquiere ANTES de tocar la base (crear o abrir) y se libera al cerrar la sesión; si la apertura falla, el candado se libera en el `catch` (no queda retenido). El candado es el handle abierto: un `.lock` sobrante de una sesión abortada no bloquea. `WorkspaceLockedException` tipada con la ruta del archivo; en WinForms `FormPrincipal` ya la muestra en MessageBox (los call sites existentes capturan `Exception`).
- Tests (7 nuevos, `WorkspaceLockTests`): creación del archivo junto a la base, segunda adquisición rechazada (mensaje con ruta), liberación al disponer (archivo eliminado, re-adquisición posible), doble dispose idempotente, `OpenProject` mientras otra sesión abierta → rechazada y reabre al cerrar, apertura fallida sin candado retenido y cierre con sesión nula. El conflicto lo impone el SO (FileShare.None), por lo que son deterministas sin procesos auxiliares.
- Deuda observada (fuera de alcance N4-2, backlog): `BtnNuevoProyecto_Click` sobrescribe `_currentSession` sin cerrar la sesión anterior (fuga preexistente del contexto; con el candado solo se nota al crear mientras otra está abierta); migración de `ExternalMatrixImportService`/`ExternalInsumoImportService`/`MatrixConsolidationService` (contexto externo) y `SchemaManager`; dependencia `_context` residual en `FormSeleccionarInsumo.Cierre.cs:54`; `async void` en 9 handlers; alinear los 26 `ChangeTracker.Clear()` de tests al modelo de fábrica.

### PR N4-3 (contexto externo + fuga de sesión) — integrado; Gate N4 cerrado

- `ExternalMatrixImportService`: constructor recibe `IProjectDbContextFactory` opcional (default `ProjectDbContextFactory`); los 3 contextos externos (`LoadExternalMatrices`, `BuildPreview`, `ImportMatrixTree`) se abren por operación con la fábrica. El contexto del proyecto DESTINO sigue llegando por parámetro (lo posee el formulario legacy; se migrará cuando la UI salga del modelo de contexto compartido).
- `ExternalInsumoImportService`: mismo patrón (2 contextos externos) y pasa la fábrica al `ExternalMatrixImportService` interno (una sola fábrica por composición). Las lecturas SQL directas de herramientas (`SqliteConnection` manual) no construyen `SOPROContext` y quedan fuera de la regla.
- `MatrixConsolidationService` y `SchemaManager`: NO construyen contextos (reciben `SOPROContext` por parámetro); la referencia del dictamen N4-2 a `MatrixConsolidationService.cs:44` era `BeginTransaction`, no `new SOPROContext` — sin cambios.
- `grep "new SOPROContext"` en `SOPRO.Application` → 0 (regla: ningún servicio de Application construye contextos; todos los reciben inyectados o abren por fábrica).
- WinForms: `FormPresupuesto`, `FormSeleccionarAPU`, `FormSeleccionarInsumo` componen `new ExternalMatrixImportService(new ProjectDbContextFactory())` / `new ExternalInsumoImportService(new ProjectDbContextFactory())` en el inicializador de campo.
- Fuga de sesión corregida en `FormPrincipal` (hallazgo 1 del dictamen N4-2): `OpenProjectPath` y `BtnNuevoProyecto_Click` cierran la sesión previa ANTES de abrir/crear (nunca sobrescriben una sesión con candado retenido; también cubre el fallo del ctor de `FormProyecto` que dejaba la sesión y el candado retenidos para siempre). `CloseCurrentSession` ya no llama `GC.Collect`/`WaitForPendingFinalizers` (innecesarios con `IDisposable` y ocultaban errores de disposición).
- Tests: la cobertura existente `ExternalImportPrecisionTests` ejercita ahora el camino de fábrica (los servicios se construyen sin argumentos → fábrica por defecto con DBs reales en temp). 250/250 (no se añadieron tests nuevos: el cambio es mecánico y queda cubierto por la suite de importación).

### PR N4-4 (bootstrap y catálogo maestro a la fábrica) — integrado; Gate N4 cerrado

- `FormCatalogoMaestro` y `FormImportarMaestro`: eliminan el literal duplicado de la ruta del maestro y usan `WorkspacePaths.MasterDatabasePath` (Application) — la misma fuente que usan los casos de uso N3; el contexto del maestro se abre con `ProjectDbContextFactory`.
- `FormPresupuesto`: campo `_factory` compartido; el preview de APU externo del autocompletado abre su contexto de solo lectura por operación con la fábrica (`using var`).
- `DatabaseInitializer.cs` ELIMINADO: era código muerto (cero referencias fuera de la propia clase; la inicialización real del maestro la hace `FormCatalogoMaestro.InicializarMasterContext`). Eliminarlo quitó 2 `new SOPROContext` del árbol sin tocar comportamiento.
- Decisión documentada (desviación del plan propuesto por el dictamen): NO se añadieron `CreateMasterAsync()`/`CreateReadOnlyAsync()` a la fábrica — `Create` ya es la única forma de construir contextos (mismo archivo SQLite, mismo esquema); la semántica de maestro/solo-lectura es del sitio que abre, no del constructor. El objetivo del dictamen (migrar esos 4 sitios a la fábrica) se cumple con la API existente.
- `grep "new SOPROContext"` en WinForms → 0 (en todo el repo solo queda el constructor de `SOPROContext` y el contexto legacy por formulario, que es el modelo que N5 retirará).
- Verificación: 250/250, Release 0 errores, `git diff --check` limpio.

### PR N4-5 (cero DbContext en `Task.Run` — cierre del Gate N4) — integrado; Gate N4 cerrado

- Acción 3 del plan (prohibir el contexto compartido dentro de `Task.Run`) aplicada en las 7 ocurrencias confirmadas de `FormProgramaObra.*`/`FormProyecto.Formato.cs` (el plan anotó 8; el recuento real tras N4-1..N4-4 es 7):
  - `FormProgramaObra.Acciones.cs`: sincronizar desde presupuesto, recalcular, actualizar calendario y cambiar tipo de periodo.
  - `FormProgramaObra.Dependencias.cs`: regeneración fire-and-forget tras guardar dependencias.
  - `FormProgramaObra.EdicionActividades.cs`: recálculo + redistribución tras editar celda.
  - `FormProyecto.Formato.cs`: recálculo global (ribbon/F9).
- Patrón N4-5 por bloque: la ruta de la base se captura ANTES en el hilo de UI (`_context.DatabasePath`); dentro del `Task.Run` la operación abre SU propio contexto transient con la fábrica (`using var ctx = _factory.Create(dbPath)`), lo usa y lo descarta ahí mismo. El contexto de sesión queda exclusivamente en el hilo de UI; el candado de hilo es estructural (cada instancia es usada desde un solo hilo), no de coordinación.
- `RegenerarPeriodosYDistribuciones` dejó de cerrar sobre `_context`: ahora recibe el contexto por parámetro (las 6 referencias — 5 call sites + la definición — están dentro de bloques `Task.Run`).
- El contador de accesos concurrentes pedido por el plan vive en `SOPRO.Tests/Services/Threading/DbContextThreadAccessGuardTests.cs`: `ThreadAccessGuardInterceptor` (interceptor de comandos EF, conectado vía `OnConfiguring` sin tocar producción) cuenta, por instancia, si dos comandos se ejecutan a la vez desde hilos distintos; un gate (`ManualResetEventSlim`) detiene el primer comando para forzar la superposición de forma determinista.
- Tests (3 nuevos, deterministas): contexto compartido usado desde dos hilos → EF Core lo rechaza (`InvalidOperationException` "second operation…", patrón prohibido); operación de fondo con contexto propio mientras la UI lee el compartido → 0 violaciones en el contexto de sesión con la superposición forzada (el contexto propio se descarta dentro del bloque y no es asertable); carga de trabajo real (recalcular + regenerar periodos + distribuir en segundo plano, forma de `btnRecalcular`) → completa, 0 violaciones y resultados persistidos verificados con lectura fresca.
- Acción 4 del plan (snapshots antes del cálculo CPU): satisfecha parcialmente por diseño — los servicios de programación son lecto-escritura mezclada (leer → calcular → `SaveChanges`); partirlos en "cargar snapshot puro → calcular puro → persistir" es la migración de servicios de N5 (empezando por `BudgetPricingService`). N4-5 elimina el riesgo del Gate (contexto compartido en hilos) sin reescribir los servicios; el refresco de la UI tras la operación ya recarga con lectura fresca (`CargarPrograma`), por lo que no queda estado compartido entre hilos.
- Riesgo residual documentado (preexistente y estrictamente menor que antes): dos ESCRITORES distintos sobre el mismo archivo SQLite en la misma ventana temporal (p. ej. edición de grid durante la regeneración fire-and-forget) pueden chocar con `SQLITE_BUSY`; `Microsoft.Data.Sqlite` espera hasta 30 s por defecto y el formulario muestra el error tipado. Antes del cambio el choque era peor: la MISMA instancia desde dos hilos (EF la rechaza).
- Verificación: 253/253 (250 + 3 nuevos), Release 0 errores, `git diff --check` limpio.

1. Sustituir `ProjectSession.Context` por información neutral de sesión.
2. Crear un contexto por consulta o comando.
3. Prohibir el contexto compartido dentro de `Task.Run`.
4. Cargar snapshots antes de ejecutar cálculo CPU en segundo plano.
5. Persistir resultados en una transacción al finalizar.
6. Añadir cancelación antes del commit.
7. Introducir bloqueo de workspace para un solo escritor.
8. Mover implementaciones que usan EF, SQLite, filesystem o ClosedXML fuera de Application.
9. Eliminar gradualmente `Application -> Data`.
10. Añadir `Data -> Application` para implementar los puertos.

No se sustituirá `IRepository<T>` por otro repositorio genérico. Los nuevos puertos serán específicos del caso de uso.

### Gate N4

- Cero `DbContext` usado desde múltiples hilos.
- Un commit definido por comando.
- Rollback comprobado ante fallo o cancelación.
- Consultas y previews no modifican la base.
- Application no crea contextos directamente en los módulos migrados.

## 14. Fase N5: Migrar consumidores

### Orden recomendado

1. `BudgetPricingService`
2. `MatrixComponentCalculationService`
3. `BudgetPreviewCalculationService`
4. `BudgetConceptAssignmentService`
5. `BudgetRowEditFlowService`
6. `BudgetLoadService`
7. `UtilidadCalculationService`
8. `MatrixComponentEditingService`
9. `MatrixCostAdjustmentService`
10. `MatrixApplicationService`
11. `PricePropagationService`
12. Imports externos
13. Programación y Curva S
14. Explosión y programa de insumos
15. `RecalculoGlobalService`
16. Financiamiento
17. WinForms y reporting

`RecalculoGlobalService` se migra al final de sus dependencias porque coordina matrices, presupuesto y programación.

### Estado ejecutivo de N5 (17 grupos del Orden recomendado)

La numeración `N5-1`…`N5-20` corresponde a PRs integrados o en revisión; algunos grupos originales se dividieron en varios PR, por lo que el conteo de PRs no equivale al de grupos. Estado actual de la rama `feat/application-n5-20-financiamiento`: **16 completos, 0 parciales, 1 pendiente** (Financiamiento queda completo; resta WinForms/reporting).

| # | Grupo (Orden N5) | Consumidor(es) | Estado | PRs |
|---|---|---|---|---|
| 1 | Presupuesto | `BudgetPricingService` | Completo | N5-1 |
| 2 | Edición matrices | `MatrixComponentCalculationService` | Completo | N5-2 |
| 3 | Preview | `BudgetPreviewCalculationService` | Completo | N5-3 |
| 4 | Asignación concepto | `BudgetConceptAssignmentService` | Completo | N5-4 |
| 5 | Edición de fila | `BudgetRowEditFlowService` | Completo | N5-5 |
| 6 | Carga presupuesto | `BudgetLoadService` | Completo | N5-6 |
| 7 | Utilidad | `UtilidadCalculationService` | Completo | N5-7 |
| 8 | Edición componente | `MatrixComponentEditingService` | Completo | N5-8 |
| 9 | Ajuste costo | `MatrixCostAdjustmentService` | Completo | N5-9 |
| 10 | Aplicación matriz | `MatrixApplicationService` | Completo | N5-10 |
| 11 | Propagación precio | `PricePropagationService` | Completo | N5-11 |
| 12 | Imports externos | `ExternalMatrixImportService`, `ExternalInsumoImportService` | Completo | N5-12, N5-13 |
| 13 | Programación y Curva S | `ProgramacionCurvaSService`, `ProgramacionDistributionService`, `ProgramacionCalculationService` | Completo | N5-14, N5-15, N5-17 completos |
| 14 | Explosión y programa de insumos | `ProgramacionInsumosService`, `ExplosionInsumosService` | Completo | N5-16, N5-18 completos |
| 15 | Recálculo global | `RecalculoGlobalService` | Completo | N5-19 |
| 16 | Financiamiento | `FinanciamientoCalculationService` | Completo | N5-20 |
| 17 | WinForms y reporting | UI + PDF/Excel | Pendiente | — |

`BudgetLoadService` (N5-6) conserva `MotorCalculoSopro` **solo para `Format*`** (contrato N0 fila 9); no contiene cálculos pendientes. El único consumidor N5 pendiente es WinForms/reporting, que aún construye `MotorCalculoSopro` directamente (~20 ocurrencias). `ExplosionInsumosService` conserva la fachada únicamente como formateador local, sin exponerla en parámetros privados.

### Verificación por consumidor

- Ejecutar legacy y nuevo sobre clones de la misma base.
- Comparar request y response.
- Comparar snapshot SQLite normalizado.
- Cerrar y reabrir la base.
- Comprobar idempotencia cuando aplique.
- Comprobar rollback ante fallo.
- Medir el rendimiento contra el baseline.

### Gate N5

- Cero construcciones directas de `MotorCalculoSopro` en WinForms.
- Cero parámetros privados tipados como motor en producción.
- Cero algoritmos equivalentes en reportes.
- WinForms no referencia `SOPRO.Calculation`.
- Los escenarios oficial y real permanecen verdes.
- La fachada solo se retira según una decisión de compatibilidad con consumidores autorizados.

### PR N5-1 (`BudgetPricingService` — primer consumidor migrado al motor)

- `BudgetPricingService` era el único wrapper que aún pasaba por la fachada (`MotorCalculoSopro`) para su aritmética: `CalculateUnitPrice` (ambas sobrecargas), `MultiplyUsingDisplayPrecision` y `RoundImporte`. El resto (factor, header, letras, porcentajes) son utilidades puras sin delegación.
- Ahora construye directamente `SoproCalculationEngine` (de `SOPRO.Calculation`) con la misma configuración de decimales que usaba la fachada (`Proyecto.Decimales*`, o 2/2/4 sin proyecto), y mapea `BudgetPercentageInput` → `PricePercentageInput` con el mismo criterio legacy ("SobreCD" case-insensitive → `OverDirectCost`; el resto → `Accumulative`). La fachada conserva ese mapeo y sigue siendo el oráculo diferencial contra el paquete.
- Sin cambio de API: los consumidores de WinForms (`FormPresupuesto`, `FormFinanciamiento.FlujoCaja`) no se tocan en este PR; sus construcciones directas de la fachada se migran en los pasos 13-17 del orden.
- Tests (`SOPRO.Tests/Services/Presupuesto/BudgetPricingServiceTests.cs`, 17 nuevos): paridad exacta servicio↔fachada para `CalculateUnitPrice` en ambos modos y 4 combinaciones de decimales, `MultiplyUsingDisplayPrecision` y `RoundImporte` (baterías con casos de redondeo simétrico y extremos); valores dorados fijos (Acumulables 5/5/2/8/3 → PU 1248.11; SobreCD → 1230.00; 652 × 13.3875 → 8730.28; redondeo `AwayFromZero` con 2.005); factor de precios en ambos modos (1.3032030 y 1.28).
- Verificación: 270/270 (253 + 17 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 8,560 casos de paridad servicio↔fachada con semilla fija, 0 discrepancias; Release y Debug 270/270). Hallazgos no bloqueantes anotados:
  - `CalculateFactor` conserva lógica propia (multiplicador sin redondeo, dorado en ambos modos): cuando `SOPRO.Calculation` exponga un `CalculateFactor`, delegar también para consistencia (N5/N6).
  - `BuildEngine` construye un engine por operación (POCO sin estado, costo irrelevante); solo replantear si algún llamador hace bucles masivos por P.U.

### PR N5-2 (`MatrixComponentCalculationService` — segundo consumidor migrado al motor)

- `MatrixComponentCalculationService.Recalculate` construía `new MotorCalculoSopro(decimalesImporte, decimalesImporte, 4)` para sus 5 operaciones (`Multiplicar`, `RedondearImporte`, `CalcularImporteSobreBase`, `SumarImportes`, `RedondearImporte` en totales). Ahora construye `SoproCalculationEngine(decimalesImporte, decimalesImporte, 4)` directo con el mapeo 1:1 (`Multiply`, `RoundAmount`, `CalculateAmountOverBase`, `SumAmounts`). Sin cambio de API ni de algoritmo: mismo orden de pasadas (materiales/maquinaria/auxiliares → base MO normal + cuadrillas → %MO y herramientas → totales).
- Tests: batería de paridad en `SOPRO.Tests/Services/MatrixComponentCalculationParityTests.cs` — 1,000 escenarios aleatorios deterministas (semilla fija) × 5 configuraciones de decimales (0-4) comparando componente a componente y total a total contra una referencia compuesta con las primitivas de la fachada (oráculo diferencial): 0 discrepancias; más 2 dorados nuevos fuera de la configuración estándar (decimales 0 y 3). Los 10 tests existentes de `Recalculate` (dorados a 2 decimales, edge cases, anidados) siguen verdes sin tocar.
- Verificación: 273/273 (270 + 3 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 36,746 comparaciones con semilla 987654321, 7 configs de decimales 0-6 y valores de 4 decimales, 0 fallos; Release y Debug 273/273). Hallazgos no bloqueantes anotados:
  - El rango de la batería del PR es 0-4 decimales con valores de 3 decimales; el barrido del auditor extendió a 0-6 y 4 decimales sin divergencias (margen confirmado).
  - `TotalManoObraResumen` se inicializa en 0 y se reasigna (:80 + :88): patrón heredado del original, inofensivo; se limpia si `MatrixComponentTotals` migra a constructor primario (opcional).

### PR N5-3 (`BudgetPreviewCalculationService` — tercer consumidor migrado al motor)

- Dos sitios migrados de `MotorCalculoSopro` a `SoproCalculationEngine`:
  - `BuildPreview`: `CalcularPrecioUnitario` → `CalculateUnitPrice` con el mapeo `BudgetPercentageInput` → `PricePercentageInput` (helper `BuildEnginePercentages` idéntico al de N5-1); `DesglosePrecios` → `PriceBreakdown` del paquete (`IndirectosCentral` → `CentralIndirectCosts`, `IndirectosCampo` → `FieldIndirectCosts`, `Financiamiento` → `Financing`, `Utilidad` → `Profit`, `CargosAdicionales` → `AdditionalCharges`, `PrecioUnitario` → `UnitPrice`; `Subtotal1..3` conservan el nombre). El mapeo es exacto porque `DesglosePrecios` ya delegaba sus propiedades computadas a `PriceBreakdown`.
  - `BuildPreviewFromReferenceCost`: el redondeador local `R(v)` ahora usa `engine.RoundAmount` (o identidad sin proyecto); la cascada de referencia (porcentajes redondeados por separado) se conserva intacta, incluida la divergencia documentada con la ruta "con conceptos".
- Tests (`SOPRO.Tests/Services/Presupuesto/BudgetPreviewCalculationParityTests.cs`, 3 nuevos): batería con conceptos reales en BD (300 escenarios deterministas con decimales aleatorios 0-6, modos Acumulables/SobreCD/sobrecd/null y `CostoDirectoReferencia` aleatorio) contra referencia compuesta con fachada; batería de la ruta de referencia sin BD (800 escenarios, con y sin proyecto); y caso dorado de `CostoDirectoReferencia` no nulo sin conceptos (1234.567 → CD 1234.57, PU 1540.89) con verificación de la delegación directa.
- Verificación: 276/276 (273 + 3 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 21,000 comparaciones de campo, 2,100 escenarios con semilla 13572468, decimales 0-6/0-7 y porcentajes de 4 decimales, 0 fallos; Release y Debug 276/276). Hallazgos no bloqueantes anotados:
  - La batería del PR usa porcentajes de 2 decimales; el barrido del auditor cubrió 0-7 con 4 decimales sin divergencias (margen, no defecto).
  - `BuildEnginePercentages` ahora existe en 3 sitios (BudgetPricingService, BudgetPreviewCalculationService y la fachada MotorCalculoSopro): consolidar en un solo lugar cuando N5 avance un par de pasos más (o que SOPRO.Calculation exponga el mapeo desde un input tipo BudgetPercentageInput) para evitar el triple mantenimiento del criterio SobreCD. Seguimiento en N5/N6.

### PR N5-4 (`BudgetConceptAssignmentService` — cuarto consumidor migrado al motor)

- `BuildDraftFromConcept` y `BuildDraftFromMatrix` migrados de `MotorCalculoSopro` a `SoproCalculationEngine` (helper `BuildEngine` con los decimales del proyecto): `RedondearImporte` → `RoundAmount` (cdUnit por [FIX-2]) y `Multiplicar` → `Multiply` (cdTotal e importe por [FIX-1]/[FIX-3]/[FIX-4]). El P.U. sigue viniendo de `BudgetPricingService.CalculateUnitPrice` (ya sobre el motor desde N5-1). Sin cambio de API ni de lógica de negocio (claves, confirmaciones, tipos de matriz).
- Tests (`SOPRO.Tests/Services/Presupuesto/BudgetConceptAssignmentParityTests.cs`, 5 nuevos — no existían tests previos para este servicio): batería de paridad de `BuildDraftFromSelectedMatrix` (500 escenarios deterministas, decimales aleatorios 0-6, modos Acumulables/SobreCD/sobrecd/null y porcentajes aleatorios) contra referencia compuesta con fachada; batería del camino "copia desde grid" vía `ResolveByKey` (300 escenarios, sin BD — el draft se compara campo a campo incluido MatrizId); dorados de ambos drafts (13.3875×652 → cdUnit 13.39, PU 16.71, cdTotal 8730.28, importe 10894.92; 100×10 → PU 124.82, cdTotal 1000.00, importe 1248.20); y caso de cantidad inválida → default 1.
- Verificación: 281/281 (276 + 5 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 15,000 comparaciones de campo, 2,500 escenarios con semilla 246813579, decimales 0-6/0-7, porcentajes hasta 40%, 0 fallos; Release y Debug 281/281). Hallazgos no bloqueantes anotados:
  - La consolidación de `BuildEngine` (ahora en N5-1, N5-3, N5-4) y `BuildEnginePercentages` (N5-1, N5-3, fachada) acumula urgencia: unificar en un solo sitio o exponerlo desde SOPRO.Calculation (mismo seguimiento N5/N6).
  - Supuesto de cobertura: la batería de concepto reusa la semántica de la de matriz (comparten aritmética); si `BuildDraftFromConcept` divergiera algún día (p. ej. reusar el P.U. del origen en vez de recalcularlo), la referencia compuesta no lo detectaría por diseño — cobertura actual suficiente.

### PR N5-5 (`BudgetRowEditFlowService` — quinto consumidor migrado al motor)

- `HandleQuantityCellChange` migrado de `MotorCalculoSopro` a `SoproCalculationEngine` (único sitio numérico del servicio): `RedondearCantidad` → `RoundQuantity` (primera aparición de esta operación en N5), `Multiplicar` → `Multiply`, `RedondearImporte` → `RoundAmount` (IVA y total). El P.U. sigue viniendo de `BudgetPricingService.CalculateUnitPrice` (N5-1) con el CD unitario crudo — el motor redondea internamente, igual que antes. `HandleTypeCellChange` no tiene aritmética. Sin cambio de API ni de lógica.
- Tests (`SOPRO.Tests/Services/Presupuesto/BudgetRowEditFlowParityTests.cs`, 5 nuevos — no existía cobertura previa): batería de paridad de 800 escenarios deterministas (decimales 0-6, modos Acumulables/SobreCD/sobrecd/null, IVA 0 o aleatorio, 10% de textos inválidos/vacíos) contra referencia compuesta con fachada — 8 campos por escenario incluidos los textos en letra; dorados con IVA (652×124.82 → importe 81382.64, IVA 13021.22, total 94403.86), sin IVA (total = subtotal), normalización de cantidad visible (652.5555 → 652.56) y textos inválidos/sin matriz → resultado vacío.
- Verificación: 286/286 (281 + 5 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 27,000 comparaciones de campo, 3,000 escenarios con semilla 1123581321, IVA 0-50%, cantidades de 6 decimales contra configs 0-6, 0 fallos; Release y Debug 286/286). Hallazgos no bloqueantes anotados:
  - `Subtotal` siempre es copia de `Importe` y ambos se reportan en `BudgetQuantityChangeResult`: heredado del original, semánticamente redundante pero parte del contrato del DTO — anotado para la limpieza de DTOs en N6.
  - El servicio usa el patrón inline (`new SoproCalculationEngine` en el cuerpo) en vez del helper `BuildEngine` de N5-4; la consolidación futura (ya registrada) unificará todos los sitios por igual.

### PR N5-6 (`BudgetLoadService` — sexto consumidor migrado al motor)

- `BuildRowDisplay` (único sitio numérico del servicio) migra su aritmética a `SoproCalculationEngine`: `CalcularPrecioUnitario` → `CalculateUnitPrice` con `PricePercentageInput` construido inline (mismo criterio de `BuildEnginePercentages`: "SobreCD" case-insensitive → `OverDirectCost`, resto → `Accumulative`; el null → Accumulative igual que el `?? "Acumulables"` previo), `Multiplicar` → `Multiply`, `RedondearImporte` → `RoundAmount` (IVA y total). `BudgetPercentageInput` deja de usarse en el servicio. Mapeo de resultados del breakdown: `Indirectos→IndirectCosts`, `Financiamiento→Financing`, `Utilidad→Profit`, `PrecioUnitario→UnitPrice` (primer consumo directo de los miembros del breakdown; `DesglosePrecios` de la fachada es un record equivalente por construcción). `BuildColumnDefinitions`/`GetTypeText` sin aritmética — intactos.
- DECISIÓN (documentada en el header del servicio): `FormatCantidad`/`FormatImporte`/`FormatPorcentaje` PERMANECEN en `MotorCalculoSopro` — por N0 fila 9 el formato con cultura es rol sancionado de la fachada y el motor no tiene formato por diseño; el motor del paquete no ofrece equivalente y crearlo duplicaría la única vía. La variable `motor` queda exclusivamente para esas llamadas de formato. Se consolida cuando exista un formateador compartido (mismo bucket que la consolidación de helpers).
- Tests (`SOPRO.Tests/Services/Presupuesto/BudgetLoadServiceParityTests.cs`, 5 nuevos — no existía cobertura previa): batería de paridad de 600 escenarios de concepto hoja (decimales cantidad/importe 0-4, porcentaje 0-6, porcentajes de cascada 0-40/0-20, IVA 0-20 con 10% en cero, modos Acumulables/SobreCD/sobrecd/SOBRECD/null) y 200 de agrupador (nivel 0-7) contra referencia compuesta con fachada — las 23 entradas del diccionario `ValuesByInternalName` por escenario; dorados con cultura controlada es-MX (hoja: CD 100, 5/5/2/8/3 → Indirectos 10.00/Financiamiento 2.20/Utilidad 8.98/PU 124.82, importe 1248.20, IVA 199.71, total 1447.91; agrupador: "Nivel 1"/$5,432.10/resto vacío; sin IVA: total = subtotal).
- Verificación: 291/291 (286 + 5 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 3,000 escenarios en es-MX y 3,000 en en-US, 144,000 comparaciones en total, 0 fallos; cobertura extendida a cantidad/importe 0-6, porcentaje 0-7, IVA 0-50%; Debug y Release 291/291). Hallazgos no bloqueantes anotados:
  - El diccionario `ValuesByInternalName` tiene 23 entradas, no 22: la documentación del PR decía 22 pero los tests comparan las 23 correctamente — texto corregido aquí y en el comentario del test.
  - La batería del PR genera precisión cantidad/importe 0-4 y porcentaje 0-6 (no 0-6/0-7 como declaraba el plan); el barrido independiente cubrió el rango declarado sin divergencias — texto del plan corregido al rango real de la batería.
  - El mapeo inline de porcentajes añade un sitio más al bucket de consolidación N5/N6 (BuildEngine/BuildEnginePercentages) ya registrado.

### PR N5-7 (`UtilidadCalculationService` — séptimo consumidor migrado al motor)

- `Calcular` (único sitio numérico) migra sus 5 `RedondearImporte` de `MotorCalculoSopro` a `SoproCalculationEngine.RoundAmount` (baseUtilidad, importeUtilidad, importeIsr, importePtu, utilidadNeta). `MotorCalculoSopro` desaparece por completo de este servicio (no había formato). Los `Math.Round(..., 5, AwayFromZero)` de `porcentajeBruto`/`porcentajeNeto` se conservan por diseño: son coeficientes intermedios con precisión fija 5 (documentado en el header del servicio), no operaciones del motor. `BuildPreview` (N5-3) sigue intacto y compartido. Sin cambios de API.
- Tests (`SOPRO.Tests/Services/Presupuesto/UtilidadCalculationParityTests.cs`, 5 nuevos — se suman a los 2 dorados existentes de `UtilidadCalculationServiceTests`): batería de paridad de 300 escenarios deterministas (mitad con conceptos reales en BD — matriz + 1-4 conceptos — y mitad por costo de referencia; decimales cantidad/importe 0-4 y porcentaje 0-6, porcentajes de cascada 0-40/0-20, utilidad/ISR/PTU con valores negativos ocasionales para ejercitar los `Math.Max(0, ...)`, modos Acumulables/SobreCD/sobrecd/null, asistido/directo) contra referencia compuesta con la fachada — 10 campos por escenario; dorados: directo (CD 1000, 10/5/2 → base 1173.00, bruto 8.00000, neto 4.80000, utilidad 93.84, ISR 28.15, PTU 9.38, neta 56.31), asistido (deseada 6, ISR 20/PTU 5 → bruto 8.00000, neta 70.38), sin conceptos con referencia de 3 decimales (1234.567 → base 1385.19, utilidad 110.82, ISR 33.25, PTU 11.08, neta 66.49) y factor ≤ 0 (ISR 60 + PTU 50 → asistido bruto 0; directo neto −0.80000 heredado sin clamp).
- Verificación: 296/296 (291 + 5 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 1,200 escenarios, 12,000 comparaciones, 0 fallos — incluyó 600 rutas con conceptos persistidos, 600 por referencia, precisiones 0-6/0-7, tasas negativas y 200 casos con ISR+PTU ≥ 100; Debug y Release 296/296). Hallazgos no bloqueantes anotados:
  - El rango real de la batería es cantidad/importe 0-4 y porcentaje 0-6 (el plan declaraba 0-6/0-7); el barrido independiente cubrió el rango declarado sin divergencias — texto corregido.
  - La batería no cubría el branch asistido `factor <= 0` (ISR máx 39, PTU máx 29): se añadió el dorado explícito `Calcular_Dorado_FactorMenorOIgualACero` en el mismo PR (asistido → bruto 0; directo → neto negativo heredado sin clamp).
  - Nota de entorno: durante la auditoría el SDK se actualizó a 9.0.317; bajo ese SDK el build produce 556 warnings (vs ~452 preexistentes) y 0 errores, suites verdes. Los warnings adicionales son del SDK más nuevo, no de este paso.

### PR N5-8 (`MatrixComponentEditingService` — octavo consumidor migrado al motor)

- `UpdateUnitPrice` (único sitio numérico) migra su única operación del motor de `MotorCalculoSopro` a `SoproCalculationEngine.Multiply` (mismo criterio de configuración: `new SoproCalculationEngine(decimalesImporte, decimalesImporte, 4)` — igual que el ctor de la fachada usado antes). `SyncRendimiento` conserva `Math.Round(1/cantidad, 5, AwayFromZero)`: precisión fija 5 por contrato de captura (documentado en el header del servicio), no es operación del motor — mismo patrón que los coeficientes de N5-7. `UpdateDescription`/`UpdateUnit`/`UpdateQuantity`/`UpdateQuantityFromDialog`/`TryParseDecimal` sin aritmética de motor — intactos. Sin cambios de API.
- Tests (`SOPRO.Tests/Services/Matrices/MatrixComponentEditingServiceParityTests.cs`, 6 nuevos — no existía cobertura previa): batería de paridad de 600 escenarios de `UpdateUnitPrice` (decimalesImporte 0-6, cantidades y precios con 3 decimales, 10% de textos con formato `$`/`,` ejercitando el path de parseo — la referencia replica `TryParseDecimal` idéntico, el oráculo diferencial valida Success/ErrorMessage/RequiresRecalculation/Importe/PrecioUnitario) contra referencia compuesta con la fachada; dorados: P.U. visible redondeado antes de multiplicar (13.3875 × R2(124.8225)=124.82 → R2(1671.02775) = 1671.03, con el P.U. crudo 124.8225 conservado en el material), cero decimales (R0(15.49)=15 → 10×15 = 150), no-materiales → Fail sin tocar Importe, texto inválido/negativo/vacío → Fail sin tocar Importe, y `SyncRendimiento` (0.33333 para maquinaria, 0 para el resto).
- Verificación: 302/302 (296 + 6 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 4,500 escenarios, 22,500 comparaciones, 0 fallos — precisiones 0-8, culturas en-US/es-MX/es-ES, 900 textos monetarios válidos y 900 con sufijo inválido; Debug y Release 302/302). Hallazgo no bloqueante anotado y corregido:
  - Los textos "formateados" de la batería generaban `$1,234.50 MXN`: `TryParseDecimal` no elimina el sufijo "MXN", así que esos 60 escenarios fallaban en ambos lados sin comparar Importe/P.U. Corregido en el mismo PR: el texto monetario ahora usa `"$" + N2` con cultura en-US y el texto plano usa InvariantCulture — el path de strip de `$`/`,` queda ejercitado de verdad. No se amplió el parser (alteraría comportamiento legacy).

### PR N5-9 (`MatrixCostAdjustmentService` — noveno consumidor migrado al motor)

- Única operación del motor del servicio: `motor.RedondearCantidad(original * factor)` en `ApplyFactor` → `engine.RoundQuantity`. Los tres motores de instancia (GetAdjustmentBasis, AdjustMatrixByFactor, AdjustMatrixByTargetCost) y los parámetros privados de `ApplyFactor`/`EvaluateCost` se re-tipan de `MotorCalculoSopro` a `SoproCalculationEngine` (privados, sin impacto de API). `SyncRendimiento` y `MatrixComponentCalculationService.Recalculate` (N5-2) compartidos e intactos. Sin cambios de comportamiento observable.
- Tests (`SOPRO.Tests/Services/Matrices/MatrixCostAdjustmentParityTests.cs`, 4 nuevos — se suman a los 5 existentes de `MatrixCostAdjustmentServiceTests`): baterías de paridad contra referencias que replican el flujo completo de los tres métodos públicos con la fachada (oráculo diferencial) — 400 escenarios de `GetAdjustmentBasis`, 300 de `AdjustMatrixByFactor` (factores 0-3, 10% negativos → Fail) y 300 de `AdjustMatrixByTargetCost` (target 30%-170% del costo actual: debajo del mínimo y búsqueda binaria) — matrices aleatorias de 3-8 componentes de los 5 tipos (incluyendo auxiliares cuadrilla/básico y %MO), decimales cantidad 0-4/importe 0-6/porcentaje 0-7, alcances aleatorios de 1-4 rubros; comparación completa de resultados (Success/Message/CurrentCost/TargetCost/AchievedCost/FactorApplied) + `CostoDirecto` + estado profundo de componentes (Cantidad/Importe/Rendimiento por índice ordenado); dorado explícito "ya-en-monto" (target = costo actual → FactorApplied 1, mensaje y estado profundo intactos).
- Verificación: 306/306 (302 + 4 nuevos), Release 0 errores, `git diff --check` limpio.
- Dictamen GO (barrido independiente del auditor: 55,000 comparaciones de RoundQuantity, 0 fallos, precisiones 0-10 y factores positivos/negativos; replay exacto confirmó 41 casos bajo mínimo y 187 búsquedas binarias; Debug y Release 306/306). Hallazgo no bloqueante anotado y corregido:
  - El branch "ya-en-monto" (tolerancia ±0.01) nunca se ejecutó en la batería: el multiplicador continuo aleatorio lo hace extremadamente improbable. Añadido el dorado explícito `AdjustMatrixByTargetCost_Dorado_YaEnMonto` (target = CurrentCost exacto → FactorApplied 1, mensaje "La matriz ya se encuentra en el monto solicitado." y estado profundo intacto). No bloqueaba: RoundQuantity validado directamente y el branch no cambió.

### PR N5-10 (`MatrixApplicationService` — décimo consumidor migrado al motor)

- Única operación del motor del servicio: `RedondearImporte(totals.CostoDirectoTotal)` en la cascada (`PropagateCascadeAsync`) → `SoproCalculationEngine.RoundAmount`. `MapComponent` conserva `Math.Round(1/cantidad, 5, AwayFromZero)` (precisión fija por contrato de captura, no es operación del motor — patrón N5-8). CRUD/consultas EF intactos. Sin cambios de API ni de comportamiento observable.
- Tests (`SOPRO.Tests/Services/Matrices/MatrixApplicationServiceParityTests.cs`, 3 nuevos — no existía cobertura previa): paridad de integración sobre SQLite — `SaveAsync` (edición) dispara la cascada en dos contextos construidos desde los MISMOS valores aleatorios (100 escenarios, decimales importe 0-4, cantidades/precios con 3 decimales; el esperado independiente recalcula B con el CD nuevo de A + RoundAmount, y la referencia replica SaveAsync+PropagateCascadeAsync con la fachada) comparando IsNew/CascadedMatricesUpdated/CostoDirecto de la matriz dependiente/estado de componentes; dorado fijo (A CD 350, B: aux 2×350=700 + mat 3×100=300 → CostoDirecto 1000.00, cascadas=1); sin dependientes → 0 cascadas. Nota: un primer intento falló porque los dos `CrearEscenario` consumían valores aleatorios distintos — corregido extrayendo `Valores` una sola vez por iteración.
- Verificación: 309/309 (306 + 3 nuevos), Release 0 errores, `git diff --check` limpio.

### PR N5-11 (`PricePropagationService` — undécimo consumidor migrado al motor)

- Seis operaciones del motor en 5 sitios: `Multiplicar` → `Multiply` ×3 (importe de componente con `(dec, dec, 4)`, CostoDirectoTotal e ImporteTotal del concepto), `RedondearImporte` → `RoundAmount` ×2 (CostoDirecto de la matriz en `RecalcularConMotor`, CostoDirectoUnitario del concepto) y `SumarImportes` → `SumAmounts` ×1 (totales de agrupadores). Los 4 `MotorCalculoSopro` de instancia desaparecen; CRUD/consultas EF, el flujo (Propagar → RecalcularConMotor → PropagarAuxiliares recursivo → ActualizarConceptos → ActualizarAgrupadores) y `RecalcularConMotor` público (reutilizado por ExternalMatrixImportService) intactos.
- Tests (`SOPRO.Tests/Services/Precios/PricePropagationServiceParityTests.cs`, 3 nuevos): batería diferencial de 50 escenarios × 4 fases (PropagarMaterial/ManoDeObra/Maquinaria y PropagarEliminacion tras quitar maquinaria) en dos contextos SQLite construidos desde los MISMOS valores (decimales importe 0-4, % indirectos central 0-30, precios/cantidades con 3 decimales) — la referencia replica el flujo completo con primitivas de la fachada y se comparan CostoDirecto de matrices, Importe de componentes y los 4 campos de conceptos + agrupadores; dorado fijo de cascada completa (A 70+40+4=114.00, B aux 2×114=228.00, concepto A 10×114=1140, concepto B 5×228=1140, agrupador 2280.00, PU==CDU con %0); aislamiento por proyecto (lista de ids de dos proyectos + filtro → solo recalcula el del proyecto indicado: 90.00 vs 94.00 intacto).
- Verificación: 312/312 (309 + 3 nuevos), Release 0 errores, `git diff --check` limpio.
- Hallazgos del dictamen N5-11 (corregidos en esta rama): (1) las variables locales ahora llamadas `engine` conservaban el nombre `motor` (PricePropagationService.cs:155 y :203) — renombradas; (2) la batería ejecutaba las fases sin modificar el precio del insumo propagado, dejando escrituras idempotentes tras el setup — ahora cada fase modifica primero el precio (PrecioUnitario/SalarioReal/CostoHorario) en AMBOS contextos con el mismo valor, de modo que ninguna propagación es idempotente y se valida una actualización efectiva.

### PR N5-12 (`ExternalMatrixImportService` — duodécimo consumidor migrado al motor)

- Único motor del servicio: `RedondearImporte` → `RoundAmount` en `RecalcularÁrbol` (post-orden, auxiliares antes que padres) con las tres precisiones del proyecto destino. `RecalcularConMotorDelProyecto` reutiliza la ruta canónica `PricePropagationService.RecalcularConMotor` (N5-11) y el Rendimiento de maquinaria conserva `Math.Round 5` por contrato. Copia de importación/CRUD intactos.
- Tests (`SOPRO.Tests/Services/Importacion/ExternalMatrixImportServiceParityTests.cs`, 3 nuevos; el dorado O2 preexistente 30.03 ya cubría la línea end-to-end): batería de 25 árboles aleatorios de 3 niveles (raíz 2 materiales + auxiliar → hijo 1 material + auxiliar → nieto 1 material) con decimales importe del destino 0-4, oráculo independiente solo con `Math.Round` sobre el Recalculate compartido recorriendo post-orden (valida que el CostoDirecto redondeado del auxiliar es el que consume su padre); dorado con auxiliar (hijo 2×20.00=40.00 → raíz 3×10.01=30.03 + aux 2×40.00=80.00 = **110.03**); dorado matriz sin componentes → 0.00 (no se copia el costo del origen).
- Verificación: 315/315 (312 + 3 nuevos), Release 0 errores, `git diff --check` limpio.
- Hallazgos del dictamen N5-12 (corregidos en `c66a7f4`): `CS8629` en `ExternalMatrixImportServiceParityTests.cs:257` (`comp.AuxiliarId.Value`) → `comp.AuxiliarId!.Value`; referencia de línea rígida `:386` eliminada del header de `ExternalMatrixImportService`.

### PR N5-13 (`ExternalInsumoImportService` — decimotercer consumidor migrado al motor)

- Tres operaciones `Multiplicar` → `Multiply` (Importe del componente importado de Material/Maquinaria/Auxiliar) con la precisión del proyecto destino (o `2/2/4` sin proyecto). El auxiliar reutiliza `ImportMatrixTree` (N5-12) para obtener el CostoDirecto redondeado del básico; `Rendimiento` conserva `Math.Round 5`.
- Tests (`SOPRO.Tests/Services/Importacion/ExternalInsumoImportServiceParityTests.cs`, 2 nuevos; O1 preexistente 20.02 ya cubría Material/Maquinaria): batería de 35 iteraciones × 3 insumos con decimales importe del destino 0-4, oráculo independiente `Math.Round(RoundAmount(precio) × cantidad)` sin engine ni fachada (material `puMat`, maquinaria `costoMaq`, auxiliar `puMat` → `CD 10.01` → `20.02`); dorado 10.005 × 2 = **20.02** (básico importado 10.01 y auxiliar 2 × 10.01 = 20.02).
- Verificación: 317/317 (315 + 2 nuevos), Release 0 errores, `git diff --check` limpio.
- Hallazgos del dictamen N5-13 (corregidos en `a5d3aa6`): headers `[N5-12]` → `[N5-13]`, batería con cantidades fraccionarias `0.0001–9.9999` y renombres `ImportSelected_BateriaMultiplicar` / `Dorado_MaterialYAuxiliar`.

### PR N5-14 (`ProgramacionCurvaSService` — decimocuarto consumidor migrado al motor)

- Diez redondeos en `BuildInternal` (`RoundAmount` ×3, `RoundQuantity` ×3, `RoundPercentage` ×4) más las dos construcciones del motor en `BuildFinancialCurve` (con proyecto y fallback `2/2/4`) migrados a `SoproCalculationEngine` con las tres precisiones del proyecto. Sin cambios de API ni de comportamiento observable.
- Tests (`SOPRO.Tests/Services/Programacion/ProgramacionCurvaSServiceParityTests.cs`, 3 nuevos; el dorado existente `333.34/3.333` ya cubría 3/2/4): batería de 50 escenarios con decimales cantidad/importe 0-4 y porcentaje 0-6, oráculo diferencial que replica `BuildInternal` con `MotorCalculoSopro` en contexto gemelo; dorado de paridad y valores conocidos `333.34/333.34/3.333/33.3340` y `1000.01/10.000`; sobrecargas con proyecto existente coinciden (`333.34` y `3.33` con `2/2/4`).
- Verificación: 320/320 (317 + 3 nuevos), Release 0 errores, `git diff --check` limpio.
- Hallazgos del dictamen N5-14 (corregidos en `6d372e8`): conteo `RoundAmount ×3`/`RoundQuantity ×3`/`RoundPercentage ×4` y tercer test usa `BuildFinancialCurveFallbackConFachada` para paridad de la sobrecarga.

### PR N5-15 (`ProgramacionDistributionService` — decimoquinto consumidor migrado al motor)

- Tres construcciones (`DistributeUniform`, `DistributeUniformBatch`, `DistributeByPercentages`) y 24 operaciones (`Multiply` ×6, `RoundAmount` ×3, `RoundQuantity` ×6, `RoundPercentage` ×9) migradas a `SoproCalculationEngine` con las tres precisiones del proyecto (fallback `4/2/4`). Sin cambios de API ni de comportamiento observable.
- Tests (`SOPRO.Tests/Services/Programacion/ProgramacionDistributionServiceParityTests.cs`, 4 nuevos): baterías de 30 escenarios para `DistributeUniform`, `DistributeUniformBatch` y `DistributeByPercentages` con decimales cantidad/importe 0-4 y porcentaje 0-6, oráculo diferencial que replica el flujo completo con `MotorCalculoSopro` en contexto gemelo; dorado uniforme `5.00/500.00/50.00` con paridad y valores conocidos.
- Verificación: 324/324 (320 + 4 nuevos), Release 0 errores, `git diff --check` limpio.

### PR N5-16 (`ProgramacionInsumosService` — decimosexto consumidor migrado al motor)

- Motor con las tres precisiones del proyecto migrado a `SoproCalculationEngine` en el punto de entrada `Build` y en los **cuatro** helpers `ExplotarCanonicoRecursivo`, `ExplotarImportesCanonicosPorInsumo`, `ExplotarMatrizEnPeriodo` y `CalcularImportesUnitarios` (parámetros `SoproCalculationEngine` en lugar de `MotorCalculoSopro`). Operaciones `Multiply` ×8, `RoundAmount` ×11 y `RoundQuantity` ×5 = **24 operaciones** con las tres precisiones del proyecto. Sin cambios de API ni de comportamiento observable.
- Tests (`SOPRO.Tests/Services/Programacion/ProgramacionInsumosServiceParityTests.cs`, 2 métodos + oráculo literal `ProgramacionInsumosServiceLegacyOracle.cs` copiado de `c26cdb9` con `MotorCalculoSopro`): batería de 20 escenarios con precisiones independientes (cantidad/importe/porcentaje 0-4/0-4/0-6) y escenario completo de 8 componentes (MO normal/%MO, herramienta normal/%MO, maquinaria, auxiliar básico recursivo, cuadrilla) ejecutados para los **cuatro** `ProgramaInsumoTipo` (Materiales, ManoDeObra, Maquinaria, Herramienta) en contextos gemelos; oráculo legacy literal que replica el flujo completo (IDs por `MaterialId/ManoDeObraId/MaquinariaId/HerramientaId`, explosión recursiva de auxiliares, reconciliación por concepto/insumo, cantidades físicas y rendimiento ponderado) y valida `ProgramaInsumosResultDto` completo con igualdad exacta de conjuntos; dorado `10×10=100` distribuido `50/50` con paridad y valores conocidos (`Total 10`, `ImporteTotal 100`, `PU 10`).
- Verificación: 326/326, Debug y Release 0 errores, `git diff --check` limpio, sin warnings nuevos (CS0649 eliminado al remover `InsumoAcumLegacy`).

## 15. Fase N6: Empaquetado y validación privada

### Metadatos obligatorios

- `PackageId=SOPRO.Calculation`
- Licencia MIT
- Authors y descripción
- README del paquete
- XML documentation
- PDB portable
- Changelog

`RepositoryUrl`, Source Link y `.snupkg` se habilitan cuando sean útiles para los consumidores autorizados. No deben apuntar a recursos públicos inexistentes.

### CI del paquete

1. SDK fijado mediante `global.json`.
2. Restore reproducible.
3. Build Release.
4. Tests en Windows, Linux y macOS.
5. Pack sin recompilar.
6. Validación del `.nuspec` y dependencias.
7. Instalación desde carpeta local o feed privado en un consumidor fuera de la solución.
8. Baseline de API pública.
9. Conservación del `.nupkg` validado como artefacto privado de CI.

### Gate N6

- Versión interna inicial `1.0.0-preview.1` o `0.1.0`.
- Cero dependencias de ejecución.
- API pública inventariada.
- Ejemplos compilados como tests.
- Versión, commit y artefacto privado coinciden.
- `1.0.0` se reserva hasta estabilizar API y resultados.

### Publicación futura fuera de alcance

N6 no publica en NuGet.org ni abre el repositorio. Si posteriormente se decide una distribución pública, se requiere una decisión separada que cubra:

- Visibilidad del código fuente.
- Propiedad y acceso a NuGet.org.
- Source Link y símbolos públicos.
- Firma o procedencia del paquete.
- Seguridad del pipeline.
- Política de soporte y vulnerabilidades.
- Licencias y avisos de terceros.
- Promoción del mismo artefacto validado, sin recompilar.

## 16. Fase N7: Módulos avanzados

### Orden

1. Grafo de matrices y `%MO`.
2. Costo horario.
3. FSR.
4. Utilidad e indirectos.
5. Explosión.
6. Calendario y programación.
7. Programa de insumos.
8. Financiamiento.

### Gate común por módulo

- Inputs y resultados inmutables.
- Sin entidades EF.
- Sin mutación de inputs.
- Invariantes documentadas.
- Goldens exactos.
- Cultura, reloj y orden explícitos.
- Detección de ciclos cuando aplique.
- Una sola implementación en producción.
- Adaptador SQLite probado separadamente.

La migración WPF no espera a que termine N7. Un módulo puede migrarse visualmente si ya tiene un caso de uso headless estable aunque su algoritmo aún permanezca en Application.

## 17. Estrategia de pruebas

| Suite | Responsabilidad |
|---|---|
| Unitarias | Algoritmos y reglas puras |
| Caracterización | Comportamiento legacy |
| Contract tests | Legacy, paquete y fachada |
| Goldens | Escenario oficial y proyecto real |
| Integración SQLite | Persistencia, rollback y recarga |
| Arquitectura | Dependencias y APIs prohibidas |
| Reporting | Modelos y artefactos semánticos |
| Performance | Regresión de operaciones críticas |

Los snapshots SQLite deben normalizar IDs técnicos variables, timestamps, rutas temporales y orden de filas. Nunca deben excluir valores económicos, jerarquía, precisión o flags funcionales.

## 18. Riesgos

| Riesgo | Mitigación |
|---|---|
| Cambiar resultados durante la extracción | N0 separado y pruebas diferenciales |
| Validación nueva rompe legacy | API estricta y fachada compatible |
| Fachada incompleta | Snapshot de API pública completa |
| Contexto EF compartido | Contexto por operación y cancelación |
| Transacciones parciales | Un commit lógico por comando |
| Algoritmos duplicados | Inventario y gate de una sola implementación |
| Paquete inestable | Preview privado, API baseline y consumidor fuera de la solución |
| Fase avanzada demasiado grande | Gates y releases independientes por módulo |

## 19. Criterios de finalización

El Plan 01 termina cuando:

- `SOPRO.Calculation` funciona copiando únicamente su carpeta.
- El paquete no expone ni referencia tipos SOPRO.
- SOPRO y el consumidor de validación usan la misma implementación del motor.
- La API legacy está retirada o delega completamente.
- Application ofrece casos de uso headless para todos los módulos.
- Application no referencia Data, EF, SQLite, WinForms, WPF ni ClosedXML.
- Ninguna UI accede directamente a persistencia.
- Los reportes reciben modelos neutrales.
- Todos los goldens y escenarios reales pasan.
- Las correcciones pendientes están separadas de la extracción.

## 20. Estrategia de PRs

- Un PR por fase o vertical verificable.
- N0 siempre separado de N1 y N2.
- No mezclar movimiento estructural con corrección contable.
- No eliminar el código legacy en el mismo PR que introduce el reemplazo.
- Cada PR incluye comandos ejecutados, resultados y diferencias conocidas.
- Un módulo solo avanza al siguiente gate cuando el anterior está integrado.
