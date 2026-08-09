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

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Exportación del catálogo de herramientas a Excel.
    /// </summary>
    public partial class FormCatalogoHerramientas
    {

        private void ExportarCatalogoExcel()
        {
            try
            {
                var lista = dgvHerramientas.DataSource as List<Herramienta>;
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
                var colsVis = _columnasConfig.Where(c => c.Visible).ToList();
                int numCols = colsVis.Any() ? colsVis.Count : 4;

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
                    string[] hdrs = { "Clave", "Descripción", "Unidad", "Precio/Porcentaje" };
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
                foreach (var h2 in lista)
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
                                case "Clave": cell.Value = h2.Clave ?? ""; break;
                                case "Descripcion": cell.Value = h2.Descripcion ?? ""; break;
                                case "Unidad": cell.Value = h2.Unidad ?? ""; break;
                                case "PrecioUnitario":
                                    if (h2.EsPorcentajeMO)
                                    {
                                        cell.Value = $"{h2.PrecioUnitario:N2}%";
                                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    }
                                    else
                                    {
                                        cell.Value = h2.PrecioUnitario;
                                        cell.Style.NumberFormat.Format = "#,##0.00";
                                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        ws.Cell(fila, 1).Value = h2.Clave ?? "";
                        ws.Cell(fila, 2).Value = h2.Descripcion ?? "";
                        ws.Cell(fila, 3).Value = h2.Unidad ?? "";
                        if (h2.EsPorcentajeMO)
                            ws.Cell(fila, 4).Value = $"{h2.PrecioUnitario:N2}%";
                        else
                        {
                            ws.Cell(fila, 4).Value = h2.PrecioUnitario;
                            ws.Cell(fila, 4).Style.NumberFormat.Format = "#,##0.00";
                        }
                        ws.Range(fila, 1, fila, 4).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
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
