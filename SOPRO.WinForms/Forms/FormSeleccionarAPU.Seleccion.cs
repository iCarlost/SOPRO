using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;
using System.Reflection;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Selección de filas, navegación con teclado y cálculo de importe.
    /// </summary>
    public partial class FormSeleccionarAPU
    {

        private void SeleccionarFilaInicial()
        {
            if (dgvMatrices.Rows.Count == 0) return;

            DataGridViewRow? targetRow = null;
            if (_matrizIdPreseleccionada.HasValue)
            {
                foreach (DataGridViewRow row in dgvMatrices.Rows)
                {
                    if (!row.Visible) continue;
                    if (row.Tag is MatrixListItemDto lm && lm.Id == _matrizIdPreseleccionada.Value)
                    { targetRow = row; break; }
                }
            }
            targetRow ??= dgvMatrices.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Visible)
                          ?? dgvMatrices.Rows[0];

            dgvMatrices.ClearSelection();
            targetRow.Selected = true;

            var cell = targetRow.Cells.Cast<DataGridViewCell>()
                .FirstOrDefault(c => c.Visible && c.OwningColumn.Visible && !c.ReadOnly)
                ?? targetRow.Cells.Cast<DataGridViewCell>()
                    .FirstOrDefault(c => c.Visible && c.OwningColumn.Visible);
            if (cell != null && cell.Visible && cell.OwningColumn.Visible)
                dgvMatrices.CurrentCell = cell;
            if (targetRow.Visible)
                dgvMatrices.FirstDisplayedScrollingRowIndex = targetRow.Index;
        }


        private void FormSeleccionarAPU_Shown(object? sender, EventArgs e)
        {
            BeginInvoke(new Action(FocusSearchBox));
        }

        private void FocusSearchBox()
        {
            if (!txtBuscar.CanFocus)
                return;

            txtBuscar.Focus();
            txtBuscar.SelectionStart = txtBuscar.TextLength;
            txtBuscar.SelectionLength = 0;
        }

        private void txtBuscar_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                if (dgvMatrices.Rows.Count == 0)
                    return;

                e.Handled = true;
                e.SuppressKeyPress = true;
                MoveGridSelection(e.KeyCode == Keys.Down ? 1 : -1);
                dgvMatrices.Focus();
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                if (dgvMatrices.SelectedRows.Count == 0)
                    return;

                e.Handled = true;
                e.SuppressKeyPress = true;
                btnAceptar_Click(sender ?? this, EventArgs.Empty);
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnCancelar_Click(sender ?? this, EventArgs.Empty);
            }
        }

        private void dgvMatrices_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar) && e.KeyChar != '\b')
                return;

            e.Handled = true;
            FocusSearchBox();

            if (e.KeyChar == '\b')
            {
                if (txtBuscar.SelectionLength > 0)
                {
                    var start = txtBuscar.SelectionStart;
                    txtBuscar.Text = txtBuscar.Text.Remove(start, txtBuscar.SelectionLength);
                    txtBuscar.SelectionStart = start;
                }
                else if (txtBuscar.SelectionStart > 0)
                {
                    var start = txtBuscar.SelectionStart;
                    txtBuscar.Text = txtBuscar.Text.Remove(start - 1, 1);
                    txtBuscar.SelectionStart = start - 1;
                }

                return;
            }

            var selectionStart = txtBuscar.SelectionStart;
            var selectionLength = txtBuscar.SelectionLength;
            if (selectionLength > 0)
            {
                txtBuscar.Text = txtBuscar.Text.Remove(selectionStart, selectionLength)
                    .Insert(selectionStart, e.KeyChar.ToString());
                txtBuscar.SelectionStart = selectionStart + 1;
            }
            else
            {
                txtBuscar.Text = txtBuscar.Text.Insert(selectionStart, e.KeyChar.ToString());
                txtBuscar.SelectionStart = selectionStart + 1;
            }
        }

        private void MoveGridSelection(int delta)
        {
            if (dgvMatrices.Rows.Count == 0)
                return;

            var currentRow = dgvMatrices.CurrentRow;
            var currentIndex = currentRow?.Index ?? -1;
            var nextIndex = currentIndex < 0
                ? 0
                : Math.Max(0, Math.Min(dgvMatrices.Rows.Count - 1, currentIndex + delta));

            var targetRow = dgvMatrices.Rows[nextIndex];
            dgvMatrices.ClearSelection();
            targetRow.Selected = true;

            var targetCell = targetRow.Cells.Cast<DataGridViewCell>()
                .FirstOrDefault(c => c.Visible && c.OwningColumn.Visible)
                ?? targetRow.Cells[0];
            dgvMatrices.CurrentCell = targetCell;

            if (targetRow.Index >= 0)
                dgvMatrices.FirstDisplayedScrollingRowIndex = targetRow.Index;
        }

        private void dgvMatrices_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvMatrices.SelectedRows.Count == 0) { btnEditarMatriz.Enabled = false; return; }
            var row = dgvMatrices.SelectedRows[0];
            switch (row.Tag)
            {
                case MatrixListItemDto lm:
                    lblUnidad.Text = lm.Unidad;
                    lblCostoUnitario.Text = lm.CostoDirecto.ToString("C4");
                    btnEditarMatriz.Enabled = true;
                    break;
                case ExternalProjectMatrixOption em:
                    lblUnidad.Text = em.Unidad;
                    lblCostoUnitario.Text = em.CostoDirecto.ToString("C4");
                    btnEditarMatriz.Enabled = false;
                    break;
                default:
                    btnEditarMatriz.Enabled = false;
                    break;
            }
            CalcularImporte();
        }

        private void nudCantidad_ValueChanged(object sender, EventArgs e) => CalcularImporte();

        private void CalcularImporte()
        {
            if (dgvMatrices.SelectedRows.Count == 0) return;
            decimal costo = 0m;
            var row = dgvMatrices.SelectedRows[0];
            if (row.Tag is MatrixListItemDto lm) costo = lm.CostoDirecto;
            else if (row.Tag is ExternalProjectMatrixOption em) costo = em.CostoDirecto;
            lblImporte.Text = (nudCantidad.Value * costo).ToString("C2");
        }
    }
}
