# Contribuyendo a SOPRO

Gracias por tu interés en contribuir a SOPRO. Antes de enviar un cambio, lee esta guía.

## Cómo contribuir

1. Haz un fork del repositorio.
2. Crea una rama descriptiva: `fix/nombre-del-arreglo` o `feature/nombre-de-la-mejora`.
3. Realiza tus cambios siguiendo las reglas de arquitectura del proyecto.
4. Ejecuta las pruebas unitarias antes de enviar el PR.
5. Abre un Pull Request hacia `main` describiendo el cambio y cómo se probó.

## Reglas de arquitectura

* No crear nuevas capas (Core, Application, Data, WinForms).
* No duplicar lógica; reutilizar servicios existentes.
* No mover lógica de negocio a la UI.
* El motor de cálculo debe ser único (`MotorCalculoSopro`).
* No romper la compatibilidad de cálculo ni la precisión de pantalla.

## Estándares de código

* C# con tipado explícito y `nullable` habilitado.
* Mensajes de UI en español, respetando la terminología existente.
* Sin comentarios superfluos; el código debe ser autoexplicativo.

## Pruebas

Toda lógica de cálculo debe incluir pruebas unitarias en `SOPRO.Tests`.

```powershell
dotnet test SOPRO.sln
```

## Versionado

* Cambios funcionales → minor
* Correcciones → patch
* Cambios estructurales → major

El versionado se define en `SOPRO.WinForms.csproj` y en `installer/SOPRO-InnoSetup.iss`.

## Reportar problemas

Usa el template de issues del repositorio e incluye:

* Versión de SOPRO (visible en la barra de estado).
* Pasos para reproducir.
* Comportamiento esperado vs. obtenido.
* Logs o capturas si aplican.
