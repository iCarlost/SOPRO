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

namespace SOPRO.WinForms.Services
{
    public class GeneradorPdfCatalogoManoObra
    {
        private readonly ReporteService _svc;
        public GeneradorPdfCatalogoManoObra(ReporteService svc) => _svc = svc;

        public string GenerarCatalogo(
            Proyecto proyecto,
            List<ManoDeObra> items,
            PlantillaReporte plantilla,
            List<ColumnaManoObra> columnas,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (columnas == null) throw new ArgumentNullException(nameof(columnas));

            var cols = columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (!cols.Any())
            {
                cols = new List<ColumnaManoObra>
                {
                    new() { Nombre = "Clave", NombreInterno = "Clave", Visible = true, Orden = 1, AnchoColumna = 110, Alineacion = AlineacionColumna.Centro },
                    new() { Nombre = "Descripción", NombreInterno = "Descripcion", Visible = true, Orden = 2, AnchoColumna = 300, Alineacion = AlineacionColumna.Justificado, WrapTexto = true },
                    new() { Nombre = "Unidad", NombreInterno = "Unidad", Visible = true, Orden = 3, AnchoColumna = 80, Alineacion = AlineacionColumna.Centro },
                    new() { Nombre = "Salario Base", NombreInterno = "SalarioBase", Visible = true, Orden = 4, AnchoColumna = 120, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "#,##0.00" },
                    new() { Nombre = "FSR", NombreInterno = "FactorSalarioReal", Visible = true, Orden = 5, AnchoColumna = 90, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "0.0000" },
                    new() { Nombre = "Salario Real", NombreInterno = "SalarioReal", Visible = true, Orden = 6, AnchoColumna = 120, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "#,##0.00" },
                    new() { Nombre = "Origen", NombreInterno = "Origen", Visible = true, Orden = 7, AnchoColumna = 90, Alineacion = AlineacionColumna.Centro },
                };
            }

            if (string.IsNullOrWhiteSpace(rutaDestino))
                throw new InvalidOperationException("Debe especificarse la ruta destino del PDF.");

            var doc = new Document();
            DefinirEstilos(doc);
            var orientation = ReportPageLayoutHelper.DetermineAutoOrientation(
                cols.Select(c => (c.NombreInterno ?? c.Nombre ?? string.Empty, Math.Max(24, c.AnchoColumna))),
                1.0,
                1.0);
            var section = doc.AddSection();
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            ConfigurarPaginaCatalogo(section, plantilla, elementosPdf, orientation, out var headerHeightCm, out var footerHeightCm);
            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpoCatalogo(section, proyecto, items, cols, tituloCfg);

            var renderer = new PdfDocumentRenderer(true) { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        public string GenerarTabuladorFsr(
            Proyecto proyecto,
            List<ManoDeObra> items,
            PlantillaReporte plantilla,
            string rutaDestino = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));

            var filas = GeneradorExcelFSR.CalcularFilasAE2C(proyecto, items);
            if (!filas.Any()) throw new InvalidOperationException("No hay datos suficientes para generar el tabulador FSR.");

            if (string.IsNullOrWhiteSpace(rutaDestino))
                throw new InvalidOperationException("Debe especificarse la ruta destino del PDF.");

            var doc = new Document();
            DefinirEstilos(doc);
            var section = doc.AddSection();
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            section.PageSetup.PageFormat = PageFormat.Legal;
            section.PageSetup.Orientation = MOrientation.Landscape;
            var headerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoCm(plantilla, elementosPdf);
            var footerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaPieCm(plantilla, elementosPdf);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(0.8);
            section.PageSetup.RightMargin = Unit.FromCentimeter(0.8);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            var topGapTabulador = PlantillaLibrePdfRenderer.TieneEncabezadoLibre(plantilla, elementosPdf) ? 0.55 : PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoSuperiorCm(plantilla, elementosPdf, 1.0);
            var bottomGapTabulador = PlantillaLibrePdfRenderer.TienePieLibre(plantilla, elementosPdf) ? 0.15 : PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoInferiorCm(plantilla, elementosPdf, 0.8);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + topGapTabulador);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + bottomGapTabulador);
            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpoTabulador(section, proyecto, filas);

            var renderer = new PdfDocumentRenderer(true) { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
            normal.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
            normal.Font.Size = 8;
            var title = doc.Styles.AddStyle("CatalogoManoObraTitle", "Normal");
            title.Font.Bold = true;
            title.Font.Size = 12;
            title.ParagraphFormat.Alignment = MParagraphAlignment.Center;
            var project = doc.Styles.AddStyle("CatalogoManoObraProject", "Normal");
            project.Font.Size = 10;
            project.ParagraphFormat.Alignment = MParagraphAlignment.Center;
        }

