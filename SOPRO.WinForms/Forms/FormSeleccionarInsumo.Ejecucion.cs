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
    /// Ejecución de búsqueda transversal y aceptación de la selección.
    /// </summary>
    public partial class FormSeleccionarInsumo
    {

        private void EjecutarBusquedaTransversal(string busqueda)
        {
            try
            {
                var includeCurrentProject = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeCurrentTag;
                var includeRecentProjects = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeRecentTag;
                var includeFavoriteProjects = _searchScopeTag == ScopeAllTag || _searchScopeTag == ScopeFavoritesTag;
                IReadOnlyList<InsumoSearchResultDto> results;
                if (_searchScopeTag != ScopeAllTag && _searchScopeTag != ScopeCurrentTag && _searchScopeTag != ScopeRecentTag && _searchScopeTag != ScopeFavoritesTag)
                {
                    results = _insumoSearchService.SearchInsumos(_context, _proyectoId, _tipoComponente, busqueda, false, false, false, MaxRowsInGrid, new[] { _searchScopeTag }, IncluirManoDeObraIndividual, IncluirCuadrillas);
                }
                else
                {
                    results = _insumoSearchService.SearchInsumos(_context, _proyectoId, _tipoComponente, busqueda, includeCurrentProject, includeRecentProjects, includeFavoriteProjects, MaxRowsInGrid, null, IncluirManoDeObraIndividual, IncluirCuadrillas);
                }

                RunGridUpdate(() =>
                {
                    _showingSearchResults = true;
                    dgvInsumos.Rows.Clear();
                    foreach (var item in results) AddRow(item);
                    lblStatus.Text = results.Count > 0 ? $"{results.Count} resultados para '{busqueda}'." : $"Sin resultados para '{busqueda}'.";
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en búsqueda transversal de insumos:\n{ex.Message}", "Buscar insumos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cboProyecto_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cboProyectoLoading) return;
            if (cboProyecto.SelectedItem is not ProyectoComboItem item) return;
            if (item.Kind == ProyectoComboKind.Separator) return;
            if (item.Kind == ProyectoComboKind.Action && item.Value == ExaminarTag)
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Seleccionar proyecto SOPRO",
                    Filter = "Bases de proyecto SOPRO (*.db;*.sopro)|*.db;*.sopro|Todos los archivos (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _searchScopeTag = SelectorUiDefaults.NormalizePath(dialog.FileName);
                    PopulateProyectoCombo();
                    SelectScopeInCombo();
                    ApplyFilterToGrid();
                }
                else
                {
                    SelectScopeInCombo();
                }
                return;
            }

            _searchScopeTag = item.Value;
            ApplyFilterToGrid();
        }


        private Dictionary<string, object> ObtenerSeleccionEfectiva()
        {
            // Si ya hay acumulados persistentes, Enter/Agregar debe respetar
            // exclusivamente esa selección. Un clic simple posterior solo sirve
            // para moverse por el grid y no debe colarse como un insumo extra.
            if (_selectedItems.Count > 0)
                return new Dictionary<string, object>(_selectedItems, StringComparer.OrdinalIgnoreCase);

            var effective = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in dgvInsumos.SelectedRows)
            {
                var key = BuildSelectionKey(row.Tag);
                if (key != null && row.Tag != null)
                    effective[key] = row.Tag;
            }

            return effective;
        }

        private List<ExternalProjectInsumoOption> ObtenerExternosSeleccionados()
        {
            return _selectedItems.Values.OfType<ExternalProjectInsumoOption>().ToList();
        }

        private List<SelectableInsumoDto> ObtenerInsumosLocalesSeleccionados()
        {
            return _selectedItems.Values.OfType<SelectableInsumoDto>().ToList();
        }

        private void btnAceptar_Click(object sender, EventArgs e)
        {
            if (nudCantidad.Value <= 0)
            {
                MessageBox.Show("La cantidad debe ser mayor a cero.", "Cantidad Inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var seleccionEfectiva = ObtenerSeleccionEfectiva();
            var locales = seleccionEfectiva.Values.OfType<SelectableInsumoDto>().ToList();
            var externos = seleccionEfectiva.Values.OfType<ExternalProjectInsumoOption>().ToList();
            if (locales.Count == 0 && externos.Count == 0)
            {
                MessageBox.Show("Seleccione al menos un insumo de la lista.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ComponentesSeleccionados = new List<ComponenteMatriz>();

            if (locales.Count > 0)
            {
                var ids = new List<int>();
                var tags = new Dictionary<int, string>();
                foreach (var dto in locales)
                {
                    ids.Add(dto.Id);
                    if (!string.IsNullOrWhiteSpace(dto.Tag)) tags[dto.Id] = dto.Tag;
                }
                ComponentesSeleccionados.AddRange(InsumoSelectionService.BuildSelectedComponents(_context, _tipoComponente, ids, tags, nudCantidad.Value));
            }

            if (externos.Count > 0)
            {
                foreach (var grupo in externos.GroupBy(x => SelectorUiDefaults.NormalizePath(x.ProjectPath), StringComparer.OrdinalIgnoreCase))
                {
                    var grupoItems = grupo.ToList();
                    var preview = _externalInsumoImportService.BuildPreview(_context, _proyectoId, grupo.Key, grupoItems);
                    ExternalMatrixImportConflictPolicy policy;
                    using (var previewForm = new FormPreviewImportacionInsumos(preview))
                    {
                        if (previewForm.ShowDialog(this) != DialogResult.OK || !previewForm.SelectedPolicy.HasValue)
                            return;
                        policy = previewForm.SelectedPolicy.Value;
                    }
                    var importResult = _externalInsumoImportService.ImportSelected(_context, _proyectoId, grupo.Key, grupoItems, policy, nudCantidad.Value);
                    ComponentesSeleccionados.AddRange(importResult.ImportedComponents);
                }
            }

            RegisterUsageForSelection(seleccionEfectiva);

            InsumosSeleccionados.Clear();
            foreach (var componente in ComponentesSeleccionados)
            {
                if (componente.Material != null) InsumosSeleccionados.Add(componente.Material);
                else if (componente.ManoDeObra != null) InsumosSeleccionados.Add(componente.ManoDeObra);
                else if (componente.Maquinaria != null) InsumosSeleccionados.Add(componente.Maquinaria);
                else if (componente.Auxiliar != null) InsumosSeleccionados.Add(componente.Auxiliar);
                else if (componente.Herramienta != null) InsumosSeleccionados.Add(componente.Herramienta);
            }

            if (ComponentesSeleccionados.Count == 0)
            {
                MessageBox.Show("Marque al menos un insumo de la lista.", "Selección Requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Cantidad = nudCantidad.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
