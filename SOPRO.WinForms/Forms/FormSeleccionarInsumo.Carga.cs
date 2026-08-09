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
    /// Carga de insumos locales y externos en el grid.
    /// </summary>
    public partial class FormSeleccionarInsumo
    {

        private void CargarInsumosActuales()
        {
            try
            {
                RunGridUpdate(() =>
                {
                    _showingSearchResults = false;
                    _externalProjectPath = null;
                    dgvInsumos.Rows.Clear();
                    var items = InsumoSelectionService.GetSelectableInsumos(_context, _proyectoId, _tipoComponente, Filtro(), IncluirManoDeObraIndividual, IncluirCuadrillas);
                    var shown = items.Take(MaxRowsInGrid).ToList();
                    var fechaRef = SelectorUiDefaults.GetProjectReferenceDate(_context.DatabasePath);
                    foreach (var item in shown)
                    {
                        AddRow(item, true, _projectNameActual, fechaRef);
                    }
                    RestoreSelections();
                    _projectIndexService.RegisterProjectOpened(_context.DatabasePath, _projectNameActual);
                    lblStatus.Text = SelectorUiDefaults.BuildBaseLoadStatus(items.Count, shown.Count, ObtenerNombreTipo().ToLowerInvariant() + "s", "proyecto actual");
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar insumos:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarInsumosExternos(string projectPath)
        {
            try
            {
                RunGridUpdate(() =>
                {
                    _showingSearchResults = false;
                    dgvInsumos.Rows.Clear();
                    var result = _externalInsumoImportService.LoadExternalInsumos(projectPath, _tipoComponente, Filtro(), IncluirManoDeObraIndividual, IncluirCuadrillas);
                    var shown = result.Items.Take(MaxRowsInGrid).ToList();
                    var fechaRef = SelectorUiDefaults.GetProjectReferenceDate(result.ProjectPath);
                    foreach (var item in shown)
                        AddRow(item, false, result.ProjectName, fechaRef);
                    RestoreSelections();
                    _externalProjectPath = result.ProjectPath;
                    _projectIndexService.RegisterProjectOpened(result.ProjectPath, result.ProjectName);
                    try { _insumoSearchService.RebuildProjectInsumoIndex(result.ProjectPath); } catch { }
                    lblStatus.Text = SelectorUiDefaults.BuildBaseLoadStatus(result.Items.Count, shown.Count, "insumos", $"'{result.ProjectName}'");
                    _searchScopeTag = SelectorUiDefaults.NormalizePath(result.ProjectPath);
                    PopulateProyectoCombo();
                    SelectScopeInCombo();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar insumos externos:\n{ex.Message}", "Proyecto externo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddRow(SelectableInsumoDto item, bool esActual, string projectName, DateTime? fechaRef)
        {
            var idx = dgvInsumos.Rows.Add(item.Id, item.Clave, item.Descripcion, item.Unidad, item.PrecioMostrado, esActual ? "Actual" : "Externo", projectName, fechaRef?.ToString("dd/MM/yyyy") ?? string.Empty);
            dgvInsumos.Rows[idx].Tag = item;
            ApplyAccumulatedState(dgvInsumos.Rows[idx], reselectVisibleRow: true);
        }

        private void AddRow(ExternalProjectInsumoOption item, bool esActual, string projectName, DateTime? fechaRef)
        {
            var idx = dgvInsumos.Rows.Add(item.ItemId, item.Clave, item.Descripcion, item.Unidad, item.PrecioMostrado, esActual ? "Actual" : "Externo", projectName, fechaRef?.ToString("dd/MM/yyyy") ?? string.Empty);
            dgvInsumos.Rows[idx].Tag = item;
            ApplyAccumulatedState(dgvInsumos.Rows[idx], reselectVisibleRow: true);
        }

        private void AddRow(InsumoSearchResultDto item)
        {
            object tagObj = item.EsActual
                ? new SelectableInsumoDto
                {
                    Id = item.ElementoId,
                    TipoComponente = item.TipoComponente,
                    Tag = item.Tag ?? string.Empty,
                    Clave = item.Clave,
                    Descripcion = item.Descripcion,
                    Unidad = item.Unidad,
                    PrecioUnitario = item.PrecioUnitario,
                    PrecioMostrado = item.PrecioMostrado
                }
                : new ExternalProjectInsumoOption
                {
                    ItemId = item.ElementoId,
                    ProjectName = item.NombreProyecto,
                    ProjectPath = item.RutaProyecto,
                    TipoComponente = item.TipoComponente,
                    Tag = item.Tag,
                    Clave = item.Clave,
                    Descripcion = item.Descripcion,
                    Unidad = item.Unidad,
                    PrecioUnitario = item.PrecioUnitario,
                    PrecioMostrado = item.PrecioMostrado
                };

            var idx = dgvInsumos.Rows.Add(item.ElementoId, item.Clave, item.Descripcion, item.Unidad, item.PrecioMostrado, item.EsActual ? "Actual" : "Externo", item.NombreProyecto, item.FechaReferencia?.ToString("dd/MM/yyyy") ?? string.Empty);
            dgvInsumos.Rows[idx].Tag = tagObj;
            ApplyAccumulatedState(dgvInsumos.Rows[idx], reselectVisibleRow: true);
        }
    }
}
