using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormMatrices : Form, IGridFormato, IBusquedaGrid, IRecalculable, IConsolidacionInsumos
    {
        private readonly SOPROContext _context;
        private readonly ProjectSessionInfo _sessionInfo;
        private readonly Repository<Matriz> _repository;
        private readonly MatrixCatalogViewService _matrixCatalogViewService = new();
        private readonly MatrixUsageLookupService _matrixUsageLookupService = new();
        private readonly MatrixDeleteFlowService _matrixDeleteFlowService = new();
        private readonly MatrixConsolidationService _matrixConsolidationService = new();
        private readonly CatalogoMatricesExportService _exportService = new(new ProjectDbContextFactory());
        private readonly int _proyectoId;
        private readonly bool _modoEmbebido;
        private readonly EventHandler _onInsumos;
        private ContextMenuStrip? _menuMatrices;

        private Matriz _matrizSeleccionada;
        private List<ColumnaMatriz> _columnasConfig = new();
        private List<Matriz> _listaActual = new();
        private List<MatrixGridRowDisplay> _rowsActuales = new();
        private bool _cargandoColumnas = false;

        public DataGridView GridPrincipal => dgvMatrices;
        public DataGridView GridBusqueda => dgvMatrices;
        public event EventHandler ColumnaSeleccionadaCambiada;
        public event EventHandler EstadoConsolidacionCambiado;

        private ColumnaMatriz _colMatRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public FormMatrices(SOPROContext context, int proyectoId, bool modoEmbebido = false)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);

            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Matriz>(_context);
            _proyectoId = proyectoId;
            _modoEmbebido = modoEmbebido;
            _sessionInfo = LegacySessionBridge.FromLegacy(_context, _proyectoId);

            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId, ReportTitleModuleKeys.CatalogoMatrices).Attach();

            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvMatrices.AplicarEstiloSOPRO();

            _columnasConfig = ColumnasMatrizHelper.ObtenerColumnas(_context, _proyectoId);

            ConfigurarModoEmbebido();
            ConfigurarGrid();

            Load += (_, __) => CargarMatrices();
            dgvMatrices.ColumnHeaderMouseClick += (_, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvMatrices.ColumnWidthChanged += DgvMatrices_ColumnWidthChanged;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Mat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Mat;
            dgvMatrices.KeyDown += dgvMatrices_KeyDown;
            dgvMatrices.CellFormatting += DgvMatrices_CellFormatting;
            dgvMatrices.CellMouseDown += dgvMatrices_CellMouseDown;

            _onInsumos = (_, __) => CargarMatrices();
            FormCatalogoMateriales.InsumosModificados += _onInsumos;
            FormCatalogoManoObra.InsumosModificados += _onInsumos;
            FormCatalogoHerramientas.InsumosModificados += _onInsumos;
            FormCatalogoMaquinaria.InsumosModificados += _onInsumos;
            FormEditarMatriz.MatrizGuardada += _onInsumos;
        }






        private int DecimalesImporte => _context.Proyectos.Find(_proyectoId)?.DecimalesImporte ?? 2;



    }
}
