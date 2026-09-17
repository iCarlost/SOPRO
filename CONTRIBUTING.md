# Contribuyendo a SOPRO

Gracias por tu interes en contribuir a SOPRO. Lee esta guia antes de enviar un cambio.

## Modelo de contribucion: Issues-first

Toda contribucion comienza con un **Issue** antes de enviar codigo:

1. Abre un Issue describiendo el problema o la mejora.
2. Espera la confirmacion o aprobacion del maintainers.
3. Crea una rama descriptiva: `fix/nombre-del-arreglo` o `feature/nombre-de-la-mejora`.
4. Realiza tus cambios siguiendo las reglas del proyecto.
5. Ejecuta las pruebas unitarias.
6. Abre un Pull Request vinculado al Issue.

No se aceptan Pull Requests sin un Issue asociado abierto o aprobado.

## Reglas de arquitectura

- No duplicar logica; reutilizar servicios existentes.
- No mover logica de negocio a la UI.
- El motor de calculo debe ser unico; no crear rutas paralelas de calculo.
- No romper la compatibilidad de calculo ni la precision de pantalla.
- Nuevos proyectos, capas o inversiones de dependencia requieren un ADR aprobado.
- WinForms y WPF consumen los mismos casos de uso; no se permiten rutas paralelas de persistencia o calculo.
- Ningun cambio debe publicar codigo, paquetes o simbolos en servicios publicos sin aprobacion explicita.

## Estandares de codigo

- C# con tipado explicito y `nullable` habilitado.
- Mensajes de UI en espanol, respetando la terminologia existente.
- Sin comentarios superfluos; el codigo debe ser autoexplicativo.

## Pruebas

Toda logica de calculo debe incluir pruebas unitarias en el proyecto de tests correspondiente.

```powershell
dotnet test SOPRO.sln -c Release
```

## Versionado

- Cambios funcionales: minor
- Correcciones: patch
- Cambios estructurales: major

El versionado se define en `SOPRO.WinForms.csproj` y en `installer/SOPRO-InnoSetup.iss`.

## Reportar problemas

Usa el template de issues del repositorio e incluye:

- Version de SOPRO (visible en la barra de estado).
- Pasos para reproducir.
- Comportamiento esperado vs. obtenido.
- Logs o capturas si aplican.
