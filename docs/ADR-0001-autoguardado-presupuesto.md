# ADR 0001 (propuesta): integridad del autoguardado de Presupuesto

Estado: **aprobada por el mantenedor el 2026-10-22**. La aprobación autoriza la migración; este documento no da por resuelto el defecto hasta superar las puertas de verificación.

## Contexto

`FormPresupuesto.GuardarCambios()` ignora las excepciones de `BudgetPersistenceService.ApplyAutoSaveChanges`. Las filas del grid conservan entidades de un `SOPROContext` compartido con otros módulos. Varias de las trece rutas que llaman a `GuardarCambios()` modifican entidades y ejecutan `SaveChanges()` antes de llegar a él; el cierre de la pestaña también llama a `SaveChanges()`.

Una prueba diagnóstica con SQLite temporal y `RAISE(ABORT)` reproduce el peligro: falla el guardado de la cantidad de un concepto, pero un `SaveChanges()` posterior para la descripción del proyecto guarda también la cantidad que había fallado (10 → 25). No es solo un problema de mensajes.

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

## Decisión propuesta

Cada acción raíz de Presupuesto será un comando explícito con valores base y propuestos, IDs de filas existentes y claves provisionales para altas. El formulario conserva un borrador independiente de EF. El comando se ejecuta en un `SOPROContext` **nuevo por operación**, creado por `IProjectDbContextFactory` ya existente; comprueba las bases, aplica la operación y sus efectos económicos con los servicios canónicos, llama exactamente una vez a `SaveChanges()` y dispone ese contexto. No se añade una capa, proyecto, dependencia, motor ni segunda ruta de persistencia.

Los comandos expresan **intención**, no una copia arbitraria de diez columnas con importes ya calculados. El cálculo derivado y los agrupadores se resuelven en Application mediante los servicios existentes. Un único ejecutor interno posee contexto, transacción corta, resultado y diagnóstico; los handlers tipados de cada acción no duplican ese ciclo. Los comandos conservan su base y propuesta de forma inmutable para que Retry no cambie la operación original.

Ante fallo se descarta el contexto de escritura completo; el borrador no se pierde. Una respuesta distingue confirmado, conflicto, fallo y resultado incierto: si no se sabe si el commit ocurrió, hay que reconciliar con la base antes de reintentar una alta o importación. No se reutilizan IDs de una operación fallida.

El `SOPROContext` compartido permanece para otros módulos, **no** escribe el presupuesto. Antes del intento se comprueban cambios pendientes que intersecten conceptos, matrices, agrupadores o parámetros afectados: en caso de conflicto no se guardan ni descartan cambios ajenos. Tras confirmar se sincronizan solo las entidades afectadas que estén limpias; nunca `ChangeTracker.Clear()` o `Reload()` global. `row.Tag` deja de contener entidades EF del contexto compartido y pasa a un snapshot de fila con identidad estable. Si la sincronización falla, no se repite un comando ya confirmado: se bloquean acciones dependientes hasta reconciliar la vista.

La migración cubre edición y Undo/Redo, creación, asignación de matriz, pegado, borrado, ordenamiento, reajuste de costo, importación, refresco de precios y recálculo. Cada comando debe incluir todos sus efectos: concepto, matriz/componente si aplica, orden, importes y agrupadores. Cargar o representar el presupuesto es **solo lectura**. Los eventos originados por renderizado no inician otro guardado. No se integrará una ruta nueva al lado de la antigua: `GuardarCambios` y los guardados internos heredados se retirarán tras migrar sus consumidores.

Ninguna transacción estará abierta durante diálogos, espera del usuario o reintentos. Se puede usar una transacción SQLite corta dentro del comando para proteger lectura/base y escritura, pero sin guardados internos ni rollback manual de entidades tracked.

## Puertas de verificación previas a la integración

- La prueba del fallo SQLite se vuelve verde con la ruta nueva: un guardado ajeno persiste su dato y no el presupuesto fallido; el borrador conserva los valores propuestos.
- Pruebas de fallo tras una primera escritura para alta, borrado, asignación e importación: en la base y en el grid no quedan resultados parciales ni IDs inválidos.
- Retry no duplica altas/importaciones; una base cambiada devuelve conflicto y un commit incierto exige reconciliación antes de reintentar.
- Carga del presupuesto sin escrituras incidentales; Undo/Redo no produce intentos duplicados.
- Cierre de pestaña y de proyecto cancelable si hay borrador pendiente. Ninguna pestaña se elimina si su cierre se cancela.
- Validación enfocada, suite completa y revisión humana del recorrido en una copia de proyecto. No usar bases reales para pruebas de fallo.

## Puertas de ejecución

1. El mantenedor aprobó esta separación de contextos y el contrato de conflictos antes de editar producción.
2. Se preparan contratos y pruebas de Application; luego se migran las operaciones WinForms por conjuntos completos, sin enviar un PR parcial que conserve `GuardarCambios` operativo.
3. Después de la integridad se implementan estado visible, reintento y cierre cancelable; no se anuncia «Guardado» mientras haya una ruta heredada capaz de confirmar cambios fuera del contrato.
