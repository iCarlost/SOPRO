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
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato y estilo del grid del catálogo de herramientas.
    /// </summary>
    public partial class FormCatalogoHerramientas
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colHerRibbon == null) return;

            _colHerRibbon.NombreFuente = fmt.NombreFuente;
            _colHerRibbon.TamanoFuente = fmt.TamanoFuente;
            _colHerRibbon.Negrita = fmt.Negrita;
            _colHerRibbon.Cursiva = fmt.Cursiva;
            _colHerRibbon.Alineacion = fmt.Alineacion;
            _colHerRibbon.ColorFondo = fmt.ColorFondo;
            _colHerRibbon.ColorFuente = fmt.ColorFuente;
            _colHerRibbon.WrapTexto = fmt.WrapTexto;
            _colHerRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colHerRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvHerramientas.Columns)
            {
                if (col.Tag == _colHerRibbon)
                {
                    AplicarEstiloDesdeColHer((DataGridViewTextBoxColumn)col, _colHerRibbon);
                    break;
                }
            }
            dgvHerramientas.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvHerramientas.Columns)
            {
                if (col.Tag is not ColumnaHerramienta colHer) continue;
                colHer.NombreFuente = fmt.NombreFuente;
                colHer.TamanoFuente = fmt.TamanoFuente;
                colHer.Negrita = fmt.Negrita;
                colHer.Cursiva = fmt.Cursiva;
                colHer.Alineacion = fmt.Alineacion;
                colHer.ColorFuente = fmt.ColorFuente;
                colHer.WrapTexto = fmt.WrapTexto;
                colHer.AlineacionVertical = fmt.AlineacionVertical;
                colHer.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColHer((DataGridViewTextBoxColumn)col, colHer);
            }
            _context.SaveChanges();
            dgvHerramientas.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colHerRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvHerramientas.Columns.Count)
            {
                var col = dgvHerramientas.Columns[colIndex];
                if (col.Tag is ColumnaHerramienta ch)
                {
                    _colHerRibbon = ch;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = ch.Nombre,
                        NombreFuente = ch.NombreFuente,
                        TamanoFuente = ch.TamanoFuente,
                        Negrita = ch.Negrita,
                        Cursiva = ch.Cursiva,
                        Alineacion = ch.Alineacion,
                        ColorFondo = ch.ColorFondo,
                        ColorFuente = ch.ColorFuente,
                        WrapTexto = ch.WrapTexto,
                        AlineacionVertical = ch.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public FormCatalogoHerramientas(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            dgvHerramientas.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Herramienta>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelHeader, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoHerramientas).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasHerramientaHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            ConfigurarFormatoCeldas();
            this.Load += (s, e) => CargarHerramientas();

            dgvHerramientas.ColumnHeaderMouseClick += (s, e) =>
            {
                _formatoCeldas.SeleccionPorEncabezado = true;
                NotificarColumnaSeleccionada(e.ColumnIndex);
            };
            dgvHerramientas.ColumnWidthChanged += DgvHerramientas_ColumnWidthChanged;
        }

        // ═══════════════════ Formato de celdas (paso 6.3) ═══════════════════
        // Selección tipo Excel → direcciones ESTABLES (INS:{Id} + identificador de
        // columna) → persistencia por lote y undo compuesto. El formato por COLUMNA
        // existente (ColumnaHerramienta) sigue intacto y la celda lo sobreescribe.

        /// <summary>Tipo de reporte estable de este catálogo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = HerramientasReportSnapshotBuilder.TipoReporte;

        /// <summary>Vista por defecto (comodín) del catálogo.</summary>
        internal const string FormatoVista = HerramientasReportSnapshotBuilder.ClaveVista;

        private CatalogoFormatoCeldas _formatoCeldas;

        private void ConfigurarFormatoCeldas()
        {
            _formatoCeldas = new CatalogoFormatoCeldas(dgvHerramientas, _context, _proyectoId, FormatoTipoReporte, FormatoVista);
            dgvHerramientas.CellFormatting += DgvHerramientas_CellFormattingFormato;
            dgvHerramientas.CellMouseDown += DgvHerramientas_CellMouseDownFormato;
            dgvHerramientas.DataBindingComplete += (_, __) => _formatoCeldas.Cargar();
        }

        // Ctrl+Z / Ctrl+Y deshacen/rehacen el último lote de formato de celdas.
        // Se replica el patrón de FormPresupuesto; no se interceptan mientras el
        // grid está en edición (para no robar los atajos de la celda).
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && !dgvHerramientas.IsCurrentCellInEditMode
                && _formatoCeldas is not null && _formatoCeldas.TryUndo())
                return true;

            if (keyData == (Keys.Control | Keys.Y) && !dgvHerramientas.IsCurrentCellInEditMode
                && _formatoCeldas is not null && _formatoCeldas.TryRedo())
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DgvHerramientas_CellMouseDownFormato(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _formatoCeldas.SeleccionPorEncabezado = false;
        }

        private void DgvHerramientas_CellFormattingFormato(object sender, DataGridViewCellFormattingEventArgs e)
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
        private ReportColumnSnapshot ConstruirSnapshotHerramientas(IEnumerable<ColumnaHerramienta> columnas)
        {
            var snapshot = HerramientasCatalogExportResolver.BuildSnapshot(
                _proyectoId ?? 0, lblTitulo.Text, columnas,
                FormatoHelper.DecimalesCantidad, FormatoHelper.DecimalesImporte, FormatoHelper.DecimalesPorcentaje);
            return HerramientasReportSnapshotBuilder.ConEstilos(snapshot, new FormatoReporteService(_context));
        }
    }
}
