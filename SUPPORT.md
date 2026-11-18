# Soporte

## Politica de soporte

El soporte de SOPRO es **best-effort** y se aplica exclusivamente a la **ultima version estable** publicada.

No se garantiza soporte ni correcciones para versiones anteriores a la ultima estable.

## Canal de soporte

El canal oficial de soporte es a traves de los **Issues** del repositorio en GitHub.

## Alcance del soporte

- Uso documentado de la aplicacion.
- Errores y problemas reproducibles en la ultima version estable.
- Preguntas sobre funcionalidad documentada.

## Fuera de alcance

- Configuraciones personalizadas o integraciones no estandar.
- Versiones anteriores a la ultima estable.
- Modificaciones o compilaciones personalizadas del codigo fuente.
- Soporte comercial o SLA contractual.

## Reportar problemas

Al abrir un Issue de soporte, incluye:

1. Version de SOPRO (visible en la barra de estado).
2. Sistema operativo y version de .NET.
3. Pasos para reproducir el problema.
4. Comportamiento esperado vs. obtenido.
5. Una captura de pantalla solo si no muestra informacion sensible.

### Registros de diagnóstico

SOPRO guarda registros localmente en `%LOCALAPPDATA%\SOPRO\logs\`. La retención es best-effort: hasta 1 MiB por archivo, 10 archivos y 7 días. Incluyen datos estructurados de eventos (por ejemplo, fecha, nivel, origen, operación, versión y correlación); las excepciones se limitan al tipo y otros datos estructurales. No se guardan mensajes ni trazas de excepción. Los registros no se envían automáticamente.

Puedes revisarlos y exportar manualmente una instantánea desde **Registros locales** en la pantalla principal. La vista previa corresponde al archivo exportado; revisa su contenido antes de adjuntarlo. Una exportación puede contener información sensible indirecta y una copia exportada no se elimina al borrar los registros de SOPRO. **No publiques registros en Issues públicos.** Si el equipo de soporte los necesita, comparte solo la copia revisada por un canal privado acordado. También puedes borrar los registros locales desde esa pantalla; el borrado no elimina copias exportadas.

## Seguridad

Para reportar vulnerabilidades, consulta [SECURITY.md](SECURITY.md). Utiliza el canal de **Private Vulnerability Reporting** de GitHub, no los Issues publicos.
