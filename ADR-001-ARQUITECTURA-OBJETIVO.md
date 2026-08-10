# ADR-001: Arquitectura objetivo para desacoplar cálculo, infraestructura y UI

**Estado:** aceptado  
**Fecha:** 2026-08-10  
**Alcance:** arquitectura de transición y objetivo para la modernización de SOPRO  
**Distribución actual:** repositorio fuente privado; `SOPRO.Calculation` todavía no está publicado

## 1. Contexto

SOPRO es actualmente una aplicación de escritorio Windows basada en WinForms. El cálculo central vive en `SOPRO.Application`, mientras que la aplicación y la UI conocen directamente persistencia, entidades mutables y servicios concretos.

El grafo actual es:

```text
SOPRO.Core        -> BCL
SOPRO.Data        -> SOPRO.Core + EF Core + SQLite
SOPRO.Application -> SOPRO.Core + SOPRO.Data + ClosedXML
SOPRO.WinForms    -> SOPRO.Core + SOPRO.Data + SOPRO.Application
```

Esto genera dos problemas distintos:

1. El motor de cálculo no puede consumirse sin arrastrar EF Core, SQLite y ClosedXML.
2. WinForms no puede reemplazarse con seguridad porque formularios y controles consultan, mutan y persisten entidades directamente.

La extracción de `SOPRO.Calculation` resuelve el primer problema, pero no basta para resolver el segundo. La UI futura necesita casos de uso headless, contratos neutrales y una frontera de persistencia independiente del framework visual.

Este ADR es documentación interna de arquitectura. Describe un estado objetivo, no afirma que los proyectos futuros ya existan ni que el repositorio o sus paquetes sean públicos.

## 2. Fuerzas de decisión

- Preservar inicialmente los resultados contables actuales.
- Permitir empaquetar el motor para reutilizarlo sin decidir todavía una publicación abierta.
- Mantener el formato SQLite existente durante la migración visual.
- Sustituir WinForms por módulos, sin una reescritura de una sola vez.
- Mantener SOPRO como aplicación Windows a mediano plazo.
- Reutilizar inicialmente PDFsharp GDI, ClosedXML, Inno Setup y el canal actual de actualización.
- Evitar capas, contratos y abstracciones genéricas que no resuelvan una dependencia concreta.
- Mantener una sola implementación de cada algoritmo y caso de uso.

## 3. Decisiones

### 3.1 Motor independiente

Se creará `SOPRO.Calculation` como biblioteca `net8.0`, no Windows y empaquetable en formato NuGet, con estas reglas:

- Cero `ProjectReference`.
- Cero dependencias NuGet de ejecución.
- Cero tipos de otros proyectos SOPRO en su API `public` de C#.
- Contratos escalares e inmutables.
- Resultados deterministas con entradas estables.
- Cultura, reloj y orden explícitos cuando afecten el resultado.
- API `public` de C# en inglés.

`SOPRO.Application.Services.MotorCalculoSopro` permanecerá temporalmente como fachada compatible en español.

### 3.2 Frontera de aplicación

`SOPRO.Application` será el punto de entrada funcional para cualquier UI. Contendrá:

- Casos de uso.
- Requests y results.
- DTOs neutrales.
- Validación y orquestación.
- Puertos específicos de persistencia y reporting.
- Códigos de error independientes de la presentación.

La UI no recibirá `SOPROContext`, `DbSet`, `IQueryable`, entidades rastreadas ni repositorios genéricos.

### 3.3 Persistencia como adaptador

`SOPRO.Data` implementará los puertos definidos por Application y será propietario de:

- EF Core y SQLite.
- Esquema y migraciones.
- Transacciones.
- Mapeo entre entidades persistentes y contratos de aplicación.
- Ciclo de vida de `SOPROContext`.

El objetivo es un contexto por consulta o comando, no un contexto compartido por toda la sesión visual.

### 3.4 Reporting fuera de la UI

Cuando se migre el primer reporte se creará `SOPRO.Reporting`, si sigue siendo necesaria una separación física. Este proyecto contendrá los adaptadores PDF y Excel.

Los generadores recibirán un `ReportDocumentModel` neutral. No recibirán `DataGridView`, controles WPF, formularios ni ViewModels.

PDFsharp GDI y ClosedXML pueden mantenerse porque el producto seguirá siendo Windows-only. Portabilizarlos no forma parte de esta decisión.

### 3.5 WPF como UI objetivo

Se adopta WPF porque:

- El producto seguirá siendo Windows-only.
- Permite convivencia gradual con WinForms mediante `ElementHost` y `WindowsFormsHost`.
- Reduce cambios simultáneos en reporting, tipografías, instalador y actualización.
- Proporciona DataGrid, binding, MVVM y primitivas de dibujo adecuadas para presupuesto, Gantt y diseñador.

Avalonia se reconsiderará solamente si Linux o macOS se convierten en requisitos de producto.

### 3.6 Migración modular

WinForms y WPF coexistirán temporalmente. La unidad de migración será una vertical funcional completa:

```text
Request -> caso de uso -> resultado/efectos -> ViewModel o adaptador WinForms -> vista
```

No se migrarán handlers copiando su lógica al nuevo frontend. Antes de crear una vista WPF, WinForms debe consumir el mismo caso de uso headless.

### 3.7 Compatibilidad antes que corrección

La extracción inicial preservará:

- Valores decimales exactos.
- Orden de operaciones.
- Secuencias y receptor del residuo.
- Strings visibles.
- Cultura legacy.
- Tipo y momento observable de excepciones cuando sea contractual.
- Fallbacks y normalizaciones existentes.

Las correcciones de defectos conocidos se realizarán en cambios posteriores, documentados y aprobados por separado.

### 3.8 Composición mínima

