using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormUtilidad : Form, IGridFormato, IRecalculable
    {
        private readonly DataGridView _gridDummy = new DataGridView { Visible = false };
        private readonly ColumnaPersonalizada _columnaRibbon = new ColumnaPersonalizada
        {
            Nombre = "Utilidad",
            NombreInterno = "Utilidad",
            NombreFuente = "Segoe UI",
            TamanoFuente = 10,
            Negrita = false,
            Cursiva = false,
            Alineacion = AlineacionColumna.Izquierda,
            ColorFuente = "#000000",
            ColorFondo = "#FFFFFF",
            WrapTexto = false,
            AlineacionVertical = 1
        };
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly UtilidadCalculationService _service = new();
        private UtilidadCalculationResult? _resultado;

        public static event EventHandler? UtilidadTransferida;


        public DataGridView GridPrincipal => _gridDummy;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;
        public event EventHandler? ColumnaSeleccionadaCambiada;

        public bool GenerarReporteExcel() => ExportarReporteExcel();

        public void GenerarPdfUtilidad()
        {
            try
            {
                Recalcular();
                if (_resultado == null)
                {
                    MessageBox.Show("No hay cálculo de utilidad para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF de utilidad",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Utilidad_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);

                var preview = BudgetPreviewCalculationService.BuildPreview(_context, _proyecto, new BudgetPercentageInput
                {
                    CostoDirectoReferencia = 0m,
                    IndirectosCentral = _proyecto.PorcentajeIndirectosCentral,
                    IndirectosCampo = _proyecto.PorcentajeIndirectosCampo,
                    Financiamiento = _proyecto.PorcentajeFinanciamiento,
                    Utilidad = 0m,
                    CargosAdicionales = 0m,
                    ModoCalculoPorcentajes = _proyecto.ModoCalculoPorcentajes
                });

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Utilidad, lblTitulo.Text);
                var generador = new GeneradorPdfUtilidad(svcRep);
                string ruta = generador.Generar(_proyecto, plantilla, preview, _resultado, _columnaRibbon, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte PDF:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (fmt == null) return;
            CopiarFormatoRibbon(fmt);
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            if (fmt == null) return;
            CopiarFormatoRibbon(fmt);
        }

        private void CopiarFormatoRibbon(ColumnaPersonalizada fmt)
        {
            _columnaRibbon.NombreFuente = fmt.NombreFuente;
            _columnaRibbon.TamanoFuente = fmt.TamanoFuente;
            _columnaRibbon.Negrita = fmt.Negrita;
            _columnaRibbon.Cursiva = fmt.Cursiva;
            _columnaRibbon.Alineacion = fmt.Alineacion;
            _columnaRibbon.ColorFuente = fmt.ColorFuente;
            _columnaRibbon.ColorFondo = fmt.ColorFondo;
            _columnaRibbon.WrapTexto = fmt.WrapTexto;
            _columnaRibbon.AlineacionVertical = fmt.AlineacionVertical;
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }


        public FormUtilidad(SOPROContext context, Proyecto proyecto)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));
            InitializeComponent();
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.Utilidad).Attach();
            lblProyecto.Text = _proyecto.Nombre ?? $"Proyecto #{_proyecto.Id}";
        }

        private void FormUtilidad_Load(object sender, EventArgs e)
        {
            cboModo.SelectedIndex = 0;
            nudUtilidadDirecta.Value = _proyecto.PorcentajeUtilidad > 0 ? Math.Min(_proyecto.PorcentajeUtilidad, nudUtilidadDirecta.Maximum) : 10m;
            nudISR.Value = 30m;
            nudPTU.Value = 10m;
            lblModoCalculo.Text = _proyecto.ModoCalculoPorcentajes == "SobreCD" ? "Sobre CD" : "Acumulables";
            Recalcular();
        }

        private void ValoresChanged(object? sender, EventArgs e)
        {
            Recalcular();
        }

        private void Recalcular()
        {
            try
            {
                bool asistido = cboModo.SelectedIndex == 1;
                nudUtilidadNeta.Enabled = asistido;
                nudUtilidadDirecta.Enabled = !asistido;

                var preview = BudgetPreviewCalculationService.BuildPreview(_context, _proyecto, new BudgetPercentageInput
                {
                    CostoDirectoReferencia = 0m,
                    IndirectosCentral = _proyecto.PorcentajeIndirectosCentral,
                    IndirectosCampo = _proyecto.PorcentajeIndirectosCampo,
                    Financiamiento = _proyecto.PorcentajeFinanciamiento,
                    Utilidad = 0m,
                    CargosAdicionales = 0m,
                    ModoCalculoPorcentajes = _proyecto.ModoCalculoPorcentajes
                });

                lblCD.Text = preview.CostoDirecto.ToStringImporte();
                lblCI.Text = (preview.Subtotal1 - preview.CostoDirecto).ToStringImporte();
                lblF.Text = preview.MontoFinanciamiento.ToStringImporte();

                var input = new UtilidadCalculationInput
                {
                    CostoDirectoReferencia = preview.CostoDirecto,
                    IndirectosCentral = _proyecto.PorcentajeIndirectosCentral,
                    IndirectosCampo = _proyecto.PorcentajeIndirectosCampo,
                    Financiamiento = _proyecto.PorcentajeFinanciamiento,
                    UtilidadDirecta = nudUtilidadDirecta.Value,
                    UtilidadNetaDeseada = nudUtilidadNeta.Value,
                    Isr = nudISR.Value,
                    Ptu = nudPTU.Value,
                    ModoCalculoPorcentajes = _proyecto.ModoCalculoPorcentajes,
                    ModoAsistido = asistido
                };

                _resultado = _service.Calcular(_context, _proyecto, input);

                lblBase.Text = _resultado.BaseUtilidad.ToStringImporte();
                lblPorcentaje.Text = _resultado.PorcentajeUtilidadBruta.ToString("N5") + "%";
                lblImporteUtilidad.Text = _resultado.ImporteUtilidad.ToStringImporte();
                lblImporteISR.Text = _resultado.ImporteIsr.ToStringImporte();
                lblImportePTU.Text = _resultado.ImportePtu.ToStringImporte();
                lblUtilidadNeta.Text = _resultado.UtilidadNetaEstimada.ToStringImporte();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al recalcular utilidad:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ExportarReporteExcel()
        {
            try
            {
                Recalcular();
                if (_resultado == null)
                {
                    MessageBox.Show("No hay cálculo de utilidad para exportar.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                var preview = BudgetPreviewCalculationService.BuildPreview(_context, _proyecto, new BudgetPercentageInput
                {
                    CostoDirectoReferencia = 0m,
                    IndirectosCentral = _proyecto.PorcentajeIndirectosCentral,
                    IndirectosCampo = _proyecto.PorcentajeIndirectosCampo,
                    Financiamiento = _proyecto.PorcentajeFinanciamiento,
                    Utilidad = 0m,
                    CargosAdicionales = 0m,
                    ModoCalculoPorcentajes = _proyecto.ModoCalculoPorcentajes
                });

                decimal costoDirecto = BudgetPricingService.RoundImporte(_proyecto, preview.CostoDirecto);
                decimal costoIndirecto = BudgetPricingService.RoundImporte(_proyecto, preview.Subtotal1 - preview.CostoDirecto);
                decimal financiamiento = BudgetPricingService.RoundImporte(_proyecto, preview.MontoFinanciamiento);
                decimal subtotal = BudgetPricingService.RoundImporte(_proyecto, _resultado.BaseUtilidad);

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte de utilidad",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Utilidad_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return false;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Utilidad");

                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);

                int totalCols = 6;
                int fila = 1;
                fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, _proyecto, totalCols, fila, svcRep);
                ws.Row(fila).Height = Math.Max(ws.Row(fila).Height, 18);
                fila += 2;

                var titulo = ws.Range(fila, 1, fila, totalCols);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Utilidad, lblTitulo.Text);
                ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "DETERMINACIÓN DE LA UTILIDAD NETA", "#F4B183");
                ws.Row(fila).Height = 24;
                fila++;

                var subtitulo = ws.Range(fila, 2, fila, 5);
                subtitulo.Merge();
                subtitulo.Value = "ANÁLISIS, CÁLCULO E INTEGRACIÓN DE % DE UTILIDAD";
                AplicarTitulo(subtitulo, 12, true, "#FCE4D6", XLAlignmentHorizontalValues.Center);
                ws.Row(fila).Height = 22;
                fila += 2;

                int etiquetaCol = 1;
                int valorCol = 3;

                EscribirFilaMoneda(ws, fila++, etiquetaCol, valorCol, "COSTO DIRECTO", costoDirecto);
                EscribirFilaMoneda(ws, fila++, etiquetaCol, valorCol, "COSTO INDIRECTO", costoIndirecto);
                EscribirFilaMoneda(ws, fila++, etiquetaCol, valorCol, "FINANCIAMIENTO", financiamiento);
                EscribirFilaMoneda(ws, fila++, etiquetaCol, valorCol, "SUBTOTAL", subtotal, true, "#FFF2CC", "#C00000");

                fila++;

                EscribirFilaParametro(ws, fila++, "Up = Utilidad Propuesta", _resultado.PorcentajeUtilidadBruta / 100m, null, true, "#C6E0B4");
                EscribirFilaParametro(ws, fila++, "ISR = Impuesto Sobre la Renta", _resultado.Isr / 100m, "SAT");
                EscribirFilaParametro(ws, fila++, "PTU = Participación de los Trabajadores en la Utilidad", _resultado.Ptu / 100m, "LFT");

                fila++;

                ws.Range(fila, 1, fila, 3).Merge();
                ws.Cell(fila, 1).Value = "UTILIDAD NETA = Up / 1 - (ISR + PTU) =";
                ws.Cell(fila, 1).Style.Font.Bold = true;
                ws.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml("#C00000");
                ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                ws.Cell(fila, 4).Value = _resultado.PorcentajeUtilidadBruta / 100m;
                ws.Cell(fila, 4).Style.NumberFormat.Format = "0.00%";
                ws.Cell(fila, 4).Style.Font.Bold = true;
                ws.Cell(fila, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#9DC3E6");
                ws.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                AplicarBordeCaja(ws.Range(fila, 1, fila, 4));
                fila++;

                ws.Range(fila, 1, fila, 3).Merge();
                ws.Cell(fila, 1).Value = $"={_resultado.PorcentajeUtilidadNeta:N2}% / (1 - ({_resultado.Isr:N0}% + {_resultado.Ptu:N0}%))";
                ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Cell(fila, 1).Style.Font.Italic = true;
                fila += 2;

                ws.Range(fila, 1, fila, 3).Merge();
                ws.Cell(fila, 1).Value = "IMPORTE DE UTILIDAD =";
                ws.Cell(fila, 1).Style.Font.Bold = true;
                ws.Cell(fila, 1).Style.Font.FontColor = XLColor.Black;
                ws.Cell(fila, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#92D050");
                ws.Cell(fila, 4).Value = _resultado.ImporteUtilidad;
                ws.Cell(fila, 4).Style.NumberFormat.Format = "$ #,##0.00";
                ws.Cell(fila, 4).Style.Font.Bold = true;
                ws.Cell(fila, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#92D050");
                AplicarBordeCaja(ws.Range(fila, 1, fila, 4));
                fila += 2;

                EscribirFilaMoneda(ws, fila++, etiquetaCol, valorCol, "IMPORTE ISR", _resultado.ImporteIsr);
                EscribirFilaMoneda(ws, fila++, etiquetaCol, valorCol, "IMPORTE PTU", _resultado.ImportePtu);
                EscribirFilaMoneda(ws, fila++, etiquetaCol, valorCol, "UTILIDAD NETA ESTIMADA", _resultado.UtilidadNetaEstimada, true, "#D9EAD3", null);

                for (int col = 1; col <= totalCols; col++)
                    ws.Column(col).Width = col switch { 1 => 30, 2 => 4, 3 => 18, 4 => 18, 5 => 14, _ => 12 };

                ws.Columns(1, totalCols).Style.Font.FontName = _columnaRibbon.NombreFuente ?? "Segoe UI";
                ws.Columns(1, totalCols).Style.Font.FontSize = _columnaRibbon.TamanoFuente > 0 ? _columnaRibbon.TamanoFuente : 10;
                ws.Columns(1, totalCols).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Range(6, 1, 22, totalCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(6, 1, 22, totalCols).Style.Border.InsideBorder = XLBorderStyleValues.Hair;
                ws.Range(15, 1, 16, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");
                ws.Range(18, 1, 18, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
                ws.Range(20, 1, 22, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
                ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
                ws.PageSetup.Margins.Top = 1.10;
                ws.PageSetup.Margins.Bottom = 0.55;
                ws.PageSetup.FitToPages(1, 1);
                ws.ShowGridLines = true;

                wb.SaveAs(dlg.FileName);

                if (MessageBox.Show("Reporte exportado correctamente.\n\n¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar el reporte de utilidad:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static void AplicarTitulo(IXLRange range, double size, bool bold, string backColor, XLAlignmentHorizontalValues alineacion)
        {
            range.Style.Font.Bold = bold;
            range.Style.Font.FontSize = size;
            range.Style.Alignment.Horizontal = alineacion;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(backColor);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        private static void EscribirFilaMoneda(IXLWorksheet ws, int fila, int etiquetaCol, int valorCol, string etiqueta, decimal valor, bool resaltar = false, string? fondo = null, string? colorTexto = null)
        {
            ws.Range(fila, etiquetaCol, fila, valorCol - 1).Merge();
            ws.Cell(fila, etiquetaCol).Value = etiqueta;
            ws.Cell(fila, etiquetaCol).Style.Font.Italic = true;
            ws.Cell(fila, etiquetaCol).Style.Font.Bold = resaltar;

            ws.Cell(fila, valorCol).Value = valor;
            ws.Cell(fila, valorCol).Style.NumberFormat.Format = "$ #,##0.00";
            ws.Cell(fila, valorCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(fila, valorCol).Style.Font.Bold = resaltar;

            if (!string.IsNullOrWhiteSpace(fondo))
            {
                ws.Range(fila, etiquetaCol, fila, valorCol).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
            }
            if (!string.IsNullOrWhiteSpace(colorTexto))
            {
                ws.Range(fila, etiquetaCol, fila, valorCol).Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(colorTexto);
            }

            AplicarBordeCaja(ws.Range(fila, etiquetaCol, fila, valorCol));
        }

        private static void EscribirFilaParametro(IXLWorksheet ws, int fila, string etiqueta, decimal porcentaje, string? nota = null, bool resaltar = false, string? fondo = null)
        {
            ws.Range(fila, 1, fila, 3).Merge();
            ws.Cell(fila, 1).Value = etiqueta;
            ws.Cell(fila, 1).Style.Font.Bold = resaltar;
            ws.Cell(fila, 1).Style.Font.Italic = resaltar;

            ws.Cell(fila, 4).Value = porcentaje;
            ws.Cell(fila, 4).Style.NumberFormat.Format = "0.00%";
            ws.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(fila, 4).Style.Font.Bold = true;

            if (!string.IsNullOrWhiteSpace(nota))
                ws.Cell(fila, 5).Value = nota;

            if (!string.IsNullOrWhiteSpace(fondo))
                ws.Range(fila, 1, fila, 4).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);

            AplicarBordeCaja(ws.Range(fila, 1, fila, 4));
        }

        private static void AplicarBordeCaja(IXLRange range)
        {
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        }

        private void btnCalcular_Click(object sender, EventArgs e)
        {
            Recalcular();
        }


        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnTransferir_Click(object sender, EventArgs e)
        {
            try
            {
                Recalcular();
                if (_resultado == null) return;

                var proy = _context.Proyectos.Find(_proyecto.Id);
                if (proy == null) return;

                proy.PorcentajeUtilidad = _resultado.PorcentajeUtilidadBruta;
                _context.SaveChanges();
                _proyecto.PorcentajeUtilidad = proy.PorcentajeUtilidad;

                MessageBox.Show(
                    "Utilidad transferida al proyecto:\n\n" +
                    $"  % Utilidad: {_resultado.PorcentajeUtilidadBruta:N5}%\n" +
                    $"  Base CD+CI+F: {_resultado.BaseUtilidad.ToStringImporte()}\n" +
                    $"  Importe utilidad: {_resultado.ImporteUtilidad.ToStringImporte()}",
                    "Transferido", MessageBoxButtons.OK, MessageBoxIcon.Information);

                UtilidadTransferida?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al transferir utilidad:\n\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void RecalcularTodo()
        {
            Recalcular();
        }
    }
}
