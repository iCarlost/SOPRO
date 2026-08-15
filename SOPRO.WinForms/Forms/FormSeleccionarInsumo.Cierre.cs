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
    /// Registro de uso, botones de cierre y eventos finales del selector.
    /// </summary>
    public partial class FormSeleccionarInsumo
    {

        private void RegisterUsageForSelection(IReadOnlyDictionary<string, object> seleccionEfectiva)
        {
            foreach (var item in seleccionEfectiva.Values)
            {
                switch (item)
                {
                    case SelectableInsumoDto dto:
                        _projectUsageService.RegisterInsumoSelection(_context.DatabasePath, dto.TipoComponente, dto.Id, dto.Tag);
                        break;
                    case ExternalProjectInsumoOption ext:
                        _projectUsageService.RegisterInsumoSelection(ext.ProjectPath, ext.TipoComponente, ext.ItemId, ext.Tag);
                        break;
                }
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnNuevoInsumo_Click(object sender, EventArgs e)
        {
            int? nuevoId = null;
            string? nuevoTag = null;
            switch (_tipoComponente)
            {
                case TipoComponenteMatriz.Material:
                    // N4: la sesión es datos puros (no posee contexto): no se dispone.
                    var formSession = LegacySessionBridge.FromLegacy(_context, _proyectoId);
                    using (var form = new FormEditarMaterial(formSession))
                    {
                        // El Id del material creado lo devuelve el caso de uso
                        // (SaveMaterialResult.MaterialId); no se adivina con el
                        // Id máximo del catálogo. Si se guardó en el maestro, el
                        // Id pertenece a otra base: NO se compara contra las
                        // filas del proyecto (una coincidencia numérica
                        // seleccionaría un material local equivocado).
                        if (form.ShowDialog(this) == DialogResult.OK && !form.UltimoGuardadoEnMaestro)
                            nuevoId = form.UltimoMaterialIdGuardado;
                    }
                    break;
                case TipoComponenteMatriz.ManoDeObra:
                    if (rbMOCuadrillas.Checked)
                    {
                        using (var form = new FormEditarMatriz(_context, _proyectoId, tipoInicial: TipoMatriz.Cuadrilla))
                        {
                            if (form.ShowDialog(this) == DialogResult.OK)
                            {
                                nuevoId = _context.Matrices
                                    .Where(m => m.ProyectoId == _proyectoId && m.Tipo == TipoMatriz.Cuadrilla)
                                    .OrderByDescending(m => m.Id)
                                    .Select(m => (int?)m.Id)
                                    .FirstOrDefault();
                                nuevoTag = "Cuadrilla";
                            }
                        }
                    }
                    else
                    {
                        var proyFSR = _context.Proyectos.Find(_proyectoId);
                        using (var form = new FormEditarManoObra(_context, _proyectoId, proyecto: proyFSR))
                        {
                            if (form.ShowDialog(this) == DialogResult.OK)
                                nuevoId = _context.ManoDeObra.Where(m => m.ProyectoId == _proyectoId).OrderByDescending(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault();
                        }
                    }
                    break;
                case TipoComponenteMatriz.Maquinaria:
                    using (var form = new FormEditarMaquinaria(_context, _proyectoId))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK)
                            nuevoId = _context.Maquinaria.Where(m => m.ProyectoId == _proyectoId).OrderByDescending(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault();
                    }
                    break;
                case TipoComponenteMatriz.Auxiliar:
                    using (var form = new FormEditarMatriz(_context, _proyectoId))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK)
                            nuevoId = _context.Matrices.Where(m => m.ProyectoId == _proyectoId && m.Tipo == TipoMatriz.Basico).OrderByDescending(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault();
                    }
                    break;
                case TipoComponenteMatriz.Herramienta:
                    using (var form = new FormEditarHerramienta(_context, _proyectoId))
                    {
                        if (form.ShowDialog(this) == DialogResult.OK)
                            nuevoId = _context.Herramientas.Where(h => h.ProyectoId == _proyectoId).OrderByDescending(h => h.Id).Select(h => (int?)h.Id).FirstOrDefault();
                    }
                    break;
            }

            if (nuevoId.HasValue)
            {
                txtBuscar.Text = string.Empty;
                _searchScopeTag = ScopeCurrentTag;
                SelectScopeInCombo();
                CargarInsumosActuales();
                dgvInsumos.ClearSelection();
                foreach (DataGridViewRow row in dgvInsumos.Rows)
                {
                    var rowId = Convert.ToInt32(row.Cells["colId"].Value);
                    var rowTag = row.Tag;
                    var isExpectedTag = nuevoTag == null ||
                        (rowTag is SelectableInsumoDto dto && string.Equals(dto.Tag, nuevoTag, StringComparison.OrdinalIgnoreCase)) ||
                        (rowTag is ExternalProjectInsumoOption ext && string.Equals(ext.Tag, nuevoTag, StringComparison.OrdinalIgnoreCase));

                    if (rowId == nuevoId.Value && isExpectedTag)
                    {
                        var targetCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible) ?? row.Cells[0];
                        row.Selected = true;
                        dgvInsumos.CurrentCell = targetCell;
                        if (row.Index >= 0)
                            dgvInsumos.FirstDisplayedScrollingRowIndex = row.Index;
                        dgvInsumos.Focus();
                        BeginInvoke(new Action(() => dgvInsumos.Focus()));
                        break;
                    }
                }
            }
        }

        private void rbFiltroMO_CheckedChanged(object sender, EventArgs e)
        {
            if (_tipoComponente != TipoComponenteMatriz.ManoDeObra) return;
            ApplyFilterToGrid(forceCurrentReload: true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_searchDebounceTimer != null)
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Dispose();
                _searchDebounceTimer = null;
            }
            base.OnFormClosing(e);
        }

        private sealed class ProyectoComboItem
        {
            public ProyectoComboItem(string display, string value, ProyectoComboKind kind)
            {
                Display = display;
                Value = value;
                Kind = kind;
            }
            public string Display { get; }
            public string Value { get; }
            public ProyectoComboKind Kind { get; }
            public override string ToString() => Display;
        }

        private enum ProyectoComboKind
        {
            Scope,
            Project,
            Separator,
            Action
        }
    }
}
