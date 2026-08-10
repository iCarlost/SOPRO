# Plan 02: Migración Modular de WinForms a WPF

**Estado:** acordado, pendiente de implementación  
**Decisión arquitectónica:** [ADR-001](ADR-001-ARQUITECTURA-OBJETIVO.md)  
**Prerrequisito funcional:** [Plan 01](PLAN-01-DESACOPLAMIENTO-NUCLEO-APLICACION.md)  
**Distribución actual:** documentación interna en repositorio privado

## 1. Objetivo

Reemplazar gradualmente SOPRO.WinForms con una interfaz WPF sin duplicar lógica, cambiar resultados contables ni crear una segunda ruta de persistencia.

La nueva UI reutilizará:

- Los casos de uso de `SOPRO.Application`.
- El motor `SOPRO.Calculation` a través de Application.
- La misma base SQLite y el mismo esquema.
- Los mismos adaptadores de reporting.
- Requests, results, validaciones y códigos de error compartidos.

## 2. Decisiones de producto y tecnología

| Tema | Decisión |
|---|---|
| Plataformas | Solo Windows |
| Framework objetivo | WPF sobre .NET 8 o versión LTS vigente al iniciar |
| Entrega | Por módulos |
| Convivencia inicial | WPF alojado dentro del shell WinForms |
| Compatibilidad | Preservar comportamiento antes de corregirlo |
| Base de datos | Un solo formato SQLite |
| Reporting inicial | Mantener PDFsharp GDI y ClosedXML desacoplados de la UI |

WPF se elige sobre Avalonia porque no existe un requisito cross-platform y permite una transición gradual con menos cambios simultáneos.

## 3. Estado actual relevante

- Existen decenas de formularios WinForms y controles personalizados.
- Formularios y controles conservan `SOPROContext` y ejecutan `SaveChanges`.
- El contexto del proyecto se comparte entre módulos.
- Existen cálculos oficiales dentro de handlers y generadores.
- Presupuesto combina grid, jerarquía, persistencia, undo, clipboard y reporting.
- `PanelMatricesEmbebido` combina UI, EF, cálculo y propagación.
- Gantt y Curva S dependen del comportamiento de `DataGridView` y GDI.
- Algunos reportes leen directamente columnas, estilos y filas de grids.
- Existen eventos estáticos y refrescos basados en formularios abiertos.

La dificultad principal no es convertir controles, sino retirar responsabilidades no visuales de los formularios antes de reemplazarlos.

## 4. Alcance

### Incluido

- Proyecto y shell WPF.
- MVVM y ViewModels comprobables sin infraestructura.
- Alojamiento temporal WPF/WinForms.
- Migración vertical de todos los módulos funcionales.
- Reimplementación visual de grids, Gantt, Curva S y diseñador.
- Adaptadores para diálogos, clipboard, launcher, layout y eventos.
- Automatización UI y regresión visual.
- Cutover, rollback y retiro final de WinForms.

### No incluido

- Soporte Linux o macOS.
- Cambio de reglas contables por diferencias visuales.
- Cambio del esquema SQLite por conveniencia de binding.
- Compartir controles o code-behind entre WinForms y WPF.
- Pixel-perfect entre ambos frameworks.
- Introducir controles comerciales sin evaluación y aprobación separadas.

## 5. Prerrequisitos por módulo

Un módulo puede comenzar su migración WPF cuando:

1. Existe un caso de uso headless para sus consultas y comandos.
2. WinForms ya usa ese caso de uso.
3. No existe una ruta alternativa de cálculo o persistencia en el formulario.
4. Inputs, resultados y errores son DTOs neutrales.
5. Hay pruebas unitarias y de integración SQLite.
6. Existe un baseline funcional del módulo legacy.
7. Los reportes del módulo reciben modelos neutrales o tienen un plan explícito para hacerlo.

No es necesario que todos los módulos avanzados estén incorporados en `SOPRO.Calculation`.

## 6. Reglas de presentación

