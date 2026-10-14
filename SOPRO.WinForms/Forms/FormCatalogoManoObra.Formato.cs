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
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato y estilo del grid del catálogo de mano de obra.
    /// </summary>
    public partial class FormCatalogoManoObra
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMORibbon == null) return;

            _colMORibbon.NombreFuente = fmt.NombreFuente;
            _colMORibbon.TamanoFuente = fmt.TamanoFuente;
            _colMORibbon.Negrita = fmt.Negrita;
            _colMORibbon.Cursiva = fmt.Cursiva;
            _colMORibbon.Alineacion = fmt.Alineacion;
            _colMORibbon.ColorFondo = fmt.ColorFondo;
            _colMORibbon.ColorFuente = fmt.ColorFuente;
            _colMORibbon.WrapTexto = fmt.WrapTexto;
            _colMORibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMORibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvManoObra.Columns)
            {
                if (col.Tag == _colMORibbon)
                {
                    AplicarEstiloDesdeColMO((DataGridViewTextBoxColumn)col, _colMORibbon);
                    break;
                }
            }
            dgvManoObra.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvManoObra.Columns)
            {
                if (col.Tag is not ColumnaManoObra colMO) continue;
                colMO.NombreFuente = fmt.NombreFuente;
                colMO.TamanoFuente = fmt.TamanoFuente;
                colMO.Negrita = fmt.Negrita;
                colMO.Cursiva = fmt.Cursiva;
                colMO.Alineacion = fmt.Alineacion;
                colMO.ColorFuente = fmt.ColorFuente;
                colMO.WrapTexto = fmt.WrapTexto;
                colMO.AlineacionVertical = fmt.AlineacionVertical;
                colMO.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMO((DataGridViewTextBoxColumn)col, colMO);
            }
            _context.SaveChanges();
            dgvManoObra.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMORibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvManoObra.Columns.Count)
            {
                var col = dgvManoObra.Columns[colIndex];
                if (col.Tag is ColumnaManoObra cm)
                {
                    _colMORibbon = cm;
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

        // ═══════════════════ Formato de celdas (paso 6.3) ═══════════════════
        // Selección tipo Excel → direcciones ESTABLES (INS:{Id} + identificador de
        // columna) → persistencia por lote y undo compuesto. El formato por COLUMNA
        // existente (ColumnaManoObra) sigue intacto y la celda lo sobreescribe.

        /// <summary>Tipo de reporte estable de este catálogo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = ManoObraReportSnapshotBuilder.TipoReporte;

        /// <summary>Vista por defecto (comodín) del catálogo.</summary>
        internal const string FormatoVista = ManoObraReportSnapshotBuilder.ClaveVista;

        private CatalogoFormatoCeldas _formatoCeldas;

        private void ConfigurarFormatoCeldas()
        {
            _formatoCeldas = new CatalogoFormatoCeldas(dgvManoObra, _context, _proyectoId, FormatoTipoReporte, FormatoVista);
            dgvManoObra.CellFormatting += DgvManoObra_CellFormattingFormato;
            dgvManoObra.CellMouseDown += DgvManoObra_CellMouseDownFormato;
            dgvManoObra.DataBindingComplete += (_, __) => _formatoCeldas.Cargar();
        }

        // Ctrl+Z / Ctrl+Y deshacen/rehacen el último lote de formato de celdas.
        // Se replica el patrón de FormPresupuesto; no se interceptan mientras el
        // grid está en edición (para no robar los atajos de la celda).
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && !dgvManoObra.IsCurrentCellInEditMode
                && _formatoCeldas is not null && _formatoCeldas.TryUndo())
                return true;

            if (keyData == (Keys.Control | Keys.Y) && !dgvManoObra.IsCurrentCellInEditMode
                && _formatoCeldas is not null && _formatoCeldas.TryRedo())
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DgvManoObra_CellMouseDownFormato(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _formatoCeldas.SeleccionPorEncabezado = false;
        }

        private void DgvManoObra_CellFormattingFormato(object sender, DataGridViewCellFormattingEventArgs e)
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
        private ReportColumnSnapshot ConstruirSnapshotManoObra(IEnumerable<ColumnaManoObra> columnas)
        {
            var snapshot = ManoObraCatalogExportResolver.BuildSnapshot(
                _proyectoId ?? 0, lblTitulo.Text, columnas,
                FormatoHelper.DecimalesCantidad, FormatoHelper.DecimalesImporte, FormatoHelper.DecimalesPorcentaje);
            return ManoObraReportSnapshotBuilder.ConEstilos(snapshot, new FormatoReporteService(_context));
        }
    }
}
