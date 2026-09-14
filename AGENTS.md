# Instrucciones para SOPRO

## Entorno

- Usar el SDK indicado por `global.json`: .NET SDK `10.0.400`.
- Los proyectos usan `net8.0` o `net8.0-windows`.
- La solución `SOPRO.sln` contiene 8 proyectos activos.
- La solución debe abrirse, compilarse y probarse en Windows.

## Validación CI en Release

Desde la raíz del repositorio, ejecutar en este orden:

```powershell
dotnet restore SOPRO.sln
dotnet build SOPRO.sln -c Release --no-restore
dotnet test SOPRO.sln -c Release --no-build --no-restore
```

Las pruebas principales están en `SOPRO.Tests` y `SOPRO.Reporting.Tests`. Antes de liberar, ambas deben pasar.

## Arquitectura y límites

- `SOPRO.Calculation` es la biblioteca de cálculo independiente (`net8.0`), sin dependencias de ejecución NuGet ni referencias a otros proyectos SOPRO.
- `SOPRO.Application` contiene casos de uso, contratos neutrales, validación y orquestación.
- `SOPRO.Data` es el adaptador de EF Core/SQLite y propietario del esquema, migraciones y transacciones.
- `SOPRO.Reporting` contiene los adaptadores de PDF y Excel; recibe modelos neutrales de reporte.
- `SOPRO.WinForms` es la UI actual. La lógica de negocio no debe vivir en la UI ni duplicarse.
- WinForms y WPF deben consumir los mismos casos de uso; no se permiten rutas paralelas de persistencia o cálculo.
- Los cambios de arquitectura, nuevas capas, proyectos o inversiones de dependencia requieren un ADR aprobado. Las excepciones están documentadas en `ADR-001-ARQUITECTURA-OBJETIVO.md`.
- No publicar código, paquetes ni símbolos en servicios públicos sin aprobación explícita. El repositorio fuente permanece privado; los instaladores pueden publicarse en `SOPRO-Releases`.

## Goldens y artefactos

- Los goldens congelan compatibilidad y no deben modificarse para hacer pasar una prueba. Su hogar canónico de reporting es `SOPRO.Reporting.Tests/TestData/Goldens`.
- El escenario real `SOPRO.Tests/TestData/***REMOVED***.db` es inmutable; las pruebas trabajan sobre copias temporales de solo lectura.
- No editar artefactos generados (`bin/`, `obj/`, `TestResults/`, `publish/`, instaladores ni paquetes). Regenerarlos solo mediante su proceso documentado.

## Release

- Actualizar la versión en `SOPRO.WinForms.csproj` y `installer/SOPRO-InnoSetup.iss` antes de empaquetar.
- `build-release.ps1` publica `SOPRO.WinForms` para `win-x64` y genera el instalador con Inno Setup 6. Requiere Inno Setup instalado.
- Comando de empaquetado: `./build-release.ps1 -Version X.Y.Z`.
- El instalador generado es `SOPRO-Setup-X.Y.Z.exe` y se adjunta al release de `SOPRO-Releases` con etiqueta `vX.Y.Z`.

## Sincronización post-merge

- Tras confirmar que un PR se fusionó en `main` de GitHub, ejecutar `git pull --ff-only origin main` local y eliminar la rama temporal fusionada localmente y en `origin`; no borrar ramas no fusionadas ni la rama actual.

## Herramientas no aplicables

- No ejecutar lint, formatter ni codegen: no forman parte del flujo del repositorio.
