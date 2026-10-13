using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato y estilo del grid del catálogo de materiales.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMatRibbon == null) return;

            CatalogColumnLayoutService.GuardarFormatoColumna(_context, _colMatRibbon.Id, fmt);

            foreach (DataGridViewColumn col in dgvMateriales.Columns)
            {
                if (col.Tag == _colMatRibbon)
                {
                    AplicarEstiloDesdeColumna((DataGridViewTextBoxColumn)col, fmt);
                    break;
                }
            }
            dgvMateriales.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMateriales.Columns)
            {
                if (col.Tag is not ColumnaMaterial) continue;

                AplicarEstiloDesdeColumna((DataGridViewTextBoxColumn)col, fmt);
            }
            if (_proyectoId.HasValue)
                CatalogColumnLayoutService.GuardarFormatoGlobal(_context, _proyectoId.Value, fmt);
            dgvMateriales.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMatRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvMateriales.Columns.Count)
            {
                var col = dgvMateriales.Columns[colIndex];
                if (col.Tag is ColumnaMaterial cm)
                {
                    _colMatRibbon = cm;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = cm.Nombre,
                        NombreFuente = cm.NombreFuente,
                        TamanoFuente = cm.TamanoFuente,
                        Negrita = cm.Negrita,
                        Cursiva = cm.Cursiva,
                        Alineacion = cm.Alineacion,
                        ColorFondo = cm.ColorFondo,
                        ColorFuente = cm.ColorFuente,
                        WrapTexto = cm.WrapTexto,
                        AlineacionVertical = cm.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public FormCatalogoMateriales(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);
            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvMateriales.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _sessionInfo = LegacySessionBridge.FromLegacy(_context, proyectoId);
            _proyectoId = proyectoId;

            // N4: la sesión es datos puros (no posee contexto): no hay Dispose.
            _listMaterials = new ListMaterials(_factory);
            _deleteMaterial = new DeleteMaterial(_factory);
            _previewMaterialDeletion = new PreviewMaterialDeletion(_factory);

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoMateriales).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasMaterialHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            ConfigurarFormatoCeldas();
            this.Load += (s, e) => CargarMateriales();

            dgvMateriales.ColumnHeaderMouseClick += (s, e) =>
            {
                _formatoCeldas.SeleccionPorEncabezado = true;
                NotificarColumnaSeleccionada(e.ColumnIndex);
            };
            dgvMateriales.ColumnWidthChanged += DgvMateriales_ColumnWidthChanged;
        }

        // ═══════════════════ Formato de celdas (paso 6.3) ═══════════════════
        // Selección tipo Excel → direcciones ESTABLES (INS:{Id} + identificador de
        // columna) → persistencia por lote con FormatoReporteService y undo
        // compuesto. Replica el patrón de Indirectos/Matrices. El formato por
        // COLUMNA existente (ColumnaMaterial) sigue intacto y la celda lo sobreescribe.

        /// <summary>Tipo de reporte estable de este catálogo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = MaterialesReportSnapshotBuilder.TipoReporte;

        /// <summary>Vista por defecto (comodín) del catálogo.</summary>
        internal const string FormatoVista = MaterialesReportSnapshotBuilder.ClaveVista;

        private CatalogoFormatoCeldas _formatoCeldas;

        private void ConfigurarFormatoCeldas()
        {
            _formatoCeldas = new CatalogoFormatoCeldas(dgvMateriales, _context, _proyectoId, FormatoTipoReporte, FormatoVista);
            dgvMateriales.CellFormatting += DgvMateriales_CellFormattingFormato;
            dgvMateriales.CellMouseDown += DgvMateriales_CellMouseDownFormato;
            dgvMateriales.DataBindingComplete += (_, __) => _formatoCeldas.Cargar();
        }

        private void DgvMateriales_CellMouseDownFormato(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _formatoCeldas.SeleccionPorEncabezado = false;
        }

        private void DgvMateriales_CellFormattingFormato(object sender, DataGridViewCellFormattingEventArgs e)
            => _formatoCeldas.AplicarOverride(e);

        // ── IGridSeleccionFormato ─────────────────────────────────────────────

        public bool TieneSeleccionCeldas => _formatoCeldas?.TieneSeleccionCeldas ?? false;

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
            => _formatoCeldas?.ObtenerDireccionesSeleccionadas() ?? Array.Empty<ReportCellAddress>();

        public bool EsCeldaFormateable(ReportCellAddress direccion)
            => _formatoCeldas?.EsCeldaFormateable(direccion) ?? false;

        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
            => _formatoCeldas?.ObtenerOverlayActual(direccion);

        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
            => _formatoCeldas?.AplicarOverlayCelda(direccion, overlay);

        // ── Funciones puras (verificables sin UI) ─────────────────────────────

        internal static string? ObtenerClaveFilaFormato(DataGridView grid, int rowIndex)
            => CatalogoFormatoCeldas.ClaveFila(grid, rowIndex);

        internal static string? ObtenerNombreColumnaFormato(DataGridView grid, int columnIndex)
            => grid is not null && columnIndex >= 0 && columnIndex < grid.Columns.Count
                ? CatalogoFormatoCeldas.NombreColumna(grid.Columns[columnIndex])
                : null;

        internal static IReadOnlyList<ReportCellAddress> ConstruirDireccionesSeleccionadas(
            DataGridView grid, IEnumerable<DataGridViewCell> celdas)
            => CatalogoFormatoCeldas.ConstruirDirecciones(grid, celdas, FormatoVista);

        internal static IReadOnlyList<CambioFormatoCelda> ConstruirLoteCambios(
            IEnumerable<CambioFormatoSeleccion> cambios, bool aplicarAntes)
            => CatalogoFormatoCeldas.ConstruirLote(cambios, aplicarAntes, FormatoVista);

        /// <summary>
        /// Construye el snapshot neutral del catálogo poblado con los overrides de
        /// celda/rol persistidos. Es la única fuente que consumen PDF y Excel.
        /// </summary>
        private ReportColumnSnapshot ConstruirSnapshotMateriales(IEnumerable<ColumnaMaterial> columnas)
        {
            var snapshot = MaterialCatalogExportResolver.BuildSnapshot(
                _proyectoId ?? 0, lblTitulo.Text, columnas,
                FormatoHelper.DecimalesCantidad, FormatoHelper.DecimalesImporte, FormatoHelper.DecimalesPorcentaje);
            return MaterialesReportSnapshotBuilder.ConEstilos(snapshot, new FormatoReporteService(_context));
        }
    }
}
