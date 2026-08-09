using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Exportación del catálogo a Excel.
    /// </summary>
    public partial class FormCatalogoManoObra
    {

        private void ExportarCatalogoExcel()
        {
            try
            {
                var lista = dgvManoObra.DataSource as List<ManoDeObra>;
                if (lista == null || !lista.Any())
                {
                    MessageBox.Show("No hay registros para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Selección de tipo de reporte
                using var dlgTipo = new FormSeleccionReporteMO();
                if (dlgTipo.ShowDialog(this) != DialogResult.OK) return;

                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Mano de Obra" };

                // Reporte tabulador desglosado FSR
                if (dlgTipo.Seleccion == FormSeleccionReporteMO.TipoReporte.TabuladorFSR)
                {
                    if (proyecto == null || string.IsNullOrEmpty(proyecto.ParametrosFSR))
                    {
                        MessageBox.Show(
                            "Este proyecto no tiene parámetros FSR configurados.\n\n" +
                            "Configure el FSR en el módulo correspondiente antes de generar este reporte.",
                            "Sin parámetros FSR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    using var dlgFSR = new SaveFileDialog
                    {
                        Title = "Guardar tabulador FSR",
                        Filter = "Excel (*.xlsx)|*.xlsx",
                        FileName = $"TabuladorFSR_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    };
                    if (dlgFSR.ShowDialog() != DialogResult.OK) return;

                    using var wbFSR = new XLWorkbook();
                    GeneradorExcelFSR.GenerarAE2C(wbFSR, proyecto, lista, plantilla, svcRep);
                    wbFSR.SaveAs(dlgFSR.FileName);

                    if (MessageBox.Show("Reporte generado exitosamente.\n¿Desea abrirlo?",
                        "Reporte generado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlgFSR.FileName) { UseShellExecute = true });
                    return;
                }

                // Reporte Catálogo (flujo original)
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de mano de obra",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Catalogo_ManoObra_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Mano de Obra");

                var colsVis = _columnasConfig.Where(c => c.Visible).ToList();
                int numCols = colsVis.Any() ? colsVis.Count : 7;

                // ── Encabezado + Título ─────────────────────────────────
                int fila = 1;
                if (_proyectoId.HasValue)
                    fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, svcRep);

                var titulo = ws.Range(fila, 1, fila, numCols);
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoManoObra, lblTitulo.Text)
                    : null;
                ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "CATÁLOGO DE MANO DE OBRA");
                ws.Row(fila).Height = 24;
                fila++;

                // Encabezados de columnas
                if (colsVis.Any())
                {
                    for (int i = 0; i < colsVis.Count; i++)
                    {
                        var h = ws.Cell(fila, i + 1);
                        h.Value = colsVis[i].Nombre;
                        h.Style.Font.Bold = true;
                        h.Style.Fill.BackgroundColor = XLColor.FromHtml("#4A4A6A");
                        h.Style.Font.FontColor = XLColor.White;
                        h.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Column(i + 1).Width = colsVis[i].AnchoColumna / 7.0;
                    }
                }
                else
                {
                    string[] hdrs = { "Clave", "Descripción", "Unidad", "Salario Base", "FSR", "Salario Real", "Origen" };
                    for (int i = 0; i < hdrs.Length; i++)
                    {
                        var h = ws.Cell(fila, i + 1);
                        h.Value = hdrs[i];
                        h.Style.Font.Bold = true;
                        h.Style.Fill.BackgroundColor = XLColor.FromHtml("#4A4A6A");
                        h.Style.Font.FontColor = XLColor.White;
                        h.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }
                ws.Row(fila).Height = 18;
                fila++;

                // Datos
                bool alt = false;
                foreach (var mo in lista)
                {
                    string fondo = alt ? "#F5F5F5" : "#FFFFFF";
                    alt = !alt;

                    if (colsVis.Any())
                    {
                        for (int i = 0; i < colsVis.Count; i++)
                        {
                            var cell = ws.Cell(fila, i + 1);
                            cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                            if (colsVis[i].Negrita) cell.Style.Font.Bold = true;
                            if (colsVis[i].Cursiva) cell.Style.Font.Italic = true;
                            if (!string.IsNullOrEmpty(colsVis[i].NombreFuente))
                                cell.Style.Font.FontName = colsVis[i].NombreFuente;
                            if (colsVis[i].TamanoFuente > 0)
                                cell.Style.Font.FontSize = colsVis[i].TamanoFuente;
                            cell.Style.Alignment.WrapText = colsVis[i].WrapTexto;
                            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                            switch (colsVis[i].NombreInterno)
                            {
                                case "Clave": cell.Value = mo.Clave ?? ""; break;
                                case "Descripcion": cell.Value = mo.Descripcion ?? ""; break;
                                case "Unidad": cell.Value = mo.Unidad ?? ""; break;
                                case "SalarioBase":
                                    cell.Value = mo.SalarioBase;
                                    cell.Style.NumberFormat.Format = "#,##0.00";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    break;
                                case "FactorSalarioReal":
                                    cell.Value = mo.FactorSalarioReal;
                                    cell.Style.NumberFormat.Format = "0.0000";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    break;
                                case "SalarioReal":
                                    cell.Value = mo.SalarioReal;
                                    cell.Style.NumberFormat.Format = "#,##0.00";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    break;
                                case "Origen":
                                    cell.Value = mo.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                    break;
                            }
                        }
                    }
                    else
                    {
                        ws.Cell(fila, 1).Value = mo.Clave ?? "";
                        ws.Cell(fila, 2).Value = mo.Descripcion ?? "";
                        ws.Cell(fila, 3).Value = mo.Unidad ?? "";
                        ws.Cell(fila, 4).Value = mo.SalarioBase; ws.Cell(fila, 4).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(fila, 5).Value = mo.FactorSalarioReal; ws.Cell(fila, 5).Style.NumberFormat.Format = "0.0000";
                        ws.Cell(fila, 6).Value = mo.SalarioReal; ws.Cell(fila, 6).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(fila, 7).Value = mo.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";
                        ws.Range(fila, 1, fila, 7).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                    }

                    ws.Row(fila).Height = CalcularAlturaFilaCatalogo(colsVis, fila, ws, 14);
                    fila++;
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
    }
}
