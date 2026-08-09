using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Undo/redo de edición de celdas y cuadros de configuración.
    /// </summary>
    public partial class FormIndirectos
    {

        private static bool EsColumnaUndoIndirectos(string columnName)
        {
            return string.Equals(columnName, "colImporteMensual", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "colDuracion", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryUndoIndirectos()
        {
            if (!_undoManager.CanUndo)
                return false;

            try
            {
                _isUndoRedo = true;
                return _undoManager.Undo();
            }
            finally
            {
                _isUndoRedo = false;
            }
        }

        private bool TryRedoIndirectos()
        {
            if (!_undoManager.CanRedo)
                return false;

            try
            {
                _isUndoRedo = true;
                return _undoManager.Redo();
            }
            finally
            {
                _isUndoRedo = false;
            }
        }

        private void AplicarUndoRedoIndirectosCelda(DataGridView dgv, int rowIndex, int columnIndex, string value)
        {
            if (dgv == null || rowIndex < 0 || rowIndex >= dgv.Rows.Count)
                return;
            if (columnIndex < 0 || columnIndex >= dgv.Columns.Count)
                return;

            var cell = dgv.Rows[rowIndex].Cells[columnIndex];
            dgv.CurrentCell = cell;
            cell.Value = value;
            Dgv_CellEndEdit(dgv, new DataGridViewCellEventArgs(columnIndex, rowIndex));
            dgv.Refresh();
        }

        private void AplicarUndoRedoConfigTextBox(TextBox textBox, string value)
        {
            if (textBox == null)
                return;

            textBox.Text = value ?? string.Empty;
            textBox.Focus();
            textBox.SelectionStart = textBox.TextLength;
        }

        private void ConfigTextBox_Enter(object? sender, EventArgs e)
        {
            if (sender is TextBox textBox)
            {
                _undoTextBox = textBox;
                _undoTextBoxOldValue = textBox.Text ?? string.Empty;
            }
        }

        private void ConfigTextBox_Leave(object? sender, EventArgs e)
        {
            if (_isUndoRedo)
                return;

            if (sender is not TextBox textBox)
                return;

            var newValue = textBox.Text ?? string.Empty;
            var oldValue = _undoTextBox == textBox ? _undoTextBoxOldValue : string.Empty;
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                return;

            string descripcion = textBox == txtVolumenAnual
                ? "Editar volumen anual"
                : "Editar costo directo";

            _undoManager.Push(new DelegateUndoableAction(
                descripcion,
                () => AplicarUndoRedoConfigTextBox(textBox, oldValue),
                () => AplicarUndoRedoConfigTextBox(textBox, newValue)));
        }

        private void Dgv_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (sender is not DataGridView dgv)
                return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            _undoGrid = dgv;
            _undoRowIndex = e.RowIndex;
            _undoColumnIndex = e.ColumnIndex;
            _undoOldValue = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0) return;

            var tipo = dgv.Rows[e.RowIndex].Cells["colTipo"].Value?.ToString();

            if (tipo == "GRUPO")
            {
                e.CellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                e.CellStyle.BackColor = Color.FromArgb(230, 230, 230);
            }
        }

        private void Dgv_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var tipo = dgv.Rows[e.RowIndex].Cells["colTipo"].Value?.ToString();
            if (tipo != "CONCEPTO") return;

            int conceptoId = Convert.ToInt32(dgv.Rows[e.RowIndex].Cells["colId"].Value);
            var concepto = _context.ConceptosIndirectos.Find(conceptoId);

            if (concepto == null) return;

            string valorNuevoUndo = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;

            try
            {
                // Actualizar importe mensual
                if (e.ColumnIndex == dgv.Columns["colImporteMensual"].Index)
                {
                    if (decimal.TryParse(dgv.Rows[e.RowIndex].Cells["colImporteMensual"].Value?.ToString(), out decimal importe))
                    {
                        concepto.ImporteMensual = importe;
                    }
                }

                // Actualizar duración
                if (e.ColumnIndex == dgv.Columns["colDuracion"].Index && dgv.Columns["colDuracion"].Visible)
                {
                    if (int.TryParse(dgv.Rows[e.RowIndex].Cells["colDuracion"].Value?.ToString(), out int duracion))
                    {
                        concepto.DuracionMeses = duracion;
                    }
                }

                _context.SaveChanges();

                // Actualizar solo esta celda de importe total (sin recargar todo)
                dgv.Rows[e.RowIndex].Cells["colImporteTotal"].Value = concepto.ImporteTotal;

                // Buscar y actualizar el total del grupo padre (sin recargar)
                var grupo = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .FirstOrDefault(g => g.Id == concepto.GrupoIndirectoId);

                if (grupo != null)
                {
                    // Buscar la fila del grupo en el grid
                    for (int i = e.RowIndex - 1; i >= 0; i--)
                    {
                        if (dgv.Rows[i].Cells["colTipo"].Value?.ToString() == "GRUPO")
                        {
                            dgv.Rows[i].Cells["colImporteTotal"].Value = grupo.Total;
                            break;
                        }
                    }
                }

                // Actualizar resumen sin recargar
                ActualizarResumen();

                if (!_isUndoRedo
                    && ReferenceEquals(_undoGrid, dgv)
                    && _undoRowIndex == e.RowIndex
                    && _undoColumnIndex == e.ColumnIndex
                    && EsColumnaUndoIndirectos(dgv.Columns[e.ColumnIndex].Name)
                    && !string.Equals(_undoOldValue, valorNuevoUndo, StringComparison.Ordinal))
                {
                    int rowIndex = e.RowIndex;
                    int columnIndex = e.ColumnIndex;
                    string oldValue = _undoOldValue;
                    string newValue = valorNuevoUndo;
                    _undoManager.Push(new DelegateUndoableAction(
                        $"Editar {dgv.Columns[e.ColumnIndex].HeaderText} en indirectos",
                        () => AplicarUndoRedoIndirectosCelda(dgv, rowIndex, columnIndex, oldValue),
                        () => AplicarUndoRedoIndirectosCelda(dgv, rowIndex, columnIndex, newValue)));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _undoGrid = null;
                _undoRowIndex = -1;
                _undoColumnIndex = -1;
                _undoOldValue = string.Empty;
            }
        }
    }
}
