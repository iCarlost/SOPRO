using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
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
        private readonly ExternalMatrixImportService _externalMatrixImportService = new();
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
            _catalogSearchService   = new CatalogSearchService(_projectIndexService, _projectUsageService);
            _projectNameActual      = _context.Proyectos.Find(_proyectoId)?.Nombre
                                      ?? Path.GetFileNameWithoutExtension(_context.DatabasePath);

            InitializeComponent();
            ApplyRenderOptimizations();
            InitializeTipoSelector();
            InitializeFavoritosContextMenu();
            RestoreSelectorContext();
            PopulateProyectoCombo();

            if (cantidadInicial < nudCantidad.Minimum) cantidadInicial = nudCantidad.Minimum;
            if (cantidadInicial > nudCantidad.Maximum) cantidadInicial = nudCantidad.Maximum;
            nudCantidad.Value = cantidadInicial;

            CargarMatricesProyectoActual();
            ApplyInitialFilter();
        }

        // ════════════════════════════════════════════════════════════════════
        // Inicialización
        // ════════════════════════════════════════════════════════════════════

        private void ApplyRenderOptimizations()
        {
            FormRenderHelper.OptimizeForGridRendering(this);
            EnableDoubleBuffer(dgvMatrices);
            EnableDoubleBuffer(panelTop);
            EnableDoubleBuffer(panelInfo);
        }

        private static void EnableDoubleBuffer(Control control)
        {
            if (control == null) return;

            var prop = typeof(Control).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop?.SetValue(control, true, null);
        }

        private void RunGridUpdate(Action action)
        {
            if (action == null) return;

            SuspendLayout();
            panelInfo.SuspendLayout();
            dgvMatrices.SuspendLayout();
            using (GridRedrawHelper.Suspend(dgvMatrices))
            {
                try
                {
                    action();
                }
                finally
                {
                    dgvMatrices.ResumeLayout();
                    panelInfo.ResumeLayout();
                    ResumeLayout(true);
                }
            }
        }

        private void InitializeTipoSelector()
        {
            if (!_includeAuxiliaries) return;

            // Cargar ítems (TipoFiltroOption es clase interna, no serializable en Designer)
            _cboTipo.Items.Add(new TipoFiltroOption("Todos",     null));
            _cboTipo.Items.Add(new TipoFiltroOption("APU",       TipoMatriz.APU));
            _cboTipo.Items.Add(new TipoFiltroOption("Básico",    TipoMatriz.Basico));
            _cboTipo.Items.Add(new TipoFiltroOption("Cuadrilla", TipoMatriz.Cuadrilla));
            _cboTipo.SelectedIndex = 0;
            _cboTipo.SelectedIndexChanged += (_, __) => ApplyFilterToGrid();
            _cboTipo.Visible = true;
        }

        /// <summary>
        /// Carga el selector de alcance/origen de búsqueda en _cboProyecto.
        /// </summary>
        private void PopulateProyectoCombo()
        {
            _cboProyectoLoading = true;
            try
            {
                var currentPath = SelectorUiDefaults.NormalizePath(_context.DatabasePath);
                var favorites = _projectIndexService.GetFavoriteProjects()
                    .Where(p => File.Exists(p.FilePath))
                    .ToList();
                var recents = _projectIndexService.GetRecentProjects(MaxProjectsInCombo)
                    .Where(p => File.Exists(p.FilePath))
                    .ToList();

                _cboProyecto.Items.Clear();
                _cboProyecto.Items.Add(new ProyectoComboItem("🔎 Todos", ScopeAllTag, ProyectoComboKind.Scope));
                _cboProyecto.Items.Add(new ProyectoComboItem("📁 Proyecto actual", ScopeCurrentTag, ProyectoComboKind.Scope));
                _cboProyecto.Items.Add(new ProyectoComboItem("🕘 Recientes", ScopeRecentTag, ProyectoComboKind.Scope));
                _cboProyecto.Items.Add(new ProyectoComboItem("⭐ Favoritos", ScopeFavoritesTag, ProyectoComboKind.Scope));
                _cboProyecto.Items.Add(new ProyectoComboItem("────────────────", string.Empty, ProyectoComboKind.Separator));

                var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in favorites)
                {
                    var path = SelectorUiDefaults.NormalizePath(p.FilePath);
                    if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!added.Add(path)) continue;
                    _cboProyecto.Items.Add(new ProyectoComboItem($"★ {p.Name}", path, ProyectoComboKind.Project));
                }

                foreach (var p in recents)
                {
                    var path = SelectorUiDefaults.NormalizePath(p.FilePath);
                    if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!added.Add(path)) continue;
                    _cboProyecto.Items.Add(new ProyectoComboItem($"🕘 {p.Name}", path, ProyectoComboKind.Project));
                }

                _cboProyecto.Items.Add(new ProyectoComboItem("────────────────", string.Empty, ProyectoComboKind.Separator));
                _cboProyecto.Items.Add(new ProyectoComboItem("📁 Examinar archivo...", ExaminarTag, ProyectoComboKind.Action));

                SelectScopeInCombo();
            }
            finally
            {
                _cboProyectoLoading = false;
            }
        }

        /// <summary>Restaura contexto mínimo del selector. El alcance del combo siempre inicia en "Todos".</summary>
        private void RestoreSelectorContext()
        {
            var state = _selectorContextService.Get(SelectorKey);
            if (!string.IsNullOrWhiteSpace(state.LastExternalProjectPath)
                && File.Exists(state.LastExternalProjectPath))
            {
                _externalProjectPath = state.LastExternalProjectPath;
            }

            _searchScopeTag = ScopeAllTag;
        }

        private void ApplyInitialFilter()
        {
            if (!string.IsNullOrWhiteSpace(_filtroInicial))
            {
                _suppressSearchDebounce = true;
                try
                {
                    txtBuscar.Text = _filtroInicial.Trim();
                    txtBuscar.SelectionStart = txtBuscar.TextLength;
                }
                finally
                {
                    _suppressSearchDebounce = false;
                }

                ApplyFilterToGrid();
            }
            else
            {
                _suppressSearchDebounce = true;
                try
                {
                    txtBuscar.Clear();
                }
                finally
                {
                    _suppressSearchDebounce = false;
                }

                ApplyFilterToGrid();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // ComboBox de proyecto unificado
        // ════════════════════════════════════════════════════════════════════

        private void cboProyecto_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cboProyectoLoading) return;
            if (_cboProyecto.SelectedItem is not ProyectoComboItem item) return;

            if (item.Kind == ProyectoComboKind.Separator)
            {
                SelectScopeInCombo();
                return;
            }

            if (item.Kind == ProyectoComboKind.Action && string.Equals(item.Value, ExaminarTag, StringComparison.OrdinalIgnoreCase))
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Seleccionar proyecto SOPRO",
                    Filter = "Bases de proyecto SOPRO (*.db;*.sopro)|*.db;*.sopro|Todos los archivos (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _searchScopeTag = SelectorUiDefaults.NormalizePath(dlg.FileName);
                    ApplyFilterToGrid();
                    PopulateProyectoCombo();
                }
                else
                {
                    SelectScopeInCombo();
                }
                return;
            }

            if (item.Kind == ProyectoComboKind.Scope)
            {
                _searchScopeTag = item.Value;
            }
            else if (item.Kind == ProyectoComboKind.Project)
            {
                _searchScopeTag = SelectorUiDefaults.NormalizePath(item.Value);
            }

            ApplyFilterToGrid();
            SaveSelectorContext();
        }

        // ════════════════════════════════════════════════════════════════════
        // Carga de matrices
        // ════════════════════════════════════════════════════════════════════

        private void CargarMatricesProyectoActual()
        {
            try
            {
                _showingSearchResults = false;
                dgvMatrices.Rows.Clear();
                var selectedTipo = GetSelectedTipoFiltro();
                var matrices = _includeAuxiliaries
                    ? MatrixApplicationService.GetProjectMatrices(_context, _proyectoId, selectedTipo)
                    : MatrixApplicationService.GetProjectApus(_context, _proyectoId);
                var totalMatrices = matrices.Count;
                matrices = matrices.Take(MaxRowsInGrid).ToList();
                var fechaRef = SelectorUiDefaults.GetProjectReferenceDate(_context.DatabasePath);

                foreach (var matriz in matrices)
                {
                    var idx = dgvMatrices.Rows.Add(
                        matriz.Id,
                        GetTipoBadge(matriz.Tipo),
                        matriz.Clave,
                        matriz.Descripcion,
                        matriz.Unidad,
                        matriz.CostoDirecto.ToString("C4"),
                        "Actual",
                        _projectNameActual,
                        fechaRef?.ToString("dd/MM/yyyy") ?? string.Empty);
                    dgvMatrices.Rows[idx].Tag = matriz;
                }

                _projectIndexService.RegisterProjectOpened(_context.DatabasePath, _projectNameActual);
                try { _catalogSearchService.RebuildProjectCatalogIndex(_context.DatabasePath); } catch { }
                _externalProjectPath      = null;
                lblStatus.Text = _includeAuxiliaries
                    ? SelectorUiDefaults.BuildBaseLoadStatus(totalMatrices, matrices.Count, "matrices", "proyecto actual")
                    : SelectorUiDefaults.BuildBaseLoadStatus(totalMatrices, matrices.Count, "APUs", "proyecto actual");

                FinalizeGridAfterLoad();
                SaveSelectorContext();
                PopulateProyectoCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar matrices:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarMatricesExternas(string projectPath)
        {
            try
            {
                _showingSearchResults = false;
                var selectedTipo = GetSelectedTipoFiltro();
                var result = _includeAuxiliaries
                    ? _externalMatrixImportService.LoadExternalMatrices(projectPath, selectedTipo)
                    : _externalMatrixImportService.LoadExternalApus(projectPath);
                dgvMatrices.Rows.Clear();
                var totalMatrices = result.Matrices.Count;
                var matricesToShow = result.Matrices.Take(MaxRowsInGrid).ToList();
                var fechaRef = SelectorUiDefaults.GetProjectReferenceDate(result.ProjectPath);

                foreach (var matriz in matricesToShow)
                {
                    var idx = dgvMatrices.Rows.Add(
                        matriz.MatrixId,
                        "🌐 " + GetTipoEtiquetaCorta(matriz.Tipo),
                        matriz.Clave,
                        matriz.Descripcion,
                        matriz.Unidad,
                        matriz.CostoDirecto.ToString("C4"),
                        "Externo",
                        result.ProjectName,
                        fechaRef?.ToString("dd/MM/yyyy") ?? string.Empty);
                    dgvMatrices.Rows[idx].Tag = matriz;
                }

                _projectIndexService.RegisterProjectOpened(result.ProjectPath, result.ProjectName);
                try { _catalogSearchService.RebuildProjectCatalogIndex(result.ProjectPath); } catch { }
                _externalProjectPath      = result.ProjectPath;
                lblStatus.Text = _includeAuxiliaries
                    ? SelectorUiDefaults.BuildBaseLoadStatus(totalMatrices, matricesToShow.Count, "matrices", $"'{result.ProjectName}'")
                    : SelectorUiDefaults.BuildBaseLoadStatus(totalMatrices, matricesToShow.Count, "APUs", $"'{result.ProjectName}'");

                FinalizeGridAfterLoad();
                SaveSelectorContext();
                _searchScopeTag = SelectorUiDefaults.NormalizePath(result.ProjectPath);
                PopulateProyectoCombo();
                SelectScopeInCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar matrices externas:\n{ex.Message}",
                    "Proyecto externo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Sincroniza el combo con el alcance activo actual.</summary>
        private void SelectScopeInCombo()
        {
            _cboProyectoLoading = true;
            try
            {
                var target = _searchScopeTag;
                for (int i = 0; i < _cboProyecto.Items.Count; i++)
                {
                    if (_cboProyecto.Items[i] is not ProyectoComboItem item) continue;
                    if (item.Kind == ProyectoComboKind.Separator) continue;
                    if (string.Equals(item.Value, target, StringComparison.OrdinalIgnoreCase))
                    {
                        _cboProyecto.SelectedIndex = i;
                        return;
                    }
                }
                _cboProyecto.SelectedIndex = 0;
            }
            finally
            {
                _cboProyectoLoading = false;
            }
        }

        private void InitializeFavoritosContextMenu()
        {
            ctxProyectoFavorito.Opening += ctxProyectoFavorito_Opening;
            mnuToggleFavorito.Click += mnuToggleFavorito_Click;
            dgvMatrices.MouseDown += dgvMatrices_MouseDown;
            dgvMatrices.ContextMenuStrip = ctxProyectoFavorito;
        }

        private void dgvMatrices_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = dgvMatrices.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0) return;

            dgvMatrices.ClearSelection();
            var row = dgvMatrices.Rows[hit.RowIndex];
            row.Selected = true;
            if (hit.ColumnIndex >= 0)
                dgvMatrices.CurrentCell = dgvMatrices.Rows[hit.RowIndex].Cells[hit.ColumnIndex];
        }

        private void ctxProyectoFavorito_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var row = dgvMatrices.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (row?.Tag is not ExternalProjectMatrixOption externalMatrix)
            {
                e.Cancel = true;
                return;
            }

            var isFavorite = _projectIndexService.IsFavorite(externalMatrix.ProjectPath);
            mnuToggleFavorito.Text = isFavorite
                ? "Quitar proyecto origen de favoritos"
                : "Marcar proyecto origen como favorito";
        }

        private void mnuToggleFavorito_Click(object? sender, EventArgs e)
        {
            var row = dgvMatrices.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (row?.Tag is not ExternalProjectMatrixOption externalMatrix) return;

            var isFavorite = _projectIndexService.IsFavorite(externalMatrix.ProjectPath);
            _projectIndexService.SetFavorite(externalMatrix.ProjectPath, !isFavorite, externalMatrix.ProjectName);
            PopulateProyectoCombo();
            SaveSelectorContext();

            if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
                EjecutarBusquedaTransversal(txtBuscar.Text.Trim());
            else
                lblStatus.Text = !isFavorite
                    ? $"'{externalMatrix.ProjectName}' marcado como favorito."
                    : $"'{externalMatrix.ProjectName}' removido de favoritos.";
        }

        private void FinalizeGridAfterLoad()
        {
            if (string.IsNullOrWhiteSpace(txtBuscar.Text))
            {
                foreach (DataGridViewRow row in dgvMatrices.Rows)
                    row.Visible = true;
                EnsureVisibleSelection();
                return;
            }

            EjecutarBusquedaTransversal(txtBuscar.Text.Trim());
        }

        // ════════════════════════════════════════════════════════════════════
        // Filtro de grid
        // ════════════════════════════════════════════════════════════════════

        private void txtBuscar_TextChanged(object sender, EventArgs e)
        {
            if (_suppressSearchDebounce)
                return;

            // Debounce: esperar 350ms desde el último keystroke antes de ejecutar
            // la búsqueda transversal, que puede abrir múltiples archivos .db.
            if (_searchDebounceTimer == null)
            {
                _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = SelectorUiDefaults.SearchDebounceMs };
                _searchDebounceTimer.Tick += (_, __) =>
                {
                    _searchDebounceTimer.Stop();
                    ApplyFilterToGrid();
                    SaveSelectorContext();
                };
            }
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void ApplyFilterToGrid()
        {
            var busqueda = txtBuscar.Text.Trim();
            if (string.IsNullOrWhiteSpace(busqueda))
            {
                ApplyEmptyScopeState();
                return;
            }

            EjecutarBusquedaTransversal(busqueda);
        }

        private void ApplyEmptyScopeState()
        {
            if (_searchScopeTag == ScopeCurrentTag)
            {
                ShowCurrentProjectBase("Mostrando matrices del proyecto actual.");
                return;
            }

            if (_searchScopeTag == ScopeAllTag)
            {
                ShowCurrentProjectBase(SelectorUiDefaults.BuildAllScopePrompt("matrices"));
                return;
            }

            if (_searchScopeTag == ScopeRecentTag)
            {
                ClearGridForEmptyScope(SelectorUiDefaults.BuildRecentScopePrompt());
                return;
            }

            if (_searchScopeTag == ScopeFavoritesTag)
            {
                ClearGridForEmptyScope(SelectorUiDefaults.BuildFavoritesScopePrompt());
                return;
            }

            CargarMatricesExternas(_searchScopeTag);
        }

        private void ShowCurrentProjectBase(string statusMessage)
        {
            var hasCurrentBaseLoaded = !_showingSearchResults
                && string.IsNullOrWhiteSpace(_externalProjectPath)
                && dgvMatrices.Rows.Count > 0;
            if (!hasCurrentBaseLoaded)
            {
                CargarMatricesProyectoActual();
            }

            lblStatus.Text = statusMessage;
            EnsureVisibleSelection();
        }

        private void ClearGridForEmptyScope(string statusMessage)
        {
            RunGridUpdate(() =>
            {
                _showingSearchResults = false;
                _externalProjectPath = null;
                dgvMatrices.Rows.Clear();
                dgvMatrices.ClearSelection();
                btnEditarMatriz.Enabled = false;
                lblUnidad.Text = "---";
                lblCostoUnitario.Text = "$0.00";
                lblImporte.Text = "$0.00";
                lblStatus.Text = statusMessage;
            });
        }

        private void EjecutarBusquedaTransversal(string busqueda)
        {
            try
            {
                var includeCurrentProject = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeCurrentTag;
                var includeRecentProjects = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeRecentTag;
                var includeFavoriteProjects = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeFavoritesTag;
                IReadOnlyList<CatalogSearchResultDto> results;

                if (_searchScopeTag != ScopeAllTag
                    && _searchScopeTag != ScopeCurrentTag
                    && _searchScopeTag != ScopeRecentTag
                    && _searchScopeTag != ScopeFavoritesTag)
                {
                    results = SearchInSingleProject(busqueda, _searchScopeTag);
                }
                else
                {
                    results = _catalogSearchService.SearchMatrices(
                        _context,
                        _proyectoId,
                        busqueda,
                        includeCurrentProject: includeCurrentProject,
                        includeRecentProjects: includeRecentProjects,
                        includeFavoriteProjects: includeFavoriteProjects,
                        includeAuxiliaries: _includeAuxiliaries,
                        tipoFiltro: GetSelectedTipoFiltro(),
                        maxResults: 120);
                }

                BindSearchResults(results);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar matrices en proyectos indexados:{ex.Message}", "Búsqueda",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IReadOnlyList<CatalogSearchResultDto> SearchInSingleProject(string busqueda, string projectPath)
        {
            if (!File.Exists(projectPath))
                return Array.Empty<CatalogSearchResultDto>();

            var normalizedTarget = SelectorUiDefaults.NormalizePath(projectPath);
            var normalizedCurrent = SelectorUiDefaults.NormalizePath(_context.DatabasePath);
            if (string.Equals(normalizedTarget, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                return _catalogSearchService.SearchMatrices(
                    _context,
                    _proyectoId,
                    busqueda,
                    includeCurrentProject: true,
                    includeRecentProjects: false,
                    includeFavoriteProjects: false,
                    includeAuxiliaries: _includeAuxiliaries,
                    tipoFiltro: GetSelectedTipoFiltro(),
                    maxResults: 120);
            }

            return _catalogSearchService.SearchMatrices(
                _context,
                _proyectoId,
                busqueda,
                includeCurrentProject: false,
                includeRecentProjects: false,
                includeFavoriteProjects: false,
                includeAuxiliaries: _includeAuxiliaries,
                tipoFiltro: GetSelectedTipoFiltro(),
                maxResults: 120,
                specificProjectPaths: new[] { normalizedTarget });
        }

        private void BindSearchResults(IReadOnlyList<CatalogSearchResultDto> results)
        {
            RunGridUpdate(() =>
            {
                _showingSearchResults = true;
                dgvMatrices.Rows.Clear();

                foreach (var result in results)
                {
                    var idx = dgvMatrices.Rows.Add(
                        result.ElementoId,
                        result.EsActual ? GetTipoBadge(result.TipoMatriz) : "🌐 " + GetTipoEtiquetaCorta(result.TipoMatriz),
                        result.Clave,
                        result.Descripcion,
                        result.Unidad,
                        result.PrecioOCosto.ToString("C4"),
                        result.EsActual ? "Actual" : "Externo",
                        result.NombreProyecto,
                        result.FechaReferencia?.ToString("dd/MM/yyyy") ?? string.Empty);

                    dgvMatrices.Rows[idx].Tag = result.EsActual
                        ? new MatrixListItemDto
                        {
                            Id = result.ElementoId,
                            Clave = result.Clave,
                            Descripcion = result.Descripcion,
                            Unidad = result.Unidad,
                            CostoDirecto = result.PrecioOCosto,
                            Tipo = result.TipoMatriz
                        }
                        : new ExternalProjectMatrixOption
                        {
                            MatrixId = result.ElementoId,
                            ProjectName = result.NombreProyecto,
                            ProjectPath = result.RutaProyecto,
                            Clave = result.Clave,
                            Descripcion = result.Descripcion,
                            Unidad = result.Unidad,
                            CostoDirecto = result.PrecioOCosto,
                            Tipo = result.TipoMatriz
                        };
                }

                lblStatus.Text = results.Count == 0
                    ? "Sin coincidencias en el ámbito seleccionado."
                    : $"{results.Count} coincidencia(s) en el ámbito seleccionado.";

                EnsureVisibleSelection();
            });
        }

        private void EnsureVisibleSelection()
        {
            if (dgvMatrices.Rows.Count == 0) return;

            if (dgvMatrices.CurrentRow != null && dgvMatrices.CurrentRow.Visible) return;

            var visible = dgvMatrices.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Visible);
            if (visible == null)
            {
                dgvMatrices.ClearSelection();
                btnEditarMatriz.Enabled = false;
                lblUnidad.Text        = "---";
                lblCostoUnitario.Text = "$0.00";
                lblImporte.Text       = "$0.00";
                return;
            }
            SeleccionarFilaInicial();
        }

        // ════════════════════════════════════════════════════════════════════
        // Selección y cálculo
        // ════════════════════════════════════════════════════════════════════

        private void SeleccionarFilaInicial()
        {
            if (dgvMatrices.Rows.Count == 0) return;

            DataGridViewRow? targetRow = null;
            if (_matrizIdPreseleccionada.HasValue)
            {
                foreach (DataGridViewRow row in dgvMatrices.Rows)
                {
                    if (!row.Visible) continue;
                    if (row.Tag is MatrixListItemDto lm && lm.Id == _matrizIdPreseleccionada.Value)
                    { targetRow = row; break; }
                }
            }
            targetRow ??= dgvMatrices.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Visible)
                          ?? dgvMatrices.Rows[0];

            dgvMatrices.ClearSelection();
            targetRow.Selected = true;

            var cell = targetRow.Cells.Cast<DataGridViewCell>()
                .FirstOrDefault(c => c.Visible && c.OwningColumn.Visible && !c.ReadOnly)
                ?? targetRow.Cells.Cast<DataGridViewCell>()
                    .FirstOrDefault(c => c.Visible && c.OwningColumn.Visible);
            if (cell != null && cell.Visible && cell.OwningColumn.Visible)
                dgvMatrices.CurrentCell = cell;
            if (targetRow.Visible)
                dgvMatrices.FirstDisplayedScrollingRowIndex = targetRow.Index;
        }

        private void dgvMatrices_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvMatrices.SelectedRows.Count == 0) { btnEditarMatriz.Enabled = false; return; }
            var row = dgvMatrices.SelectedRows[0];
            switch (row.Tag)
            {
                case MatrixListItemDto lm:
                    lblUnidad.Text = lm.Unidad;
                    lblCostoUnitario.Text = lm.CostoDirecto.ToString("C4");
                    btnEditarMatriz.Enabled = true;
                    break;
                case ExternalProjectMatrixOption em:
                    lblUnidad.Text = em.Unidad;
                    lblCostoUnitario.Text = em.CostoDirecto.ToString("C4");
                    btnEditarMatriz.Enabled = false;
                    break;
                default:
                    btnEditarMatriz.Enabled = false;
                    break;
            }
            CalcularImporte();
        }

        private void nudCantidad_ValueChanged(object sender, EventArgs e) => CalcularImporte();

        private void CalcularImporte()
        {
            if (dgvMatrices.SelectedRows.Count == 0) return;
            decimal costo = 0m;
            var row = dgvMatrices.SelectedRows[0];
            if (row.Tag is MatrixListItemDto lm) costo = lm.CostoDirecto;
            else if (row.Tag is ExternalProjectMatrixOption em) costo = em.CostoDirecto;
            lblImporte.Text = (nudCantidad.Value * costo).ToString("C2");
        }

        // ════════════════════════════════════════════════════════════════════
        // Eventos de botones existentes
        // ════════════════════════════════════════════════════════════════════

        private void dgvMatrices_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnAceptar_Click(sender, e);
        }

        private void dgvMatrices_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && dgvMatrices.CurrentRow?.Index >= 0)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnAceptar_Click(sender, EventArgs.Empty);
            }
        }

        private void btnAceptar_Click(object sender, EventArgs e)
        {
            if (dgvMatrices.SelectedRows.Count == 0)
            {
                MessageBox.Show("Seleccione una matriz de la lista.", "Selección requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (nudCantidad.Value <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a cero.", "Cantidad inválida",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = dgvMatrices.SelectedRows[0];

            if (row.Tag is MatrixListItemDto localMatrix)
            {
                if (_includeAuxiliaries && localMatrix.Tipo != TipoMatriz.APU)
                {
                    MessageBox.Show("Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. Seleccione una matriz de tipo APU.",
                        "Selección no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                MatrizSeleccionada = _context.Matrices.Find(localMatrix.Id);
                Cantidad           = nudCantidad.Value;
                _projectUsageService.RegisterMatrixSelection(_context.DatabasePath, localMatrix.Id);
                DialogResult       = DialogResult.OK;
                Close();
                return;
            }

            if (row.Tag is not ExternalProjectMatrixOption externalMatrix) return;

            if (_includeAuxiliaries && externalMatrix.Tipo != TipoMatriz.APU)
            {
                MessageBox.Show("Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. Seleccione una matriz de tipo APU.",
                    "Selección no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.Equals(_context.DatabasePath, externalMatrix.ProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("El proyecto externo coincide con el proyecto actual.", "Importación",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var preview = _externalMatrixImportService.BuildPreview(
                _context, _proyectoId, externalMatrix.ProjectPath, externalMatrix.MatrixId);
            using var previewDialog = new FormPreviewImportacionMatrices(preview);
            if (previewDialog.ShowDialog(this) != DialogResult.OK || previewDialog.SelectedPolicy == null)
                return;

            try
            {
                var result = _externalMatrixImportService.ImportMatrixTree(
                    _context, _proyectoId, externalMatrix.ProjectPath,
                    externalMatrix.MatrixId, previewDialog.SelectedPolicy.Value);

                MatrizSeleccionada = _context.Matrices.Find(result.RootMatrixId);
                Cantidad           = nudCantidad.Value;
                _projectUsageService.RegisterMatrixSelection(externalMatrix.ProjectPath, externalMatrix.MatrixId);
                _projectUsageService.RegisterMatrixSelection(_context.DatabasePath, result.RootMatrixId);

                MessageBox.Show(
                    "Importación completada.\n\n" +
                    $"Matrices nuevas: {result.ImportedMatrices}\n" +
                    $"Materiales nuevos: {result.ImportedMateriales}\n" +
                    $"Mano de obra nueva: {result.ImportedManoDeObra}\n" +
                    $"Maquinaria nueva: {result.ImportedMaquinaria}\n" +
                    $"Herramientas nuevas: {result.ImportedHerramientas}",
                    "Importación completada", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar la matriz externa:\n{ex.Message}",
                    "Importación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnNuevaMatriz_Click(object sender, EventArgs e)
        {
            using var formAPU = new FormEditarMatriz(_context, _proyectoId);
            if (formAPU.ShowDialog(this) != DialogResult.OK) return;

            CargarMatricesProyectoActual();

            var ultima = _context.Matrices
                .Where(m => m.ProyectoId == _proyectoId)
                .OrderByDescending(m => m.Id)
                .FirstOrDefault();
            if (ultima == null) return;

            foreach (DataGridViewRow r in dgvMatrices.Rows)
            {
                if (Convert.ToInt32(r.Cells["colId"].Value) == ultima.Id)
                {
                    r.Selected = true;
                    dgvMatrices.FirstDisplayedScrollingRowIndex = r.Index;
                    lblStatus.Text = $"Matriz '{ultima.Clave}' creada y seleccionada.";
                    break;
                }
            }
        }

        private void btnEditarMatriz_Click(object sender, EventArgs e)
        {
            if (dgvMatrices.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecciona una matriz para editar.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var row      = dgvMatrices.SelectedRows[0];
            var matrizId = Convert.ToInt32(row.Cells["colId"].Value);
            var matriz   = _context.Matrices.Find(matrizId);
            if (matriz == null) return;

            using var form = new FormEditarMatriz(_context, _proyectoId, matriz);
            if (form.ShowDialog(this) != DialogResult.OK) return;

            CargarMatricesProyectoActual();
            try
            {
                foreach (DataGridViewRow r in dgvMatrices.Rows)
                {
                    if (Convert.ToInt32(r.Cells["colId"].Value) != matrizId) continue;
                    r.Selected = true;
                    foreach (DataGridViewColumn col in dgvMatrices.Columns)
                    {
                        if (!col.Visible) continue;
                        dgvMatrices.CurrentCell = r.Cells[col.Index];
                        break;
                    }
                    if (r.Index >= 0 && r.Index < dgvMatrices.Rows.Count)
                        dgvMatrices.FirstDisplayedScrollingRowIndex = r.Index;
                    break;
                }
            }
            catch (InvalidOperationException) { /* celda invisible */ }
            lblStatus.Text = $"Matriz '{matriz.Clave}' actualizada.";
        }

        private void btnProyectoActual_Click(object sender, EventArgs e)
        {
            _searchScopeTag = ScopeCurrentTag;
            SelectScopeInCombo();
            CargarMatricesProyectoActual();
        }

        // ════════════════════════════════════════════════════════════════════
        // Persistencia de contexto y cierre
        // ════════════════════════════════════════════════════════════════════

        private void SaveSelectorContext()
        {
            var mode = _searchScopeTag switch
            {
                ScopeCurrentTag => "CurrentOnly",
                ScopeRecentTag => "RecentOnly",
                ScopeFavoritesTag => "FavoritesOnly",
                _ when !string.IsNullOrWhiteSpace(_searchScopeTag)
                     && _searchScopeTag != ScopeAllTag
                     && _searchScopeTag != ScopeCurrentTag
                     && _searchScopeTag != ScopeRecentTag
                     && _searchScopeTag != ScopeFavoritesTag => _searchScopeTag,
                _ => "All"
            };
            var externalForContext = mode.EndsWith("Only", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "All", StringComparison.OrdinalIgnoreCase)
                ? _externalProjectPath
                : _searchScopeTag;
            _selectorContextService.Save(SelectorKey, externalForContext, txtBuscar.Text, mode);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Cancelar búsqueda pendiente y liberar el timer
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Dispose();
            _searchDebounceTimer = null;

            SaveSelectorContext();
            base.OnFormClosing(e);
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        private TipoMatriz? GetSelectedTipoFiltro()
        {
            if (!_includeAuxiliaries || _cboTipo.SelectedItem is not TipoFiltroOption item)
                return null;
            return item.Tipo;
        }

        private static string GetTipoBadge(TipoMatriz tipo) => tipo switch
        {
            TipoMatriz.APU       => "APU",
            TipoMatriz.Basico    => "BÁS",
            TipoMatriz.Cuadrilla => "CUAD",
            _                    => tipo.ToString().ToUpperInvariant()
        };

        private static string GetTipoEtiquetaCorta(TipoMatriz tipo) => tipo switch
        {
            TipoMatriz.APU       => "APU",
            TipoMatriz.Basico    => "Básico",
            TipoMatriz.Cuadrilla => "Cuadrilla",
            _                    => tipo.ToString()
        };

        private string GetCurrentExternalProjectName()
        {
            if (string.IsNullOrWhiteSpace(_externalProjectPath)) return string.Empty;

            // Intentar desde el combo si existe un ítem del proyecto
            foreach (var comboItem in _cboProyecto.Items.OfType<ProyectoComboItem>())
            {
                if (comboItem.Kind == ProyectoComboKind.Project
                    && string.Equals(comboItem.Value, SelectorUiDefaults.NormalizePath(_externalProjectPath), StringComparison.OrdinalIgnoreCase))
                {
                    var parts = comboItem.DisplayText.Split(' ');
                    return parts.Length > 1 ? string.Join(" ", parts.Skip(1)).Trim() : comboItem.DisplayText.Trim();
                }
            }

            // Desde el grid
            var cell = dgvMatrices.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Visible)
                .Select(r => r.Cells["colProyecto"].Value?.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            return cell ?? Path.GetFileNameWithoutExtension(_externalProjectPath);
        }

        // ════════════════════════════════════════════════════════════════════
        // Clases internas
        // ════════════════════════════════════════════════════════════════════

        private sealed class TipoFiltroOption
        {
            public TipoFiltroOption(string texto, TipoMatriz? tipo) { Texto = texto; Tipo = tipo; }
            public string Texto { get; }
            public TipoMatriz? Tipo { get; }
            public override string ToString() => Texto;
        }

        private enum ProyectoComboKind
        {
            Scope,
            Project,
            Action,
            Separator
        }

        private sealed class ProyectoComboItem
        {
            public ProyectoComboItem(string displayText, string value, ProyectoComboKind kind)
            {
                DisplayText = displayText;
                Value = value;
                Kind = kind;
            }
            public string DisplayText { get; }
            public string Value { get; }
            public ProyectoComboKind Kind { get; }
            public override string ToString() => DisplayText;
        }
    }
}
