using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Eventos del grid, menú contextual y edición de matrices.
    /// </summary>
    public partial class FormMatrices
    {

        private void DgvMatrices_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Value == null) return;
            var col = dgvMatrices.Columns[e.ColumnIndex].Name;
            if ((col == "colCostoDirecto" || col == "col_CostoDirecto") && e.Value is decimal v)
            {
                e.Value = v.ToString($"C{DecimalesImporte}",
                    System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private void dgvMatrices_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Insert && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnNuevo_Click(sender, EventArgs.Empty);
                return;
            }

            if (e.Control && e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                _ = EliminarMatricesSeleccionadasAsync();
                return;
            }

            if (e.KeyCode == Keys.Enter && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (btnEditar.Enabled)
                    btnEditar_Click(sender, EventArgs.Empty);
            }
        }

        private void dgvMatrices_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvMatrices.Rows[e.RowIndex];
            if (!clickedRow.Selected)
            {
                dgvMatrices.ClearSelection();
                clickedRow.Selected = true;
            }

            if (e.ColumnIndex >= 0)
                dgvMatrices.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _matrizSeleccionada = (clickedRow.DataBoundItem as MatrixGridRowDisplay)?.Source;
            ActualizarEstadoBotones();

            _menuMatrices?.Dispose();
            _menuMatrices = new ContextMenuStrip();
            int n = dgvMatrices.SelectedRows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow);
            var itemEditar = _menuMatrices.Items.Add("✏️  Editar");
            itemEditar.Enabled = n == 1 && _matrizSeleccionada != null;
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var itemEliminar = _menuMatrices.Items.Add(n > 1 ? $"🗑️  Eliminar {n} matrices" : "🗑️  Eliminar");
            itemEliminar.Enabled = _matrizSeleccionada != null;
            itemEliminar.Click += async (_, __) => await EliminarMatricesSeleccionadasAsync();

            _menuMatrices.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            itemDonde.Enabled = n == 1 && _matrizSeleccionada != null && _matrizSeleccionada.Tipo != TipoMatriz.APU;
            if (_matrizSeleccionada == null || _matrizSeleccionada.Tipo == TipoMatriz.APU)
            {
                var sinUso = itemDonde.DropDownItems.Add(_matrizSeleccionada?.Tipo == TipoMatriz.APU
                    ? "(No aplica para matrices APU)"
                    : "(Sin selección)");
                sinUso.Enabled = false;
            }
            else
            {
                var referencias = _matrixUsageLookupService.FindUsageReferences(_context, _proyectoId, _matrizSeleccionada.Id);
                if (referencias.Count == 0)
                {
                    var sinUso = itemDonde.DropDownItems.Add("(Sin uso en ninguna matriz)");
                    sinUso.Enabled = false;
                }
                else
                {
                    foreach (var referencia in referencias)
                    {
                        var itemUso = itemDonde.DropDownItems.Add(referencia.DisplayLabel);
                        var matrizUso = referencia.Source;
                        itemUso.Click += (_, __) => AbrirEditorMatriz(matrizUso);
                    }
                }
            }
            _menuMatrices.Items.Add(itemDonde);

            _menuMatrices.Show(Cursor.Position);
        }


        private void AbrirEditorMatriz(Matriz matriz)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId, matriz);
            form.ShowDialog(this);
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId);
            if (form.ShowDialog() == DialogResult.OK)
            {
                CargarMatrices();
                PropagrarCambiosAlPresupuesto();
            }
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_matrizSeleccionada == null)
            {
                MessageBox.Show("Seleccione una matriz para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormEditarMatriz(_context, _proyectoId, _matrizSeleccionada);
            if (form.ShowDialog() == DialogResult.OK)
            {
                CargarMatrices();
                PropagrarCambiosAlPresupuesto();
            }
        }

        private void PropagrarCambiosAlPresupuesto()
        {
            OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
        }
    }
}