Cada frontend tendrá una única raíz de composición. La composición manual es suficiente inicialmente; no se introduce un contenedor DI como requisito.

Los proyectos de UI podrán referenciar implementaciones concretas de Data y Reporting únicamente desde su código de composición. Views, ViewModels y adaptadores visuales dependerán de Application.

### 3.9 Sin proyecto genérico de contratos

No se crea `SOPRO.Contracts`, `SOPRO.Domain`, `SOPRO.Mediator` ni un proyecto Bootstrapper por anticipado.

Los contratos de casos de uso vivirán en `SOPRO.Application`. Los contratos propios del motor vivirán en `SOPRO.Calculation`.

Un proyecto temporal de abstracciones solo se aceptará mediante otro ADR si resulta imprescindible para cortar un ciclo de dependencias de forma segura.

### 3.10 Distribución y publicación

El estado aprobado para iniciar es:

- El repositorio fuente permanece privado.
- El paquete se restaura desde una carpeta local o un feed privado.
- Un proyecto consumidor fuera de `SOPRO.sln` valida que el paquete sea autocontenido.
- Los instaladores de la aplicación pueden seguir publicándose en `SOPRO-Releases`; eso no implica publicar el código fuente.

Quedan fuera de alcance hasta una decisión posterior:

- Hacer público el repositorio fuente.
- Publicar `SOPRO.Calculation` en NuGet.org.
- Publicar el paquete en un GitHub Release público.
- Configurar Source Link público.

Cualquiera de esas acciones requiere aprobación explícita y una revisión de seguridad, metadatos, licencias y soporte.

## 4. Arquitectura objetivo

```text
SOPRO.Calculation -> .NET / BCL

SOPRO.Core        -> .NET / BCL

SOPRO.Application -> SOPRO.Calculation
                     SOPRO.Core

SOPRO.Data        -> SOPRO.Application
                     SOPRO.Core
                     EF Core / SQLite

SOPRO.Reporting   -> SOPRO.Application
                     PDFsharp / ClosedXML

SOPRO.WinForms    -> SOPRO.Application
                     SOPRO.Data y Reporting solo en Composition

SOPRO.Wpf         -> SOPRO.Application
                     SOPRO.Data y Reporting solo en Composition
```

Durante la transición se permite que `SOPRO.Application` continúe referenciando Data mientras se mueven implementaciones dependientes de infraestructura. Esa dependencia no forma parte del estado final.

## 5. Reglas obligatorias

1. Solo existe un motor de cálculo.
2. Solo existe una implementación de cada caso de uso.
3. Ninguna UI instancia el motor.
4. Ninguna UI llama `SaveChanges`, usa `DbSet` o crea repositorios.
5. Ningún contrato de Application expone controles o entidades rastreadas.
6. Un comando mutable define una sola frontera transaccional.
7. No se usa un `DbContext` compartido dentro de `Task.Run`.
8. Las consultas, previews y búsquedas no escriben salvo que su contrato lo declare.
9. Los reportes no reconstruyen cálculo ni leen grids.
10. WinForms y WPF no escriben simultáneamente sobre el mismo proyecto.
11. La migración visual no cambia resultados contables ni esquema por conveniencia del frontend.
12. No se elimina una ruta legacy antes de demostrar paridad y rollback.

## 6. Consecuencias positivas

- El motor podrá empaquetarse y probarse de manera aislada.
- WinForms y WPF compartirán exactamente los mismos casos de uso.
- La persistencia podrá probarse sin crear formularios.
- Los ViewModels podrán probarse sin EF o ventanas.
- Los reportes dejarán de depender de la implementación del grid.
- Un futuro cambio de UI no requerirá volver a mover lógica de negocio.

## 7. Costos y consecuencias negativas

- Durante la transición coexistirán dos frontends y adaptadores temporales.
- Habrá que convertir entidades rastreadas a snapshots y DTOs.
- Presupuesto, matrices, Gantt y diseñador requieren reimplementación visual.
- La inversión de Application/Data exige mover servicios dependientes de `SOPROContext`.
- Mantener compatibilidad legacy requiere pruebas diferenciales y lógica temporal de adaptación.

## 8. Alternativas descartadas

### Avalonia ahora

Descartada porque no existe un requisito cross-platform y obligaría a cambiar simultáneamente reporting GDI, tipografías, instaladores y actualización.

### Reescritura completa de la UI

Descartada por el riesgo de reconstruir 43 formularios y controles complejos sin entregas intermedias ni comparación funcional continua.

### Compartir lógica copiando handlers

Descartada porque produciría dos implementaciones y repetiría el acoplamiento actual.

### Repositorio CRUD genérico como frontera

Descartado porque expone decisiones de persistencia y no define transacciones o reglas por caso de uso.

## 9. Verificación de cumplimiento

El ADR se considera implementado cuando:

- `SOPRO.Calculation` compila y se prueba aisladamente.
- Un consumidor fuera de la solución instala el paquete desde una fuente local o privada.
- Application no referencia EF, SQLite, Data, ClosedXML, WinForms ni WPF.
- WinForms y WPF no contienen accesos directos a persistencia.
- Ambas UIs consumen los mismos casos de uso.
- Los reportes reciben modelos neutrales.
- La aplicación WPF puede reemplazar WinForms sin cambiar base de datos ni cálculo.

## 10. Documentos relacionados

- [Plan 01: Desacoplamiento del núcleo y la aplicación](PLAN-01-DESACOPLAMIENTO-NUCLEO-APLICACION.md)
- [Plan 02: Migración modular de WinForms a WPF](PLAN-02-MIGRACION-WINFORMS-WPF.md)
- [Plan histórico de desacoplamiento del motor](PLAN-DESACOPLAMIENTO-MOTOR.md)