### Views y code-behind

El code-behind puede contener únicamente comportamiento visual difícil de expresar mediante binding, por ejemplo:

- Foco.
- Drag and drop visual.
- Scroll sincronizado.
- Medición y renderizado.
- Integración con controles nativos.

No puede contener:

- EF o SQLite.
- Cálculo contable.
- `Math.Round` para importes de negocio.
- Consultas o `SaveChanges`.
- Creación del motor.
- Reglas de validación de dominio.

### ViewModels

Un ViewModel no recibe:

- `SOPROContext`.
- `DbSet` o repositorios.
- Entidades rastreadas.
- `MotorCalculoSopro` o `SoproCalculationEngine`.
- `Window`, `Form`, `DataGridView` o controles WPF.
- Acceso directo a filesystem, reloj o clipboard.

Los ViewModels consumen casos de uso y servicios visuales abstractos.

### Servicios visuales

Se usarán adaptadores para:

```text
IFileOpenPicker
IFileSavePicker
IClipboardService
IUserDialogService
IExternalLauncher
ILayoutStore<T>
IUiDispatcher
IDebouncer
IApplicationEventBus
```

Solo se introduce cada interfaz cuando aparece el primer consumidor real.

## 7. Estrategia de convivencia

La estrategia preferida es:

1. Mantener inicialmente el shell WinForms.
2. Crear módulos WPF como controles alojables.
3. Integrarlos mediante `ElementHost` dentro del proceso actual.
4. Mantener un solo workspace y una sola sesión de escritura.
5. Reemplazar el shell al final.

El spike U0 debe validar esta estrategia. Si foco, DPI, diálogos o controles complejos la hacen inviable, se documentará una decisión alternativa antes de crear el segundo módulo.

No se abrirá el mismo proyecto para escritura en dos procesos durante la coexistencia.

## 8. Resumen de fases

| Fase | Resultado | Riesgo | Esfuerzo relativo |
|---|---|---:|---:|
| U0 | Spike WPF/WinForms y estándares visuales | Medio | M |
| U1 | Catálogo de Materiales como piloto completo | Medio | M |
| U2 | Catálogos y calculadores | Medio | L |
| U3 | Consultas, explosión, indirectos y finanzas | Alto | L |
| U4 | Matrices, APU y selectores | Muy alto | XL |
| U5 | Presupuesto | Extremo | XL |
| U6 | Programación, Gantt y Curva S | Extremo | XL |
| U7 | Reporting y diseñador | Extremo | XL |
| U8 | Shell WPF y retiro de WinForms | Alto | L |

Las estimaciones de calendario se fijarán después de U0 y U1 usando velocidad real, no conteo de formularios.

## 9. Fase U0: Spike técnico

### Prototipos obligatorios

- `DataGrid` WPF con columnas dinámicas.
- Edición, validación, selección y virtualización.
- WPF alojado en WinForms mediante `ElementHost`.
- Foco, teclado, DPI y temas.
- Diálogo modal con ownership correcto.
- Llamada a un caso de uso real.
- Cancelación y progreso.
- Render de una sección simple de Gantt o Curva S.
- Reporte generado sin leer controles.

### Decisiones de U0

- Estructura del proyecto WPF.
- Convención MVVM.
- Estrategia de comandos y notificación.
- Política de recursos y localización.
- Estrategia de temas y DPI.
- Herramienta de UI Automation.
- Uso de controles de terceros, si resultara imprescindible.

### Gate U0

- El control WPF funciona dentro del shell actual.
- No hay bloqueos de foco o diálogos sin solución documentada.
- El grid cumple un baseline de rendimiento acordado.
- El ViewModel se prueba sin crear ventana.
- El caso de uso no conoce WPF.

## 10. Fase U1: Piloto Catálogo de Materiales

### Alcance

- Listado.
- Búsqueda y filtros.
- Alta y edición.
- Eliminación y preview de impacto.
- Validación.
- Propagación de precio.
- Refresco mediante eventos de aplicación.
- Estado ocupado, progreso y cancelación.
- Persistencia de columnas mediante modelo neutral.

