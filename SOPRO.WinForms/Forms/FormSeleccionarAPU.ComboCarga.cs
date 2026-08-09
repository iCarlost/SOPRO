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
    /// Combo de alcance/proyecto, carga de matrices actuales y externas, y favoritos.
    /// </summary>
    public partial class FormSeleccionarAPU
    {

        private void cboProyecto_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cboProyectoLoading) return;
            if (_cboProyecto.SelectedItem is not ProyectoComboItem item) return;

            if (item.Kind == ProyectoComboKind.Separator)
            {
                SelectScopeInCombo();
                return;
            }

            if (item.Kind == ProyectoComboKind.Action && string.Equals(item.Value, ExaminarTag, StringComparison.OrdinalIgnoreCase))
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Seleccionar proyecto SOPRO",
                    Filter = "Bases de proyecto SOPRO (*.db;*.sopro)|*.db;*.sopro|Todos los archivos (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (dlg.ShowDialog(GetDialogOwner()) == DialogResult.OK)
                {
                    _searchScopeTag = SelectorUiDefaults.NormalizePath(dlg.FileName);
                    ApplyFilterToGrid();
                    PopulateProyectoCombo();
                }
                else
                {
                    SelectScopeInCombo();
                }
                return;
            }

            if (item.Kind == ProyectoComboKind.Scope)
            {
                _searchScopeTag = item.Value;
            }
            else if (item.Kind == ProyectoComboKind.Project)
            {
                _searchScopeTag = SelectorUiDefaults.NormalizePath(item.Value);
            }

            ApplyFilterToGrid();
            SaveSelectorContext();
        }

        // ════════════════════════════════════════════════════════════════════
        // Carga de matrices
        // ════════════════════════════════════════════════════════════════════

        private void CargarMatricesProyectoActual()
        {
            try
            {
                _showingSearchResults = false;
                dgvMatrices.Rows.Clear();
                var selectedTipo = GetSelectedTipoFiltro();
                var matrices = _includeAuxiliaries
                    ? MatrixApplicationService.GetProjectMatrices(_context, _proyectoId, selectedTipo)
                    : MatrixApplicationService.GetProjectApus(_context, _proyectoId);
                var totalMatrices = matrices.Count;
                matrices = matrices.Take(MaxRowsInGrid).ToList();
                var fechaRef = SelectorUiDefaults.GetProjectReferenceDate(_context.DatabasePath);

                foreach (var matriz in matrices)
                {
                    var idx = dgvMatrices.Rows.Add(
                        matriz.Id,
                        GetTipoBadge(matriz.Tipo),
                        matriz.Clave,
                        matriz.Descripcion,
                        matriz.Unidad,
                        matriz.CostoDirecto.ToString("C4"),
                        "Actual",
                        _projectNameActual,
                        fechaRef?.ToString("dd/MM/yyyy") ?? string.Empty);
                    dgvMatrices.Rows[idx].Tag = matriz;
                }

                _projectIndexService.RegisterProjectOpened(_context.DatabasePath, _projectNameActual);
                try { _catalogSearchService.RebuildProjectCatalogIndex(_context.DatabasePath); } catch { }
                _externalProjectPath      = null;
                lblStatus.Text = _includeAuxiliaries
                    ? SelectorUiDefaults.BuildBaseLoadStatus(totalMatrices, matrices.Count, "matrices", "proyecto actual")
                    : SelectorUiDefaults.BuildBaseLoadStatus(totalMatrices, matrices.Count, "APUs", "proyecto actual");

                FinalizeGridAfterLoad();
                SaveSelectorContext();
                PopulateProyectoCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar matrices:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarMatricesExternas(string projectPath)
        {
            try
            {
                _showingSearchResults = false;
                var selectedTipo = GetSelectedTipoFiltro();
                var result = _includeAuxiliaries
                    ? _externalMatrixImportService.LoadExternalMatrices(projectPath, selectedTipo)
                    : _externalMatrixImportService.LoadExternalApus(projectPath);
                dgvMatrices.Rows.Clear();
                var totalMatrices = result.Matrices.Count;
                var matricesToShow = result.Matrices.Take(MaxRowsInGrid).ToList();
                var fechaRef = SelectorUiDefaults.GetProjectReferenceDate(result.ProjectPath);

                foreach (var matriz in matricesToShow)
                {
                    var idx = dgvMatrices.Rows.Add(
                        matriz.MatrixId,
                        "🌐 " + GetTipoEtiquetaCorta(matriz.Tipo),
                        matriz.Clave,
                        matriz.Descripcion,
                        matriz.Unidad,
                        matriz.CostoDirecto.ToString("C4"),
                        "Externo",
                        result.ProjectName,
                        fechaRef?.ToString("dd/MM/yyyy") ?? string.Empty);
                    dgvMatrices.Rows[idx].Tag = matriz;
                }

                _projectIndexService.RegisterProjectOpened(result.ProjectPath, result.ProjectName);
                try { _catalogSearchService.RebuildProjectCatalogIndex(result.ProjectPath); } catch { }
                _externalProjectPath      = result.ProjectPath;
                lblStatus.Text = _includeAuxiliaries
                    ? SelectorUiDefaults.BuildBaseLoadStatus(totalMatrices, matricesToShow.Count, "matrices", $"'{result.ProjectName}'")
                    : SelectorUiDefaults.BuildBaseLoadStatus(totalMatrices, matricesToShow.Count, "APUs", $"'{result.ProjectName}'");

                FinalizeGridAfterLoad();
                SaveSelectorContext();
                _searchScopeTag = SelectorUiDefaults.NormalizePath(result.ProjectPath);
                PopulateProyectoCombo();
                SelectScopeInCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar matrices externas:\n{ex.Message}",
                    "Proyecto externo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Sincroniza el combo con el alcance activo actual.</summary>
        private void SelectScopeInCombo()
        {
            _cboProyectoLoading = true;
            try
            {
                var target = _searchScopeTag;
                for (int i = 0; i < _cboProyecto.Items.Count; i++)
                {
                    if (_cboProyecto.Items[i] is not ProyectoComboItem item) continue;
                    if (item.Kind == ProyectoComboKind.Separator) continue;
                    if (string.Equals(item.Value, target, StringComparison.OrdinalIgnoreCase))
                    {
                        _cboProyecto.SelectedIndex = i;
                        return;
                    }
                }
                _cboProyecto.SelectedIndex = 0;
            }
            finally
            {
                _cboProyectoLoading = false;
            }
        }

        private void InitializeFavoritosContextMenu()
        {
            ctxProyectoFavorito.Opening += ctxProyectoFavorito_Opening;
            mnuToggleFavorito.Click += mnuToggleFavorito_Click;
            dgvMatrices.MouseDown += dgvMatrices_MouseDown;
            dgvMatrices.ContextMenuStrip = ctxProyectoFavorito;
        }

        private void dgvMatrices_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = dgvMatrices.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0) return;

            dgvMatrices.ClearSelection();
            var row = dgvMatrices.Rows[hit.RowIndex];
            row.Selected = true;
            if (hit.ColumnIndex >= 0)
                dgvMatrices.CurrentCell = dgvMatrices.Rows[hit.RowIndex].Cells[hit.ColumnIndex];
        }

        private void ctxProyectoFavorito_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var row = dgvMatrices.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (row?.Tag is not ExternalProjectMatrixOption externalMatrix)
            {
                e.Cancel = true;
                return;
            }

            var isFavorite = _projectIndexService.IsFavorite(externalMatrix.ProjectPath);
            mnuToggleFavorito.Text = isFavorite
                ? "Quitar proyecto origen de favoritos"
                : "Marcar proyecto origen como favorito";
        }

        private void mnuToggleFavorito_Click(object? sender, EventArgs e)
        {
            var row = dgvMatrices.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (row?.Tag is not ExternalProjectMatrixOption externalMatrix) return;

            var isFavorite = _projectIndexService.IsFavorite(externalMatrix.ProjectPath);
            _projectIndexService.SetFavorite(externalMatrix.ProjectPath, !isFavorite, externalMatrix.ProjectName);
            PopulateProyectoCombo();
            SaveSelectorContext();

            if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
                EjecutarBusquedaTransversal(txtBuscar.Text.Trim());
            else
                lblStatus.Text = !isFavorite
                    ? $"'{externalMatrix.ProjectName}' marcado como favorito."
                    : $"'{externalMatrix.ProjectName}' removido de favoritos.";
        }

        private void FinalizeGridAfterLoad()
        {
            if (string.IsNullOrWhiteSpace(txtBuscar.Text))
            {
                foreach (DataGridViewRow row in dgvMatrices.Rows)
                    row.Visible = true;
                EnsureVisibleSelection();
                return;
            }

            EjecutarBusquedaTransversal(txtBuscar.Text.Trim());
        }
    }
}
