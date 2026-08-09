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
    /// Combo de proyectos y menú contextual de proyectos favoritos.
    /// </summary>
    public partial class FormSeleccionarInsumo
    {

        private void PopulateProyectoCombo()
        {
            _cboProyectoLoading = true;
            try
            {
                var currentPath = SelectorUiDefaults.NormalizePath(_context.DatabasePath);
                var favorites = _projectIndexService.GetFavoriteProjects().Where(p => File.Exists(p.FilePath)).ToList();
                var recents = _projectIndexService.GetRecentProjects(MaxProjectsInCombo).Where(p => File.Exists(p.FilePath)).ToList();

                cboProyecto.Items.Clear();
                cboProyecto.Items.Add(new ProyectoComboItem("🔎 Todos", ScopeAllTag, ProyectoComboKind.Scope));
                cboProyecto.Items.Add(new ProyectoComboItem("📁 Proyecto actual", ScopeCurrentTag, ProyectoComboKind.Scope));
                cboProyecto.Items.Add(new ProyectoComboItem("🕘 Recientes", ScopeRecentTag, ProyectoComboKind.Scope));
                cboProyecto.Items.Add(new ProyectoComboItem("⭐ Favoritos", ScopeFavoritesTag, ProyectoComboKind.Scope));
                cboProyecto.Items.Add(new ProyectoComboItem("────────────────", string.Empty, ProyectoComboKind.Separator));

                var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in favorites)
                {
                    var path = SelectorUiDefaults.NormalizePath(p.FilePath);
                    if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!added.Add(path)) continue;
                    cboProyecto.Items.Add(new ProyectoComboItem($"★ {p.Name}", path, ProyectoComboKind.Project));
                }
                foreach (var p in recents)
                {
                    var path = SelectorUiDefaults.NormalizePath(p.FilePath);
                    if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!added.Add(path)) continue;
                    cboProyecto.Items.Add(new ProyectoComboItem($"🕘 {p.Name}", path, ProyectoComboKind.Project));
                }
                cboProyecto.Items.Add(new ProyectoComboItem("────────────────", string.Empty, ProyectoComboKind.Separator));
                cboProyecto.Items.Add(new ProyectoComboItem("📁 Examinar archivo...", ExaminarTag, ProyectoComboKind.Action));
                SelectScopeInCombo();
            }
            finally { _cboProyectoLoading = false; }
        }

        private void SelectScopeInCombo()
        {
            _cboProyectoLoading = true;
            try
            {
                for (int i = 0; i < cboProyecto.Items.Count; i++)
                {
                    if (cboProyecto.Items[i] is not ProyectoComboItem item || item.Kind == ProyectoComboKind.Separator) continue;
                    if (string.Equals(item.Value, _searchScopeTag, StringComparison.OrdinalIgnoreCase))
                    {
                        cboProyecto.SelectedIndex = i;
                        return;
                    }
                }
                cboProyecto.SelectedIndex = 0;
            }
            finally { _cboProyectoLoading = false; }
        }

        private void InitializeFavoritosContextMenu()
        {
            ctxProyectoFavorito.Opening += ctxProyectoFavorito_Opening;
            mnuToggleFavorito.Click += mnuToggleFavorito_Click;
            dgvInsumos.ContextMenuStrip = ctxProyectoFavorito;
            dgvInsumos.MouseDown += dgvInsumos_MouseDown;
        }

        private void dgvInsumos_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = dgvInsumos.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0) return;
            dgvInsumos.ClearSelection();
            var row = dgvInsumos.Rows[hit.RowIndex];
            row.Selected = true;
            if (hit.ColumnIndex >= 0)
                dgvInsumos.CurrentCell = row.Cells[hit.ColumnIndex];
        }

        private void ctxProyectoFavorito_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var row = dgvInsumos.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (row?.Tag is not ExternalProjectInsumoOption ext)
            {
                e.Cancel = true;
                return;
            }
            var isFavorite = _projectIndexService.IsFavorite(ext.ProjectPath);
            mnuToggleFavorito.Text = isFavorite ? "Quitar proyecto origen de favoritos" : "Marcar proyecto origen como favorito";
        }

        private void mnuToggleFavorito_Click(object? sender, EventArgs e)
        {
            var row = dgvInsumos.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
            if (row?.Tag is not ExternalProjectInsumoOption ext) return;
            var isFavorite = _projectIndexService.IsFavorite(ext.ProjectPath);
            _projectIndexService.SetFavorite(ext.ProjectPath, !isFavorite, ext.ProjectName);
            PopulateProyectoCombo();
            if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
                EjecutarBusquedaTransversal(txtBuscar.Text.Trim());
            else
                lblStatus.Text = !isFavorite ? $"'{ext.ProjectName}' marcado como favorito." : $"'{ext.ProjectName}' removido de favoritos.";
        }
    }
}
