using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Exportación del catálogo de herramientas a Excel. Consume el MISMO snapshot
    /// neutral de columnas que el PDF (visibilidad, orden, ancho, formato, estilo)
    /// y NO lee columnas ni estilos del grid.
    /// </summary>
    public partial class FormCatalogoHerramientas
    {

        private void ExportarCatalogoExcel()
        {
            try
            {
                // Filas desde el dominio (contexto EF), nunca desde el grid.
                var lista = ObtenerHerramientasParaExportar();
                if (lista == null || !lista.Any())
                {
                    MessageBox.Show("No hay herramientas para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de herramientas",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Catalogo_Herramientas_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Herramientas");

                // ── Encabezado estándar SOPRO ─────────────────────────────
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Herramientas" };

                // ── Snapshot neutral compartido con el PDF ────────────────
                var snapshot = HerramientasCatalogExportResolver.BuildSnapshot(
                    _proyectoId ?? 0, lblTitulo.Text, _columnasConfig,
                    FormatoHelper.DecimalesCantidad, FormatoHelper.DecimalesImporte, FormatoHelper.DecimalesPorcentaje);
                var estiloTabla = snapshot.EstiloTabla;
                var colsVis = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                if (colsVis.Count == 0)
                    colsVis = HerramientasCatalogExportResolver.DefaultColumns().Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                int numCols = colsVis.Count > 0 ? colsVis.Count : 5;

                // ── Encabezado + Título ─────────────────────────────────
                int fila = 1;
                if (_proyectoId.HasValue)
                    fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, svcRep);

                var titulo = ws.Range(fila, 1, fila, numCols);
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoHerramientas, lblTitulo.Text)
                    : null;
                ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "CATÁLOGO DE HERRAMIENTAS");
                ws.Row(fila).Height = 24;
                fila++;

                // Encabezados de columnas (estilo desde el contrato neutral)
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
                    ws.Column(i + 1).Width = ReportColumnWidthConverter.PxToExcelWidth(def.Ancho);
                }
                ws.Row(fila).Height = 18;
                fila++;

                // Datos
                bool alt = false;
                foreach (var h2 in lista)
                {
                    for (int i = 0; i < colsVis.Count; i++)
                    {
                        var def = colsVis[i];
                        var cont = def.EstiloContenido;
                        var cell = ws.Cell(fila, i + 1);

                        cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(
                            HerramientasCatalogExportResolver.ResolveCellBackground(def, estiloTabla, alt));
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

                        if (HerramientasCatalogExportResolver.EsPrecioUnitario(def) && !h2.EsPorcentajeMO)
                        {
                            // Paridad grid↔export: valor numérico + formato monetario
                            // neutral (símbolo '$' + DecimalesImporte del proyecto).
                            cell.Value = h2.PrecioUnitario;
                            cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(def, snapshot);
                        }
                        else if (HerramientasCatalogExportResolver.EsPrecioUnitario(def) && h2.EsPorcentajeMO)
                        {
                            // Herramienta %MO: porcentaje con DecimalesPorcentaje del proyecto.
                            cell.Value = ReportColumnGridFormat.FormatearPorcentajePdf(h2.PrecioUnitario, snapshot) + "%";
                        }
                        else
                        {
                            cell.Value = HerramientasCatalogExportResolver.ResolveValue(h2, def);
                        }
                    }

                    ws.Row(fila).Height = CalcularAlturaFilaCatalogoNeutral(colsVis, fila, ws, 14);
                    fila++;
                    alt = !alt;
                }

                ws.Range(2, 1, fila - 1, numCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(2, 1, fila - 1, numCols).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

                wb.SaveAs(dlg.FileName);

                if (MessageBox.Show("Catálogo exportado.\n\n¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
