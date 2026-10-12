using System;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using DrawingColor = System.Drawing.Color;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;

namespace SOPRO.WinForms.Services
{
    public class GeneradorPdfUtilidad
    {
        private readonly ReporteService _svc;

        public GeneradorPdfUtilidad(ReporteService svc) => _svc = svc;

        public string Generar(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            BudgetPercentagePreviewResult preview,
            UtilidadCalculationResult resultado,
            ReportColumnSnapshot snapshot,
            ColumnaPersonalizada? estiloBase,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            if (resultado == null) throw new ArgumentNullException(nameof(resultado));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (string.IsNullOrWhiteSpace(rutaDestino))
            {
                var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta, $"Utilidad_{SanitizarNombre(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            var doc = new Document();
            DefinirEstilos(doc, estiloBase);

            var section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.Letter;
            section.PageSetup.Orientation = MOrientation.Portrait;

            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            var headerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoCm(plantilla, elementosPdf);
            var footerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaPieCm(plantilla, elementosPdf);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.25);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoSuperiorCm(plantilla, elementosPdf, 2.25));
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + 0.75);

            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpo(section, proyecto, preview, resultado, snapshot, estiloBase, tituloCfg);

            var renderer = new PdfDocumentRenderer() { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc, ColumnaPersonalizada? estiloBase)
        {
            var normal = doc.Styles["Normal"];
            PdfFontHelper.ApplyFont(normal.Font,
                estiloBase?.NombreFuente ?? "Segoe UI",
                estiloBase?.TamanoFuente > 0 ? estiloBase.TamanoFuente : 9,
                false,
                estiloBase?.Cursiva == true);
            if (!string.IsNullOrWhiteSpace(estiloBase?.ColorFuente))
                normal.Font.Color = ParseColor(estiloBase.ColorFuente);

            var title = doc.Styles.AddStyle("UtilidadTitle", "Normal");
            title.Font.Bold = true;
            title.Font.Size = 11.5;
            title.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var subtitle = doc.Styles.AddStyle("UtilidadSubtitle", "Normal");
            subtitle.Font.Bold = true;
            subtitle.Font.Size = 9.5;
            subtitle.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var section = doc.Styles.AddStyle("UtilidadSection", "Normal");
            section.Font.Bold = true;
            section.Font.Size = 9.3;

            var emphasis = doc.Styles.AddStyle("UtilidadEmphasis", "Normal");
            emphasis.Font.Bold = true;
        }

        private void ConstruirHeader(Section section, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
                return;
            ConstruirTablaPlantilla(section.Headers.Primary.AddTable(), proyecto, plantilla, headerHeightCm, false);
            ConstruirTablaPlantilla(section.Headers.FirstPage.AddTable(), proyecto, plantilla, headerHeightCm, false);
        }

        private void ConstruirFooter(Section section, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
                return;
            ConstruirTablaPlantilla(section.Footers.Primary.AddTable(), proyecto, plantilla, footerHeightCm, true);
            ConstruirTablaPlantilla(section.Footers.FirstPage.AddTable(), proyecto, plantilla, footerHeightCm, true);
        }

        private void ConstruirTablaPlantilla(Table table, Proyecto proyecto, PlantillaReporte plantilla, double altoCm, bool esPie)
        {
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(6.2));
            table.AddColumn(Unit.FromCentimeter(6.2));
            table.AddColumn(Unit.FromCentimeter(6.2));
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(altoCm);

