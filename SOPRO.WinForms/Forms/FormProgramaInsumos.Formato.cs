using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato por celda/rol del Programa de Insumos (paso 6.2). Replica el patrón de
    /// Presupuesto (4.1), Indirectos/Matrices (5.1) y Programa de Obra (6.2):
    /// selección del grid → direcciones ESTABLES (<c>INS:{InsumoId}</c>) →
    /// persistencia por lote con <see cref="FormatoReporteService"/> → undo/redo como
    /// UNA acción compuesta. Las columnas DINÁMICAS de período usan el identificador
    /// estable del período (<c>per_</c>,<c>acu_</c>,<c>imp_</c>,<c>iacu_</c> + PeriodoId,
    /// nunca el índice visual), de modo que el formato sobrevive a regenerar períodos.
    /// </summary>
    public partial class FormProgramaInsumos
    {
        /// <summary>Tipo de reporte estable de este módulo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = "ProgramaInsumos";

        /// <summary>Vista por defecto (comodín de vista) del programa de insumos.</summary>
        internal const string FormatoVista = "";

        private FormatoReporteService? _formatoReporteService;
        private Dictionary<ReportCellAddress, ReportStyleOverlay>? _overlaysFormato;
        private readonly List<CambioFormatoSeleccion> _pendientesFormato = new();
        private readonly UndoManager _undoManager = new();
        private bool _flushFormatoProgramado;
        private bool _seleccionPorEncabezado;

        private Dictionary<string, ReportStyleOverlay>? _estilosFilaFormato;
        private int _estilosFilaFormatoProyectoId = -1;

        private FormatoReporteService FormatoService
            => _formatoReporteService ??= new FormatoReporteService(_context);

        /// <summary>
        /// CTRL+Z / CTRL+Y revierten/rehacen la última acción del
        /// <see cref="UndoManager"/> compartido (ediciones y lotes de formato compuestos).
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z))
            {
                if (_undoManager.CanUndo)
                {
                    try { _undoManager.Undo(); }
                    catch (FormatoCeldasPersistenceException ex) { FormatoCeldasPersistence.ManejarErrorDeFormato(ex); }
                    return true;
                }
            }
            else if (keyData == (Keys.Control | Keys.Y))
            {
                if (_undoManager.CanRedo)
                {
                    try { _undoManager.Redo(); }
                    catch (FormatoCeldasPersistenceException ex) { FormatoCeldasPersistence.ManejarErrorDeFormato(ex); }
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// Conecta los eventos del grid con el formato por celda.
        /// </summary>
        private void InicializarFormatoProgramaInsumos()
        {
            // H4: el formato por celda exige selección tipo Excel. El grid se diseñó
            // con FullRowSelect, donde un clic sobre UNA celda seleccionaba TODA la
            // fila: SelectedCells devolvía todas las columnas y el ribbon aplicaba el
            // override a la fila completa. Se fija CellSelect (+MultiSelect para rango
            // con Shift / multi-rango con Ctrl) igual que Explosión, Indirectos,
            // Presupuesto y Programa de Obra, de modo que la dirección de celda sea la
            // de la celda realmente seleccionada. El encabezado sigue resolviéndose por
            // COLUMNA. Se configura también aquí (y no sólo en ConfigurarGrid) para que
            // la corrección persista aunque el Designer se regenere.
            ConfigurarSeleccionPorCelda(dgvProgramaInsumos);

            dgvProgramaInsumos.CellMouseDown += DgvInsumos_CellMouseDownFormato;
            dgvProgramaInsumos.ColumnHeaderMouseClick += DgvInsumos_ColumnHeaderMouseClickFormato;
            dgvProgramaInsumos.SelectionChanged += DgvInsumos_SelectionChangedFormato;
            dgvProgramaInsumos.CellFormatting += DgvInsumos_CellFormattingFormato;
        }

        /// <summary>
        /// Fuerza en el grid la selección POR CELDAS (tipo Excel): fija
        /// <see cref="DataGridViewSelectionMode.CellSelect"/> y habilita
        /// multi-selección para que un rango (Shift) o varios rangos (Ctrl) se
        /// reporten como celdas concretas. Es requisito del formato por celda: con
        /// <c>FullRowSelect</c> el clic sobre una celda selecciona TODA la fila y el
        /// ribbon aplicaría el override a todas sus columnas (hallazgo H4).
        /// </summary>
        internal static void ConfigurarSeleccionPorCelda(DataGridView grid)
        {
            if (grid is null) return;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.MultiSelect = true;
        }

        /// <summary>
        /// Identificador ESTABLE de la columna, o <c>null</c> si la columna no es
        /// formateable (p. ej. el relleno <c>colDummy</c>). Para las columnas de
        /// período el nombre del grid ES el identificador estable (<c>per_</c>,
        /// <c>acu_</c>, <c>imp_</c>, <c>iacu_</c> + PeriodoId), nunca su índice.
        /// </summary>
        internal static string? ObtenerNombreColumnaFormato(DataGridView grid, int columnIndex)
        {
            if (grid is null || columnIndex < 0 || columnIndex >= grid.Columns.Count) return null;
            var nombre = grid.Columns[columnIndex].Name;
            if (string.IsNullOrWhiteSpace(nombre)) return null;
            if (string.Equals(nombre, "colDummy", StringComparison.OrdinalIgnoreCase)) return null;
            return nombre;
        }

        /// <summary>
        /// Clave ESTABLE de la fila para el formato de celdas (<c>INS:{InsumoId}</c>),
        /// o <c>null</c> si la fila no tiene insumo persistido.
        /// </summary>
        internal static string? ObtenerClaveFilaFormato(DataGridView grid, int rowIndex)
        {
            if (grid is null) return null;
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;
            if (grid.Rows[rowIndex].Tag is not int insumoId || insumoId <= 0) return null;

            return ProgramaInsumosReportSnapshotBuilder.ClaveFila(insumoId);
        }

        /// <summary>
        /// Convierte las celdas seleccionadas en direcciones estables, descartando
        /// filas sin insumo y columnas no formateables. NO deduplica.
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

                direcciones.Add(new ReportCellAddress(FormatoVista, clave, columna));
            }

            return direcciones;
        }

        /// <summary>
        /// Construye el lote de comandos con base esperada que
        /// <see cref="FormatoCeldasCommandService"/> persiste: con
        /// <paramref name="aplicarAntes"/> el estado deseado es el "antes" (undo) y
        /// la base esperada el "después"; en otro caso, al revés.
        /// </summary>
        internal static IReadOnlyList<CambioFormatoCeldaCommand> ConstruirLoteCambios(
            IEnumerable<CambioFormatoSeleccion> cambios,
            bool aplicarAntes)
            => FormatoLoteCambios.Construir(cambios, aplicarAntes, FormatoVista);

        // ── IGridSeleccionFormato ─────────────────────────────────────────────

        public bool TieneSeleccionCeldas
        {
            get
            {
                if (_seleccionPorEncabezado) return false;
                foreach (DataGridViewCell celda in dgvProgramaInsumos.SelectedCells)
                    if (celda.RowIndex >= 0) return true;
                return false;
            }
        }

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
        {
            if (_seleccionPorEncabezado) return Array.Empty<ReportCellAddress>();
            return ConstruirDireccionesSeleccionadas(
                dgvProgramaInsumos,
                dgvProgramaInsumos.SelectedCells.Cast<DataGridViewCell>());
        }

        public bool EsCeldaFormateable(ReportCellAddress direccion)
        {
            if (direccion is null) return false;
            if (string.IsNullOrWhiteSpace(direccion.ClaveFila)) return false;
            if (string.IsNullOrWhiteSpace(direccion.NombreColumna)) return false;
            return dgvProgramaInsumos.Columns
                .Cast<DataGridViewColumn>()
                .Any(c => string.Equals(c.Name, direccion.NombreColumna, StringComparison.OrdinalIgnoreCase));
        }

        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
        {
            if (direccion is null) return null;
            var overlays = _overlaysFormato ?? CargarOverlaysFormato();
            return overlays.TryGetValue(direccion, out var overlay) ? overlay : null;
        }

        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
        {
            if (direccion is null || overlay is null) return;
            if (_undoManager.RequiresReconciliation) return;

            var overlays = _overlaysFormato ?? CargarOverlaysFormato();
            var antes = overlays.TryGetValue(direccion, out var previo) ? previo : null;
            overlays[direccion] = overlay;
            _pendientesFormato.Add(new CambioFormatoSeleccion(direccion, antes, overlay));

            dgvProgramaInsumos.Invalidate();
            ProgramarFlushFormato();
        }

        private void DgvInsumos_CellMouseDownFormato(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _seleccionPorEncabezado = false;
            else if (e.ColumnIndex >= 0) _seleccionPorEncabezado = true;
        }

        private void DgvInsumos_ColumnHeaderMouseClickFormato(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex >= 0) _seleccionPorEncabezado = true;
        }

        private void DgvInsumos_SelectionChangedFormato(object? sender, EventArgs e)
        {
            if (_seleccionPorEncabezado) return;
            if (_columnaRibbon is null) return;

            _columnaRibbon = null;
            _colRibbon = null;
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Estilos por ROL de fila (default + override persistido) ───────────

        internal IReadOnlyDictionary<string, ReportStyleOverlay> ObtenerEstilosFilaUsuario()
        {
            if (_estilosFilaFormato is not null && _estilosFilaFormatoProyectoId == _proyecto.Id)
                return _estilosFilaFormato;

            try
            {
                _estilosFilaFormato = new Dictionary<string, ReportStyleOverlay>(
                    FormatoService.CargarEstilosFila(_proyecto.Id, FormatoTipoReporte),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ObtenerEstilosFilaUsuario(ProgramaInsumos): {ex.Message}");
                _estilosFilaFormato = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);
            }

            _estilosFilaFormatoProyectoId = _proyecto.Id;
            return _estilosFilaFormato;
        }

        /// <summary>Rol funcional de fila del Programa de Insumos.</summary>
        internal static string RolFilaFormato(bool esResumen)
            => ProgramaInsumosReportSnapshotBuilder.RolFila(esResumen);

        private void DgvInsumos_CellFormattingFormato(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            AplicarOverrideFormatoCeldaInsumos(dgvProgramaInsumos, e);
        }

        private void AplicarOverrideFormatoCeldaInsumos(DataGridView grid, DataGridViewCellFormattingEventArgs e)
        {
            var overlays = _overlaysFormato;
            if (overlays is null || overlays.Count == 0) return;

            var clave = ObtenerClaveFilaFormato(grid, e.RowIndex);
            var columna = ObtenerNombreColumnaFormato(grid, e.ColumnIndex);
            if (clave is null || columna is null) return;

            var direccion = new ReportCellAddress(FormatoVista, clave, columna);
            if (!overlays.TryGetValue(direccion, out var overlay) || overlay is null) return;

            FormPresupuesto.AplicarOverlayACellStyle(e.CellStyle, overlay);
        }

        // ── Persistencia por lote + undo compuesto ─────────────────────────────

        private Dictionary<ReportCellAddress, ReportStyleOverlay> CargarOverlaysFormato()
        {
            try
            {
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    FormatoService.CargarCeldas(_proyecto.Id, FormatoTipoReporte));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CargarOverlaysFormato(ProgramaInsumos): {ex.Message}");
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
                return _overlaysFormato;
            }

            if (_undoManager.RequiresReconciliation)
            {
                _pendientesFormato.Clear();
                FormatoCeldasPersistence.ReconciliarSiCorresponde(_undoManager);
            }

            return _overlaysFormato;
        }

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
                $"Formato de {cambios.Count} celda(s) en el programa de insumos",
                cambiosUndo,
                _ => AplicarCambiosFormato(cambios, aplicarAntes: true),
                _ => AplicarCambiosFormato(cambios, aplicarAntes: false));

            try
            {
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

            dgvProgramaInsumos.Invalidate();
        }

        private void RevertirCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, cambio.Antes);

            dgvProgramaInsumos.Invalidate();
        }

        private void EstablecerOverlayEnMemoria(ReportCellAddress direccion, ReportStyleOverlay? overlay)
        {
            var overlays = _overlaysFormato ?? CargarOverlaysFormato();
            if (overlay is null)
                overlays.Remove(direccion);
            else
                overlays[direccion] = overlay;
        }

        /// <summary>
        /// Construye el snapshot neutral del Programa de Insumos poblado con los
        /// overrides de celda/rol persistidos. NO lee del <c>DataGridView</c>.
        /// </summary>
        private ReportColumnSnapshot ConstruirSnapshotProgramaInsumos(string tituloReporte)
        {
            var snapshot = new ProgramaInsumosReportSnapshotBuilder()
                .Build(_proyecto, tituloReporte, _actual, MapearVistaReporte(VistaActual()));

            return ProgramaInsumosReportSnapshotBuilder.ConEstilos(snapshot, FormatoService);
        }
    }
}
