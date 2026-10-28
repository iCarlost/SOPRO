using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato por CELDA del grid de flujo de Financiamiento (paso 6.4). Replica el
    /// contrato de Presupuesto/Indirectos/APU: selección tipo Excel → direcciones
    /// ESTABLES (<c>PER:{NumeroPeriodo}</c> × columna del snapshot) → persistencia por
    /// lote con <see cref="FormatoReporteService"/> → undo/redo como UNA sola acción
    /// compuesta. El grid-con de columnas configurables sigue siendo la fuente de la
    /// geometría, pero la identidad de la celda NO depende del índice visual.
    /// </summary>
    public partial class FormFinanciamiento
    {
        /// <summary>Tipo de reporte estable de este módulo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = "Financiamiento";

        /// <summary>Vista por defecto (comodín) del reporte de Financiamiento.</summary>
        internal const string FormatoVistaFinanciamiento = "";

        /// <summary>Prefijo de la clave de fila estable de un período: <c>PER:{NumeroPeriodo}</c>.</summary>
        internal const string PrefijoPeriodoFormato = "PER:";

        /// <summary>
        /// Clave ESTABLE de la fila del período (nunca el índice visual del grid), de
        /// modo que los overrides sobreviven a reordenamientos y recálculos.
        /// </summary>
        internal static string ClaveFilaFormato(int numeroPeriodo) => PrefijoPeriodoFormato + numeroPeriodo;

        private FormatoReporteService? _formatoReporteService;
        private Dictionary<ReportCellAddress, ReportStyleOverlay> _overlaysFormato = new();
        private readonly List<CambioFormatoSeleccion> _pendientesFormato = new();
        private readonly UndoManager _undoManager = new();
        private bool _flushFormatoProgramado;
        private bool _seleccionPorEncabezado;

        private FormatoReporteService FormatoService
            => _formatoReporteService ??= new FormatoReporteService(_context);

        /// <summary>Mapa estable nombre de columna del grid → identificador neutral del snapshot.</summary>
        private static readonly Dictionary<string, string> MapaColumnasFormato =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["colPeriodo"] = "colPeriodo",
                ["colInicio"] = "colInicio",
                ["colFin"] = "colFin",
                ["colDias"] = "colDias",
                ["colCD"] = "colCD",
                ["colCI"] = "colCI",
                ["colEgresos"] = "colEgresos",
                ["colAnticipo"] = "colAnticipo",
                ["colEstim"] = "colEstim",
                ["colAmort"] = "colAmort",
                ["colCobroNeto"] = "colCobro", // el snapshot usa "colCobro"; el grid "colCobroNeto"
                ["colFlujoNeto"] = "colFlujoNeto",
                ["colSaldo"] = "colSaldo",
                ["colTasa"] = "colTasa",
                ["colInteres"] = "colInteres",
            };

        /// <summary>
        /// Clave ESTABLE de la fila para el formato de celdas, o <c>null</c> si la
        /// fila no tiene una entidad persistida. Deriva de <c>NumeroPeriodo</c>.
        /// </summary>
        internal static string? ObtenerClaveFilaFormato(DataGridView grid, int rowIndex)
        {
            if (grid is null) return null;
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;

            return grid.Rows[rowIndex].Tag is FilaFlujoCajaFinanciamiento fila && fila.NumeroPeriodo > 0
                ? ClaveFilaFormato(fila.NumeroPeriodo)
                : null;
        }

        /// <summary>
        /// Identificador ESTABLE de la columna, o <c>null</c> si no es formateable
        /// (p. ej. el relleno <c>colDummy</c>).
        /// </summary>
        internal static string? ObtenerNombreColumnaFormato(DataGridView grid, int columnIndex)
        {
            if (grid is null || columnIndex < 0 || columnIndex >= grid.Columns.Count) return null;

            return MapaColumnasFormato.TryGetValue(grid.Columns[columnIndex].Name, out var nombre)
                ? nombre
                : null;
        }

        /// <summary>
        /// Convierte las celdas seleccionadas en direcciones estables, descartando
        /// filas sin entidad y columnas no formateables. NO deduplica: el coordinador
        /// es quien deduplica y filtra.
        /// </summary>
        internal static IReadOnlyList<ReportCellAddress> ConstruirDireccionesSeleccionadas(
            DataGridView grid,
            IEnumerable<DataGridViewCell> celdas)
        {
            var direcciones = new List<ReportCellAddress>();
            if (grid is null || celdas is null) return direcciones;

            foreach (var celda in celdas)
            {
                if (celda is null || celda.RowIndex < 0 || celda.ColumnIndex < 0) continue;

                var clave = ObtenerClaveFilaFormato(grid, celda.RowIndex);
                if (clave is null) continue;

                var columna = ObtenerNombreColumnaFormato(grid, celda.ColumnIndex);
                if (columna is null) continue;

                direcciones.Add(new ReportCellAddress(FormatoVistaFinanciamiento, clave, columna));
            }

            return direcciones;
        }

        /// <summary>
        /// Construye el lote de comandos con base esperada que
        /// <see cref="FormatoCeldasCommandService"/> persiste: con
        /// <paramref name="aplicarAntes"/> el estado deseado es el "antes" (undo) y
        /// la base esperada el "después"; en otro caso, al revés. Un overlay
        /// <c>null</c> en el estado deseado borra el override.
        /// </summary>
        internal static IReadOnlyList<CambioFormatoCeldaCommand> ConstruirLoteCambios(
            IEnumerable<CambioFormatoSeleccion> cambios,
            bool aplicarAntes)
            => FormatoLoteCambios.Construir(cambios, aplicarAntes, FormatoVistaFinanciamiento);

        // ── IGridSeleccionFormato ─────────────────────────────────────────────

        public bool TieneSeleccionCeldas
        {
            get
            {
                if (_seleccionPorEncabezado) return false;
                foreach (DataGridViewCell celda in dgvFlujo.SelectedCells)
                {
                    if (celda.RowIndex >= 0) return true;
                }
                return false;
            }
        }

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
        {
            if (_seleccionPorEncabezado) return Array.Empty<ReportCellAddress>();
            return ConstruirDireccionesSeleccionadas(dgvFlujo, dgvFlujo.SelectedCells.Cast<DataGridViewCell>());
        }

        public bool EsCeldaFormateable(ReportCellAddress direccion)
        {
            if (direccion is null) return false;
            if (string.IsNullOrWhiteSpace(direccion.ClaveFila)) return false;
            return MapaColumnasFormato.ContainsValue(direccion.NombreColumna);
        }

        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
        {
            if (direccion is null) return null;
            return _overlaysFormato.TryGetValue(direccion, out var overlay) ? overlay : null;
        }

        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
        {
            if (direccion is null || overlay is null) return;
            if (_undoManager.RequiresReconciliation) return;

            var antes = _overlaysFormato.TryGetValue(direccion, out var previo) ? previo : null;
            _overlaysFormato[direccion] = overlay;
            _pendientesFormato.Add(new CambioFormatoSeleccion(direccion, antes, overlay));

            dgvFlujo.Invalidate();
            ProgramarFlushFormato();
        }

        // ── Carga y pintado ───────────────────────────────────────────────────

        /// <summary>
        /// Carga (UNA sola consulta por proyecto/tipo) los overrides persistidos en el
        /// diccionario en memoria que consume el pintado. Se invoca al cargar el
        /// formulario; nunca por celda.
        /// </summary>
        internal void CargarOverlaysFormato()
        {
            try
            {
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    FormatoService.CargarCeldas(_proyecto.Id, FormatoTipoReporte));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CargarOverlaysFormato(financiamiento): {ex.Message}");
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
                return;
            }

            if (_undoManager.RequiresReconciliation)
            {
                _pendientesFormato.Clear();
                FormatoCeldasPersistence.ReconciliarSiCorresponde(_undoManager);
            }
        }

        private void DgvFlujo_CellFormattingFormato(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_overlaysFormato.Count == 0) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var clave = ObtenerClaveFilaFormato(dgvFlujo, e.RowIndex);
            var columna = ObtenerNombreColumnaFormato(dgvFlujo, e.ColumnIndex);
            if (clave is null || columna is null) return;

            var direccion = new ReportCellAddress(FormatoVistaFinanciamiento, clave, columna);
            if (!_overlaysFormato.TryGetValue(direccion, out var overlay) || overlay is null) return;

            FormPresupuesto.AplicarOverlayACellStyle(e.CellStyle, overlay);
        }

        private void DgvFlujo_CellMouseDownFormato(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _seleccionPorEncabezado = false;
            else if (e.ColumnIndex >= 0) _seleccionPorEncabezado = true;
        }

        private void DgvFlujo_SelectionChangedFormato(object? sender, EventArgs e)
        {
            if (_seleccionPorEncabezado) return;
            if (_columnaRibbon is null) return;

            // Al interactuar con el cuerpo del grid, el destino deja de ser la columna.
            _colFinRibbon = null;
            _columnaRibbon = null;
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Persistencia por lote + undo compuesto ─────────────────────────────

        private void ProgramarFlushFormato()
        {
            if (_flushFormatoProgramado) return;
            _flushFormatoProgramado = true;

            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(FlushFormatoPendiente));
            else
                FlushFormatoPendiente();
        }

        private void FlushFormatoPendiente()
        {
            _flushFormatoProgramado = false;
            if (_pendientesFormato.Count == 0) return;

            var cambios = ColapsarCambiosFormato(_pendientesFormato);
            _pendientesFormato.Clear();

            var cambiosUndo = cambios
                .Select(c => new UndoChange(c.Antes, c.Despues))
                .ToList();

            var accion = new CompositeUndoableAction(
                $"Formato de {cambios.Count} celda(s) en financiamiento",
                cambiosUndo,
                _ => AplicarCambiosFormato(cambios, aplicarAntes: true),
                _ => AplicarCambiosFormato(cambios, aplicarAntes: false));

            try
            {
                // Execute aplica el "después" (persiste + repinta) y sólo si tiene
                // éxito apila la acción; ante un fallo no queda nada a medias.
                _undoManager.Execute(accion);
            }
            catch (FormatoCeldasPersistenceException ex)
            {
                if (ex.Status != BudgetOperationStatus.OutcomeUnknown)
                    RevertirCambiosFormato(cambios);

                MessageBox.Show(ex.Message, "Formato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                RevertirCambiosFormato(cambios);
                MessageBox.Show(
                    $"No se pudo guardar el formato de las celdas:\n{ex.Message}",
                    "Formato",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Colapsa los cambios pendientes a UNA entrada por dirección: el primer
        /// "antes" observado y el último "después".
        /// </summary>
        private static List<CambioFormatoSeleccion> ColapsarCambiosFormato(
            IEnumerable<CambioFormatoSeleccion> pendientes)
        {
            var orden = new List<ReportCellAddress>();
            var antes = new Dictionary<ReportCellAddress, ReportStyleOverlay?>();
            var despues = new Dictionary<ReportCellAddress, ReportStyleOverlay>();

            foreach (var cambio in pendientes)
            {
                if (cambio is null || cambio.Direccion is null) continue;

                if (!antes.ContainsKey(cambio.Direccion))
                {
                    orden.Add(cambio.Direccion);
                    antes[cambio.Direccion] = cambio.Antes;
                }
                despues[cambio.Direccion] = cambio.Despues;
            }

            var resultado = new List<CambioFormatoSeleccion>(orden.Count);
            foreach (var direccion in orden)
                resultado.Add(new CambioFormatoSeleccion(direccion, antes[direccion], despues[direccion]));

            return resultado;
        }

        private void AplicarCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios, bool aplicarAntes)
        {
            FormatoCeldasPersistence.Aplicar(
                _context.DatabasePath,
                _proyecto.Id,
                FormatoTipoReporte,
                ConstruirLoteCambios(cambios, aplicarAntes),
                _undoManager);

            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, aplicarAntes ? cambio.Antes : cambio.Despues);

            dgvFlujo.Invalidate();
        }

        private void RevertirCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, cambio.Antes);

            dgvFlujo.Invalidate();
        }

        private void EstablecerOverlayEnMemoria(ReportCellAddress direccion, ReportStyleOverlay? overlay)
        {
            if (overlay is null)
                _overlaysFormato.Remove(direccion);
            else
                _overlaysFormato[direccion] = overlay;
        }

        // ── Atajos undo/redo ───────────────────────────────────────────────────

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && _undoManager.CanUndo)
            {
                try { _undoManager.Undo(); }
                catch (FormatoCeldasPersistenceException ex) { FormatoCeldasPersistence.ManejarErrorDeFormato(ex); }
                return true;
            }

            if (keyData == (Keys.Control | Keys.Y) && _undoManager.CanRedo)
            {
                try { _undoManager.Redo(); }
                catch (FormatoCeldasPersistenceException ex) { FormatoCeldasPersistence.ManejarErrorDeFormato(ex); }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
