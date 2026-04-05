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
    ///   • Color suave de selección de fila.
    ///   • Borde azul en la celda activa.
    ///   • Ctrl+C inteligente:
    ///       - 1 fila seleccionada normalmente  → copia solo la celda activa.
    ///       - Múltiples filas (Shift/Ctrl+clic) → copia todas las filas completas (TSV).
    ///       - Clic en RowHeader                → copia esa fila completa (TSV).
    /// Uso: DgvCeldaHelper.Aplicar(miDgv);
    /// </summary>
    public static class DgvCeldaHelper
    {
        private static readonly Color ColorSeleccionFila = Color.FromArgb(230, 240, 255);
        private static readonly Color ColorBordeCelda    = Color.FromArgb(0, 120, 215);

        // Rastrear si el último clic fue en un RowHeader (por instancia de DGV)
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridView, RowHeaderState>
            _estados = new();

        private class RowHeaderState
        {
            public bool ClicoEnRowHeader;
            public bool Inicializado;
            public DataGridViewTextBoxEditingControl? EditorActivo;
        }

        public static void Aplicar(DataGridView dgv, bool conMenuCopia = true)
        {
            if (dgv == null) return;

            if (!_estados.TryGetValue(dgv, out var estado))
            {
                estado = new RowHeaderState();
                _estados.Add(dgv, estado);
            }

            // Color de selección suave
            dgv.DefaultCellStyle.SelectionBackColor = ColorSeleccionFila;
            dgv.DefaultCellStyle.SelectionForeColor = Color.Black;

            // Deshabilitar el copiado interno del DataGridView.
            // Si no se hace, en modo edición el grid puede copiar la fila/selección completa.
            dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;

            // Habilitar RowHeaders (ancho compacto, estilo limpio)
            dgv.RowHeadersVisible = true;
            dgv.RowHeadersWidth   = 20;
            dgv.RowHeadersDefaultCellStyle.BackColor          = Color.FromArgb(245, 245, 248);
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(180, 210, 245);
            dgv.RowHeadersDefaultCellStyle.SelectionForeColor = Color.Black;

            if (estado.Inicializado) return;
            estado.Inicializado = true;

            // Detectar clic en RowHeader
            dgv.RowHeaderMouseClick += (s, e) => estado.ClicoEnRowHeader = true;
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
                    MostrarMenuCopia((DataGridView)s, estado);
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
                e.RowIndex    == dgv.CurrentCell.RowIndex &&
                e.ColumnIndex == dgv.CurrentCell.ColumnIndex)
            {
                using var pen = new Pen(ColorBordeCelda, 2);
                e.Graphics.DrawRectangle(pen,
                    e.CellBounds.Left + 1,
                    e.CellBounds.Top  + 1,
                    e.CellBounds.Width  - 3,
                    e.CellBounds.Height - 3);
            }

            e.Handled = true;
        }

        private static void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Control || e.KeyCode != Keys.C) return;

            var dgv = (DataGridView)sender;
            if (dgv.CurrentCell == null) return;

            // En modo edición, el TextBox editor se encarga de la copia.
            if (dgv.IsCurrentCellInEditMode)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            _estados.TryGetValue(dgv, out var estado);
            bool rowHeaderActivo = estado?.ClicoEnRowHeader == true;

            if (dgv.SelectedRows.Count > 1 || rowHeaderActivo)
            {
                // Múltiples filas o clic en RowHeader → copiar filas completas como TSV
                CopiarFilas(dgv, dgv.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index));
            }
            else
            {
                // Fila única → copiar solo la celda activa
                string texto = dgv.CurrentCell.FormattedValue?.ToString() ?? string.Empty;
                if (texto.Length > 0)
                    Clipboard.SetText(texto);
            }

            e.Handled          = true;
            e.SuppressKeyPress = true;
        }

        private static void MostrarMenuCopia(DataGridView dgv, RowHeaderState estado)
        {
            if (dgv.CurrentCell == null) return;

            bool multiFilas = dgv.SelectedRows.Count > 1;
            string labelFilas = multiFilas ? $"Copiar {dgv.SelectedRows.Count} filas" : "Copiar fila";

            var menu = new ContextMenuStrip();

            // Copiar celda
            var itemCelda = menu.Items.Add("📋  Copiar celda\tCtrl+C");
            itemCelda.Click += (_, __) =>
            {
                string texto = dgv.CurrentCell.FormattedValue?.ToString() ?? string.Empty;
                if (texto.Length > 0) Clipboard.SetText(texto);
            };

            // Copiar fila(s)
            var itemFila = menu.Items.Add($"📄  {labelFilas}");
            itemFila.Click += (_, __) =>
                CopiarFilas(dgv, dgv.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index));

            menu.Show(dgv, dgv.PointToClient(Cursor.Position));
        }

        private static void CopiarFilas(DataGridView dgv, IEnumerable<DataGridViewRow> filas)
        {
            var colIndices = Enumerable.Range(0, dgv.ColumnCount)
                .Where(i => dgv.Columns[i].Visible)
                .OrderBy(i => dgv.Columns[i].DisplayIndex)
                .ToList();

            var sb = new StringBuilder();
            foreach (var row in filas)
                sb.AppendLine(string.Join("\t",
                    colIndices.Select(i => row.Cells[i].FormattedValue?.ToString() ?? "")));

            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString().TrimEnd());
        }
    }
}
