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
using SOPRO.WinForms.Undo;

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
        private readonly UndoManager _undoManager = new();
        private bool _isUndoRedo;
        private int _undoActividadRowIndex = -1;
        private int _undoActividadColumnIndex = -1;
        private string _undoActividadColumnName = string.Empty;
        private object? _undoActividadOldValue;
        private int _undoDependenciaRowIndex = -1;
        private int _undoDependenciaColumnIndex = -1;
        private string _undoDependenciaColumnName = string.Empty;
        private object? _undoDependenciaOldValue;


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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && TryUndoPrograma())
                return true;

            if (keyData == (Keys.Control | Keys.Y) && TryRedoPrograma())
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
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








    }
}
