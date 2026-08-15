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
using SOPRO.Data.Factories;
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
        private readonly ExternalInsumoImportService _externalInsumoImportService = new(new ProjectDbContextFactory());
        private readonly ProjectIndexService _projectIndexService;
        private readonly InsumoSearchService _insumoSearchService;
        private readonly ProjectUsageService _projectUsageService;
        private readonly string _projectNameActual;
        private readonly Dictionary<string, object> _selectedItems = new(StringComparer.OrdinalIgnoreCase);

        private string? _externalProjectPath;
        private bool _cboProyectoLoading;
        private bool _showingSearchResults;
        private bool _suppressPersistentSelectionSync;
        private string _searchScopeTag = ScopeAllTag;
        private System.Windows.Forms.Timer? _searchDebounceTimer;

        private static readonly Color AccumulatedRowBackColor = SystemColors.Highlight;
        private static readonly Color AccumulatedRowSelectionBackColor = SystemColors.Highlight;

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
            _insumoSearchService = new InsumoSearchService(_projectIndexService, _projectUsageService, workspaceService, new ProjectDbContextFactory());
            _projectNameActual = _context.Proyectos.Find(_proyectoId)?.Nombre ?? Path.GetFileNameWithoutExtension(_context.DatabasePath);

            InsumosSeleccionados = new List<object>();
            ComponentesSeleccionados = new List<ComponenteMatriz>();

            InitializeComponent();
            ApplyRenderOptimizations();
            ConfigurarFormulario();
            InitializeFavoritosContextMenu();
            PopulateProyectoCombo();
            KeyPreview = true;
            Shown += FormSeleccionarInsumo_Shown;
            KeyDown += FormSeleccionarInsumo_KeyDown;
            txtBuscar.KeyDown += txtBuscar_KeyDown;
            dgvInsumos.SelectionChanged += dgvInsumos_SelectionChanged;
            dgvInsumos.KeyDown += dgvInsumos_KeyDown;
            dgvInsumos.KeyPress += dgvInsumos_KeyPress;
            dgvInsumos.CellDoubleClick += dgvInsumos_CellDoubleClick;
            CargarInsumosActuales();
        }


        private bool IncluirManoDeObraIndividual => _tipoComponente != TipoComponenteMatriz.ManoDeObra || rbMOTodos.Checked || rbMOIndividual.Checked;
        private bool IncluirCuadrillas => _tipoComponente == TipoComponenteMatriz.ManoDeObra && (rbMOTodos.Checked || rbMOCuadrillas.Checked);







    }
}
