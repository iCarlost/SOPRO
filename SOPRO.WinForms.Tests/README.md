# SOPRO.WinForms.Tests

Proyecto de **andamiaje temporal** para N7-18a: caracteriza la salida real (goldens) del
"Catálogo de matrices" legacy sin invocar código de producción modificado.

## Alcance (N7-18a)

- `GeneradorExcelCatalogoMatrices` → snapshot semántico JSON (celdas, formatos, estructura,
  configuración de página) en `GeneradorExcelCatalogoMatricesTests`.
- `GeneradorPdfCatalogoMatrices` → hash del PDF **normalizado** (fechas/IDs fijos + parches
  byte a byte de las fuentes no-deterministas de PDFsharp/MigraDoc) + manifiesto en
  `GeneradorPdfCatalogoMatricesTests`.

Los goldens viven en su hogar canónico `SOPRO.Reporting.Tests/TestData/Goldens/` (N7-18c) y este
proyecto los consume como linked items (`TestInfrastructure/*.cs` + `TestData/Goldens/*` del
proyecto de Reporting); son el oráculo de N7-18c (renderer neutral en `SOPRO.Reporting`
validado contra el MISMO golden), antes del swap (N7-18d) y retiro del legacy (N7-18e). Los
sentinels del normalizador corren en ambas suites desde el mismo fuente.

## Flujo de trabajo

- **Modo normal:** los tests comparan contra la copia de `bin\...\TestData\Goldens`
  (copiada desde `SOPRO.Reporting.Tests/TestData/Goldens`).
- **Regenerar goldens:** `$env:SOPRO_REGENERATE_GOLDENS=1; dotnet test` escribe en la fuente
  canónica de `SOPRO.Reporting.Tests`. **Guarda anti-sobrescritura (oráculo):** solo escribe si
  el archivo no existe o si el contenido nuevo es idéntico al versionado; si difiere, el test
  FALLA (eliminar/renombrar el artefacto y regenerar explícitamente). Quitar la variable tras
  regenerar para volver a comparar.

## Determinismo del PDF

El PDF de PDFsharp 6.2.4-gdi no es determinista entre ejecuciones: el tag de subconjunto de
fuente (`/FontName/...`/`/BaseFont/...`) y el stream XMP (fechas + UUIDs `xmpMM`) cambian cada
vez. El normalizador actúa SOLO dentro de esos campos: tags en `/BaseFont`/`/FontName`, fechas y
UUIDs únicamente dentro de sus elementos XMP (paquete `xpacket`). Todo patrón idéntico fuera de
esos campos queda intacto (test centinela) y toda forma no reconocida provoca fallo rápido. El
test de determinismo exige como contrato que los normalizados sean byte a byte idénticos; que
los originales difieran se registra solo como diagnóstico (si PDFsharp fuera determinista el
hash normalizado seguiría siendo válido).

## Borrado

Este proyecto es andamiaje; se elimina en N7-18e. Los goldens y la infraestructura ya
migraron a `SOPRO.Reporting.Tests` (N7-18c); este proyecto queda solo como consumidor
temporal de esos artefactos hasta el swap.