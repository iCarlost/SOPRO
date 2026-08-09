using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga de datos y solicitudes de recarga del programa de insumos.
    /// </summary>
    public partial class FormProgramaInsumos
    {

        private void CargarProgramaInsumos()
        {
            var tipo = cboTipo.ComboBox.SelectedItem is ProgramaInsumoTipo t ? t : ProgramaInsumoTipo.Materiales;
            var vista = cboVista.ComboBox.SelectedItem is VistaProgramaInsumos v ? v : VistaProgramaInsumos.Cantidades;
            var estado = DataGridViewStateHelper.Capture(dgvProgramaInsumos);
            var selectedInsumoId = ObtenerInsumoSeleccionadoId();

            _cargando = true;
            dgvProgramaInsumos.SuspendLayout();
            splitPrincipal.SuspendLayout();
            try
            {
                _actual = _service.Build(_context, _proyecto, tipo);
                ConstruirGrid(_actual, tipo);
                ColumnasProgramaInsumosHelper.SincronizarDesdeGrid(_context, _proyecto.Id, dgvProgramaInsumos.Columns);
                _columnasConfig = ColumnasProgramaInsumosHelper.ObtenerColumnas(_context, _proyecto.Id);
                AplicarConfiguracionColumnas();
                DataGridViewStateHelper.Restore(dgvProgramaInsumos, estado);
                RestaurarSeleccionInsumo(selectedInsumoId);

                if (dgvProgramaInsumos.Rows.Count > 0 && dgvProgramaInsumos.CurrentCell == null)
                {
                    var safeCell = DataGridViewStateHelper.GetFirstVisibleCell(dgvProgramaInsumos);
                    if (safeCell != null)
                    {
                        dgvProgramaInsumos.ClearSelection();
                        safeCell.OwningRow.Selected = true;
                        dgvProgramaInsumos.CurrentCell = safeCell;
                    }
                }
                else if (dgvProgramaInsumos.Rows.Count == 0)
                {
                    dgvProgramaInsumos.ClearSelection();
                }

                ActualizarGanttInsumos(tipo, vista);

                lblResumen.Text = _actual.Periodos.Count == 0 ? "No hay programa de obra generado o no existen periodos." : $"{_actual.Rows.Count:N0} insumos · {_actual.Periodos.Count:N0} periodos · Tipo: {tipo} · Vista: {vista}";
                lblEstado.Text = _actual.Rows.Count == 0 ? "Sin datos de programa de insumos." : $"Total insumos: {_actual.Rows.Count:N0}";
            }
            finally
            {
                splitPrincipal.ResumeLayout(true);
                dgvProgramaInsumos.ResumeLayout(true);
                _cargando = false;
            }

            SolicitarActualizacionVisualInsumos(true);
            GuardarLayoutPersistido();
        }


        private void SolicitarRecargaProgramaInsumos(bool forzar = false)
        {
            if (_programaReloadPending)
                return;

            _programaReloadPending = true;
            BeginInvokeSeguro(() =>
            {
                _programaReloadPending = false;
                if (IsDisposed)
                    return;

                if (_cargando && !forzar)
                    return;

                CargarProgramaInsumos();
            });
        }

        private void SolicitarActualizacionVisualInsumos(bool forzar = false)
        {
            if (_visualRefreshPending)
                return;

            _visualRefreshPending = true;
            BeginInvokeSeguro(() =>
            {
                _visualRefreshPending = false;
                if (IsDisposed || _ganttControl == null || !_ganttControl.Visible)
                    return;

                if (_cargando && !forzar)
                    return;

                _ganttControl.SelectedActivityId = ObtenerInsumoSeleccionadoId();
                _ganttControl.RequestRefresh();
            });
        }

        private void BeginInvokeSeguro(Action action)
        {
            if (IsDisposed)
                return;

            if (IsHandleCreated)
            {
                BeginInvoke(action);
                return;
            }

            if (_ganttControl != null && _ganttControl.IsHandleCreated)
            {
                _ganttControl.BeginInvoke(action);
                return;
            }

            EventHandler? handler = null;
            handler = (_, __) =>
            {
                HandleCreated -= handler;
                if (!IsDisposed)
                    BeginInvoke(action);
            };
            HandleCreated += handler;
        }


        private void RestaurarSeleccionInsumo(int? insumoId)
        {
            if (!insumoId.HasValue || dgvProgramaInsumos.Rows.Count == 0)
                return;

            foreach (DataGridViewRow row in dgvProgramaInsumos.Rows)
            {
                if (row.Tag is not int rowId || rowId != insumoId.Value)
                    continue;

                var firstVisibleCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible && c.OwningColumn.Visible);
                if (firstVisibleCell == null)
                    return;

                dgvProgramaInsumos.ClearSelection();
                row.Selected = true;
                dgvProgramaInsumos.CurrentCell = firstVisibleCell;
                return;
            }
        }
    }
}
