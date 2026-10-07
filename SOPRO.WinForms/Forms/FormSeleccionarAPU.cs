using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;
using System.Reflection;

namespace SOPRO.WinForms.Forms
{
    public partial class FormSeleccionarAPU : Form
    {
        // ── Constante de clave para persistencia de contexto ────────────────
        private const string SelectorKey = "APU";

        // ── Ítems centinela del ComboBox de proyecto ─────────────────────────
        private const string ExaminarTag = SelectorUiDefaults.ExaminarTag;
        private const string ScopeAllTag = SelectorUiDefaults.ScopeAllTag;
        private const string ScopeCurrentTag = SelectorUiDefaults.ScopeCurrentTag;
        private const string ScopeRecentTag = SelectorUiDefaults.ScopeRecentTag;
        private const string ScopeFavoritesTag = SelectorUiDefaults.ScopeFavoritesTag;

        // ── Dependencias ────────────────────────────────────────────────────
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly ExternalMatrixImportService _externalMatrixImportService = new(new ProjectDbContextFactory());
        private readonly ProjectIndexService _projectIndexService;
        private readonly SelectorContextService _selectorContextService;
        private readonly CatalogSearchService _catalogSearchService;
        private readonly ProjectUsageService _projectUsageService;

        // ── Estado ──────────────────────────────────────────────────────────
        private readonly string? _filtroInicial;
        private readonly string _projectNameActual;
        private string? _externalProjectPath;
        private readonly bool _includeAuxiliaries;
        private readonly int? _matrizIdPreseleccionada;

        // ── Flag para evitar reentrada en SelectedIndexChanged ──────────────
        private bool _cboProyectoLoading;
        private bool _showingSearchResults;
        private string _searchScopeTag = ScopeAllTag;

        // ── Debounce para búsqueda transversal (evita consultas por cada keystroke) ──
        private System.Windows.Forms.Timer? _searchDebounceTimer;
        private bool _suppressSearchDebounce;

        private const int MaxProjectsInCombo = SelectorUiDefaults.MaxProjectsInCombo;
        private const int MaxRowsInGrid = SelectorUiDefaults.MaxRowsInGrid;

        // ── Propiedades de resultado ─────────────────────────────────────────
        public Matriz MatrizSeleccionada { get; private set; }
        public decimal Cantidad { get; private set; }
        public bool EmbeddedMode { get; private set; }
        public bool WorkspaceChromeHidden { get; private set; }
        public event EventHandler? EmbeddedAccepted;
        public event EventHandler? EmbeddedCancelled;
        public event EventHandler? EmbeddedRequestNewMatrix;
        public event EventHandler? EmbeddedRequestEditMatrix;

        /// <summary>
        /// Se dispara cuando cambia la selección del grid de matrices. El host embebido
        /// lo usa para sincronizar el estado habilitado de sus comandos.
        /// </summary>
        public event EventHandler? MatrixSelectionChanged;

        private IWin32Window GetDialogOwner() => FindForm() ?? this;



        // ════════════════════════════════════════════════════════════════════
        // Constructor
        // ════════════════════════════════════════════════════════════════════

        public FormSeleccionarAPU(
            SOPROContext context,
            int proyectoId,
            decimal cantidadInicial = 1,
            bool includeAuxiliaries = false,
            int? matrizIdPreseleccionada = null,
            string? filtroInicial = null)
        {
            _context                 = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId              = proyectoId;
            _includeAuxiliaries      = includeAuxiliaries;
            _matrizIdPreseleccionada = matrizIdPreseleccionada;
            _filtroInicial           = filtroInicial;

            var workspaceService    = new ProjectWorkspaceService();
            _projectIndexService    = new ProjectIndexService(workspaceService);
            _selectorContextService = new SelectorContextService(workspaceService);
            _projectUsageService    = new ProjectUsageService(workspaceService);
            _catalogSearchService   = new CatalogSearchService(_projectIndexService, _projectUsageService, new ProjectDbContextFactory());
            _projectIndexService.RefreshKnownProjects(_context.DatabasePath, force: true);
            _projectNameActual      = _context.Proyectos.Find(_proyectoId)?.Nombre
                                      ?? Path.GetFileNameWithoutExtension(_context.DatabasePath);

            InitializeComponent();
            dgvMatrices.AplicarEstiloSOPRO();
            dgvMatrices.ColumnHeadersHeight = SoproUiMetrics.GridHeaderHeight;
            ConfigureSelectorColumns();
            ApplyCorporateVisuals();
            ApplySelectorGridSelectionStyle();
            dgvMatrices.SelectionChanged += OnMatrizSelectionChanged;
            ApplyRenderOptimizations();
            InitializeTipoSelector();
            InitializeFavoritosContextMenu();
            RestoreSelectorContext();
            PopulateProyectoCombo();
            KeyPreview = true;
            Shown += FormSeleccionarAPU_Shown;
            txtBuscar.KeyDown += txtBuscar_KeyDown;
            dgvMatrices.KeyPress += dgvMatrices_KeyPress;

            if (cantidadInicial < nudCantidad.Minimum) cantidadInicial = nudCantidad.Minimum;
            if (cantidadInicial > nudCantidad.Maximum) cantidadInicial = nudCantidad.Maximum;
            nudCantidad.Value = cantidadInicial;

            CargarMatricesProyectoActual();
            ApplyInitialFilter();
            SyncActionCommandStates();
        }