### Estrategia

1. WinForms adopta primero los casos de uso compartidos.
2. Se crea el ViewModel WPF.
3. Se crea la vista WPF.
4. Se habilita mediante feature flag.
5. WinForms y WPF se comparan sobre clones idénticos de la base.

### Gate U1

- Ambos frontends observan el mismo request y response.
- Los snapshots SQLite son idénticos.
- Cada comando produce un solo commit.
- No existe acceso directo a Data desde la vista o ViewModel.
- Guardar, cerrar y reabrir conserva el mismo estado.
- El módulo puede volver a WinForms sin migración de datos.

## 11. Fase U2: Catálogos y calculadores

### Orden

1. Mano de obra.
2. Maquinaria.
3. Herramientas.
4. Catálogos auxiliares.
5. FSR.
6. Costo horario.
7. Porcentajes.
8. Utilidad.

Antes de migrar FSR o Costo Horario, sus fórmulas deben estar centralizadas y probadas fuera de WinForms. Los reportes deben consumir el mismo resultado detallado que la UI.

### Gate U2

- No queda cálculo normativo en handlers.
- Los defaults y la cultura están centralizados.
- UI y reportes muestran el mismo desglose.
- Cada catálogo cumple la definición de terminado por módulo.

## 12. Fase U3: Consultas, costos indirectos y finanzas

### Orden

1. Explosión de insumos.
2. Programa de insumos de solo lectura.
3. Curva S de solo lectura.
4. Indirectos.
5. Financiamiento.

Las primeras vistas de solo lectura validan grids, filtros y reportes sin introducir nuevos comandos. Indirectos y Financiamiento se migran después de tener contratos puros y una definición canónica de sus resultados.

### Gate U3

- Mismo número, orden y valores de filas.
- Mismos totales y formatos.
- Financiamiento persiste una sola proyección compartida.
- UI, PDF y Excel no reconstruyen flujo o acumulados.
- Los modelos clásico y dual están explícitamente tipados.

## 13. Fase U4: Matrices y APU

### Alcance

- Editor de matrices.
- Componentes.
- Selectores de insumos y APU.
- Básicos y cuadrillas.
- `%MO`.
- Reajuste por objetivo.
- Propagación.
- Importación externa.
- Panel embebido.

### Diseño objetivo

```text
LoadMatrixEditor -> MatrixEditorSnapshot
SaveMatrix       -> SaveMatrixResult
DeleteMatrix     -> DeleteMatrixResult
SearchResources  -> ResourceSearchResult
```

Los selectores devuelven IDs y snapshots. No devuelven entidades rastreadas ni alojan formularios como contenido.

### Gate U4

- Grafo de auxiliares validado y sin ciclos silenciosos.
- Guardado de cabecera, componentes y propagación en una transacción.
- Mismo costo antes y después de reabrir.
- Mismos resultados para normal, cuadrilla y `%MO`.
- Ninguna fila visual contiene una entidad EF como estado canónico.

## 14. Fase U5: Presupuesto

Presupuesto es la vertical de mayor riesgo porque combina edición intensiva, jerarquía, matrices, autosave, totales, reportes y estado visual.

### Alcance funcional

- Carga y jerarquía.
- Edición de celdas.
- Cambio de tipo.
- Asignación de APU.
- Inserción y eliminación.
- Drag and drop.
- Copiar y pegar.
- Undo y redo.
- Autosave y guardado explícito.
- Totales y porcentajes.
- Configuración de columnas.
- Búsqueda y selección.
- Reportes de presupuesto y APU.

### Reglas

- La persistencia usa IDs y valores de ViewModel, nunca strings formateados del grid.
- El orden y nivel son parte del contrato.
- Los agrupadores se recalculan en el caso de uso.
- La selección de APU usa un contrato compartido con Matrices.
- Undo/redo almacena comandos o snapshots neutrales, no controles.

