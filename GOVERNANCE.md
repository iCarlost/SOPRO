# Gobernanza de SOPRO

## Titularidad

SOPRO es mantenido por CEPM. El titular del repositorio toma las decisiones finales sobre direccion del proyecto, revision de pull requests y administracion de releases.

## Decisiones arquitectonicas

Los cambios estructurales, nuevas capas, proyectos o inversiones de dependencia requieren un **Architecture Decision Record (ADR)** aprobado antes de implementarse.

Los ADRs se registran como parte del proceso de revision de los cambios estructurales.

## Modelo de contribuciones

Toda contribucion comienza con un **Issue**. Ver [CONTRIBUTING.md](CONTRIBUTING.md) para el flujo detallado.

### Pull Requests

- Los PRs requieren al menos una aprobacion antes de merge.
- Los tests deben pasar antes de merge.
- Los PRs deben estar vinculados a un Issue abierto o aprobado.

## Revision de codigo

Los maintainers revisan los PRs buscando:

- Cumplimiento de reglas de arquitectura.
- Ausencia de datos sensibles o reales.
- Pruebas unitarias para logica de calculo.
- Compatibilidad con la version estable.

## Release

El proceso de release esta documentado en [README.md](README.md).

## Seguridad

Las vulnerabilidades se reportan via **GitHub Private Vulnerability Reporting**. Ver [SECURITY.md](SECURITY.md).

## Conflicto de intereses

Los maintainers no deben usar su posicion para ventaja personal o de terceros. Las decisiones se toman en beneficio del proyecto y su comunidad.
