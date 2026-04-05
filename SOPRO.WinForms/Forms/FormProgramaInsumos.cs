using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormProgramaInsumos : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly ProgramacionInsumosService _service = new();
        private readonly ProgramacionInsumosGanttService _ganttService = new();
        private bool _cargando;
        private bool _layoutPersistenceReady;
        private bool _programaReloadPending;
        private bool _visualRefreshPending;
        private bool _splitterTrackingSuspended;
        private ProgramaInsumosResultDto? _actual;
        private GanttTimelineControl? _ganttControl;

        private List<ColumnaProgramaInsumos> _columnasConfig = new();
        private bool _cargandoColumnas = false;
        private ColumnaProgramaInsumos? _colRibbon;
        private ColumnaPersonalizada? _columnaRibbon;

        public DataGridView GridPrincipal => dgvProgramaInsumos;
        public DataGridView GridBusqueda => dgvProgramaInsumos;
        public event EventHandler? ColumnaSeleccionadaCambiada;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon!;

        private enum VistaProgramaInsumos
        {
            Cantidades,
            Importes,
            Mixto
        }

        public FormProgramaInsumos(SOPROContext context, Proyecto proyecto)
        {
            _context = context;
            _proyecto = proyecto;
            InitializeComponent();
            ConfigurarFormulario();
            ConfigurarGrid();
            ConfigurarGantt();

            // Suscribirse a cambios de configuración de decimales
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados;
        }

        private void OnDecimalesActualizados(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // Actualizar propiedades de decimales sin reasignar el campo readonly
            var proyFresco = _context.Proyectos.Find(_proyecto.Id);
            if (proyFresco != null)
            {
                _proyecto.DecimalesCantidad   = proyFresco.DecimalesCantidad;
                _proyecto.DecimalesImporte    = proyFresco.DecimalesImporte;
                _proyecto.DecimalesPorcentaje = proyFresco.DecimalesPorcentaje;
            }
            if (IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) SolicitarRecargaProgramaInsumos(true); }));
        }

        private void ConfigurarFormulario()
        {
            FormRenderHelper.OptimizarRender(this);
            ControlRenderHelper.HabilitarDobleBuffer(panelTop);
            ControlRenderHelper.HabilitarDobleBuffer(toolStripTop);
            lblTitulo.Text = "PROGRAMA DE INSUMOS";
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.ProgramaInsumos).Attach();
            lblSubtitulo.Text = $"Proyecto: {_proyecto.Nombre}";
            lblResumen.Text = "Consumos programados por período.";
            lblEstado.Text = "Módulo de insumos listo.";
            this.MinimumSize = new Size(1200, 700);
        }

        private void ConfigurarGrid()
        {
            dgvProgramaInsumos.AplicarEstiloSOPRO();
            dgvProgramaInsumos.MultiSelect = true;
            dgvProgramaInsumos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvProgramaInsumos.AllowUserToAddRows = false;
            dgvProgramaInsumos.AllowUserToDeleteRows = false;
            dgvProgramaInsumos.AllowUserToResizeRows = false;
            dgvProgramaInsumos.ReadOnly = true;
            dgvProgramaInsumos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvProgramaInsumos.AllowUserToOrderColumns = false;
            dgvProgramaInsumos.ColumnHeadersHeight = 36;
            dgvProgramaInsumos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvProgramaInsumos.RowHeadersVisible = false;
            DgvCeldaHelper.Aplicar(dgvProgramaInsumos);
            dgvProgramaInsumos.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvProgramaInsumos.ColumnWidthChanged += dgvProgramaInsumos_ColumnWidthChanged;
            dgvProgramaInsumos.SelectionChanged += dgvProgramaInsumos_SelectionChanged;
            dgvProgramaInsumos.VisibleChanged += (_, __) => SolicitarActualizacionVisualInsumos();
            ControlRenderHelper.HabilitarDobleBuffer(dgvProgramaInsumos);
        }

        private void ConfigurarGantt()
        {
            _ganttControl = new GanttTimelineControl
            {
                Dock = DockStyle.Fill,
                FooterDisplayMode = GanttFooterDisplayMode.Ninguno,
                RenderModel = new GanttRenderModel()
            };
            _ganttControl.BindGrid(dgvProgramaInsumos);
            _ganttControl.TimelineCellWidthChanged += (_, __) => GuardarLayoutPersistido();
            _ganttControl.VisualSettingsChanged += (_, __) => GuardarLayoutPersistido();
            panelGantt.Controls.Add(_ganttControl);
            splitPrincipal.SplitterMoved += splitPrincipal_SplitterMoved;
        }

        public bool GenerarReporteExcel()
        {
            try
            {
                if (_actual == null || _ganttControl == null || _ganttControl.RenderModel == null)
                {
                    MessageBox.Show("No hay un programa de insumos cargado para exportar.", "Programa de insumos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                var ganttModel = _ganttControl.RenderModel;
                if (ganttModel.Filas.Count == 0 || ganttModel.Escala.Count == 0)
                {
                    MessageBox.Show("El programa de insumos no tiene datos suficientes para generar el reporte Excel.", "Programa de insumos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                FormatoHelper.EstablecerProyecto(_proyecto);

                var tituloReporte = ObtenerTituloReporteProgramaInsumos();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaInsumos, lblTitulo.Text);
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Excel del programa de insumos",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"{tituloReporte.Replace(' ', '_')}_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK)
                    return false;

                Cursor = Cursors.WaitCursor;
                var generador = new Services.GeneradorExcelProgramaInsumos(svc);
                var ruta = generador.Generar(
                    _proyecto,
                    plantilla,
                    dgvProgramaInsumos,
                    ganttModel,
                    _ganttControl.VisualSettings,
                    _ganttControl.FooterDisplayMode,
                    _ganttControl.TimelineCellWidth,
                    tituloReporte,
                    dlg.FileName,
                    tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte generado:\n" + ruta + "\n\n¿Abrir ahora?", "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ruta,
                        UseShellExecute = true
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte de insumos:\n" + ex.Message, "Programa de insumos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colRibbon == null) return;
            _colRibbon.NombreFuente = fmt.NombreFuente;
            _colRibbon.TamanoFuente = fmt.TamanoFuente;
            _colRibbon.Negrita = fmt.Negrita;
            _colRibbon.Cursiva = fmt.Cursiva;
            _colRibbon.Alineacion = fmt.Alineacion;
            _colRibbon.ColorFondo = fmt.ColorFondo;
            _colRibbon.ColorFuente = fmt.ColorFuente;
            _colRibbon.WrapTexto = fmt.WrapTexto;
            _colRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();
            CargarProgramaInsumos();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (var cfg in _columnasConfig)
            {
                cfg.NombreFuente = fmt.NombreFuente;
                cfg.TamanoFuente = fmt.TamanoFuente;
                cfg.Negrita = fmt.Negrita;
                cfg.Cursiva = fmt.Cursiva;
                cfg.Alineacion = fmt.Alineacion;
                cfg.ColorFuente = fmt.ColorFuente;
                cfg.WrapTexto = fmt.WrapTexto;
                cfg.AlineacionVertical = fmt.AlineacionVertical;
                cfg.FechaModificacion = DateTime.Now;
            }
            _context.SaveChanges();
            CargarProgramaInsumos();
        }

        private void NotificarColumnaSeleccionada(int colIndex)
        {
            _colRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvProgramaInsumos.Columns.Count)
            {
                var col = dgvProgramaInsumos.Columns[colIndex];
                if (col.Tag is ColumnaProgramaInsumos cfg)
                {
                    _colRibbon = cfg;
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

        private void dgvProgramaInsumos_ColumnWidthChanged(object? sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null || e.Column.Name == "colDummy") return;
            if (e.Column.Tag is not ColumnaProgramaInsumos cfg) return;
            if (e.Column.Width <= 20) return;
            var db = _context.ColumnasProgramaInsumos.Find(cfg.Id);
            if (db == null) return;
            db.AnchoColumna = e.Column.Width;
            db.FechaModificacion = DateTime.Now;
            _context.SaveChanges();
        }

        private void dgvProgramaInsumos_SelectionChanged(object? sender, EventArgs e)
        {
            if (_cargando)
                return;

            SolicitarActualizacionVisualInsumos();
        }

        private void splitPrincipal_SplitterMoved(object? sender, SplitterEventArgs e)
        {
            if (_splitterTrackingSuspended)
                return;

            GuardarLayoutPersistido();
            SolicitarActualizacionVisualInsumos();
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            ColumnasProgramaInsumosHelper.SincronizarDesdeGrid(_context, _proyecto.Id, dgvProgramaInsumos.Columns);
            using var form = new FormColumnasAPU(_context, _proyecto.Id, FormColumnasAPU.ModoColumnas.ProgramaInsumos);
            if (form.ShowDialog(this) == DialogResult.OK || form.CambiosRealizados)
                SolicitarRecargaProgramaInsumos(true);
        }

        private void FormProgramaInsumos_Load(object sender, EventArgs e)
        {
            _cargando = true;
            _layoutPersistenceReady = false;
            _splitterTrackingSuspended = true;

            SuspendLayout();
            splitPrincipal.SuspendLayout();
            dgvProgramaInsumos.SuspendLayout();
            panelGantt.SuspendLayout();

            cboTipo.ComboBox.DataSource = Enum.GetValues(typeof(ProgramaInsumoTipo));
            cboVista.ComboBox.DataSource = Enum.GetValues(typeof(VistaProgramaInsumos));

            var state = FormProgramaInsumosLayoutStateStore.Load(_proyecto.Id);
            cboTipo.ComboBox.SelectedItem = state?.TipoInsumo is int tipoGuardado && Enum.IsDefined(typeof(ProgramaInsumoTipo), tipoGuardado)
                ? (ProgramaInsumoTipo)tipoGuardado
                : ProgramaInsumoTipo.Materiales;
            cboVista.ComboBox.SelectedItem = state?.Vista != null && Enum.TryParse<VistaProgramaInsumos>(state.Vista, out var vistaGuardada)
                ? vistaGuardada
                : VistaProgramaInsumos.Cantidades;

            if (state != null)
            {
                if (state.GanttPanelWidth > 120 && state.GanttPanelWidth < splitPrincipal.Width - 160)
                    splitPrincipal.SplitterDistance = Math.Max(320, splitPrincipal.Width - state.GanttPanelWidth - splitPrincipal.SplitterWidth);
                if (_ganttControl != null)
                {
                    if (state.GanttCellWidth.HasValue)
                        _ganttControl.TimelineCellWidth = state.GanttCellWidth.Value;
                    _ganttControl.VisualSettings = state.ToVisualSettings();
                }
            }

            CargarProgramaInsumos();

            panelGantt.ResumeLayout(true);
            dgvProgramaInsumos.ResumeLayout(true);
            splitPrincipal.ResumeLayout(true);
            ResumeLayout(true);

            _splitterTrackingSuspended = false;
            _cargando = false;
            _layoutPersistenceReady = true;
            SolicitarActualizacionVisualInsumos(true);
        }

        private void cboTipo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            SolicitarRecargaProgramaInsumos();
        }

        private void btnRecargar_Click(object sender, EventArgs e) => SolicitarRecargaProgramaInsumos(true);

        private void cboVista_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            SolicitarRecargaProgramaInsumos();
        }

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private string ObtenerTituloReporteProgramaInsumos()
        {
            var vista = cboVista.ComboBox.SelectedItem is VistaProgramaInsumos v ? v : VistaProgramaInsumos.Cantidades;
            var baseTitle = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaInsumos, lblTitulo.Text).TextoTitulo;
            return vista switch
            {
                VistaProgramaInsumos.Importes => $"{baseTitle} - Erogaciones",
                VistaProgramaInsumos.Mixto => $"{baseTitle} - Mixto",
                _ => $"{baseTitle} - Cantidades"
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            GuardarLayoutPersistido();
            base.OnFormClosing(e);
        }

        private void CargarProgramaInsumos()
        {
            var tipo = cboTipo.ComboBox.SelectedItem is ProgramaInsumoTipo t ? t : ProgramaInsumoTipo.Materiales;
            var vista = cboVista.ComboBox.SelectedItem is VistaProgramaInsumos v ? v : VistaProgramaInsumos.Cantidades;
            var estado = DataGridViewStateHelper.Capture(dgvProgramaInsumos);
            var selectedInsumoId = ObtenerInsumoSeleccionadoId();

            _cargando = true;
            dgvProgramaInsumos.SuspendLayout();
            splitPrincipal.SuspendLayout();
            try
            {
                _actual = _service.Build(_context, _proyecto, tipo);
                ConstruirGrid(_actual, tipo);
                ColumnasProgramaInsumosHelper.SincronizarDesdeGrid(_context, _proyecto.Id, dgvProgramaInsumos.Columns);
                _columnasConfig = ColumnasProgramaInsumosHelper.ObtenerColumnas(_context, _proyecto.Id);
                AplicarConfiguracionColumnas();
                DataGridViewStateHelper.Restore(dgvProgramaInsumos, estado);
                RestaurarSeleccionInsumo(selectedInsumoId);

                if (dgvProgramaInsumos.Rows.Count > 0 && dgvProgramaInsumos.CurrentCell == null)
                {
                    var safeCell = DataGridViewStateHelper.GetFirstVisibleCell(dgvProgramaInsumos);
                    if (safeCell != null)
                    {
                        dgvProgramaInsumos.ClearSelection();
                        safeCell.OwningRow.Selected = true;
                        dgvProgramaInsumos.CurrentCell = safeCell;
                    }
                }
                else if (dgvProgramaInsumos.Rows.Count == 0)
                {
                    dgvProgramaInsumos.ClearSelection();
                }

                ActualizarGanttInsumos(tipo, vista);

                lblResumen.Text = _actual.Periodos.Count == 0 ? "No hay programa de obra generado o no existen periodos." : $"{_actual.Rows.Count:N0} insumos · {_actual.Periodos.Count:N0} periodos · Tipo: {tipo} · Vista: {vista}";
                lblEstado.Text = _actual.Rows.Count == 0 ? "Sin datos de programa de insumos." : $"Total insumos: {_actual.Rows.Count:N0}";
            }
            finally
            {
                splitPrincipal.ResumeLayout(true);
                dgvProgramaInsumos.ResumeLayout(true);
                _cargando = false;
            }

            SolicitarActualizacionVisualInsumos(true);
            GuardarLayoutPersistido();
        }


        private void SolicitarRecargaProgramaInsumos(bool forzar = false)
        {
            if (_programaReloadPending)
                return;

            _programaReloadPending = true;
            BeginInvokeSeguro(() =>
            {
                _programaReloadPending = false;
                if (IsDisposed)
                    return;

                if (_cargando && !forzar)
                    return;

                CargarProgramaInsumos();
            });
        }

        private void SolicitarActualizacionVisualInsumos(bool forzar = false)
        {
            if (_visualRefreshPending)
                return;

            _visualRefreshPending = true;
            BeginInvokeSeguro(() =>
            {
                _visualRefreshPending = false;
                if (IsDisposed || _ganttControl == null || !_ganttControl.Visible)
                    return;

                if (_cargando && !forzar)
                    return;

                _ganttControl.SelectedActivityId = ObtenerInsumoSeleccionadoId();
                _ganttControl.RequestRefresh();
            });
        }

        private void BeginInvokeSeguro(Action action)
        {
            if (IsDisposed)
                return;

            if (IsHandleCreated)
            {
                BeginInvoke(action);
                return;
            }

            if (_ganttControl != null && _ganttControl.IsHandleCreated)
            {
                _ganttControl.BeginInvoke(action);
                return;
            }

            EventHandler? handler = null;
            handler = (_, __) =>
            {
                HandleCreated -= handler;
                if (!IsDisposed)
                    BeginInvoke(action);
            };
            HandleCreated += handler;
        }


        private void RestaurarSeleccionInsumo(int? insumoId)
        {
            if (!insumoId.HasValue || dgvProgramaInsumos.Rows.Count == 0)
                return;

            foreach (DataGridViewRow row in dgvProgramaInsumos.Rows)
            {
                if (row.Tag is not int rowId || rowId != insumoId.Value)
                    continue;

                var firstVisibleCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible && c.OwningColumn.Visible);
                if (firstVisibleCell == null)
                    return;

                dgvProgramaInsumos.ClearSelection();
                row.Selected = true;
                dgvProgramaInsumos.CurrentCell = firstVisibleCell;
                return;
            }
        }

        private void ActualizarGanttInsumos(ProgramaInsumoTipo tipo, VistaProgramaInsumos vista)
        {
            if (_ganttControl == null)
                return;

            var mode = vista switch
            {
                VistaProgramaInsumos.Cantidades => GanttViewMode.ProgramaObra,
                VistaProgramaInsumos.Importes => GanttViewMode.Erogaciones,
                VistaProgramaInsumos.Mixto => GanttViewMode.Mixto,
                _ => GanttViewMode.ProgramaObra
            };

            var model = _ganttService.Build(_context, _proyecto, tipo, mode, out var programa);
            _actual = programa;
            _ganttControl.FooterDisplayMode = mode == GanttViewMode.Erogaciones ? GanttFooterDisplayMode.ImportePeriodo : GanttFooterDisplayMode.Ninguno;
            _ganttControl.RenderModel = model;
            _ganttControl.SelectedActivityId = ObtenerInsumoSeleccionadoId();
            _ganttControl.RequestRefresh();
        }

        private int? ObtenerInsumoSeleccionadoId()
        {
            if (dgvProgramaInsumos.CurrentRow?.Tag is int id)
                return id;
            if (dgvProgramaInsumos.SelectedRows.Count > 0 && dgvProgramaInsumos.SelectedRows[0].Tag is int selectedId)
                return selectedId;
            return null;
        }

        private void ConstruirGrid(ProgramaInsumosResultDto dto, ProgramaInsumoTipo tipo)
        {
            var vista = cboVista.ComboBox.SelectedItem is VistaProgramaInsumos v ? v : VistaProgramaInsumos.Cantidades;
            dgvProgramaInsumos.SuspendLayout();
            dgvProgramaInsumos.Columns.Clear();
            dgvProgramaInsumos.Rows.Clear();

            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", Width = 110, /*Frozen = true,*/ ReadOnly = true });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", Width = 420, /*Frozen = true,*/ ReadOnly = true });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", Width = 80, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = CenterStyle() });
            if (tipo == ProgramaInsumoTipo.Maquinaria)
                dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRendimiento", HeaderText = "Rendimiento", Width = 110, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = DecimalStyle() });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colInicio", HeaderText = "Inicio", Width = 95, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = DateStyle() });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTermino", HeaderText = "Término", Width = 95, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = DateStyle() });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDondeSeUsa", HeaderText = "Dónde se usa", Width = 180, /*Frozen = true,*/ ReadOnly = true });
            if (vista != VistaProgramaInsumos.Cantidades)
                dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPU", HeaderText = "P.U.", Width = 90, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = MoneyStyle() });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "Total", Width = 90, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = DecimalStyle() });
            if (vista != VistaProgramaInsumos.Cantidades)
                dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colImporteTotal", HeaderText = "Importe total", Width = 110, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = MoneyStyle(FontStyle.Bold) });

            foreach (var p in dto.Periodos.OrderBy(x => x.Orden))
            {
                if (vista != VistaProgramaInsumos.Importes)
                {
                    dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = $"per_{p.PeriodoId}", HeaderText = p.Etiqueta, Width = 105, ReadOnly = true, DefaultCellStyle = DecimalStyle() });
                    dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = $"acu_{p.PeriodoId}", HeaderText = $"Acum {p.Orden:00}", Width = 110, ReadOnly = true, DefaultCellStyle = DecimalStyle(FontStyle.Bold) });
                }
                if (vista != VistaProgramaInsumos.Cantidades)
                {
                    dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = $"imp_{p.PeriodoId}", HeaderText = $"Imp. {p.Orden:00}", Width = 105, ReadOnly = true, DefaultCellStyle = MoneyStyle() });
                    dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = $"iacu_{p.PeriodoId}", HeaderText = $"Imp. acum {p.Orden:00}", Width = 115, ReadOnly = true, DefaultCellStyle = MoneyStyle(FontStyle.Bold) });
                }
            }

            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDummy",
                HeaderText = string.Empty,
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False,
                MinimumWidth = 20
            });

            foreach (var row in dto.Rows)
            {
                var cells = new List<object?>
                {
                    row.Clave,
                    row.Descripcion,
                    row.Unidad,
                };
                if (tipo == ProgramaInsumoTipo.Maquinaria)
                    cells.Add(row.Rendimiento > 0 ? FormatDecimal(row.Rendimiento) : string.Empty);
                cells.AddRange(new object?[]
                {
                    row.FechaInicio?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? string.Empty,
                    row.FechaFin?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? string.Empty,
                    row.DondeSeUsa
                });
                if (vista != VistaProgramaInsumos.Cantidades)
                    cells.Add(FormatMoney(row.PrecioUnitario));
                cells.Add(FormatDecimal(row.Total));
                if (vista != VistaProgramaInsumos.Cantidades)
                    cells.Add(FormatMoney(row.ImporteTotal));
                foreach (var p in dto.Periodos.OrderBy(x => x.Orden))
                {
                    row.CantidadesPorPeriodo.TryGetValue(p.PeriodoId, out var cant);
                    row.AcumuladosPorPeriodo.TryGetValue(p.PeriodoId, out var acum);
                    row.ImportesPorPeriodo.TryGetValue(p.PeriodoId, out var imp);
                    row.ImportesAcumuladosPorPeriodo.TryGetValue(p.PeriodoId, out var iacu);
                    if (vista != VistaProgramaInsumos.Importes)
                    {
                        cells.Add(cant == 0m ? string.Empty : FormatDecimal(cant));
                        cells.Add(acum == 0m ? string.Empty : FormatDecimal(acum));
                    }
                    if (vista != VistaProgramaInsumos.Cantidades)
                    {
                        cells.Add(imp == 0m ? string.Empty : FormatMoney(imp));
                        cells.Add(iacu == 0m ? string.Empty : FormatMoney(iacu));
                    }
                }
                int idx = dgvProgramaInsumos.Rows.Add(cells.ToArray());
                dgvProgramaInsumos.Rows[idx].Tag = row.InsumoId;
            }

            foreach (DataGridViewColumn col in dgvProgramaInsumos.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            if (dgvProgramaInsumos.Columns.Contains("colDummy"))
            {
                var dummy = dgvProgramaInsumos.Columns["colDummy"];
                dummy.DefaultCellStyle.BackColor = dgvProgramaInsumos.BackgroundColor;
                dummy.HeaderCell.Style.BackColor = dgvProgramaInsumos.ColumnHeadersDefaultCellStyle.BackColor;
            }
            dgvProgramaInsumos.ResumeLayout();
        }

        private void AplicarConfiguracionColumnas()
        {
            _cargandoColumnas = true;
            try
            {
                var columnas = dgvProgramaInsumos.Columns.Cast<DataGridViewColumn>()
                    .Where(c => c.Name != "colDummy")
                    .ToList();

                foreach (var col in columnas)
                {
                    var cfg = _columnasConfig.FirstOrDefault(c => c.NombreInterno == col.Name);
                    if (cfg == null) continue;
                    col.HeaderText = cfg.Nombre;
                    col.Visible = cfg.Visible;
                    if (cfg.AnchoColumna > 20) col.Width = cfg.AnchoColumna;
                    col.Tag = cfg;
                    AplicarEstiloInsumos(col, cfg);
                }

                var visiblesFijas = columnas.Where(c => c.Visible && c.Frozen)
                    .OrderBy(c => (_columnasConfig.FirstOrDefault(x => x.NombreInterno == c.Name)?.Orden) ?? int.MaxValue).ToList();
                var visiblesMoviles = columnas.Where(c => c.Visible && !c.Frozen)
                    .OrderBy(c => (_columnasConfig.FirstOrDefault(x => x.NombreInterno == c.Name)?.Orden) ?? int.MaxValue).ToList();

                int idx = 0;
                foreach (var col in visiblesFijas) col.DisplayIndex = idx++;
                foreach (var col in visiblesMoviles) col.DisplayIndex = idx++;
            }
            finally
            {
                _cargandoColumnas = false;
            }
        }

        private void GuardarLayoutPersistido()
        {
            if (!_layoutPersistenceReady)
                return;

            var state = new FormProgramaInsumosLayoutState
            {
                GanttPanelWidth = Math.Max(180, splitPrincipal.Panel2.Width),
                GanttCellWidth = _ganttControl?.TimelineCellWidth,
                TipoInsumo = cboTipo.ComboBox.SelectedItem is ProgramaInsumoTipo tipo ? (int)tipo : null,
                Vista = cboVista.ComboBox.SelectedItem?.ToString(),
                GanttFontFamily = _ganttControl?.VisualSettings.FontFamilyName,
                GanttFontStyle = _ganttControl == null ? null : (int?)_ganttControl.VisualSettings.FontStyle,
                GanttTextColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.TextColor.ToArgb(),
                GanttOutlineColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.OutlineColor.ToArgb(),
                GanttNormalBarColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.NormalBarColor.ToArgb(),
                GanttCriticalBarColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.CriticalBarColor.ToArgb(),
                GanttSummaryBarColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.SummaryBarColor.ToArgb()
            };
            FormProgramaInsumosLayoutStateStore.Save(_proyecto.Id, state);
        }

        private static void AplicarEstiloInsumos(DataGridViewColumn col, ColumnaProgramaInsumos cfg)
        {
            var font = new Font(cfg.NombreFuente ?? "Segoe UI", cfg.TamanoFuente > 0 ? cfg.TamanoFuente : 9,
                (cfg.Negrita ? FontStyle.Bold : FontStyle.Regular) | (cfg.Cursiva ? FontStyle.Italic : FontStyle.Regular));
            col.DefaultCellStyle.Font = font;
            col.HeaderCell.Style.Font = font;
            try { col.DefaultCellStyle.BackColor = ColorTranslator.FromHtml(cfg.ColorFondo ?? "#FFFFFF"); } catch { }
            try { col.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml(cfg.ColorFuente ?? "#000000"); } catch { }
            try { col.HeaderCell.Style.BackColor = ColorTranslator.FromHtml(cfg.ColorFondo ?? "#FFFFFF"); } catch { }
            try { col.HeaderCell.Style.ForeColor = ColorTranslator.FromHtml(cfg.ColorFuente ?? "#000000"); } catch { }
            col.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(cfg.Alineacion, cfg.AlineacionVertical);
            col.DefaultCellStyle.WrapMode = cfg.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
        }

        private DataGridViewCellStyle DecimalStyle(FontStyle style = FontStyle.Regular) => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F, style) };
        private DataGridViewCellStyle MoneyStyle(FontStyle style = FontStyle.Regular) => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F, style) };
        private DataGridViewCellStyle CenterStyle() => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
        private DataGridViewCellStyle DateStyle() => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Format = "dd/MM/yyyy" };
        private string FormatDecimal(decimal value) => value.ToString($"N{_proyecto.DecimalesCantidad}", CultureInfo.CurrentCulture);
        private string FormatMoney(decimal value) => value.ToString($"N{_proyecto.DecimalesImporte}", CultureInfo.CurrentCulture);



        public void GenerarPdfProgramaInsumos()
        {
            try
            {
                if (_ganttControl?.RenderModel == null)
                    throw new InvalidOperationException("No hay vista gantt disponible para exportar.");

                var ganttModel = _ganttControl.RenderModel;
                if (ganttModel.Filas.Count == 0 || ganttModel.Escala.Count == 0)
                    throw new InvalidOperationException("El programa de insumos no tiene datos suficientes para generar el PDF.");

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloReporte = ObtenerTituloReporteProgramaInsumos();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaInsumos, lblTitulo.Text);

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF del programa de insumos",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"{tituloReporte.Replace(' ', '_')}_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };

                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                Cursor = Cursors.WaitCursor;
                var generador = new Services.GeneradorPdfProgramaInsumos(svc);
                var ruta = generador.Generar(
                    _proyecto,
                    plantilla,
                    dgvProgramaInsumos,
                    ganttModel,
                    _ganttControl.VisualSettings,
                    tituloReporte,
                    dlg.FileName,
                    tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte generado:" + ruta + "¿Abrir ahora?", "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ruta,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el PDF del programa de insumos:" + ex.Message, "Programa de insumos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void RecalcularTodo()
        {
            SolicitarRecargaProgramaInsumos(true);
        }
    }
}