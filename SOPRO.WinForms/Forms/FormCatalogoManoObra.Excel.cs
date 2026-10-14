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
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;

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
                var lista = ObtenerManoDeObraParaExportacion();
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
                var plantilla = svcRep.ObtenerPlantillaSoloLectura(_proyectoId ?? 0);
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
                    // Snapshot neutral AE-2(C) compartido con la ruta PDF del Tabulador
                    // (misma lista/orden/ancho/formato). No afecta a la rama Catálogo (F1.1).
                    var snapshotAE2C = new FsrReportSnapshotBuilder().BuildAE2C(proyecto, lblTitulo.Text);
                    GeneradorExcelFSR.GenerarAE2C(wbFSR, proyecto, lista, plantilla, svcRep, snapshotAE2C);
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

                // Snapshot neutral compartido con el PDF (misma lista/orden/ancho/
                // formato/estilo). Los defaults se construyen en memoria, sin persistir.
                var snapshot = ConstruirSnapshotManoObra(_columnasConfig);
                var snapshotBase = ReportCellOverlayResolver.SinEstilos(snapshot);
                var estiloTabla = snapshot.EstiloTabla;
                var colsVis = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                if (colsVis.Count == 0)
                    colsVis = ManoObraCatalogExportResolver.DefaultColumns().Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                int numCols = colsVis.Count > 0 ? colsVis.Count : 7;

                // ── Encabezado + Título ─────────────────────────────────
                int fila = 1;
                if (_proyectoId.HasValue)
                    fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, svcRep);

                var titulo = ws.Range(fila, 1, fila, numCols);
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerSoloLectura(_proyectoId.Value, ReportTitleModuleKeys.CatalogoManoObra, lblTitulo.Text)
                    : null;
                ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "CATÁLOGO DE MANO DE OBRA");
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
                foreach (var mo in lista)
                {
                    var claveFila = ClaveFilaReporte.Insumo(mo.Id);
                    for (int i = 0; i < colsVis.Count; i++)
                    {
                        var def = colsVis[i];
                        var cont = def.EstiloContenido;
                        var cell = ws.Cell(fila, i + 1);
                        cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(
                            ManoObraCatalogExportResolver.ResolveCellBackground(def, estiloTabla, alt));
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

                        if (def.EsNumerica && ManoObraCatalogExportResolver.TryResolveNumber(mo, def, out var valor))
                        {
                            cell.Value = valor;
                            // Paridad grid↔export: la regla compartida decide el formato
                            // (monetario → símbolo '$' + decimales de importe; cantidad →
                            // decimales de cantidad).
                            cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(def, snapshot);
                        }
                        else
                        {
                            cell.Value = ManoObraCatalogExportResolver.ResolveValue(mo, def);
                        }

                        // Override de celda (máxima precedencia) sobre el estilo de columna.
                        AplicarOverlayExcel(cell.Style, ReportCellOverlayResolver.OverrideEfectivo(
                            snapshot, snapshotBase, ManoObraReportSnapshotBuilder.ClaveVista,
                            claveFila, def.Identificador));
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

        /// <summary>
        /// Aplica al estilo SOLO las propiedades no nulas del override. No toca el
        /// formato numérico: lo resuelve <see cref="ReportColumnGridFormat"/>.
        /// </summary>
        private static void AplicarOverlayExcel(IXLStyle estilo, ReportStyleOverlay overrideEfectivo)
        {
            if (estilo is null || overrideEfectivo is null) return;

            if (!string.IsNullOrWhiteSpace(overrideEfectivo.Fuente))
                estilo.Font.FontName = overrideEfectivo.Fuente;
            if (overrideEfectivo.TamanoFuente is float tamano && tamano > 0)
                estilo.Font.FontSize = tamano;
            if (overrideEfectivo.Negrita is bool negrita)
                estilo.Font.Bold = negrita;
            if (overrideEfectivo.Cursiva is bool cursiva)
                estilo.Font.Italic = cursiva;
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.ColorFuente))
                estilo.Font.FontColor = ExcelColorHelper.SafeFromHtml(overrideEfectivo.ColorFuente);
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.ColorFondo))
                estilo.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(overrideEfectivo.ColorFondo);
            if (overrideEfectivo.AlineacionHorizontal is ReportTextAlignment alineacion)
                estilo.Alignment.Horizontal = ConvertirAlineacion(alineacion);
            if (overrideEfectivo.AlineacionVertical is ReportVerticalAlignment alineacionV)
                estilo.Alignment.Vertical = ConvertirAlineacionVertical(alineacionV);
            if (overrideEfectivo.Wrap is bool wrap)
                estilo.Alignment.WrapText = wrap;
        }

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
