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
    /// Filtros, búsqueda y estados de ámbito vacío del selector.
    /// </summary>
    public partial class FormSeleccionarInsumo
    {

        private void dgvInsumos_SelectionChanged(object? sender, EventArgs e)
        {
            RefreshAccumulatedSelectionFromGrid();
        }

        private void dgvInsumos_KeyDown(object? sender, KeyEventArgs e)
        {
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
                return;
            }

            if (e.KeyCode == Keys.Back)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                FocusSearchBox();
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                FocusSearchBox();
                txtBuscar.Clear();
            }
        }

        private void dgvInsumos_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex >= 0)
                dgvInsumos.CurrentCell = dgvInsumos.Rows[e.RowIndex].Cells[e.ColumnIndex];
            btnAceptar.PerformClick();
        }

        private static string? BuildSelectionKey(object? tag)
        {
            return tag switch
            {
                SelectableInsumoDto dto => $"LOCAL|{dto.TipoComponente}|{dto.Id}|{dto.Tag?.Trim().ToUpperInvariant()}",
                ExternalProjectInsumoOption ext => $"EXT|{NormalizeStaticPath(ext.ProjectPath)}|{ext.TipoComponente}|{ext.ItemId}|{ext.Tag?.Trim().ToUpperInvariant()}",
                _ => null
            };
        }

        private static string NormalizeStaticPath(string path) => Path.GetFullPath(path.Trim());

        private void txtBuscar_TextChanged(object sender, EventArgs e)
        {
            if (_searchDebounceTimer == null)
            {
                _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = SelectorUiDefaults.SearchDebounceMs };
                _searchDebounceTimer.Tick += (_, __) =>
                {
                    _searchDebounceTimer.Stop();
                    ApplyFilterToGrid();
                };
            }
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void ApplyFilterToGrid(bool forceCurrentReload = false)
        {
            var busqueda = txtBuscar.Text.Trim();
            if (string.IsNullOrWhiteSpace(busqueda))
            {
                ApplyEmptyScopeState(forceCurrentReload);
                return;
            }
            EjecutarBusquedaTransversal(busqueda);
        }

        private void ApplyEmptyScopeState(bool forceCurrentReload = false)
        {
            if (_searchScopeTag == ScopeCurrentTag)
            {
                ShowCurrentProjectBase("Mostrando insumos del proyecto actual.", forceCurrentReload);
                return;
            }
            if (_searchScopeTag == ScopeAllTag)
            {
                ShowCurrentProjectBase(SelectorUiDefaults.BuildAllScopePrompt("insumos"), forceCurrentReload);
                return;
            }
            if (_searchScopeTag == ScopeRecentTag)
            {
                ClearGridForEmptyScope(SelectorUiDefaults.BuildRecentScopePrompt());
                return;
            }
            if (_searchScopeTag == ScopeFavoritesTag)
            {
                ClearGridForEmptyScope(SelectorUiDefaults.BuildFavoritesScopePrompt());
                return;
            }
            CargarInsumosExternos(_searchScopeTag);
        }

        private void ShowCurrentProjectBase(string statusMessage, bool forceReload = false)
        {
            var hasCurrentBaseLoaded = !_showingSearchResults && string.IsNullOrWhiteSpace(_externalProjectPath) && dgvInsumos.Rows.Count > 0;
            if (forceReload || !hasCurrentBaseLoaded) CargarInsumosActuales();
            lblStatus.Text = statusMessage;
        }

        private void ClearGridForEmptyScope(string statusMessage)
        {
            RunGridUpdate(() =>
            {
                _showingSearchResults = false;
                _externalProjectPath = null;
                dgvInsumos.Rows.Clear();
                dgvInsumos.ClearSelection();
                lblStatus.Text = statusMessage;
            });
        }
    }
}
