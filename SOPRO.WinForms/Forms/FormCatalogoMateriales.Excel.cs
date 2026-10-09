using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.Models.Catalogs;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Exportación del catálogo de materiales a Excel. Consume el mismo snapshot
    /// neutral de columnas que el PDF (visibilidad, orden, ancho, formato, estilo).
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        private void ExportarCatalogoExcel()
        {
            try
            {
                var materiales = dgvMateriales.DataSource as List<MaterialListItem>;
                if (materiales == null || !materiales.Any())
                {
                    MessageBox.Show("No hay materiales para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de materiales",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Catalogo_Materiales_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Materiales");

                // ── Encabezado estándar SOPRO ─────────────────────────────
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Materiales" };

                // ── Snapshot neutral compartido con el PDF ────────────────
                var snapshot = MaterialCatalogExportResolver.BuildSnapshot(_proyectoId ?? 0, lblTitulo.Text, _columnasConfig);
                var estiloTabla = snapshot.EstiloTabla;
                var colsVis = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                if (colsVis.Count == 0)
                    colsVis = MaterialCatalogExportResolver.DefaultColumns().Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                int numCols = colsVis.Count > 0 ? colsVis.Count : 5;

                // ── Encabezado + Título ─────────────────────────────────
                int fila = 1;
                if (_proyectoId.HasValue)
                    fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, svcRep);

                var titulo = ws.Range(fila, 1, fila, numCols);
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoMateriales, lblTitulo.Text)
                    : null;
                ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "CATÁLOGO DE MATERIALES");
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
                foreach (var m in materiales)
                {
                    for (int i = 0; i < colsVis.Count; i++)
                    {
                        var def = colsVis[i];
                        var cont = def.EstiloContenido;
                        var cell = ws.Cell(fila, i + 1);

                        cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(
                            MaterialCatalogExportResolver.ResolveCellBackground(def, estiloTabla, alt));
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

                        if (def.EsNumerica && def.Identificador == "PrecioUnitario")
                        {
                            cell.Value = m.PrecioUnitario;
                            string formato = string.IsNullOrWhiteSpace(def.FormatoNumerico)
                                ? MaterialCatalogExportResolver.FormatoNumericoPredeterminado
                                : def.FormatoNumerico;
                            cell.Style.NumberFormat.Format = ReportNumberFormatMapper.ToExcelFormat(formato);
                        }
                        else
                        {
                            cell.Value = MaterialCatalogExportResolver.ResolveValue(m, def);
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
