using System.ComponentModel;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Models;

namespace SOPRO.WinForms.Forms
{
    public partial class FormProgramaObra : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly ProgramacionLoadService _loadService = new();
        private readonly ProgramacionGenerationService _generationService = new();
        private readonly ProgramacionCalculationService _calculationService = new();
        private readonly ProgramacionPersistenceService _persistenceService = new();
        private readonly ProgramacionSynchronizationService _syncService = new();
        private readonly ProgramacionValidationService _validationService = new();
        private readonly ProgramacionDistributionService _distributionService = new();
        private readonly ProgramacionCurvaSService _curvaSService = new();
        private readonly ProgramacionGanttService _ganttService = new();

        private ProgramLoadDto? _programaActual;
        private BindingList<ActivityGridRowDto> _actividades = new();
        private BindingList<PeriodEditDto> _periodos = new();
        private BindingList<DependenciaEditableRow> _dependenciasEditables = new();
        private BindingList<CurvaSRowDto> _curvaS = new();
        private bool _cargando = false;
        private DataGridViewStateSnapshot? _pendingGridStateRestore;
        private readonly BindingList<ActividadLookupItem> _actividadesLookup = new();
        private bool _cancelandoEdicionDependencia = false;
        private ComboBox? _comboDependenciaActivo = null;

        private TabPage? _tabCurvaS;
        private SplitContainer? _splitCurvaS;
        private BufferedChartPanel? _picCurvaS;
        private DataGridView? _dgvCurvaS;
        private Label? _lblCurvaResumen;
        private Panel? _panelCurvaHerramientas;
        private GanttTimelineControl? _ganttControl;
        private bool _aplicandoLayoutPersistido = false;
        private int _ultimaAlturaPanelInferior = 243;
        private bool _curvaSRedrawPending = false;
        private bool _detalleSeleccionRefreshPending = false;
        private bool _visualProgramRefreshPending = false;
        private bool _programaReloadPending = false;
        private bool _detalleSeleccionDeferred = false;
        private int? _ultimaActividadDetalleId = null;
        private bool _layoutPersistenceReady = false;
        private bool _suspendBottomPanelTracking = false;

        private List<ColumnaProgramaObra> _columnasConfig = new();
        private bool _cargandoColumnas = false;
        private ColumnaProgramaObra? _colProgRibbon;
        private ColumnaPersonalizada? _columnaRibbon;

        public DataGridView GridPrincipal => dgvActividades;
        public DataGridView GridBusqueda => dgvActividades;
        public event EventHandler? ColumnaSeleccionadaCambiada;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon!;

        private enum CurvaSViewMode
        {
            Fisica,
            Financiera,
            Ambas
        }

        private sealed class VistaCurvaOption
        {
            public string Text { get; init; } = string.Empty;
            public CurvaSViewMode Value { get; init; }
        }

        private sealed class PieGanttOption
        {
            public string Text { get; init; } = string.Empty;
            public GanttFooterDisplayMode Value { get; init; }
        }

        private sealed class SegmentLabelGanttOption
        {
            public string Text { get; init; } = string.Empty;
            public GanttSegmentLabelPosition Value { get; init; }
        }

        private readonly List<DependencyTypeOption> _tiposDependencia = new()
        {
            new DependencyTypeOption(TipoDependenciaActividad.FS.ToString(), "Fin → Inicio (FS)"),
            new DependencyTypeOption(TipoDependenciaActividad.SS.ToString(), "Inicio → Inicio (SS)"),
            new DependencyTypeOption(TipoDependenciaActividad.FF.ToString(), "Fin → Fin (FF)"),
            new DependencyTypeOption(TipoDependenciaActividad.SF.ToString(), "Inicio → Fin (SF)")
        };

