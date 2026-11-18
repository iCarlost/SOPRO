using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Contracts;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Data.Factories;

namespace SOPRO.WinForms.Forms
{
    public partial class FormCatalogoManoObra : Form, IGridFormato, IBusquedaGrid, IRecalculable, IConsolidacionInsumos, IGridSeleccionFormato
    {
        public static event EventHandler InsumosModificados;

        private readonly SOPROContext _context;
        private readonly Repository<ManoDeObra> _repository;
        private readonly CatalogLoadService _catalogLoadService = new();
        private readonly int? _proyectoId;
        private readonly ProjectSessionInfo _sessionInfo;
        private readonly IProjectDbContextFactory _factory = new ProjectDbContextFactory();
        private readonly PreviewCatalogBatchDeletion _previewCatalogBatchDeletion;
        private readonly DeleteCatalogBatch _deleteCatalogBatch;
        private readonly IDiagnosticsSink? _diagnosticsSink;
        private readonly string? _productVersion;
        private ManoDeObra _manoDeObraSeleccionada;
        private Proyecto ProyectoActual => _proyectoId.HasValue
            ? _context.Proyectos.Find(_proyectoId.Value)
            : null;
        private List<ColumnaManoObra> _columnasConfig = new List<ColumnaManoObra>();
        private bool _cargandoColumnas = false;
        private readonly InsumoConsolidationService _consolidationService = new();

        public event EventHandler EstadoConsolidacionCambiado;

        // ── IGridFormato ──────────────────────────────────────────────────────
        public DataGridView GridPrincipal => dgvManoObra;
        public DataGridView GridBusqueda => dgvManoObra;


        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaManoObra _colMORibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;


        // ── Constructor ───────────────────────────────────────────────────────
        public FormCatalogoManoObra(SOPROContext context, int? proyectoId = null)
            : this(context, proyectoId, diagnosticsSink: null, productVersion: null)
        {
        }

        /// <summary>
        /// Seam de composición: sink y versión de diagnóstico opcionales que se
        /// propagan al editor de mano de obra (cálculo de FSR). El constructor
        /// público conserva el comportamiento sin diagnóstico.
        /// </summary>
        internal FormCatalogoManoObra(
            SOPROContext context,
            int? proyectoId,
            IDiagnosticsSink? diagnosticsSink,
            string? productVersion)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);
            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvManoObra.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<ManoDeObra>(_context);
            _proyectoId = proyectoId;
            _diagnosticsSink = diagnosticsSink;
            _productVersion = productVersion;
            _sessionInfo = LegacySessionBridge.FromLegacy(_context, proyectoId);
            _previewCatalogBatchDeletion = new PreviewCatalogBatchDeletion(_factory);
            _deleteCatalogBatch = new DeleteCatalogBatch(_factory);

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoManoObra).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasManoObraHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            ConfigurarFormatoCeldas();
            this.Load += (s, e) => CargarManoDeObra();

            dgvManoObra.ColumnHeaderMouseClick += (s, e) =>
            {
                _formatoCeldas.SeleccionPorEncabezado = true;
                NotificarColumnaSeleccionada(e.ColumnIndex);
            };
            dgvManoObra.ColumnWidthChanged += DgvManoObra_ColumnWidthChanged;
        }

        /// <summary>
        /// Fábrica del editor con el sink/versión de diagnóstico ya compuestos.
        /// La usan las entradas productivas del catálogo (Nuevo/Editar).
        /// </summary>
        internal FormEditarManoObra CrearEditorManoObra(ManoDeObra? manoDeObra)
            => new(_context, _proyectoId, manoDeObra, ProyectoActual, _diagnosticsSink, _productVersion);

        internal FormEditarMatriz CrearEditorMatriz(Matriz? matriz = null)
            => new(_context, _proyectoId ?? 0, matriz, null, null, _diagnosticsSink, _productVersion);


        // ── Grid ──────────────────────────────────────────────────────────────

        // ── Carga de datos ────────────────────────────────────────────────────

        // ── Exportar Excel ────────────────────────────────────────────────────

        // ── MENÚ CONTEXTUAL ───────────────────────────────────────────────────


        // ── IRecalculable ────────────────────────────────────────────────────
    }
}
