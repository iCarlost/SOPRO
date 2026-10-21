# Changelog

Todos los cambios notables en SOPRO se documentan en este archivo.

El formato se basa en [Keep a Changelog](https://keepachangelog.com/es/1.1.0/).

## [No publicado]

## [1.10.0] - 2026-10-21

### Agregado
- Botón «Apoyar» en el selector de proyectos que abre la página Ko-fi del proyecto en el navegador.
- Formato de celdas estilo Excel en todos los reportes: desde el ribbon se puede formatear una columna completa (clic en el encabezado) o una seleccion de celdas (rango contiguo con Shift, celdas o rangos discontinuos con Ctrl), con indicador de destino ("selección de N celda(s)"), aviso de estado mixto y deshacer/rehacer (Ctrl+Z / Ctrl+Y) donde un rango aplicado es UNA sola accion. Disponible en Presupuesto, Indirectos, APU/Matrices, Explosion de Insumos, Programas de Obra/Insumos, Financiamiento y los catalogos de Materiales, Mano de Obra, Herramientas y Maquinaria.
- Formato por celda y por rol de fila persistido en el proyecto, con precedencia override de celda > estilo de rol > estilo de columna > fila alterna > estilo de tabla, aplicado de forma identica en pantalla, PDF y Excel via snapshot neutral de reporte (los generadores no leen el grid).
- Migracion M014 (esquema `2026.04.4`): tablas `FormatosCeldaReporte` (override por celda) y `ConfiguracionesEstiloFilaReporte` (estilo por rol), con indices unicos, `ClaveVista` "" como vista por defecto y borrado en cascada con el proyecto.
- Guia de la feature en [docs/formato-celdas-reportes.md](docs/formato-celdas-reportes.md) y documentacion de M014 en [SCHEMA_MANTENIMIENTO.md](SCHEMA_MANTENIMIENTO.md).
- Politica de soporte (best-effort, ultima version estable).
- Canal de reporte de vulnerabilidades via Private Vulnerability Reporting.
- Codigo de Conducta para contribuyentes.
- Documentacion de gobernanza del proyecto.
- Templates de Issues y Pull Requests.
- CODEOWNERS para revision automatica.
- CHANGELOG.md.

### Cambiado
- README actualizado con documentacion publica del proyecto.
- CONTRIBUTING.md actualizado con modelo Issues-first.
- LICENSE actualizado con copyright CEPM.
- THIRD-PARTY-NOTICES.md actualizado con alcance de la licencia.

## [1.9.1] - 2026-10-14

### Corregido
- Conversión de números a letras siempre en mayúsculas (VEINTINUEVE, DIECISEIS).
- Redondeo de centavos con acarreo correcto en la conversión a letras.
- Paridad PDF↔Excel del Importe de filas agrupadoras.
- Ortografía "CÁLCULO" en el Excel FSR.
- Undo/redo del formato por celdas en catálogos y Explosión.
- Factor P.U. del panel de matrices delega en el servicio canónico.

### Cambiado
- Conversor número-a-letra unificado en `BudgetPricingService` (fuente única).
- Excepciones antes silenciosas ahora registran diagnóstico.

### Eliminado
- Código muerto: `NumeroALetrasHelper`, conversores duplicados de `BudgetLoadService`, `CargoAdicional.CalcularMonto`, `FormPresupuesto.CalcularColumnaCalculada`, `GeneradorExcelPresupuesto.AgregarRecursivo`/`EsDescendiente`.

## [1.8.0] - 2026

### Añadido
- Extension de proyecto propia `.soproproj`: los proyectos nuevos se crean con esta extension, con compatibilidad `.db` (los existentes siguen abriéndose y `.sopro` se acepta como alias) y apertura de proyectos por doble clic mediante asociacion por usuario (per-user, sin permisos de administrador).

### Corregido
- Icono del tipo de archivo `.soproproj`: se corrige el registro `DefaultIcon` para que la ruta del ejecutable y el indice de icono queden entrecomillados correctamente, mostrando el icono de la aplicacion en el Explorador.
- Parpadeo del selector al abrir un proyecto por doble clic: el proyecto se abre antes de mostrar cualquier ventana, evitando el parpadeo de la pantalla de seleccion.

## [1.7.2] - 2026

### Corregido
- Saneo de tipos de dependencia inválidos en el Programa de obra: los valores fuera de rango (p. ej. 0, procedentes de proyectos externos) se normalizan a "Fin → Inicio" (FS) al cargar, evitando el error que impedía abrir el programa de obra. La normalización es en memoria y no reescribe los datos históricos.

## [1.7.1] - 2026

### Corregido
- Restaurado el icono de marca en el ejecutable (`SOPRO.WinForms.exe`) y en el instalador. El icono se referencia como asset local (fuera del repositorio publico) y solo se embebe en build cuando esta presente.

## [1.7.0] - 2026

### Mejorado
- Panel APU embebido responsivo: layout declarativo (TableLayoutPanel/FlowLayoutPanel) sin solapes ni controles encimados en laptop.
- Botones (Agregar / Guardar / Cancelar) con altura natural y textos alineados.
- Etiqueta "Agregar:" alineada con los botones.

### Cambiado
- Estilo de selección unificado en los grids (fila azul claro + contorno azul de la celda activa) en presupuesto, matriz embebida y selector APU.
- Descripciones de matriz y concepto ampliadas hasta 2000 caracteres.

## [1.6.0] - 2026

### Nota
- Version actual del instalador disponible en `SOPRO-Releases`.

### Cambiado
- Motor de calculo desacoplado en `SOPRO.Calculation`.
- Frontera de Application con casos de uso headless.
- Persistencia encapsulada con contexto por operacion.
- Consumidores migrados al motor y casos de uso.

### Integrado
- M modulos avanzados extraidos individualmente (N7).
