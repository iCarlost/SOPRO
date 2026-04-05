using System;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormBuscarEnGrid : Form
    {
        private readonly DataGridView _grid;
        private string _ultimoTexto = string.Empty;
        private int _ultimaFila = -1;
        private int _ultimaColumna = -1;

        public FormBuscarEnGrid(DataGridView grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            txtBuscar.Focus();
            txtBuscar.SelectAll();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F3)
            {
                BuscarSiguiente();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void txtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                BuscarSiguiente();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void btnSiguiente_Click(object sender, EventArgs e)
        {
            BuscarSiguiente();
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void FormBuscarEnGrid_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!_grid.IsDisposed)
                _grid.Focus();
        }

        public void BuscarSiguiente()
        {
            var texto = txtBuscar.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(texto))
            {
                lblEstado.Text = "Escribe un texto para buscar.";
                return;
            }

            if (!string.Equals(_ultimoTexto, texto, StringComparison.Ordinal))
            {
                _ultimoTexto = texto;
                _ultimaFila = -1;
                _ultimaColumna = -1;
            }

            if (BuscarDesdePosicion(texto, _ultimaFila, _ultimaColumna, out var fila, out var col))
            {
                _ultimaFila = fila;
                _ultimaColumna = col;
                SeleccionarCelda(fila, col);
                lblEstado.Text = $"Coincidencia en fila {fila + 1}, columna {_grid.Columns[col].HeaderText}.";
                return;
            }

            if ((_ultimaFila > -1 || _ultimaColumna > -1) && BuscarDesdePosicion(texto, -1, -1, out fila, out col))
            {
                _ultimaFila = fila;
                _ultimaColumna = col;
                SeleccionarCelda(fila, col);
                lblEstado.Text = $"Se reinició la búsqueda. Coincidencia en fila {fila + 1}, columna {_grid.Columns[col].HeaderText}.";
                return;
            }

            lblEstado.Text = $"No se encontraron coincidencias para '{texto}'.";
        }

        private bool BuscarDesdePosicion(string texto, int filaActual, int colActual, out int filaEncontrada, out int colEncontrada)
        {
            filaEncontrada = -1;
            colEncontrada = -1;

            var filas = _grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
            int startRow = 0;
            int startCol = 0;

            if (filaActual >= 0)
            {
                startRow = filaActual;
                startCol = colActual + 1;
            }

            for (int i = startRow; i < filas.Count; i++)
            {
                var row = filas[i];
                int c0 = i == startRow ? startCol : 0;
                for (int j = c0; j < _grid.Columns.Count; j++)
                {
                    var col = _grid.Columns[j];
                    if (!col.Visible) continue;

                    var val = row.Cells[j].Value?.ToString();
                    if (!string.IsNullOrEmpty(val) && val.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filaEncontrada = row.Index;
                        colEncontrada = j;
                        return true;
                    }
                }
            }
            return false;
        }

        private void SeleccionarCelda(int fila, int columna)
        {
            if (fila < 0 || columna < 0) return;

            _grid.ClearSelection();
            if (fila < _grid.Rows.Count && columna < _grid.Columns.Count)
            {
                _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, fila);
                _grid.CurrentCell = _grid.Rows[fila].Cells[columna];
                _grid.Rows[fila].Cells[columna].Selected = true;
                _grid.Focus();
            }
        }
    }
}
