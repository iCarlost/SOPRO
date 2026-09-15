# N7 — Matriz de cierre 6.1

## Dictamen

**Cierre N7: GO.** Las capturas finales son verdes (advertencias `0/0` y
compilación/pruebas `656/656` en Debug y Release, Gate N0 `44/44`, catálogo
`40/40` y consumer package verde) y el usuario decidió explícitamente el cierre
formal. Nota factual conservada: el baseline histórico `421/446` no es
reproducible con la base y el entorno capturados, y el baseline final es `0/0`;
el GO acepta esa limitación de reproducibilidad histórica sin reclasificar
`421/446` como advertencia vigente.

## Matriz de evidencia sanitizada

| Área | Evidencia final | Estado | Límite o riesgo |
|---|---|---|---|
| Baseline histórico | `421/446`, no reproducible | Informativo | No se puede atribuir la diferencia a una remediación concreta |
| Advertencias Debug | `0/0` | Verde | Log completo externo; identificado solo por hash |
| Advertencias Release | `0/0` | Verde | Log completo externo; identificado solo por hash |
| Compilación/pruebas Debug | `656/656` | Verde | Conteo independiente del inventario de warnings |
| Compilación/pruebas Release | `656/656` | Verde | Conteo independiente del inventario de warnings |
| Gate N0 | `44/44` | Verde | Escenarios dorados; no sustituye la comparación histórica no reproducible |
| Catálogo | `40/40` | Verde | Evidencia de la suite de catálogo |
| Consumer package | Verde | Verde | Validación externa del paquete; distribución permanece privada |
| Integridad protegida | Goldens y fixture con hashes versionados | Protegida | No editar fuentes, goldens ni fixture |

## Gates y acciones N7

1. Registrar la captura final `0/0` para Debug y Release.
2. Registrar por separado los gates `656/656`, N0 `44/44`, catálogo `40/40` y
   el consumer package verde.
3. Mantener el baseline `421/446` como histórico no reproducible, sin
   reclasificarlo como warning actual.
4. Conservar logs externos fuera del repositorio y versionar únicamente sus
   hashes e identificadores lógicos sanitizados.
5. El diff incluye fuentes Application, tests de Programación,
   `.github/workflows/ci.yml`, `PLAN-01` y documentación. WPF, goldens,
   fixture real, reporting contable y `.slim` permanecen sin cambios.
6. Publicar el cierre con commit Conventional Commit y push de la rama de
   cierre (sin fusionar a `main` en este paso).

## Logs externos

No se versionan rutas personales ni contenido de logs. Los artefactos externos
se identifican mediante URI lógica y SHA-256:

| Configuración | URI lógica | SHA-256 |
|---|---|---|
| Debug | `external://sopro-warning-audit/final/build-Debug.warnings.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| Release | `external://sopro-warning-audit/final/build-Release.warnings.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |

El hash corresponde a un log vacío, coherente con `0/0`; no prueba por sí solo
la reproducibilidad del baseline histórico.

## Hashes protegidos

Los siguientes hashes son controles de integridad versionados; no autorizan ni
implican edición de los artefactos:

| Artefacto | SHA-256 |
|---|---|
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesExcel.Legacy.json` | `c4a7b0b8dd2cff703605486b54d3ec27289116b5fc98e07483513a6382b27b6c` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesExcel.Real.Legacy.json` | `9eed3c08028a11668012ef5f3ade63095b1297d0305e0ef3d78810daa05e458b` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesPdf.Legacy.manifest.json` | `bf63fbc1488887459b41bca62a6559f492385a6950dce0d411b608e0a388c79e` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesPdf.Legacy.sha256` | `6897154dc6157cb0ca8a09666aab24033777f362087bfcf7359654a7691fbb02` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesPdf.Real.Legacy.manifest.json` | `fed9914ad04b29d89840296b453c55dd91186f5fde5ea9db2aea72057f238478` |
| `SOPRO.Reporting.Tests/TestData/Goldens/CatalogoMatricesPdf.Real.Legacy.sha256` | `5b111e1a6b7b27fd5e7633424b3ed44941753d799581cb65cf6655db920c8c70` |
| `SOPRO.Tests/TestData/***REMOVED***.db` | `e79e19476c1377ccbf74499091ebd8779186756dac43c2917215b7c42365729e` |

La misma lista queda consignada en `N7-INVENTARIO-ADVERTENCIAS.md` como
registro canónico del inventario.

## Riesgos abiertos

- No existe una captura reproducible del contexto que originó `421/446`.
- `0/0` demuestra el estado de la captura final, no una explicación causal del
  baseline.
- Un GO global requiere evidencia histórica reproducible o una decisión
  explícita que acepte esta limitación: el usuario tomó esa decisión explícita,
  por lo que N7 queda cerrado GO con la limitación histórica documentada.

## Alcance y verificación

WPF, goldens, fixture real, reporting contable y `.slim` permanecen sin cambios.
La verificación de este cambio es `git diff --check`; el cierre se publica con
commit Conventional Commit y push de la rama de cierre.
