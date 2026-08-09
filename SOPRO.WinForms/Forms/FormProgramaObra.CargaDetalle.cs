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
    /// Carga del programa, detalle de la actividad seleccionada y estado general del grid.
    /// </summary>
    public partial class FormProgramaObra
    {

        private void CargarPrograma(DataGridViewStateSnapshot? stateToRestore = null)
        {
            dgvActividades.SuspendLayout();
            dgvPeriodos.SuspendLayout();
            dgvDistribucion.SuspendLayout();
            dgvDependencias.SuspendLayout();

            try
            {
                _cargando = true;
                _programaActual = _loadService.LoadProgram(_context, _proyecto.Id);
                _actividades = new BindingList<ActivityGridRowDto>();
                _periodos = new BindingList<PeriodEditDto>();

                if (_programaActual == null)
                {
                    dgvActividades.DataSource = _actividades;
                    dgvPeriodos.DataSource = _periodos;
                    dgvDistribucion.DataSource = null;
                    dgvDependencias.DataSource = null;
                    LimpiarCurvaS();
                    ActualizarGantt();
                    lblEstado.Text = "No existe un programa de obra generado para este proyecto.";
                    return;
                }

                cmbTipoPeriodo.SelectedItem = _programaActual.TipoPeriodo;
                _actividades = new BindingList<ActivityGridRowDto>(_programaActual.Actividades.OrderBy(a => a.Orden).ToList());
                _periodos = new BindingList<PeriodEditDto>(_programaActual.Periodos.OrderBy(p => p.NumeroPeriodo).ToList());

                RecargarLookupActividades();
                dgvActividades.DataSource = _actividades;
                dgvPeriodos.DataSource = _periodos;

                FormatearGridActividades();
                FormatearGridPeriodos();
                AplicarConfiguracionColumnas();

                ActualizarEstadoGeneral();
                CargarCurvaS();
                ActualizarGantt();

                _detalleSeleccionDeferred = false;
                _ultimaActividadDetalleId = null;

                if (dgvActividades.Rows.Count > 0)
                {
                    var restoreState = stateToRestore ?? _pendingGridStateRestore;
                    if (restoreState != null)
                    {
                        DataGridViewStateHelper.Restore(dgvActividades, restoreState);
                    }

                    if (dgvActividades.CurrentCell == null)
                        DataGridViewStateHelper.EnsureSafeCurrentCell(dgvActividades);

                    SolicitarActualizacionDetalleSeleccion(true);
                    SolicitarActualizacionVisualPrograma(true);
                }
                else
                {
                    dgvActividades.ClearSelection();
                    dgvDistribucion.DataSource = null;
                    dgvDependencias.DataSource = null;
                    _detalleSeleccionDeferred = false;
                    _ultimaActividadDetalleId = null;
                    if (_ganttControl != null)
                        _ganttControl.SelectedActivityId = null;
                    SolicitarRedibujoCurvaS();
                }

                _pendingGridStateRestore = null;
            }
            finally
            {
                _cargando = false;
                dgvDependencias.ResumeLayout();
                dgvDistribucion.ResumeLayout();
                dgvPeriodos.ResumeLayout();
                dgvActividades.ResumeLayout();
            }
        }

        private void RecargarLookupActividades()
        {
            _actividadesLookup.Clear();
            foreach (var act in _actividades.Where(a => !a.EsResumen).OrderBy(a => a.Clave))
            {
                _actividadesLookup.Add(new ActividadLookupItem
                {
                    Id = act.Id,
                    Display = string.IsNullOrWhiteSpace(act.Clave) ? act.Descripcion : $"{act.Clave} - {act.Descripcion}"
                });
            }
        }

        private void FormatearGridActividades()
        {
            foreach (DataGridViewRow row in dgvActividades.Rows)
            {
                if (row.DataBoundItem is ActivityGridRowDto dto)
                {
                    row.DefaultCellStyle.BackColor = dto.EsResumen ? Color.FromArgb(240, 240, 240) : Color.White;

                    if (dgvActividades.Columns.Contains("colDescripcion"))
                    {
                        var descCell = row.Cells["colDescripcion"];
                        descCell.Style.Padding = new Padding(Math.Max(0, dto.Nivel) * 18, 0, 0, 0);
                    }
                }
            }
        }

        private void FormatearGridPeriodos()
        {
            foreach (DataGridViewColumn col in dgvPeriodos.Columns)
            {
                col.ReadOnly = true;
            }
        }

        private void dgvActividades_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargando)
                return;

            SolicitarActualizacionDetalleSeleccion();
            SolicitarActualizacionVisualPrograma();
        }

        private void dgvActividades_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            _undoActividadRowIndex = -1;
            _undoActividadColumnIndex = -1;
            _undoActividadColumnName = string.Empty;
            _undoActividadOldValue = null;

            if (dgvActividades.Rows[e.RowIndex].DataBoundItem is ActivityGridRowDto dto && dto.EsResumen)
            {
                e.Cancel = true;
                lblEstado.Text = $"El agrupador '{dto.Clave}' se calcula automáticamente a partir de sus hijos.";
                return;
            }

            if (dgvActividades.Rows[e.RowIndex].DataBoundItem is ActivityGridRowDto dtoEditable)
            {
                var columnName = dgvActividades.Columns[e.ColumnIndex].Name;
                if (EsColumnaUndoActividad(columnName))
                {
                    _undoActividadRowIndex = e.RowIndex;
                    _undoActividadColumnIndex = e.ColumnIndex;
                    _undoActividadColumnName = columnName;
                    _undoActividadOldValue = ObtenerValorUndoActividad(dtoEditable, columnName);
                }
            }
        }

        private void ActualizarEstadoGeneral()
        {
            if (_programaActual == null)
            {
                lblEstado.Text = "No existe un programa de obra generado para este proyecto.";
                return;
            }

            var conceptos = _actividades.Count(a => !a.EsResumen);
            var agrupadores = _actividades.Count(a => a.EsResumen);
            var inicio = _actividades.Where(a => !a.EsResumen && a.FechaInicioProgramada.HasValue)
                .Select(a => a.FechaInicioProgramada!.Value.Date)
                .DefaultIfEmpty()
                .Min();
            var fin = _actividades.Where(a => !a.EsResumen && a.FechaFinProgramada.HasValue)
                .Select(a => a.FechaFinProgramada!.Value.Date)
                .DefaultIfEmpty()
                .Max();

            if (inicio == default || fin == default)
            {
                lblEstado.Text = $"Programa cargado: {agrupadores} agrupadores, {conceptos} conceptos y {_periodos.Count} periodos.";
                return;
            }

            var dias = _calculationService.CalculateBusinessDaysInclusive(_context, _programaActual.ProgramaObraId, inicio, fin);
            lblEstado.Text = $"Programa cargado: {agrupadores} agrupadores, {conceptos} conceptos, {_periodos.Count} periodos. Plazo total: {dias} días hábiles ({inicio:dd/MM/yyyy} → {fin:dd/MM/yyyy}).";
        }

        private void CargarDetalleActividadSeleccionada()
        {
            if (DebeDiferirCargaDetalle())
            {
                _detalleSeleccionDeferred = true;
                return;
            }

            var actividad = GetActividadSeleccionada();
            if (actividad == null)
            {
                _ultimaActividadDetalleId = null;
                _detalleSeleccionDeferred = false;
                dgvDistribucion.DataSource = null;
                _dependenciasEditables = new BindingList<DependenciaEditableRow>();
                dgvDependencias.DataSource = _dependenciasEditables;
                return;
            }

            if (actividad.EsResumen)
            {
                _ultimaActividadDetalleId = actividad.Id;
                _detalleSeleccionDeferred = false;
                dgvDistribucion.DataSource = null;
                _dependenciasEditables = new BindingList<DependenciaEditableRow>();
                dgvDependencias.DataSource = _dependenciasEditables;
                dgvDependencias.AllowUserToAddRows = false;
                dgvDependencias.AllowUserToDeleteRows = false;
                dgvDependencias.ReadOnly = true;
                lblEstado.Text = $"Agrupador '{actividad.Clave}' calculado automáticamente a partir de sus conceptos hijos. Los agrupadores no admiten dependencias directas.";
                return;
            }

            dgvDependencias.AllowUserToAddRows = true;
            dgvDependencias.AllowUserToDeleteRows = true;
            dgvDependencias.ReadOnly = false;
            colDepDescripcion.ReadOnly = true;
            colDepLag.ReadOnly = false;
            colDepClave.ReadOnly = false;
            colDepTipo.ReadOnly = false;

            var distribucion = _context.DistribucionesPeriodo
                .AsNoTracking()
                .Where(d => d.ActividadProgramadaId == actividad.Id)
                .Join(_context.PeriodosPrograma.AsNoTracking(),
                    d => d.PeriodoProgramaId,
                    p => p.Id,
                    (d, p) => new DistribucionRow
                    {
                        PeriodoProgramaId = p.Id,
                        NumeroPeriodo = p.NumeroPeriodo,
                        Periodo = p.Etiqueta,
                        FechaInicio = actividad.FechaInicioProgramada.HasValue && actividad.FechaInicioProgramada.Value.Date > p.FechaInicio.Date
                            ? actividad.FechaInicioProgramada.Value.Date
                            : p.FechaInicio.Date,
                        FechaFin = actividad.FechaFinProgramada.HasValue && actividad.FechaFinProgramada.Value.Date < p.FechaFin.Date
                            ? actividad.FechaFinProgramada.Value.Date
                            : p.FechaFin.Date,
                        Cantidad = d.CantidadProgramada,
                        Porcentaje = d.PorcentajeProgramado,
                        Importe = d.ImporteProgramado
                    })
                .OrderBy(x => x.NumeroPeriodo)
                .ToList();

            dgvDistribucion.DataSource = new BindingList<DistribucionRow>(distribucion);

            var dependencias = _context.DependenciasActividad
                .AsNoTracking()
                .Where(d => d.ActividadDestinoId == actividad.Id)
                .Join(_context.ActividadesProgramadas.AsNoTracking(),
                    d => d.ActividadOrigenId,
                    a => a.Id,
                    (d, a) => new DependenciaEditableRow
                    {
                        ActividadOrigenId = a.Id,
                        Clave = a.Clave,
                        Descripcion = a.Descripcion,
                        TipoDependencia = d.TipoDependencia.ToString(),
                        DesfaseDias = d.DesfaseDias
                    })
                .OrderBy(x => x.Clave)
                .ToList();

            _dependenciasEditables = new BindingList<DependenciaEditableRow>(dependencias);
            dgvDependencias.DataSource = _dependenciasEditables;
            _ultimaActividadDetalleId = actividad.Id;
            _detalleSeleccionDeferred = false;
        }

        private ActivityGridRowDto? GetActividadSeleccionada()
        {
            if (dgvActividades.CurrentRow?.DataBoundItem is ActivityGridRowDto dto)
                return dto;

            if (dgvActividades.SelectedRows.Count > 0 && dgvActividades.SelectedRows[0].DataBoundItem is ActivityGridRowDto dtoSeleccionado)
                return dtoSeleccionado;

            return null;
        }
    }
}
