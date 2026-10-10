using ClosedXML.Excel;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte Excel del catálogo de Maquinaria (Costo Horario).
    /// Consume el MISMO <see cref="ReportColumnSnapshot"/> neutral que la ruta PDF
    /// (visibilidad, orden, encabezado, ancho, alineación, wrap, estilo y formato
    /// numérico), garantizando paridad de cantidades e importes con el grid y con
    /// el PDF.
    /// </summary>
    public static class GeneradorExcelCostoHorario
    {
        public static void Generar(
            XLWorkbook wb,
            IEnumerable<Maquinaria> lista,
            Proyecto proyecto,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            ReporteService svc,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (wb == null) throw new ArgumentNullException(nameof(wb));
            if (lista == null) throw new ArgumentNullException(nameof(lista));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (svc == null) throw new ArgumentNullException(nameof(svc));

            var maquinas = lista.ToList();
            var estiloTabla = snapshot.EstiloTabla;
            var colsVis = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (colsVis.Count == 0)
                colsVis = CostoHorarioReportSnapshotBuilder.DefaultColumns()
                    .Where(c => c.Visible)
                    .OrderBy(c => c.Orden)
                    .ToList();
            int numCols = colsVis.Count > 0 ? colsVis.Count : 5;

            var ws = wb.Worksheets.Add("Costo Horario");

            // ── Encabezado estándar SOPRO ─────────────────────────────
            int fila = 1;
            fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, svc);

            // ── Título del análisis ───────────────────────────────────
            var titulo = ws.Range(fila, 1, fila, numCols);
            ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "ANÁLISIS DE COSTO HORARIO DE MAQUINARIA Y EQUIPO");
            ws.Row(fila).Height = 24;
            fila++;

            // ── Encabezados de las columnas (estilo del contrato neutral) ─────
            for (int i = 0; i < colsVis.Count; i++)
            {
                var def = colsVis[i];
                var enc = def.EstiloEncabezado;
                var h = ws.Cell(fila, i + 1);
                h.Value = def.Encabezado ?? string.Empty;
                h.Style.Font.Bold = enc.Negrita;
                h.Style.Font.Italic = enc.Cursiva;
                if (!string.IsNullOrEmpty(enc.Fuente))
                    h.Style.Font.FontName = enc.Fuente;
                if (enc.Tamano > 0)
                    h.Style.Font.FontSize = enc.Tamano;
                h.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(enc.ColorFondo ?? "#4A4A6A");
                h.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(enc.ColorFuente, "#FFFFFF");
                h.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                h.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Column(i + 1).Width = ReportColumnWidthConverter.PxToExcelWidth(def.Ancho);
            }
            ws.Row(fila).Height = 18;
            fila++;

            // ── Datos ─────────────────────────────────────────────────
            bool alt = false;
            foreach (var maq in maquinas)
            {
                for (int i = 0; i < colsVis.Count; i++)
                {
                    var def = colsVis[i];
                    var cont = def.EstiloContenido;
                    var cell = ws.Cell(fila, i + 1);

                    cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(
                        ResolveCellBackground(def, estiloTabla, alt));
                    cell.Style.Font.Bold = cont.Negrita;
                    cell.Style.Font.Italic = cont.Cursiva;
                    if (!string.IsNullOrEmpty(cont.Fuente))
                        cell.Style.Font.FontName = cont.Fuente;
                    if (cont.Tamano > 0)
                        cell.Style.Font.FontSize = cont.Tamano;
                    cell.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(cont.ColorFuente, "#000000");
                    cell.Style.Alignment.WrapText = def.Wrap;
                    cell.Style.Alignment.Vertical = ConvertirAlineacionVertical(def.AlineacionVertical);
                    cell.Style.Alignment.Horizontal = ConvertirAlineacion(def.Alineacion);

                    if (MaquinariaReportValues.TryGetValorNumerico(maq, def.Identificador, out var numerico))
                    {
                        cell.Value = numerico;
                        // Paridad grid↔export: símbolo de moneda centralizado + decimales
                        // de importe (o de cantidad) del proyecto, no el token legacy crudo.
                        cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(def, snapshot);
                    }
                    else
                    {
                        cell.Value = MaquinariaReportValues.ResolveTexto(maq, def.Identificador);
                    }
                }

                ws.Row(fila).Height = CalcularAlturaFilaCatalogoNeutral(colsVis, fila, ws, 14);
                fila++;
                alt = !alt;
            }

            // Borde general de la tabla
            ws.Range(1, 1, fila - 1, numCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(1, 1, fila - 1, numCols).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            // Ajuste de impresión: una hoja vertical
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.CenterHorizontally = true;
            ws.PageSetup.Margins.Left = 0.30;
            ws.PageSetup.Margins.Right = 0.30;
            ws.PageSetup.Margins.Top = 0.35;
            ws.PageSetup.Margins.Bottom = 0.35;
            ws.PageSetup.Margins.Header = 0.15;
            ws.PageSetup.Margins.Footer = 0.15;
            ws.ShowGridLines = false;
            ws.SheetView.FreezeRows(1);
        }

        private static string ResolveCellBackground(ReportColumnDefinition columna, ReportTableStyle tabla, bool esFilaAlterna)
        {
            var porColumna = columna.EstiloContenido.ColorFondo;
            var porDefectoTabla = tabla.EstiloContenido.ColorFondo ?? "#FFFFFF";
            var esOverrideColumna = !string.IsNullOrWhiteSpace(porColumna)
                && !string.Equals(porColumna, porDefectoTabla, StringComparison.OrdinalIgnoreCase);

            if (esOverrideColumna)
                return porColumna!;

            if (esFilaAlterna && !string.IsNullOrWhiteSpace(tabla.FilaAlterna.ColorFondoAlterno))
                return tabla.FilaAlterna.ColorFondoAlterno!;

            return string.IsNullOrWhiteSpace(porColumna) ? porDefectoTabla : porColumna!;
        }

        private static XLAlignmentHorizontalValues ConvertirAlineacion(ReportTextAlignment a) => a switch
        {
            ReportTextAlignment.Centro => XLAlignmentHorizontalValues.Center,
            ReportTextAlignment.Derecha => XLAlignmentHorizontalValues.Right,
            ReportTextAlignment.Justificado => XLAlignmentHorizontalValues.Justify,
            _ => XLAlignmentHorizontalValues.Left,
        };

        private static XLAlignmentVerticalValues ConvertirAlineacionVertical(ReportVerticalAlignment v) => v switch
        {
            ReportVerticalAlignment.Superior => XLAlignmentVerticalValues.Top,
            ReportVerticalAlignment.Inferior => XLAlignmentVerticalValues.Bottom,
            _ => XLAlignmentVerticalValues.Center,
        };

        private static double CalcularAlturaFilaCatalogoNeutral(IReadOnlyList<ReportColumnDefinition> colsVis, int fila, IXLWorksheet ws, double alturaBase)
        {
            if (colsVis == null || colsVis.Count == 0) return alturaBase;

            double altura = alturaBase;
            for (int i = 0; i < colsVis.Count; i++)
            {
                var def = colsVis[i];
                if (!def.Wrap) continue;

                var valor = ws.Cell(fila, i + 1).GetFormattedString();
                if (string.IsNullOrWhiteSpace(valor)) continue;

                var cont = def.EstiloContenido;
                using var font = new Font(
                    string.IsNullOrWhiteSpace(cont.Fuente) ? "Segoe UI" : cont.Fuente,
                    Math.Max(8f, cont.Tamano > 0 ? cont.Tamano : 9f),
                    cont.Negrita ? FontStyle.Bold : FontStyle.Regular);

                int anchoPx = Math.Max(24, (int)Math.Round(def.Ancho - 8d));
                var proposed = new Size(anchoPx, int.MaxValue);
                var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
                var measured = TextRenderer.MeasureText(valor, font, proposed, flags);
                double alturaPts = Math.Max(alturaBase, measured.Height * 72.0 / 96.0 + 6);
                if (alturaPts > altura) altura = alturaPts;
            }

            return altura;
        }
    }
}
