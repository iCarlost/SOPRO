# Plan de Desacoplamiento del Motor de Cálculo de SOPRO

**Estado:** documento histórico reemplazado  
**Distribución actual:** repositorio fuente privado; paquete de cálculo no publicado

Este archivo se conserva únicamente como punto de entrada para enlaces o referencias anteriores. El contenido original fue reemplazado porque mezclaba diagnóstico, extracción, correcciones funcionales y supuestos de publicación que ya no forman parte de una única fase ejecutable.

No debe utilizarse para implementar cambios.

La documentación vigente es:

1. [ADR-001: Arquitectura objetivo](ADR-001-ARQUITECTURA-OBJETIVO.md)
2. [Plan 01: Desacoplamiento del núcleo y la aplicación](PLAN-01-DESACOPLAMIENTO-NUCLEO-APLICACION.md)
3. [Plan 02: Migración modular de WinForms a WPF](PLAN-02-MIGRACION-WINFORMS-WPF.md)

Política vigente de distribución:

- El código fuente permanece privado.
- `SOPRO.Calculation` se validará primero como `.nupkg` local o mediante un feed privado.
- Publicar en NuGet.org, abrir el repositorio o exponer símbolos públicos requiere una decisión posterior y aprobación explícita.
- El repositorio `SOPRO-Releases` distribuye instaladores de la aplicación y no representa la visibilidad del código fuente.
