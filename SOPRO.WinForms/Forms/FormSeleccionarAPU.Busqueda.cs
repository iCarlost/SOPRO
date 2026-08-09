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
    /// Búsqueda transversal con debounce, alcances y resultados en el grid.
    /// </summary>
    public partial class FormSeleccionarAPU
    {

        private void HandleTipoFiltroChanged()
        {
            if (string.IsNullOrWhiteSpace(txtBuscar.Text)
                && (_searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeCurrentTag))
            {
                CargarMatricesProyectoActual();
                if (_searchScopeTag == ScopeCurrentTag)
                    lblStatus.Text = "Mostrando matrices del proyecto actual.";
                else
                    lblStatus.Text = SelectorUiDefaults.BuildAllScopePrompt("matrices");
                EnsureVisibleSelection();
                return;
            }

            ApplyFilterToGrid();
        }

        private void FocusMatrixRowById(int matrizId, string? statusMessage = null)
        {
            try
            {
                foreach (DataGridViewRow r in dgvMatrices.Rows)
                {
                    if (Convert.ToInt32(r.Cells["colId"].Value) != matrizId) continue;

                    dgvMatrices.ClearSelection();
                    r.Selected = true;
                    foreach (DataGridViewColumn col in dgvMatrices.Columns)
                    {
                        if (!col.Visible) continue;
                        dgvMatrices.CurrentCell = r.Cells[col.Index];
                        break;
                    }
                    if (r.Index >= 0 && r.Index < dgvMatrices.Rows.Count)
                        dgvMatrices.FirstDisplayedScrollingRowIndex = r.Index;

                    dgvMatrices.Focus();
                    if (dgvMatrices.CurrentCell != null)
                    {
                        try { dgvMatrices.BeginEdit(false); dgvMatrices.EndEdit(); } catch { }
                    }

                    if (!string.IsNullOrWhiteSpace(statusMessage))
                        lblStatus.Text = statusMessage;
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                // celda invisible / grid sin estado válido
            }
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e)
        {
            if (_suppressSearchDebounce)
                return;

            // Debounce: esperar 350ms desde el último keystroke antes de ejecutar
            // la búsqueda transversal, que puede abrir múltiples archivos .db.
            if (_searchDebounceTimer == null)
            {
                _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = SelectorUiDefaults.SearchDebounceMs };
                _searchDebounceTimer.Tick += (_, __) =>
                {
                    _searchDebounceTimer.Stop();
                    ApplyFilterToGrid();
                    SaveSelectorContext();
                };
            }
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void ApplyFilterToGrid()
        {
            var busqueda = txtBuscar.Text.Trim();
            if (string.IsNullOrWhiteSpace(busqueda))
            {
                ApplyEmptyScopeState();
                return;
            }

            EjecutarBusquedaTransversal(busqueda);
        }

        private void ApplyEmptyScopeState()
        {
            if (_searchScopeTag == ScopeCurrentTag)
            {
                ShowCurrentProjectBase("Mostrando matrices del proyecto actual.");
                return;
            }

            if (_searchScopeTag == ScopeAllTag)
            {
                ShowCurrentProjectBase(SelectorUiDefaults.BuildAllScopePrompt("matrices"));
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

            CargarMatricesExternas(_searchScopeTag);
        }

        private void ShowCurrentProjectBase(string statusMessage)
        {
            var hasCurrentBaseLoaded = !_showingSearchResults
                && string.IsNullOrWhiteSpace(_externalProjectPath)
                && dgvMatrices.Rows.Count > 0;
            if (!hasCurrentBaseLoaded)
            {
                CargarMatricesProyectoActual();
            }

            lblStatus.Text = statusMessage;
            EnsureVisibleSelection();
        }

        private void ClearGridForEmptyScope(string statusMessage)
        {
            RunGridUpdate(() =>
            {
                _showingSearchResults = false;
                _externalProjectPath = null;
                dgvMatrices.Rows.Clear();
                dgvMatrices.ClearSelection();
                btnEditarMatriz.Enabled = false;
                lblUnidad.Text = "---";
                lblCostoUnitario.Text = "$0.00";
                lblImporte.Text = "$0.00";
                lblStatus.Text = statusMessage;
            });
        }

        private void EjecutarBusquedaTransversal(string busqueda)
        {
            try
            {
                var includeCurrentProject = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeCurrentTag;
                var includeRecentProjects = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeRecentTag;
                var includeFavoriteProjects = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeFavoritesTag;
                IReadOnlyList<CatalogSearchResultDto> results;

                if (_searchScopeTag != ScopeAllTag
                    && _searchScopeTag != ScopeCurrentTag
                    && _searchScopeTag != ScopeRecentTag
                    && _searchScopeTag != ScopeFavoritesTag)
                {
                    results = SearchInSingleProject(busqueda, _searchScopeTag);
                }
                else
                {
                    results = _catalogSearchService.SearchMatrices(
                        _context,
                        _proyectoId,
                        busqueda,
                        includeCurrentProject: includeCurrentProject,
                        includeRecentProjects: includeRecentProjects,
                        includeFavoriteProjects: includeFavoriteProjects,
                        includeAuxiliaries: _includeAuxiliaries,
                        tipoFiltro: GetSelectedTipoFiltro(),
                        maxResults: 120);
                }

                BindSearchResults(results);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar matrices en proyectos indexados:{ex.Message}", "Búsqueda",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IReadOnlyList<CatalogSearchResultDto> SearchInSingleProject(string busqueda, string projectPath)
        {
            if (!File.Exists(projectPath))
                return Array.Empty<CatalogSearchResultDto>();

            var normalizedTarget = SelectorUiDefaults.NormalizePath(projectPath);
            var normalizedCurrent = SelectorUiDefaults.NormalizePath(_context.DatabasePath);
            if (string.Equals(normalizedTarget, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                return _catalogSearchService.SearchMatrices(
                    _context,
                    _proyectoId,
                    busqueda,
                    includeCurrentProject: true,
                    includeRecentProjects: false,
                    includeFavoriteProjects: false,
                    includeAuxiliaries: _includeAuxiliaries,
                    tipoFiltro: GetSelectedTipoFiltro(),
                    maxResults: 120);
            }

            return _catalogSearchService.SearchMatrices(
                _context,
                _proyectoId,
                busqueda,
                includeCurrentProject: false,
                includeRecentProjects: false,
                includeFavoriteProjects: false,
                includeAuxiliaries: _includeAuxiliaries,
                tipoFiltro: GetSelectedTipoFiltro(),
                maxResults: 120,
                specificProjectPaths: new[] { normalizedTarget });
        }

        private void BindSearchResults(IReadOnlyList<CatalogSearchResultDto> results)
        {
            RunGridUpdate(() =>
            {
                _showingSearchResults = true;
                dgvMatrices.Rows.Clear();

                foreach (var result in results)
                {
                    var idx = dgvMatrices.Rows.Add(
                        result.ElementoId,
                        result.EsActual ? GetTipoBadge(result.TipoMatriz) : "🌐 " + GetTipoEtiquetaCorta(result.TipoMatriz),
                        result.Clave,
                        result.Descripcion,
                        result.Unidad,
                        result.PrecioOCosto.ToString("C4"),
                        result.EsActual ? "Actual" : "Externo",
                        result.NombreProyecto,
                        result.FechaReferencia?.ToString("dd/MM/yyyy") ?? string.Empty);

                    dgvMatrices.Rows[idx].Tag = result.EsActual
                        ? new MatrixListItemDto
                        {
                            Id = result.ElementoId,
                            Clave = result.Clave,
                            Descripcion = result.Descripcion,
                            Unidad = result.Unidad,
                            CostoDirecto = result.PrecioOCosto,
                            Tipo = result.TipoMatriz
                        }
                        : new ExternalProjectMatrixOption
                        {
                            MatrixId = result.ElementoId,
                            ProjectName = result.NombreProyecto,
                            ProjectPath = result.RutaProyecto,
                            Clave = result.Clave,
                            Descripcion = result.Descripcion,
                            Unidad = result.Unidad,
                            CostoDirecto = result.PrecioOCosto,
                            Tipo = result.TipoMatriz
                        };
                }

                lblStatus.Text = results.Count == 0
                    ? "Sin coincidencias en el ámbito seleccionado."
                    : $"{results.Count} coincidencia(s) en el ámbito seleccionado.";

                EnsureVisibleSelection();
            });
        }

        private void EnsureVisibleSelection()
        {
            if (dgvMatrices.Rows.Count == 0) return;

            if (dgvMatrices.CurrentRow != null && dgvMatrices.CurrentRow.Visible) return;

            var visible = dgvMatrices.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Visible);
            if (visible == null)
            {
                dgvMatrices.ClearSelection();
                btnEditarMatriz.Enabled = false;
                lblUnidad.Text        = "---";
                lblCostoUnitario.Text = "$0.00";
                lblImporte.Text       = "$0.00";
                return;
            }
            SeleccionarFilaInicial();
        }
    }
}
