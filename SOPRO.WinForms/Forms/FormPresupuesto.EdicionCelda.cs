using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Edición de celdas de la grilla (validación, autocompletado de clave, undo/redo, clipboard).
    /// </summary>
    public partial class FormPresupuesto
    {

        private void HandleClipboardPaste()
        {
            if (dgvPresupuesto.CurrentCell == null) return;

            var currentColumn = dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex];
            string? startInternalName = (currentColumn.Tag as ColumnaPersonalizada)?.NombreInterno;
            var plan = BudgetClipboardPasteService.BuildPlan(startInternalName, Clipboard.GetText());

            if (!plan.IsValid)
            {
                MessageBox.Show(plan.ErrorMessage, "Pegado no permitido", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int startRowIndex = dgvPresupuesto.CurrentCell.RowIndex;
            int skippedCells = 0;
            bool hadChanges = false;

            dgvPresupuesto.SuspendLayout();
            try
            {
                foreach (var pasteRow in plan.Rows.OrderBy(r => r.RowOffset))
                {
                    int rowIndex = startRowIndex + pasteRow.RowOffset;
                    while (rowIndex >= dgvPresupuesto.Rows.Count)
                    {
                        dgvPresupuesto.Rows.Add();
                    }

                    string tipo = ObtenerCeldaTexto(rowIndex, "Tipo");
                    bool hasKey = !string.IsNullOrWhiteSpace(ObtenerCeldaTexto(rowIndex, "Clave"));
                    bool rowWillHaveKeyAfterPaste = hasKey || pasteRow.ValuesByColumn.ContainsKey("Clave");

                    foreach (var columnName in BudgetClipboardPasteService.GetColumnOrder())
                    {
                        if (!pasteRow.ValuesByColumn.TryGetValue(columnName, out var rawValue))
                            continue;

                        if (!BudgetClipboardPasteService.CanPasteIntoColumn(tipo, columnName, hasKey, rowWillHaveKeyAfterPaste))
                        {
                            skippedCells++;
                            continue;
                        }

                        if (!BudgetClipboardPasteService.TryNormalizeValue(columnName, rawValue, out var normalizedValue))
                        {
                            skippedCells++;
                            continue;
                        }

                        var cell = ObtenerCeldaPorNombreInterno(rowIndex, columnName);
                        if (cell == null)
                        {
                            skippedCells++;
                            continue;
                        }

                        cell.Value = normalizedValue;
                        hadChanges = true;

                        if (string.Equals(columnName, "Clave", StringComparison.OrdinalIgnoreCase))
                        {
                            hasKey = !string.IsNullOrWhiteSpace(normalizedValue);
                            rowWillHaveKeyAfterPaste = hasKey;
                            TryApplyBudgetConceptAssignmentByKey(rowIndex);
                        }
                    }
                }
            }
            finally
            {
                dgvPresupuesto.ResumeLayout();
            }

            if (hadChanges)
            {
                GuardarCambios();
                ActualizarEstadisticas();
                dgvPresupuesto.Refresh();
            }

            if (skippedCells > 0)
            {
                MessageBox.Show($"Se omitieron {skippedCells} celdas por restricciones de edición o valores inválidos.", "Pegado parcial", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DgvPresupuesto_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var columnaEditada = dgvPresupuesto.Columns[e.ColumnIndex];
            if (!(columnaEditada.Tag is ColumnaPersonalizada colDefClave) || colDefClave.NombreInterno != "Clave")
                return;

            var tipoCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Tipo");
            if (tipoCell?.Value?.ToString() != "Concepto")
                return;

            string claveNueva = (e.FormattedValue?.ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(claveNueva))
            {
                _ultimoIntentoClaveInvalida = false;
                return;
            }

            string claveNormalizada = claveNueva.ToUpperInvariant();
            var matriz = _context.Matrices
                .FirstOrDefault(m => m.ProyectoId == _proyecto.Id
                    && m.Clave != null
                    && m.Clave.Trim().ToUpper() == claveNormalizada);

            if (matriz == null || matriz.Tipo == TipoMatriz.APU)
            {
                _ultimoIntentoClaveInvalida = false;
                return;
            }

            _ultimoIntentoClaveInvalida = true;
            e.Cancel = true;
            MessageBox.Show(
                "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. Seleccione una matriz de tipo APU.",
                "Selección no válida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            if (dgvPresupuesto.EditingControl is TextBox tb)
            {
                tb.SelectionStart = 0;
                tb.SelectionLength = tb.TextLength;
            }
        }

        private void DgvPresupuesto_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is not TextBox tb)
                return;

            tb.KeyDown -= DgvPresupuesto_EditingControl_KeyDown;
            tb.PreviewKeyDown -= DgvPresupuesto_EditingControl_PreviewKeyDown;
            tb.TextChanged -= DgvPresupuesto_DescripcionTextChanged;
            tb.LostFocus -= DgvPresupuesto_DescripcionLostFocus;

            _txtDescripcionEnEdicion = null;

            if (dgvPresupuesto.CurrentCell?.OwningColumn?.Tag is not ColumnaPersonalizada colDefEdit)
                return;

            if (colDefEdit.NombreInterno == "Clave")
            {
                OcultarAutocompleteApu(false);
                tb.KeyDown += DgvPresupuesto_EditingControl_KeyDown;
                tb.PreviewKeyDown += DgvPresupuesto_EditingControl_PreviewKeyDown;
                return;
            }

            OcultarAutocompleteApu(false);
            _autocompleteRowIndex = -1;
            _autocompleteColumnIndex = -1;

            if (colDefEdit.NombreInterno == "Descripcion")
            {
                tb.KeyDown += DgvPresupuesto_EditingControl_KeyDown;
                tb.PreviewKeyDown += DgvPresupuesto_EditingControl_PreviewKeyDown;
                _txtDescripcionEnEdicion = tb;
                _autocompleteRowIndex = dgvPresupuesto.CurrentCell.RowIndex;
                _autocompleteColumnIndex = dgvPresupuesto.CurrentCell.ColumnIndex;
                tb.TextChanged += DgvPresupuesto_DescripcionTextChanged;
                tb.LostFocus += DgvPresupuesto_DescripcionLostFocus;
                MostrarAutocompleteApuParaTexto(tb.Text);
            }
        }

        private void DgvPresupuesto_DescripcionTextChanged(object? sender, EventArgs e)
        {
            if (sender is not TextBox tb)
                return;

            if (dgvPresupuesto.CurrentCell?.OwningColumn?.Tag is not ColumnaPersonalizada colActual
                || !string.Equals(colActual.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase)
                || _autocompleteRowIndex < 0
                || ObtenerCeldaTexto(_autocompleteRowIndex, "Tipo") != "Concepto")
            {
                OcultarAutocompleteApu(false);
                return;
            }

            MostrarAutocompleteApuParaTexto(tb.Text);
        }

        private void DgvPresupuesto_DescripcionLostFocus(object? sender, EventArgs e)
        {
            BeginInvoke(new Action(() =>
            {
                if (_lstApuAutocomplete != null)
                {
                    bool cursorSobreLista = _lstApuAutocomplete.Visible && _lstApuAutocomplete.Bounds.Contains(PointToClient(Cursor.Position));
                    if (_lstApuAutocomplete.Focused || _lstApuAutocomplete.ContainsFocus || _mouseDownEnAutocomplete || _confirmandoSeleccionAutocomplete || cursorSobreLista)
                        return;
                }

                OcultarAutocompleteApu(false);
            }));
        }

        private void DgvPresupuesto_EditingControl_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (_lstApuAutocomplete is not { Visible: true })
                return;

            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape)
                e.IsInputKey = true;
        }

        private void DgvPresupuesto_EditingControl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (ProcesarTeclaAutocomplete(e.KeyData))
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.KeyCode != Keys.Escape || !_ultimoIntentoClaveInvalida)
                return;

            _ultimoIntentoClaveInvalida = false;

            if (sender is TextBox tb)
            {
                tb.Text = _valorAnteriorClaveEnEdicion ?? string.Empty;
                tb.SelectionStart = tb.TextLength;
                tb.SelectionLength = 0;
            }

            e.SuppressKeyPress = true;
            e.Handled = true;

            BeginInvoke(new Action(() =>
            {
                dgvPresupuesto.CancelEdit();

                if (_filaClaveEnEdicion >= 0
                    && _filaClaveEnEdicion < dgvPresupuesto.Rows.Count
                    && _columnaClaveEnEdicion >= 0
                    && _columnaClaveEnEdicion < dgvPresupuesto.Columns.Count)
                {
                    var celdaClave = dgvPresupuesto.Rows[_filaClaveEnEdicion].Cells[_columnaClaveEnEdicion];
                    celdaClave.Value = _valorAnteriorClaveEnEdicion ?? string.Empty;
                    dgvPresupuesto.CurrentCell = celdaClave;
                }
            }));
        }

        private static bool EsColumnaUndoPresupuesto(string nombreInterno)
        {
            return string.Equals(nombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "Unidad", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryUndoBudgetEdit()
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

        private bool TryRedoBudgetEdit()
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

        private void AplicarUndoRedoPresupuestoCelda(int rowIndex, int columnIndex, string value)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                return;
            if (columnIndex < 0 || columnIndex >= dgvPresupuesto.Columns.Count)
                return;

            var cell = dgvPresupuesto.Rows[rowIndex].Cells[columnIndex];
            dgvPresupuesto.CurrentCell = cell;
            cell.Value = value;

            if (dgvPresupuesto.Columns[columnIndex].Tag is ColumnaPersonalizada colDef
                && string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
            {
                DgvPresupuesto_CellValueChanged(dgvPresupuesto, new DataGridViewCellEventArgs(columnIndex, rowIndex));
            }
            else
            {
                GuardarCambios();
                ActualizarEstadisticas();
                dgvPresupuesto.Refresh();
            }
        }

        private void DgvPresupuesto_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var valorNuevoUndo = dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            if (!_isUndoRedo
                && e.RowIndex == _undoBudgetRowIndex
                && e.ColumnIndex == _undoBudgetColumnIndex
                && EsColumnaUndoPresupuesto(_undoBudgetColumnName)
                && !string.Equals(_undoBudgetOldValue, valorNuevoUndo, StringComparison.Ordinal))
            {
                int rowIndex = e.RowIndex;
                int columnIndex = e.ColumnIndex;
                string oldValue = _undoBudgetOldValue;
                string newValue = valorNuevoUndo;
                string descripcion = $"Editar {_undoBudgetColumnName} en presupuesto";

                _undoManager.Push(new DelegateUndoableAction(
                    descripcion,
                    () => AplicarUndoRedoPresupuestoCelda(rowIndex, columnIndex, oldValue),
                    () => AplicarUndoRedoPresupuestoCelda(rowIndex, columnIndex, newValue)));
            }

            _undoBudgetRowIndex = -1;
            _undoBudgetColumnIndex = -1;
            _undoBudgetColumnName = string.Empty;
            _undoBudgetOldValue = string.Empty;

            // Restaurar color de fondo al terminar edición
            dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.White;
            bool cursorSobreLista = _lstApuAutocomplete != null && _lstApuAutocomplete.Visible && _lstApuAutocomplete.Bounds.Contains(PointToClient(Cursor.Position));
            bool mantenerAutocomplete = _confirmandoSeleccionAutocomplete || _mouseDownEnAutocomplete || cursorSobreLista || (_lstApuAutocomplete?.Focused ?? false) || (_lstApuAutocomplete?.ContainsFocus ?? false);
            if (!mantenerAutocomplete)
                OcultarAutocompleteApu(false);

            if (e.RowIndex == _filaClaveEnEdicion && e.ColumnIndex == _columnaClaveEnEdicion)
            {
                _filaClaveEnEdicion = -1;
                _columnaClaveEnEdicion = -1;
                _ultimoIntentoClaveInvalida = false;
            }
        }

        private void DgvPresupuesto_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            _autocompleteRowIndex = e.RowIndex;
            _undoBudgetRowIndex = e.RowIndex;
            _undoBudgetColumnIndex = e.ColumnIndex;
            _undoBudgetColumnName = (dgvPresupuesto.Columns[e.ColumnIndex].Tag as ColumnaPersonalizada)?.NombreInterno ?? string.Empty;
            _undoBudgetOldValue = dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            _autocompleteColumnIndex = e.ColumnIndex;
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var columnaEditada = dgvPresupuesto.Columns[e.ColumnIndex];
                if (columnaEditada.Tag is ColumnaPersonalizada colDefClaveEdit && colDefClaveEdit.NombreInterno == "Clave")
                {
                    _filaClaveEnEdicion = e.RowIndex;
                    _columnaClaveEnEdicion = e.ColumnIndex;
                    _valorAnteriorClaveEnEdicion = dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
                    _ultimoIntentoClaveInvalida = false;
                }
            }

            // Validar que no se salten filas vacías
            int primeraFilaVacia = ObtenerPrimeraFilaVacia();

            if (e.RowIndex > primeraFilaVacia)
            {
                e.Cancel = true;

                // Mover silenciosamente a la primera fila vacía
                BeginInvoke(new Action(() =>
                {
                    if (primeraFilaVacia >= 0 && primeraFilaVacia < dgvPresupuesto.Rows.Count)
                    {
                        var tipoCell = ObtenerCeldaPorNombreInterno(primeraFilaVacia, "Tipo");
                        if (tipoCell != null)
                        {
                            dgvPresupuesto.CurrentCell = tipoCell;
                        }
                    }
                }));
                return;
            }

            // Si está cerca del final (quedan menos de 10 filas), agregar más
            int filasRestantes = dgvPresupuesto.Rows.Count - e.RowIndex;
            if (filasRestantes < 10)
            {
                for (int i = 0; i < 50; i++)
                {
                    dgvPresupuesto.Rows.Add();
                }
            }

            // Validar qué columnas son editables según el tipo de fila
            var tipoCell = dgvPresupuesto.Rows[e.RowIndex].Cells.Cast<DataGridViewCell>()
                .FirstOrDefault(c => c.OwningColumn.Tag is ColumnaPersonalizada col && col.NombreInterno == "Tipo");

            // Obtener la columna que se está editando
            var colActual = dgvPresupuesto.Columns[e.ColumnIndex];
            if (!(colActual.Tag is ColumnaPersonalizada colDef)) return;

            string nombreInterno = colDef.NombreInterno;

            // Si NO hay Tipo seleccionado, solo permitir editar Tipo
            if (tipoCell?.Value == null || string.IsNullOrWhiteSpace(tipoCell.Value.ToString()))
            {
                if (nombreInterno != "Tipo")
                {
                    e.Cancel = true;
                    MessageBox.Show("Primero debe seleccionar un Tipo.", "Campo Requerido",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            string tipo = tipoCell.Value.ToString();

            bool cancelarEdicion = false;

            if (tipo == "Capitulo" || tipo == "Subcapitulo" || tipo == "Nivel 1" || tipo == "Nivel 2" || tipo == "Nivel 3")
            {
                // AGRUPADORES: Solo pueden editar Tipo, Clave, Descripción
                if (nombreInterno != "Tipo" && nombreInterno != "Clave" && nombreInterno != "Descripcion")
                {
                    cancelarEdicion = true;
                }
            }
            else if (tipo == "Concepto")
            {
                // CONCEPTOS: No pueden editar P.U. e Importe (calculados)
                if (nombreInterno == "PrecioUnitario" || nombreInterno == "Importe")
                {
                    cancelarEdicion = true;
                }

                // Si no tiene clave, solo puede editar Tipo y Clave
                var claveCell = dgvPresupuesto.Rows[e.RowIndex].Cells.Cast<DataGridViewCell>()
                    .FirstOrDefault(c => c.OwningColumn.Tag is ColumnaPersonalizada col && col.NombreInterno == "Clave");

                if ((claveCell?.Value == null || string.IsNullOrWhiteSpace(claveCell.Value.ToString())) &&
                    nombreInterno != "Tipo" && nombreInterno != "Clave" && nombreInterno != "Descripcion")
                {
                    cancelarEdicion = true;
                    MessageBox.Show("Primero debe asignar una Clave al concepto.", "Campo Requerido",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            if (cancelarEdicion)
            {
                e.Cancel = true;

                // Marcar visualmente que no es editable
                dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.FromArgb(245, 245, 245);

                BeginInvoke(new Action(() =>
                {
                    dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.White;
                }));
            }
        }
    }
}
