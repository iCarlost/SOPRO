using System.ComponentModel;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Models;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Grid de dependencias: edición, combos, normalización y guardado.
    /// </summary>
    public partial class FormProgramaObra
    {

        private void dgvDependencias_UserDeletingRow(object? sender, DataGridViewRowCancelEventArgs e)
        {
            if (_cargando)
                return;

            BeginInvoke(new Action(GuardarDependenciasActuales));
        }

        private void dgvDependencias_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            _cancelandoEdicionDependencia = false;
            _undoDependenciaRowIndex = -1;
            _undoDependenciaColumnIndex = -1;
            _undoDependenciaColumnName = string.Empty;
            _undoDependenciaOldValue = null;

            if (e.RowIndex < 0 || e.RowIndex >= dgvDependencias.Rows.Count)
                return;

            if (dgvDependencias.Rows[e.RowIndex].DataBoundItem is DependenciaEditableRow row)
            {
                var columnName = dgvDependencias.Columns[e.ColumnIndex].Name;
                if (EsColumnaUndoDependencia(columnName))
                {
                    _undoDependenciaRowIndex = e.RowIndex;
                    _undoDependenciaColumnIndex = e.ColumnIndex;
                    _undoDependenciaColumnName = columnName;
                    _undoDependenciaOldValue = ObtenerValorUndoDependencia(row, columnName);
                }
            }
        }

        private void dgvDependencias_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_comboDependenciaActivo != null)
            {
                _comboDependenciaActivo.KeyDown -= ComboDependencia_KeyDown;
                _comboDependenciaActivo = null;
            }

            if (e.Control is ComboBox combo)
            {
                _comboDependenciaActivo = combo;
                combo.KeyDown -= ComboDependencia_KeyDown;
                combo.KeyDown += ComboDependencia_KeyDown;
            }
        }

        private void ComboDependencia_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _cancelandoEdicionDependencia = true;
            }
        }

        private void dgvDependencias_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_cargando || e.RowIndex < 0)
                return;

            if (_cancelandoEdicionDependencia)
            {
                _cancelandoEdicionDependencia = false;
                return;
            }

            try
            {
                if (e.RowIndex >= dgvDependencias.Rows.Count)
                    return;

                var gridRow = dgvDependencias.Rows[e.RowIndex];
                if (gridRow.IsNewRow)
                    return;

                if (gridRow.DataBoundItem is not DependenciaEditableRow row)
                    return;

                NormalizarDependenciaEditada(row);
                GuardarDependenciasActuales();

                if (!_isUndoRedo
                    && e.RowIndex == _undoDependenciaRowIndex
                    && e.ColumnIndex == _undoDependenciaColumnIndex
                    && EsColumnaUndoDependencia(_undoDependenciaColumnName))
                {
                    var oldValue = _undoDependenciaOldValue;
                    var newValue = ObtenerValorUndoDependencia(row, _undoDependenciaColumnName);
                    if (!SonValoresUndoIguales(oldValue, newValue))
                    {
                        int rowIndex = e.RowIndex;
                        string columnName = _undoDependenciaColumnName;
                        string descripcion = $"Editar {columnName} en dependencias";
                        _undoManager.Push(new DelegateUndoableAction(
                            descripcion,
                            () => AplicarUndoRedoDependenciaCelda(rowIndex, columnName, oldValue),
                            () => AplicarUndoRedoDependenciaCelda(rowIndex, columnName, newValue)));
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                // Puede ocurrir cuando se cancela un ComboBox con ESC antes de confirmar un valor.
                _cancelandoEdicionDependencia = false;
            }
            catch (InvalidOperationException)
            {
                // DataGridView puede quedar transitoriamente sin fila enlazada al cancelar edición con ESC.
                _cancelandoEdicionDependencia = false;
            }
            finally
            {
                _undoDependenciaRowIndex = -1;
                _undoDependenciaColumnIndex = -1;
                _undoDependenciaColumnName = string.Empty;
                _undoDependenciaOldValue = null;
            }
        }

        private void NormalizarDependenciaEditada(DependenciaEditableRow row)
        {
            row.DesfaseDias = Math.Max(0, row.DesfaseDias);

            if (!row.ActividadOrigenId.HasValue || row.ActividadOrigenId.Value <= 0)
            {
                row.ActividadOrigenId = null;
                row.Clave = string.Empty;
                row.Descripcion = string.Empty;
                row.TipoDependencia = TipoDependenciaActividad.FS.ToString();
                return;
            }

            var origen = _actividades.FirstOrDefault(a => a.Id == row.ActividadOrigenId.Value && a.Id != GetActividadSeleccionada()?.Id);
            if (origen != null)
            {
                row.Clave = origen.Clave;
                row.Descripcion = origen.Descripcion;
            }
            else
            {
                row.ActividadOrigenId = null;
                row.Clave = string.Empty;
                row.Descripcion = string.Empty;
            }

            if (!Enum.TryParse<TipoDependenciaActividad>(row.TipoDependencia, true, out var tipo))
            {
                row.TipoDependencia = TipoDependenciaActividad.FS.ToString();
            }
            else
            {
                row.TipoDependencia = tipo.ToString();
            }

            dgvDependencias.Refresh();
        }

        private void GuardarDependenciasActuales()
        {
            var actividad = GetActividadSeleccionada();
            if (actividad == null || _programaActual == null)
                return;

            if (actividad.EsResumen)
            {
                lblEstado.Text = "Los agrupadores no admiten dependencias directas. Captura dependencias en conceptos.";
                return;
            }

            var deps = _dependenciasEditables
                .Select(x =>
                {
                    NormalizarDependenciaEditada(x);
                    return x;
                })
                .Where(x => x.ActividadOrigenId.HasValue && x.ActividadOrigenId.Value > 0)
                .GroupBy(x => x.ActividadOrigenId!.Value)
                .Select(g => g.First())
                .Select(x => new DependencyEditDto
                {
                    ActividadOrigenId = x.ActividadOrigenId!.Value,
                    ActividadDestinoId = actividad.Id,
                    TipoDependencia = Enum.TryParse<TipoDependenciaActividad>(x.TipoDependencia, true, out var tipo) ? tipo : TipoDependenciaActividad.FS,
                    DesfaseDias = Math.Max(0, x.DesfaseDias)
                })
                .ToList();

            var state = DataGridViewStateHelper.Capture(dgvActividades);
            var result = _persistenceService.SaveDependencies(_context, actividad.Id, deps);
            if (!result.Ok)
            {
                MessageBox.Show(result.Error, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var programaIdDep = _programaActual.ProgramaObraId;
            var tipoPeriodoDep = ObtenerTipoPeriodoSeleccionado();
            var dbPathDep = _context.DatabasePath;
            SetOcupado(true, $"Actualizando dependencias de '{actividad.Clave}'...");
            _ = Task.Run(() =>
                {
                    using var ctx = _factory.Create(dbPathDep);
                    RegenerarPeriodosYDistribuciones(ctx, programaIdDep, tipoPeriodoDep);
                })
                .ContinueWith(_ =>
                {
                    BeginInvoke(new Action(() =>
                    {
                        SetOcupado(false);
                        lblEstado.Text = $"Dependencias de '{actividad.Clave}' actualizadas.";
                        RecargarProgramaDiferido(state);
                    }));
                });
        }

        private void tabPrograma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cargando)
                return;

            if (tabPrograma.SelectedTab == tabDependencias)
            {
                lblEstado.Text = "Dependencias: selecciona una actividad concepto. Los agrupadores no admiten dependencias. Fin → Inicio (FS): la otra termina y esta inicia. Inicio → Inicio (SS): ambas inician relacionadas.";
                ProcesarDetallePendienteSiAplica(true);
                SolicitarActualizacionDetalleSeleccion(true);
                return;
            }

            if (tabPrograma.SelectedTab == tabDistribucion)
            {
                ProcesarDetallePendienteSiAplica(true);
                return;
            }

            if (tabPrograma.SelectedTab == _tabCurvaS)
            {
                SolicitarActualizacionVisualPrograma();
            }
        }


        private void dgvActividades_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (_cargando)
                return;

            if (EsAtajoDependenciasValido(e.RowIndex, e.ColumnIndex))
            {
                AbrirDependenciasDesdePrograma();
            }
        }

        private void dgvActividades_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_cargando || e.KeyCode != Keys.F2)
                return;

            var cell = dgvActividades.CurrentCell;
            if (cell == null)
                return;

            if (EsAtajoDependenciasValido(cell.RowIndex, cell.ColumnIndex))
            {
                AbrirDependenciasDesdePrograma();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void dgvActividades_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (dgvActividades.Rows[e.RowIndex].DataBoundItem is not ActivityGridRowDto dto)
                return;

            var columnName = dgvActividades.Columns[e.ColumnIndex].Name;
            if (dto.EsResumen)
            {
                if (columnName is "colCantidad" or "colRendimientoDiario" or "colPrecioUnitario")
                {
                    e.Value = string.Empty;
                    e.FormattingApplied = true;
                    return;
                }

                if (columnName == "colFrentes")
                {
                    e.Value = string.Empty;
                    e.FormattingApplied = true;
                    return;
                }
            }

            if (columnName == "colPrecioUnitario" && e.Value is decimal pu)
            {
                e.Value = pu.ToStringImporte();
                e.FormattingApplied = true;
                return;
            }

            if (columnName == "colImporte" && e.Value is decimal importe)
            {
                e.Value = importe.ToStringImporte();
                e.FormattingApplied = true;
                return;
            }

            if (columnName == "colCantidad" && e.Value is decimal cant)
            {
                e.Value = cant.ToString($"N{_proyecto.DecimalesCantidad}",
                    System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
                return;
            }
        }
    }
}