            if (!esPie)
            {
                EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.EncabezadoIzqTipo, plantilla.EncabezadoIzqContenido,
                    plantilla.EncabezadoIzqFuente, plantilla.EncabezadoIzqTamaño, plantilla.EncabezadoIzqNegrita, plantilla.EncabezadoIzqCursiva,
                    plantilla.EncabezadoIzqAlineacion, false);
                EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.EncabezadoCenTipo, plantilla.EncabezadoCenContenido,
                    plantilla.EncabezadoCenFuente, plantilla.EncabezadoCenTamaño, plantilla.EncabezadoCenNegrita, plantilla.EncabezadoCenCursiva,
                    plantilla.EncabezadoCenAlineacion, false);
                EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.EncabezadoDerTipo, plantilla.EncabezadoDerContenido,
                    plantilla.EncabezadoDerFuente, plantilla.EncabezadoDerTamaño, plantilla.EncabezadoDerNegrita, plantilla.EncabezadoDerCursiva,
                    plantilla.EncabezadoDerAlineacion, false);
            }
            else
            {
                EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.PiePaginaIzqTipo, plantilla.PiePaginaIzqContenido,
                    plantilla.PiePaginaIzqFuente, plantilla.PiePaginaIzqTamaño, plantilla.PiePaginaIzqNegrita, plantilla.PiePaginaIzqCursiva,
                    plantilla.PiePaginaIzqAlineacion, true);
                EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.PiePaginaCenTipo, plantilla.PiePaginaCenContenido,
                    plantilla.PiePaginaCenFuente, plantilla.PiePaginaCenTamaño, plantilla.PiePaginaCenNegrita, plantilla.PiePaginaCenCursiva,
                    plantilla.PiePaginaCenAlineacion, true);
                EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.PiePaginaDerTipo, plantilla.PiePaginaDerContenido,
                    plantilla.PiePaginaDerFuente, plantilla.PiePaginaDerTamaño, plantilla.PiePaginaDerNegrita, plantilla.PiePaginaDerCursiva,
                    plantilla.PiePaginaDerAlineacion, true);
            }
        }

        private void EscribirCeldaPlantilla(Cell cell, Proyecto proyecto, PlantillaReporte plantilla,
            string tipo, string contenido, string fuente, float tamano, bool negrita, bool cursiva,
            string alineacion, bool soportaCamposPagina)
        {
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Format.Alignment = ConvertirAlineacionTexto(alineacion);
            cell.Borders.Visible = false;

            if (string.Equals(tipo, "Imagen", StringComparison.OrdinalIgnoreCase) && File.Exists(contenido))
            {
                var img = cell.AddImage(contenido);
                img.LockAspectRatio = true;
                img.Height = Unit.FromCentimeter(1.5);
                return;
            }

            var p = cell.AddParagraph();
            p.Format.Alignment = ConvertirAlineacionTexto(alineacion);
            p.Format.SpaceAfter = 0;
            p.Format.SpaceBefore = 0;
            PdfFontHelper.ApplyFont(p.Format.Font, fuente, tamano <= 0 ? 9 : tamano, negrita, cursiva);

            var texto = _svc.ResolverCampos(contenido ?? string.Empty, proyecto, plantilla);
            if (!soportaCamposPagina)
            {
                p.AddText(texto);
                return;
            }

            AgregarTextoConCamposPagina(p, texto);
        }

        private static void AgregarTextoConCamposPagina(Paragraph p, string texto)
        {
            texto = texto ?? string.Empty;
            int index = 0;
            while (index < texto.Length)
            {
                int posPagina = texto.IndexOf("{pagina}", index, StringComparison.OrdinalIgnoreCase);
                int posTotal = texto.IndexOf("{total_paginas}", index, StringComparison.OrdinalIgnoreCase);
                int next = new[] { posPagina, posTotal }.Where(x => x >= 0).DefaultIfEmpty(-1).Min();
                if (next < 0)
                {
                    p.AddText(texto.Substring(index));
                    break;
                }

                if (next > index)
                    p.AddText(texto.Substring(index, next - index));

                if (next == posPagina)
                {
                    p.AddPageField();
                    index = posPagina + "{pagina}".Length;
                }
                else
                {
                    p.AddNumPagesField();
                    index = posTotal + "{total_paginas}".Length;
                }
            }
        }

        private void ConstruirCuerpo(Section section, Proyecto proyecto, BudgetPercentagePreviewResult preview, UtilidadCalculationResult resultado, ReportColumnSnapshot snapshot, ColumnaPersonalizada? estiloBase, ConfiguracionTituloReporte? tituloCfg)
        {
            decimal costoDirecto   = BudgetPricingService.RoundImporte(proyecto, preview.CostoDirecto);
            decimal costoIndirecto = BudgetPricingService.RoundImporte(proyecto, preview.Subtotal1 - preview.CostoDirecto);
            decimal financiamiento = BudgetPricingService.RoundImporte(proyecto, preview.MontoFinanciamiento);
            decimal subtotal       = BudgetPricingService.RoundImporte(proyecto, resultado.BaseUtilidad);

            // Columnas y borde de tabla del MISMO snapshot neutral que la ruta Excel.
            var colBase         = ResolverColumna(snapshot, UtilidadReportColumns.Base);
            var colPorcentaje   = ResolverColumna(snapshot, UtilidadReportColumns.Porcentaje);
            var colImporteFinal = ResolverColumna(snapshot, UtilidadReportColumns.ImporteFinal);
            var bordes          = snapshot.EstiloTabla.Bordes;

            var titulo = section.AddTable();
            titulo.Borders.Visible = false;
            titulo.AddColumn(Unit.FromCentimeter(18.6));
            var tr = titulo.AddRow();
            tr.Shading.Color = ParseColor(ReportTitleStyleHelper.StandardBackgroundHex);
            tr.Height = Unit.FromCentimeter(0.7);
            tr.HeightRule = RowHeightRule.AtLeast;
            var p = tr.Cells[0].AddParagraph(ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "DETERMINACIÓN DE LA UTILIDAD NETA"));
            p.Style = "UtilidadTitle";
            ReportTitleStyleHelper.ApplyToParagraph(p, tituloCfg, "DETERMINACIÓN DE LA UTILIDAD NETA");
            tr.Cells[0].Format.Alignment = MParagraphAlignment.Center;
            tr.Cells[0].VerticalAlignment = VerticalAlignment.Center;

            var subt = section.AddTable();
            subt.Borders.Visible = false;
            subt.AddColumn(Unit.FromCentimeter(18.6));
            var sr = subt.AddRow();
            sr.Shading.Color = ParseColor("#FCE4D6");
            sr.Height = Unit.FromCentimeter(0.62);
            var ps = sr.Cells[0].AddParagraph("ANÁLISIS, CÁLCULO E INTEGRACIÓN DE % DE UTILIDAD");
            ps.Style = "UtilidadSubtitle";
            sr.Cells[0].Format.Alignment = MParagraphAlignment.Center;
            sr.Cells[0].VerticalAlignment = VerticalAlignment.Center;

            section.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.45);

            var t = section.AddTable();
            t.Borders.Visible = false;
            t.Format.SpaceAfter = Unit.FromCentimeter(0.08);
            t.AddColumn(Unit.FromCentimeter(8.4));
            t.AddColumn(Unit.FromCentimeter(1.2));
            t.AddColumn(Unit.FromCentimeter(3.2));
            t.AddColumn(Unit.FromCentimeter(3.0));
            t.AddColumn(Unit.FromCentimeter(2.8));

            AgregarFilaMonedaPdf(t, "COSTO DIRECTO", costoDirecto, colBase, snapshot, bordes, false, null, null, estiloBase);
            AgregarFilaMonedaPdf(t, "COSTO INDIRECTO", costoIndirecto, colBase, snapshot, bordes, false, null, null, estiloBase);
            AgregarFilaMonedaPdf(t, "FINANCIAMIENTO", financiamiento, colBase, snapshot, bordes, false, null, null, estiloBase);
            AgregarFilaMonedaPdf(t, "SUBTOTAL", subtotal, colBase, snapshot, bordes, true, "#FFF2CC", "#C00000", estiloBase);

            t.AddRow().Height = Unit.FromCentimeter(0.18);

            AgregarFilaParametroPdf(t, "Up = Utilidad Propuesta", resultado.PorcentajeUtilidadBruta, snapshot, bordes, null, true, "#C6E0B4", estiloBase);
            AgregarFilaParametroPdf(t, "ISR = Impuesto Sobre la Renta", resultado.Isr, snapshot, bordes, "SAT", false, null, estiloBase);
            AgregarFilaParametroPdf(t, "PTU = Participación de los Trabajadores en la Utilidad", resultado.Ptu, snapshot, bordes, "LFT", false, null, estiloBase);

            t.AddRow().Height = Unit.FromCentimeter(0.18);

            AgregarFilaFormulaPdf(t, "UTILIDAD NETA = Up / 1 - (ISR + PTU) =", resultado.PorcentajeUtilidadNeta, colPorcentaje, snapshot, bordes, estiloBase);
            AgregarFilaTextoPdf(t, $"={resultado.PorcentajeUtilidadNeta:N2}% / (1 - ({resultado.Isr:N0}% + {resultado.Ptu:N0}%))", estiloBase, "#E2F0D9", bordes);

            t.AddRow().Height = Unit.FromCentimeter(0.18);

            AgregarFilaFormulaPdf(t, "IMPORTE DE UTILIDAD =", resultado.ImporteUtilidad, colImporteFinal, snapshot, bordes, estiloBase);
            t.AddRow().Height = Unit.FromCentimeter(0.18);
            AgregarFilaMonedaPdf(t, "IMPORTE ISR", resultado.ImporteIsr, colImporteFinal, snapshot, bordes, false, "#F2F2F2", null, estiloBase);
            AgregarFilaMonedaPdf(t, "IMPORTE PTU", resultado.ImportePtu, colImporteFinal, snapshot, bordes, false, "#F2F2F2", null, estiloBase);
            AgregarFilaMonedaPdf(t, "UTILIDAD NETA ESTIMADA", resultado.UtilidadNetaEstimada, colImporteFinal, snapshot, bordes, true, "#DDEBF7", null, estiloBase);
        }

        /// <summary>
        /// Resuelve la columna neutral del snapshot para un identificador; si el
        /// snapshot no la expone, cae a los defaults neutrales del reporte (nunca a
        /// un control de UI).
        /// </summary>
        private static ReportColumnDefinition ResolverColumna(ReportColumnSnapshot snapshot, string identificador)
            => UtilidadReportColumns.Buscar(snapshot, identificador)
               ?? UtilidadReportSnapshotBuilder.DefaultColumns()
                      .FirstOrDefault(c => string.Equals(c.Identificador, identificador, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"El snapshot de Utilidad no expone la columna '{identificador}'.");

        /// <summary>
        /// Formatea un valor con la regla neutral compartida: porcentaje (con el
        /// signo '%') cuando la columna es de porcentaje; en caso contrario respeta
        /// la semántica de la columna (monetaria '$' + decimales de importe).
        /// </summary>
        private static string FormatearValor(decimal valor, ReportColumnDefinition columna, ReportColumnSnapshot snapshot)
            => ReportColumnGridFormat.EsPorcentajeGrid(columna)
                ? ReportColumnGridFormat.FormatearPorcentajePdf(valor, snapshot) + "%"
                : ReportColumnGridFormat.FormatearPdf(valor, columna, snapshot);

        private static void AplicarEstiloBase(Cell cell, ColumnaPersonalizada? estiloBase, bool bold = false, bool italic = false, string? colorTexto = null)
        {
            PdfFontHelper.ApplyFont(cell.Format.Font,
                estiloBase?.NombreFuente ?? "Segoe UI",
                estiloBase?.TamanoFuente > 0 ? estiloBase.TamanoFuente : 9,
                bold || (estiloBase?.Negrita == true),
                italic || (estiloBase?.Cursiva == true));
            cell.Format.Alignment = MParagraphAlignment.Left;
            if (!string.IsNullOrWhiteSpace(colorTexto))
                cell.Format.Font.Color = ParseColor(colorTexto);
            else if (!string.IsNullOrWhiteSpace(estiloBase?.ColorFuente))
                cell.Format.Font.Color = ParseColor(estiloBase.ColorFuente);
            cell.VerticalAlignment = VerticalAlignment.Center;
        }

        private static void AgregarFilaMonedaPdf(Table t, string etiqueta, decimal valor, ReportColumnDefinition columna, ReportColumnSnapshot snapshot, ReportTableBorder bordes, bool resaltar, string? fondo, string? colorTexto, ColumnaPersonalizada? estiloBase)
        {
            var row = t.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.56);
            row.Cells[0].MergeRight = 1;
            row.Cells[0].AddParagraph(etiqueta);
            AplicarEstiloBase(row.Cells[0], estiloBase, resaltar, true, colorTexto);

            row.Cells[2].AddParagraph(FormatearValor(valor, columna, snapshot));
            AplicarEstiloBase(row.Cells[2], estiloBase, resaltar, false, colorTexto);
            row.Cells[2].Format.Alignment = MParagraphAlignment.Right;

            if (!string.IsNullOrWhiteSpace(fondo))
            {
                row.Cells[0].Shading.Color = ParseColor(fondo);
                row.Cells[1].Shading.Color = ParseColor(fondo);
                row.Cells[2].Shading.Color = ParseColor(fondo);
            }

            AplicarCaja(row.Cells[0], bordes);
            AplicarCaja(row.Cells[1], bordes);
            AplicarCaja(row.Cells[2], bordes);
        }

        private static void AgregarFilaParametroPdf(Table t, string etiqueta, decimal porcentaje, ReportColumnSnapshot snapshot, ReportTableBorder bordes, string? nota, bool resaltar, string? fondo, ColumnaPersonalizada? estiloBase)
        {
            var row = t.AddRow();
            row.Cells[0].MergeRight = 2;
            row.Cells[0].AddParagraph(etiqueta);
            AplicarEstiloBase(row.Cells[0], estiloBase, true, resaltar);
            row.Cells[3].AddParagraph(ReportColumnGridFormat.FormatearPorcentajePdf(porcentaje, snapshot) + "%");
            AplicarEstiloBase(row.Cells[3], estiloBase, true);
            row.Cells[3].Format.Alignment = MParagraphAlignment.Right;
            if (!string.IsNullOrWhiteSpace(nota))
            {
                row.Cells[4].AddParagraph(nota);
                AplicarEstiloBase(row.Cells[4], estiloBase);
            }
            if (!string.IsNullOrWhiteSpace(fondo))
                for (int i = 0; i <= 3; i++) row.Cells[i].Shading.Color = ParseColor(fondo);
            for (int i = 0; i <= 3; i++) AplicarCaja(row.Cells[i], bordes);
        }

        private static void AgregarFilaFormulaPdf(Table t, string etiqueta, decimal valor, ReportColumnDefinition columna, ReportColumnSnapshot snapshot, ReportTableBorder bordes, ColumnaPersonalizada? estiloBase)
        {
            var row = t.AddRow();
            row.Cells[0].MergeRight = 2;
            row.Cells[0].AddParagraph(etiqueta);
            AplicarEstiloBase(row.Cells[0], estiloBase, true, true);
            row.Cells[3].AddParagraph(FormatearValor(valor, columna, snapshot));
            AplicarEstiloBase(row.Cells[3], estiloBase, true);
            row.Cells[3].Format.Alignment = MParagraphAlignment.Right;
            for (int i = 0; i <= 3; i++) AplicarCaja(row.Cells[i], bordes);
        }

        private static void AgregarFilaTextoPdf(Table t, string texto, ColumnaPersonalizada? estiloBase, string? fondo, ReportTableBorder bordes)
        {
            var row = t.AddRow();
            row.Cells[0].MergeRight = 3;
            row.Cells[0].AddParagraph(texto);
            AplicarEstiloBase(row.Cells[0], estiloBase);
            if (!string.IsNullOrWhiteSpace(fondo))
                row.Cells[0].Shading.Color = ParseColor(fondo);
            AplicarCaja(row.Cells[0], bordes);
        }

        private static void AplicarCaja(Cell cell, ReportTableBorder borde)
        {
            cell.Borders.Visible = borde.Visible;
            cell.Borders.Color = ParseColor(borde.ColorHex);
            cell.Borders.Width = Unit.FromPoint(borde.GrosorPuntos);
            cell.Format.SpaceBefore = 0;
            cell.Format.SpaceAfter = 0;
            cell.Format.SpaceBefore = Unit.FromPoint(1);
            cell.Format.SpaceAfter = Unit.FromPoint(1);
        }

        private static MParagraphAlignment ConvertirAlineacionTexto(string alineacion)
        {
            return (alineacion ?? string.Empty) switch
            {
                "Centro" => MParagraphAlignment.Center,
                "Derecha" => MParagraphAlignment.Right,
                _ => MParagraphAlignment.Left
            };
        }

        private static MColor ParseColor(string value)
        {
            try
            {
                var c = DrawingColorTranslator(value);
                return new MColor(c.R, c.G, c.B);
            }
            catch
            {
                return MColor.Parse("#000000");
            }
        }

        private static DrawingColor DrawingColorTranslator(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DrawingColor.Black;
            try
            {
                return System.Drawing.ColorTranslator.FromHtml(value);
            }
            catch
            {
                return DrawingColor.Black;
            }
        }

        private static string SanitizarNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre.Substring(0, 40) : nombre;
        }
    }
}
