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
        }

        // ════════════════════════════════════════════════════════════════════
        // Inicialización
        // ════════════════════════════════════════════════════════════════════


        // ════════════════════════════════════════════════════════════════════
        // ComboBox de proyecto unificado
        // ════════════════════════════════════════════════════════════════════


        // ════════════════════════════════════════════════════════════════════
        // Filtro de grid
        // ════════════════════════════════════════════════════════════════════


        // ════════════════════════════════════════════════════════════════════
        // Selección y cálculo
        // ════════════════════════════════════════════════════════════════════


        // ════════════════════════════════════════════════════════════════════
        // Eventos de botones existentes
        // ════════════════════════════════════════════════════════════════════


        // ════════════════════════════════════════════════════════════════════
        // Persistencia de contexto y cierre
        // ════════════════════════════════════════════════════════════════════

    }
}
