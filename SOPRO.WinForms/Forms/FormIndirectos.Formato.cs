using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Implementación de IGridFormato y aplicación de estilos desde la BD.
    /// </summary>
    public partial class FormIndirectos
    {

        public bool GenerarReporteExcel()
        {
            btnExportar_Click(this, EventArgs.Empty);
            return true;
        }
        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaIndirectos _columnaIndRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _columnaIndRibbon = null;
            _columnaRibbon = null;
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            // Mapear índice visual a NombreInterno
            var mapaIdx = new[] { "Grupo", "ImporteMensual", "Duracion", "ImporteTotal" };
            if (colIndex >= 0 && colIndex < mapaIdx.Length)
            {
                var ni = mapaIdx[colIndex];
                var col = cols.FirstOrDefault(c => c.NombreInterno == ni);
                if (col != null)
                {
                    _columnaIndRibbon = col;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = col.Nombre,
                        NombreFuente = col.NombreFuente,
                        TamanoFuente = col.TamanoFuente,
                        Negrita = col.Negrita,
                        Cursiva = col.Cursiva,
                        Alineacion = col.Alineacion,
                        ColorFondo = col.ColorFondo,
                        ColorFuente = col.ColorFuente,
                        WrapTexto = col.WrapTexto,
                        AlineacionVertical = col.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_columnaIndRibbon == null) return;
            CopiarFormato(fmt, _columnaIndRibbon);
            _context.SaveChanges();
            AplicarEstilosDesdeDB();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            foreach (var col in cols)
            {
                CopiarFormato(fmt, col, excluirFondo: true);
            }
            _context.SaveChanges();
            AplicarEstilosDesdeDB();
        }

        private void CopiarFormato(ColumnaPersonalizada fmt, ColumnaIndirectos dest,
                                    bool excluirFondo = false)
        {
            dest.NombreFuente = fmt.NombreFuente;
            dest.TamanoFuente = fmt.TamanoFuente;
            dest.Negrita = fmt.Negrita;
            dest.Cursiva = fmt.Cursiva;
            dest.Alineacion = fmt.Alineacion;
            dest.ColorFuente = fmt.ColorFuente;
            if (!excluirFondo) dest.ColorFondo = fmt.ColorFondo;
            dest.WrapTexto = fmt.WrapTexto;
            dest.AlineacionVertical = fmt.AlineacionVertical;
            dest.FechaModificacion = DateTime.Now;
        }

        private void AplicarEstilosDesdeDB()
        {
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            var mapaNames = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Grupo",          "colGrupo"          },
                { "ImporteMensual", "colImporteMensual"  },
                { "Duracion",       "colDuracion"        },
                { "ImporteTotal",   "colImporteTotal"    },
            };
            foreach (var col in cols)
            {
                if (!mapaNames.TryGetValue(col.NombreInterno, out string colName)) continue;
                AplicarEstiloAGrid(dgvOficinaCentral, colName, col);
                AplicarEstiloAGrid(dgvCampo, colName, col);
            }
            dgvOficinaCentral.Invalidate();
            dgvCampo.Invalidate();
        }

        private static void AplicarEstiloAGrid(DataGridView dgv, string colName,
                                                ColumnaIndirectos col)
        {
            if (!dgv.Columns.Contains(colName)) return;
            var dgvCol = dgv.Columns[colName] as System.Windows.Forms.DataGridViewTextBoxColumn;
            if (dgvCol == null) return;
            try
            {
                FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                dgvCol.DefaultCellStyle.Font = new Font(col.NombreFuente ?? "Segoe UI", col.TamanoFuente > 0 ? col.TamanoFuente : 9f, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColorInd(col.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColorInd(col.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                dgvCol.Width = col.AnchoColumna;
                dgvCol.Tag = col;
            }
            catch { }
        }

        private static Color TryColorInd(string hex, Color fallback)
        {
            try { return System.Drawing.ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }

        // ═══════════════════ Formato de celdas (paso 5.1) ═══════════════════
        // Mapeo selección → direcciones ESTABLES (G:{id}/IC:{id}), carga única del
        // diccionario en memoria, persistencia por lote con FormatoReporteService y
        // undo/redo como UNA acción compuesta. Mismo patrón que Presupuesto (4.1).

        /// <summary>Tipo de reporte estable de este módulo para la persistencia de formato.</summary>
        internal const string FormatoTipoReporte = "Indirectos";

        /// <summary>
        /// Vista por defecto de indirectos. Se usa "" (comodín de vista) para que el
        /// override guardado desde el grid también resuelva en cualquier vista del
        /// reporte (ReportCellStyleResolver trata "" como comodín).
        /// </summary>
        internal const string FormatoVistaIndirectos = "";

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
                ["colGrupo"] = "Grupo",
                ["colImporteMensual"] = "ImporteMensual",
                ["colDuracion"] = "Duracion",
                ["colImporteTotal"] = "ImporteTotal",
            };

        private System.Collections.Generic.IEnumerable<DataGridView> GridsFormato()
        {
            yield return dgvOficinaCentral;
            yield return dgvCampo;
        }

        private DataGridView? GridSeleccionActual()
        {
            if (dgvOficinaCentral.ContainsFocus) return dgvOficinaCentral;
            if (dgvCampo.ContainsFocus) return dgvCampo;
            if (dgvOficinaCentral.SelectedCells.Count > 0) return dgvOficinaCentral;
            if (dgvCampo.SelectedCells.Count > 0) return dgvCampo;
            return null;
        }

        /// <summary>
        /// Clave ESTABLE de la fila para el formato de celdas, o <c>null</c> si la
        /// fila no tiene una entidad persistida (sin tipo/id). Los grupos usan
        /// <c>G:{Id}</c>; los conceptos <c>IC:{Id}</c>.
        /// </summary>
        internal static string? ObtenerClaveFilaFormato(DataGridView grid, int rowIndex)
        {
            if (grid is null) return null;
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;
            if (!grid.Columns.Contains("colTipo") || !grid.Columns.Contains("colId")) return null;

            string tipo = grid.Rows[rowIndex].Cells["colTipo"].Value?.ToString() ?? string.Empty;
            if (!int.TryParse(grid.Rows[rowIndex].Cells["colId"].Value?.ToString(), out int id) || id <= 0)
                return null;

            return tipo.Trim().ToUpperInvariant() switch
            {
                "GRUPO" => ClaveFilaReporte.GrupoIndirecto(id),
                "CONCEPTO" => ClaveFilaReporte.ConceptoIndirecto(id),
                _ => null,
            };
        }

        /// <summary>
        /// Identificador ESTABLE de la columna, o <c>null</c> si la columna no es
        /// formateable (p. ej. las auxiliares ocultas colId/colTipo).
        /// </summary>
        internal static string? ObtenerNombreColumnaFormato(DataGridView grid, int columnIndex)
        {
            if (grid is null) return null;
            if (columnIndex < 0 || columnIndex >= grid.Columns.Count) return null;

            return MapaColumnasFormato.TryGetValue(grid.Columns[columnIndex].Name, out var nombreInterno)
                ? nombreInterno
                : null;
        }

        /// <summary>
        /// Convierte las celdas seleccionadas en direcciones estables, descartando
        /// filas sin entidad y columnas no formateables. NO deduplica: el coordinador
        /// (Ctrl / rangos discontinuos) es quien deduplica y filtra.
        /// </summary>
        internal static IReadOnlyList<ReportCellAddress> ConstruirDireccionesSeleccionadas(
            DataGridView grid,
            System.Collections.Generic.IEnumerable<DataGridViewCell> celdas)
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

                direcciones.Add(new ReportCellAddress(FormatoVistaIndirectos, clave, columna));
            }

            return direcciones;
        }

        /// <summary>
        /// Construye el lote que <see cref="FormatoReporteService.AplicarCeldas"/>
        /// persiste: con <paramref name="aplicarAntes"/> se toma el estado "antes"
        /// (undo) y en otro caso el "después" (do/redo). Un overlay <c>null</c>
        /// indica BORRAR el override de esa celda.
        /// </summary>
        internal static IReadOnlyList<CambioFormatoCelda> ConstruirLoteCambios(
            System.Collections.Generic.IEnumerable<CambioFormatoSeleccion> cambios,
            bool aplicarAntes)
        {
            var lote = new List<CambioFormatoCelda>();
            if (cambios is null) return lote;

            foreach (var cambio in cambios)
            {
                if (cambio is null || cambio.Direccion is null) continue;

                var vista = string.IsNullOrEmpty(cambio.Direccion.ClaveVista)
                    ? FormatoVistaIndirectos
                    : cambio.Direccion.ClaveVista;

                lote.Add(new CambioFormatoCelda(
                    vista,
                    cambio.Direccion.ClaveFila,
                    cambio.Direccion.NombreColumna,
                    aplicarAntes ? cambio.Antes : cambio.Despues));
            }

            return lote;
        }

        // ── IGridSeleccionFormato ─────────────────────────────────────────────

        public bool TieneSeleccionCeldas
        {
            get
            {
                if (_seleccionPorEncabezado) return false;
                foreach (var grid in GridsFormato())
                    foreach (DataGridViewCell celda in grid.SelectedCells)
                        if (celda.RowIndex >= 0) return true;
                return false;
            }
        }

        public IReadOnlyList<ReportCellAddress> ObtenerDireccionesSeleccionadas()
        {
            if (_seleccionPorEncabezado) return Array.Empty<ReportCellAddress>();
            var grid = GridSeleccionActual();
            return grid is null
                ? Array.Empty<ReportCellAddress>()
                : ConstruirDireccionesSeleccionadas(grid, grid.SelectedCells.Cast<DataGridViewCell>());
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

            var antes = _overlaysFormato.TryGetValue(direccion, out var previo) ? previo : null;
            _overlaysFormato[direccion] = overlay;
            _pendientesFormato.Add(new CambioFormatoSeleccion(direccion, antes, overlay));

            dgvOficinaCentral.Invalidate();
            dgvCampo.Invalidate();
            ProgramarFlushFormato();
        }

        private void DgvIndirectos_CellMouseDownFormato(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _seleccionPorEncabezado = false;
            else if (e.ColumnIndex >= 0) _seleccionPorEncabezado = true;
        }

        private void DgvIndirectos_SelectionChangedFormato(object? sender, EventArgs e)
        {
            if (_seleccionPorEncabezado) return;
            if (_columnaRibbon is null) return;

            // Al interactuar con el cuerpo del grid, el destino deja de ser la
            // columna y pasa a ser la selección de celdas.
            _columnaRibbon = null;
            _columnaIndRibbon = null;
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Estilos por ROL de fila (default + override persistido) ───────────

        /// <summary>
        /// Overrides de estilo por rol persistidos del (proyecto, tipo), cargados
        /// con UNA sola consulta y memoizados por proyecto. El pintado los combina
        /// sobre los defaults de <see cref="EstilosFilaPredeterminados"/>
        /// (override del usuario &gt; default).
        /// </summary>
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
                System.Diagnostics.Debug.WriteLine($"ObtenerEstilosFilaUsuario: {ex.Message}");
                _estilosFilaFormato = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);
            }

            _estilosFilaFormatoProyectoId = _proyecto.Id;
            return _estilosFilaFormato;
        }

        /// <summary>
        /// Rol funcional de fila (GRUPO→Grupo, CONCEPTO→Concepto) del tipo del grid.
        /// Coincide con <c>IndirectosReportSnapshotBuilder.RolFila</c> para PDF/Excel.
        /// </summary>
        internal static string? ObtenerRolFilaFormato(string? tipo)
            => Application.UseCases.Reporting.IndirectosReportSnapshotBuilder.RolFila(tipo);

        /// <summary>
        /// Vuelca el estilo EFECTIVO del rol (default + override persistido) sobre el
        /// estilo de la celda. Sólo pisa las propiedades presentes en el overlay, de
        /// modo que las columnas conservan su fondo/alineación cuando el rol no los
        /// impone (p. ej. la columna calculada gris de Importe Total).
        /// </summary>
        internal static void AplicarEstiloRolIndirectos(
            DataGridViewCellStyle estilo,
            Font fontBase,
            string? tipo,
            IReadOnlyDictionary<string, ReportStyleOverlay>? estilosFilaUsuario)
        {
            if (estilo is null) return;

            if (estilo.Font is null) estilo.Font = fontBase;

            // La fusión del nivel de rol se hace con la MISMA semántica del resolver
            // neutral compartido (punto único de precedencia).
            var overlay = ReportCellStyleResolver.Combinar(
                null,
                EstilosFilaPredeterminados.CombinarIndirectos(
                    ObtenerRolFilaFormato(tipo), estilosFilaUsuario));

            FormPresupuesto.AplicarOverlayACellStyle(estilo, overlay);
        }

        /// <summary>
        /// Devuelve, si existe, el override en memoria de la celda (fila × columna),
        /// o <c>null</c>. Sólo consulta el diccionario en memoria: no hace ninguna
        /// consulta a la base de datos por celda. La fusión con el estilo de rol la
        /// resuelve <see cref="ReportCellStyleResolver"/> en el punto de pintado.
        /// </summary>
        private ReportStyleOverlay? ObtenerOverlayFormatoCeldaIndirectos(
            DataGridView grid,
            DataGridViewCellFormattingEventArgs e)
        {
            if (_overlaysFormato.Count == 0) return null;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return null;

            var clave = ObtenerClaveFilaFormato(grid, e.RowIndex);
            var columna = ObtenerNombreColumnaFormato(grid, e.ColumnIndex);
            if (clave is null || columna is null) return null;

            var direccion = new ReportCellAddress(FormatoVistaIndirectos, clave, columna);
            return _overlaysFormato.TryGetValue(direccion, out var overlay) ? overlay : null;
        }

        // ── Persistencia por lote + undo compuesto ─────────────────────────────

        /// <summary>
        /// Carga (UNA sola consulta por proyecto/tipo) los overrides persistidos en
        /// el diccionario en memoria que consume el pintado. Se llama al cargar los
        /// grupos; nunca por celda.
        /// </summary>
        private void CargarOverlaysFormato()
        {
            try
            {
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    FormatoService.CargarCeldas(_proyecto.Id, FormatoTipoReporte));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CargarOverlaysFormato: {ex.Message}");
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
            }
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
                $"Formato de {cambios.Count} celda(s) en indirectos",
                cambiosUndo,
                _ => AplicarCambiosFormato(cambios, aplicarAntes: true),
                _ => AplicarCambiosFormato(cambios, aplicarAntes: false));

            try
            {
                // Execute aplica el "después" (persiste + repinta) y sólo si tiene
                // éxito apila la acción; ante un fallo no queda nada a medias.
                _undoManager.Execute(accion);
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
            System.Collections.Generic.IEnumerable<CambioFormatoSeleccion> pendientes)
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
            FormatoService.AplicarCeldas(
                _proyecto.Id,
                FormatoTipoReporte,
                ConstruirLoteCambios(cambios, aplicarAntes));

            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, aplicarAntes ? cambio.Antes : cambio.Despues);

            dgvOficinaCentral.Invalidate();
            dgvCampo.Invalidate();
        }

        private void RevertirCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, cambio.Antes);

            dgvOficinaCentral.Invalidate();
            dgvCampo.Invalidate();
        }

        private void EstablecerOverlayEnMemoria(ReportCellAddress direccion, ReportStyleOverlay? overlay)
        {
            if (overlay is null)
                _overlaysFormato.Remove(direccion);
            else
                _overlaysFormato[direccion] = overlay;
        }

        /// <summary>
        /// Construye el snapshot neutral de columnas del reporte de Indirectos a
        /// partir de la configuración persistida y lo puebla con los overrides de
        /// celda/rol del usuario (mecanismo de 4.2). NO lee del <c>DataGridView</c>.
        /// </summary>
        private Application.Models.Reporting.ReportColumns.ReportColumnSnapshot ConstruirSnapshotIndirectos(
            System.Collections.Generic.IEnumerable<ColumnaIndirectos>? columnas)
        {
            var snapshot = IndirectosExportResolver.BuildSnapshot(
                _proyecto.Id,
                lblTitulo.Text,
                columnas,
                FormatoHelper.DecimalesCantidad,
                FormatoHelper.DecimalesImporte,
                FormatoHelper.DecimalesPorcentaje);

            return Application.UseCases.Reporting.IndirectosReportSnapshotBuilder.ConEstilos(
                snapshot, FormatoService);
        }
    }
}
