using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato y estilo del grid del catálogo de maquinaria.
    /// </summary>
    public partial class FormCatalogoMaquinaria
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMaqRibbon == null) return;

            _colMaqRibbon.NombreFuente = fmt.NombreFuente;
            _colMaqRibbon.TamanoFuente = fmt.TamanoFuente;
            _colMaqRibbon.Negrita = fmt.Negrita;
            _colMaqRibbon.Cursiva = fmt.Cursiva;
            _colMaqRibbon.Alineacion = fmt.Alineacion;
            _colMaqRibbon.ColorFondo = fmt.ColorFondo;
            _colMaqRibbon.ColorFuente = fmt.ColorFuente;
            _colMaqRibbon.WrapTexto = fmt.WrapTexto;
            _colMaqRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMaqRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvMaquinaria.Columns)
            {
                if (col.Tag == _colMaqRibbon)
                {
                    AplicarEstiloDesdeColMaq((DataGridViewTextBoxColumn)col, _colMaqRibbon);
                    break;
                }
            }
            dgvMaquinaria.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMaquinaria.Columns)
            {
                if (col.Tag is not ColumnaMaquinaria colMaq) continue;
                colMaq.NombreFuente = fmt.NombreFuente;
                colMaq.TamanoFuente = fmt.TamanoFuente;
                colMaq.Negrita = fmt.Negrita;
                colMaq.Cursiva = fmt.Cursiva;
                colMaq.Alineacion = fmt.Alineacion;
                colMaq.ColorFuente = fmt.ColorFuente;
                colMaq.WrapTexto = fmt.WrapTexto;
                colMaq.AlineacionVertical = fmt.AlineacionVertical;
                colMaq.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMaq((DataGridViewTextBoxColumn)col, colMaq);
            }
            _context.SaveChanges();
            dgvMaquinaria.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMaqRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvMaquinaria.Columns.Count)
            {
                var col = dgvMaquinaria.Columns[colIndex];
                if (col.Tag is ColumnaMaquinaria cm)
                {
                    _colMaqRibbon = cm;
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
        public FormCatalogoMaquinaria(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);
            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvMaquinaria.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Maquinaria>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoMaquinaria).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasMaquinariaHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            ConfigurarFormatoCeldas();
            this.Load += (s, e) => CargarMaquinaria();

            dgvMaquinaria.ColumnHeaderMouseClick += (s, e) =>
            {
                _formatoCeldas.SeleccionPorEncabezado = true;
                NotificarColumnaSeleccionada(e.ColumnIndex);
            };
            dgvMaquinaria.ColumnWidthChanged += DgvMaquinaria_ColumnWidthChanged;
        }

        // ═══════════════════ Formato de celdas (paso 6.3) ═══════════════════
        // Selección tipo Excel → direcciones ESTABLES (INS:{Id} + identificador de
        // columna) → persistencia por lote y undo compuesto. El formato por COLUMNA
        // existente (ColumnaMaquinaria) sigue intacto y la celda lo sobreescribe.

        /// <summary>Tipo de reporte estable de este catálogo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = CostoHorarioReportSnapshotBuilder.TipoReporte;

        /// <summary>Vista por defecto (comodín) del catálogo.</summary>
        internal const string FormatoVista = CostoHorarioReportSnapshotBuilder.ClaveVista;

        private CatalogoFormatoCeldas _formatoCeldas;

        private void ConfigurarFormatoCeldas()
        {
            _formatoCeldas = new CatalogoFormatoCeldas(dgvMaquinaria, _context, _proyectoId, FormatoTipoReporte, FormatoVista);
            dgvMaquinaria.CellFormatting += DgvMaquinaria_CellFormattingFormato;
            dgvMaquinaria.CellMouseDown += DgvMaquinaria_CellMouseDownFormato;
            dgvMaquinaria.DataBindingComplete += (_, __) => _formatoCeldas.Cargar();
        }

        // Ctrl+Z / Ctrl+Y deshacen/rehacen el último lote de formato de celdas.
        // Se replica el patrón de FormPresupuesto; no se interceptan mientras el
        // grid está en edición (para no robar los atajos de la celda).
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && !dgvMaquinaria.IsCurrentCellInEditMode
                && _formatoCeldas is not null && _formatoCeldas.TryUndo())
                return true;

            if (keyData == (Keys.Control | Keys.Y) && !dgvMaquinaria.IsCurrentCellInEditMode
                && _formatoCeldas is not null && _formatoCeldas.TryRedo())
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DgvMaquinaria_CellMouseDownFormato(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _formatoCeldas.SeleccionPorEncabezado = false;
        }

        private void DgvMaquinaria_CellFormattingFormato(object sender, DataGridViewCellFormattingEventArgs e)
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

        internal static IReadOnlyList<CambioFormatoCeldaCommand> ConstruirLoteCambios(
            IEnumerable<CambioFormatoSeleccion> cambios, bool aplicarAntes)
            => CatalogoFormatoCeldas.ConstruirLote(cambios, aplicarAntes, FormatoVista);

        /// <summary>
        /// Construye el snapshot neutral del catálogo poblado con los overrides de
        /// celda/rol persistidos. Es la única fuente que consumen PDF y Excel.
        /// </summary>
        private ReportColumnSnapshot ConstruirSnapshotMaquinaria(IEnumerable<ColumnaMaquinaria> columnas)
        {
            var snapshot = new CostoHorarioReportSnapshotBuilder().Build(
                _proyectoId ?? 0, lblTitulo.Text, columnas,
                FormatoHelper.DecimalesCantidad, FormatoHelper.DecimalesImporte, FormatoHelper.DecimalesPorcentaje);
            return CostoHorarioReportSnapshotBuilder.ConEstilos(snapshot, new FormatoReporteService(_context));
        }
    }
}
