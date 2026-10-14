using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Pipeline de formato por CELDA (selección tipo Excel) para los grids de
    /// catálogo (Materiales, Mano de Obra, Herramientas y Maquinaria). Replica el
    /// patrón de la Fase 5 (Indirectos / Matrices):
    /// <list type="bullet">
    ///   <item>traduce la selección del grid a direcciones ESTABLES
    ///   (<see cref="ReportCellAddress"/>) con clave de fila <c>INS:{Id}</c> y el
    ///   identificador neutral de la columna (nunca índices visuales);</item>
    ///   <item>mantiene el diccionario en memoria de overrides y el pintado
    ///   inmediato, con UNA sola consulta por (proyecto, tipo);</item>
    ///   <item>persiste por lote con <see cref="FormatoReporteService"/> y agrupa
    ///   todo el lote como UNA acción compuesta de undo;</item>
    ///   <item>el grid usa <c>SelectionMode.CellSelect</c> (selección tipo Excel):
    ///   cada celda seleccionada produce UNA dirección, de modo que el formato NO
    ///   se derrama al resto de la fila.</item>
    /// </list>
    /// Es no-op cuando el catálogo es el maestro (sin proyecto): el ribbon
    /// mantiene el comportamiento por columna.
    /// </summary>
    internal sealed class CatalogoFormatoCeldas
    {
        private readonly DataGridView _grid;
        private readonly int? _proyectoId;
        private readonly string _tipoReporte;
        private readonly string _claveVista;
        private readonly FormatoReporteService _service;
        private readonly UndoManager _undoManager = new();

        private Dictionary<ReportCellAddress, ReportStyleOverlay>? _overlays;
        private readonly List<CambioFormatoSeleccion> _pendientes = new();
        private bool _flushProgramado;

        public CatalogoFormatoCeldas(
            DataGridView grid,
            SOPROContext context,
            int? proyectoId,
            string tipoReporte,
            string claveVista)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (context is null) throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _tipoReporte = tipoReporte ?? throw new ArgumentNullException(nameof(tipoReporte));
            _claveVista = claveVista ?? string.Empty;
            _service = new FormatoReporteService(context);
        }

        /// <summary>
        /// <c>true</c> cuando la última interacción fue sobre el ENCABEZADO de una
        /// columna (destino columna), no sobre el cuerpo del grid.
        /// </summary>
        public bool SeleccionPorEncabezado { get; set; }

        // ── Delegación de IGridSeleccionFormato ────────────────────────────────

        public bool TieneSeleccionCeldas
        {
            get
            {
                if (_proyectoId is not > 0 || SeleccionPorEncabezado) return false;
                foreach (DataGridViewCell celda in _grid.SelectedCells)
                    if (celda.RowIndex >= 0) return true;
                return false;
            }
        }

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
        {
            if (SeleccionPorEncabezado) return Array.Empty<ReportCellAddress>();
            return ConstruirDirecciones(_grid, _grid.SelectedCells.Cast<DataGridViewCell>(), _claveVista);
        }

        public bool EsCeldaFormateable(ReportCellAddress direccion)
        {
            if (_proyectoId is not > 0 || direccion is null) return false;
            if (string.IsNullOrWhiteSpace(direccion.ClaveFila)) return false;
            if (string.IsNullOrWhiteSpace(direccion.NombreColumna)) return false;

            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (string.Equals(NombreColumna(col), direccion.NombreColumna, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
        {
            if (direccion is null) return null;
            var overlays = _overlays ?? CargarDeBaseDatos();
            return overlays.TryGetValue(direccion, out var overlay) ? overlay : null;
        }

        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
        {
            if (direccion is null || overlay is null || _proyectoId is not > 0) return;

            var overlays = _overlays ?? CargarDeBaseDatos();
            var antes = overlays.TryGetValue(direccion, out var previo) ? previo : null;
            overlays[direccion] = overlay;
            _pendientes.Add(new CambioFormatoSeleccion(direccion, antes, overlay));

            _grid.Invalidate();
            ProgramarFlush();
        }

        // ── Carga y pintado ────────────────────────────────────────────────────

        /// <summary>
        /// Recarga (UNA consulta) los overrides persistidos del (proyecto, tipo).
        /// Se invoca al terminar cada binding del grid. Es no-op sin proyecto.
        /// </summary>
        public void Cargar()
        {
            _overlays = CargarDeBaseDatos();
            _grid.Invalidate();
        }

        private Dictionary<ReportCellAddress, ReportStyleOverlay> CargarDeBaseDatos()
        {
            if (_proyectoId is not > 0)
                return new Dictionary<ReportCellAddress, ReportStyleOverlay>();

            try
            {
                return new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    _service.CargarCeldas(_proyectoId.Value, _tipoReporte));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CatalogoFormatoCeldas.Cargar: {ex.Message}");
                return new Dictionary<ReportCellAddress, ReportStyleOverlay>();
            }
        }

        /// <summary>
        /// Aplica, si existe, el override en memoria de la celda sobre el estilo del
        /// evento de formato. Sólo consulta el diccionario en memoria (sin N+1).
        /// </summary>
        public void AplicarOverride(DataGridViewCellFormattingEventArgs e)
        {
            var overlays = _overlays;
            if (overlays is null || overlays.Count == 0) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var clave = ClaveFila(_grid, e.RowIndex);
            var columna = NombreColumna(_grid.Columns[e.ColumnIndex]);
            if (clave is null || columna is null) return;

            var direccion = new ReportCellAddress(_claveVista, clave, columna);
            if (!overlays.TryGetValue(direccion, out var overlay) || overlay is null) return;

            FormPresupuesto.AplicarOverlayACellStyle(e.CellStyle, overlay);
        }

        // ── Persistencia por lote + undo compuesto ─────────────────────────────

        private void ProgramarFlush()
        {
            if (_flushProgramado) return;
            _flushProgramado = true;

            if (_grid.IsHandleCreated && !_grid.IsDisposed)
                _grid.BeginInvoke(new Action(FlushPendiente));
            else
                FlushPendiente();
        }

        private void FlushPendiente()
        {
            _flushProgramado = false;
            if (_pendientes.Count == 0) return;

            var cambios = ColapsarCambios(_pendientes);
            _pendientes.Clear();

            var cambiosUndo = cambios
                .Select(c => new UndoChange(c.Antes, c.Despues))
                .ToList();

            var accion = new CompositeUndoableAction(
                $"Formato de {cambios.Count} celda(s) en catálogo",
                cambiosUndo,
                _ => AplicarCambios(cambios, aplicarAntes: true),
                _ => AplicarCambios(cambios, aplicarAntes: false));

            try
            {
                _undoManager.Execute(accion);
            }
            catch (Exception ex)
            {
                RevertirCambios(cambios);
                MessageBox.Show(
                    $"No se pudo guardar el formato de las celdas:\n{ex.Message}",
                    "Formato",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void AplicarCambios(IReadOnlyList<CambioFormatoSeleccion> cambios, bool aplicarAntes)
        {
            _service.AplicarCeldas(
                _proyectoId!.Value,
                _tipoReporte,
                ConstruirLote(cambios, aplicarAntes, _claveVista));

            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, aplicarAntes ? cambio.Antes : cambio.Despues);

            _grid.Invalidate();
        }

        private void RevertirCambios(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, cambio.Antes);

            _grid.Invalidate();
        }

        private void EstablecerOverlayEnMemoria(ReportCellAddress direccion, ReportStyleOverlay? overlay)
        {
            var overlays = _overlays ?? CargarDeBaseDatos();
            if (overlay is null)
                overlays.Remove(direccion);
            else
                overlays[direccion] = overlay;
        }

        // ── Funciones puras (verificables sin UI) ──────────────────────────────

        /// <summary>
        /// Clave ESTABLE de la fila (<c>INS:{Id}</c>), o <c>null</c> si la fila no
        /// tiene una entidad persistida (fila vacía / sin Id).
        /// </summary>
        public static string? ClaveFila(DataGridView grid, int rowIndex)
        {
            if (grid is null) return null;
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;

            var id = ObtenerIdInsumo(ObtenerEntidadFila(grid.Rows[rowIndex]));
            return id > 0 ? ClaveFilaReporte.Insumo(id) : null;
        }

        /// <summary>
        /// Identificador ESTABLE de la columna (<see cref="ReportColumnDefinition.Identificador"/>),
        /// o <c>null</c> si la columna no es formateable (p. ej. <c>colDummy</c>).
        /// </summary>
        public static string? NombreColumna(DataGridViewColumn columna)
        {
            if (columna is null) return null;
            return NombreColumna(columna.Tag);
        }

        private static string? NombreColumna(object? tag)
        {
            var nombreInterno = tag switch
            {
                ColumnaMaterial cm => cm.NombreInterno,
                ColumnaManoObra cm => cm.NombreInterno,
                ColumnaHerramienta ch => ch.NombreInterno,
                ColumnaMaquinaria cm => cm.NombreInterno,
                _ => null,
            };

            if (string.IsNullOrWhiteSpace(nombreInterno)) return null;
            return ReportColumnDefinitionMapper.NormalizarIdentificador(nombreInterno);
        }

        /// <summary>
        /// Convierte las celdas seleccionadas en direcciones estables, descartando
        /// filas sin entidad y columnas no formateables. NO deduplica: el
        /// coordinador (Ctrl / rangos discontinuos) deduplica y filtra.
        /// </summary>
        public static IReadOnlyList<ReportCellAddress> ConstruirDirecciones(
            DataGridView grid,
            IEnumerable<DataGridViewCell> celdas,
            string claveVista)
        {
            var direcciones = new List<ReportCellAddress>();
            if (grid is null || celdas is null) return direcciones;

            foreach (var celda in celdas)
            {
                if (celda is null || celda.RowIndex < 0 || celda.ColumnIndex < 0) continue;

                var clave = ClaveFila(grid, celda.RowIndex);
                if (clave is null) continue;

                var columna = NombreColumna(grid.Columns[celda.ColumnIndex]);
                if (columna is null) continue;

                direcciones.Add(new ReportCellAddress(claveVista ?? string.Empty, clave, columna));
            }

            return direcciones;
        }

        /// <summary>
        /// Construye el lote que <see cref="FormatoReporteService.AplicarCeldas"/>
        /// persiste: con <paramref name="aplicarAntes"/> se toma el estado "antes"
        /// (undo) y en otro caso el "después" (do/redo). Un overlay <c>null</c>
        /// indica BORRAR el override de esa celda.
        /// </summary>
        public static IReadOnlyList<CambioFormatoCelda> ConstruirLote(
            IEnumerable<CambioFormatoSeleccion> cambios,
            bool aplicarAntes,
            string claveVista)
        {
            var lote = new List<CambioFormatoCelda>();
            if (cambios is null) return lote;

            foreach (var cambio in cambios)
            {
                if (cambio?.Direccion is null) continue;

                var vista = string.IsNullOrEmpty(cambio.Direccion.ClaveVista)
                    ? claveVista ?? string.Empty
                    : cambio.Direccion.ClaveVista;

                lote.Add(new CambioFormatoCelda(
                    vista,
                    cambio.Direccion.ClaveFila,
                    cambio.Direccion.NombreColumna,
                    aplicarAntes ? cambio.Antes : cambio.Despues));
            }

            return lote;
        }

        /// <summary>
        /// Colapsa los cambios pendientes a UNA entrada por dirección: el primer
        /// "antes" observado y el último "después".
        /// </summary>
        private static List<CambioFormatoSeleccion> ColapsarCambios(
            IEnumerable<CambioFormatoSeleccion> pendientes)
        {
            var orden = new List<ReportCellAddress>();
            var antes = new Dictionary<ReportCellAddress, ReportStyleOverlay?>();
            var despues = new Dictionary<ReportCellAddress, ReportStyleOverlay>();

            foreach (var cambio in pendientes)
            {
                if (cambio?.Direccion is null) continue;

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

        private static object? ObtenerEntidadFila(DataGridViewRow fila)
        {
            if (fila is null) return null;
            if (fila.Tag is MaterialListItem or ManoDeObra or Herramienta or Maquinaria) return fila.Tag;
            return fila.DataBoundItem;
        }

        private static int ObtenerIdInsumo(object? item) => item switch
        {
            MaterialListItem m => m.Id,
            ManoDeObra m => m.Id,
            Herramienta h => h.Id,
            Maquinaria m => m.Id,
            _ => 0,
        };
    }
}
