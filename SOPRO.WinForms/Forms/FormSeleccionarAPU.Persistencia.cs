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
    /// Persistencia de contexto, cierre, helpers y tipos internos del selector.
    /// </summary>
    public partial class FormSeleccionarAPU
    {

        private void SaveSelectorContext()
        {
            var mode = _searchScopeTag switch
            {
                ScopeCurrentTag => "CurrentOnly",
                ScopeRecentTag => "RecentOnly",
                ScopeFavoritesTag => "FavoritesOnly",
                _ when !string.IsNullOrWhiteSpace(_searchScopeTag)
                     && _searchScopeTag != ScopeAllTag
                     && _searchScopeTag != ScopeCurrentTag
                     && _searchScopeTag != ScopeRecentTag
                     && _searchScopeTag != ScopeFavoritesTag => _searchScopeTag,
                _ => "All"
            };
            var externalForContext = mode.EndsWith("Only", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "All", StringComparison.OrdinalIgnoreCase)
                ? _externalProjectPath
                : _searchScopeTag;
            _selectorContextService.Save(SelectorKey, externalForContext, txtBuscar.Text, mode);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Cancelar búsqueda pendiente y liberar el timer
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Dispose();
            _searchDebounceTimer = null;

            SaveSelectorContext();
            base.OnFormClosing(e);
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        private TipoMatriz? GetSelectedTipoFiltro()
        {
            if (!_includeAuxiliaries || _cboTipo.SelectedItem is not TipoFiltroOption item)
                return null;
            return item.Tipo;
        }

        private static string GetTipoBadge(TipoMatriz tipo) => tipo switch
        {
            TipoMatriz.APU       => "APU",
            TipoMatriz.Basico    => "BÁS",
            TipoMatriz.Cuadrilla => "CUAD",
            _                    => tipo.ToString().ToUpperInvariant()
        };

        private static string GetTipoEtiquetaCorta(TipoMatriz tipo) => tipo switch
        {
            TipoMatriz.APU       => "APU",
            TipoMatriz.Basico    => "Básico",
            TipoMatriz.Cuadrilla => "Cuadrilla",
            _                    => tipo.ToString()
        };

        private string GetCurrentExternalProjectName()
        {
            if (string.IsNullOrWhiteSpace(_externalProjectPath)) return string.Empty;

            // Intentar desde el combo si existe un ítem del proyecto
            foreach (var comboItem in _cboProyecto.Items.OfType<ProyectoComboItem>())
            {
                if (comboItem.Kind == ProyectoComboKind.Project
                    && string.Equals(comboItem.Value, SelectorUiDefaults.NormalizePath(_externalProjectPath), StringComparison.OrdinalIgnoreCase))
                {
                    var parts = comboItem.DisplayText.Split(' ');
                    return parts.Length > 1 ? string.Join(" ", parts.Skip(1)).Trim() : comboItem.DisplayText.Trim();
                }
            }

            // Desde el grid
            var cell = dgvMatrices.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Visible)
                .Select(r => r.Cells["colProyecto"].Value?.ToString())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            return cell ?? Path.GetFileNameWithoutExtension(_externalProjectPath);
        }

        // ════════════════════════════════════════════════════════════════════
        // Clases internas
        // ════════════════════════════════════════════════════════════════════

        private sealed class TipoFiltroOption
        {
            public TipoFiltroOption(string texto, TipoMatriz? tipo) { Texto = texto; Tipo = tipo; }
            public string Texto { get; }
            public TipoMatriz? Tipo { get; }
            public override string ToString() => Texto;
        }

        private enum ProyectoComboKind
        {
            Scope,
            Project,
            Action,
            Separator
        }

        private sealed class ProyectoComboItem
        {
            public ProyectoComboItem(string displayText, string value, ProyectoComboKind kind)
            {
                DisplayText = displayText;
                Value = value;
                Kind = kind;
            }
            public string DisplayText { get; }
            public string Value { get; }
            public ProyectoComboKind Kind { get; }
            public override string ToString() => DisplayText;
        }
    }
}