### Gate U5

- Igualdad exacta de conceptos, orden, niveles y matrices.
- Igualdad exacta de importes y totales.
- Escenarios oficial y real permanecen verdes.
- Guardar, cerrar y reabrir mantiene el estado.
- E2E cubre teclado, pegado, drag and drop y edición inválida.
- Reportes son semánticamente equivalentes.

## 15. Fase U6: Programación, Gantt y Curva S

### Alcance

- Actividades.
- Dependencias.
- Calendario laboral.
- Periodos.
- Distribuciones uniformes y manuales.
- Curva S.
- Gantt.
- Programa de insumos.

### Reglas

- El cálculo de fechas y distribución es headless.
- WPF recibe un modelo de render neutral.
- Los hitos y tipos de dependencia tienen semántica explícita.
- Las distribuciones manuales no se reemplazan sin una orden explícita.
- Los ciclos se rechazan con una ruta diagnóstica.
- Scroll, selección y alturas del grid se coordinan desde un adaptador visual.

### Gate U6

- Mismas fechas, periodos, cantidades e importes.
- Mismos acumulados y Curva S.
- Ciclos directos e indirectos detectados.
- Virtualización y scroll cumplen el baseline.
- Gantt no lee entidades EF o controles WinForms.

## 16. Fase U7: Reporting y diseñador

### Contrato objetivo

```text
ReportRequest
    -> ReportModelBuilder
    -> ReportDocumentModel
    -> PdfRenderer | ExcelRenderer
```

`ReportDocumentModel` incluye:

- Secciones.
- Columnas.
- Filas.
- Jerarquía.
- Estilos semánticos.
- Totales.
- Encabezado y pie.
- Orientación y papel.
- Reglas de paginación.

### Reglas

- Los renderers no reciben grids o ventanas.
- Las fuentes de fecha y cultura son explícitas.
- PDF y Excel no recalculan importes.
- Los modelos del diseñador no usan tipos WinForms.
- Coordenadas físicas, z-order, snap y resize se mantienen como dominio del diseñador.

### Gate U7

- Ambas UIs producen el mismo `ReportRequest`.
- Ambas reciben el mismo `ReportDocumentModel`.
- PDF y XLSX contienen los mismos valores y totales.
- No se compara el ZIP o PDF por hash bruto.
- PDF se valida por texto, páginas y render visual.
- XLSX se valida por hojas, celdas, formatos, merges y configuración de impresión.

## 17. Fase U8: Shell WPF y retiro de WinForms

### Acciones

1. Crear navegación y lifecycle definitivo en WPF.
2. Trasladar todos los módulos WPF al nuevo shell.
3. Retirar `ElementHost` y adaptadores de convivencia.
4. Cambiar el ejecutable en build, instalador y actualización.
5. Mantener la política actual de conservación de bases y configuración.
6. Distribuir una versión de transición con rollback probado.
7. Marcar WinForms como legacy.
8. Eliminar WinForms en una versión posterior estable.

### Gate U8

- WPF abre, edita, guarda, reabre y reporta el proyecto real.
- No requiere conversión de datos por frontend.
- Cero rutas funcionales exclusivas de WinForms.
- Cero locks residuales de SQLite al cerrar.
- Instalación, actualización y rollback pasan smoke tests.
- WinForms puede retirarse sin retirar lógica de negocio.

## 18. Definición de terminado por módulo

Un módulo está migrado cuando:

- WinForms y WPF llaman el mismo caso de uso.
- No hay cálculo o persistencia en la vista.
- El ViewModel se prueba sin Window ni Dispatcher real.
- Requests y responses coinciden.
- Los snapshots SQLite coinciden.
- Las validaciones son equivalentes.
- Los reportes son equivalentes.
- Tiene pruebas unitarias, integración y E2E.
- Tiene feature flag o rollback.
- No exige ejecutar ambos frontends sobre la misma base.

## 19. Estrategia de paridad