        private static void ConfigurarPaginaCatalogo(Section section, PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementosPdf, MOrientation orientation, out double headerHeightCm, out double footerHeightCm)
        {
            section.PageSetup.PageFormat = PageFormat.Letter;
            section.PageSetup.Orientation = orientation;
            headerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoCm(plantilla, elementosPdf);
            footerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaPieCm(plantilla, elementosPdf);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoSuperiorCm(plantilla, elementosPdf, 1.2));
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoInferiorCm(plantilla, elementosPdf, 0.8));
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

        private void ConstruirCuerpoCatalogo(Section section, Proyecto proyecto, List<ManoDeObra> items, List<ColumnaManoObra> cols, ConfiguracionTituloReporte? tituloCfg)
        {
            var pTitle = section.AddParagraph("CATÁLOGO DE MANO DE OBRA", "CatalogoManoObraTitle");
            ReportTitleStyleHelper.ApplyToParagraph(pTitle, tituloCfg, "CATÁLOGO DE MANO DE OBRA");
            pTitle.Format.Shading.Color = ParseColor(ReportTitleStyleHelper.StandardBackgroundHex);
            pTitle.Format.Font.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
            pTitle.Format.SpaceAfter = Unit.FromCentimeter(0.12);
            pTitle.Format.KeepWithNext = true;

            var pProject = section.AddParagraph(proyecto.Nombre ?? string.Empty, "CatalogoManoObraProject");
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
                p.Format.Font.Color = ParseColor("#FFFFFF");
                cell.VerticalAlignment = VerticalAlignment.Center;
            }

            bool alt = false;
            foreach (var item in items)
            {
                var row = table.AddRow();
                var rowColor = alt ? "#F5F5F5" : "#FFFFFF";
                row.HeightRule = RowHeightRule.AtLeast;
                row.Height = Unit.FromPoint(EstimarAlturaFila(cols, table, item, 14d));
                row.Shading.Color = ParseColor(rowColor);
                alt = !alt;

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

        private void ConstruirCuerpoTabulador(Section section, Proyecto proyecto, List<GeneradorExcelFSR.AE2CRowData> filas)
        {
            var pTitle = section.AddParagraph("TABLA DE CÁLCULO DEL FACTOR DE SALARIO REAL", "CatalogoManoObraTitle");
            ReportTitleStyleHelper.ApplyToParagraph(pTitle, null, "TABLA DE CÁLCULO DEL FACTOR DE SALARIO REAL");
            pTitle.Format.Alignment = MParagraphAlignment.Center;
            pTitle.Format.SpaceAfter = Unit.FromCentimeter(0.12);
            pTitle.Format.KeepWithNext = true;

            var pProject = section.AddParagraph(proyecto.Nombre ?? string.Empty, "CatalogoManoObraProject");
            pProject.Format.Shading.Color = ParseColor("#E8EAF6");
            pProject.Format.SpaceAfter = Unit.FromCentimeter(0.2);
            pProject.Format.KeepWithNext = true;

            string[] headers = {
                "Clave","Descripción","Sal. Base M.N.","Salario Nominal","Factor SBC","Salario Base Cotización","Cuota fija","Excedente a 3 SMGDF",
                "Prest. especie","Prest. dinero","Inv. y vida","Guarderías","Cesantía y vejez","Retiro","Suma cuotas IMSS","INFONAVIT",
                "Suma prest. patronales","Obligac. PS","Factor TP/TL","FSR","Salario Real"};
            var widths = new List<int>{70,220,90,80,70,85,70,70,70,70,70,70,70,60,80,75,95,80,70,60,85};

            var table = section.AddTable();
            table.Rows.LeftIndent = 0;
            table.Borders.Width = 0.2;
            table.Borders.Color = ParseColor("#D8D8D8");
            AgregarColumnas(table, widths, ReportPageLayoutHelper.GetContentWidthCm(section));

            var head = table.AddRow();
            head.HeadingFormat = true;
            head.Height = Unit.FromPoint(28);
            head.HeightRule = RowHeightRule.AtLeast;
            head.Shading.Color = ParseColor("#4A4A6A");
            for (int i=0;i<headers.Length;i++)
            {
                var p = head.Cells[i].AddParagraph(headers[i]);
                p.Format.Alignment = MParagraphAlignment.Center;
                p.Format.SpaceAfter = 0;
                p.Format.SpaceBefore = 0;
                PdfFontHelper.ApplyFont(p.Format.Font, "Segoe UI", 7.2, true, false);
                p.Format.Font.Color = ParseColor("#FFFFFF");
                head.Cells[i].VerticalAlignment = VerticalAlignment.Center;
            }

            bool alt = false;
            foreach (var r in filas)
            {
                var row = table.AddRow();
                var rowColor = alt ? "#F5F5F5" : "#FFFFFF";
                row.Shading.Color = ParseColor(rowColor);
                alt = !alt;
                var vals = new string[] {
                    r.Clave, r.Descripcion, r.SalarioBaseTexto, r.SalarioNominalTexto, r.FactorSbcTexto, r.SalarioBaseCotTexto,
                    r.CuotaFijaTexto, r.ExcedenteTexto, r.PrestacionesEspecieTexto, r.PrestacionesDineroTexto, r.InvalidezVidaTexto,
                    r.GuarderiasTexto, r.CesantiaVejezTexto, r.RetiroTexto, r.SumaCuotasImssTexto, r.InfonavitTexto, r.SumaPrestPatronalesTexto,
                    r.ObligacionesPsTexto, r.FactorTpTlTexto, r.FsrTexto, r.SalarioRealTexto
                };
                row.HeightRule = RowHeightRule.AtLeast;
                row.Height = Unit.FromPoint(EstimarAlturaFilaTabulador(table, vals, 13d));
                for (int i=0;i<vals.Length;i++)
                {
                    var cell = row.Cells[i];
                    cell.VerticalAlignment = VerticalAlignment.Center;
                    cell.Shading.Color = ParseColor(rowColor);
                    var p = cell.AddParagraph(vals[i] ?? string.Empty);
                    p.Format.SpaceBefore = 0;
                    p.Format.SpaceAfter = 0;
                    p.Format.Alignment = i == 1 ? MParagraphAlignment.Justify : (i==0 ? MParagraphAlignment.Center : MParagraphAlignment.Right);
                    PdfFontHelper.ApplyFont(p.Format.Font, "Segoe UI", 7.2, false, false);
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
                    ? Math.Max(0.9, availableCm - assigned)
                    : Math.Max(0.9, availableCm * Math.Max(24, widthsPx[i]) / totalPx);
                assigned += widthCm;
                table.AddColumn(Unit.FromCentimeter(widthCm));
            }
        }

        private double EstimarAlturaFila(List<ColumnaManoObra> cols, Table table, ManoDeObra item, double alturaBase)
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

        private double EstimarAlturaFilaTabulador(Table table, string[] vals, double alturaBase)
        {
            double altura = alturaBase;
            for (int i = 0; i < vals.Length; i++)
            {
                var texto = vals[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(texto)) continue;
                using var font = new DrawingFont(PdfFontHelper.NormalizeFontName("Segoe UI"), 7.2f, FontStyle.Regular);
                var widthPts = table.Columns[i].Width.Point;
                var widthPx = Math.Max(20, (int)Math.Round(widthPts * 96.0 / 72.0) - 6);
                var measured = TextRenderer.MeasureText(texto, font, new Size(widthPx, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                var alturaPts = Math.Max(alturaBase, measured.Height * 72.0 / 96.0 + 5);
                if (alturaPts > altura) altura = alturaPts;
            }
            return altura;
        }

        private static string ObtenerValor(ManoDeObra m, ColumnaManoObra c) => c.NombreInterno switch
        {
            "Clave" => m.Clave ?? string.Empty,
            "Descripcion" => m.Descripcion ?? string.Empty,
            "Unidad" => m.Unidad ?? string.Empty,
            "SalarioBase" => m.SalarioBase.ToString("#,##0.00"),
            "FactorSalarioReal" => m.FactorSalarioReal.ToString("0.0000"),
            "SalarioReal" => m.SalarioReal.ToString("#,##0.00"),
            "Origen" => m.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto",
            _ => string.Empty
        };

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
        private static MParagraphAlignment ConvertirAlineacionTexto(string alineacion) => (alineacion ?? string.Empty).ToLowerInvariant() switch
        {
            "centrado" or "centro" => MParagraphAlignment.Center,
            "derecha" => MParagraphAlignment.Right,
            "justificado" => MParagraphAlignment.Justify,
            _ => MParagraphAlignment.Left,
        };
        private static VerticalAlignment ConvertirAlineacionVertical(int v) => v switch { 0 => VerticalAlignment.Top, 2 => VerticalAlignment.Bottom, _ => VerticalAlignment.Center };
        private static string SanitizarNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "Reporte";
            foreach (var c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return nombre.Trim();
        }
        private static MColor ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return MColors.Black;
            try { var c = ColorTranslator.FromHtml(value); return MColor.FromRgb(c.R, c.G, c.B); }
            catch { return MColors.Black; }
        }
    }
}
