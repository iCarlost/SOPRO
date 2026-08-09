using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Exportación a Excel con ClosedXML y helpers de escritura/formato.
    /// </summary>
    public partial class FormFinanciamiento
    {

        private bool ExportarReporteExcel()
        {
            var filas = _context.FilasFlujoCajaFinanciamiento
                .AsNoTracking()
                .Where(f => f.ConfiguracionFinanciamientoId == _config.Id)
                .OrderBy(f => f.NumeroPeriodo)
                .ToList();

            if (filas.Count == 0)
            {
                MessageBox.Show("No hay cálculo de financiamiento para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar reporte de financiamiento",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"Financiamiento_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return false;

            var columnasCfg = ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id)
                .ToDictionary(c => c.NombreInterno, StringComparer.OrdinalIgnoreCase);

            var baseRows = BuildDisplayRows();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Financiamiento");

            var svcRep = new ReporteService(_context);
            var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);

            int numCols = Math.Max(filas.Count + 2, 9);
            int fila = 1;

            fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, _proyecto, numCols, fila, svcRep);

            var titulo = ws.Range(fila, 1, fila, numCols);
            titulo.Merge();
            var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Financiamiento, lblTitulo.Text);
            ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "ANÁLISIS DE FINANCIAMIENTO");
            ws.Row(fila).Height = 24;
            fila++;

            int datosInicio = fila;
            ws.Cell(fila, 1).Value = "DATOS";
            ws.Cell(fila, 1).Style.Font.Bold = true;
            ws.Cell(fila, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E9EEF7");
            fila++;

            decimal totalCD = filas.Sum(x => baseRows.TryGetValue(x.NumeroPeriodo, out var b) ? b.CostoDirecto : 0m);
            decimal totalCI = filas.Sum(x => baseRows.TryGetValue(x.NumeroPeriodo, out var b) ? b.CostoIndirecto : 0m);

            EscribirDato(ws, fila++, "COSTO DIRECTO", totalCD, "INDICADOR ECONÓMICO", "TIIE", $"{_config.TasaTIIE:N4}%", formatoIzq: "#,##0.00");
            EscribirDato(ws, fila++, $"COSTO INDIRECTO = {(_proyecto.PorcentajeIndirectosCentral + _proyecto.PorcentajeIndirectosCampo):N2}%", totalCI,
                "TASA DE INTERÉS ANUAL", string.Empty, $"{(_config.TasaTIIE + _config.PuntosAdicionales):N4}%", formatoIzq: "#,##0.00");
            EscribirDato(ws, fila++, "% ANTICIPO", _config.PorcentajeAnticipo / 100m, "TASA DE INTERÉS PERIODO BASE", string.Empty,
                filas.Count > 0 ? $"{GetTasaPeriodoLabel(filas[0].DiasPeriodo, filas[0].SaldoAcumulado):N4}%" : "0.0000%", formatoIzq: "0.0000%");
            EscribirDato(ws, fila++, "DESFASE DE COBRO", _config.DesfaseCobro, "BASE DE CÁLCULO", string.Empty, _config.BaseCalculo);

            fila++;

            int headerRow = fila;
            ws.Cell(headerRow, 1).Value = "CONCEPTO";
            ws.Cell(headerRow, 2).Value = string.Empty;
            for (int i = 0; i < filas.Count; i++)
                ws.Cell(headerRow, i + 3).Value = filas[i].Etiqueta;
            AplicarFilaEncabezado(ws, headerRow, numCols, columnasCfg.TryGetValue("colPeriodo", out var cfgPeriodo) ? cfgPeriodo : null);
            fila++;

            decimal totalBase = totalCD + totalCI;
            var ingresosAcumulados = new List<decimal>();
            var egresosAcumulados = new List<decimal>();
            decimal ingresoAcum = 0m;
            decimal egresoAcum = 0m;
            foreach (var f in filas)
            {
                ingresoAcum += f.AnticipoRecibido + f.EstimacionCobrada - f.AmortizacionAnticipo;
                egresoAcum += f.Egresos;
                ingresosAcumulados.Add(new MotorCalculoSopro(_proyecto).RedondearImporte(ingresoAcum));
                egresosAcumulados.Add(new MotorCalculoSopro(_proyecto).RedondearImporte(egresoAcum));
            }

            decimal[] avanceProgramado = totalBase > 0m
                ? filas.Select(x => decimal.Round(x.Egresos / totalBase, 4, MidpointRounding.AwayFromZero)).ToArray()
                : filas.Select(_ => 0m).ToArray();

            EscribirFilaValores(ws, fila++, "AVANCE PROGRAMADO", filas, x => avanceProgramado[x], columnasCfg, "colPeriodo", "colEgresos", "0.0000%");
            fila++;

            EscribirFilaSeccion(ws, fila++, numCols, "INGRESOS");
            EscribirFilaValores(ws, fila++, "ESTIMACIONES DE OBRA (CD + CI)", filas, x => filas[x].EstimacionCobrada, columnasCfg, "colPeriodo", "colEstim", "#,##0.00");
            EscribirFilaValores(ws, fila++, "AMORTIZACIÓN ANTICIPO", filas, x => filas[x].AmortizacionAnticipo, columnasCfg, "colPeriodo", "colAmort", "#,##0.00");
            EscribirFilaValores(ws, fila++, "COBRO NETO", filas, x => filas[x].EstimacionCobrada - filas[x].AmortizacionAnticipo, columnasCfg, "colPeriodo", "colCobro", "#,##0.00");
            EscribirFilaValores(ws, fila++, "ANTICIPOS (CD + CI)", filas, x => filas[x].AnticipoRecibido, columnasCfg, "colPeriodo", "colAnticipo", "#,##0.00");
            EscribirFilaValores(ws, fila++, "INGRESOS ACUMULADOS", filas, x => ingresosAcumulados[x], columnasCfg, "colPeriodo", "colCobro", "#,##0.00");

            fila++;

            EscribirFilaSeccion(ws, fila++, numCols, "EGRESOS");
            EscribirFilaValores(ws, fila++, "COSTO DIRECTO", filas, x => baseRows.TryGetValue(filas[x].NumeroPeriodo, out var b) ? b.CostoDirecto : 0m, columnasCfg, "colPeriodo", "colCD", "#,##0.00");
            EscribirFilaValores(ws, fila++, "COSTO INDIRECTO", filas, x => baseRows.TryGetValue(filas[x].NumeroPeriodo, out var b) ? b.CostoIndirecto : 0m, columnasCfg, "colPeriodo", "colCI", "#,##0.00");
            EscribirFilaValores(ws, fila++, "C.D. + C.I.", filas, x => filas[x].Egresos, columnasCfg, "colPeriodo", "colEgresos", "#,##0.00");
            EscribirFilaValores(ws, fila++, "EGRESOS ACUMULADOS", filas, x => egresosAcumulados[x], columnasCfg, "colPeriodo", "colEgresos", "#,##0.00");

            fila++;

            EscribirFilaValores(ws, fila++, "EGRESOS ACUM - INGRESOS ACUM", filas, x => (egresosAcumulados[x] - ingresosAcumulados[x]), columnasCfg, "colPeriodo", "colSaldo", "#,##0.00");
            EscribirFilaValores(ws, fila++, "TASA PERÍODO", filas, x => GetTasaPeriodoLabel(filas[x].DiasPeriodo, filas[x].SaldoAcumulado) / 100m, columnasCfg, "colPeriodo", "colTasa", "0.0000%");
            EscribirFilaValores(ws, fila++, "COSTO FINANC. PARCIAL (INTERESES)", filas, x => filas[x].InteresPeriodo, columnasCfg, "colPeriodo", "colInteres", "#,##0.0000");
            decimal interesAcum = 0m;
            EscribirFilaValores(ws, fila++, "COSTO FINANC. ACUMULADO", filas, x =>
            {
                interesAcum += filas[x].InteresPeriodo;
                return interesAcum;
            }, columnasCfg, "colPeriodo", "colInteres", "#,##0.0000");

            fila++;

            ws.Cell(fila, 1).Value = "PORCENTAJE DE FINANCIAMIENTO";
            ws.Cell(fila, 1).Style.Font.Bold = true;
            ws.Cell(fila, numCols - 1).Value = "RESULTADO";
            ws.Cell(fila, numCols - 1).Style.Font.Bold = true;
            ws.Cell(fila, numCols).Value = _config.PorcentajeCalculado / 100m;
            ws.Cell(fila, numCols).Style.NumberFormat.Format = "0.00000%";
            ws.Cell(fila, numCols).Style.Font.Bold = true;
            ws.Cell(fila, numCols).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
            fila++;

            ws.Range(datosInicio, 1, fila - 1, numCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(datosInicio, 1, fila - 1, numCols).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            ws.SheetView.FreezeRows(headerRow);
            ws.Column(1).Width = 34;
            ws.Column(2).Width = 14;
            for (int i = 0; i < filas.Count; i++)
                ws.Column(i + 3).Width = Math.Max(13, filas[i].Etiqueta.Length + 2);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);

            wb.SaveAs(dlg.FileName);

            if (MessageBox.Show("Reporte exportado correctamente.\n\n¿Desea abrir el archivo?",
                "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }

            return true;
        }

        private void EscribirDato(
            IXLWorksheet ws,
            int fila,
            string etiquetaIzq,
            object valorIzq,
            string etiquetaDer,
            string subEtiquetaDer,
            object valorDer,
            string? formatoIzq = null,
            string? formatoDer = null)
        {
            ws.Cell(fila, 1).Value = etiquetaIzq;
            AsignarValorCeldaExcel(ws.Cell(fila, 3), valorIzq);
            ws.Cell(fila, 6).Value = etiquetaDer;
            ws.Cell(fila, 8).Value = subEtiquetaDer;
            AsignarValorCeldaExcel(ws.Cell(fila, 9), valorDer);

            ws.Cell(fila, 1).Style.Font.Bold = true;
            ws.Cell(fila, 6).Style.Font.Bold = true;
            if (!string.IsNullOrWhiteSpace(subEtiquetaDer))
                ws.Cell(fila, 8).Style.Font.Bold = true;

            AplicarFormatoDatoExcel(ws.Cell(fila, 3), valorIzq, etiquetaIzq, formatoIzq);
            AplicarFormatoDatoExcel(ws.Cell(fila, 9), valorDer, etiquetaDer, formatoDer);

            ws.Range(fila, 1, fila, 9).Style.Fill.BackgroundColor = XLColor.White;
        }

        private void AplicarFormatoDatoExcel(IXLCell cell, object? valor, string etiqueta, string? formatoForzado = null)
        {
            if (!string.IsNullOrWhiteSpace(formatoForzado))
            {
                cell.Style.NumberFormat.Format = formatoForzado;
                return;
            }

            if (valor is decimal or double or float)
            {
                cell.Style.NumberFormat.Format = etiqueta.Contains("%")
                    ? "0.0000%"
                    : "#,##0.00";
            }
            else if (valor is int or long or short)
            {
                cell.Style.NumberFormat.Format = "0";
            }
        }

        private void AplicarFilaEncabezado(IXLWorksheet ws, int fila, int numCols, ColumnaFinanciamiento? cfgPeriodo)
        {
            for (int col = 1; col <= numCols; col++)
            {
                var cell = ws.Cell(fila, col);
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4A4A6A");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Alignment.WrapText = true;
            }

            if (cfgPeriodo != null)
                AplicarFormatoExcel(ws.Range(fila, 1, fila, numCols), cfgPeriodo, esEncabezado: true);

            ws.Row(fila).Height = 28;
        }

        private void EscribirFilaSeccion(IXLWorksheet ws, int fila, int numCols, string titulo)
        {
            var rng = ws.Range(fila, 1, fila, numCols);
            rng.Merge();
            rng.Value = titulo;
            rng.Style.Font.Bold = true;
            rng.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        private void AsignarValorCeldaExcel(IXLCell cell, object? valor)
        {
            if (valor == null)
            {
                cell.Value = string.Empty;
                return;
            }

            switch (valor)
            {
                case string s:
                    cell.Value = s;
                    break;
                case decimal dec:
                    cell.Value = dec;
                    break;
                case double d:
                    cell.Value = d;
                    break;
                case float f:
                    cell.Value = f;
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case long l:
                    cell.Value = l;
                    break;
                case short sh:
                    cell.Value = sh;
                    break;
                case bool b:
                    cell.Value = b;
                    break;
                case DateTime dt:
                    cell.Value = dt;
                    break;
                default:
                    cell.Value = valor.ToString();
                    break;
            }
        }

        private void EscribirFilaValores(
            IXLWorksheet ws,
            int fila,
            string concepto,
            List<FilaFlujoCajaFinanciamiento> filas,
            Func<int, decimal> selector,
            Dictionary<string, ColumnaFinanciamiento> columnasCfg,
            string keyPeriodo,
            string keyValor,
            string formatoNumerico)
        {
            ws.Cell(fila, 1).Value = concepto;
            ws.Cell(fila, 1).Style.Font.Bold = true;

            if (columnasCfg.TryGetValue(keyPeriodo, out var cfgPeriodo))
                AplicarFormatoExcel(ws.Range(fila, 1, fila, 1), cfgPeriodo);

            for (int i = 0; i < filas.Count; i++)
            {
                var cell = ws.Cell(fila, i + 3);
                decimal valor = selector(i);
                cell.Value = valor;
                cell.Style.NumberFormat.Format = formatoNumerico;
                if (valor == 0m)
                    cell.Clear(XLClearOptions.Contents);
            }

            if (columnasCfg.TryGetValue(keyValor, out var cfgValor))
                AplicarFormatoExcel(ws.Range(fila, 3, fila, filas.Count + 2), cfgValor);
        }

        private void AplicarFormatoExcel(IXLRangeBase rango, ColumnaFinanciamiento cfg, bool esEncabezado = false)
        {
            if (!string.IsNullOrWhiteSpace(cfg.NombreFuente))
                rango.Style.Font.FontName = cfg.NombreFuente;
            if (cfg.TamanoFuente > 0)
                rango.Style.Font.FontSize = cfg.TamanoFuente;
            rango.Style.Font.Bold = cfg.Negrita || esEncabezado;
            rango.Style.Font.Italic = cfg.Cursiva;

            var colorFuente = ObtenerColorXL(cfg.ColorFuente);
            if (colorFuente != null)
                rango.Style.Font.FontColor = colorFuente;

            if (!esEncabezado)
            {
                var colorFondo = ObtenerColorXL(cfg.ColorFondo);
                if (colorFondo != null)
                    rango.Style.Fill.BackgroundColor = colorFondo;
            }

            rango.Style.Alignment.Horizontal = cfg.Alineacion switch
            {
                AlineacionColumna.Centro => XLAlignmentHorizontalValues.Center,
                AlineacionColumna.Derecha => XLAlignmentHorizontalValues.Right,
                _ => XLAlignmentHorizontalValues.Left
            };
            rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private XLColor? ObtenerColorXL(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return null;

            try
            {
                return ExcelColorHelper.SafeFromHtml(html);
            }
            catch
            {
                return null;
            }
        }
    }
}
