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
    /// Interacción del grid: doble clic, teclado, menú contextual y eliminación de filas.
    /// </summary>
    public partial class FormPresupuesto
    {

        private void DgvPresupuesto_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var colClickeada = dgvPresupuesto.Columns[e.ColumnIndex];
            var interaction = BudgetGridInteractionService.HandleCellDoubleClick(
                e.RowIndex,
                e.ColumnIndex,
                colClickeada.ReadOnly,
                colClickeada.Name,
                (colClickeada.Tag as ColumnaPersonalizada)?.NombreInterno,
                ObtenerCeldaTexto(e.RowIndex, "Tipo"));

            if (interaction.ShouldOpenApuSelector)
            {
                OpenApuSelectorForRow(e.RowIndex);
                return;
            }

            if (interaction.ShouldBeginEdit)
            {
                dgvPresupuesto.BeginEdit(true);
            }
        }

        private void DgvPresupuesto_KeyDown(object sender, KeyEventArgs e)
        {

            if (dgvPresupuesto.CurrentCell == null) return;


            int rowIndex = dgvPresupuesto.CurrentCell.RowIndex;
            var currentColumn = dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex];
            string? currentInternalName = (currentColumn.Tag as ColumnaPersonalizada)?.NombreInterno;
            string currentTipo = ObtenerCeldaTexto(rowIndex, "Tipo");

            if (e.Control && e.KeyCode == Keys.V)
            {
                // Si el foco real está en el panel embebido o en un TextBox de edición,
                // dejar que el control activo procese el pegado nativo.
                if ((_panelMatricesEmbebido != null
                     && _panelMatricesEmbebido.Visible
                     && _panelMatricesEmbebido.ContainsFocus
                     && _panelMatricesEmbebido.TieneFocoEnEntradaTexto())
                    || (dgvPresupuesto.IsCurrentCellInEditMode && dgvPresupuesto.EditingControl is TextBox))
                {
                    return;
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
                HandleClipboardPaste();
                return;
            }

            if (e.KeyCode == Keys.F2)
            {
                var interaction = BudgetGridInteractionService.HandleF2(rowIndex, currentTipo, currentInternalName);
                e.Handled = interaction.Handled;
                e.SuppressKeyPress = interaction.SuppressKeyPress;

                if (interaction.ShouldOpenApuSelector)
                {
                    OpenApuSelectorForRow(rowIndex);
                }
                return;
            }

            if (e.KeyCode == Keys.Insert && !e.Control && !e.Shift)
            {
                var interaction = BudgetGridInteractionService.HandleInsert(rowIndex, ObtenerPrimeraFilaVacia(), ObtenerUltimaFilaConDatos());
                e.Handled = interaction.Handled;
                e.SuppressKeyPress = interaction.SuppressKeyPress;

                if (interaction.FocusRowIndex.HasValue && !interaction.ShouldInsertConceptRow)
                {
                    var focusCell = ObtenerCeldaPorNombreInterno(interaction.FocusRowIndex.Value, "Tipo");
                    if (focusCell != null)
                    {
                        dgvPresupuesto.CurrentCell = focusCell;
                    }
                    return;
                }

                if (interaction.ShouldInsertConceptRow && interaction.InsertRowIndex.HasValue)
                {
                    int insertIndex = interaction.InsertRowIndex.Value;
                    dgvPresupuesto.Rows.Insert(insertIndex);
                    var nuevaTipoCell = ObtenerCeldaPorNombreInterno(insertIndex, "Tipo");
                    if (nuevaTipoCell != null)
                    {
                        nuevaTipoCell.Value = "Concepto";
                        dgvPresupuesto.CurrentCell = nuevaTipoCell;
                    }
                }
                return;
            }

            if (e.KeyCode == Keys.Delete && e.Control)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                DeleteCurrentSelectionFromBudget();
                return;
            }
        }

        private void DgvPresupuesto_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvPresupuesto.Rows[e.RowIndex];

            // Con CellSelect (formato por celda) un clic sobre una celda NO puebla
            // SelectedRows y clickedRow.Selected es falso aunque existan celdas
            // seleccionadas: el criterio se resuelve con DgvCeldaHelper para no
            // borrar una multiselección al abrir el menú contextual.
            if (!DgvCeldaHelper.ClicDentroDeSeleccion(dgvPresupuesto, e.RowIndex))
            {
                dgvPresupuesto.ClearSelection();
                // Bajo CellSelect `clickedRow.Selected = true` es un no-op; hay que
                // seleccionar la celda clicada (o la fila completa si se pulsó el
                // encabezado) para que la copia del menú tenga contenido.
                if (e.ColumnIndex >= 0)
                    clickedRow.Cells[e.ColumnIndex].Selected = true;
                else
                    DgvCeldaHelper.SeleccionarFilaCompleta(dgvPresupuesto, e.RowIndex);
            }

            if (e.ColumnIndex >= 0)
                dgvPresupuesto.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _menuPresupuesto?.Dispose();
            _menuPresupuesto = new ContextMenuStrip();

            // H8: copia tipo Excel (bloque exacto de celdas o fila[s] completa[s] por
            // encabezado). La lógica se centraliza en DgvCeldaHelper.
            string etiquetaCopiar = DgvCeldaHelper.EtiquetaCopiar(dgvPresupuesto);
            if (etiquetaCopiar != "Copiar celda")
            {
                var itemCopiarCelda = _menuPresupuesto.Items.Add("📋  Copiar celda");
                itemCopiarCelda.Click += (_, __) =>
                {
                    var cell = dgvPresupuesto.CurrentCell;
                    if (cell != null)
                    {
                        string txt = cell.FormattedValue?.ToString() ?? "";
                        if (txt.Length > 0) Clipboard.SetText(txt);
                    }
                };
            }

            var itemCopiar = _menuPresupuesto.Items.Add($"📄  {etiquetaCopiar}");
            itemCopiar.Click += (_, __) => DgvCeldaHelper.CopiarSeleccionAlPortapapeles(dgvPresupuesto);

            // El borrado SÍ trabaja por filas (correcto para eliminar): usa FilasSeleccionadas.
            int n = SeleccionGridHelper.FilasSeleccionadas(dgvPresupuesto).Count(r => !r.IsNewRow);

            _menuPresupuesto.Items.Add(new ToolStripSeparator());

            var itemEliminar = _menuPresupuesto.Items.Add(n > 1 ? $"🗑️  Eliminar {n} filas" : "🗑️  Eliminar");
            itemEliminar.Click += (_, __) => DeleteCurrentSelectionFromBudget();
            _menuPresupuesto.Show(Cursor.Position);
        }

        private void DeleteCurrentSelectionFromBudget()
        {
            if (dgvPresupuesto.CurrentCell == null) return;

            if (TryDeleteMultipleSelectedConceptRows())
                return;

            int rowIndex = dgvPresupuesto.CurrentCell.RowIndex;
            bool rowHasContent = !string.IsNullOrWhiteSpace(ObtenerCeldaTexto(rowIndex, "Tipo"))
                || !string.IsNullOrWhiteSpace(ObtenerCeldaTexto(rowIndex, "Descripcion"));

            var interaction = BudgetGridInteractionService.BuildDeletionPlan(rowIndex, rowHasContent, BuildHierarchyRowsSnapshot());
            if (!interaction.HasDeletionPlan)
                return;

            var result = MessageBox.Show(
                interaction.ConfirmationMessage,
                "Confirmar Eliminación Jerárquica",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            EjecutarEliminacionDeFilas(interaction.RowsToDelete);
        }

        private bool TryDeleteMultipleSelectedConceptRows()
        {
            // Con CellSelect (formato por celda) SelectedRows queda vacío aunque
            // haya celdas seleccionadas: las filas tocadas se resuelven vía helper.
            var filasSeleccionadas = SeleccionGridHelper.FilasSeleccionadas(dgvPresupuesto);
            if (filasSeleccionadas.Count <= 1)
                return false;

            var selectedIndexes = filasSeleccionadas
                .Where(r => !r.IsNewRow)
                .Select(r => r.Index)
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            if (selectedIndexes.Count <= 1)
                return false;

            var snapshot = BuildHierarchyRowsSnapshot();
            var selectedRows = snapshot.Where(r => selectedIndexes.Contains(r.RowIndex)).ToList();
            if (selectedRows.Count != selectedIndexes.Count)
                return false;

            bool allAreConcepts = selectedRows.All(r => string.Equals(r.Tipo, "Concepto", StringComparison.OrdinalIgnoreCase));
            if (!allAreConcepts)
            {
                MessageBox.Show(
                    "La eliminación múltiple con Ctrl + Supr solo está disponible cuando todas las filas seleccionadas son conceptos.\n\nPara agrupadores, use la eliminación jerárquica normal sobre una sola fila.",
                    "Selección no compatible",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return true;
            }

            var result = MessageBox.Show(
                $"¿Desea eliminar los {selectedIndexes.Count} conceptos seleccionados?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación Múltiple",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return true;

            EjecutarEliminacionDeFilas(selectedIndexes);
            return true;
        }

        private int ObtenerNivelIndentacion(int rowIndex)
        {
            return BudgetHierarchyService.GetIndentLevel(ObtenerCeldaTexto(rowIndex, "Tipo"));
        }

        /// <summary>
        /// Comportamiento estilo Excel: empezar a escribir activa edición automática.
        /// </summary>
        private void DgvPresupuesto_KeyPress(object sender, KeyPressEventArgs e)
        {
            bool shouldStartEdit = BudgetGridInteractionService.ShouldStartTypingEdit(
                dgvPresupuesto.CurrentCell != null,
                dgvPresupuesto.CurrentCell != null && dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex].ReadOnly,
                dgvPresupuesto.CurrentCell != null ? dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex].Name : string.Empty,
                e.KeyChar,
                dgvPresupuesto.IsCurrentCellInEditMode);

            if (!shouldStartEdit) return;

            dgvPresupuesto.BeginEdit(true);

            if (dgvPresupuesto.EditingControl is TextBox txt)
            {
                txt.Text = e.KeyChar.ToString();
                txt.SelectionStart = 1;
            }

            e.Handled = true;
        }
    }
}
