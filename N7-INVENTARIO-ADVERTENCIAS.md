# N7 — Inventario de advertencias

## Baseline 0.1

Baseline capturado el 2026-09-14, antes de cualquier remediación de advertencias.
Este documento no contiene rutas personales, contenido de bases de datos ni
variables de entorno.

### Identidad reproducible

- Rama: `main`
- Commit `HEAD`: `2cb04a3db72be5a0d47b48df991de2e2d812360e`
- SDK .NET seleccionado por `global.json`: `10.0.400`
- `global.json`: `rollForward=latestPatch`, `allowPrerelease=false`

### Estado de Git

Estado observado:

```text
 M .github/workflows/ci.yml
 M PLAN-01-DESACOPLAMIENTO-NUCLEO-APLICACION.md
 M SOPRO.Application/Services/ExplosionInsumosService.cs
 M SOPRO.Application/Services/Programacion/ActivityNetworkAdapter.cs
 M SOPRO.Application/Services/Programacion/CalendarioCache.cs
 M SOPRO.Application/Services/Programacion/ProgramacionCalculationService.cs
 M SOPRO.Application/Services/Programacion/ProgramacionDistributionService.cs
 M SOPRO.Application/Services/Programacion/ProgramacionGenerationService.cs
 M SOPRO.Application/Services/Programacion/ProgramacionInsumosService.cs
 M SOPRO.Application/Services/Programacion/ProgramacionLoadService.cs
 M SOPRO.Application/Services/Programacion/ProgramacionPeriodHelper.cs
 M SOPRO.Application/Services/Programacion/ProgramacionPersistenceService.cs
 M SOPRO.Application/Services/Programacion/ProgramacionSynchronizationService.cs
 M SOPRO.Tests/Services/Programacion/CalendarioCharacterizationTests.cs
?? .bob/
?? .ignore
?? .opencode/
?? SOPRO.Application/Services/InsumosEntitySnapshotAdapter.cs
```

Todos los cambios anteriores son preexistentes y deben preservarse sin
alteración. El único cambio de esta ejecución es este inventario.

### Rutas protegidas

No modificar durante N7-0.1:

- `.github/workflows/ci.yml`
- `PLAN-01-DESACOPLAMIENTO-NUCLEO-APLICACION.md`
- `SOPRO.Reporting.Tests/TestData/Goldens/`
- `SOPRO.Tests/TestData/proyecto-sintetico-vial-demo.db`
- Artefactos generados: `bin/`, `obj/`, `TestResults/`, `publish/`

El fixture sintético es inmutable; las pruebas deben usar copias temporales de solo
lectura. Los goldens son la referencia congelada de compatibilidad.

### Hashes SHA-256 de goldens

| Ruta relativa | SHA-256 |
|---|---|
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesExcel.Legacy.json` | `c4a7b0b8dd2cff703605486b54d3ec27289116b5fc98e07483513a6382b27b6c` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesExcel.ProyectoSintetico.Legacy.json` | `53831d4f4db9b67774721886efb5cee47b3453abb3c491f18066fe950021928f` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesPdf.Legacy.manifest.json` | `bf63fbc1488887459b41bca62a6559f492385a6950dce0d411b608e0a388c79e` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesPdf.Legacy.sha256` | `6897154dc6157cb0ca8a09666aab24033777f362087bfcf7359654a7691fbb02` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesPdf.ProyectoSintetico.Legacy.manifest.json` | `312004292d8c4f20f9244a3f0abd79a4a192eefb4c3d0ec49b4ec9b8214240ab` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesPdf.ProyectoSintetico.Legacy.sha256` | `214afff8cf856011a219f9bdbea578098c247c9810db0644488a91c3447b915f` |

### Hash SHA-256 del fixture sintético

| Ruta relativa | SHA-256 |
|---|---|
| `SOPRO.Tests/TestData/proyecto-sintetico-vial-demo.db` | `92c1e7520145a7e34236b668d2f425afbc432a0dcb20d806e6b20a90bdc6a272` |

## Alcance de 0.1

