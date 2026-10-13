using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Configuración del grid y formato de celdas del catálogo.
    /// </summary>
    public partial class FormMatrices
    {

        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvMatrices.AutoGenerateColumns = false;
            dgvMatrices.AllowUserToAddRows = false;
            dgvMatrices.AllowUserToDeleteRows = false;
            dgvMatrices.ReadOnly = true;
            dgvMatrices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMatrices.MultiSelect = true;
            dgvMatrices.BackgroundColor = Color.White;
            dgvMatrices.BorderStyle = BorderStyle.None;
            dgvMatrices.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvMatrices.GridColor = Color.FromArgb(230, 230, 230);
            dgvMatrices.ColumnHeadersHeight = 40;
            dgvMatrices.RowTemplate.Height = 35;
            dgvMatrices.EnableHeadersVisualStyles = false;
            dgvMatrices.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvMatrices.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMatrices.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvMatrices.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMatrices.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvMatrices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvMatrices.ScrollBars = ScrollBars.Both;

            DgvCeldaHelper.Aplicar(dgvMatrices, conMenuCopia: false);

            dgvMatrices.Columns.Clear();
            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    string dataProperty = cfg.NombreInterno switch
                    {
                        "Tipo" => nameof(MatrixGridRowDisplay.TipoTexto),
                        "NumInsumos" => nameof(MatrixGridRowDisplay.NumInsumos),
                        _ => cfg.NombreInterno,
                    };

                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = dataProperty,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvMatrices.Columns.Add(col);
                    AplicarEstiloDesdeColMat(col, cfg);
                }
            }
            else
            {
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = nameof(MatrixGridRowDisplay.Clave), Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = nameof(MatrixGridRowDisplay.Descripcion), Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = nameof(MatrixGridRowDisplay.Unidad), Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTipo", HeaderText = "Tipo", DataPropertyName = nameof(MatrixGridRowDisplay.TipoTexto), Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", DataPropertyName = nameof(MatrixGridRowDisplay.OrigenDetalle), Width = 180, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCostoDirecto", HeaderText = "Costo Directo", DataPropertyName = nameof(MatrixGridRowDisplay.CostoDirecto), Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colComponentes", HeaderText = "# Insumos", DataPropertyName = nameof(MatrixGridRowDisplay.NumInsumos), Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            }

            dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDummy",
                HeaderText = string.Empty,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });

            _cargandoColumnas = false;
        }

        private void DgvMatrices_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaMatriz cfg) return;

                var columnaDb = _context.ColumnasMatriz.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en matrices: {ex.Message}");
            }
        }

        private static void AplicarEstiloDesdeColMat(DataGridViewTextBoxColumn dgvCol, ColumnaMatriz col)
        {
            try
            {
                FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                string fuente = !string.IsNullOrEmpty(col.NombreFuente) ? col.NombreFuente : "Segoe UI";
                float tam = col.TamanoFuente > 0 ? col.TamanoFuente : 9f;
                dgvCol.DefaultCellStyle.Font = new Font(fuente, tam, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColor(col.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColor(col.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                if (!string.IsNullOrEmpty(col.FormatoNumerico))
                    dgvCol.DefaultCellStyle.Format = col.FormatoNumerico;
            }
            catch
            {
            }
        }

        private static DataGridViewContentAlignment AlineacionADGV(AlineacionColumna alin) =>
            alin switch
            {
                AlineacionColumna.Centro => DataGridViewContentAlignment.MiddleCenter,
                AlineacionColumna.Derecha => DataGridViewContentAlignment.MiddleRight,
                AlineacionColumna.Justificado => DataGridViewContentAlignment.MiddleLeft,
                _ => DataGridViewContentAlignment.MiddleLeft,
            };

        private static Color TryColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }

        // ═══════════════════ Formato por celda (paso 5.2) ═══════════════════
        // Selección tipo Excel → direcciones ESTABLES (sin índices visuales) →
        // persistencia por lote con FormatoReporteService (TipoReporte = "APU") →
        // undo/redo como UNA acción compuesta. Se conserva FullRowSelect/MultiSelect/
        // ReadOnly: la selección de filas produce una dirección por celda formateable.

        /// <summary>
        /// Entidad de dominio de la fila (Tag explícito o el <c>Matriz</c> del
        /// <see cref="MatrixGridRowDisplay"/> enlazado).
        /// </summary>
        private static object? ObtenerEntidadFila(DataGridView grid, int rowIndex)
        {
            if (grid is null || rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;
            var fila = grid.Rows[rowIndex];
            if (fila.Tag is MatrixGridRowDisplay tagDisplay) return tagDisplay.Source;
            if (fila.Tag is not null) return fila.Tag;
            if (fila.DataBoundItem is MatrixGridRowDisplay display) return display.Source;
            return fila.DataBoundItem;
        }

        /// <summary>
        /// Clave ESTABLE de la fila para el formato de celdas, o <c>null</c> si la
        /// fila no tiene una entidad persistida. Los conceptos usan
        /// <c>C:{ConceptoPresupuesto.Id}</c>, los componentes del APU
        /// <c>COMP:{ComponenteMatriz.Id}</c> y las matrices del catálogo
        /// <c>M:{Matriz.Id}</c>.
        /// </summary>
        internal static string? ObtenerClaveFilaFormato(DataGridView grid, int rowIndex)
            => ObtenerEntidadFila(grid, rowIndex) switch
            {
                ConceptoPresupuesto concepto when concepto.Id > 0 => ClaveFilaReporte.Concepto(concepto.Id),
                ComponenteMatriz componente when componente.Id > 0 => ClaveFilaReporte.Componente(componente.Id),
                Matriz matriz when matriz.Id > 0 => PrefijoMatriz + matriz.Id,
                _ => null,
            };

        /// <summary>
        /// Identificador ESTABLE de la columna (<see cref="ColumnaMatriz.NombreInterno"/>),
        /// o <c>null</c> si la columna no es formateable (p. ej. el relleno <c>colDummy</c>).
        /// </summary>
        internal static string? ObtenerNombreColumnaFormato(DataGridView grid, int columnIndex)
        {
            if (grid is null || columnIndex < 0 || columnIndex >= grid.Columns.Count) return null;
            return grid.Columns[columnIndex].Tag is ColumnaMatriz col && !string.IsNullOrWhiteSpace(col.NombreInterno)
                ? col.NombreInterno
                : null;
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
        /// Construye el lote que <see cref="FormatoReporteService.AplicarCeldas"/>
        /// persiste: con <paramref name="aplicarAntes"/> toma el estado "antes" (undo)
        /// y en otro caso el "después" (do/redo).
        /// </summary>
        internal static IReadOnlyList<CambioFormatoCelda> ConstruirLoteCambios(
            IEnumerable<CambioFormatoSeleccion> cambios,
            bool aplicarAntes)
        {
            var lote = new List<CambioFormatoCelda>();
            if (cambios is null) return lote;

            foreach (var cambio in cambios)
            {
                if (cambio?.Direccion is null) continue;

                var vista = string.IsNullOrEmpty(cambio.Direccion.ClaveVista)
                    ? FormatoVista
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
                if (_proyectoId <= 0 || _seleccionPorEncabezado) return false;
                foreach (DataGridViewCell celda in dgvMatrices.SelectedCells)
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
                dgvMatrices,
                dgvMatrices.SelectedCells.Cast<DataGridViewCell>());
        }

        public bool EsCeldaFormateable(ReportCellAddress direccion)
        {
            if (_proyectoId <= 0 || direccion is null) return false;
            if (string.IsNullOrWhiteSpace(direccion.ClaveFila)) return false;
            if (string.IsNullOrWhiteSpace(direccion.NombreColumna)) return false;

            foreach (DataGridViewColumn col in dgvMatrices.Columns)
            {
                if (col.Tag is ColumnaMatriz colDef
                    && string.Equals(colDef.NombreInterno, direccion.NombreColumna, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public ReportStyleOverlay? ObtenerOverlayActual(ReportCellAddress direccion)
        {
            if (direccion is null) return null;
            var overlays = _overlaysFormato ?? CargarOverlaysFormato();
            return overlays.TryGetValue(direccion, out var overlay) ? overlay : null;
        }

        public void AplicarOverlayCelda(ReportCellAddress direccion, ReportStyleOverlay overlay)
        {
            if (direccion is null || overlay is null || _proyectoId <= 0) return;

            var overlays = _overlaysFormato ?? CargarOverlaysFormato();
            var antes = overlays.TryGetValue(direccion, out var previo) ? previo : null;
            overlays[direccion] = overlay;
            _pendientesFormato.Add(new CambioFormatoSeleccion(direccion, antes, overlay));

            dgvMatrices.Invalidate();
            ProgramarFlushFormato();
        }

        // ── Carga y pintado ───────────────────────────────────────────────────

        /// <summary>
        /// Carga (UNA sola consulta por proyecto/tipo) los overrides persistidos en
        /// el diccionario en memoria que consume el pintado. Es no-op cuando el
        /// formulario es el catálogo maestro embebido (sin proyecto).
        /// </summary>
        private Dictionary<ReportCellAddress, ReportStyleOverlay> CargarOverlaysFormato()
        {
            if (_overlaysFormato is not null) return _overlaysFormato;

            if (_proyectoId <= 0)
            {
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
                return _overlaysFormato;
            }

            try
            {
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    _formatoReporteService.CargarCeldas(_proyectoId, FormatoTipoReporte));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CargarOverlaysFormato(matrices): {ex.Message}");
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
            }

            return _overlaysFormato;
        }

        private void DgvMatrices_CellFormattingFormato(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            var overlays = _overlaysFormato;
            if (overlays is null || overlays.Count == 0) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var clave = ObtenerClaveFilaFormato(dgvMatrices, e.RowIndex);
            var columna = ObtenerNombreColumnaFormato(dgvMatrices, e.ColumnIndex);
            if (clave is null || columna is null) return;

            var direccion = new ReportCellAddress(FormatoVista, clave, columna);
            if (!overlays.TryGetValue(direccion, out var overlay) || overlay is null) return;

            FormPresupuesto.AplicarOverlayACellStyle(e.CellStyle, overlay);
        }

        private void DgvMatrices_CellMouseDownFormato(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0) _seleccionPorEncabezado = false;
            else if (e.ColumnIndex >= 0) _seleccionPorEncabezado = true;
        }

        private void DgvMatrices_KeyDownFormato(object? sender, KeyEventArgs e)
        {
            if (!e.Control) return;

            if (e.KeyCode == Keys.Z && _undoManager.CanUndo)
            {
                _undoManager.Undo();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Y && _undoManager.CanRedo)
            {
                _undoManager.Redo();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // ── Persistencia + undo compuesto ─────────────────────────────────────

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
                $"Formato de {cambios.Count} celda(s) en matrices",
                cambiosUndo,
                _ => AplicarCambiosFormato(cambios, aplicarAntes: true),
                _ => AplicarCambiosFormato(cambios, aplicarAntes: false));

            try
            {
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

        private static List<CambioFormatoSeleccion> ColapsarCambiosFormato(
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

        private void AplicarCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios, bool aplicarAntes)
        {
            _formatoReporteService.AplicarCeldas(
                _proyectoId,
                FormatoTipoReporte,
                ConstruirLoteCambios(cambios, aplicarAntes));

            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, aplicarAntes ? cambio.Antes : cambio.Despues);

            dgvMatrices.Invalidate();
        }

        private void RevertirCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, cambio.Antes);

            dgvMatrices.Invalidate();
        }

        private void EstablecerOverlayEnMemoria(ReportCellAddress direccion, ReportStyleOverlay? overlay)
        {
            var overlays = _overlaysFormato ?? CargarOverlaysFormato();
            if (overlay is null)
                overlays.Remove(direccion);
            else
                overlays[direccion] = overlay;
        }
    }
}
