# Contribuyendo a SOPRO

Gracias por tu interés en contribuir a SOPRO. Antes de enviar un cambio, lee esta guía.

## Cómo contribuir

Mientras el repositorio permanezca privado, el flujo de trabajo es interno:

1. Verifica que tienes acceso al repositorio y al issue o tarea correspondiente.
2. Crea una rama descriptiva en el repositorio: `fix/nombre-del-arreglo` o `feature/nombre-de-la-mejora`.
3. Realiza tus cambios siguiendo las reglas de arquitectura del proyecto.
4. Ejecuta las pruebas unitarias antes de enviar el PR.
5. Abre un Pull Request hacia `main` describiendo el cambio y cómo se probó.

## Reglas de arquitectura

* No duplicar lógica; reutilizar servicios existentes.
* No mover lógica de negocio a la UI.
* El motor de cálculo debe ser único; durante la transición `MotorCalculoSopro` actúa como fachada compatible.
* No romper la compatibilidad de cálculo ni la precisión de pantalla.
* Nuevos proyectos, capas o inversiones de dependencia requieren un ADR aprobado.
* Las excepciones aprobadas para la modernización están descritas en `ADR-001-ARQUITECTURA-OBJETIVO.md`.
* WinForms y WPF deben consumir los mismos casos de uso; no se permiten rutas paralelas de persistencia o cálculo.
* Ningún cambio debe publicar código, paquetes o símbolos en servicios públicos sin aprobación explícita.

## Estándares de código

* C# con tipado explícito y `nullable` habilitado.
* Mensajes de UI en español, respetando la terminología existente.
* Sin comentarios superfluos; el código debe ser autoexplicativo.

## Pruebas

Toda lógica de cálculo debe incluir pruebas unitarias en el proyecto de tests correspondiente. Las pruebas de compatibilidad e integración de SOPRO permanecen en `SOPRO.Tests`; las pruebas puras del paquete vivirán en `SOPRO.Calculation.Tests` cuando se cree.

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