- Capturar y documentar el baseline sanitizado.
- No ejecutar build, restore, test ni modificación de código productivo.
- No modificar CI ni `PLAN-01`.
- Preservar el baseline histórico como no reproducible; el usuario aceptó el cierre GO y la publicación.

## Baseline 0.2 — auditoría de builds

Captura realizada el 2026-09-14 con SDK `10.0.400`, después de `dotnet restore
SOPRO.sln`. Los builds se ejecutaron serialmente con `-m:1` y `--no-restore`.
Los archivos contienen únicamente líneas de advertencia; no se conservaron logs
de versión completa ni binlogs. Los contadores son preliminares: emitido cuenta
líneas de advertencia y único cuenta líneas distintas.

| Configuración | Resultado | Ruta relativa del log | SHA-256 | Emitido | Único preliminar |
|---|---:|---|---|---:|---:|
| Debug | 0 | `%TEMP%/sopro-warning-audit/20260914-155349/build-Debug.warnings.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | 0 | 0 |
| Release | 0 | `%TEMP%/sopro-warning-audit/20260914-155349/build-Release.warnings.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | 0 | 0 |

Restore y ambos builds finalizaron con código 0. Los logs existen fuera del
repositorio; `git status` no reporta logs ni binlogs.

## Cierre 0.3 — inventario emitido

Las capturas 0.2 de Debug y Release no contienen advertencias. El contador
emitido/único es `0/0` en ambas configuraciones y no hay filas de warning que
clasificar o inventariar.

Los hashes SHA-256 y las rutas relativas de los logs externos están consignados
en la tabla de 0.2. Los logs permanecen fuera del repositorio y las rutas están
sanitizadas; este inventario no incluye rutas personales, contenido de bases de
datos ni variables de entorno.

Los totales históricos `421/446` no son reproducibles en la base capturada con
las capturas 0.2 y, por tanto, no se presentan como inventario vigente ni se
les asigna clasificación.

## Cierre N7 — captura final 6.1

Esta sección sustituye cualquier lectura de los contadores históricos como
resultado actual. El baseline histórico `421/446` queda conservado únicamente
como dato no reproducible; no se ha inventariado ni reclasificado como warning
vigente.

### Resultado reproducible de advertencias

| Captura | Configuración | Advertencias emitidas | Únicas | Resultado |
|---|---|---:|---:|---|
| Final | Debug | 0 | 0 | Verde |
| Final | Release | 0 | 0 | Verde |

Los logs de advertencias permanecen fuera del repositorio. Se identifican solo
por hash SHA-256 y por una ruta lógica sanitizada, sin rutas personales:

| Captura | Configuración | Identificador lógico | SHA-256 | Estado |
|---|---|---|---|---|
| Final | Debug | `external://sopro-warning-audit/final/build-Debug.warnings.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | Verificado: 0/0 |
| Final | Release | `external://sopro-warning-audit/final/build-Release.warnings.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | Verificado: 0/0 |

### Gates y acciones de N7

- Cerrar el inventario de advertencias con captura final `0/0` en Debug y
  Release.
- Mantener `421/446` como baseline histórico no reproducible, sin afirmar que
  fue corregido por una remediación concreta.
- Conservar fuera del repositorio los logs completos; versionar únicamente
  contadores, identificadores lógicos y hashes.
- No modificar fuentes, goldens, CI, `PLAN-01` ni `.slim` durante este cierre.
- Mantener separados los conteos de compilación y los conteos de advertencias:
  la evidencia de pruebas final es `656/656` en Debug y `656/656` en Release;
  esto no convierte el baseline histórico en reproducible.

### Riesgos y límites

- La ausencia de advertencias en la captura final no reconstruye el entorno que
  produjo `421/446`; queda una limitación de reproducibilidad histórica.
- Los hashes de goldens y del fixture real consignados arriba son controles de
  integridad, no una autorización para editar esos artefactos.
- El baseline histórico `421/446` permanece como dato no reproducible y no se
  presenta como comparación reproducible. El usuario aceptó el cierre GO de N7 y
  la publicación.
