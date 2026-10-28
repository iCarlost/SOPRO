using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato por celda/rol de la Explosión de Insumos (paso 6.1). Replica el
    /// patrón de Presupuesto (4.1/4.3) e Indirectos (5.1/5.2): selección tipo Excel
    /// → direcciones ESTABLES → persistencia por lote con
    /// <see cref="FormatoReporteService"/> → undo/redo como UNA acción compuesta;
    /// snapshot neutral poblado con overrides para PDF/Excel; defaults por rol en
    /// <see cref="EstilosFilaPredeterminados"/>; aislamiento por
    /// <c>TipoReporte</c> ("Explosion").
    /// </summary>
    public partial class FormExplosionInsumos
    {
        /// <summary>Tipo de reporte estable de este módulo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = ExplosionReportSnapshotBuilder.TipoReporte;

        /// <summary>
        /// Vista por defecto de la explosión. Se usa "" (comodín de vista) para que el
        /// override guardado desde el grid también resuelva en cualquier vista del
        /// reporte (ReportCellStyleResolver trata "" como comodín).
        /// </summary>
        internal const string FormatoVistaExplosion = ExplosionReportSnapshotBuilder.ClaveVista;

        private readonly UndoManager _undoManager = new();
        private FormatoReporteService? _formatoReporteService;
        private Dictionary<ReportCellAddress, ReportStyleOverlay> _overlaysFormato = new();
        private readonly List<CambioFormatoSeleccion> _pendientesFormato = new();
        private bool _flushFormatoProgramado;
        private bool _seleccionPorEncabezado;

        private Dictionary<string, ReportStyleOverlay>? _estilosFilaFormato;
        private int _estilosFilaFormatoProyectoId = -1;

        private FormatoReporteService FormatoService
            => _formatoReporteService ??= new FormatoReporteService(_context);

        /// <summary>Mapa estable nombre de columna del grid → identificador neutral.</summary>
        private static readonly Dictionary<string, string> MapaColumnasFormato =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["colClave"] = "Clave",
                ["colDescripcion"] = "Descripcion",
                ["colUnidad"] = "Unidad",
                ["colCantidad"] = "Cantidad",
                ["colPrecioUnitario"] = "PrecioUnitario",
                // El alias legacy Importe se normaliza a ImporteTotal en el snapshot.
                ["colImporte"] = "ImporteTotal",
                ["colPorcentaje"] = "Porcentaje",
            };

        // ═══════════════ Mapeo selección → direcciones estables ═══════════════

        /// <summary>
        /// Clave ESTABLE de la fila para el formato de celdas, o <c>null</c> si la
        /// fila no es formateable (fila vacía). La clave la fijó
        /// <see cref="ExplosionGridRenderService"/> al construir la fila.
        /// </summary>
        internal static string? ObtenerClaveFilaFormato(DataGridView grid, int rowIndex)
        {
            if (grid is null || rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;
            return (grid.Rows[rowIndex].Tag as ExplosionFilaInfo)?.Clave;
        }

        /// <summary>
        /// Identificador ESTABLE de la columna, o <c>null</c> si la columna no es
        /// formateable (p. ej. la columna de relleno <c>colDummy</c>).
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
        /// filas sin clave y columnas no formateables. NO deduplica: el coordinador
        /// (Ctrl / rangos discontinuos) es quien deduplica y filtra.
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

                direcciones.Add(new ReportCellAddress(FormatoVistaExplosion, clave, columna));
            }

            return direcciones;
        }

        /// <summary>
        /// Construye el lote de comandos con base esperada que
        /// <see cref="FormatoCeldasCommandService"/> persiste: con
        /// <paramref name="aplicarAntes"/> el estado deseado es el "antes" (undo) y
        /// la base esperada el "después"; en otro caso, al revés. Un overlay
        /// <c>null</c> en el estado deseado indica BORRAR el override.
        /// </summary>
        internal static IReadOnlyList<CambioFormatoCeldaCommand> ConstruirLoteCambios(
            IEnumerable<CambioFormatoSeleccion> cambios,
            bool aplicarAntes)
            => FormatoLoteCambios.Construir(cambios, aplicarAntes, FormatoVistaExplosion);

        // ═══════════════════════ IGridSeleccionFormato ═══════════════════════

        public bool TieneSeleccionCeldas
        {
            get
            {
                if (_seleccionPorEncabezado) return false;
                foreach (DataGridViewCell celda in dgvExplosion.SelectedCells)
                {
                    if (celda.RowIndex >= 0) return true;
                }
                return false;
            }
        }

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
        {
            if (_seleccionPorEncabezado) return Array.Empty<ReportCellAddress>();
            return ConstruirDireccionesSeleccionadas(
                dgvExplosion,
                dgvExplosion.SelectedCells.Cast<DataGridViewCell>());
        }

        public bool EsCeldaFormateable(ReportCellAddress direccion)
        {
            if (direccion is null) return false;
            if (string.IsNullOrWhiteSpace(direccion.ClaveFila)) return false;
            if (string.IsNullOrWhiteSpace(direccion.NombreColumna)) return false;
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

            dgvExplosion.Invalidate();
            ProgramarFlushFormato();
        }

        /// <summary>
        /// Distingue la interacción de CUERPO (destino CELDAS) de la de ENCABEZADO
        /// (destino COLUMNA). <c>CellMouseDown</c> se dispara ANTES del cambio de
        /// selección.
        /// </summary>
        private void DgvExplosion_CellMouseDownFormato(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _seleccionPorEncabezado = false;
            else if (e.ColumnIndex >= 0) _seleccionPorEncabezado = true;
        }

        private void DgvExplosion_SelectionChangedFormato(object? sender, EventArgs e)
        {
            if (_seleccionPorEncabezado) return;
            if (_columnaRibbon is null) return;

            // Al interactuar con el cuerpo del grid, el destino deja de ser la
            // columna y pasa a ser la selección de celdas.
            _columnaRibbon = null;
            _columnaExpRibbon = null;
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ═══════════════ Estilos por ROL de fila (default + override) ═══════════════

        /// <summary>
        /// Overrides de estilo por rol persistidos del (proyecto, tipo), cargados
        /// con UNA sola consulta y memoizados por proyecto. El pintado los combina
        /// sobre los defaults de <see cref="EstilosFilaPredeterminados"/> (override
        /// del usuario &gt; default).
        /// </summary>
        internal IReadOnlyDictionary<string, ReportStyleOverlay> ObtenerEstilosFilaUsuario()
        {
            if (_estilosFilaFormato is not null && _estilosFilaFormatoProyectoId == _proyectoId)
                return _estilosFilaFormato;

            try
            {
                _estilosFilaFormato = new Dictionary<string, ReportStyleOverlay>(
                    FormatoService.CargarEstilosFila(_proyectoId, FormatoTipoReporte),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ObtenerEstilosFilaUsuario(explosion): {ex.Message}");
                _estilosFilaFormato = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);
            }

            _estilosFilaFormatoProyectoId = _proyectoId;
            return _estilosFilaFormato;
        }

        // ═══════════════ Carga y pintado de overrides de celda ═══════════════

        /// <summary>
        /// Carga (UNA sola consulta por proyecto/tipo) los overrides persistidos en
        /// el diccionario en memoria que consume el pintado. Se llama al generar la
        /// explosión; nunca por celda.
        /// </summary>
        internal void CargarOverlaysFormato()
        {
            try
            {
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    FormatoService.CargarCeldas(_proyectoId, FormatoTipoReporte));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CargarOverlaysFormato(explosion): {ex.Message}");
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
                return;
            }

            if (_undoManager.RequiresReconciliation)
            {
                _pendientesFormato.Clear();
                FormatoCeldasPersistence.ReconciliarSiCorresponde(_undoManager);
            }
        }

        /// <summary>
        /// Aplica, si existe, el override en memoria de la celda (fila × columna)
        /// sobre el estilo del evento. Sólo consulta el diccionario en memoria: no
        /// hace ninguna consulta a la base de datos por celda.
        /// </summary>
        private void DgvExplosion_CellFormattingFormato(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_overlaysFormato.Count == 0) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var clave = ObtenerClaveFilaFormato(dgvExplosion, e.RowIndex);
            var columna = ObtenerNombreColumnaFormato(dgvExplosion, e.ColumnIndex);
            if (clave is null || columna is null) return;

            var direccion = new ReportCellAddress(FormatoVistaExplosion, clave, columna);
            if (!_overlaysFormato.TryGetValue(direccion, out var overlay) || overlay is null) return;

            FormPresupuesto.AplicarOverlayACellStyle(e.CellStyle, overlay);
        }

        // ═══════════════ Persistencia por lote + undo compuesto ═══════════════

        private void ProgramarFlushFormato()
        {
            if (_flushFormatoProgramado) return;
            _flushFormatoProgramado = true;

            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(FlushFormatoPendiente));
            else
                FlushFormatoPendiente();
        }

        // Ctrl+Z / Ctrl+Y deshacen/rehacen el último lote de formato de celdas de la
        // explosión. Se replica el patrón de FormPresupuesto; el grid es de solo
        // lectura, así que nunca queda en edición.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && !dgvExplosion.IsCurrentCellInEditMode && _undoManager.CanUndo)
            {
                try { _undoManager.Undo(); }
                catch (FormatoCeldasPersistenceException ex) { FormatoCeldasPersistence.ManejarErrorDeFormato(ex); }
                return true;
            }

            if (keyData == (Keys.Control | Keys.Y) && !dgvExplosion.IsCurrentCellInEditMode && _undoManager.CanRedo)
            {
                try { _undoManager.Redo(); }
                catch (FormatoCeldasPersistenceException ex) { FormatoCeldasPersistence.ManejarErrorDeFormato(ex); }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
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
                $"Formato de {cambios.Count} celda(s) en explosión",
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
                _proyectoId,
                FormatoTipoReporte,
                ConstruirLoteCambios(cambios, aplicarAntes),
                _undoManager);

            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, aplicarAntes ? cambio.Antes : cambio.Despues);

            dgvExplosion.Invalidate();
        }

        private void RevertirCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, cambio.Antes);

            dgvExplosion.Invalidate();
        }

        private void EstablecerOverlayEnMemoria(ReportCellAddress direccion, ReportStyleOverlay? overlay)
        {
            if (overlay is null)
                _overlaysFormato.Remove(direccion);
            else
                _overlaysFormato[direccion] = overlay;
        }

        // ═══════════════ Snapshot neutral para PDF/Excel ═══════════════

        /// <summary>
        /// Construye el snapshot neutral de columnas del reporte de Explosión a
        /// partir de la configuración persistida y lo puebla con los overrides de
        /// celda/rol del usuario (mecanismo de 4.2). NO lee del <c>DataGridView</c>.
        /// </summary>
        internal ReportColumnSnapshot ConstruirSnapshotExplosion(IEnumerable<ColumnaExplosion>? columnas)
        {
            var snapshot = ExplosionExportResolver.BuildSnapshot(
                _proyectoId,
                lblTitulo.Text,
                columnas,
                FormatoHelper.DecimalesCantidad,
                FormatoHelper.DecimalesImporte,
                FormatoHelper.DecimalesPorcentaje);

            return ExplosionReportSnapshotBuilder.ConEstilos(snapshot, FormatoService);
        }
    }
}
