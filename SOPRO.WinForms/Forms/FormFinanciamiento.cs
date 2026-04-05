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
    /// Módulo de cálculo de financiamiento conforme al RLOPSRM.
    /// Calcula el %F desde el flujo de caja del programa de obra
    /// y lo guarda en ConfiguracionesFinanciamiento.
    /// El botón Transferir lo copia a Proyecto.PorcentajeFinanciamiento
    /// para que el módulo de Porcentajes lo recoja.
    /// </summary>
    public partial class FormFinanciamiento : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly FinanciamientoCalculationService _service = new();
        private ConfiguracionFinanciamiento _config = null!;
        private bool _cargando;
        private bool _aplicandoLayoutColumnas;
        private ColumnaFinanciamiento? _colFinRibbon;
        private ColumnaPersonalizada? _columnaRibbon;
        private Label? _lblCostoDirectoRef;
        private Label? _lblCostoDirectoValor;
        private Label? _lblCostoIndirectoRef;
        private Label? _lblCostoIndirectoValor;

        public DataGridView GridPrincipal => dgvFlujo;
        public DataGridView GridBusqueda => dgvFlujo;
        public event EventHandler? ColumnaSeleccionadaCambiada;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon!;

        /// <summary>Notifica a FormPresupuesto que el porcentaje cambió.</summary>
        public static event EventHandler? FinanciamientoTransferido;

        public FormFinanciamiento(SOPROContext context, Proyecto proyecto)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));
            FormatoHelper.EstablecerProyecto(_proyecto);
            _cargando = true;
            InitializeComponent();
            ConfigurarComboModeloFinanciamiento();
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.Financiamiento).Attach();
            ConfigurarGrid();
            EnsureReferenceLabels();
            _cargando = false;
        }


        public bool GenerarReporteExcel() => ExportarReporteExcel();

        public void GenerarPdfFinanciamiento()
        {
            try
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
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF de financiamiento",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Financiamiento_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnasCfg = ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id)
                    .Where(c => c.Visible)
                    .OrderBy(c => c.Orden)
                    .ToList();

                var baseRows = BuildDisplayRows();
                var baseRowsPdf = baseRows.Values
                    .OrderBy(x => x.NumeroPeriodo)
                    .Select(x => new GeneradorPdfFinanciamiento.BaseRowInfo
                    {
                        NumeroPeriodo = x.NumeroPeriodo,
                        CostoDirecto = x.CostoDirecto,
                        CostoIndirecto = x.CostoIndirecto
                    })
                    .ToList();

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Financiamiento, lblTitulo.Text);
                var generador = new GeneradorPdfFinanciamiento(svcRep);
                string ruta = generador.Generar(_proyecto, plantilla, columnasCfg, _config, filas, baseRowsPdf, dlg.FileName, tituloCfg);
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
            if (_colFinRibbon == null) return;

            _colFinRibbon.NombreFuente = fmt.NombreFuente;
            _colFinRibbon.TamanoFuente = fmt.TamanoFuente;
            _colFinRibbon.Negrita = fmt.Negrita;
            _colFinRibbon.Cursiva = fmt.Cursiva;
            _colFinRibbon.Alineacion = fmt.Alineacion;
            _colFinRibbon.ColorFondo = fmt.ColorFondo;
            _colFinRibbon.ColorFuente = fmt.ColorFuente;
            _colFinRibbon.WrapTexto = fmt.WrapTexto;
            _colFinRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colFinRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvFlujo.Columns)
            {
                if (col.Tag == _colFinRibbon && col is DataGridViewTextBoxColumn txtCol)
                {
                    AplicarEstiloDesdeColFin(txtCol, _colFinRibbon);
                    break;
                }
            }

            FormatoHelper.AjustarAutoAlturaFilas(dgvFlujo);
            dgvFlujo.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (var colFin in ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id))
            {
                colFin.NombreFuente = fmt.NombreFuente;
                colFin.TamanoFuente = fmt.TamanoFuente;
                colFin.Negrita = fmt.Negrita;
                colFin.Cursiva = fmt.Cursiva;
                colFin.Alineacion = fmt.Alineacion;
                colFin.ColorFuente = fmt.ColorFuente;
                colFin.WrapTexto = fmt.WrapTexto;
                colFin.AlineacionVertical = fmt.AlineacionVertical;
                colFin.FechaModificacion = DateTime.Now;
            }

            _context.SaveChanges();
            AplicarLayoutColumnas();
            dgvFlujo.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colFinRibbon = null;
            _columnaRibbon = null;

            if (colIndex >= 0 && colIndex < dgvFlujo.Columns.Count)
            {
                var col = dgvFlujo.Columns[colIndex];
                if (col.Tag is ColumnaFinanciamiento cfg)
                {
                    _colFinRibbon = cfg;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = cfg.Nombre,
                        NombreFuente = cfg.NombreFuente,
                        TamanoFuente = cfg.TamanoFuente,
                        Negrita = cfg.Negrita,
                        Cursiva = cfg.Cursiva,
                        Alineacion = cfg.Alineacion,
                        ColorFondo = cfg.ColorFondo,
                        ColorFuente = cfg.ColorFuente,
                        WrapTexto = cfg.WrapTexto,
                        AlineacionVertical = cfg.AlineacionVertical,
                    };
                }
            }

            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        private void ConfigurarGrid()
        {
            dgvFlujo.AplicarEstiloSOPRO();
            dgvFlujo.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvFlujo.AllowUserToAddRows = false;
            dgvFlujo.AllowUserToDeleteRows = false;
            dgvFlujo.AllowUserToResizeRows = false;
            dgvFlujo.ReadOnly = true;
            dgvFlujo.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFlujo.MultiSelect = true;
            dgvFlujo.AllowUserToOrderColumns = false;
            dgvFlujo.ColumnHeadersHeight = 40;
            dgvFlujo.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvFlujo.RowTemplate.Height = 35;
            dgvFlujo.BackgroundColor = Color.White;
            dgvFlujo.BorderStyle = BorderStyle.None;
            dgvFlujo.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvFlujo.GridColor = Color.FromArgb(230, 230, 230);
            dgvFlujo.EnableHeadersVisualStyles = false;
            dgvFlujo.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvFlujo.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvFlujo.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvFlujo.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvFlujo.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvFlujo.ScrollBars = ScrollBars.Both;
            DgvCeldaHelper.Aplicar(dgvFlujo);
            dgvFlujo.ColumnWidthChanged += dgvFlujo_ColumnWidthChanged;
            dgvFlujo.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);

            foreach (DataGridViewColumn col in dgvFlujo.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        private void EnsureReferenceLabels()
        {
            if (_lblCostoDirectoRef != null)
                return;

            _lblCostoDirectoRef = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(10, 82),
                Name = "lblCostoDirectoRef",
                Text = "Costo Directo:"
            };

            _lblCostoDirectoValor = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 51, 76),
                Location = new Point(140, 82),
                Name = "lblCostoDirectoValor",
                Text = "$0.00"
            };

            _lblCostoIndirectoRef = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(10, 102),
                Name = "lblCostoIndirectoRef",
                Text = "Costo Indirecto:"
            };

            _lblCostoIndirectoValor = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 51, 76),
                Location = new Point(140, 102),
                Name = "lblCostoIndirectoValor",
                Text = "$0.00"
            };

            gbBaseCalculo.Height = 128;
            gbBaseCalculo.Controls.Add(_lblCostoDirectoRef);
            gbBaseCalculo.Controls.Add(_lblCostoDirectoValor);
            gbBaseCalculo.Controls.Add(_lblCostoIndirectoRef);
            gbBaseCalculo.Controls.Add(_lblCostoIndirectoValor);
        }

        private void ActualizarEtiquetasReferencia()
        {
            EnsureReferenceLabels();

            var baseRows = BuildDisplayRows();
            decimal totalCD = baseRows.Values.Sum(x => x.CostoDirecto);
            decimal totalCI = baseRows.Values.Sum(x => x.CostoIndirecto);

            if (_lblCostoDirectoValor != null)
                _lblCostoDirectoValor.Text = totalCD.ToStringImporte();
            if (_lblCostoIndirectoValor != null)
                _lblCostoIndirectoValor.Text = totalCI.ToStringImporte();
        }

        private void AplicarEstiloDesdeColFin(DataGridViewTextBoxColumn dgvCol, ColumnaFinanciamiento col)
        {
            var alineacion = col.Alineacion switch
            {
                AlineacionColumna.Centro => DataGridViewContentAlignment.MiddleCenter,
                AlineacionColumna.Derecha => DataGridViewContentAlignment.MiddleRight,
                _ => DataGridViewContentAlignment.MiddleLeft,
            };

            FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                         | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
            string fuente = !string.IsNullOrWhiteSpace(col.NombreFuente) ? col.NombreFuente : "Segoe UI";
            float tam = col.TamanoFuente > 0 ? col.TamanoFuente : 9f;

            dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
            dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
            dgvCol.DefaultCellStyle.Format = col.FormatoNumerico ?? string.Empty;
            dgvCol.DefaultCellStyle.Font = new Font(fuente, tam, fs);
            dgvCol.DefaultCellStyle.ForeColor = TryColor(col.ColorFuente, Color.Black);
            dgvCol.DefaultCellStyle.BackColor = TryColor(col.ColorFondo, Color.White);
        }

        private static Color TryColor(string? hex, Color fallback)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(hex) ? ColorTranslator.FromHtml(hex) : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void AplicarLayoutColumnas()
        {
            _aplicandoLayoutColumnas = true;
            try
            {
                var columnas = ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id);
                foreach (DataGridViewColumn col in dgvFlujo.Columns)
                {
                    if (col.Name == "colDummy")
                        continue;
                    var cfg = columnas.FirstOrDefault(c => c.NombreInterno == col.Name)
                          ?? (col.Name == "colCobroNeto" ? columnas.FirstOrDefault(c => c.NombreInterno == "colCobro") : null);
                    if (cfg == null)
                        continue;
                    col.Tag = cfg;
                    col.HeaderText = cfg.Nombre;
                    col.Visible = cfg.Visible;
                    col.Width = cfg.AnchoColumna;
                    if (col is DataGridViewTextBoxColumn txtCol)
                        AplicarEstiloDesdeColFin(txtCol, cfg);
                    if (cfg.Orden > 0)
                        col.DisplayIndex = Math.Max(0, cfg.Orden - 1);
                }

                FormatoHelper.AjustarAutoAlturaFilas(dgvFlujo);
            }
            finally
            {
                _aplicandoLayoutColumnas = false;
            }
        }

        private void GuardarLayoutColumnas()
        {
            if (_aplicandoLayoutColumnas) return;
            var columnas = ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id);
            foreach (var cfg in columnas)
            {
                var col = dgvFlujo.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Name == cfg.NombreInterno || (cfg.NombreInterno == "colCobro" && c.Name == "colCobroNeto"));
                if (col == null || col.Name == "colDummy")
                    continue;
                cfg.Visible = col.Visible;
                cfg.AnchoColumna = col.Width;
                cfg.Orden = col.DisplayIndex + 1;
                cfg.FechaModificacion = DateTime.Now;
            }
            _context.SaveChanges();
        }

        private void dgvFlujo_ColumnWidthChanged(object? sender, DataGridViewColumnEventArgs e)
        {
            if (_aplicandoLayoutColumnas || e?.Column == null || e.Column.Name == "colDummy")
                return;
            GuardarLayoutColumnas();
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            using var form = new FormColumnasAPU(_context, _proyecto.Id, FormColumnasAPU.ModoColumnas.Financiamiento);
            if (form.ShowDialog(this) == DialogResult.OK || form.CambiosRealizados)
            {
                AplicarLayoutColumnas();
            }
        }

        private void FormFinanciamiento_Load(object sender, EventArgs e)
        {
            try
            {
                _cargando = true;
                _config = _service.ObtenerOCrear(_context, _proyecto.Id);

                lblProyecto.Text = $"📁 {_proyecto.Nombre}";

                // Cargar parámetros guardados
                nudTIIE.Value = Math.Min(_config.TasaTIIE, nudTIIE.Maximum);
                nudPuntos.Value = Math.Min(_config.PuntosAdicionales, nudPuntos.Maximum);
                nudAnticipo.Value = Math.Min(_config.PorcentajeAnticipo, nudAnticipo.Maximum);
                nudPeriodosAmort.Value = 1;
                nudPeriodosAmort.Enabled = false;
                nudDesfase.Value = Math.Min(_config.DesfaseCobro, nudDesfase.Maximum);
                rbAcumulable.Checked = _config.BaseCalculo == "Acumulable";
                rbSobreCD.Checked = _config.BaseCalculo == "SobreCD";

                // Verificar si hay programa de obra
                bool tienePrograma = _context.ProgramasObra
                    .Any(p => p.ProyectoId == _proyecto.Id && p.Activo);

                if (!tienePrograma)
                {
                    lblSinPrograma.Visible = true;
                    btnCalcular.Enabled = false;
                }

                ActualizarTasaEfectiva();
                MostrarResultados();
                ActualizarEtiquetasReferencia();

                // Cargar flujo si ya fue calculado
                if (_config.FilasFlujo.Count > 0 || _context.FilasFlujoCajaFinanciamiento
                        .Any(f => f.ConfiguracionFinanciamientoId == _config.Id))
                    CargarTablaFlujo();
                AplicarLayoutColumnas();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No fue posible inicializar la configuración de financiamiento para este proyecto." + "El esquema detectado no es compatible o el proyecto fue creado por una versión intermedia/importador en pruebas." +
                    $"Detalle técnico: {ex.Message}",
                    "Financiamiento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
            }
            finally
            {
                _cargando = false;
            }
        }

        // ── Parámetros ────────────────────────────────────────────────────────

        private void ActualizarTasaEfectiva()
        {
            lblTasaEfectiva.Text = $"Tasa efectiva: {nudTIIE.Value + nudPuntos.Value:N4}% anual";
        }

        private void nudTIIE_ValueChanged(object s, EventArgs e) { if (!_cargando) ActualizarTasaEfectiva(); }
        private void nudPuntos_ValueChanged(object s, EventArgs e) { if (!_cargando) ActualizarTasaEfectiva(); }
        private void ConfigurarComboModeloFinanciamiento()
        {
            cboModeloFinanciamiento.Items.Clear();
            cboModeloFinanciamiento.Items.AddRange(new object[] { "Clásico", "Dual" });
            cboModeloFinanciamiento.DropDownStyle = ComboBoxStyle.DropDownList;
            cboModeloFinanciamiento.SelectedIndexChanged -= cboModeloFinanciamiento_SelectedIndexChanged;
            cboModeloFinanciamiento.SelectedIndexChanged += cboModeloFinanciamiento_SelectedIndexChanged;
            cboModeloFinanciamiento.SelectedIndex = 0;
        }

        private void cboModeloFinanciamiento_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cargando || _config == null)
                return;

            RecalcularTodo();
        }

        private bool EsModeloDualSeleccionado()
            => string.Equals(Convert.ToString(cboModeloFinanciamiento.SelectedItem), "Dual", StringComparison.OrdinalIgnoreCase);

        private decimal GetTasaPeriodoLabel(int diasPeriodo, decimal saldoAcumulado)
        {
            decimal tasaAnual = (_config.TasaTIIE + _config.PuntosAdicionales) / 100m;
            if (EsModeloDualSeleccionado() && saldoAcumulado > 0m)
                tasaAnual = _config.TasaTIIE / 100m;

            if (tasaAnual <= 0m || diasPeriodo <= 0)
                return 0m;
            return decimal.Round(tasaAnual * diasPeriodo / 365m * 100m, 4, MidpointRounding.AwayFromZero);
        }


        // ── Calcular ──────────────────────────────────────────────────────────

        private void btnCalcular_Click(object sender, EventArgs e)
        {
            try
            {
                GuardarParametros();
                Cursor = Cursors.WaitCursor;

                decimal porcF = _service.Calcular(_context, _config, _proyecto, EsModeloDualSeleccionado());

                // Recargar entidad con filas
                _context.Entry(_config).Reload();
                _context.Entry(_config).Collection(c => c.FilasFlujo).Load();

                CargarTablaFlujo();
                AplicarLayoutColumnas();
                MostrarResultados();
                ActualizarEtiquetasReferencia();

                Cursor = Cursors.Default;
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al calcular:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GuardarParametros()
        {
            if (_config == null)
                return;

            _config.TasaTIIE = nudTIIE.Value;
            _config.PuntosAdicionales = nudPuntos.Value;
            _config.PorcentajeAnticipo = nudAnticipo.Value;
            _config.PeriodosAmortizacionAnticipo = 1;
            _config.DesfaseCobro = (int)nudDesfase.Value;
            _config.BaseCalculo = rbAcumulable.Checked ? "Acumulable" : "SobreCD";
            _context.SaveChanges();
        }

        // ── Tabla de flujo de caja ────────────────────────────────────────────

        private void CargarTablaFlujo()
        {
            var filas = _context.FilasFlujoCajaFinanciamiento
                .Where(f => f.ConfiguracionFinanciamientoId == _config.Id)
                .OrderBy(f => f.NumeroPeriodo)
                .ToList();

            var baseRows = BuildDisplayRows();

            dgvFlujo.SuspendLayout();
            dgvFlujo.Rows.Clear();

            string fmtImp = $"N{Math.Max(0, _proyecto.DecimalesImporte)}";
            foreach (var fila in filas)
            {
                baseRows.TryGetValue(fila.NumeroPeriodo, out var baseRow);
                decimal cobroNeto = fila.EstimacionCobrada - fila.AmortizacionAnticipo;
                decimal tasaPeriodo = GetTasaPeriodoLabel(fila.DiasPeriodo, fila.SaldoAcumulado);

                var idx = dgvFlujo.Rows.Add(
                    fila.Etiqueta,
                    fila.FechaInicio.ToString("dd/MM/yy"),
                    fila.FechaFin.ToString("dd/MM/yy"),
                    fila.DiasPeriodo,
                    (baseRow?.CostoDirecto ?? 0m).ToString(fmtImp, CultureInfo.CurrentCulture),
                    (baseRow?.CostoIndirecto ?? 0m).ToString(fmtImp, CultureInfo.CurrentCulture),
                    fila.Egresos.ToString(fmtImp, CultureInfo.CurrentCulture),
                    fila.AnticipoRecibido > 0 ? fila.AnticipoRecibido.ToString(fmtImp, CultureInfo.CurrentCulture) : string.Empty,
                    fila.EstimacionCobrada > 0 ? fila.EstimacionCobrada.ToString(fmtImp, CultureInfo.CurrentCulture) : string.Empty,
                    fila.AmortizacionAnticipo > 0 ? fila.AmortizacionAnticipo.ToString(fmtImp, CultureInfo.CurrentCulture) : string.Empty,
                    cobroNeto != 0 ? cobroNeto.ToString(fmtImp, CultureInfo.CurrentCulture) : string.Empty,
                    fila.FlujoNeto.ToString(fmtImp, CultureInfo.CurrentCulture),
                    fila.SaldoAcumulado.ToString(fmtImp, CultureInfo.CurrentCulture),
                    tasaPeriodo.ToString("N4", CultureInfo.CurrentCulture) + "%",
                    fila.InteresPeriodo.ToString("N4", CultureInfo.CurrentCulture));

                var row = dgvFlujo.Rows[idx];
                if (fila.SaldoAcumulado < 0)
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
                else if (fila.SaldoAcumulado > 0)
                    row.DefaultCellStyle.BackColor = Color.FromArgb(235, 255, 235);

                if (EsModeloDualSeleccionado())
                {
                    if (fila.InteresPeriodo < 0)
                        row.Cells["colInteres"].Style.ForeColor = Color.DarkRed;
                    else if (fila.InteresPeriodo > 0)
                        row.Cells["colInteres"].Style.ForeColor = Color.DarkGreen;
                }
                else if (fila.InteresPeriodo > 0)
                {
                    row.Cells["colInteres"].Style.ForeColor = fila.SaldoAcumulado < 0 ? Color.DarkRed : Color.DarkGreen;
                }
            }

            dgvFlujo.ResumeLayout();
        }

        private Dictionary<int, DisplayFlowBaseRow> BuildDisplayRows()
        {
            var result = new Dictionary<int, DisplayFlowBaseRow>();

            var programa = _context.ProgramasObra
                .AsNoTracking()
                .FirstOrDefault(p => p.ProyectoId == _proyecto.Id && p.Activo);
            if (programa == null)
                return result;

            var periodos = _context.PeriodosPrograma
                .AsNoTracking()
                .Where(p => p.ProgramaObraId == programa.Id)
                .OrderBy(p => p.NumeroPeriodo)
                .ToList();
            if (periodos.Count == 0)
                return result;

            var distribuciones = _context.DistribucionesPeriodo
                .AsNoTracking()
                .Include(d => d.ActividadProgramada)
                    .ThenInclude(a => a.ConceptoPresupuesto)
                .Where(d => d.PeriodoPrograma.ProgramaObraId == programa.Id)
                .ToList();

            foreach (var periodo in periodos)
            {
                result[periodo.NumeroPeriodo] = new DisplayFlowBaseRow
                {
                    NumeroPeriodo = periodo.NumeroPeriodo,
                    CostoDirecto = 0m,
                    CostoIndirecto = 0m
                };
            }

            var periodOrder = periodos.ToDictionary(p => p.Id, p => p.NumeroPeriodo);
            var gruposConcepto = distribuciones
                .Where(d => d.ActividadProgramada?.ConceptoPresupuesto != null
                         && !d.ActividadProgramada.ConceptoPresupuesto.EsAgrupador
                         && d.ActividadProgramada.ConceptoPresupuesto.MatrizId.HasValue)
                .GroupBy(d => d.ActividadProgramada!.ConceptoPresupuestoId!.Value);

            foreach (var grupo in gruposConcepto)
            {
                var concepto = grupo.First().ActividadProgramada!.ConceptoPresupuesto!;
                decimal cantidadConcepto = concepto.Cantidad;
                if (cantidadConcepto <= 0m)
                    continue;

                decimal cdUnit = concepto.CostoDirectoUnitario;
                if (cdUnit <= 0m && concepto.CostoDirectoTotal > 0m)
                    cdUnit = concepto.CostoDirectoTotal / cantidadConcepto;

                decimal totalCdConcepto = new MotorCalculoSopro(_proyecto).Multiplicar(cantidadConcepto, cdUnit);

                var distribucionesConcepto = grupo
                    .OrderBy(d => periodOrder.TryGetValue(d.PeriodoProgramaId, out var orden) ? orden : int.MaxValue)
                    .ToList();

                DistribuirImportePorConcepto(distribucionesConcepto, cdUnit, totalCdConcepto, result, periodOrder, true);
            }

            foreach (var periodo in periodos)
            {
                if (!result.TryGetValue(periodo.NumeroPeriodo, out var row))
                    continue;

                row.CostoDirecto = BudgetPricingService.RoundImporte(_proyecto, row.CostoDirecto);
                row.CostoIndirecto = 0m;
            }

            ReconciliarTotalesConPreview(result);

            if (periodos.Count > 0 && _config.DesfaseCobro > 0)
            {
                var ultimo = periodos[^1];
                for (int extra = 1; extra <= _config.DesfaseCobro; extra++)
                {
                    result[ultimo.NumeroPeriodo + extra] = new DisplayFlowBaseRow
                    {
                        NumeroPeriodo = ultimo.NumeroPeriodo + extra,
                        CostoDirecto = 0m,
                        CostoIndirecto = 0m
                    };
                }
            }

            var filasPersistidas = _context.FilasFlujoCajaFinanciamiento
                .AsNoTracking()
                .Where(f => f.ConfiguracionFinanciamientoId == _config.Id)
                .Select(f => new { f.NumeroPeriodo, f.Egresos })
                .ToList();
            foreach (var filaPersistida in filasPersistidas)
            {
                if (result.TryGetValue(filaPersistida.NumeroPeriodo, out var row))
                {
                    row.CostoDirecto = BudgetPricingService.RoundImporte(_proyecto, row.CostoDirecto);
                    row.CostoIndirecto = BudgetPricingService.RoundImporte(_proyecto, filaPersistida.Egresos - row.CostoDirecto);
                }
            }

            return result;
        }


        private void ReconciliarTotalesConPreview(Dictionary<int, DisplayFlowBaseRow> result)
        {
            if (result.Count == 0)
                return;

            var preview = BudgetPreviewCalculationService.BuildPreview(_context, _proyecto, new BudgetPercentageInput
            {
                CostoDirectoReferencia = 0m,
                IndirectosCentral = _proyecto.PorcentajeIndirectosCentral,
                IndirectosCampo = _proyecto.PorcentajeIndirectosCampo,
                Financiamiento = 0m,
                Utilidad = 0m,
                CargosAdicionales = 0m,
                ModoCalculoPorcentajes = _proyecto.ModoCalculoPorcentajes
            });

            var totalCiOficial = BudgetPricingService.RoundImporte(_proyecto, preview.Subtotal1 - preview.CostoDirecto);

            ReconciliarDistribucion(result, totalCiOficial, false);

            foreach (var row in result.Values)
            {
                row.CostoDirecto = BudgetPricingService.RoundImporte(_proyecto, row.CostoDirecto);
                row.CostoIndirecto = BudgetPricingService.RoundImporte(_proyecto, row.CostoIndirecto);
            }
        }

        private void ReconciliarDistribucion(Dictionary<int, DisplayFlowBaseRow> result, decimal totalOficial, bool esCostoDirecto)
        {
            var filas = result.Values
                .OrderBy(x => x.NumeroPeriodo)
                .ToList();

            decimal totalActual = filas.Sum(x => esCostoDirecto ? x.CostoDirecto : x.CostoIndirecto);
            if (totalActual == totalOficial)
                return;

            var filasConMonto = filas
                .Where(x => (esCostoDirecto ? x.CostoDirecto : x.CostoIndirecto) != 0m)
                .ToList();

            if (filasConMonto.Count == 0)
            {
                filasConMonto = filas.ToList();
                if (filasConMonto.Count == 0)
                    return;
            }

            if (totalActual == 0m)
            {
                decimal totalBase = filas.Sum(x => x.CostoDirecto);
                if (totalBase > 0m)
                {
                    decimal acumuladoBase = 0m;
                    for (int i = 0; i < filas.Count; i++)
                    {
                        var fila = filas[i];
                        decimal nuevo = i == filas.Count - 1
                            ? totalOficial - acumuladoBase
                            : BudgetPricingService.RoundImporte(_proyecto, totalOficial * fila.CostoDirecto / totalBase);

                        if (esCostoDirecto)
                            fila.CostoDirecto = nuevo;
                        else
                            fila.CostoIndirecto = nuevo;

                        acumuladoBase += nuevo;
                    }
                }
                else
                {
                    var ultima = filasConMonto[^1];
                    if (esCostoDirecto)
                        ultima.CostoDirecto = totalOficial;
                    else
                        ultima.CostoIndirecto = totalOficial;
                }
                return;
            }

            decimal acumulado = 0m;
            for (int i = 0; i < filasConMonto.Count; i++)
            {
                var fila = filasConMonto[i];
                decimal actual = esCostoDirecto ? fila.CostoDirecto : fila.CostoIndirecto;

                decimal nuevo = i == filasConMonto.Count - 1
                    ? totalOficial - acumulado
                    : BudgetPricingService.RoundImporte(_proyecto, totalOficial * actual / totalActual);

                if (esCostoDirecto)
                    fila.CostoDirecto = nuevo;
                else
                    fila.CostoIndirecto = nuevo;

                acumulado += nuevo;
            }
        }

        private void DistribuirImportePorConcepto(
            List<DistribucionPeriodo> distribucionesConcepto,
            decimal precioUnitario,
            decimal totalEsperado,
            Dictionary<int, DisplayFlowBaseRow> result,
            Dictionary<int, int> periodOrder,
            bool esCostoDirecto)
        {
            if (distribucionesConcepto.Count == 0 || totalEsperado == 0m)
                return;

            decimal suma = 0m;
            int ultimoIndiceConMonto = -1;
            var importes = new decimal[distribucionesConcepto.Count];

            for (int i = 0; i < distribucionesConcepto.Count; i++)
            {
                var distribucion = distribucionesConcepto[i];
                decimal importe = new MotorCalculoSopro(_proyecto).Multiplicar(distribucion.CantidadProgramada, precioUnitario);
                importes[i] = importe;
                suma += importe;
                if (distribucion.CantidadProgramada != 0m || importe != 0m)
                    ultimoIndiceConMonto = i;
            }

            if (ultimoIndiceConMonto < 0)
                ultimoIndiceConMonto = distribucionesConcepto.Count - 1;

            importes[ultimoIndiceConMonto] += totalEsperado - suma;

            for (int i = 0; i < distribucionesConcepto.Count; i++)
            {
                var distribucion = distribucionesConcepto[i];
                if (!periodOrder.TryGetValue(distribucion.PeriodoProgramaId, out var numeroPeriodo))
                    continue;
                if (!result.TryGetValue(numeroPeriodo, out var row))
                    continue;

                if (esCostoDirecto)
                    row.CostoDirecto += importes[i];
                else
                    row.CostoIndirecto += importes[i];
            }
        }

        private sealed class DisplayFlowBaseRow
        {
            public int NumeroPeriodo { get; set; }
            public decimal CostoDirecto { get; set; }
            public decimal CostoIndirecto { get; set; }
        }

        // ── Resultado ─────────────────────────────────────────────────────────

        private void MostrarResultados()
        {
            lblInteresesNeg.Text = _config.InteresesNegativos.ToStringImporte();
            lblInteresesPos.Text = _config.InteresesPositivos.ToStringImporte();
            lblFinanciamientoNeto.Text = _config.FinanciamientoNeto.ToStringImporte();
            lblPorcentaje.Text = $"{_config.PorcentajeCalculado:N5}%";
            lblFechaCalculo.Text = _config.FechaCalculo.HasValue
                ? $"Calculado: {_config.FechaCalculo.Value:dd/MM/yyyy HH:mm}"
                : "Sin calcular";

            ActualizarEtiquetasReferencia();

            bool tieneResultado = _config.PorcentajeCalculado != 0;
            btnTransferir.Enabled = tieneResultado;
        }

        // ── Transferir al proyecto ────────────────────────────────────────────

        private void btnTransferir_Click(object sender, EventArgs e)
        {
            if (_config.PorcentajeCalculado == 0)
            {
                var r = MessageBox.Show(
                    "El porcentaje calculado es 0%.\\n¿Deseas transferir de todas formas?",
                    "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            try
            {
                var proy = _context.Proyectos.Find(_proyecto.Id);
                if (proy == null) return;

                proy.PorcentajeFinanciamiento = _config.PorcentajeCalculado;
                _context.SaveChanges();

                _proyecto.PorcentajeFinanciamiento = _config.PorcentajeCalculado;

                MessageBox.Show(
                    $"Transferido al proyecto:\n\n" +
                    $"  % Financiamiento: {_config.PorcentajeCalculado:N5}%\n\n" +
                    "Usa el módulo de Porcentajes para revisar\n" +
                    "y aplicar al presupuesto.",
                    "Transferido ✓", MessageBoxButtons.OK, MessageBoxIcon.Information);

                FinanciamientoTransferido?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


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

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        public void RecalcularTodo()
        {
            btnCalcular_Click(this, EventArgs.Empty);
        }
    }
}