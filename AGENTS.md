# Instrucciones para SOPRO

## Entorno

- Usar el SDK indicado por `global.json`: .NET SDK `10.0.400`.
- Los proyectos usan `net8.0` o `net8.0-windows`.
- La solucion `SOPRO.sln` contiene 8 proyectos activos.
- La solucion debe abrirse, compilarse y probarse en Windows.

## Validacion CI en Release

Desde la raiz del repositorio, ejecutar en este orden:

```powershell
dotnet restore SOPRO.sln
dotnet build SOPRO.sln -c Release --no-restore
dotnet test SOPRO.sln -c Release --no-build --no-restore
```

Las pruebas principales estan en `SOPRO.Tests` y `SOPRO.Reporting.Tests`. Antes de liberar, ambas deben pasar.

## Arquitectura y limites

- `SOPRO.Calculation` es la biblioteca de calculo independiente (`net8.0`), sin dependencias de ejecucion NuGet ni referencias a otros proyectos SOPRO.
- `SOPRO.Application` contiene casos de uso, contratos neutrales, validacion y orquestacion.
- `SOPRO.Data` es el adaptador de EF Core/SQLite y propietario del esquema, migraciones y transacciones.
- `SOPRO.Reporting` contiene los adaptadores de PDF y Excel; recibe modelos neutrales de reporte.
- `SOPRO.WinForms` es la UI actual. La logica de negocio no debe vivir en la UI ni duplicarse.
- WinForms y WPF deben consumir los mismos casos de uso; no se permiten rutas paralelas de persistencia o calculo.
- Los cambios de arquitectura, nuevas capas, proyectos o inversiones de dependencia requieren un ADR aprobado. Las excepciones estan documentadas en `ADR-001-ARQUITECTURA-OBJETIVO.md`.
- No publicar codigo, paquetes ni simbolos en servicios publicos sin aprobacion explicita. El repositorio fuente permanece privado; los instaladores pueden publicarse en `SOPRO-Releases`.

## Goldens y artefactos

- Los goldens congelan compatibilidad y no deben modificarse para hacer pasar una prueba. Su hogar canonico de reporting es `SOPRO.Reporting.Tests/TestData/Goldens`.
- El escenario sintético `SOPRO.Tests/TestData/proyecto-sintetico-vial-demo.db` es inmutable; las pruebas trabajan sobre copias temporales de solo lectura.
- No editar artefactos generados (`bin/`, `obj/`, `TestResults/`, `publish/`, instaladores ni paquetes). Regenerarlos solo mediante su proceso documentado.

## Release

- Actualizar la version en `SOPRO.WinForms.csproj` y `installer/SOPRO-InnoSetup.iss` antes de empaquetar.
- `build-release.ps1` publica `SOPRO.WinForms` para `win-x64` y genera el instalador con Inno Setup 6. Requiere Inno Setup instalado.
- Comando de empaquetado: `./build-release.ps1 -Version X.Y.Z`.
- El instalador generado es `SOPRO-Setup-X.Y.Z.exe` y se adjunta al release de `SOPRO-Releases` con etiqueta `vX.Y.Z`.

## Sincronizacion post-merge

- Tras confirmar que un PR se fusiono en `main` de GitHub, ejecutar `git pull --ff-only origin main` local y eliminar la rama temporal fusionada localmente y en `origin`; no borrar ramas no fusionadas ni la rama actual.

## Herramientas no aplicables

- No ejecutar lint, formatter ni codegen: no forman parte del flujo del repositorio.
