using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato por celda/rol del Programa de Obra (paso 6.2). Replica el patrón de
    /// Presupuesto (4.1) e Indirectos/Matrices (5.1): selección del grid → direcciones
    /// ESTABLES (<c>ACT:{Id}</c> actividades, <c>RES:{Clave}</c> agrupadores) →
    /// persistencia por lote con <see cref="FormatoReporteService"/> → undo/redo como
    /// UNA acción compuesta. Los generadores PDF/Excel consumen el snapshot neutral
    /// (con overrides) sin leer el <c>DataGridView</c>.
    /// </summary>
    public partial class FormProgramaObra
    {
        /// <summary>Tipo de reporte estable de este módulo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = "ProgramaObra";

        /// <summary>
        /// Vista por defecto del programa. Se usa "" (comodín de vista) para que el
        /// override guardado desde el grid resuelva en cualquier vista del reporte
        /// (<c>ReportCellStyleResolver</c> trata "" como comodín).
        /// </summary>
        internal const string FormatoVista = "";

        private FormatoReporteService? _formatoReporteService;
        private Dictionary<ReportCellAddress, ReportStyleOverlay>? _overlaysFormato;
        private readonly List<CambioFormatoSeleccion> _pendientesFormato = new();
        private bool _flushFormatoProgramado;
        private bool _seleccionPorEncabezado;

        private Dictionary<string, ReportStyleOverlay>? _estilosFilaFormato;
        private int _estilosFilaFormatoProyectoId = -1;

        /// <summary>
        /// Estilos de usuario "vacíos" que se devuelven mientras el estado persistido
        /// es incierto: se pintan los defaults y NO se memoiza nada.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, ReportStyleOverlay> SinEstilosFilaUsuario =
            new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Historial de undo/redo de la vista (interno para pruebas del módulo: permite
        /// verificar el bloqueo por resultado incierto y su reconciliación).
        /// </summary>
        internal UndoManager HistorialUndo => _undoManager;

        private FormatoReporteService FormatoService
            => _formatoReporteService ??= new FormatoReporteService(_context);

        /// <summary>
        /// Identificadores ESTABLES de las columnas formateables del Programa de Obra.
        /// Coinciden con el identificador neutral (<see cref="ReportColumnDefinition.Identificador"/>)
        /// que emite <c>ProgramaObraReportSnapshotBuilder</c>.
        /// </summary>
        private static readonly HashSet<string> ColumnasFormateables = new(StringComparer.OrdinalIgnoreCase)
        {
            ProgramaObraReportSnapshotBuilder.ColOrden,
            ProgramaObraReportSnapshotBuilder.ColClave,
            ProgramaObraReportSnapshotBuilder.ColDescripcion,
            ProgramaObraReportSnapshotBuilder.ColUnidad,
            ProgramaObraReportSnapshotBuilder.ColPredecesora,
            ProgramaObraReportSnapshotBuilder.ColCantidad,
            ProgramaObraReportSnapshotBuilder.ColFechaInicio,
            ProgramaObraReportSnapshotBuilder.ColFechaFin,
            ProgramaObraReportSnapshotBuilder.ColDuracionDias,
            ProgramaObraReportSnapshotBuilder.ColRendimientoDiario,
            ProgramaObraReportSnapshotBuilder.ColFrentes,
            ProgramaObraReportSnapshotBuilder.ColPrecioUnitario,
            ProgramaObraReportSnapshotBuilder.ColImporte,
            ProgramaObraReportSnapshotBuilder.ColRutaCritica,
        };

        /// <summary>Conecta los eventos del grid con el formato por celda.</summary>
        private void InicializarFormatoProgramaObra()
        {
            // H3: el formato por celda exige selección tipo Excel. El grid se diseñó
            // con FullRowSelect, donde un clic sobre UNA celda selecciona TODA la fila:
            // SelectedCells devolvía todas las columnas y el ribbon aplicaba el
            // override a la fila completa. Se fija CellSelect (+MultiSelect para
            // rango con Shift / multi-rango con Ctrl) igual que Explosión, Indirectos
            // y Presupuesto, de modo que la dirección de celda sea la de la celda
            // realmente seleccionada. El encabezado sigue resolviéndose por COLUMNA.
            ConfigurarSeleccionPorCelda(dgvActividades);

            // Distinguir interacción de cuerpo (=> destino CELDAS) de encabezado
            // (=> destino COLUMNA). CellMouseDown se dispara ANTES del cambio de selección.
            dgvActividades.CellMouseDown += DgvActividades_CellMouseDownFormato;
            dgvActividades.SelectionChanged += DgvActividades_SelectionChangedFormato;
            dgvActividades.CellFormatting += DgvActividades_CellFormattingFormato;
        }

        /// <summary>
        /// Fuerza en el grid la selección POR CELDAS (tipo Excel): fija
        /// <see cref="DataGridViewSelectionMode.CellSelect"/> y habilita
        /// multi-selección para que un rango (Shift) o varios rangos (Ctrl) se
        /// reporten como celdas concretas. Es requisito del formato por celda: con
        /// <c>FullRowSelect</c> el clic sobre una celda selecciona TODA la fila y el
        /// ribbon aplicaría el override a todas sus columnas (hallazgo H3).
        /// </summary>
        internal static void ConfigurarSeleccionPorCelda(DataGridView grid)
        {
            if (grid is null) return;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.MultiSelect = true;
        }

        /// <summary>Mapa estable nombre de columna del grid → identificador neutral.</summary>
        internal static string? ObtenerNombreColumnaFormato(DataGridView grid, int columnIndex)
        {
            if (grid is null || columnIndex < 0 || columnIndex >= grid.Columns.Count) return null;
            var nombre = grid.Columns[columnIndex].Name;
            return ColumnasFormateables.Contains(nombre) ? nombre : null;
        }

        /// <summary>
        /// Clave ESTABLE de la fila para el formato de celdas, o <c>null</c> si la
        /// fila no tiene una entidad persistida. Los agrupadores usan
        /// <c>RES:{ConceptoPresupuestoId}</c> (fallback <c>RES:{Clave}</c>) y las
        /// actividades <c>ACT:{Id}</c>.
        /// </summary>
        internal static string? ObtenerClaveFilaFormato(DataGridView grid, int rowIndex)
        {
            if (grid is null) return null;
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;
            var fila = grid.Rows[rowIndex];
            if ((fila.DataBoundItem as ActivityGridRowDto ?? fila.Tag as ActivityGridRowDto) is not ActivityGridRowDto dto)
                return null;
            if (!dto.EsResumen && dto.Id <= 0) return null;

            return ProgramaObraReportSnapshotBuilder.ClaveFila(
                dto.EsResumen, dto.ConceptoPresupuestoId ?? 0, dto.Id, dto.Clave);
        }

        /// <summary>
        /// Convierte las celdas seleccionadas en direcciones estables, descartando
        /// filas sin entidad y columnas no formateables. NO deduplica: el coordinador
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

                direcciones.Add(new ReportCellAddress(FormatoVista, clave, columna));
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
            => FormatoLoteCambios.Construir(cambios, aplicarAntes, FormatoVista);

        // ── IGridSeleccionFormato ─────────────────────────────────────────────

        public bool TieneSeleccionCeldas
        {
            get
            {
                if (_seleccionPorEncabezado) return false;
                foreach (DataGridViewCell celda in dgvActividades.SelectedCells)
                    if (celda.RowIndex >= 0) return true;
                return false;
            }
        }

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
        {
            if (_seleccionPorEncabezado) return Array.Empty<ReportCellAddress>();
            return ConstruirDireccionesSeleccionadas(
                dgvActividades,
                dgvActividades.SelectedCells.Cast<DataGridViewCell>());
        }

        public bool EsCeldaFormateable(ReportCellAddress direccion)
        {
            if (direccion is null) return false;
            if (string.IsNullOrWhiteSpace(direccion.ClaveFila)) return false;
            return ColumnasFormateables.Contains(direccion.NombreColumna);
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

            dgvActividades.Invalidate();
            ProgramarFlushFormato();
        }

        private void DgvActividades_CellMouseDownFormato(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _seleccionPorEncabezado = false;
            else if (e.ColumnIndex >= 0) _seleccionPorEncabezado = true;
        }

        private void DgvActividades_SelectionChangedFormato(object? sender, EventArgs e)
        {
            if (_seleccionPorEncabezado) return;
            if (_columnaRibbon is null) return;

            // Al interactuar con el cuerpo del grid, el destino deja de ser la
            // columna y pasa a ser la selección de celdas.
            _columnaRibbon = null;
            _colProgRibbon = null;
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Estilos por ROL de fila (default + override persistido) ───────────

        /// <summary>
        /// Overrides de estilo por rol persistidos del (proyecto, tipo), cargados con
        /// UNA sola consulta y memoizados por proyecto. El pintado los combina sobre
        /// los defaults de <see cref="EstilosFilaPredeterminados"/> (override &gt; default).
        /// </summary>
        internal IReadOnlyDictionary<string, ReportStyleOverlay> ObtenerEstilosFilaUsuario()
        {
            // Estado INCIERTO: ni memoria ni caché parcial. Los estilos se vuelven a
            // leer SÓLO en la recarga completa (celdas + estilos) que reconcilia.
            if (_undoManager.RequiresReconciliation)
                return SinEstilosFilaUsuario;

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
                System.Diagnostics.Debug.WriteLine($"ObtenerEstilosFilaUsuario(ProgramaObra): {ex.Message}");
                _estilosFilaFormato = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);
            }

            _estilosFilaFormatoProyectoId = _proyecto.Id;
            return _estilosFilaFormato;
        }

        /// <summary>Rol funcional de fila del Programa de Obra (agrupador → Resumen).</summary>
        internal static string RolFilaFormato(bool esResumen)
            => ProgramaObraReportSnapshotBuilder.RolFila(esResumen);

        /// <summary>
        /// Vuelca el estilo EFECTIVO del rol (default + override persistido) sobre el
        /// estilo de la celda. Sólo pisa las propiedades presentes en el overlay.
        /// </summary>
        internal static void AplicarEstiloRolProgramaObra(
            DataGridViewCellStyle estilo,
            Font fontBase,
            bool esResumen,
            IReadOnlyDictionary<string, ReportStyleOverlay>? estilosFilaUsuario)
        {
            if (estilo is null) return;
            if (estilo.Font is null) estilo.Font = fontBase;

            // La fusión del nivel de rol se hace con la MISMA semántica del resolver
            // neutral compartido (punto único de precedencia).
            var overlay = ReportCellStyleResolver.Combinar(
                null,
                EstilosFilaPredeterminados.CombinarProgramas(
                    RolFilaFormato(esResumen), estilosFilaUsuario));

            FormPresupuesto.AplicarOverlayACellStyle(estilo, overlay);
        }

        private void DgvActividades_CellFormattingFormato(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.CellStyle is null) return;
            if (dgvActividades.Rows[e.RowIndex].DataBoundItem is not ActivityGridRowDto dto) return;

            // Precedencia ÚNICA (override de celda > estilo de rol, default + override
            // persistido) resuelta por el resolver neutral compartido; la UI no
            // recompone la cascada en dos pasos.
            if (e.CellStyle.Font is null) e.CellStyle.Font = dgvActividades.Font;

            var rolOverlay = EstilosFilaPredeterminados.CombinarProgramas(
                RolFilaFormato(dto.EsResumen), ObtenerEstilosFilaUsuario());
            var celdaOverlay = ObtenerOverlayFormatoCeldaProgramaObra(dgvActividades, e);

            var overlay = ReportCellStyleResolver.Combinar(rolOverlay, celdaOverlay);
            FormPresupuesto.AplicarOverlayACellStyle(e.CellStyle, overlay);
        }

        /// <summary>
        /// Devuelve, si existe, el override en memoria de la celda (fila × columna),
        /// o <c>null</c>. Sólo consulta el diccionario en memoria. La fusión con el
        /// estilo de rol la resuelve <see cref="ReportCellStyleResolver"/>.
        /// </summary>
        private ReportStyleOverlay? ObtenerOverlayFormatoCeldaProgramaObra(
            DataGridView grid,
            DataGridViewCellFormattingEventArgs e)
        {
            var overlays = _overlaysFormato;
            if (overlays is null || overlays.Count == 0) return null;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return null;

            var clave = ObtenerClaveFilaFormato(grid, e.RowIndex);
            var columna = ObtenerNombreColumnaFormato(grid, e.ColumnIndex);
            if (clave is null || columna is null) return null;

            var direccion = new ReportCellAddress(FormatoVista, clave, columna);
            return overlays.TryGetValue(direccion, out var overlay) ? overlay : null;
        }

        // ── Persistencia por lote + undo compuesto ─────────────────────────────

        /// <summary>
        /// Carga (UNA sola consulta por proyecto/tipo) los overrides persistidos en el
        /// diccionario en memoria que consume el pintado.
        /// </summary>
        private Dictionary<ReportCellAddress, ReportStyleOverlay> CargarOverlaysFormato()
        {
            // Con la persistencia INCIERTA no se cachea ni se pinta una lectura
            // PARCIAL (sólo celdas, sin estilos de fila): esta ruta no puede
            // reconciliar ni desbloquear por sí sola.
            if (_undoManager.RequiresReconciliation)
                return new Dictionary<ReportCellAddress, ReportStyleOverlay>();

            try
            {
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    FormatoService.CargarCeldas(_proyecto.Id, FormatoTipoReporte));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CargarOverlaysFormato(ProgramaObra): {ex.Message}");
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
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
                $"Formato de {cambios.Count} celda(s) en el programa de obra",
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

            dgvActividades.Invalidate();
        }

        private void RevertirCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, cambio.Antes);

            dgvActividades.Invalidate();
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
        /// Lectura FRESCA del formato persistido del Programa de Obra: overrides de
        /// celda y estilos de fila se releen con un contexto NUEVO (sin caché ni
        /// tracker compartido) y SÓLO si la lectura COMPLETA tuvo éxito se reemplazan
        /// ambas cachés y se reconcilia el historial (desbloquea Undo/Redo y descarta
        /// las acciones obsoletas).
        ///
        /// Es la salida obligatoria tras un resultado INCIERTO de formato: la caché
        /// anterior no es evidencia de nada. Si cualquiera de las dos lecturas falla,
        /// NO se toca ninguna caché y NO se reconcilia: el historial sigue bloqueado
        /// y las rutas de pintado siguen sin cachear datos parciales.
        /// </summary>
        /// <param name="factory">Seam de pruebas; en producción la fábrica del formulario.</param>
        internal bool RecargarFormatoPersistidoDesdeBase(IProjectDbContextFactory? factory = null)
        {
            Dictionary<ReportCellAddress, ReportStyleOverlay> overlays;
            Dictionary<string, ReportStyleOverlay> estilosFila;

            try
            {
                using var contextoFresco = (factory ?? _factory).Create(_context.DatabasePath);
                var servicio = new FormatoReporteService(contextoFresco);

                overlays = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    servicio.CargarCeldas(_proyecto.Id, FormatoTipoReporte));
                estilosFila = new Dictionary<string, ReportStyleOverlay>(
                    servicio.CargarEstilosFila(_proyecto.Id, FormatoTipoReporte),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                // Sin lectura verificada no hay reconciliación: se conserva el bloqueo
                // y las cachés anteriores (no se publica un estado parcial).
                System.Diagnostics.Debug.WriteLine($"RecargarFormatoPersistidoDesdeBase: {ex.Message}");
                return false;
            }

            _overlaysFormato = overlays;
            _estilosFilaFormato = estilosFila;
            _estilosFilaFormatoProyectoId = _proyecto.Id;

            // Los overrides pendientes eran parte del estado incierto: no se vuelcan.
            _pendientesFormato.Clear();

            // Recién con la base releída el historial puede descartarse y desbloquearse.
            FormatoCeldasPersistence.ReconciliarSiCorresponde(_undoManager);

            if (dgvActividades is not null && !dgvActividades.IsDisposed)
                dgvActividades.Invalidate();

            return true;
        }

        /// <summary>
        /// Restablece la apariencia configurada del Programa de Obra del proyecto
        /// actual: elimina los overrides de celda y los estilos de fila persistidos
        /// del par (ProyectoId, <see cref="FormatoTipoReporte"/>) —sólo de ESTE tipo
        /// de reporte— e invalida las cachés en memoria, de modo que el repintado use
        /// los defaults (conceptos con fondo blanco, agrupadores con su color por
        /// defecto). Un fallo al limpiar la persistencia se registra para diagnóstico
        /// y NO interrumpe el flujo de borrado del programa.
        /// </summary>
        internal void RestablecerFormatoProgramaObra()
        {
            try
            {
                FormatoService.EliminarCeldas(_proyecto.Id, FormatoTipoReporte);
                FormatoService.EliminarEstilosFila(_proyecto.Id, FormatoTipoReporte);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestablecerFormatoProgramaObra: {ex.Message}");
            }

            InvalidarCachesFormatoProgramaObra();
        }

        /// <summary>
        /// Descarta las cachés en memoria del formato de Programa de Obra; la próxima
        /// consulta recarga desde la base (ya sin overrides tras el reset).
        /// </summary>
        private void InvalidarCachesFormatoProgramaObra()
        {
            _overlaysFormato = null;
            _estilosFilaFormato = null;
            _estilosFilaFormatoProyectoId = -1;
        }

        /// <summary>
        /// Construye el snapshot neutral del Programa de Obra poblado con los overrides
        /// de celda/rol persistidos. NO lee del <c>DataGridView</c>.
        /// </summary>
        private ReportColumnSnapshot ConstruirSnapshotProgramaObra(string tituloReporte)
        {
            var snapshot = new ProgramaObraReportSnapshotBuilder()
                .Build(_proyecto, tituloReporte, _columnasConfig);

            return ProgramaObraReportSnapshotBuilder.ConEstilos(snapshot, FormatoService);
        }
    }
}