        public FormProgramaObra(SOPROContext context, Proyecto proyecto)
        {
            _context = context;
            _proyecto = proyecto;
            FormatoHelper.EstablecerProyecto(_proyecto);
            InitializeComponent();
            ConfigurarFormulario();
            ConfigurarGrids();
            _columnasConfig = ColumnasProgramaObraHelper.ObtenerColumnas(_context, _proyecto.Id);
            AplicarConfiguracionColumnas();

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
            // Reaplicar formatos de columnas con los nuevos decimales
            AplicarFormatosNumericosPrograma();
            // Recargar el programa para reflejar los nuevos decimales
            if (IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) CargarPrograma(); }));
        }

        private void ConfigurarFormulario()
        {
            FormRenderHelper.OptimizarRender(this);
            ControlRenderHelper.HabilitarDobleBuffer(splitPrincipal);
            ControlRenderHelper.HabilitarDobleBuffer(splitActividadesGantt);
            ControlRenderHelper.HabilitarDobleBuffer(tabPrograma);

            lblTitulo.Text = "PROGRAMA DE OBRA";
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.ProgramaObra).Attach();
            lblSubtitulo.Text = $"Proyecto: {_proyecto.Nombre}";
            lblEstado.Text = "Módulo de programación listo.";
            cmbTipoPeriodo.ComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTipoPeriodo.ComboBox.DataSource = Enum.GetValues(typeof(TipoPeriodoPrograma));
            cmbVistaCurva.ComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbVistaCurva.ComboBox.DisplayMember = nameof(VistaCurvaOption.Text);
            cmbVistaCurva.ComboBox.ValueMember = nameof(VistaCurvaOption.Value);
            cmbVistaCurva.ComboBox.DataSource = new List<VistaCurvaOption>
            {
                new() { Text = "Cantidad", Value = CurvaSViewMode.Fisica },
                new() { Text = "Financiera", Value = CurvaSViewMode.Financiera },
                new() { Text = "Mixta", Value = CurvaSViewMode.Ambas }
            };
            cmbVistaCurva.ComboBox.SelectedValue = CurvaSViewMode.Ambas;
            lblVistaCurva.Visible = true;
            cmbVistaCurva.Visible = true;
            cmbPieGantt.ComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPieGantt.ComboBox.DisplayMember = nameof(PieGanttOption.Text);
            cmbPieGantt.ComboBox.ValueMember = nameof(PieGanttOption.Value);
            cmbPieGantt.ComboBox.DataSource = new List<PieGanttOption>
            {
                new() { Text = "Sin pie", Value = GanttFooterDisplayMode.Ninguno },
                new() { Text = "Importe período", Value = GanttFooterDisplayMode.ImportePeriodo },
                new() { Text = "Importe acumulado", Value = GanttFooterDisplayMode.ImporteAcumulado },
                new() { Text = "% período", Value = GanttFooterDisplayMode.PorcentajePeriodo },
                new() { Text = "% acumulado", Value = GanttFooterDisplayMode.PorcentajeAcumulado }
            };
            cmbPieGantt.ComboBox.SelectedValue = GanttFooterDisplayMode.Ninguno;
            cmbEtiquetaSegmentoGantt.ComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbEtiquetaSegmentoGantt.ComboBox.DisplayMember = nameof(SegmentLabelGanttOption.Text);
            cmbEtiquetaSegmentoGantt.ComboBox.ValueMember = nameof(SegmentLabelGanttOption.Value);
            cmbEtiquetaSegmentoGantt.ComboBox.DataSource = new List<SegmentLabelGanttOption>
            {
                new() { Text = "Arriba", Value = GanttSegmentLabelPosition.Arriba },
                new() { Text = "Abajo", Value = GanttSegmentLabelPosition.Abajo }
            };
            cmbEtiquetaSegmentoGantt.ComboBox.SelectedValue = GanttSegmentLabelPosition.Arriba;
            lblEtiquetaSegmentoGantt.Visible = false;
            cmbEtiquetaSegmentoGantt.Visible = false;
            InicializarTabCurvaS();
            InicializarPanelGantt();
            ActualizarDisponibilidadPieGantt();
            ActualizarTextoBotonDetalle();

            splitActividadesGantt.SplitterMoved += splitActividadesGantt_SplitterMoved;
            splitPrincipal.SplitterMoved += splitPrincipal_SplitterMoved;
            FormClosing += FormProgramaObra_FormClosing;
        }

        private void ConfigurarGrids()
        {
            dgvActividades.AutoGenerateColumns = false;
            dgvActividades.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvPeriodos.AutoGenerateColumns = false;
            dgvDistribucion.AutoGenerateColumns = false;
            dgvDependencias.AutoGenerateColumns = false;

            dgvActividades.AplicarEstiloSOPRO();
            dgvPeriodos.AplicarEstiloSOPRO();
            dgvDistribucion.AplicarEstiloSOPRO();
            dgvDependencias.AplicarEstiloSOPRO();

            DgvCeldaHelper.Aplicar(dgvActividades);
            DgvCeldaHelper.Aplicar(dgvPeriodos);
            DgvCeldaHelper.Aplicar(dgvDistribucion);
            DgvCeldaHelper.Aplicar(dgvDependencias);

            ConfigurarGridDependencias();

            dgvActividades.DataSource = _actividades;
            dgvPeriodos.DataSource = _periodos;
            dgvDependencias.DataSource = _dependenciasEditables;

            dgvActividades.DataError += Dgv_DataError;
            dgvPeriodos.DataError += Dgv_DataError;
            dgvDistribucion.DataError += Dgv_DataError;
            dgvDependencias.DataError += Dgv_DataError;
            AplicarFormatosNumericosPrograma();
            dgvActividades.CellValidating += dgvActividades_CellValidating;
            dgvActividades.CellFormatting += dgvActividades_CellFormatting;
            dgvDependencias.CellEndEdit += dgvDependencias_CellEndEdit;
            dgvDependencias.UserDeletingRow += dgvDependencias_UserDeletingRow;
            dgvDependencias.EditingControlShowing += dgvDependencias_EditingControlShowing;
            dgvDependencias.CellBeginEdit += dgvDependencias_CellBeginEdit;
            dgvActividades.CellBeginEdit += dgvActividades_CellBeginEdit;
            dgvActividades.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvActividades.ColumnWidthChanged += dgvActividades_ColumnWidthChanged;
            dgvActividades.CellDoubleClick += dgvActividades_CellDoubleClick;
            dgvActividades.KeyDown += dgvActividades_KeyDown;
            ConfigurarGridCurvaS();
        }

        private void AplicarFormatosNumericosPrograma()
        {
            string fmtCant = $"N{_proyecto.DecimalesCantidad}";

            colPrecioUnitario.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colPrecioUnitario.DefaultCellStyle.Format = string.Empty; // usa CellFormatting ToStringImporte

            colImporte.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colImporte.DefaultCellStyle.Format = string.Empty; // usa CellFormatting ToStringImporte

            // colCantidad usa CellFormatting con _proyecto.DecimalesCantidad — no requiere Format aquí

            // Columna de cantidad en el grid de distribución
            colDistCantidad.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colDistCantidad.DefaultCellStyle.Format = fmtCant;
        }

        private void ConfigurarGridDependencias()
        {
            dgvDependencias.AllowUserToAddRows = true;
            dgvDependencias.AllowUserToDeleteRows = true;
            dgvDependencias.ReadOnly = false;
            colDepDescripcion.ReadOnly = true;
            colDepLag.ReadOnly = false;
            colDepClave.ReadOnly = false;
            colDepTipo.ReadOnly = false;

            colDepClave.DisplayMember = nameof(ActividadLookupItem.Display);
            colDepClave.ValueMember = nameof(ActividadLookupItem.Id);
            colDepClave.DataSource = _actividadesLookup;
            colDepClave.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;

            colDepTipo.DisplayMember = nameof(DependencyTypeOption.Text);
            colDepTipo.ValueMember = nameof(DependencyTypeOption.Value);
            colDepTipo.DataSource = _tiposDependencia;
            colDepTipo.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
            colDepTipo.HeaderText = "Tipo de vínculo";

            dgvDependencias.Columns[nameof(colDepDescripcion)].ToolTipText = "La actividad seleccionada depende de la predecesora elegida.";
            dgvDependencias.Columns[nameof(colDepTipo)].ToolTipText = "FS: la otra termina y esta inicia. SS: la otra inicia y esta inicia. FF: la otra termina y esta termina. SF: la otra inicia y esta termina.";
        }

        public bool GenerarReporteExcel()
        {
            btnExportar_Click(this, EventArgs.Empty);
            return true;
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colProgRibbon == null) return;
            _colProgRibbon.NombreFuente = fmt.NombreFuente;
            _colProgRibbon.TamanoFuente = fmt.TamanoFuente;
            _colProgRibbon.Negrita = fmt.Negrita;
            _colProgRibbon.Cursiva = fmt.Cursiva;
            _colProgRibbon.Alineacion = fmt.Alineacion;
            _colProgRibbon.ColorFondo = fmt.ColorFondo;
            _colProgRibbon.ColorFuente = fmt.ColorFuente;
            _colProgRibbon.WrapTexto = fmt.WrapTexto;
            _colProgRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colProgRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();
            AplicarConfiguracionColumnas();
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
            AplicarConfiguracionColumnas();
        }

        private void NotificarColumnaSeleccionada(int colIndex)
        {
            _colProgRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvActividades.Columns.Count)
            {
                var col = dgvActividades.Columns[colIndex];
                if (col.Tag is ColumnaProgramaObra cfg)
                {
                    _colProgRibbon = cfg;
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

        private void dgvActividades_ColumnWidthChanged(object? sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column?.Tag is not ColumnaProgramaObra cfg) return;
            if (e.Column.Width <= 20) return;
            var db = _context.ColumnasProgramaObra.Find(cfg.Id);
            if (db == null) return;
            db.AnchoColumna = e.Column.Width;
            db.FechaModificacion = DateTime.Now;
            _context.SaveChanges();
        }

        private void AplicarConfiguracionColumnas()
        {
            _cargandoColumnas = true;
            foreach (DataGridViewColumn col in dgvActividades.Columns)
            {
                var cfg = _columnasConfig.FirstOrDefault(c => c.NombreInterno == col.Name);
                if (cfg == null) continue;
                col.HeaderText = cfg.Nombre;
                col.Visible = cfg.Visible;
                if (cfg.AnchoColumna > 20) col.Width = cfg.AnchoColumna;
                col.DisplayIndex = Math.Min(Math.Max(cfg.Orden - 1, 0), dgvActividades.Columns.Count - 1);
                col.Tag = cfg;
                AplicarEstiloPrograma(col, cfg);
            }
            _cargandoColumnas = false;
        }

        private static void AplicarEstiloPrograma(DataGridViewColumn col, ColumnaProgramaObra cfg)
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

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            using var form = new FormColumnasAPU(_context, _proyecto.Id, FormColumnasAPU.ModoColumnas.ProgramaObra);
            if (form.ShowDialog(this) == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasProgramaObraHelper.ObtenerColumnas(_context, _proyecto.Id);
                AplicarConfiguracionColumnas();
            }
        }

        private void InicializarTabCurvaS()
        {
            _tabCurvaS = new TabPage("Curva S");
            _splitCurvaS = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 110
            };

            _lblCurvaResumen = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(10, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Curva S física y financiera programada"
            };
            _picCurvaS = new BufferedChartPanel
            {
                Dock = DockStyle.Fill
            };
            _picCurvaS.Paint += picCurvaS_Paint;
            _dgvCurvaS = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _splitCurvaS.Panel1.Controls.Add(_picCurvaS);
            _splitCurvaS.Panel1.Controls.Add(_lblCurvaResumen);
            _splitCurvaS.Panel2.Controls.Add(_dgvCurvaS);
            _tabCurvaS.Controls.Add(_splitCurvaS);
            tabPrograma.Controls.Add(_tabCurvaS);
            ControlRenderHelper.HabilitarDobleBuffer(_tabCurvaS);
            ControlRenderHelper.HabilitarDobleBuffer(_splitCurvaS);
            ControlRenderHelper.HabilitarDobleBuffer(_splitCurvaS.Panel1);
            ControlRenderHelper.HabilitarDobleBuffer(_splitCurvaS.Panel2);
        }

        private void InicializarPanelGantt()
        {
            _ganttControl = ganttTimeline;
            _ganttControl.BindGrid(dgvActividades);
            _ganttControl.TimelineCellWidthChanged += ganttTimeline_TimelineCellWidthChanged;
            _ganttControl.VisualSettingsChanged += ganttTimeline_VisualSettingsChanged;
            _ganttControl.RenderModel = new GanttRenderModel
            {
                TipoPeriodo = ObtenerTipoPeriodoSeleccionado(),
                ViewMode = GanttViewMode.ProgramaObra
            };
        }

        private void ganttTimeline_VisualSettingsChanged(object? sender, EventArgs e)
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady)
                return;

            GuardarLayoutPersistido();
        }

        private GanttVisualSettings ObtenerConfiguracionVisualGanttActual()
        {
            return _ganttControl?.VisualSettings ?? GanttVisualSettings.CreateDefault();
        }

        private void AplicarConfiguracionVisualGanttDesdeEstado(FormProgramaObraLayoutState state)
        {
            if (_ganttControl == null)
                return;

            var settings = GanttVisualSettings.CreateDefault();

            if (!string.IsNullOrWhiteSpace(state.GanttFontFamily))
                settings.FontFamilyName = state.GanttFontFamily;
            if (state.GanttFontStyle.HasValue && Enum.IsDefined(typeof(FontStyle), state.GanttFontStyle.Value))
                settings.FontStyle = (FontStyle)state.GanttFontStyle.Value;
            if (state.GanttTextColorArgb.HasValue)
                settings.TextColor = Color.FromArgb(state.GanttTextColorArgb.Value);
            if (state.GanttOutlineColorArgb.HasValue)
                settings.OutlineColor = Color.FromArgb(state.GanttOutlineColorArgb.Value);
            if (state.GanttNormalBarColorArgb.HasValue)
                settings.NormalBarColor = Color.FromArgb(state.GanttNormalBarColorArgb.Value);
            if (state.GanttCriticalBarColorArgb.HasValue)
                settings.CriticalBarColor = Color.FromArgb(state.GanttCriticalBarColorArgb.Value);
            if (state.GanttSummaryBarColorArgb.HasValue)
                settings.SummaryBarColor = Color.FromArgb(state.GanttSummaryBarColorArgb.Value);

            _ganttControl.VisualSettings = settings;
        }

        private bool IntentarBeginInvokeSeguro(Action action)
        {
            if (action == null || IsDisposed)
                return false;

            Control? dispatcher = null;
            if (IsHandleCreated)
                dispatcher = this;
            else if (dgvActividades != null && dgvActividades.IsHandleCreated)
                dispatcher = dgvActividades;
            else if (_ganttControl != null && _ganttControl.IsHandleCreated)
                dispatcher = _ganttControl;
            else if (_picCurvaS != null && _picCurvaS.IsHandleCreated)
                dispatcher = _picCurvaS;

            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(action);
                return true;
            }

            EventHandler? onHandleCreated = null;
            onHandleCreated = (_, __) =>
            {
                HandleCreated -= onHandleCreated;
                if (IsDisposed)
                    return;

                BeginInvoke(action);
            };

            HandleCreated += onHandleCreated;
            return true;
        }

        private void SolicitarRedibujoCurvaS()
        {
            if (_picCurvaS == null)
                return;

            if (!IsHandleCreated || IsDisposed)
            {
                _picCurvaS.Invalidate();
                return;
            }

            if (_curvaSRedrawPending)
                return;

            _curvaSRedrawPending = true;
            if (!IntentarBeginInvokeSeguro(() =>
            {
                _curvaSRedrawPending = false;
                if (IsDisposed || _picCurvaS == null)
                    return;

                _picCurvaS.Invalidate();
            }))
            {
                _curvaSRedrawPending = false;
            }
        }

        private void ActualizarGantt()
        {
            if (_ganttControl == null)
                return;

            var vista = ObtenerVistaCurvaSeleccionada();
            var tipoPeriodo = ObtenerTipoPeriodoSeleccionado();
            var model = vista switch
            {
                CurvaSViewMode.Financiera => _ganttService.BuildFinancialGantt(_context, _programaActual, tipoPeriodo),
                CurvaSViewMode.Ambas => _ganttService.BuildFinancialGantt(_context, _programaActual, tipoPeriodo, mixedMode: true),
                _ => _ganttService.BuildProgramGantt(_programaActual, tipoPeriodo, GanttViewMode.ProgramaObra)
            };

            _ganttControl.FooterDisplayMode = vista == CurvaSViewMode.Financiera ? ObtenerPieGanttSeleccionado() : GanttFooterDisplayMode.Ninguno;
            _ganttControl.RenderModel = model;
            _ganttControl.SelectedActivityId = GetActividadSeleccionada()?.Id;
        }

        private bool DebeDiferirCargaDetalle()
        {
            return splitPrincipal.Panel2Collapsed;
        }

        private bool DebeRecargarDetalle(int? actividadId, bool forzar)
        {
            if (forzar)
                return true;

            if (_detalleSeleccionDeferred)
                return true;

            return _ultimaActividadDetalleId != actividadId;
        }

        private void ProcesarDetallePendienteSiAplica(bool forzar = false)
        {
            if (!forzar && !_detalleSeleccionDeferred)
                return;

            if (DebeDiferirCargaDetalle())
                return;

            SolicitarActualizacionDetalleSeleccion(true);
        }

        private void SolicitarActualizacionDetalleSeleccion(bool forzar = false)
        {
            if (!forzar && (_cargando || !IsHandleCreated || IsDisposed))
                return;

            var actividadId = GetActividadSeleccionada()?.Id;
            if (!DebeRecargarDetalle(actividadId, forzar))
                return;

            if (DebeDiferirCargaDetalle())
            {
                _detalleSeleccionDeferred = true;
                return;
            }

            if (_detalleSeleccionRefreshPending)
                return;

            _detalleSeleccionRefreshPending = true;
            if (!IntentarBeginInvokeSeguro(() =>
            {
                _detalleSeleccionRefreshPending = false;
                if (IsDisposed)
                    return;

                if (_cargando && !forzar)
                    return;

                if (DebeDiferirCargaDetalle())
                {
                    _detalleSeleccionDeferred = true;
                    return;
                }

                CargarDetalleActividadSeleccionada();
            }))
            {
                _detalleSeleccionRefreshPending = false;
            }
        }

        private void SolicitarActualizacionVisualPrograma(bool forzar = false)
        {
            if (!forzar && (_cargando || !IsHandleCreated || IsDisposed))
                return;

            if (_visualProgramRefreshPending)
                return;

            _visualProgramRefreshPending = true;
            if (!IntentarBeginInvokeSeguro(() =>
            {
                _visualProgramRefreshPending = false;
                if (IsDisposed)
                    return;

                if (_cargando && !forzar)
                    return;

                if (_ganttControl != null)
                {
                    var vistaActual = ObtenerVistaCurvaSeleccionada();
                    var expectedViewMode = vistaActual switch
                    {
                        CurvaSViewMode.Financiera => GanttViewMode.Erogaciones,
                        CurvaSViewMode.Ambas => GanttViewMode.Mixto,
                        _ => GanttViewMode.ProgramaObra
                    };
                    var expectedFooterMode = vistaActual == CurvaSViewMode.Financiera ? ObtenerPieGanttSeleccionado() : GanttFooterDisplayMode.Ninguno;
                    var requiereReconstruirGantt = _ganttControl.RenderModel == null
                        || _ganttControl.RenderModel.ViewMode != expectedViewMode
                        || _ganttControl.FooterDisplayMode != expectedFooterMode;

                    if (requiereReconstruirGantt)
                    {
                        ActualizarGantt();
                    }
                    else
                    {
                        var actividad = GetActividadSeleccionada();
                        _ganttControl.SelectedActivityId = actividad?.Id;
                        _ganttControl.RequestRefresh();
                    }
                }

                SolicitarRedibujoCurvaS();
            }))
            {
                _visualProgramRefreshPending = false;
            }
        }

        private void ConfigurarGridCurvaS()
        {
            if (_dgvCurvaS == null)
                return;

            _dgvCurvaS.AplicarEstiloSOPRO();
            DgvCeldaHelper.Aplicar(_dgvCurvaS);
            _dgvCurvaS.Columns.Clear();
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaNumero", DataPropertyName = nameof(CurvaSRowDto.NumeroPeriodo), HeaderText = "#", Width = 45 });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaEtiqueta", DataPropertyName = nameof(CurvaSRowDto.Etiqueta), HeaderText = "Periodo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaInicio", DataPropertyName = nameof(CurvaSRowDto.FechaInicio), HeaderText = "Inicio", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaFin", DataPropertyName = nameof(CurvaSRowDto.FechaFin), HeaderText = "Fin", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaCantPeriodo", DataPropertyName = nameof(CurvaSRowDto.CantidadPeriodo), HeaderText = "Cant. período", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "N4" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaCantAcum", DataPropertyName = nameof(CurvaSRowDto.CantidadAcumulada), HeaderText = "Cant. acumulada", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "N4" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaPctFisPeriodo", DataPropertyName = nameof(CurvaSRowDto.PorcentajeFisicoPeriodo), HeaderText = "% físico período", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaPctFisAcum", DataPropertyName = nameof(CurvaSRowDto.PorcentajeFisicoAcumulado), HeaderText = "% físico acumulado", Width = 115, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaImpPeriodo", DataPropertyName = nameof(CurvaSRowDto.ImportePeriodo), HeaderText = "Importe período", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaImpAcum", DataPropertyName = nameof(CurvaSRowDto.ImporteAcumulado), HeaderText = "Importe acumulado", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaPctFinPeriodo", DataPropertyName = nameof(CurvaSRowDto.PorcentajePeriodo), HeaderText = "% financiero período", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaPctFinAcum", DataPropertyName = nameof(CurvaSRowDto.PorcentajeAcumulado), HeaderText = "% financiero acumulado", Width = 130, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.DataSource = _curvaS;
            AplicarVistaCurvaS();
        }

        private void AplicarVistaCurvaS()
        {
            if (_dgvCurvaS == null)
                return;
            var vista = ObtenerVistaCurvaSeleccionada();
            bool fis = vista != CurvaSViewMode.Financiera;
            bool fin = vista != CurvaSViewMode.Fisica;
            void SetVisible(string name, bool value)
            {
                if (_dgvCurvaS.Columns.Contains(name))
                    _dgvCurvaS.Columns[name].Visible = value;
            }
            SetVisible("colCurvaCantPeriodo", fis);
            SetVisible("colCurvaCantAcum", fis);
            SetVisible("colCurvaPctFisPeriodo", fis);
            SetVisible("colCurvaPctFisAcum", fis);
            SetVisible("colCurvaImpPeriodo", fin);
            SetVisible("colCurvaImpAcum", fin);
            SetVisible("colCurvaPctFinPeriodo", fin);
            SetVisible("colCurvaPctFinAcum", fin);
            SolicitarRedibujoCurvaS();
        }

        private void LimpiarCurvaS()
        {
            _curvaS = new BindingList<CurvaSRowDto>();
            if (_dgvCurvaS != null)
                _dgvCurvaS.DataSource = _curvaS;
            if (_lblCurvaResumen != null)
                _lblCurvaResumen.Text = "Curva S física y financiera programada";
            SolicitarRedibujoCurvaS();
        }

        private void CargarCurvaS()
        {
            if (_programaActual == null)
            {
                LimpiarCurvaS();
                return;
            }

            var rows = _curvaSService.BuildFinancialCurve(_context, _programaActual.ProgramaObraId);
            _curvaS = new BindingList<CurvaSRowDto>(rows);
            if (_dgvCurvaS != null)
                _dgvCurvaS.DataSource = _curvaS;

            if (_lblCurvaResumen != null)
            {
                var total = rows.LastOrDefault()?.ImporteAcumulado ?? 0m;
                var periodos = rows.Count;
                var totalFisico = rows.LastOrDefault()?.CantidadAcumulada ?? 0m;
                _lblCurvaResumen.Text = $"Curva S física y financiera programada. Periodos: {periodos}. Total físico: {totalFisico:N4}. Total financiero: {total:N2}";
            }

            AplicarVistaCurvaS();
            SolicitarRedibujoCurvaS();
        }

        private void picCurvaS_Paint(object? sender, PaintEventArgs e)
        {
            if (_picCurvaS == null)
                return;

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);
            var rows = _curvaS?.ToList() ?? new List<CurvaSRowDto>();
            var rect = new Rectangle(50, 15, Math.Max(100, _picCurvaS.Width - 75), Math.Max(80, _picCurvaS.Height - 45));
            using var axisPen = new Pen(Color.Silver, 1);
            using var linePenFin = new Pen(Color.FromArgb(41, 128, 185), 2);
            using var fillBrushFin = new SolidBrush(Color.FromArgb(41, 128, 185));
            using var linePenFis = new Pen(Color.FromArgb(39, 174, 96), 2);
            using var fillBrushFis = new SolidBrush(Color.FromArgb(39, 174, 96));
            using var textBrush = new SolidBrush(Color.DimGray);
            using var font = new Font("Segoe UI", 8f);

            g.DrawRectangle(axisPen, rect);
            for (int i = 0; i <= 4; i++)
            {
                var y = rect.Bottom - (rect.Height * i / 4f);
                g.DrawLine(axisPen, rect.Left, y, rect.Right, y);
                var pct = i * 25;
                g.DrawString($"{pct}%", font, textBrush, 8, y - 8);
            }

            if (rows.Count == 0)
            {
                g.DrawString("Sin datos para Curva S.", font, textBrush, rect.Left + 10, rect.Top + 10);
                return;
            }

            var pointsFin = new List<PointF>();
            var pointsFis = new List<PointF>();
            for (int i = 0; i < rows.Count; i++)
            {
                var x = rows.Count == 1 ? rect.Left : rect.Left + (rect.Width * i / (float)(rows.Count - 1));
                var yFin = rect.Bottom - (rect.Height * ((float)rows[i].PorcentajeAcumulado / 100f));
                var yFis = rect.Bottom - (rect.Height * ((float)rows[i].PorcentajeFisicoAcumulado / 100f));
                pointsFin.Add(new PointF(x, yFin));
                pointsFis.Add(new PointF(x, yFis));
                g.DrawString(rows[i].NumeroPeriodo.ToString(), font, textBrush, x - 6, rect.Bottom + 4);
            }

            var vista = ObtenerVistaCurvaSeleccionada();
            bool showFis = vista != CurvaSViewMode.Financiera;
            bool showFin = vista != CurvaSViewMode.Fisica;

            if (showFin && pointsFin.Count > 1)
                g.DrawLines(linePenFin, pointsFin.ToArray());
            if (showFis && pointsFis.Count > 1)
                g.DrawLines(linePenFis, pointsFis.ToArray());

            if (showFin)
                foreach (var p in pointsFin)
                    g.FillEllipse(fillBrushFin, p.X - 3, p.Y - 3, 6, 6);
            if (showFis)
                foreach (var p in pointsFis)
                    g.FillEllipse(fillBrushFis, p.X - 3, p.Y - 3, 6, 6);

            // Leyenda
            var legendY = rect.Top + 8;
            if (showFis)
            {
                g.DrawLine(linePenFis, rect.Right - 190, legendY + 7, rect.Right - 165, legendY + 7);
                g.FillEllipse(fillBrushFis, rect.Right - 180, legendY + 4, 6, 6);
                g.DrawString("Física acumulada", font, textBrush, rect.Right - 160, legendY);
            }
            if (showFin)
            {
                var off = showFis ? 17 : 0;
                g.DrawLine(linePenFin, rect.Right - 190, legendY + 7 + off, rect.Right - 165, legendY + 7 + off);
                g.FillEllipse(fillBrushFin, rect.Right - 180, legendY + 4 + off, 6, 6);
                g.DrawString("Financiera acumulada", font, textBrush, rect.Right - 160, legendY + off);
            }
        }

        private CurvaSViewMode ObtenerVistaCurvaSeleccionada()
        {
            if (cmbVistaCurva.ComboBox.SelectedValue is CurvaSViewMode vistaPorValor)
                return vistaPorValor;

            if (cmbVistaCurva.ComboBox.SelectedItem is VistaCurvaOption opcion)
                return opcion.Value;

            return CurvaSViewMode.Ambas;
        }

        private GanttFooterDisplayMode ObtenerPieGanttSeleccionado()
        {
            if (cmbPieGantt.ComboBox.SelectedValue is GanttFooterDisplayMode modoPorValor)
                return modoPorValor;

            if (cmbPieGantt.ComboBox.SelectedItem is PieGanttOption opcion)
                return opcion.Value;

            return GanttFooterDisplayMode.Ninguno;
        }

        private GanttSegmentLabelPosition ObtenerPosicionEtiquetaSegmentoSeleccionada()
        {
            if (cmbEtiquetaSegmentoGantt.ComboBox.SelectedValue is GanttSegmentLabelPosition modoPorValor)
                return modoPorValor;

            if (cmbEtiquetaSegmentoGantt.ComboBox.SelectedItem is SegmentLabelGanttOption opcion)
                return opcion.Value;

            return GanttSegmentLabelPosition.Arriba;
        }

        private void ActualizarDisponibilidadPieGantt()
        {
            var vista = ObtenerVistaCurvaSeleccionada();
            var financiera = vista == CurvaSViewMode.Financiera;
            lblPieGantt.Visible = financiera;
            cmbPieGantt.Visible = financiera;
            cmbPieGantt.Enabled = financiera;
            lblEtiquetaSegmentoGantt.Visible = false;
            cmbEtiquetaSegmentoGantt.Visible = false;
            cmbEtiquetaSegmentoGantt.Enabled = false;
            if (_ganttControl != null)
            {
                _ganttControl.FooterDisplayMode = financiera ? ObtenerPieGanttSeleccionado() : GanttFooterDisplayMode.Ninguno;
                _ganttControl.SegmentLabelPosition = ObtenerPosicionEtiquetaSegmentoSeleccionada();
            }
        }

        private void FormProgramaObra_Load(object sender, EventArgs e)
        {
            _layoutPersistenceReady = false;
            var previousOpacity = Opacity;
            var hideWithOpacity = previousOpacity > 0d;

            if (hideWithOpacity)
                Opacity = 0d;

            SuspendLayout();
            splitPrincipal.SuspendLayout();
            splitActividadesGantt.SuspendLayout();
            try
            {
                AplicarLayoutPersistido();
                CargarPrograma();
            }
            finally
            {
                splitActividadesGantt.ResumeLayout(true);
                splitPrincipal.ResumeLayout(true);
                ResumeLayout(true);
            }

            BeginInvoke(new Action(() =>
            {
                try
                {
                    _aplicandoLayoutPersistido = true;
                    AplicarLayoutPersistido();
                }
                finally
                {
                    _aplicandoLayoutPersistido = false;
                    _layoutPersistenceReady = true;
                    if (hideWithOpacity && !IsDisposed)
                        Opacity = previousOpacity;
                }
            }));
        }



        private void AplicarLayoutPersistido()
        {
            if (splitActividadesGantt.Width <= 0)
                return;

            try
            {
                _aplicandoLayoutPersistido = true;
                var state = FormProgramaObraLayoutStateStore.Load(_proyecto.Id);
                if (state != null)
                {
                    var oldCargando = _cargando;
                    _cargando = true;
                    try
                    {
                        if (state.VistaCurvaMode.HasValue && Enum.IsDefined(typeof(CurvaSViewMode), state.VistaCurvaMode.Value))
                            cmbVistaCurva.ComboBox.SelectedValue = (CurvaSViewMode)state.VistaCurvaMode.Value;

                        if (state.PieGanttMode.HasValue && Enum.IsDefined(typeof(GanttFooterDisplayMode), state.PieGanttMode.Value))
                            cmbPieGantt.ComboBox.SelectedValue = (GanttFooterDisplayMode)state.PieGanttMode.Value;

                        if (state.SegmentLabelPosition.HasValue && Enum.IsDefined(typeof(GanttSegmentLabelPosition), state.SegmentLabelPosition.Value))
                            cmbEtiquetaSegmentoGantt.ComboBox.SelectedValue = (GanttSegmentLabelPosition)state.SegmentLabelPosition.Value;

                        if (state.GanttCellWidth.HasValue && _ganttControl != null)
                            _ganttControl.TimelineCellWidth = state.GanttCellWidth.Value;

                        AplicarConfiguracionVisualGanttDesdeEstado(state);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        _cargando = oldCargando;
                    }

                    ActualizarDisponibilidadPieGantt();
                    _ultimaAlturaPanelInferior = state.BottomPanelExpandedHeight > 0 ? state.BottomPanelExpandedHeight : Math.Max(_ultimaAlturaPanelInferior, state.BottomPanelHeight);

                    if (state.GanttPanelWidth > 0)
                    {
                        var splitterWidth = Math.Max(splitActividadesGantt.SplitterWidth, 6);
                        var minLeft = Math.Max(splitActividadesGantt.Panel1MinSize, 320);
                        var minRight = Math.Max(splitActividadesGantt.Panel2MinSize, 260);
                        var maxRight = Math.Max(minRight, splitActividadesGantt.Width - splitterWidth - minLeft);
                        var clampedRight = Math.Max(minRight, Math.Min(state.GanttPanelWidth, maxRight));
                        var desiredDistance = splitActividadesGantt.Width - splitterWidth - clampedRight;
                        desiredDistance = Math.Max(minLeft, Math.Min(desiredDistance, splitActividadesGantt.Width - splitterWidth - minRight));

                        if (desiredDistance > 0 && desiredDistance < splitActividadesGantt.Width)
                            splitActividadesGantt.SplitterDistance = desiredDistance;
                    }

                    if (state.BottomPanelCollapsed)
                    {
                        splitPrincipal.Panel2Collapsed = true;
                    }
                    else if (state.BottomPanelHeight > 0 && splitPrincipal.Height > 0)
                    {
                        splitPrincipal.SuspendLayout();
                        try
                        {
                            RestaurarAlturaPanelInferior(state.BottomPanelHeight);
                            if (splitPrincipal.Panel2Collapsed)
                                splitPrincipal.Panel2Collapsed = false;
                        }
                        finally
                        {
                            splitPrincipal.ResumeLayout(true);
                        }
                    }

                    ActualizarTextoBotonDetalle();
                }
            }
            catch
            {
            }
            finally
            {
                _aplicandoLayoutPersistido = false;
            }
        }

        private void GuardarLayoutPersistido()
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady || splitActividadesGantt.Width <= 0)
                return;

            try
            {
                var ganttWidth = splitActividadesGantt.Panel2.Width;
                if (ganttWidth <= 0)
                    return;

                if (!splitPrincipal.Panel2Collapsed && splitPrincipal.Panel2.Height > 0)
                    _ultimaAlturaPanelInferior = splitPrincipal.Panel2.Height;

                var bottomHeight = splitPrincipal.Panel2Collapsed ? 0 : splitPrincipal.Panel2.Height;

                var visualSettings = ObtenerConfiguracionVisualGanttActual();

                FormProgramaObraLayoutStateStore.Save(_proyecto.Id, new FormProgramaObraLayoutState
                {
                    GanttPanelWidth = ganttWidth,
                    BottomPanelHeight = bottomHeight,
                    BottomPanelExpandedHeight = _ultimaAlturaPanelInferior,
                    BottomPanelCollapsed = splitPrincipal.Panel2Collapsed,
                    VistaCurvaMode = (int)ObtenerVistaCurvaSeleccionada(),
                    PieGanttMode = (int)ObtenerPieGanttSeleccionado(),
                    SegmentLabelPosition = (int)ObtenerPosicionEtiquetaSegmentoSeleccionada(),
                    GanttCellWidth = _ganttControl?.TimelineCellWidth,
                    GanttFontFamily = visualSettings.FontFamilyName,
                    GanttFontStyle = (int)visualSettings.FontStyle,
                    GanttTextColorArgb = visualSettings.TextColor.ToArgb(),
                    GanttOutlineColorArgb = visualSettings.OutlineColor.ToArgb(),
                    GanttNormalBarColorArgb = visualSettings.NormalBarColor.ToArgb(),
                    GanttCriticalBarColorArgb = visualSettings.CriticalBarColor.ToArgb(),
                    GanttSummaryBarColorArgb = visualSettings.SummaryBarColor.ToArgb()
                });
            }
            catch
            {
            }
        }

        private void ganttTimeline_TimelineCellWidthChanged(object? sender, EventArgs e)
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady)
                return;

            GuardarLayoutPersistido();
        }

        private void splitActividadesGantt_SplitterMoved(object? sender, SplitterEventArgs e)
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady)
                return;

            GuardarLayoutPersistido();
        }

        private void splitPrincipal_SplitterMoved(object? sender, SplitterEventArgs e)
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady || _suspendBottomPanelTracking)
                return;

            if (!splitPrincipal.Panel2Collapsed && splitPrincipal.Panel2.Height > 0)
                _ultimaAlturaPanelInferior = splitPrincipal.Panel2.Height;

            GuardarLayoutPersistido();
        }

        private void FormProgramaObra_FormClosing(object? sender, FormClosingEventArgs e)
        {
            GuardarLayoutPersistido();
        }

        private void RestaurarAlturaPanelInferior(int desiredBottomHeight)
        {
            if (splitPrincipal.Height <= 0)
                return;

            var splitterWidthBottom = Math.Max(splitPrincipal.SplitterWidth, 6);
            var minTop = 180;
            var minBottom = 150;
            var maxBottom = Math.Max(minBottom, splitPrincipal.Height - splitterWidthBottom - minTop);
            var clampedBottom = Math.Max(minBottom, Math.Min(desiredBottomHeight, maxBottom));
            var desiredTop = splitPrincipal.Height - splitterWidthBottom - clampedBottom;
            desiredTop = Math.Max(minTop, Math.Min(desiredTop, splitPrincipal.Height - splitterWidthBottom - minBottom));

            if (desiredTop > 0 && desiredTop < splitPrincipal.Height)
                splitPrincipal.SplitterDistance = desiredTop;
        }

        private void ActualizarTextoBotonDetalle()
        {
            btnToggleDetalle.Text = splitPrincipal.Panel2Collapsed ? "▸ Detalle" : "▾ Detalle";
            btnToggleDetalle.ToolTipText = splitPrincipal.Panel2Collapsed
                ? "Mostrar panel inferior con periodos, distribución, dependencias y Curva S."
                : "Ocultar panel inferior para dar más espacio al programa.";
        }

        private void AsegurarPanelDetalleVisible(bool enfocarTabs = false)
        {
            var estabaColapsado = splitPrincipal.Panel2Collapsed;
            if (estabaColapsado)
            {
                var desiredExpandedHeight = _ultimaAlturaPanelInferior > 0 ? _ultimaAlturaPanelInferior : 243;
                _suspendBottomPanelTracking = true;
                splitPrincipal.SuspendLayout();
                try
                {
                    RestaurarAlturaPanelInferior(desiredExpandedHeight);
                    splitPrincipal.Panel2Collapsed = false;
                }
                finally
                {
                    splitPrincipal.ResumeLayout(true);
                    _suspendBottomPanelTracking = false;
                }

                if (!splitPrincipal.Panel2Collapsed && splitPrincipal.Panel2.Height > 0)
                    _ultimaAlturaPanelInferior = splitPrincipal.Panel2.Height;

                ActualizarTextoBotonDetalle();
                GuardarLayoutPersistido();
            }

            if (enfocarTabs)
                tabPrograma.Focus();

            if (estabaColapsado)
                ProcesarDetallePendienteSiAplica(true);
        }

        private void AlternarPanelDetalle()
        {
            if (splitPrincipal.Panel2Collapsed)
            {
                AsegurarPanelDetalleVisible(true);
                return;
            }

            if (splitPrincipal.Panel2.Height > 0)
                _ultimaAlturaPanelInferior = splitPrincipal.Panel2.Height;

            _suspendBottomPanelTracking = true;
            try
            {
                splitPrincipal.Panel2Collapsed = true;
            }
            finally
            {
                _suspendBottomPanelTracking = false;
            }

            ActualizarTextoBotonDetalle();
            GuardarLayoutPersistido();
        }

        private bool EsAtajoDependenciasValido(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || columnIndex < 0)
                return false;

            var columnName = dgvActividades.Columns[columnIndex].Name;
            if (!string.Equals(columnName, "colPredecesora", StringComparison.Ordinal))
                return false;

            return dgvActividades.Rows[rowIndex].DataBoundItem is ActivityGridRowDto dto && !dto.EsResumen;
        }

        private void AbrirDependenciasDesdePrograma()
        {
            AsegurarPanelDetalleVisible();
            tabPrograma.SelectedTab = tabDependencias;
            CargarDetalleActividadSeleccionada();
            dgvDependencias.Focus();
        }

        private void btnGenerarPrograma_Click(object sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                var fechaInicio = _proyecto.FechaInicio == default ? DateTime.Today : _proyecto.FechaInicio.Date;
                var result = _generationService.GenerateFromBudget(_context, _proyecto.Id, fechaInicio, ObtenerTipoPeriodoSeleccionado());
                lblEstado.Text = result.Message;
                CargarPrograma();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnRecargar_Click(object sender, EventArgs e)
        {
            CargarPrograma(DataGridViewStateHelper.Capture(dgvActividades));
        }

        private async void btnRecalcular_Click(object sender, EventArgs e)
        {
            if (_programaActual == null)
            {
                try
                {
                    SetOcupado(true, "Sincronizando programa...");
                    var resultSinPrograma = await Task.Run(() => _syncService.SyncFromBudget(_context, _proyecto.Id));
                    lblEstado.Text = resultSinPrograma.Message;
                    CargarPrograma();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al sincronizar el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetOcupado(false);
                }
                return;
            }

            try
            {
                var state = DataGridViewStateHelper.Capture(dgvActividades);
                SetOcupado(true, "Recalculando programa...");
                var programaId = _programaActual.ProgramaObraId;
                var tipoPeriodo = ObtenerTipoPeriodoSeleccionado();
                await Task.Run(() =>
                {
                    _calculationService.RecalculateProgram(_context, programaId);
                    RegenerarPeriodosYDistribuciones(programaId, tipoPeriodo);
                });
                CargarPrograma(state);
                lblEstado.Text = "Programa recalculado correctamente.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al recalcular el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetOcupado(false);
            }
        }

        private async void btnCalendario_Click(object sender, EventArgs e)
        {
            if (_programaActual == null)
            {
                MessageBox.Show("Primero genera o carga un programa de obra.", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var frm = new FormCalendarioLaboral(_context, _proyecto, _programaActual.CalendarioLaboralId);
            if (frm.ShowDialog(this) == DialogResult.OK && frm.CalendarioActualizado)
            {
                if (!_programaActual.CalendarioLaboralId.HasValue)
                {
                    var programa = _context.ProgramasObra.FirstOrDefault(p => p.Id == _programaActual.ProgramaObraId);
                    var calendario = _context.CalendariosLaborales
                        .Where(c => c.ProyectoId == _proyecto.Id && c.Activo)
                        .OrderByDescending(c => c.Id)
                        .FirstOrDefault();
                    if (programa != null && calendario != null)
                    {
                        programa.CalendarioLaboralId = calendario.Id;
                        _context.SaveChanges();
                    }
                }

                var state = DataGridViewStateHelper.Capture(dgvActividades);
                var programaId2 = _programaActual.ProgramaObraId;
                var tipoPeriodo2 = ObtenerTipoPeriodoSeleccionado();
                SetOcupado(true, "Actualizando calendario...");
                try
                {
                    await Task.Run(() =>
                    {
                        _calculationService.RecalculateProgram(_context, programaId2);
                        RegenerarPeriodosYDistribuciones(programaId2, tipoPeriodo2);
                    });
                    CargarPrograma(state);
                    lblEstado.Text = "Calendario laboral actualizado.";
                }
                catch (Exception ex2)
                {
                    MessageBox.Show($"Error al actualizar el calendario: {ex2.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetOcupado(false);
                }
            }
        }

        private TipoPeriodoPrograma ObtenerTipoPeriodoSeleccionado()
        {
            return cmbTipoPeriodo.SelectedItem is TipoPeriodoPrograma tipo ? tipo : TipoPeriodoPrograma.Semana;
        }

        private void cmbVistaCurva_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando)
                return;

            AplicarVistaCurvaS();
            ActualizarDisponibilidadPieGantt();
            ActualizarGantt();
            SolicitarRedibujoCurvaS();
            GuardarLayoutPersistido();
        }

        private void cmbPieGantt_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando)
                return;

            ActualizarDisponibilidadPieGantt();
            SolicitarActualizacionVisualPrograma(true);
            GuardarLayoutPersistido();
        }

        private void cmbEtiquetaSegmentoGantt_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando)
                return;

            ActualizarDisponibilidadPieGantt();
            SolicitarActualizacionVisualPrograma(true);
            GuardarLayoutPersistido();
        }

        private async void cmbTipoPeriodo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando || _programaActual == null)
                return;

            try
            {
                var nuevoTipo = ObtenerTipoPeriodoSeleccionado();
                if (_programaActual.TipoPeriodo == nuevoTipo)
                    return;

                var state = DataGridViewStateHelper.Capture(dgvActividades);
                var programa = _context.ProgramasObra.FirstOrDefault(p => p.Id == _programaActual.ProgramaObraId);
                if (programa == null)
                    return;

                programa.TipoPeriodo = nuevoTipo;
                programa.DuracionPeriodoDias = nuevoTipo switch
                {
                    TipoPeriodoPrograma.Dia => 1,
                    TipoPeriodoPrograma.Semana => 7,
                    TipoPeriodoPrograma.Quincena => 15,
                    TipoPeriodoPrograma.Mes => 30,
                    _ => 7
                };
                programa.FechaModificacion = DateTime.Now;
                _context.SaveChanges();

                var programaId3 = programa.Id;
                SetOcupado(true, $"Regenerando periodos en modo {nuevoTipo}...");
                await Task.Run(() =>
                {
                    _calculationService.RecalculateProgram(_context, programaId3);
                    RegenerarPeriodosYDistribuciones(programaId3, nuevoTipo);
                });
                CargarPrograma(state);
                lblEstado.Text = $"Periodos regenerados en modo {nuevoTipo}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cambiar el tipo de periodo: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetOcupado(false);
            }
        }

        // ── Control de estado ocupado — deshabilita disparadores mientras corre async ──
        private void SetOcupado(bool ocupado, string mensaje = "")
        {
            UseWaitCursor  = ocupado;
            // Deshabilitar controles que disparan operaciones pesadas
            btnRecalcular.Enabled   = !ocupado;
            cmbTipoPeriodo.Enabled      = !ocupado;
            btnCalendario.Enabled       = !ocupado;
            btnSincronizar.Enabled      = !ocupado;
            btnReconstruir.Enabled      = !ocupado;
            if (!string.IsNullOrEmpty(mensaje))
                lblEstado.Text = mensaje;
        }

        private void RegenerarPeriodosYDistribuciones(int programaObraId, TipoPeriodoPrograma tipoPeriodo)
        {
            _generationService.RegeneratePeriodsFromProgramRange(_context, programaObraId, tipoPeriodo);

            // Batch: distribuye todas las actividades hoja en una sola transacción
            // en lugar de N SaveChanges (uno por actividad).
            _distributionService.DistributeUniformBatch(_context, programaObraId);

            _calculationService.RecalculateProgram(_context, programaObraId);
        }

        private void btnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                if (_programaActual == null || _ganttControl == null || _ganttControl.RenderModel == null)
                {
                    MessageBox.Show("No hay un programa cargado para exportar.", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var ganttModel = _ganttControl.RenderModel;
                if (ganttModel.Filas.Count == 0 || ganttModel.Escala.Count == 0)
                {
                    MessageBox.Show("El programa no tiene datos suficientes para generar el reporte Excel.", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                FormatoHelper.EstablecerProyecto(_proyecto);

                var tituloReporte = ObtenerTituloReportePrograma();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaObra, lblTitulo.Text);
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Excel del programa",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"{tituloReporte.Replace(' ', '_')}_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                Cursor = Cursors.WaitCursor;
                var generador = new Services.GeneradorExcelProgramaObra(svc);
                var ruta = generador.Generar(
                    _proyecto,
                    plantilla,
                    dgvActividades,
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
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte del programa:\n" + ex.Message, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        public void GenerarPdfProgramaObra()
        {
            try
            {
                if (_ganttControl?.RenderModel == null)
                    throw new InvalidOperationException("No hay vista gantt disponible para exportar.");

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloReporte = ObtenerTituloReportePrograma();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaObra, lblTitulo.Text);

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF del programa",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"{tituloReporte.Replace(' ', '_')}_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };

                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                Cursor = Cursors.WaitCursor;
                var generador = new Services.GeneradorPdfProgramaObra(svc);
                var ruta = generador.Generar(
                    _proyecto,
                    plantilla,
                    dgvActividades,
                    _ganttControl.RenderModel,
                    _ganttControl.VisualSettings,
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
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el PDF del programa:\n" + ex.Message, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private string ObtenerTituloReportePrograma()
        {
            var baseTitle = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaObra, lblTitulo.Text).TextoTitulo;
            return ObtenerVistaCurvaSeleccionada() switch
            {
                CurvaSViewMode.Financiera => $"{baseTitle} - Erogaciones",
                CurvaSViewMode.Ambas => $"{baseTitle} - Mixto",
                _ => $"{baseTitle} - Cantidades"
            };
        }

        private void btnSincronizar_Click(object sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                var state = DataGridViewStateHelper.Capture(dgvActividades);
                var result = _syncService.SyncFromBudget(_context, _proyecto.Id);
                lblEstado.Text = result.Message;
                CargarPrograma(state);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al sincronizar el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnReconstruir_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Se reconstruirá el programa desde el presupuesto. Esto eliminará dependencias, distribuciones y ajustes manuales del programa actual.\n\n¿Deseas continuar?",
                "Reconstruir programa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                UseWaitCursor = true;
                var result = _syncService.RebuildProgram(_context, _proyecto.Id);
                lblEstado.Text = result.Message;
                CargarPrograma();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al reconstruir el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnBorrarPrograma_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Se eliminará por completo el programa de obra del proyecto, incluyendo actividades, dependencias, distribuciones y calendarios.\n\n¿Deseas continuar?",
                "Borrar programa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                UseWaitCursor = true;
                var result = _syncService.DeleteProgram(_context, _proyecto.Id);
                lblEstado.Text = result.Message;
                CargarPrograma();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al borrar el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnToggleDetalle_Click(object sender, EventArgs e)
        {
            AlternarPanelDetalle();
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void CargarPrograma(DataGridViewStateSnapshot? stateToRestore = null)
        {
            dgvActividades.SuspendLayout();
            dgvPeriodos.SuspendLayout();
            dgvDistribucion.SuspendLayout();
            dgvDependencias.SuspendLayout();

            try
            {
                _cargando = true;
                _programaActual = _loadService.LoadProgram(_context, _proyecto.Id);
                _actividades = new BindingList<ActivityGridRowDto>();
                _periodos = new BindingList<PeriodEditDto>();

                if (_programaActual == null)
                {
                    dgvActividades.DataSource = _actividades;
                    dgvPeriodos.DataSource = _periodos;
                    dgvDistribucion.DataSource = null;
                    dgvDependencias.DataSource = null;
                    LimpiarCurvaS();
                    ActualizarGantt();
                    lblEstado.Text = "No existe un programa de obra generado para este proyecto.";
                    return;
                }

                cmbTipoPeriodo.SelectedItem = _programaActual.TipoPeriodo;
                _actividades = new BindingList<ActivityGridRowDto>(_programaActual.Actividades.OrderBy(a => a.Orden).ToList());
                _periodos = new BindingList<PeriodEditDto>(_programaActual.Periodos.OrderBy(p => p.NumeroPeriodo).ToList());

                RecargarLookupActividades();
                dgvActividades.DataSource = _actividades;
                dgvPeriodos.DataSource = _periodos;

                FormatearGridActividades();
                FormatearGridPeriodos();
                AplicarConfiguracionColumnas();

                ActualizarEstadoGeneral();
                CargarCurvaS();
                ActualizarGantt();

                _detalleSeleccionDeferred = false;
                _ultimaActividadDetalleId = null;

                if (dgvActividades.Rows.Count > 0)
                {
                    var restoreState = stateToRestore ?? _pendingGridStateRestore;
                    if (restoreState != null)
                    {
                        DataGridViewStateHelper.Restore(dgvActividades, restoreState);
                    }

                    if (dgvActividades.CurrentCell == null)
                        DataGridViewStateHelper.EnsureSafeCurrentCell(dgvActividades);

                    SolicitarActualizacionDetalleSeleccion(true);
                    SolicitarActualizacionVisualPrograma(true);
                }
                else
                {
                    dgvActividades.ClearSelection();
                    dgvDistribucion.DataSource = null;
                    dgvDependencias.DataSource = null;
                    _detalleSeleccionDeferred = false;
                    _ultimaActividadDetalleId = null;
                    if (_ganttControl != null)
                        _ganttControl.SelectedActivityId = null;
                    SolicitarRedibujoCurvaS();
                }

                _pendingGridStateRestore = null;
            }
            finally
            {
                _cargando = false;
                dgvDependencias.ResumeLayout();
                dgvDistribucion.ResumeLayout();
                dgvPeriodos.ResumeLayout();
                dgvActividades.ResumeLayout();
            }
        }

        private void RecargarLookupActividades()
        {
            _actividadesLookup.Clear();
            foreach (var act in _actividades.Where(a => !a.EsResumen).OrderBy(a => a.Clave))
            {
                _actividadesLookup.Add(new ActividadLookupItem
                {
                    Id = act.Id,
                    Display = string.IsNullOrWhiteSpace(act.Clave) ? act.Descripcion : $"{act.Clave} - {act.Descripcion}"
                });
            }
        }

        private void FormatearGridActividades()
        {
            foreach (DataGridViewRow row in dgvActividades.Rows)
            {
                if (row.DataBoundItem is ActivityGridRowDto dto)
                {
                    row.DefaultCellStyle.BackColor = dto.EsResumen ? Color.FromArgb(240, 240, 240) : Color.White;

                    if (dgvActividades.Columns.Contains("colDescripcion"))
                    {
                        var descCell = row.Cells["colDescripcion"];
                        descCell.Style.Padding = new Padding(Math.Max(0, dto.Nivel) * 18, 0, 0, 0);
                    }
                }
            }
        }

        private void FormatearGridPeriodos()
        {
            foreach (DataGridViewColumn col in dgvPeriodos.Columns)
            {
                col.ReadOnly = true;
            }
        }

        private void dgvActividades_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargando)
                return;

            SolicitarActualizacionDetalleSeleccion();
            SolicitarActualizacionVisualPrograma();
        }

        private void dgvActividades_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (dgvActividades.Rows[e.RowIndex].DataBoundItem is ActivityGridRowDto dto && dto.EsResumen)
            {
                e.Cancel = true;
                lblEstado.Text = $"El agrupador '{dto.Clave}' se calcula automáticamente a partir de sus hijos.";
            }
        }

        private void ActualizarEstadoGeneral()
        {
            if (_programaActual == null)
            {
                lblEstado.Text = "No existe un programa de obra generado para este proyecto.";
                return;
            }

            var conceptos = _actividades.Count(a => !a.EsResumen);
            var agrupadores = _actividades.Count(a => a.EsResumen);
            var inicio = _actividades.Where(a => !a.EsResumen && a.FechaInicioProgramada.HasValue)
                .Select(a => a.FechaInicioProgramada!.Value.Date)
                .DefaultIfEmpty()
                .Min();
            var fin = _actividades.Where(a => !a.EsResumen && a.FechaFinProgramada.HasValue)
                .Select(a => a.FechaFinProgramada!.Value.Date)
                .DefaultIfEmpty()
                .Max();

            if (inicio == default || fin == default)
            {
                lblEstado.Text = $"Programa cargado: {agrupadores} agrupadores, {conceptos} conceptos y {_periodos.Count} periodos.";
                return;
            }

            var dias = _calculationService.CalculateBusinessDaysInclusive(_context, _programaActual.ProgramaObraId, inicio, fin);
            lblEstado.Text = $"Programa cargado: {agrupadores} agrupadores, {conceptos} conceptos, {_periodos.Count} periodos. Plazo total: {dias} días hábiles ({inicio:dd/MM/yyyy} → {fin:dd/MM/yyyy}).";
        }

        private void CargarDetalleActividadSeleccionada()
        {
            if (DebeDiferirCargaDetalle())
            {
                _detalleSeleccionDeferred = true;
                return;
            }

            var actividad = GetActividadSeleccionada();
            if (actividad == null)
            {
                _ultimaActividadDetalleId = null;
                _detalleSeleccionDeferred = false;
                dgvDistribucion.DataSource = null;
                _dependenciasEditables = new BindingList<DependenciaEditableRow>();
                dgvDependencias.DataSource = _dependenciasEditables;
                return;
            }

            if (actividad.EsResumen)
            {
                _ultimaActividadDetalleId = actividad.Id;
                _detalleSeleccionDeferred = false;
                dgvDistribucion.DataSource = null;
                _dependenciasEditables = new BindingList<DependenciaEditableRow>();
                dgvDependencias.DataSource = _dependenciasEditables;
                dgvDependencias.AllowUserToAddRows = false;
                dgvDependencias.AllowUserToDeleteRows = false;
                dgvDependencias.ReadOnly = true;
                lblEstado.Text = $"Agrupador '{actividad.Clave}' calculado automáticamente a partir de sus conceptos hijos. Los agrupadores no admiten dependencias directas.";
                return;
            }

            dgvDependencias.AllowUserToAddRows = true;
            dgvDependencias.AllowUserToDeleteRows = true;
            dgvDependencias.ReadOnly = false;
            colDepDescripcion.ReadOnly = true;
            colDepLag.ReadOnly = false;
            colDepClave.ReadOnly = false;
            colDepTipo.ReadOnly = false;

            var distribucion = _context.DistribucionesPeriodo
                .AsNoTracking()
                .Where(d => d.ActividadProgramadaId == actividad.Id)
                .Join(_context.PeriodosPrograma.AsNoTracking(),
                    d => d.PeriodoProgramaId,
                    p => p.Id,
                    (d, p) => new DistribucionRow
                    {
                        PeriodoProgramaId = p.Id,
                        NumeroPeriodo = p.NumeroPeriodo,
                        Periodo = p.Etiqueta,
                        FechaInicio = actividad.FechaInicioProgramada.HasValue && actividad.FechaInicioProgramada.Value.Date > p.FechaInicio.Date
                            ? actividad.FechaInicioProgramada.Value.Date
                            : p.FechaInicio.Date,
                        FechaFin = actividad.FechaFinProgramada.HasValue && actividad.FechaFinProgramada.Value.Date < p.FechaFin.Date
                            ? actividad.FechaFinProgramada.Value.Date
                            : p.FechaFin.Date,
                        Cantidad = d.CantidadProgramada,
                        Porcentaje = d.PorcentajeProgramado,
                        Importe = d.ImporteProgramado
                    })
                .OrderBy(x => x.NumeroPeriodo)
                .ToList();

            dgvDistribucion.DataSource = new BindingList<DistribucionRow>(distribucion);

            var dependencias = _context.DependenciasActividad
                .AsNoTracking()
                .Where(d => d.ActividadDestinoId == actividad.Id)
                .Join(_context.ActividadesProgramadas.AsNoTracking(),
                    d => d.ActividadOrigenId,
                    a => a.Id,
                    (d, a) => new DependenciaEditableRow
                    {
                        ActividadOrigenId = a.Id,
                        Clave = a.Clave,
                        Descripcion = a.Descripcion,
                        TipoDependencia = d.TipoDependencia.ToString(),
                        DesfaseDias = d.DesfaseDias
                    })
                .OrderBy(x => x.Clave)
                .ToList();

            _dependenciasEditables = new BindingList<DependenciaEditableRow>(dependencias);
            dgvDependencias.DataSource = _dependenciasEditables;
            _ultimaActividadDetalleId = actividad.Id;
            _detalleSeleccionDeferred = false;
        }

        private ActivityGridRowDto? GetActividadSeleccionada()
        {
            if (dgvActividades.CurrentRow?.DataBoundItem is ActivityGridRowDto dto)
                return dto;

            if (dgvActividades.SelectedRows.Count > 0 && dgvActividades.SelectedRows[0].DataBoundItem is ActivityGridRowDto dtoSeleccionado)
                return dtoSeleccionado;

            return null;
        }

        private void dgvActividades_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_cargando || _programaActual == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var columnName = dgvActividades.Columns[e.ColumnIndex].Name;
            if (columnName is not "colFechaInicio" and not "colFechaFin")
                return;

            var row = dgvActividades.Rows[e.RowIndex];
            if (row.DataBoundItem is not ActivityGridRowDto dto || dto.EsResumen)
                return;

            var textoCapturado = e.FormattedValue?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(textoCapturado))
                return;

            if (!DateTime.TryParse(textoCapturado, out var fechaCapturada))
                return;

            var candidata = fechaCapturada.Date;
            var original = columnName == "colFechaInicio"
                ? dto.FechaInicioProgramada?.Date
                : dto.FechaFinProgramada?.Date;

            if (original.HasValue && original.Value == candidata)
                return;

            var previewRow = new ActivityGridRowDto
            {
                Id = dto.Id,
                ActividadPadreId = dto.ActividadPadreId,
                ConceptoPresupuestoId = dto.ConceptoPresupuestoId,
                Clave = dto.Clave,
                Descripcion = dto.Descripcion,
                Unidad = dto.Unidad,
                CantidadTotal = dto.CantidadTotal,
                PrecioUnitario = dto.PrecioUnitario,
                ImporteTotal = dto.ImporteTotal,
                FechaInicioProgramada = columnName == "colFechaInicio" ? candidata : dto.FechaInicioProgramada,
                FechaFinProgramada = columnName == "colFechaFin" ? candidata : dto.FechaFinProgramada,
                DuracionDiasHabiles = dto.DuracionDiasHabiles,
                RendimientoDiario = dto.RendimientoDiario,
                FrentesTrabajo = dto.FrentesTrabajo,
                RutaCritica = dto.RutaCritica,
                PredecesoraResumen = dto.PredecesoraResumen,
                EsResumen = dto.EsResumen,
                Nivel = dto.Nivel,
                Orden = dto.Orden
            };

            NormalizarActividadEditada(previewRow, columnName);

            var preview = new ActivityEditDto
            {
                Id = previewRow.Id,
                ProgramaObraId = _programaActual.ProgramaObraId,
                ActividadPadreId = previewRow.ActividadPadreId,
                ConceptoPresupuestoId = previewRow.ConceptoPresupuestoId,
                Clave = previewRow.Clave,
                Descripcion = previewRow.Descripcion,
                Unidad = previewRow.Unidad,
                CantidadTotal = previewRow.CantidadTotal,
                PrecioUnitario = previewRow.PrecioUnitario,
                FechaInicioProgramada = previewRow.FechaInicioProgramada,
                FechaFinProgramada = previewRow.FechaFinProgramada,
                DuracionDiasHabiles = previewRow.DuracionDiasHabiles,
                RendimientoDiario = previewRow.RendimientoDiario,
                FrentesTrabajo = previewRow.FrentesTrabajo,
                EsResumen = previewRow.EsResumen,
                EsManual = previewRow.ConceptoPresupuestoId == null,
                Nivel = previewRow.Nivel,
                Orden = previewRow.Orden
            };

            var validation = _validationService.ValidateActivity(_context, preview);
            if (validation.Ok)
                return;

            e.Cancel = true;
            dgvActividades.CancelEdit();
            MessageBox.Show(validation.Error, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            lblEstado.Text = validation.Error;
        }

        private async void dgvActividades_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_cargando || e.RowIndex < 0 || _programaActual == null)
                return;

            var row = dgvActividades.Rows[e.RowIndex];
            if (row.DataBoundItem is not ActivityGridRowDto dto)
                return;

            var gridState = DataGridViewStateHelper.Capture(dgvActividades);

            try
            {
                _cargando = true;
                NormalizarActividadEditada(dto, dgvActividades.Columns[e.ColumnIndex].Name);

                var saveDto = new ActivityEditDto
                {
                    Id = dto.Id,
                    ProgramaObraId = _programaActual.ProgramaObraId,
                    ActividadPadreId = dto.ActividadPadreId,
                    ConceptoPresupuestoId = dto.ConceptoPresupuestoId,
                    Clave = dto.Clave,
                    Descripcion = dto.Descripcion,
                    Unidad = dto.Unidad,
                    CantidadTotal = dto.CantidadTotal,
                    PrecioUnitario = dto.PrecioUnitario,
                    FechaInicioProgramada = dto.FechaInicioProgramada,
                    FechaFinProgramada = dto.FechaFinProgramada,
                    DuracionDiasHabiles = dto.DuracionDiasHabiles,
                    RendimientoDiario = dto.RendimientoDiario,
                    FrentesTrabajo = dto.FrentesTrabajo,
                    EsResumen = dto.EsResumen,
                    EsManual = dto.ConceptoPresupuestoId == null,
                    Nivel = dto.Nivel,
                    Orden = dto.Orden
                };

                var result = _persistenceService.SaveActivity(_context, saveDto);
                if (!result.Ok)
                {
                    MessageBox.Show(result.Error, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RecargarProgramaDiferido(gridState);
                    return;
                }

                // Recalcular solo esta actividad y redistribuirla — no todo el programa
                var actividadId  = dto.Id;
                var programaId   = _programaActual.ProgramaObraId;
                var tipoPeriodo  = ObtenerTipoPeriodoSeleccionado();
                SetOcupado(true, $"Actualizando '{dto.Clave}'...");

                await Task.Run(() =>
                {
                    // 1. Recalcular fechas de esta actividad
                    _calculationService.RecalculateActivity(_context, actividadId);
                    // 2. Redistribuir solo esta actividad
                    _distributionService.DistributeUniform(_context, actividadId);
                    // 3. Recalcular agrupadores y ruta crítica del programa completo
                    _calculationService.RecalculateProgram(_context, programaId);
                    // 4. Regenerar periodos si las fechas del programa cambiaron
                    RegenerarPeriodosYDistribuciones(programaId, tipoPeriodo);
                });

                lblEstado.Text = $"Actividad '{dto.Clave}' actualizada.";
                RecargarProgramaDiferido(gridState);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo guardar la actividad: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RecargarProgramaDiferido(gridState);
            }
            finally
            {
                _cargando = false;
                SetOcupado(false);
            }
        }


        private void RecargarProgramaDiferido(DataGridViewStateSnapshot? stateToRestore = null)
        {
            if (!IsHandleCreated || IsDisposed)
                return;

            _pendingGridStateRestore = stateToRestore;

            if (_programaReloadPending)
                return;

            _programaReloadPending = true;
            BeginInvoke(new Action(() =>
            {
                _programaReloadPending = false;
                if (IsDisposed)
                    return;

                CargarPrograma(_pendingGridStateRestore);
            }));
        }

        private void NormalizarActividadEditada(ActivityGridRowDto dto, string columnName)
        {
            dto.FrentesTrabajo = Math.Max(1, dto.FrentesTrabajo);
            dto.CantidadTotal = Math.Max(0, dto.CantidadTotal);
            dto.RendimientoDiario = Math.Max(0, dto.RendimientoDiario);
            dto.DuracionDiasHabiles = Math.Max(0, dto.DuracionDiasHabiles);

            var frentes = Math.Max(1, dto.FrentesTrabajo);
            var tieneCantidad = dto.CantidadTotal > 0;
            var tieneDuracion = dto.DuracionDiasHabiles > 0;
            var tieneRendimiento = dto.RendimientoDiario > 0;

            if ((columnName == "colRendimientoDiario" || columnName == "colFrentes") && tieneCantidad && tieneRendimiento)
            {
                var divisor = dto.RendimientoDiario * frentes;
                if (divisor > 0)
                    dto.DuracionDiasHabiles = (int)Math.Ceiling(dto.CantidadTotal / divisor);
            }
            else if ((columnName == "colDuracionDias" || columnName == "colFechaInicio" || columnName == "colFechaFin") && tieneCantidad && tieneDuracion)
            {
                dto.RendimientoDiario = frentes <= 0
                    ? dto.RendimientoDiario
                    : Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
            }
            else if (columnName == "colCantidad")
            {
                if (tieneDuracion)
                {
                    dto.RendimientoDiario = Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
                }
                else if (tieneRendimiento)
                {
                    var divisor = dto.RendimientoDiario * frentes;
                    if (divisor > 0)
                        dto.DuracionDiasHabiles = (int)Math.Ceiling(dto.CantidadTotal / divisor);
                }
            }

            if (columnName == "colDuracionDias" && dto.FechaInicioProgramada.HasValue && _programaActual != null)
            {
                dto.FechaFinProgramada = _calculationService.CalculateFinishDate(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.DuracionDiasHabiles);
            }
            else if (columnName == "colFechaInicio" && dto.FechaInicioProgramada.HasValue && _programaActual != null)
            {
                if (dto.DuracionDiasHabiles > 0)
                {
                    dto.FechaFinProgramada = _calculationService.CalculateFinishDate(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.DuracionDiasHabiles);
                }
                else if (dto.FechaFinProgramada.HasValue)
                {
                    if (dto.FechaFinProgramada.Value.Date < dto.FechaInicioProgramada.Value.Date)
                        dto.FechaFinProgramada = dto.FechaInicioProgramada;

                    dto.DuracionDiasHabiles = _calculationService.CalculateBusinessDaysInclusive(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.FechaFinProgramada);
                    if (dto.DuracionDiasHabiles > 0 && dto.CantidadTotal > 0)
                    {
                        dto.RendimientoDiario = Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
                    }
                }
                else
                {
                    dto.FechaFinProgramada = dto.FechaInicioProgramada;
                    dto.DuracionDiasHabiles = Math.Max(1, dto.DuracionDiasHabiles);
                    if (dto.CantidadTotal > 0)
                    {
                        dto.RendimientoDiario = Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
                    }
                }
            }
            else if (columnName == "colFechaFin" && dto.FechaFinProgramada.HasValue && _programaActual != null)
            {
                var tienePredecesoras = !string.IsNullOrWhiteSpace(dto.PredecesoraResumen);

                if (!tienePredecesoras && dto.DuracionDiasHabiles > 0)
                {
                    dto.FechaInicioProgramada = _calculationService.CalculateStartDate(_context, _programaActual.ProgramaObraId, dto.FechaFinProgramada, dto.DuracionDiasHabiles);
                }
                else if (dto.FechaInicioProgramada.HasValue && dto.FechaFinProgramada.Value.Date < dto.FechaInicioProgramada.Value.Date)
                {
                    dto.FechaFinProgramada = dto.FechaInicioProgramada;
                }

                if (dto.FechaInicioProgramada.HasValue)
                {
                    dto.DuracionDiasHabiles = _calculationService.CalculateBusinessDaysInclusive(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.FechaFinProgramada);
                    if (dto.DuracionDiasHabiles > 0 && dto.CantidadTotal > 0)
                    {
                        dto.RendimientoDiario = Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
                    }
                }
            }
            else if ((columnName == "colRendimientoDiario" || columnName == "colFrentes" || columnName == "colCantidad") && dto.FechaInicioProgramada.HasValue && _programaActual != null)
            {
                dto.FechaFinProgramada = _calculationService.CalculateFinishDate(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.DuracionDiasHabiles);
            }

            dto.ImporteTotal = _proyecto != null
                ? new MotorCalculoSopro(_proyecto).Multiplicar(dto.CantidadTotal, dto.PrecioUnitario)
                : Math.Round(dto.CantidadTotal * dto.PrecioUnitario, 2, MidpointRounding.AwayFromZero);
        }

        private void dgvDependencias_UserDeletingRow(object? sender, DataGridViewRowCancelEventArgs e)
        {
            if (_cargando)
                return;

            BeginInvoke(new Action(GuardarDependenciasActuales));
        }

        private void dgvDependencias_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            _cancelandoEdicionDependencia = false;
        }

        private void dgvDependencias_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_comboDependenciaActivo != null)
            {
                _comboDependenciaActivo.KeyDown -= ComboDependencia_KeyDown;
                _comboDependenciaActivo = null;
            }

            if (e.Control is ComboBox combo)
            {
                _comboDependenciaActivo = combo;
                combo.KeyDown -= ComboDependencia_KeyDown;
                combo.KeyDown += ComboDependencia_KeyDown;
            }
        }

        private void ComboDependencia_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _cancelandoEdicionDependencia = true;
            }
        }

        private void dgvDependencias_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_cargando || e.RowIndex < 0)
                return;

            if (_cancelandoEdicionDependencia)
            {
                _cancelandoEdicionDependencia = false;
                return;
            }

            try
            {
                if (e.RowIndex >= dgvDependencias.Rows.Count)
                    return;

                var gridRow = dgvDependencias.Rows[e.RowIndex];
                if (gridRow.IsNewRow)
                    return;

                if (gridRow.DataBoundItem is not DependenciaEditableRow row)
                    return;

                NormalizarDependenciaEditada(row);
                GuardarDependenciasActuales();
            }
            catch (IndexOutOfRangeException)
            {
                // Puede ocurrir cuando se cancela un ComboBox con ESC antes de confirmar un valor.
                _cancelandoEdicionDependencia = false;
            }
            catch (InvalidOperationException)
            {
                // DataGridView puede quedar transitoriamente sin fila enlazada al cancelar edición con ESC.
                _cancelandoEdicionDependencia = false;
            }
        }

        private void NormalizarDependenciaEditada(DependenciaEditableRow row)
        {
            row.DesfaseDias = Math.Max(0, row.DesfaseDias);

            if (!row.ActividadOrigenId.HasValue || row.ActividadOrigenId.Value <= 0)
            {
                row.ActividadOrigenId = null;
                row.Clave = string.Empty;
                row.Descripcion = string.Empty;
                row.TipoDependencia = TipoDependenciaActividad.FS.ToString();
                return;
            }

            var origen = _actividades.FirstOrDefault(a => a.Id == row.ActividadOrigenId.Value && a.Id != GetActividadSeleccionada()?.Id);
            if (origen != null)
            {
                row.Clave = origen.Clave;
                row.Descripcion = origen.Descripcion;
            }
            else
            {
                row.ActividadOrigenId = null;
                row.Clave = string.Empty;
                row.Descripcion = string.Empty;
            }

            if (!Enum.TryParse<TipoDependenciaActividad>(row.TipoDependencia, true, out var tipo))
            {
                row.TipoDependencia = TipoDependenciaActividad.FS.ToString();
            }
            else
            {
                row.TipoDependencia = tipo.ToString();
            }

            dgvDependencias.Refresh();
        }

        private void GuardarDependenciasActuales()
        {
            var actividad = GetActividadSeleccionada();
            if (actividad == null || _programaActual == null)
                return;

            if (actividad.EsResumen)
            {
                lblEstado.Text = "Los agrupadores no admiten dependencias directas. Captura dependencias en conceptos.";
                return;
            }

            var deps = _dependenciasEditables
                .Select(x =>
                {
                    NormalizarDependenciaEditada(x);
                    return x;
                })
                .Where(x => x.ActividadOrigenId.HasValue && x.ActividadOrigenId.Value > 0)
                .GroupBy(x => x.ActividadOrigenId!.Value)
                .Select(g => g.First())
                .Select(x => new DependencyEditDto
                {
                    ActividadOrigenId = x.ActividadOrigenId!.Value,
                    ActividadDestinoId = actividad.Id,
                    TipoDependencia = Enum.TryParse<TipoDependenciaActividad>(x.TipoDependencia, true, out var tipo) ? tipo : TipoDependenciaActividad.FS,
                    DesfaseDias = Math.Max(0, x.DesfaseDias)
                })
                .ToList();

            var state = DataGridViewStateHelper.Capture(dgvActividades);
            var result = _persistenceService.SaveDependencies(_context, actividad.Id, deps);
            if (!result.Ok)
            {
                MessageBox.Show(result.Error, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var programaIdDep = _programaActual.ProgramaObraId;
            var tipoPeriodoDep = ObtenerTipoPeriodoSeleccionado();
            SetOcupado(true, $"Actualizando dependencias de '{actividad.Clave}'...");
            _ = Task.Run(() => RegenerarPeriodosYDistribuciones(programaIdDep, tipoPeriodoDep))
                .ContinueWith(_ =>
                {
                    BeginInvoke(new Action(() =>
                    {
                        SetOcupado(false);
                        lblEstado.Text = $"Dependencias de '{actividad.Clave}' actualizadas.";
                        RecargarProgramaDiferido(state);
                    }));
                });
        }

        private void tabPrograma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cargando)
                return;

            if (tabPrograma.SelectedTab == tabDependencias)
            {
                lblEstado.Text = "Dependencias: selecciona una actividad concepto. Los agrupadores no admiten dependencias. Fin → Inicio (FS): la otra termina y esta inicia. Inicio → Inicio (SS): ambas inician relacionadas.";
                ProcesarDetallePendienteSiAplica(true);
                SolicitarActualizacionDetalleSeleccion(true);
                return;
            }

            if (tabPrograma.SelectedTab == tabDistribucion)
            {
                ProcesarDetallePendienteSiAplica(true);
                return;
            }

            if (tabPrograma.SelectedTab == _tabCurvaS)
            {
                SolicitarActualizacionVisualPrograma();
            }
        }


        private void dgvActividades_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (_cargando)
                return;

            if (EsAtajoDependenciasValido(e.RowIndex, e.ColumnIndex))
            {
                AbrirDependenciasDesdePrograma();
            }
        }

        private void dgvActividades_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_cargando || e.KeyCode != Keys.F2)
                return;

            var cell = dgvActividades.CurrentCell;
            if (cell == null)
                return;

            if (EsAtajoDependenciasValido(cell.RowIndex, cell.ColumnIndex))
            {
                AbrirDependenciasDesdePrograma();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void dgvActividades_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (dgvActividades.Rows[e.RowIndex].DataBoundItem is not ActivityGridRowDto dto)
                return;

            var columnName = dgvActividades.Columns[e.ColumnIndex].Name;
            if (dto.EsResumen)
            {
                if (columnName is "colCantidad" or "colRendimientoDiario" or "colPrecioUnitario")
                {
                    e.Value = string.Empty;
                    e.FormattingApplied = true;
                    return;
                }

                if (columnName == "colFrentes")
                {
                    e.Value = string.Empty;
                    e.FormattingApplied = true;
                    return;
                }
            }

            if (columnName == "colPrecioUnitario" && e.Value is decimal pu)
            {
                e.Value = pu.ToStringImporte();
                e.FormattingApplied = true;
                return;
            }

            if (columnName == "colImporte" && e.Value is decimal importe)
            {
                e.Value = importe.ToStringImporte();
                e.FormattingApplied = true;
                return;
            }

            if (columnName == "colCantidad" && e.Value is decimal cant)
            {
                e.Value = cant.ToString($"N{_proyecto.DecimalesCantidad}",
                    System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
                return;
            }
        }

        private void Dgv_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            MessageBox.Show("El valor capturado no tiene un formato válido.", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class DistribucionRow
        {
            public int PeriodoProgramaId { get; set; }
            public int NumeroPeriodo { get; set; }
            public string Periodo { get; set; } = string.Empty;
            public DateTime FechaInicio { get; set; }
            public DateTime FechaFin { get; set; }
            public decimal Cantidad { get; set; }
            public decimal Porcentaje { get; set; }
            public decimal Importe { get; set; }
        }

        private sealed class DependenciaEditableRow
        {
            public int? ActividadOrigenId { get; set; }
            public string Clave { get; set; } = string.Empty;
            public string Descripcion { get; set; } = string.Empty;
            public string TipoDependencia { get; set; } = TipoDependenciaActividad.FS.ToString();
            public int DesfaseDias { get; set; }
        }

        private sealed class ActividadLookupItem
        {
            public int Id { get; set; }
            public string Display { get; set; } = string.Empty;
        }

        private sealed record DependencyTypeOption(string Value, string Text);

        public void RecalcularTodo()
        {
            btnRecalcular_Click(this, EventArgs.Empty);
        }
    }
}