# SOPRO.WinForms.Tests

Proyecto de **andamiaje temporal** para N7-18a: caracteriza la salida real (goldens) del
"Catálogo de matrices" legacy sin invocar código de producción modificado.

## Alcance (N7-18a)

- `GeneradorExcelCatalogoMatrices` → snapshot semántico JSON (celdas, formatos, estructura,
  configuración de página) en `GeneradorExcelCatalogoMatricesTests`.
- `GeneradorPdfCatalogoMatrices` → hash del PDF **normalizado** (fechas/IDs fijos + parches
  byte a byte de las fuentes no-deterministas de PDFsharp/MigraDoc) + manifiesto en
  `GeneradorPdfCatalogoMatricesTests`.

Los goldens viven en `TestData/Goldens/` y son el oráculo de N7-18c (renderer neutral en
`SOPRO.Reporting` validado contra el MISMO golden), antes del swap (N7-18d) y retiro del
legacy (N7-18e).

## Flujo de trabajo

- **Modo normal:** los tests comparan contra la copia de `bin\...\TestData\Goldens`.
- **Regenerar goldens:** `$env:SOPRO_REGENERATE_GOLDENS=1; dotnet test` — escribe en la
  fuente (sobrescribe `TestData\Goldens\*`) y pasa sin comparar. Quitar la variable tras
  regenerar para volver a comparar.

## Determinismo del PDF

El PDF de PDFsharp 6.2.4-gdi no es determinista entre ejecuciones: el tag de subconjunto de
fuente (`/FontName LWKAJW+Arial`) y el stream XMP (fechas + UUIDs `xmpMM`) cambian cada vez.
Mientras el test de determinismo ("2 generaciones originales deben diferir") valide que los
originales NO son iguales, el normalizador aplica parches de longitud fija a los bytes del
flujo y el hash de los normalizados debe coincidir entre procesos.

## Borrado

Este proyecto es andamiaje; se elimina en N7-18e y los goldens migran a
`SOPRO.Reporting.Tests`.