# Third-Party Notices

SOPRO usa las siguientes bibliotecas de terceros. Cada una conserva su licencia original.

| Paquete | Version | Licencia |
|---|---|---|
| ClosedXML | 0.102.2 | MIT |
| PDFsharp-MigraDoc-GDI | 6.2.4 | MIT |
| Microsoft.EntityFrameworkCore | 8.x | MIT |
| Microsoft.EntityFrameworkCore.Design | 8.0.0 | MIT |
| Microsoft.Extensions.* (transitivas) | 8.x | MIT |
| .NET Runtime | 8.x | MIT |
| Inno Setup (solo empaquetado del instalador) | 6.x | Inno Setup License |

Los textos completos de las licencias estan disponibles en los sitios oficiales de cada proyecto.

---

## Recursos de identidad SOPRO

Los recursos graficos de identidad de SOPRO (iconos y logotipos) son de **autoria propia** y
no provienen de terceros.

- **Autoria:** CEPM (2026-04-04 en adelante), titular del proyecto. Introducidos y mantenidos
  en los commits `a0a41cd`, `022bc29`, `f5176d7` y `66bd071`.
- **Assets fuente:** `sopro (1).ico`, `sopro_final.svg`, `sopro_logo (1).png`.
- **Libreria de codigo:** `SOPRO.WinForms/Helpers/SoproIconProvider.cs` y
  `SOPRO.WinForms/Helpers/SoproIconType.cs` (rasterizador GDI+ propio). Los bitmaps embebidos
  en `FormProyecto.resx` son rasterizaciones pixel-exactas de esta libreria.
- **Recursos embebidos:** 2 iconos ICO (`$this.Icon` en `FormPrincipal.resx` y
  `FormProyecto.resx`) y 14 botones PNG (`btn*.Image` en `FormProyecto.resx`).

No hay licencias de terceros asociadas a estos recursos. Su titularidad de marca es una
decision separada del titular del repositorio y no se declara exclusividad alguna.

---

## Alcance de la licencia de SOPRO

La licencia MIT de SOPRO aplica al codigo fuente y documentacion tecnica de este repositorio. **No cubre** logos, identidad visual, emblemas, iconos, plantillas, contenido editorial ni signos distintivos. Los recursos de terceros conservan sus licencias originales independientemente de la licencia de SOPRO.