Para cada comando mutable:

1. Crear un `seed.db`.
2. Copiarlo a `headless.db`, `winforms.db` y `wpf.db`.
3. Fijar cultura, reloj, precisión y configuración.
4. Ejecutar el caso de uso directamente.
5. Ejecutarlo mediante WinForms.
6. Ejecutarlo mediante WPF.
7. Comparar request, response, eventos y snapshot SQLite.

Nunca se ejecutan ambos frontends secuencialmente sobre la misma base para comparar, porque duplicaría los efectos.

Se recomienda un decorador de diagnóstico en pruebas que registre:

```text
UseCaseId
ContractVersion
ExecutionId
RequestHash
ResponseHash
AffectedEntities
CommitCount
```

## 20. Estrategia de pruebas

| Suite | Propósito |
|---|---|
| ViewModel | Estado, comandos, validación, cancelación |
| Binding/XAML | Bindings, converters y recursos |
| Integración SQLite | Escritura, rollback y recarga |
| E2E compartido | Flujos equivalentes WinForms/WPF |
| Snapshot semántico | Datos, acciones, selección y mensajes |
| Regresión visual | Layout estable dentro de cada frontend |
| Reporting | Contenido y render de PDF/XLSX |
| Performance | Grids, carga, búsqueda y Gantt |

No se exige igualdad de píxeles entre WinForms y WPF. Se exige igualdad semántica:

- Mismos datos.
- Misma jerarquía.
- Mismas acciones disponibles.
- Mismas validaciones.
- Mismos resultados persistidos.
- Mismos reportes funcionales.

## 21. Jornadas E2E críticas

1. Abrir un proyecto real y navegar.
2. Editar un material y observar propagación.
3. Crear una matriz con `%MO`.
4. Asignar APU y cambiar cantidad en presupuesto.
5. Guardar, cerrar y reabrir.
6. Cambiar precisión y recalcular.
7. Generar programa y distribución.
8. Consultar Gantt y Curva S.
9. Generar explosión y programa de insumos.
10. Calcular financiamiento.
11. Calcular FSR, Costo Horario e Indirectos.
12. Exportar los reportes principales.

## 22. Riesgos

| Riesgo | Mitigación |
|---|---|
| Copiar lógica al ViewModel | Prerrequisito headless y tests de arquitectura |
| Dos rutas de escritura | Un solo caso de uso y decorador de diagnóstico |
| Contención SQLite | Un proceso escritor y workspace lock |
| Regresión del grid de presupuesto | U0, E2E de teclado y baseline de rendimiento |
| Desalineación Gantt/grid | Modelo neutral y pruebas de scroll/virtualización |
| Reportes distintos | `ReportDocumentModel` compartido |
| Fuga de eventos estáticos | Event bus con suscripciones descartables |
| Migración interminable | Feature flags, gates y módulos verticales cerrados |
| Dependencia de controles comerciales | Spike y aprobación separada |

## 23. Criterios de finalización

El Plan 02 termina cuando:

- Todos los módulos cumplen su definición de terminado.
- WPF usa exclusivamente casos de uso de Application.
- WPF no accede a EF, SQLite o al motor.
- El proyecto real conserva datos y resultados.
- PDF y Excel no dependen de controles visuales.
- Instalador y actualizador entregan el ejecutable WPF.
- WinForms puede eliminarse sin mover lógica funcional.
- No queda compatibilidad temporal de hosting.
- La base SQLite no distingue qué frontend la editó.

## 24. Condición para reconsiderar Avalonia

La tecnología se reevalúa antes de U4 si aparece un requisito contractual de Linux o macOS. En ese caso se crea un nuevo ADR que cubra:

- Backend PDF no GDI.
- Fuentes multiplataforma.
- Instaladores por sistema operativo.
- Actualización por RID.
- Rutas y permisos por plataforma.
- CI Windows, Linux y macOS.

La frontera de Application definida en el ADR actual seguirá siendo válida.
