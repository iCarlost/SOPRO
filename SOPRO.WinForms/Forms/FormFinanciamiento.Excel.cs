using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Reporting.Financiamiento;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Exportación a Excel del reporte de financiamiento. Consume el MISMO snapshot
    /// neutral de columnas y el MISMO modelo neutral de filas que el PDF (paridad de
    /// filas base/períodos y de formatos) y NO lee columnas ni estilos del grid.
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

            var svcRep = new ReporteService(_context);
            var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
            var columnasCfg = ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id);

            // ── Snapshot neutral y modelo compartidos con el PDF ─────────────
            var snapshot = FinanciamientoExportResolver.BuildSnapshot(
                _proyecto.Id, lblTitulo.Text, columnasCfg,
                _proyecto.DecimalesCantidad, _proyecto.DecimalesImporte, _proyecto.DecimalesPorcentaje);

            var baseRows = BuildDisplayRows().Values
                .OrderBy(x => x.NumeroPeriodo)
                .Select(x => new FinanciamientoBaseRow(x.NumeroPeriodo, x.CostoDirecto, x.CostoIndirecto))
                .ToList();

            var modelo = FinanciamientoExportResolver.BuildModel(
                _proyecto, _config, filas, baseRows, EsModeloDualSeleccionado());

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Financiamiento");

            int numCols = Math.Max(modelo.EtiquetasPeriodo.Count + 2, 9);
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

            foreach (var dato in modelo.Datos)
                EscribirDato(ws, fila++, dato, snapshot);

            fila++;

            int headerRow = fila;
            ws.Cell(headerRow, 1).Value = "CONCEPTO";
            ws.Cell(headerRow, 2).Value = string.Empty;
            for (int i = 0; i < modelo.EtiquetasPeriodo.Count; i++)
                ws.Cell(headerRow, i + 3).Value = modelo.EtiquetasPeriodo[i];
            AplicarFilaEncabezado(ws, headerRow, numCols, snapshot);
            fila++;

            foreach (var row in modelo.Matriz)
            {
                switch (row.Kind)
                {
                    case FinanciamientoMatrixRowKind.Seccion:
                        EscribirFilaSeccion(ws, fila++, numCols, row.Concepto);
                        break;
                    case FinanciamientoMatrixRowKind.Espaciador:
                        fila++;
                        break;
                    default:
                        EscribirFilaValores(ws, fila++, row, snapshot);
                        break;
                }
            }

            fila++;

            ws.Cell(fila, 1).Value = "PORCENTAJE DE FINANCIAMIENTO";
            ws.Cell(fila, 1).Style.Font.Bold = true;
            ws.Cell(fila, numCols - 1).Value = "RESULTADO";
            ws.Cell(fila, numCols - 1).Style.Font.Bold = true;
            ws.Cell(fila, numCols).Value = modelo.PorcentajeCalculado / 100m;
            ws.Cell(fila, numCols).Style.NumberFormat.Format = "0.00000%";
            ws.Cell(fila, numCols).Style.Font.Bold = true;
            ws.Cell(fila, numCols).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
            fila++;

            ws.Range(datosInicio, 1, fila - 1, numCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(datosInicio, 1, fila - 1, numCols).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            ws.SheetView.FreezeRows(headerRow);
            ws.Column(1).Width = 34;
            ws.Column(2).Width = 14;
            for (int i = 0; i < modelo.EtiquetasPeriodo.Count; i++)
                ws.Column(i + 3).Width = Math.Max(13, modelo.EtiquetasPeriodo[i].Length + 2);

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

        private void EscribirDato(IXLWorksheet ws, int fila, FinanciamientoDatoRow dato, ReportColumnSnapshot snapshot)
        {
            ws.Cell(fila, 1).Value = dato.EtiquetaIzquierda;
            AsignarValor(ws.Cell(fila, 3), dato.ValorIzquierda, snapshot);
            ws.Cell(fila, 6).Value = dato.EtiquetaDerecha;
            ws.Cell(fila, 8).Value = dato.SubEtiquetaDerecha;
            AsignarValor(ws.Cell(fila, 9), dato.ValorDerecha, snapshot);

            ws.Cell(fila, 1).Style.Font.Bold = true;
            ws.Cell(fila, 6).Style.Font.Bold = true;
            if (!string.IsNullOrWhiteSpace(dato.SubEtiquetaDerecha))
                ws.Cell(fila, 8).Style.Font.Bold = true;

            ws.Range(fila, 1, fila, 9).Style.Fill.BackgroundColor = XLColor.White;
        }

        private void AsignarValor(IXLCell cell, FinanciamientoValor valor, ReportColumnSnapshot snapshot)
        {
            if (valor.EsNumerico)
            {
                cell.Value = valor.Numero!.Value;
                cell.Style.NumberFormat.Format = FormatoNumerico(valor.Formato, snapshot);
            }
            else
            {
                cell.Value = valor.Texto;
            }
        }

        private void AplicarFilaEncabezado(IXLWorksheet ws, int fila, int numCols, ReportColumnSnapshot snapshot)
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

            var cfgPeriodo = BuscarColumna(snapshot, "colPeriodo");
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

        private void EscribirFilaValores(IXLWorksheet ws, int fila, FinanciamientoMatrixRow row, ReportColumnSnapshot snapshot)
        {
            ws.Cell(fila, 1).Value = row.Concepto;
            ws.Cell(fila, 1).Style.Font.Bold = true;

            var cfgConcepto = BuscarColumna(snapshot, row.ColumnaConcepto);
            if (cfgConcepto != null)
                AplicarFormatoExcel(ws.Range(fila, 1, fila, 1), cfgConcepto);

            var formato = FormatoNumerico(row.Formato, snapshot);
            for (int i = 0; i < row.Valores.Count; i++)
            {
                var cell = ws.Cell(fila, i + 3);
                decimal valor = row.Valores[i];
                cell.Value = valor;
                cell.Style.NumberFormat.Format = formato;
                if (valor == 0m)
                    cell.Clear(XLClearOptions.Contents);
            }

            var cfgValor = BuscarColumna(snapshot, row.ColumnaValor);
            if (cfgValor != null && row.Valores.Count > 0)
                AplicarFormatoExcel(ws.Range(fila, 3, fila, row.Valores.Count + 2), cfgValor);
        }

        private void AplicarFormatoExcel(IXLRangeBase rango, ReportColumnDefinition cfg, bool esEncabezado = false)
        {
            var estilo = esEncabezado ? cfg.EstiloEncabezado : cfg.EstiloContenido;
            if (!string.IsNullOrWhiteSpace(estilo.Fuente))
                rango.Style.Font.FontName = estilo.Fuente;
            if (estilo.Tamano > 0)
                rango.Style.Font.FontSize = estilo.Tamano;
            rango.Style.Font.Bold = estilo.Negrita || esEncabezado;
            rango.Style.Font.Italic = estilo.Cursiva;

            var colorFuente = ObtenerColorXL(estilo.ColorFuente);
            if (colorFuente != null)
                rango.Style.Font.FontColor = colorFuente;

            if (!esEncabezado)
            {
                var colorFondo = ObtenerColorXL(estilo.ColorFondo);
                if (colorFondo != null)
                    rango.Style.Fill.BackgroundColor = colorFondo;
            }

            rango.Style.Alignment.Horizontal = cfg.Alineacion switch
            {
                ReportTextAlignment.Centro => XLAlignmentHorizontalValues.Center,
                ReportTextAlignment.Derecha => XLAlignmentHorizontalValues.Right,
                ReportTextAlignment.Justificado => XLAlignmentHorizontalValues.Justify,
                _ => XLAlignmentHorizontalValues.Left
            };
            rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static ReportColumnDefinition? BuscarColumna(ReportColumnSnapshot snapshot, string identificador)
            => snapshot.Columnas.FirstOrDefault(c =>
                string.Equals(c.Identificador, identificador, StringComparison.OrdinalIgnoreCase));

        private static string FormatoNumerico(FinanciamientoValorFormato formato, ReportColumnSnapshot snapshot) => formato switch
        {
            FinanciamientoValorFormato.Moneda => ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, System.Globalization.CultureInfo.CurrentCulture),
            FinanciamientoValorFormato.Porcentaje => ReportColumnGridFormat.FormatoPorcentajeExcel(snapshot.DecimalesPorcentaje),
            FinanciamientoValorFormato.Cantidad => ReportColumnGridFormat.FormatoNumeroExcel(snapshot.DecimalesCantidad),
            FinanciamientoValorFormato.Entero => "0",
            _ => "N2",
        };

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
