using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Core.Entities;
using DrawingFont = System.Drawing.Font;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;
using MColors = MigraDoc.DocumentObjectModel.Colors;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Services
{
    public class GeneradorPdfCatalogoHerramientas
    {
        private readonly ReporteService _svc;

        public GeneradorPdfCatalogoHerramientas(ReporteService svc) => _svc = svc;

        public string Generar(
            Proyecto proyecto,
            List<Herramienta> herramientas,
            PlantillaReporte plantilla,
            List<ColumnaHerramienta> columnas,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (herramientas == null) throw new ArgumentNullException(nameof(herramientas));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (columnas == null) throw new ArgumentNullException(nameof(columnas));

            var cols = columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (!cols.Any())
            {
                cols = new List<ColumnaHerramienta>
                {
                    new() { Nombre = "Clave", NombreInterno = "Clave", Visible = true, Orden = 1, AnchoColumna = 110, Alineacion = AlineacionColumna.Centro },
                    new() { Nombre = "Descripción", NombreInterno = "Descripcion", Visible = true, Orden = 2, AnchoColumna = 300, Alineacion = AlineacionColumna.Izquierda, WrapTexto = true },
                    new() { Nombre = "Unidad", NombreInterno = "Unidad", Visible = true, Orden = 3, AnchoColumna = 80, Alineacion = AlineacionColumna.Centro },
                    new() { Nombre = "Precio/Porcentaje", NombreInterno = "PrecioUnitario", Visible = true, Orden = 4, AnchoColumna = 150, Alineacion = AlineacionColumna.Derecha },
                    new() { Nombre = "Origen", NombreInterno = "OrigenDetalle", Visible = true, Orden = 5, AnchoColumna = 180, Alineacion = AlineacionColumna.Izquierda },
                };
            }

            var doc = new Document();
            DefinirEstilos(doc);

            var orientation = ReportPageLayoutHelper.DetermineAutoOrientation(
                cols.Select(c => (c.NombreInterno ?? c.Nombre ?? string.Empty, Math.Max(24, c.AnchoColumna))),
                1.0,
                1.0);

            var section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.Letter;
            section.PageSetup.Orientation = orientation;

            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            var headerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoCm(plantilla, elementosPdf);
            var footerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaPieCm(plantilla, elementosPdf);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoSuperiorCm(plantilla, elementosPdf, 1.2));
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoInferiorCm(plantilla, elementosPdf, 0.8));

            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpo(section, proyecto, herramientas, cols, tituloCfg);

            var renderer = new PdfDocumentRenderer() { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
            normal.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
            normal.Font.Size = 9;

            var title = doc.Styles.AddStyle("CatalogoHerramientasTitle", "Normal");
            title.Font.Bold = true;
            title.Font.Size = 12;
            title.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var project = doc.Styles.AddStyle("CatalogoHerramientasProject", "Normal");
            project.Font.Size = 10;
            project.ParagraphFormat.Alignment = MParagraphAlignment.Center;
        }

        private void ConstruirHeader(Section section, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
            {
                PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.FirstPage, section, proyecto, plantilla, elementosPdf, _svc);
                return;
            }
            var primary = section.Headers.Primary.AddTable();
            primary.Borders.Visible = false;
            primary.Rows.LeftIndent = 0;
            ReportPageLayoutHelper.AddHeaderFooterColumns(primary, section);
            var row = primary.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(headerHeightCm);

            EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.EncabezadoIzqTipo, plantilla.EncabezadoIzqContenido,
                plantilla.EncabezadoIzqFuente, plantilla.EncabezadoIzqTamaño, plantilla.EncabezadoIzqNegrita, plantilla.EncabezadoIzqCursiva,
                plantilla.EncabezadoIzqAlineacion, false);
            EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.EncabezadoCenTipo, plantilla.EncabezadoCenContenido,
                plantilla.EncabezadoCenFuente, plantilla.EncabezadoCenTamaño, plantilla.EncabezadoCenNegrita, plantilla.EncabezadoCenCursiva,
                plantilla.EncabezadoCenAlineacion, false);
            EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.EncabezadoDerTipo, plantilla.EncabezadoDerContenido,
                plantilla.EncabezadoDerFuente, plantilla.EncabezadoDerTamaño, plantilla.EncabezadoDerNegrita, plantilla.EncabezadoDerCursiva,
                plantilla.EncabezadoDerAlineacion, false);

            var first = section.Headers.FirstPage.AddTable();
            first.Borders.Visible = false;
            first.Rows.LeftIndent = 0;
            ReportPageLayoutHelper.AddHeaderFooterColumns(first, section);
            var firstRow = first.AddRow();
            firstRow.HeightRule = RowHeightRule.AtLeast;
            firstRow.Height = Unit.FromCentimeter(headerHeightCm);

            EscribirCeldaPlantilla(firstRow.Cells[0], proyecto, plantilla, plantilla.EncabezadoIzqTipo, plantilla.EncabezadoIzqContenido,
                plantilla.EncabezadoIzqFuente, plantilla.EncabezadoIzqTamaño, plantilla.EncabezadoIzqNegrita, plantilla.EncabezadoIzqCursiva,
                plantilla.EncabezadoIzqAlineacion, false);
            EscribirCeldaPlantilla(firstRow.Cells[1], proyecto, plantilla, plantilla.EncabezadoCenTipo, plantilla.EncabezadoCenContenido,
                plantilla.EncabezadoCenFuente, plantilla.EncabezadoCenTamaño, plantilla.EncabezadoCenNegrita, plantilla.EncabezadoCenCursiva,
                plantilla.EncabezadoCenAlineacion, false);
            EscribirCeldaPlantilla(firstRow.Cells[2], proyecto, plantilla, plantilla.EncabezadoDerTipo, plantilla.EncabezadoDerContenido,
                plantilla.EncabezadoDerFuente, plantilla.EncabezadoDerTamaño, plantilla.EncabezadoDerNegrita, plantilla.EncabezadoDerCursiva,
                plantilla.EncabezadoDerAlineacion, false);
        }

        private void ConstruirFooter(Section section, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
            {
                PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.FirstPage, section, proyecto, plantilla, elementosPdf, _svc);
                return;
            }
            var primary = section.Footers.Primary.AddTable();
            primary.Borders.Visible = false;
            primary.Rows.LeftIndent = 0;
            ReportPageLayoutHelper.AddHeaderFooterColumns(primary, section);
            var row = primary.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(footerHeightCm);

            EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.PiePaginaIzqTipo, plantilla.PiePaginaIzqContenido,
                plantilla.PiePaginaIzqFuente, plantilla.PiePaginaIzqTamaño, plantilla.PiePaginaIzqNegrita, plantilla.PiePaginaIzqCursiva,
                plantilla.PiePaginaIzqAlineacion, true);
            EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.PiePaginaCenTipo, plantilla.PiePaginaCenContenido,
                plantilla.PiePaginaCenFuente, plantilla.PiePaginaCenTamaño, plantilla.PiePaginaCenNegrita, plantilla.PiePaginaCenCursiva,
                plantilla.PiePaginaCenAlineacion, true);
            EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.PiePaginaDerTipo, plantilla.PiePaginaDerContenido,
                plantilla.PiePaginaDerFuente, plantilla.PiePaginaDerTamaño, plantilla.PiePaginaDerNegrita, plantilla.PiePaginaDerCursiva,
                plantilla.PiePaginaDerAlineacion, true);

            var first = section.Footers.FirstPage.AddTable();
            first.Borders.Visible = false;
            first.Rows.LeftIndent = 0;
            ReportPageLayoutHelper.AddHeaderFooterColumns(first, section);
            var firstRow = first.AddRow();
            firstRow.HeightRule = RowHeightRule.AtLeast;
            firstRow.Height = Unit.FromCentimeter(footerHeightCm);

            EscribirCeldaPlantilla(firstRow.Cells[0], proyecto, plantilla, plantilla.PiePaginaIzqTipo, plantilla.PiePaginaIzqContenido,
                plantilla.PiePaginaIzqFuente, plantilla.PiePaginaIzqTamaño, plantilla.PiePaginaIzqNegrita, plantilla.PiePaginaIzqCursiva,
                plantilla.PiePaginaIzqAlineacion, true);
            EscribirCeldaPlantilla(firstRow.Cells[1], proyecto, plantilla, plantilla.PiePaginaCenTipo, plantilla.PiePaginaCenContenido,
                plantilla.PiePaginaCenFuente, plantilla.PiePaginaCenTamaño, plantilla.PiePaginaCenNegrita, plantilla.PiePaginaCenCursiva,
                plantilla.PiePaginaCenAlineacion, true);
            EscribirCeldaPlantilla(firstRow.Cells[2], proyecto, plantilla, plantilla.PiePaginaDerTipo, plantilla.PiePaginaDerContenido,
                plantilla.PiePaginaDerFuente, plantilla.PiePaginaDerTamaño, plantilla.PiePaginaDerNegrita, plantilla.PiePaginaDerCursiva,
                plantilla.PiePaginaDerAlineacion, true);
        }

        private void ConstruirCuerpo(Section section, Proyecto proyecto, List<Herramienta> items, List<ColumnaHerramienta> cols, ConfiguracionTituloReporte? tituloCfg)
        {
            var pTitle = section.AddParagraph("CATÁLOGO DE HERRAMIENTAS", "CatalogoHerramientasTitle");
            ReportTitleStyleHelper.ApplyToParagraph(pTitle, tituloCfg, "CATÁLOGO DE HERRAMIENTAS");
            pTitle.Format.Shading.Color = ParseColor(ReportTitleStyleHelper.StandardBackgroundHex);
            pTitle.Format.Font.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
            pTitle.Format.SpaceAfter = Unit.FromCentimeter(0.12);
            pTitle.Format.KeepWithNext = true;

            var pProject = section.AddParagraph(proyecto.Nombre ?? string.Empty, "CatalogoHerramientasProject");
            pProject.Format.Shading.Color = ParseColor("#E8EAF6");
            pProject.Format.SpaceAfter = Unit.FromCentimeter(0.2);
            pProject.Format.KeepWithNext = true;

            var table = section.AddTable();
            table.Rows.LeftIndent = 0;
            table.Borders.Width = 0.25;
            table.Borders.Color = ParseColor("#DDDDDD");
            AgregarColumnas(table, cols.Select(c => Math.Max(24, c.AnchoColumna)).ToList(), ReportPageLayoutHelper.GetLetterContentWidthCm(section));

            var head = table.AddRow();
            head.HeadingFormat = true;
            head.Height = Unit.FromPoint(18);
            head.HeightRule = RowHeightRule.AtLeast;
            head.Shading.Color = ParseColor("#4A4A6A");
            for (int i = 0; i < cols.Count; i++)
            {
                var cell = head.Cells[i];
                var p = cell.AddParagraph(cols[i].Nombre ?? string.Empty);
                p.Format.Alignment = MParagraphAlignment.Center;
                PdfFontHelper.ApplyFont(p.Format.Font, cols[i].NombreFuente, Math.Max(8, cols[i].TamanoFuente), true, cols[i].Cursiva);
                p.Format.Font.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
                cell.VerticalAlignment = VerticalAlignment.Center;
            }

            bool alt = false;
            foreach (var item in items)
            {
                var row = table.AddRow();
                var basePts = 14d;
                var height = EstimarAlturaFila(cols, table, item, basePts);
                row.HeightRule = RowHeightRule.AtLeast;
                row.Height = Unit.FromPoint(height);
                row.Shading.Color = ParseColor(alt ? "#F5F5F5" : "#FFFFFF");
                alt = !alt;
                var rowColor = alt ? "#F5F5F5" : "#FFFFFF";

                for (int i = 0; i < cols.Count; i++)
                {
                    var c = cols[i];
                    var cell = row.Cells[i];
                    cell.VerticalAlignment = ConvertirAlineacionVertical(c.AlineacionVertical);
                    cell.Shading.Color = ParseColor(string.IsNullOrWhiteSpace(c.ColorFondo) ? rowColor : c.ColorFondo);
                    var p = cell.AddParagraph(ObtenerValor(item, c));
                    p.Format.Alignment = ConvertirAlineacion(c.Alineacion);
                    p.Format.SpaceAfter = 0;
                    p.Format.SpaceBefore = 0;
                    PdfFontHelper.ApplyFont(p.Format.Font, c.NombreFuente, c.TamanoFuente <= 0 ? 9 : c.TamanoFuente, c.Negrita, c.Cursiva);
                    p.Format.Font.Color = ParseColor(c.ColorFuente);
                }
            }
        }

        private static void AgregarColumnas(Table table, List<int> widthsPx, double availableCm)
        {
            double totalPx = Math.Max(1, widthsPx.Sum(w => Math.Max(24, w)));
            double assigned = 0;
            for (int i = 0; i < widthsPx.Count; i++)
            {
                double widthCm = (i == widthsPx.Count - 1)
                    ? Math.Max(1.2, availableCm - assigned)
                    : Math.Max(1.2, availableCm * Math.Max(24, widthsPx[i]) / totalPx);
                assigned += widthCm;
                table.AddColumn(Unit.FromCentimeter(widthCm));
            }
        }

        private double EstimarAlturaFila(List<ColumnaHerramienta> cols, Table table, Herramienta item, double alturaBase)
        {
            double altura = alturaBase;
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                if (!col.WrapTexto) continue;
                var texto = ObtenerValor(item, col);
                if (string.IsNullOrWhiteSpace(texto)) continue;
                using var font = new DrawingFont(
                    PdfFontHelper.NormalizeFontName(col.NombreFuente),
                    Math.Max(8f, col.TamanoFuente > 0 ? col.TamanoFuente : 9f),
                    (col.Negrita && col.Cursiva) ? FontStyle.Bold | FontStyle.Italic : col.Negrita ? FontStyle.Bold : col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                var widthPts = table.Columns[i].Width.Point;
                var widthPx = Math.Max(24, (int)Math.Round(widthPts * 96.0 / 72.0) - 8);
                var measured = TextRenderer.MeasureText(texto, font, new Size(widthPx, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                var alturaPts = Math.Max(alturaBase, measured.Height * 72.0 / 96.0 + 6);
                if (alturaPts > altura) altura = alturaPts;
            }
            return altura;
        }

        private string ObtenerValor(Herramienta h, ColumnaHerramienta c)
        {
            return c.NombreInterno switch
            {
                "Clave" => h.Clave ?? string.Empty,
                "Descripcion" => h.Descripcion ?? string.Empty,
                "Unidad" => h.Unidad ?? string.Empty,
                "PrecioUnitario" => h.EsPorcentajeMO ? $"{h.PrecioUnitario:N2}%" : h.PrecioUnitario.ToString("#,##0.00"),
                "OrigenDetalle" => ImportOriginStampService.BuildOriginDisplay(h.Notas, h.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local"),
                "Origen" => h.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto",
                _ => string.Empty
            };
        }

        private void EscribirCeldaPlantilla(Cell cell, Proyecto proyecto, PlantillaReporte plantilla,
            string tipo, string contenido, string fuente, float tamano, bool negrita, bool cursiva, string alineacion, bool esPie)
        {
            var p = cell.AddParagraph();
            p.Format.Alignment = ConvertirAlineacionTexto(alineacion);
            p.Format.SpaceBefore = 0;
            p.Format.SpaceAfter = 0;
            PdfFontHelper.ApplyFont(p.Format.Font, fuente, tamano <= 0 ? 9 : tamano, negrita, cursiva);
            if (string.Equals(tipo, "Imagen", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(contenido) && File.Exists(contenido))
                {
                    var img = cell.AddImage(contenido);
                    img.LockAspectRatio = true;
                    img.Height = Unit.FromCentimeter(esPie ? 0.9 : 1.1);
                }
                return;
            }
            var texto = contenido ?? string.Empty;
            texto = _svc.ResolverCampos(texto, proyecto, plantilla);
            if (esPie) AgregarTextoConCamposPagina(p, texto); else p.AddText(texto);
        }

        private static void AgregarTextoConCamposPagina(Paragraph p, string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;
            const string t1 = "{pagina}"; const string t2 = "{total_paginas}";
            int index = 0;
            while (index < texto.Length)
            {
                int pos1 = texto.IndexOf(t1, index, StringComparison.OrdinalIgnoreCase);
                int pos2 = texto.IndexOf(t2, index, StringComparison.OrdinalIgnoreCase);
                int next = -1;
                if (pos1 >= 0 && pos2 >= 0) next = Math.Min(pos1, pos2);
                else next = pos1 >= 0 ? pos1 : pos2;
                if (next < 0) { p.AddText(texto.Substring(index)); break; }
                if (next > index) p.AddText(texto.Substring(index, next - index));
                if (next == pos1) { p.AddPageField(); index = pos1 + t1.Length; }
                else { p.AddNumPagesField(); index = pos2 + t2.Length; }
            }
        }

        private static MParagraphAlignment ConvertirAlineacion(AlineacionColumna a) => a switch
        {
            AlineacionColumna.Centro => MParagraphAlignment.Center,
            AlineacionColumna.Derecha => MParagraphAlignment.Right,
            AlineacionColumna.Justificado => MParagraphAlignment.Justify,
            _ => MParagraphAlignment.Left,
        };

        private static MParagraphAlignment ConvertirAlineacionTexto(string alineacion)
        {
            return (alineacion ?? string.Empty).ToLowerInvariant() switch
            {
                "centrado" or "centro" => MParagraphAlignment.Center,
                "derecha" => MParagraphAlignment.Right,
                "justificado" => MParagraphAlignment.Justify,
                _ => MParagraphAlignment.Left,
            };
        }

        private static VerticalAlignment ConvertirAlineacionVertical(int v) => v switch { 0 => VerticalAlignment.Top, 2 => VerticalAlignment.Bottom, _ => VerticalAlignment.Center };

        private static MColor ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return MColors.Black;
            try
            {
                var c = ColorTranslator.FromHtml(value);
                return MColor.FromRgb(c.R, c.G, c.B);
            }
            catch
            {
                return MColors.Black;
            }
        }
    }
}
