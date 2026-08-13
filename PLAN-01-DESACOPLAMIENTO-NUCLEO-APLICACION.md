# Plan 01: Desacoplamiento del Núcleo y la Aplicación

**Estado:** N0 cerrado (PR #2, merge `508188a`); N1 cerrado (PR #3, merge `b9a4576` + `eaa2f67`); N2 implementado en `feat/calculation-n2-facade` — `MotorCalculoSopro` delega los resultados numéricos en `SOPRO.Calculation` conservando API, normalizaciones, excepciones, forma de colecciones y formato (suite 197/197, Gate N2 pendiente de integración del PR)
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
- `FormCatalogoMateriales` y `FormEditarMaterial` consumen los casos de uso; el formulario no calcula ni persiste (el Grid solo lee DTOs ya recalculados por el motor en el guardado). Formato de columnas, decimales de importe y título del PDF provienen de la sesión neutral.
- Buscar/eliminar no mutan: previews y consultas con `AsNoTracking`.
- Validaciones (campos requeridos, clave única en alcance del proyecto) y errores tipados viven en `SaveMaterial`: probados sin formularios en `MaterialsUseCasesTests` (38 pruebas, SQLite real del mismo esquema), incluyendo rollback ante fallo de propagación (overflow), cancelación sin persistencia parcial, reapertura SQLite verificando `PrecioUnitario`/`ImporteTotal`, aislamiento entre proyectos y catálogo maestro, clave duplicada al editar, identidad maestra por `MaterialMaestroId` (colisión de Ids entre bases, edición de la fila maestra de un importado, reutilización de la misma fila maestra en guardados consecutivos), unicidad de clave maestra sin importar capitalización (exacta y mixta), limpieza del tracker tras rollback, agrupadores por Orden/Nivel (suma de bloques, agrupador vacío en cero, agrupadores legacy con totales previos), propagación y eliminación que no cruzan proyectos (Save/Delete/Preview con material de P1 en matriz de P2), y resolución de valores de exportación con `MaterialListItem`.

## 13. Fase N4: Persistencia y ciclo de vida

### Acciones

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
