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
    /// Aceptar/cancelar, crear y editar matrices, e importación de matrices externas.
    /// </summary>
    public partial class FormSeleccionarAPU
    {

        private void dgvMatrices_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnAceptar_Click(sender, e);
        }

        private void dgvMatrices_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && dgvMatrices.CurrentRow?.Index >= 0)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnAceptar_Click(sender, EventArgs.Empty);
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnCancelar_Click(sender, EventArgs.Empty);
            }
        }

        private void btnAceptar_Click(object sender, EventArgs e)
        {
            if (dgvMatrices.SelectedRows.Count == 0)
            {
                MessageBox.Show("Seleccione una matriz de la lista.", "Selección requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (nudCantidad.Value <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a cero.", "Cantidad inválida",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = dgvMatrices.SelectedRows[0];

            if (row.Tag is MatrixListItemDto localMatrix)
            {
                if (_includeAuxiliaries && localMatrix.Tipo != TipoMatriz.APU)
                {
                    MessageBox.Show("Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. Seleccione una matriz de tipo APU.",
                        "Selección no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                MatrizSeleccionada = _context.Matrices.Find(localMatrix.Id);
                Cantidad           = nudCantidad.Value;
                _projectUsageService.RegisterMatrixSelection(_context.DatabasePath, localMatrix.Id);
                if (EmbeddedMode)
                {
                    EmbeddedAccepted?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                return;
            }

            if (row.Tag is not ExternalProjectMatrixOption externalMatrix) return;

            if (_includeAuxiliaries && externalMatrix.Tipo != TipoMatriz.APU)
            {
                MessageBox.Show("Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. Seleccione una matriz de tipo APU.",
                    "Selección no válida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.Equals(_context.DatabasePath, externalMatrix.ProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("El proyecto externo coincide con el proyecto actual.", "Importación",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var preview = _externalMatrixImportService.BuildPreview(
                _context, _proyectoId, externalMatrix.ProjectPath, externalMatrix.MatrixId);
            using var previewDialog = new FormPreviewImportacionMatrices(preview);
            if (previewDialog.ShowDialog(GetDialogOwner()) != DialogResult.OK || previewDialog.SelectedPolicy == null)
                return;

            try
            {
                var result = _externalMatrixImportService.ImportMatrixTree(
                    _context, _proyectoId, externalMatrix.ProjectPath,
                    externalMatrix.MatrixId, previewDialog.SelectedPolicy.Value);

                MatrizSeleccionada = _context.Matrices.Find(result.RootMatrixId);
                Cantidad           = nudCantidad.Value;
                _projectUsageService.RegisterMatrixSelection(externalMatrix.ProjectPath, externalMatrix.MatrixId);
                _projectUsageService.RegisterMatrixSelection(_context.DatabasePath, result.RootMatrixId);

                MessageBox.Show(
                    "Importación completada.\n\n" +
                    $"Matrices nuevas: {result.ImportedMatrices}\n" +
                    $"Materiales nuevos: {result.ImportedMateriales}\n" +
                    $"Mano de obra nueva: {result.ImportedManoDeObra}\n" +
                    $"Maquinaria nueva: {result.ImportedMaquinaria}\n" +
                    $"Herramientas nuevas: {result.ImportedHerramientas}",
                    "Importación completada", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (EmbeddedMode)
                {
                    EmbeddedAccepted?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar la matriz externa:\n{ex.Message}",
                    "Importación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            if (EmbeddedMode)
            {
                EmbeddedCancelled?.Invoke(this, EventArgs.Empty);
                return;
            }

            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnNuevaMatriz_Click(object sender, EventArgs e)
        {
            if (EmbeddedMode && WorkspaceChromeHidden)
            {
                EmbeddedRequestNewMatrix?.Invoke(this, EventArgs.Empty);
                return;
            }

            using var formAPU = new FormEditarMatriz(_context, _proyectoId);
            if (formAPU.ShowDialog(GetDialogOwner()) != DialogResult.OK) return;

            CargarMatricesProyectoActual();

            var ultima = _context.Matrices
                .Where(m => m.ProyectoId == _proyectoId)
                .OrderByDescending(m => m.Id)
                .FirstOrDefault();
            if (ultima == null) return;

            FocusMatrixRowById(ultima.Id, $"Matriz '{ultima.Clave}' creada y seleccionada.");
        }

        private void btnEditarMatriz_Click(object sender, EventArgs e)
        {
            if (dgvMatrices.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecciona una matriz para editar.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (EmbeddedMode && WorkspaceChromeHidden)
            {
                EmbeddedRequestEditMatrix?.Invoke(this, EventArgs.Empty);
                return;
            }

            var row      = dgvMatrices.SelectedRows[0];
            var matrizId = Convert.ToInt32(row.Cells["colId"].Value);
            var matriz   = _context.Matrices.Find(matrizId);
            if (matriz == null) return;

            using var form = new FormEditarMatriz(_context, _proyectoId, matriz);
            if (form.ShowDialog(GetDialogOwner()) != DialogResult.OK) return;

            CargarMatricesProyectoActual();
            FocusMatrixRowById(matrizId, $"Matriz '{matriz.Clave}' actualizada.");
        }

        private void btnProyectoActual_Click(object sender, EventArgs e)
        {
            _searchScopeTag = ScopeCurrentTag;
            SelectScopeInCombo();
            CargarMatricesProyectoActual();
        }
    }
}
