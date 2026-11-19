# ADR 0001: integridad del autoguardado de Presupuesto

Estado: **aprobada por el mantenedor el 2026-10-22 e implementada**. La migración se integró en el PR [#47](https://github.com/iCarlost/SOPRO/pull/47) (squash [`79207f6`](https://github.com/iCarlost/SOPRO/commit/79207f6)) y el cierre cancelable y el estado visible en el PR [#51](https://github.com/iCarlost/SOPRO/pull/51) (squash [`2183e08`](https://github.com/iCarlost/SOPRO/commit/2183e08)). U4 (reset independiente de formato) quedó diferido y no aprobado, por lo que no forma parte de esta implementación.

## Contexto

Al momento de la propuesta, `FormPresupuesto.GuardarCambios()` ignoraba las excepciones de `BudgetPersistenceService.ApplyAutoSaveChanges`. Las filas del grid conservaban entidades de un `SOPROContext` compartido con otros módulos. Varias de las trece rutas que llamaban a `GuardarCambios()` modificaban entidades y ejecutaban `SaveChanges()` antes de llegar a él; el cierre de la pestaña también llamaba a `SaveChanges()`.

Una prueba diagnóstica con SQLite temporal y `RAISE(ABORT)` reprodujo el peligro: fallaba el guardado de la cantidad de un concepto, pero un `SaveChanges()` posterior para la descripción del proyecto guardaba también la cantidad que había fallado (10 → 25). No era solo un problema de mensajes.

## Garantías exigidas

1. Un intento fallido no puede confirmarse después por el guardado de otro módulo.
2. Un fallo no pierde cambios ajenos ni confirma una parte de la operación como si estuviera completa.
3. Reintentar no duplica altas/importaciones ni reutiliza IDs de inserciones revertidas.
4. El usuario puede cancelar un cierre conservando su trabajo; «Guardado» significa que la operación completa se confirmó.
5. La aritmética y la precisión existentes no cambian como efecto colateral del guardado.

## Enfoques descartados

- **Solo capturar la excepción y mostrar un aviso:** deja entidades modificadas en el contexto compartido; otro `SaveChanges()` puede confirmarlas.
- **Transacción exterior alrededor de los guardados internos actuales:** revertir la base no revierte `ChangeTracker`, referencias en `row.Tag`, IDs de altas ni filas eliminadas del grid. `SaveChanges()` internos ya pueden haber aceptado esos estados.
- **`ChangeTracker.Clear()` o recarga global tras un fallo:** descarta modificaciones ajenas al presupuesto.
- **Una ruta de guardado nueva al lado de la anterior:** perpetúa dos contratos de persistencia con garantías distintas.

## Decisión

Cada acción raíz de Presupuesto será un comando explícito con valores base y propuestos, IDs de filas existentes y claves provisionales para altas. El formulario conserva un borrador independiente de EF. El comando se ejecuta en un `SOPROContext` **nuevo por operación**, creado por `IProjectDbContextFactory` ya existente; comprueba las bases, aplica la operación y sus efectos económicos con los servicios canónicos, llama exactamente una vez a `SaveChanges()` y dispone ese contexto. No se añade una capa, proyecto, dependencia, motor ni segunda ruta de persistencia.

Los comandos expresan **intención**, no una copia arbitraria de diez columnas con importes ya calculados. El cálculo derivado y los agrupadores se resuelven en Application mediante los servicios existentes. Un único ejecutor interno posee contexto, transacción corta, resultado y diagnóstico; los handlers tipados de cada acción no duplican ese ciclo. Los comandos conservan su base y propuesta de forma inmutable para que Retry no cambie la operación original.

Ante fallo se descarta el contexto de escritura completo; el borrador no se pierde. Una respuesta distingue confirmado, conflicto, fallo y resultado incierto: si no se sabe si el commit ocurrió, hay que reconciliar con la base antes de reintentar una alta o importación. No se reutilizan IDs de una operación fallida.

El `SOPROContext` compartido permanece para otros módulos, **no** escribe el presupuesto. Antes del intento se comprueban cambios pendientes que intersecten conceptos, matrices, agrupadores o parámetros afectados: en caso de conflicto no se guardan ni descartan cambios ajenos. Tras confirmar se sincronizan solo las entidades afectadas que estén limpias; nunca `ChangeTracker.Clear()` o `Reload()` global. `row.Tag` deja de contener entidades EF del contexto compartido y pasa a un snapshot de fila con identidad estable. Si la sincronización falla, no se repite un comando ya confirmado: se bloquean acciones dependientes hasta reconciliar la vista.

La migración cubre edición y Undo/Redo, creación, asignación de matriz, pegado, borrado, ordenamiento, reajuste de costo, importación, refresco de precios y recálculo. Cada comando incluye todos sus efectos: concepto, matriz/componente si aplica, orden, importes y agrupadores. Cargar o representar el presupuesto es **solo lectura**. Los eventos originados por renderizado no inician otro guardado. No se integró una ruta nueva al lado de la antigua: los guardados internos heredados se retiraron y `ApplyAutoSaveChanges` no tiene llamadores en producción; se conserva únicamente como oráculo de regresión en pruebas.

Ninguna transacción estará abierta durante diálogos, espera del usuario o reintentos. Se puede usar una transacción SQLite corta dentro del comando para proteger lectura/base y escritura, pero sin guardados internos ni rollback manual de entidades tracked.

## Verificación de la implementación

- La prueba de fallo con SQLite (`RAISE(ABORT)`) quedó cubierta por la ruta nueva: un guardado ajeno persiste su dato y no el presupuesto fallido, y el borrador conserva los valores propuestos.
- Las pruebas de fallo tras una primera escritura, retry sin duplicados, conflicto con base cambiada, commit incierto y carga sin escrituras incidentales acompañaron la migración (PR #47).
- El cierre de pestaña y de proyecto es cancelable si hay un borrador pendiente: un conflicto o resultado incierto conserva la captura y cancela el cierre; la pestaña no se elimina (PR #51).
- Se ejecutaron validación enfocada, suite completa y revisión del recorrido sobre una copia de proyecto; las pruebas de fallo no usan bases reales.

## Ejecución

1. El mantenedor aprobó la separación de contextos y el contrato de conflictos antes de editar producción.
2. Se prepararon los contratos y pruebas de Application y se migraron las operaciones WinForms por conjuntos completos, sin dejar `GuardarCambios` operativo como ruta de escritura.
3. Tras la integridad se añadieron el estado visible, el reintento y el cierre cancelable.

## Consecuencias

- Cada operación raíz usa un `SOPROContext` nuevo por comando, con una transacción corta y un único `SaveChanges()`; no hay una segunda ruta de persistencia para el presupuesto.
- El cierre de la pestaña puede cancelarse conservando el trabajo, y «Guardado» solo se afirma cuando la operación completa se confirmó.
- `ApplyAutoSaveChanges` no tiene llamadores en producción y se conserva solo como oráculo de regresión; el `SOPROContext` compartido sigue existiendo para otros módulos sin escribir el presupuesto.
- U4 (reset independiente de formato) queda fuera de esta decisión por estar diferido/no aprobado.
