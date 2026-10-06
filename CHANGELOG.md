# Changelog

Todos los cambios notables en SOPRO se documentan en este archivo.

El formato se basa en [Keep a Changelog](https://keepachangelog.com/es/1.1.0/).

## [No publicado]

### Agregado
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
