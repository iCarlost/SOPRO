using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SOPRO.Application.DTOs.Insumos;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Manejo de teclado y navegación por el grid del selector.
    /// </summary>
    public partial class FormSeleccionarInsumo
    {

        private void FormSeleccionarInsumo_Shown(object? sender, EventArgs e)
        {
            FocusSearchBox();
        }

        private void FormSeleccionarInsumo_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape) return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            btnCancelar.PerformClick();
        }

        private void txtBuscar_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                MoveGridSelection(e.KeyCode == Keys.Down ? 1 : -1);
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnAceptar.PerformClick();
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnCancelar.PerformClick();
            }
        }

        private void dgvInsumos_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;

            e.Handled = true;
            RedirectTypedCharacterToSearch(e.KeyChar);
        }

        private void RedirectTypedCharacterToSearch(char keyChar)
        {
            FocusSearchBox();
            txtBuscar.SelectedText = keyChar.ToString();
            txtBuscar.SelectionStart = txtBuscar.TextLength;
        }

        private void FocusSearchBox()
        {
            if (!txtBuscar.CanFocus) return;
            txtBuscar.Focus();
            txtBuscar.SelectionStart = txtBuscar.TextLength;
            txtBuscar.SelectionLength = 0;
        }

        private void MoveGridSelection(int delta)
        {
            if (dgvInsumos.Rows.Count == 0) return;

            var visibleRows = dgvInsumos.Rows.Cast<DataGridViewRow>().Where(r => r.Visible).ToList();
            if (visibleRows.Count == 0) return;

            var currentRow = dgvInsumos.CurrentRow;
            var currentIndex = currentRow != null ? visibleRows.IndexOf(currentRow) : -1;
            var targetIndex = currentIndex < 0
                ? (delta >= 0 ? 0 : visibleRows.Count - 1)
                : Math.Max(0, Math.Min(visibleRows.Count - 1, currentIndex + delta));

            var targetRow = visibleRows[targetIndex];
            dgvInsumos.ClearSelection();
            targetRow.Selected = true;

            var targetCell = targetRow.Cells.Cast<DataGridViewCell>()
                .FirstOrDefault(c => c.Visible && c.OwningColumn.Visible)
                ?? targetRow.Cells[0];

            dgvInsumos.CurrentCell = targetCell;
            if (targetRow.Index >= 0)
                dgvInsumos.FirstDisplayedScrollingRowIndex = targetRow.Index;
            dgvInsumos.Focus();
        }
    }
}