        // ════════════════════════════════════════════════════════════════════
        // Apariencia corporativa y sincronización de comandos
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Prioriza las columnas del selector APU en anchos de laptop: descripción, unidad y datos
        /// económicos conservan mayor <see cref="DataGridViewColumn.FillWeight"/> y un ancho mínimo
        /// legible, mientras que los metadatos secundarios (tipo/origen/proyecto/fecha) usan menor
        /// peso y un mínimo reducido. Con esto, al estrechar la ventana primero se truncan los
        /// metadatos (consultables por tooltip) o aparece scroll horizontal, antes que comprimir
        /// las columnas esenciales.
        /// </summary>
        private void ConfigureSelectorColumns()
        {
            dgvMatrices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Esenciales: descripción y datos económicos con mayor peso y piso de ancho.
            colDescripcion.FillWeight = 260F;
            colDescripcion.MinimumWidth = 240;
            colCosto.FillWeight = 100F;
            colCosto.MinimumWidth = 110;
            colClave.FillWeight = 90F;
            colClave.MinimumWidth = 90;
            colUnidad.FillWeight = 45F;
            colUnidad.MinimumWidth = 64;

            // Metadatos secundarios: menor peso y mínimo reducido (truncables con tooltip).
            colTipo.FillWeight = 40F;
            colTipo.MinimumWidth = 52;
            colOrigen.FillWeight = 45F;
            colOrigen.MinimumWidth = 56;
            colProyecto.FillWeight = 70F;
            colProyecto.MinimumWidth = 80;
            colFecha.FillWeight = 55F;
            colFecha.MinimumWidth = 70;

            dgvMatrices.ShowCellToolTips = true;
            dgvMatrices.CellToolTipTextNeeded += DgvMatrices_CellToolTipTextNeeded;
        }

        /// <summary>
        /// Expone el valor completo de la celda como tooltip cuando el ancho de la columna lo
        /// trunca, de modo que la información secundaria sigue siendo consultable sin ensanchar
        /// las columnas esenciales.
        /// </summary>
        private void DgvMatrices_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var valor = Convert.ToString(dgvMatrices.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
            e.ToolTipText = string.IsNullOrWhiteSpace(valor) ? string.Empty : valor;
        }

        /// <summary>
        /// Iconos vectoriales del sistema SOPRO para los controles propios del selector.
        /// Se usan los <see cref="SoproIconType"/> disponibles; donde no exista un icono
        /// semántico se conserva solo texto (nunca emoji).
        /// </summary>
        private void ApplyCorporateVisuals()
        {
            // La etiqueta "Buscar:" se muestra solo con texto: System.Windows.Forms.Label
            // no soporta TextImageRelation, por lo que un icono en línea no es viable.
            btnNuevaMatriz.Image = SoproIconProvider.GetIcon(
                SoproIconType.Matrices, Color.White, SoproUiMetrics.RibbonGlyphButtonIconSize);
            btnNuevaMatriz.ImageAlign = ContentAlignment.MiddleLeft;
            btnNuevaMatriz.TextAlign = ContentAlignment.MiddleCenter;
            btnNuevaMatriz.TextImageRelation = TextImageRelation.ImageBeforeText;
        }

        /// <summary>
        /// Aplica al grid del selector APU el mismo estilo de selección del panel embebido y
        /// del presupuesto: fila completa en azul claro y contorno azul de 2px en la celda activa.
        /// Replica solo el efecto visual, sin usar <c>DgvCeldaHelper</c>.
        /// </summary>
        private void ApplySelectorGridSelectionStyle()
        {
            // Patrón read-mutate-assign-back: el getter devuelve una copia
            // desechable, así que hay que reasignar el style al grid.
            var baseStyle = dgvMatrices.DefaultCellStyle;
            baseStyle.SelectionBackColor = System.Drawing.Color.FromArgb(230, 240, 255);
            baseStyle.SelectionForeColor = System.Drawing.Color.Black;
            dgvMatrices.DefaultCellStyle = baseStyle;
            // Reafirmar la selección en filas alternas para que el zebra no se
            // imponga sobre el fondo de la fila seleccionada (selección uniforme).
            var altStyle = dgvMatrices.AlternatingRowsDefaultCellStyle;
            altStyle.SelectionBackColor = System.Drawing.Color.FromArgb(230, 240, 255);
            altStyle.SelectionForeColor = System.Drawing.Color.Black;
            dgvMatrices.AlternatingRowsDefaultCellStyle = altStyle;
            dgvMatrices.CellPainting += DgvMatrices_CellPainting;
            dgvMatrices.CurrentCellChanged += (s, e) => dgvMatrices.Invalidate();
        }

        // Contorno azul de 2px sobre la celda activa (replica el estilo del presupuesto).
        private static void DgvMatrices_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var dgv = (DataGridView)sender!;
            e.Paint(e.CellBounds, DataGridViewPaintParts.All);

            if (dgv.CurrentCell != null &&
                e.RowIndex == dgv.CurrentCell.RowIndex &&
                e.ColumnIndex == dgv.CurrentCell.ColumnIndex)
            {
                using var pen = new Pen(Color.FromArgb(0, 120, 215), 2);
                e.Graphics?.DrawRectangle(pen,
                    e.CellBounds.Left + 1,
                    e.CellBounds.Top + 1,
                    e.CellBounds.Width - 3,
                    e.CellBounds.Height - 3);
            }

            e.Handled = true;
        }

        private void OnMatrizSelectionChanged(object? sender, EventArgs e)
        {
            SyncActionCommandStates();
            MatrixSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sincroniza los comandos propios del selector con la selección del grid:
        /// Asignar requiere una matriz seleccionada. Editar lo controla el propio
        /// <c>dgvMatrices_SelectionChanged</c> (depende del origen de la fila).
        /// </summary>
        private void SyncActionCommandStates()
        {
            bool hasSelection = dgvMatrices.SelectedRows.Count > 0;
            btnAceptar.Enabled = hasSelection;
            btnNuevaMatriz.Enabled = true;
        }
    }
}
