using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SOPRO.Application.DTOs.Insumos;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    public partial class FormSeleccionarInsumo : Form
    {
        private const string ScopeAllTag = SelectorUiDefaults.ScopeAllTag;
        private const string ScopeCurrentTag = SelectorUiDefaults.ScopeCurrentTag;
        private const string ScopeRecentTag = SelectorUiDefaults.ScopeRecentTag;
        private const string ScopeFavoritesTag = SelectorUiDefaults.ScopeFavoritesTag;
        private const string ExaminarTag = SelectorUiDefaults.ExaminarTag;
        private const int MaxProjectsInCombo = SelectorUiDefaults.MaxProjectsInCombo;
        private const int MaxRowsInGrid = SelectorUiDefaults.MaxRowsInGrid;

        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly TipoComponenteMatriz _tipoComponente;
        private readonly ExternalInsumoImportService _externalInsumoImportService = new();
        private readonly ProjectIndexService _projectIndexService;
        private readonly InsumoSearchService _insumoSearchService;
        private readonly ProjectUsageService _projectUsageService;
        private readonly string _projectNameActual;
        private readonly Dictionary<string, object> _selectedItems = new(StringComparer.OrdinalIgnoreCase);

        private string? _externalProjectPath;
        private bool _cboProyectoLoading;
        private bool _showingSearchResults;
        private bool _suppressPersistentSelectionSync;
        private bool _skipSelectionSyncOnce;
        private string? _pendingCtrlToggleRemoveKey;
        private int _pendingCtrlToggleRemoveRowIndex = -1;
        private string _searchScopeTag = ScopeAllTag;
        private System.Windows.Forms.Timer? _searchDebounceTimer;

        private static readonly Color AccumulatedRowBackColor = Color.FromArgb(232, 245, 233);
        private static readonly Color AccumulatedRowSelectionBackColor = Color.FromArgb(200, 230, 201);

        public List<object> InsumosSeleccionados { get; private set; }
        public List<ComponenteMatriz> ComponentesSeleccionados { get; private set; }
        public decimal Cantidad { get; private set; }
        public decimal PrecioUnitario { get; private set; }
        public decimal Importe { get; private set; }

        public FormSeleccionarInsumo(SOPROContext context, int proyectoId, TipoComponenteMatriz tipoComponente)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _tipoComponente = tipoComponente;
            var workspaceService = new ProjectWorkspaceService();
            _projectIndexService = new ProjectIndexService(workspaceService);
            _projectUsageService = new ProjectUsageService(workspaceService);
            _insumoSearchService = new InsumoSearchService(_projectIndexService, _projectUsageService, workspaceService);
            _projectNameActual = _context.Proyectos.Find(_proyectoId)?.Nombre ?? Path.GetFileNameWithoutExtension(_context.DatabasePath);

            InsumosSeleccionados = new List<object>();
            ComponentesSeleccionados = new List<ComponenteMatriz>();

            InitializeComponent();
            ApplyRenderOptimizations();
            ConfigurarFormulario();
            InitializeFavoritosContextMenu();
            PopulateProyectoCombo();
            dgvInsumos.SelectionChanged += dgvInsumos_SelectionChanged;
            dgvInsumos.KeyDown += dgvInsumos_KeyDown;
            dgvInsumos.CellMouseDown += dgvInsumos_CellMouseDown;
            dgvInsumos.CellMouseUp += dgvInsumos_CellMouseUp;
            CargarInsumosActuales();
        }

        private void ApplyRenderOptimizations()
        {
            FormRenderHelper.OptimizeForGridRendering(this);
            EnableDoubleBuffer(dgvInsumos);
            EnableDoubleBuffer(panelTop);
            EnableDoubleBuffer(panelBottom);
            EnableDoubleBuffer(panelFiltroMO);
        }

        private static void EnableDoubleBuffer(Control control)
        {
            if (control == null) return;
            var prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(control, true, null);
        }

        private void RunGridUpdate(Action action)
        {
            SuspendLayout();
            dgvInsumos.SuspendLayout();
            panelBottom.SuspendLayout();
            using (GridRedrawHelper.Suspend(dgvInsumos))
            {
                try { action(); }
                finally
                {
                    panelBottom.ResumeLayout();
                    dgvInsumos.ResumeLayout();
                    ResumeLayout(true);
                }
            }
        }

        private void ConfigurarFormulario()
        {
            Text = $"Seleccionar {ObtenerNombreTipo()}";
            lblTitulo.Text = $"SELECCIONAR {ObtenerNombreTipo().ToUpper()}";
            panelFiltroMO.Visible = (_tipoComponente == TipoComponenteMatriz.ManoDeObra);
        }

        private string ObtenerNombreTipo()
        {
            return _tipoComponente switch
            {
                TipoComponenteMatriz.Material => "Material",
                TipoComponenteMatriz.ManoDeObra => "Mano de Obra",
                TipoComponenteMatriz.Maquinaria => "Maquinaria",
                TipoComponenteMatriz.Auxiliar => "Básico",
                TipoComponenteMatriz.Herramienta => "Herramienta",
                _ => "Insumo"
            };
        }

        private bool IncluirManoDeObraIndividual => _tipoComponente != TipoComponenteMatriz.ManoDeObra || rbMOTodos.Checked || rbMOIndividual.Checked;
        private bool IncluirCuadrillas => _tipoComponente == TipoComponenteMatriz.ManoDeObra && (rbMOTodos.Checked || rbMOCuadrillas.Checked);

        private void PopulateProyectoCombo()
        {
            _cboProyectoLoading = true;
            try
            {
                var currentPath = SelectorUiDefaults.NormalizePath(_context.DatabasePath);
                var favorites = _projectIndexService.GetFavoriteProjects().Where(p => File.Exists(p.FilePath)).ToList();
                var recents = _projectIndexService.GetRecentProjects(MaxProjectsInCombo).Where(p => File.Exists(p.FilePath)).ToList();

                cboProyecto.Items.Clear();
                cboProyecto.Items.Add(new ProyectoComboItem("🔎 Todos", ScopeAllTag, ProyectoComboKind.Scope));
                cboProyecto.Items.Add(new ProyectoComboItem("📁 Proyecto actual", ScopeCurrentTag, ProyectoComboKind.Scope));
                cboProyecto.Items.Add(new ProyectoComboItem("🕘 Recientes", ScopeRecentTag, ProyectoComboKind.Scope));
                cboProyecto.Items.Add(new ProyectoComboItem("⭐ Favoritos", ScopeFavoritesTag, ProyectoComboKind.Scope));
                cboProyecto.Items.Add(new ProyectoComboItem("────────────────", string.Empty, ProyectoComboKind.Separator));

                var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in favorites)
                {
                    var path = SelectorUiDefaults.NormalizePath(p.FilePath);
                    if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!added.Add(path)) continue;
                    cboProyecto.Items.Add(new ProyectoComboItem($"★ {p.Name}", path, ProyectoComboKind.Project));
                }
                foreach (var p in recents)
                {
                    var path = SelectorUiDefaults.NormalizePath(p.FilePath);
                    if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!added.Add(path)) continue;
                    cboProyecto.Items.Add(new ProyectoComboItem($"🕘 {p.Name}", path, ProyectoComboKind.Project));
                }
                cboProyecto.Items.Add(new ProyectoComboItem("────────────────", string.Empty, ProyectoComboKind.Separator));
                cboProyecto.Items.Add(new ProyectoComboItem("📁 Examinar archivo...", ExaminarTag, ProyectoComboKind.Action));
                SelectScopeInCombo();
            }
            finally { _cboProyectoLoading = false; }
        }

        private void SelectScopeInCombo()
        {
            _cboProyectoLoading = true;
            try
            {
                for (int i = 0; i < cboProyecto.Items.Count; i++)
                {
                    if (cboProyecto.Items[i] is not ProyectoComboItem item || item.Kind == ProyectoComboKind.Separator) continue;
                    if (string.Equals(item.Value, _searchScopeTag, StringComparison.OrdinalIgnoreCase))
                    {
                        cboProyecto.SelectedIndex = i;
                        return;
                    }
                }
                cboProyecto.SelectedIndex = 0;
            }
            finally { _cboProyectoLoading = false; }
        }

        private void InitializeFavoritosContextMenu()
        {
            ctxProyectoFavorito.Opening += ctxProyectoFavorito_Opening;
            mnuToggleFavorito.Click += mnuToggleFavorito_Click;
            dgvInsumos.ContextMenuStrip = ctxProyectoFavorito;
            dgvInsumos.MouseDown += dgvInsumos_MouseDown;
        }

        private void dgvInsumos_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = dgvInsumos.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0) return;
            dgvInsumos.ClearSelection();
            var row = dgvInsumos.Rows[hit.RowIndex];
            row.Selected = true;
            if (hit.ColumnIndex >= 0)
                dgvInsumos.CurrentCell = row.Cells[hit.ColumnIndex];
        }

        private void ctxProyectoFavorito_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var row = dgvInsumos.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (row?.Tag is not ExternalProjectInsumoOption ext)
            {
                e.Cancel = true;
                return;
            }
            var isFavorite = _projectIndexService.IsFavorite(ext.ProjectPath);
            mnuToggleFavorito.Text = isFavorite ? "Quitar proyecto origen de favoritos" : "Marcar proyecto origen como favorito";
        }

        private void mnuToggleFavorito_Click(object? sender, EventArgs e)
        {
            var row = dgvInsumos.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (row?.Tag is not ExternalProjectInsumoOption ext) return;
            var isFavorite = _projectIndexService.IsFavorite(ext.ProjectPath);
            _projectIndexService.SetFavorite(ext.ProjectPath, !isFavorite, ext.ProjectName);
            PopulateProyectoCombo();
            if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
                EjecutarBusquedaTransversal(txtBuscar.Text.Trim());
            else
                lblStatus.Text = !isFavorite ? $"'{ext.ProjectName}' marcado como favorito." : $"'{ext.ProjectName}' removido de favoritos.";
        }

        private void CargarInsumosActuales()
        {
            try
            {
                RunGridUpdate(() =>
                {
                    _showingSearchResults = false;
                    _externalProjectPath = null;
                    dgvInsumos.Rows.Clear();
                    var items = InsumoSelectionService.GetSelectableInsumos(_context, _proyectoId, _tipoComponente, Filtro(), IncluirManoDeObraIndividual, IncluirCuadrillas);
                    var shown = items.Take(MaxRowsInGrid).ToList();
                    var fechaRef = SelectorUiDefaults.GetProjectReferenceDate(_context.DatabasePath);
                    foreach (var item in shown)
                    {
                        AddRow(item, true, _projectNameActual, fechaRef);
                    }
                    RestoreSelections();
                    _projectIndexService.RegisterProjectOpened(_context.DatabasePath, _projectNameActual);
                    lblStatus.Text = SelectorUiDefaults.BuildBaseLoadStatus(items.Count, shown.Count, ObtenerNombreTipo().ToLowerInvariant() + "s", "proyecto actual");
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar insumos:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarInsumosExternos(string projectPath)
        {
            try
            {
                RunGridUpdate(() =>
                {
                    _showingSearchResults = false;
                    dgvInsumos.Rows.Clear();
                    var result = _externalInsumoImportService.LoadExternalInsumos(projectPath, _tipoComponente, Filtro(), IncluirManoDeObraIndividual, IncluirCuadrillas);
                    var shown = result.Items.Take(MaxRowsInGrid).ToList();
                    var fechaRef = SelectorUiDefaults.GetProjectReferenceDate(result.ProjectPath);
                    foreach (var item in shown)
                        AddRow(item, false, result.ProjectName, fechaRef);
                    RestoreSelections();
                    _externalProjectPath = result.ProjectPath;
                    _projectIndexService.RegisterProjectOpened(result.ProjectPath, result.ProjectName);
                    try { _insumoSearchService.RebuildProjectInsumoIndex(result.ProjectPath); } catch { }
                    lblStatus.Text = SelectorUiDefaults.BuildBaseLoadStatus(result.Items.Count, shown.Count, "insumos", $"'{result.ProjectName}'");
                    _searchScopeTag = SelectorUiDefaults.NormalizePath(result.ProjectPath);
                    PopulateProyectoCombo();
                    SelectScopeInCombo();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar insumos externos:\n{ex.Message}", "Proyecto externo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddRow(SelectableInsumoDto item, bool esActual, string projectName, DateTime? fechaRef)
        {
            var idx = dgvInsumos.Rows.Add(item.Id, item.Clave, item.Descripcion, item.Unidad, item.PrecioMostrado, esActual ? "Actual" : "Externo", projectName, fechaRef?.ToString("dd/MM/yyyy") ?? string.Empty);
            dgvInsumos.Rows[idx].Tag = item;
            ApplyAccumulatedState(dgvInsumos.Rows[idx], reselectVisibleRow: true);
        }

        private void AddRow(ExternalProjectInsumoOption item, bool esActual, string projectName, DateTime? fechaRef)
        {
            var idx = dgvInsumos.Rows.Add(item.ItemId, item.Clave, item.Descripcion, item.Unidad, item.PrecioMostrado, esActual ? "Actual" : "Externo", projectName, fechaRef?.ToString("dd/MM/yyyy") ?? string.Empty);
            dgvInsumos.Rows[idx].Tag = item;
            ApplyAccumulatedState(dgvInsumos.Rows[idx], reselectVisibleRow: true);
        }

        private void AddRow(InsumoSearchResultDto item)
        {
            object tagObj = item.EsActual
                ? new SelectableInsumoDto
                {
                    Id = item.ElementoId,
                    TipoComponente = item.TipoComponente,
                    Tag = item.Tag ?? string.Empty,
                    Clave = item.Clave,
                    Descripcion = item.Descripcion,
                    Unidad = item.Unidad,
                    PrecioUnitario = item.PrecioUnitario,
                    PrecioMostrado = item.PrecioMostrado
                }
                : new ExternalProjectInsumoOption
                {
                    ItemId = item.ElementoId,
                    ProjectName = item.NombreProyecto,
                    ProjectPath = item.RutaProyecto,
                    TipoComponente = item.TipoComponente,
                    Tag = item.Tag,
                    Clave = item.Clave,
                    Descripcion = item.Descripcion,
                    Unidad = item.Unidad,
                    PrecioUnitario = item.PrecioUnitario,
                    PrecioMostrado = item.PrecioMostrado
                };

            var idx = dgvInsumos.Rows.Add(item.ElementoId, item.Clave, item.Descripcion, item.Unidad, item.PrecioMostrado, item.EsActual ? "Actual" : "Externo", item.NombreProyecto, item.FechaReferencia?.ToString("dd/MM/yyyy") ?? string.Empty);
            dgvInsumos.Rows[idx].Tag = tagObj;
            ApplyAccumulatedState(dgvInsumos.Rows[idx], reselectVisibleRow: true);
        }

        private void RestoreSelections()
        {
            _suppressPersistentSelectionSync = true;
            try
            {
                foreach (DataGridViewRow row in dgvInsumos.Rows)
                    ApplyAccumulatedState(row, reselectVisibleRow: true);
            }
            finally
            {
                _suppressPersistentSelectionSync = false;
            }
        }

        private void ApplyAccumulatedState(DataGridViewRow row, bool reselectVisibleRow)
        {
            var key = BuildSelectionKey(row.Tag);
            var isAccumulated = key != null && _selectedItems.ContainsKey(key);

            if (isAccumulated && key != null)
                _selectedItems[key] = row.Tag!;

            row.DefaultCellStyle.BackColor = isAccumulated ? AccumulatedRowBackColor : Color.White;
            row.DefaultCellStyle.SelectionBackColor = isAccumulated ? AccumulatedRowSelectionBackColor : SystemColors.Highlight;
            row.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;

            // La selección persistente ya se comunica con color de fondo suave.
            // No debemos forzar row.Selected porque eso estorba la selección múltiple
            // nativa con Ctrl/Shift y hace parecer que todo ya quedó "activo".
            if (reselectVisibleRow)
                row.Selected = false;
        }

        private void RefreshAccumulatedSelectionFromGrid()
        {
            if (_suppressPersistentSelectionSync) return;

            var accumulateGesture = dgvInsumos.SelectedRows.Count > 1 ||
                                    (Control.ModifierKeys & (Keys.Control | Keys.Shift)) != Keys.None;

            if (accumulateGesture)
            {
                foreach (DataGridViewRow row in dgvInsumos.SelectedRows)
                {
                    var key = BuildSelectionKey(row.Tag);
                    if (key == null || row.Tag == null) continue;
                    _selectedItems[key] = row.Tag;
                }
            }

            _suppressPersistentSelectionSync = true;
            try
            {
                foreach (DataGridViewRow row in dgvInsumos.Rows)
                    ApplyAccumulatedState(row, reselectVisibleRow: false);
            }
            finally
            {
                _suppressPersistentSelectionSync = false;
            }
        }

        private string? Filtro()
        {
            var t = txtBuscar?.Text?.Trim();
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }

        private void dgvInsumos_SelectionChanged(object? sender, EventArgs e)
        {
            if (_skipSelectionSyncOnce)
            {
                _skipSelectionSyncOnce = false;
                return;
            }

            RefreshAccumulatedSelectionFromGrid();
        }

        private void dgvInsumos_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.Button != MouseButtons.Left) return;

            var isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            if (!isCtrl) return;

            var row = dgvInsumos.Rows[e.RowIndex];
            var key = BuildSelectionKey(row.Tag);
            if (string.IsNullOrWhiteSpace(key)) return;

            if (_selectedItems.ContainsKey(key))
            {
                _pendingCtrlToggleRemoveKey = key;
                _pendingCtrlToggleRemoveRowIndex = e.RowIndex;
                _skipSelectionSyncOnce = true;
            }
        }

        private void dgvInsumos_CellMouseUp(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_pendingCtrlToggleRemoveRowIndex != e.RowIndex || string.IsNullOrWhiteSpace(_pendingCtrlToggleRemoveKey))
                return;

            var row = dgvInsumos.Rows[e.RowIndex];

            _selectedItems.Remove(_pendingCtrlToggleRemoveKey);

            _suppressPersistentSelectionSync = true;
            try
            {
                row.Selected = false;
                ApplyAccumulatedState(row, reselectVisibleRow: false);
            }
            finally
            {
                _suppressPersistentSelectionSync = false;
            }

            _pendingCtrlToggleRemoveKey = null;
            _pendingCtrlToggleRemoveRowIndex = -1;
        }

        private void dgvInsumos_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            btnAceptar.PerformClick();
        }

        private static string? BuildSelectionKey(object? tag)
        {
            return tag switch
            {
                SelectableInsumoDto dto => $"LOCAL|{dto.TipoComponente}|{dto.Id}|{dto.Tag?.Trim().ToUpperInvariant()}",
                ExternalProjectInsumoOption ext => $"EXT|{NormalizeStaticPath(ext.ProjectPath)}|{ext.TipoComponente}|{ext.ItemId}|{ext.Tag?.Trim().ToUpperInvariant()}",
                _ => null
            };
        }

        private static string NormalizeStaticPath(string path) => Path.GetFullPath(path.Trim());

        private void txtBuscar_TextChanged(object sender, EventArgs e)
        {
            if (_searchDebounceTimer == null)
            {
                _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = SelectorUiDefaults.SearchDebounceMs };
                _searchDebounceTimer.Tick += (_, __) =>
                {
                    _searchDebounceTimer.Stop();
                    ApplyFilterToGrid();
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
                ShowCurrentProjectBase("Mostrando insumos del proyecto actual.");
                return;
            }
            if (_searchScopeTag == ScopeAllTag)
            {
                ShowCurrentProjectBase(SelectorUiDefaults.BuildAllScopePrompt("insumos"));
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
            CargarInsumosExternos(_searchScopeTag);
        }

        private void ShowCurrentProjectBase(string statusMessage)
        {
            var hasCurrentBaseLoaded = !_showingSearchResults && string.IsNullOrWhiteSpace(_externalProjectPath) && dgvInsumos.Rows.Count > 0;
            if (!hasCurrentBaseLoaded) CargarInsumosActuales();
            lblStatus.Text = statusMessage;
        }

        private void ClearGridForEmptyScope(string statusMessage)
        {
            RunGridUpdate(() =>
            {
                _showingSearchResults = false;
                _externalProjectPath = null;
                dgvInsumos.Rows.Clear();
                dgvInsumos.ClearSelection();
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
                IReadOnlyList<InsumoSearchResultDto> results;
                if (_searchScopeTag != ScopeAllTag && _searchScopeTag != ScopeCurrentTag && _searchScopeTag != ScopeRecentTag && _searchScopeTag != ScopeFavoritesTag)
                {
                    results = _insumoSearchService.SearchInsumos(_context, _proyectoId, _tipoComponente, busqueda, false, false, false, MaxRowsInGrid, new[] { _searchScopeTag }, IncluirManoDeObraIndividual, IncluirCuadrillas);
                }
                else
                {
                    results = _insumoSearchService.SearchInsumos(_context, _proyectoId, _tipoComponente, busqueda, includeCurrentProject, includeRecentProjects, includeFavoriteProjects, MaxRowsInGrid, null, IncluirManoDeObraIndividual, IncluirCuadrillas);
                }

                RunGridUpdate(() =>
                {
                    _showingSearchResults = true;
                    dgvInsumos.Rows.Clear();
                    foreach (var item in results) AddRow(item);
                    lblStatus.Text = results.Count > 0 ? $"{results.Count} resultados para '{busqueda}'." : $"Sin resultados para '{busqueda}'.";
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en búsqueda transversal de insumos:\n{ex.Message}", "Buscar insumos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cboProyecto_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cboProyectoLoading) return;
            if (cboProyecto.SelectedItem is not ProyectoComboItem item) return;
            if (item.Kind == ProyectoComboKind.Separator) return;
            if (item.Kind == ProyectoComboKind.Action && item.Value == ExaminarTag)
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Seleccionar proyecto SOPRO",
                    Filter = "Bases de proyecto SOPRO (*.db;*.sopro)|*.db;*.sopro|Todos los archivos (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _searchScopeTag = SelectorUiDefaults.NormalizePath(dialog.FileName);
                    PopulateProyectoCombo();
                    SelectScopeInCombo();
                    ApplyFilterToGrid();
                }
                else
                {
                    SelectScopeInCombo();
                }
                return;
            }

            _searchScopeTag = item.Value;
            ApplyFilterToGrid();
        }


        private Dictionary<string, object> ObtenerSeleccionEfectiva()
        {
            var effective = new Dictionary<string, object>(_selectedItems, StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in dgvInsumos.SelectedRows)
            {
                var key = BuildSelectionKey(row.Tag);
                if (key != null && row.Tag != null)
                    effective[key] = row.Tag;
            }

            return effective;
        }

        private List<ExternalProjectInsumoOption> ObtenerExternosSeleccionados()
        {
            return _selectedItems.Values.OfType<ExternalProjectInsumoOption>().ToList();
        }

        private List<SelectableInsumoDto> ObtenerInsumosLocalesSeleccionados()
        {
            return _selectedItems.Values.OfType<SelectableInsumoDto>().ToList();
        }

        private void btnAceptar_Click(object sender, EventArgs e)
        {
            if (nudCantidad.Value <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a cero.", "Cantidad Inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var seleccionEfectiva = ObtenerSeleccionEfectiva();
            var locales = seleccionEfectiva.Values.OfType<SelectableInsumoDto>().ToList();
            var externos = seleccionEfectiva.Values.OfType<ExternalProjectInsumoOption>().ToList();
            if (locales.Count == 0 && externos.Count == 0)
            {
                MessageBox.Show("Seleccione al menos un insumo de la lista.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ComponentesSeleccionados = new List<ComponenteMatriz>();

            if (locales.Count > 0)
            {
                var ids = new List<int>();
                var tags = new Dictionary<int, string>();
                foreach (var dto in locales)
                {
                    ids.Add(dto.Id);
                    if (!string.IsNullOrWhiteSpace(dto.Tag)) tags[dto.Id] = dto.Tag;
                }
                ComponentesSeleccionados.AddRange(InsumoSelectionService.BuildSelectedComponents(_context, _tipoComponente, ids, tags, nudCantidad.Value));
            }

            if (externos.Count > 0)
            {
                foreach (var grupo in externos.GroupBy(x => SelectorUiDefaults.NormalizePath(x.ProjectPath), StringComparer.OrdinalIgnoreCase))
                {
                    var grupoItems = grupo.ToList();
                    var preview = _externalInsumoImportService.BuildPreview(_context, _proyectoId, grupo.Key, grupoItems);
                    ExternalMatrixImportConflictPolicy policy;
                    using (var previewForm = new FormPreviewImportacionInsumos(preview))
                    {
                        if (previewForm.ShowDialog(this) != DialogResult.OK || !previewForm.SelectedPolicy.HasValue)
                            return;
                        policy = previewForm.SelectedPolicy.Value;
                    }
                    var importResult = _externalInsumoImportService.ImportSelected(_context, _proyectoId, grupo.Key, grupoItems, policy, nudCantidad.Value);
                    ComponentesSeleccionados.AddRange(importResult.ImportedComponents);
                }
            }

            RegisterUsageForSelection();

            InsumosSeleccionados.Clear();
            foreach (var componente in ComponentesSeleccionados)
            {
                if (componente.Material != null) InsumosSeleccionados.Add(componente.Material);
                else if (componente.ManoDeObra != null) InsumosSeleccionados.Add(componente.ManoDeObra);
                else if (componente.Maquinaria != null) InsumosSeleccionados.Add(componente.Maquinaria);
                else if (componente.Auxiliar != null) InsumosSeleccionados.Add(componente.Auxiliar);
                else if (componente.Herramienta != null) InsumosSeleccionados.Add(componente.Herramienta);
            }

            if (ComponentesSeleccionados.Count == 0)
            {
                MessageBox.Show("Marque al menos un insumo de la lista.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Cantidad = nudCantidad.Value;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void RegisterUsageForSelection()
        {
            foreach (var item in _selectedItems.Values)
            {
                switch (item)
                {
                    case SelectableInsumoDto dto:
                        _projectUsageService.RegisterInsumoSelection(_context.DatabasePath, dto.TipoComponente, dto.Id, dto.Tag);
                        break;
                    case ExternalProjectInsumoOption ext:
                        _projectUsageService.RegisterInsumoSelection(ext.ProjectPath, ext.TipoComponente, ext.ItemId, ext.Tag);
                        break;
                }
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnNuevoInsumo_Click(object sender, EventArgs e)
        {
            int? nuevoId = null;
            switch (_tipoComponente)
            {
                case TipoComponenteMatriz.Material:
                    using (var form = new FormEditarMaterial(_context, _proyectoId))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK)
                            nuevoId = _context.Materiales.Where(m => m.ProyectoId == _proyectoId).OrderByDescending(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault();
                    }
                    break;
                case TipoComponenteMatriz.ManoDeObra:
                    var proyFSR = _context.Proyectos.Find(_proyectoId);
                    using (var form = new FormEditarManoObra(_context, _proyectoId, proyecto: proyFSR))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK)
                            nuevoId = _context.ManoDeObra.Where(m => m.ProyectoId == _proyectoId).OrderByDescending(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault();
                    }
                    break;
                case TipoComponenteMatriz.Maquinaria:
                    using (var form = new FormEditarMaquinaria(_context, _proyectoId))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK)
                            nuevoId = _context.Maquinaria.Where(m => m.ProyectoId == _proyectoId).OrderByDescending(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault();
                    }
                    break;
                case TipoComponenteMatriz.Auxiliar:
                    using (var form = new FormEditarMatriz(_context, _proyectoId))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK)
                            nuevoId = _context.Matrices.Where(m => m.ProyectoId == _proyectoId && m.Tipo == TipoMatriz.Basico).OrderByDescending(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault();
                    }
                    break;
                case TipoComponenteMatriz.Herramienta:
                    using (var form = new FormEditarHerramienta(_context, _proyectoId))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK)
                            nuevoId = _context.Herramientas.Where(h => h.ProyectoId == _proyectoId).OrderByDescending(h => h.Id).Select(h => (int?)h.Id).FirstOrDefault();
                    }
                    break;
            }

            if (nuevoId.HasValue)
            {
                txtBuscar.Text = string.Empty;
                _searchScopeTag = ScopeCurrentTag;
                SelectScopeInCombo();
                CargarInsumosActuales();
                foreach (DataGridViewRow row in dgvInsumos.Rows)
                {
                    if (Convert.ToInt32(row.Cells["colId"].Value) == nuevoId.Value)
                    {
                        row.Selected = true;
                        dgvInsumos.FirstDisplayedScrollingRowIndex = row.Index;
                        break;
                    }
                }
            }
        }

        private void rbFiltroMO_CheckedChanged(object sender, EventArgs e)
        {
            if (_tipoComponente != TipoComponenteMatriz.ManoDeObra) return;
            ApplyFilterToGrid();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_searchDebounceTimer != null)
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Dispose();
                _searchDebounceTimer = null;
            }
            base.OnFormClosing(e);
        }

        private sealed class ProyectoComboItem
        {
            public ProyectoComboItem(string display, string value, ProyectoComboKind kind)
            {
                Display = display;
                Value = value;
                Kind = kind;
            }
            public string Display { get; }
            public string Value { get; }
            public ProyectoComboKind Kind { get; }
            public override string ToString() => Display;
        }

        private enum ProyectoComboKind
        {
            Scope,
            Project,
            Separator,
            Action
        }
    }
}
