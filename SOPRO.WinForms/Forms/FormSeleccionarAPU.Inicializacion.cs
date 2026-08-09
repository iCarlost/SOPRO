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
    /// Optimizaciones de render, selector de tipo, combo de proyectos y filtro inicial.
    /// </summary>
    public partial class FormSeleccionarAPU
    {

        private void ApplyRenderOptimizations()
        {
            FormRenderHelper.OptimizeForGridRendering(this);
            EnableDoubleBuffer(dgvMatrices);
            EnableDoubleBuffer(panelTop);
            EnableDoubleBuffer(panelInfo);
        }

        private static void EnableDoubleBuffer(Control control)
        {
            if (control == null) return;

            var prop = typeof(Control).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop?.SetValue(control, true, null);
        }

        private void RunGridUpdate(Action action)
        {
            if (action == null) return;

            SuspendLayout();
            panelInfo.SuspendLayout();
            dgvMatrices.SuspendLayout();
            using (GridRedrawHelper.Suspend(dgvMatrices))
            {
                try
                {
                    action();
                }
                finally
                {
                    dgvMatrices.ResumeLayout();
                    panelInfo.ResumeLayout();
                    ResumeLayout(true);
                }
            }
        }

        private void InitializeTipoSelector()
        {
            if (!_includeAuxiliaries) return;

            // Cargar ítems (TipoFiltroOption es clase interna, no serializable en Designer)
            _cboTipo.Items.Add(new TipoFiltroOption("Todos",     null));
            _cboTipo.Items.Add(new TipoFiltroOption("APU",       TipoMatriz.APU));
            _cboTipo.Items.Add(new TipoFiltroOption("Básico",    TipoMatriz.Basico));
            _cboTipo.Items.Add(new TipoFiltroOption("Cuadrilla", TipoMatriz.Cuadrilla));
            _cboTipo.SelectedIndex = 0;
            _cboTipo.SelectedIndexChanged += (_, __) => HandleTipoFiltroChanged();
            _cboTipo.Visible = true;
        }

        /// <summary>
        /// Carga el selector de alcance/origen de búsqueda en _cboProyecto.
        /// </summary>
        private void PopulateProyectoCombo()
        {
            _cboProyectoLoading = true;
            try
            {
                var currentPath = SelectorUiDefaults.NormalizePath(_context.DatabasePath);
                var favorites = _projectIndexService.GetFavoriteProjects()
                    .Where(p => File.Exists(p.FilePath))
                    .ToList();
                var recents = _projectIndexService.GetRecentProjects(MaxProjectsInCombo)
                    .Where(p => File.Exists(p.FilePath))
                    .ToList();

                _cboProyecto.Items.Clear();
                _cboProyecto.Items.Add(new ProyectoComboItem("🔎 Todos", ScopeAllTag, ProyectoComboKind.Scope));
                _cboProyecto.Items.Add(new ProyectoComboItem("📁 Proyecto actual", ScopeCurrentTag, ProyectoComboKind.Scope));
                _cboProyecto.Items.Add(new ProyectoComboItem("🕘 Recientes", ScopeRecentTag, ProyectoComboKind.Scope));
                _cboProyecto.Items.Add(new ProyectoComboItem("⭐ Favoritos", ScopeFavoritesTag, ProyectoComboKind.Scope));
                _cboProyecto.Items.Add(new ProyectoComboItem("────────────────", string.Empty, ProyectoComboKind.Separator));

                var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in favorites)
                {
                    var path = SelectorUiDefaults.NormalizePath(p.FilePath);
                    if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!added.Add(path)) continue;
                    _cboProyecto.Items.Add(new ProyectoComboItem($"★ {p.Name}", path, ProyectoComboKind.Project));
                }

                foreach (var p in recents)
                {
                    var path = SelectorUiDefaults.NormalizePath(p.FilePath);
                    if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!added.Add(path)) continue;
                    _cboProyecto.Items.Add(new ProyectoComboItem($"🕘 {p.Name}", path, ProyectoComboKind.Project));
                }

                _cboProyecto.Items.Add(new ProyectoComboItem("────────────────", string.Empty, ProyectoComboKind.Separator));
                _cboProyecto.Items.Add(new ProyectoComboItem("📁 Examinar archivo...", ExaminarTag, ProyectoComboKind.Action));

                SelectScopeInCombo();
            }
            finally
            {
                _cboProyectoLoading = false;
            }
        }

        /// <summary>Restaura contexto mínimo del selector. El alcance del combo siempre inicia en "Todos".</summary>
        private void RestoreSelectorContext()
        {
            var state = _selectorContextService.Get(SelectorKey);
            if (!string.IsNullOrWhiteSpace(state.LastExternalProjectPath)
                && File.Exists(state.LastExternalProjectPath))
            {
                _externalProjectPath = state.LastExternalProjectPath;
            }

            _searchScopeTag = ScopeAllTag;
        }

        private void ApplyInitialFilter()
        {
            if (!string.IsNullOrWhiteSpace(_filtroInicial))
            {
                _suppressSearchDebounce = true;
                try
                {
                    txtBuscar.Text = _filtroInicial.Trim();
                    txtBuscar.SelectionStart = txtBuscar.TextLength;
                }
                finally
                {
                    _suppressSearchDebounce = false;
                }

                ApplyFilterToGrid();
            }
            else
            {
                _suppressSearchDebounce = true;
                try
                {
                    txtBuscar.Clear();
                }
                finally
                {
                    _suppressSearchDebounce = false;
                }

                ApplyFilterToGrid();
            }
        }
    }
}
