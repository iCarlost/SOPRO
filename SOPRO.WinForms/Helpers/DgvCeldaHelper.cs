using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Aplica a cualquier DataGridView:
    ///   • Color suave de selección.
    ///   • Borde azul en la celda activa.
    ///   • Selección + Ctrl+C con semántica de Excel (H8):
    ///       - Si hay CELDAS seleccionadas (una o varias, incluso en filas
    ///         distintas) se copia EXACTAMENTE ese bloque de celdas como TSV.
    ///       - Si se seleccionó(n) FILA(S) COMPLETA(S) por el ENCABEZADO de fila
    ///         se copian esas filas completas (todas las columnas visibles).
    ///
    /// IMPORTANTE: bajo <see cref="DataGridViewSelectionMode.CellSelect"/> (formato
    /// por celda) WinForms NO selecciona la fila al pulsar el encabezado
    /// (<c>OnRowHeaderMouseDown</c> hace <c>break</c> para CellSelect) y deja
    /// <see cref="DataGridView.SelectedRows"/> siempre vacío. Por eso este helper
    /// selecciona la fila explícitamente al clicar el encabezado y detecta las
    /// filas completas por sus celdas.
    ///
    /// Uso: DgvCeldaHelper.Aplicar(miDgv);
    /// </summary>
    public static class DgvCeldaHelper
    {
        private static readonly Color ColorSeleccionFila = SoproTheme.Colors.SelBg;
        private static readonly Color ColorBordeCelda = Color.FromArgb(0, 120, 215);

        // Rastrear si el último clic fue en un RowHeader (por instancia de DGV)
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridView, RowHeaderState>
            _estados = new();

        private class RowHeaderState
        {
            public bool ClicoEnRowHeader;
            public bool Inicializado;
            public DataGridViewTextBoxEditingControl? EditorActivo;
            public Color ColorBorde = ColorBordeCelda;
        }

        public static void Aplicar(DataGridView dgv, bool conMenuCopia = true)
            => Aplicar(dgv, conMenuCopia, ColorSeleccionFila, ColorBordeCelda, mostrarRowHeaders: true);

        /// <summary>
        /// Variante que permite personalizar los colores de seleccion/borde activo
        /// y omitir los encabezados de fila, sin alterar el comportamiento por
        /// defecto que usan las demas pantallas.
        /// </summary>
        public static void Aplicar(DataGridView dgv, bool conMenuCopia, Color colorSeleccion, Color colorBorde, bool mostrarRowHeaders)
        {
            if (dgv == null) return;

            if (!_estados.TryGetValue(dgv, out var estado))
            {
                estado = new RowHeaderState();
                _estados.Add(dgv, estado);
            }

            estado.ColorBorde = colorBorde;

            // Color de selección suave.
            // Patrón read-mutate-assign-back: en este runtime el getter de
            // DefaultCellStyle devuelve una copia desechable, por lo que mutar la
            // propiedad directamente se pierde. Hay que reasignar SIEMPRE el objeto
            // style al grid para que la configuración persista.
            var baseStyle = dgv.DefaultCellStyle;
            baseStyle.SelectionBackColor = colorSeleccion;
            baseStyle.SelectionForeColor = Color.Black;
            dgv.DefaultCellStyle = baseStyle;

            // Se aplica el mismo patrón a las filas alternas: el zebra
            // (AlternatingRowsDefaultCellStyle.BackColor) se impone al pintar la
            // fila seleccionada si su SelectionBackColor queda sin definir, por lo
            // que el fondo de selección "alterna". Igualarlo al estilo normal
            // garantiza fondo uniforme pese al zebra.
            var altStyle = dgv.AlternatingRowsDefaultCellStyle;
            altStyle.SelectionBackColor = colorSeleccion;
            altStyle.SelectionForeColor = Color.Black;
            dgv.AlternatingRowsDefaultCellStyle = altStyle;

            // Deshabilitar el copiado interno del DataGridView.
            // Si no se hace, en modo edición el grid puede copiar la fila/selección completa.
            dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;

            if (mostrarRowHeaders)
            {
                // Habilitar RowHeaders (ancho compacto, estilo limpio)
                dgv.RowHeadersVisible = true;
                dgv.RowHeadersWidth = 20;
                dgv.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 248);
                dgv.RowHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(180, 210, 245);
                dgv.RowHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            }

            if (estado.Inicializado) return;
            estado.Inicializado = true;

            // Clic en el encabezado de fila: marcar el flag y, bajo CellSelect
            // (donde WinForms NO selecciona la fila), seleccionar todas sus celdas
            // para habilitar la copia de fila completa (H8).
            dgv.RowHeaderMouseClick += (s, e) =>
            {
                estado.ClicoEnRowHeader = true;
                if (dgv.SelectionMode == DataGridViewSelectionMode.CellSelect)
                    ManejarClicEnEncabezadoFila(dgv, e.RowIndex);
            };

            // Cualquier clic en celda normal borra el flag
            dgv.CellMouseDown += (s, e) => { if (e.ColumnIndex >= 0) estado.ClicoEnRowHeader = false; };

            // Borde azul en celda activa
            dgv.CellPainting += Dgv_CellPainting;

            // Ctrl+C inteligente
            dgv.KeyDown += Dgv_KeyDown;
            dgv.EditingControlShowing += Dgv_EditingControlShowing;
            dgv.CellEndEdit += (s, e) =>
            {
                if (_estados.TryGetValue((DataGridView)s!, out var st))
                    st.EditorActivo = null;
            };

            // Menú contextual de copia (solo si el form no tiene ya su propio menú contextual)
            if (conMenuCopia)
            {
                dgv.CellMouseDown += (s, e) =>
                {
                    if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0) return;
                    MostrarMenuCopia((DataGridView)s);
                };
            }

            // Redibujar al cambiar celda
            dgv.CurrentCellChanged += (s, e) => dgv.Invalidate();
        }


        private static void Dgv_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (sender is not DataGridView dgv) return;
            if (!_estados.TryGetValue(dgv, out var estado)) return;

            if (estado.EditorActivo != null)
                estado.EditorActivo.KeyDown -= EditorActivo_KeyDown;

            estado.EditorActivo = e.Control as DataGridViewTextBoxEditingControl;
            if (estado.EditorActivo != null)
            {
                estado.ClicoEnRowHeader = false;
                estado.EditorActivo.KeyDown -= EditorActivo_KeyDown;
                estado.EditorActivo.KeyDown += EditorActivo_KeyDown;
            }
        }

        private static void EditorActivo_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.C) return;
            if (sender is not DataGridViewTextBoxEditingControl tb) return;

            string texto = tb.SelectedText;
            if (string.IsNullOrEmpty(texto))
                texto = tb.Text ?? string.Empty;

            if (!string.IsNullOrEmpty(texto))
                Clipboard.SetText(texto);

            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private static void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var dgv = (DataGridView)sender;
            e.Paint(e.CellBounds, DataGridViewPaintParts.All);

            if (dgv.CurrentCell != null &&
                e.RowIndex == dgv.CurrentCell.RowIndex &&
                e.ColumnIndex == dgv.CurrentCell.ColumnIndex)
            {
                var borderColor = _estados.TryGetValue(dgv, out var estado) ? estado.ColorBorde : ColorBordeCelda;
                using var pen = new Pen(borderColor, 2);
                e.Graphics.DrawRectangle(pen,
                    e.CellBounds.Left + 1,
                    e.CellBounds.Top + 1,
                    e.CellBounds.Width - 3,
                    e.CellBounds.Height - 3);
            }

            e.Handled = true;
        }

        private static void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.C) return;

            var dgv = (DataGridView)sender;
            if (dgv.CurrentCell == null && dgv.SelectedCells.Count == 0) return;

            // En modo edición, el TextBox editor se encarga de la copia.
            if (dgv.IsCurrentCellInEditMode)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // Semántica Excel (H8): bloque exacto de celdas o fila(s) completa(s).
            CopiarSeleccionAlPortapapeles(dgv);

            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private static void MostrarMenuCopia(DataGridView dgv)
        {
            if (dgv.CurrentCell == null && dgv.SelectedCells.Count == 0) return;

            string etiqueta = EtiquetaCopiar(dgv);

            var menu = new ContextMenuStrip();

            // "Copiar celda" (celda activa) como ítem separado, salvo que la copia de
            // la selección ya consista exactamente en la celda activa.
            if (etiqueta != "Copiar celda" && dgv.CurrentCell != null)
            {
                var itemCelda = menu.Items.Add("📋  Copiar celda\tCtrl+C");
                itemCelda.Click += (_, __) =>
                {
                    string texto = dgv.CurrentCell.FormattedValue?.ToString() ?? string.Empty;
                    if (texto.Length > 0) Clipboard.SetText(texto);
                };
            }

            var itemSeleccion = menu.Items.Add($"📄  {etiqueta}");
            itemSeleccion.Click += (_, __) => CopiarSeleccionAlPortapapeles(dgv);

            menu.Show(dgv, dgv.PointToClient(Cursor.Position));
        }

        // ═══════════════════════ API reutilizable de copiado (H8) ═══════════════════════

        /// <summary>
        /// Filas seleccionadas COMO FILA COMPLETA (encabezado de fila o selección de
        /// banda), excluyendo la fila nueva y ordenadas por índice.
        ///
        /// Combina <see cref="DataGridView.SelectedRows"/> con las filas que tienen
        /// TODAS sus celdas VISIBLES seleccionadas. Bajo <c>CellSelect</c> el grid
        /// deja <c>SelectedRows</c> siempre vacío, por lo que la fila completa se
        /// detecta por sus celdas (el encabezado las selecciona todas): así el
        /// encabezado habilita la copia de fila completa, sin que una mera selección
        /// de celdas sueltas se expanda a fila.
        /// </summary>
        public static IReadOnlyList<DataGridViewRow> FilasCompletasSeleccionadas(DataGridView grid)
        {
            if (grid is null) return Array.Empty<DataGridViewRow>();

            var indices = new HashSet<int>();

            foreach (DataGridViewRow fila in grid.SelectedRows)
            {
                if (fila is null || fila.IsNewRow || fila.Index < 0) continue;
                indices.Add(fila.Index);
            }

            var visibles = Enumerable.Range(0, grid.ColumnCount)
                .Where(i => grid.Columns[i].Visible)
                .ToList();

            if (visibles.Count > 0)
            {
                var filasConCeldas = new HashSet<int>();
                foreach (DataGridViewCell celda in grid.SelectedCells)
                {
                    if (celda is null || celda.RowIndex < 0) continue;
                    filasConCeldas.Add(celda.RowIndex);
                }

                foreach (var rowIndex in filasConCeldas)
                {
                    if (rowIndex < 0 || rowIndex >= grid.Rows.Count) continue;
                    var fila = grid.Rows[rowIndex];
                    if (fila.IsNewRow) continue;
                    if (visibles.All(c => fila.Cells[c].Selected))
                        indices.Add(rowIndex);
                }
            }

            return indices.OrderBy(i => i).Select(i => grid.Rows[i]).ToList();
        }

        /// <summary>
        /// Construye el TSV EXACTO del bloque de <see cref="DataGridView.SelectedCells"/>:
        /// agrupa por fila (ascendente) y, dentro de cada fila, ordena por
        /// <see cref="DataGridViewColumn.DisplayIndex"/>; celdas separadas por TAB y
        /// filas por <c>\n</c>. Ignora las celdas sin fila/columna (índice &lt; 0).
        /// </summary>
        public static string ConstruirTsvDeCeldasSeleccionadas(DataGridView grid)
        {
            if (grid is null) return string.Empty;

            var celdas = new List<DataGridViewCell>();
            foreach (DataGridViewCell celda in grid.SelectedCells)
            {
                if (celda is null || celda.RowIndex < 0 || celda.ColumnIndex < 0) continue;
                celdas.Add(celda);
            }

            if (celdas.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            bool primeraFila = true;
            foreach (var grupo in celdas.GroupBy(c => c.RowIndex).OrderBy(g => g.Key))
            {
                if (!primeraFila) sb.Append('\n');
                primeraFila = false;

                var valores = grupo
                    .OrderBy(c => grid.Columns[c.ColumnIndex].DisplayIndex)
                    .Select(c => c.FormattedValue?.ToString() ?? string.Empty);
                sb.Append(string.Join("\t", valores));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Construye el TSV de FILAS COMPLETAS: una línea por fila con todas las
        /// columnas VISIBLES ordenadas por <see cref="DataGridViewColumn.DisplayIndex"/>.
        /// Excluye la fila nueva. Función pura y testeable sin UI.
        /// </summary>
        public static string ConstruirTsvDeFilas(DataGridView grid, IEnumerable<DataGridViewRow> filas)
        {
            if (grid is null || filas is null) return string.Empty;

            var lista = filas
                .Where(f => f is not null && !f.IsNewRow && f.Index >= 0)
                .OrderBy(f => f.Index)
                .ToList();
            if (lista.Count == 0) return string.Empty;

            var cols = Enumerable.Range(0, grid.ColumnCount)
                .Where(i => grid.Columns[i].Visible)
                .OrderBy(i => grid.Columns[i].DisplayIndex)
                .ToList();

            var sb = new StringBuilder();
            foreach (var fila in lista)
                sb.AppendLine(string.Join("\t", cols.Select(i => fila.Cells[i].FormattedValue?.ToString() ?? string.Empty)));

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Copia al portapapeles siguiendo la semántica de Excel (H8): si hay fila(s)
        /// completa(s) seleccionada(s) → copia esas filas completas; en caso contrario
        /// → copia el bloque exacto de celdas seleccionadas.
        /// </summary>
        public static void CopiarSeleccionAlPortapapeles(DataGridView grid)
        {
            if (grid is null) return;

            var filas = FilasCompletasSeleccionadas(grid);

            // Un clic en el encabezado que (por timing) aún no haya seleccionado la
            // fila: usar la fila de la celda actual como fila completa.
            bool clicEnEncabezado = _estados.TryGetValue(grid, out var estado) && estado.ClicoEnRowHeader;
            if (filas.Count == 0 && clicEnEncabezado
                && grid.CurrentCell != null && grid.CurrentCell.RowIndex >= 0
                && grid.CurrentCell.RowIndex < grid.Rows.Count)
            {
                var filaActual = grid.Rows[grid.CurrentCell.RowIndex];
                if (!filaActual.IsNewRow)
                    filas = new[] { filaActual };
            }

            string texto = filas.Count > 0
                ? ConstruirTsvDeFilas(grid, filas)
                : ConstruirTsvDeCeldasSeleccionadas(grid);

            if (texto.Length > 0)
                Clipboard.SetText(texto);
        }

        /// <summary>
        /// Etiqueta del ítem de copia según la selección actual:
        /// "Copiar fila" / "Copiar N filas" para fila(s) completa(s) y
        /// "Copiar celda" / "Copiar celdas" para selección por celdas.
        /// </summary>
        public static string EtiquetaCopiar(DataGridView grid)
        {
            if (grid is null) return "Copiar celdas";

            int nFilas = FilasCompletasSeleccionadas(grid).Count;
            if (nFilas == 1) return "Copiar fila";
            if (nFilas > 1) return $"Copiar {nFilas} filas";

            int nCeldas = 0;
            foreach (DataGridViewCell celda in grid.SelectedCells)
            {
                if (celda is null || celda.RowIndex < 0) continue;
                nCeldas++;
            }

            return nCeldas > 1 ? "Copiar celdas" : "Copiar celda";
        }

        /// <summary>
        /// Selecciona (o deselecciona) TODAS las celdas de una fila. Necesario porque
        /// bajo <see cref="DataGridViewSelectionMode.CellSelect"/> asignar
        /// <see cref="DataGridViewRow.Selected"/> no tiene efecto.
        /// </summary>
        public static void SeleccionarFilaCompleta(DataGridView grid, int rowIndex, bool seleccionar = true)
        {
            if (grid is null || rowIndex < 0 || rowIndex >= grid.Rows.Count) return;
            foreach (DataGridViewCell celda in grid.Rows[rowIndex].Cells)
                celda.Selected = seleccionar;
        }

        /// <summary>
        /// Indica si <paramref name="rowIndex"/> ya forma parte de la selección
        /// actual del grid (filas completas o celdas). Los catálogos lo usan para no
        /// destruir una selección múltiple cuando se abre el menú contextual con clic
        /// derecho sobre una fila ya seleccionada.
        /// </summary>
        internal static bool ClicDentroDeSeleccion(DataGridView grid, int rowIndex)
        {
            if (grid is null || rowIndex < 0) return false;
            return SeleccionGridHelper.FilasSeleccionadas(grid).Any(r => r.Index == rowIndex);
        }

        // ═══════════════════════ Helpers internos de encabezado de fila ═══════════════════════

        /// <summary>
        /// Gestiona el clic en el encabezado de fila bajo CellSelect: selecciona la
        /// fila completa (todas sus celdas), respetando Ctrl (alternar) y Shift
        /// (rango desde la celda activa). Sin modificadores reemplaza la selección.
        /// </summary>
        private static void ManejarClicEnEncabezadoFila(DataGridView dgv, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgv.Rows.Count) return;

            var mods = Control.ModifierKeys;
            if ((mods & Keys.Control) == Keys.Control)
            {
                bool yaSeleccionada = FilaCompletaSeleccionada(dgv.Rows[rowIndex]);
                SeleccionarFilaCompleta(dgv, rowIndex, !yaSeleccionada);
            }
            else if ((mods & Keys.Shift) == Keys.Shift
                     && dgv.CurrentCell != null && dgv.CurrentCell.RowIndex >= 0)
            {
                int desde = Math.Min(dgv.CurrentCell.RowIndex, rowIndex);
                int hasta = Math.Max(dgv.CurrentCell.RowIndex, rowIndex);
                dgv.ClearSelection();
                for (int i = Math.Max(0, desde); i <= Math.Min(hasta, dgv.Rows.Count - 1); i++)
                    SeleccionarFilaCompleta(dgv, i, true);
            }
            else
            {
                dgv.ClearSelection();
                SeleccionarFilaCompleta(dgv, rowIndex, true);
            }
        }

        private static bool FilaCompletaSeleccionada(DataGridViewRow fila)
        {
            if (fila.Cells.Count == 0) return false;
            foreach (DataGridViewCell celda in fila.Cells)
                if (!celda.Selected) return false;
            return true;
        }
    }
}
