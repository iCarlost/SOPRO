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
    /// Edición de actividades: validación de celdas, normalización y recarga diferida.
    /// </summary>
    public partial class FormProgramaObra
    {

        private void dgvActividades_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_cargando || _programaActual == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var columnName = dgvActividades.Columns[e.ColumnIndex].Name;
            if (columnName is not "colFechaInicio" and not "colFechaFin")
                return;

            var row = dgvActividades.Rows[e.RowIndex];
            if (row.DataBoundItem is not ActivityGridRowDto dto || dto.EsResumen)
                return;

            var textoCapturado = e.FormattedValue?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(textoCapturado))
                return;

            if (!DateTime.TryParse(textoCapturado, out var fechaCapturada))
                return;

            var candidata = fechaCapturada.Date;
            var original = columnName == "colFechaInicio"
                ? dto.FechaInicioProgramada?.Date
                : dto.FechaFinProgramada?.Date;

            if (original.HasValue && original.Value == candidata)
                return;

            var previewRow = new ActivityGridRowDto
            {
                Id = dto.Id,
                ActividadPadreId = dto.ActividadPadreId,
                ConceptoPresupuestoId = dto.ConceptoPresupuestoId,
                Clave = dto.Clave,
                Descripcion = dto.Descripcion,
                Unidad = dto.Unidad,
                CantidadTotal = dto.CantidadTotal,
                PrecioUnitario = dto.PrecioUnitario,
                ImporteTotal = dto.ImporteTotal,
                FechaInicioProgramada = columnName == "colFechaInicio" ? candidata : dto.FechaInicioProgramada,
                FechaFinProgramada = columnName == "colFechaFin" ? candidata : dto.FechaFinProgramada,
                DuracionDiasHabiles = dto.DuracionDiasHabiles,
                RendimientoDiario = dto.RendimientoDiario,
                FrentesTrabajo = dto.FrentesTrabajo,
                RutaCritica = dto.RutaCritica,
                PredecesoraResumen = dto.PredecesoraResumen,
                EsResumen = dto.EsResumen,
                Nivel = dto.Nivel,
                Orden = dto.Orden
            };

            NormalizarActividadEditada(previewRow, columnName);

            var preview = new ActivityEditDto
            {
                Id = previewRow.Id,
                ProgramaObraId = _programaActual.ProgramaObraId,
                ActividadPadreId = previewRow.ActividadPadreId,
                ConceptoPresupuestoId = previewRow.ConceptoPresupuestoId,
                Clave = previewRow.Clave,
                Descripcion = previewRow.Descripcion,
                Unidad = previewRow.Unidad,
                CantidadTotal = previewRow.CantidadTotal,
                PrecioUnitario = previewRow.PrecioUnitario,
                FechaInicioProgramada = previewRow.FechaInicioProgramada,
                FechaFinProgramada = previewRow.FechaFinProgramada,
                DuracionDiasHabiles = previewRow.DuracionDiasHabiles,
                RendimientoDiario = previewRow.RendimientoDiario,
                FrentesTrabajo = previewRow.FrentesTrabajo,
                EsResumen = previewRow.EsResumen,
                EsManual = previewRow.ConceptoPresupuestoId == null,
                Nivel = previewRow.Nivel,
                Orden = previewRow.Orden
            };

            var validation = _validationService.ValidateActivity(_context, preview);
            if (validation.Ok)
                return;

            e.Cancel = true;
            dgvActividades.CancelEdit();
            MessageBox.Show(validation.Error, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            lblEstado.Text = validation.Error;
        }

        private async void dgvActividades_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_cargando || e.RowIndex < 0 || _programaActual == null)
                return;

            var row = dgvActividades.Rows[e.RowIndex];
            if (row.DataBoundItem is not ActivityGridRowDto dto)
                return;

            var gridState = DataGridViewStateHelper.Capture(dgvActividades);

            try
            {
                _cargando = true;
                NormalizarActividadEditada(dto, dgvActividades.Columns[e.ColumnIndex].Name);

                var saveDto = new ActivityEditDto
                {
                    Id = dto.Id,
                    ProgramaObraId = _programaActual.ProgramaObraId,
                    ActividadPadreId = dto.ActividadPadreId,
                    ConceptoPresupuestoId = dto.ConceptoPresupuestoId,
                    Clave = dto.Clave,
                    Descripcion = dto.Descripcion,
                    Unidad = dto.Unidad,
                    CantidadTotal = dto.CantidadTotal,
                    PrecioUnitario = dto.PrecioUnitario,
                    FechaInicioProgramada = dto.FechaInicioProgramada,
                    FechaFinProgramada = dto.FechaFinProgramada,
                    DuracionDiasHabiles = dto.DuracionDiasHabiles,
                    RendimientoDiario = dto.RendimientoDiario,
                    FrentesTrabajo = dto.FrentesTrabajo,
                    EsResumen = dto.EsResumen,
                    EsManual = dto.ConceptoPresupuestoId == null,
                    Nivel = dto.Nivel,
                    Orden = dto.Orden
                };

                var result = _persistenceService.SaveActivity(_context, saveDto);
                if (!result.Ok)
                {
                    MessageBox.Show(result.Error, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RecargarProgramaDiferido(gridState);
                    return;
                }

                // Recalcular solo esta actividad y redistribuirla — no todo el programa
                var actividadId  = dto.Id;
                var programaId   = _programaActual.ProgramaObraId;
                var tipoPeriodo  = ObtenerTipoPeriodoSeleccionado();
                var dbPathAct = _context.DatabasePath;
                SetOcupado(true, $"Actualizando '{dto.Clave}'...");

                await Task.Run(() =>
                {
                    using var ctx = _factory.Create(dbPathAct);
                    // 1. Recalcular fechas de esta actividad
                    _calculationService.RecalculateActivity(ctx, actividadId);
                    // 2. Redistribuir solo esta actividad
                    _distributionService.DistributeUniform(ctx, actividadId);
                    // 3. Recalcular agrupadores y ruta crítica del programa completo
                    _calculationService.RecalculateProgram(ctx, programaId);
                    // 4. Regenerar periodos si las fechas del programa cambiaron
                    RegenerarPeriodosYDistribuciones(ctx, programaId, tipoPeriodo);
                });

                if (!_isUndoRedo
                    && e.RowIndex == _undoActividadRowIndex
                    && e.ColumnIndex == _undoActividadColumnIndex
                    && EsColumnaUndoActividad(_undoActividadColumnName))
                {
                    var oldValue = _undoActividadOldValue;
                    var newValue = ObtenerValorUndoActividad(dto, _undoActividadColumnName);
                    if (!SonValoresUndoIguales(oldValue, newValue))
                    {
                        int rowIndex = e.RowIndex;
                        string columnName = _undoActividadColumnName;
                        string descripcion = $"Editar {columnName} en programa";
                        _undoManager.Push(new DelegateUndoableAction(
                            descripcion,
                            () => AplicarUndoRedoActividadCelda(rowIndex, columnName, oldValue),
                            () => AplicarUndoRedoActividadCelda(rowIndex, columnName, newValue)));
                    }
                }

                lblEstado.Text = $"Actividad '{dto.Clave}' actualizada.";
                RecargarProgramaDiferido(gridState);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo guardar la actividad: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RecargarProgramaDiferido(gridState);
            }
            finally
            {
                _undoActividadRowIndex = -1;
                _undoActividadColumnIndex = -1;
                _undoActividadColumnName = string.Empty;
                _undoActividadOldValue = null;
                _cargando = false;
                SetOcupado(false);
            }
        }


        private void RecargarProgramaDiferido(DataGridViewStateSnapshot? stateToRestore = null)
        {
            if (!IsHandleCreated || IsDisposed)
                return;

            _pendingGridStateRestore = stateToRestore;

            if (_programaReloadPending)
                return;

            _programaReloadPending = true;
            BeginInvoke(new Action(() =>
            {
                _programaReloadPending = false;
                if (IsDisposed)
                    return;

                CargarPrograma(_pendingGridStateRestore);
            }));
        }

        private void NormalizarActividadEditada(ActivityGridRowDto dto, string columnName)
        {
            dto.FrentesTrabajo = Math.Max(1, dto.FrentesTrabajo);
            dto.CantidadTotal = Math.Max(0, dto.CantidadTotal);
            dto.RendimientoDiario = Math.Max(0, dto.RendimientoDiario);
            dto.DuracionDiasHabiles = Math.Max(0, dto.DuracionDiasHabiles);

            var frentes = Math.Max(1, dto.FrentesTrabajo);
            var tieneCantidad = dto.CantidadTotal > 0;
            var tieneDuracion = dto.DuracionDiasHabiles > 0;
            var tieneRendimiento = dto.RendimientoDiario > 0;

            if ((columnName == "colRendimientoDiario" || columnName == "colFrentes") && tieneCantidad && tieneRendimiento)
            {
                var divisor = dto.RendimientoDiario * frentes;
                if (divisor > 0)
                    dto.DuracionDiasHabiles = (int)Math.Ceiling(dto.CantidadTotal / divisor);
            }
            else if ((columnName == "colDuracionDias" || columnName == "colFechaInicio" || columnName == "colFechaFin") && tieneCantidad && tieneDuracion)
            {
                dto.RendimientoDiario = frentes <= 0
                    ? dto.RendimientoDiario
                    : Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
            }
            else if (columnName == "colCantidad")
            {
                if (tieneDuracion)
                {
                    dto.RendimientoDiario = Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
                }
                else if (tieneRendimiento)
                {
                    var divisor = dto.RendimientoDiario * frentes;
                    if (divisor > 0)
                        dto.DuracionDiasHabiles = (int)Math.Ceiling(dto.CantidadTotal / divisor);
                }
            }

            if (columnName == "colDuracionDias" && dto.FechaInicioProgramada.HasValue && _programaActual != null)
            {
                dto.FechaFinProgramada = _calculationService.CalculateFinishDate(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.DuracionDiasHabiles);
            }
            else if (columnName == "colFechaInicio" && dto.FechaInicioProgramada.HasValue && _programaActual != null)
            {
                if (dto.DuracionDiasHabiles > 0)
                {
                    dto.FechaFinProgramada = _calculationService.CalculateFinishDate(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.DuracionDiasHabiles);
                }
                else if (dto.FechaFinProgramada.HasValue)
                {
                    if (dto.FechaFinProgramada.Value.Date < dto.FechaInicioProgramada.Value.Date)
                        dto.FechaFinProgramada = dto.FechaInicioProgramada;

                    dto.DuracionDiasHabiles = _calculationService.CalculateBusinessDaysInclusive(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.FechaFinProgramada);
                    if (dto.DuracionDiasHabiles > 0 && dto.CantidadTotal > 0)
                    {
                        dto.RendimientoDiario = Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
                    }
                }
                else
                {
                    dto.FechaFinProgramada = dto.FechaInicioProgramada;
                    dto.DuracionDiasHabiles = Math.Max(1, dto.DuracionDiasHabiles);
                    if (dto.CantidadTotal > 0)
                    {
                        dto.RendimientoDiario = Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
                    }
                }
            }
            else if (columnName == "colFechaFin" && dto.FechaFinProgramada.HasValue && _programaActual != null)
            {
                var tienePredecesoras = !string.IsNullOrWhiteSpace(dto.PredecesoraResumen);

                if (!tienePredecesoras && dto.DuracionDiasHabiles > 0)
                {
                    dto.FechaInicioProgramada = _calculationService.CalculateStartDate(_context, _programaActual.ProgramaObraId, dto.FechaFinProgramada, dto.DuracionDiasHabiles);
                }
                else if (dto.FechaInicioProgramada.HasValue && dto.FechaFinProgramada.Value.Date < dto.FechaInicioProgramada.Value.Date)
                {
                    dto.FechaFinProgramada = dto.FechaInicioProgramada;
                }

                if (dto.FechaInicioProgramada.HasValue)
                {
                    dto.DuracionDiasHabiles = _calculationService.CalculateBusinessDaysInclusive(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.FechaFinProgramada);
                    if (dto.DuracionDiasHabiles > 0 && dto.CantidadTotal > 0)
                    {
                        dto.RendimientoDiario = Math.Round(dto.CantidadTotal / (dto.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
                    }
                }
            }
            else if ((columnName == "colRendimientoDiario" || columnName == "colFrentes" || columnName == "colCantidad") && dto.FechaInicioProgramada.HasValue && _programaActual != null)
            {
                dto.FechaFinProgramada = _calculationService.CalculateFinishDate(_context, _programaActual.ProgramaObraId, dto.FechaInicioProgramada, dto.DuracionDiasHabiles);
            }

            // Auditoría: _proyecto es no-nullable por constructor (línea 123 usa _proyecto.Id);
            // el fallback previo a 2 decimales era inalcanzable y rompía precisión de pantalla.
            dto.ImporteTotal = new MotorCalculoSopro(_proyecto).Multiplicar(dto.CantidadTotal, dto.PrecioUnitario);
        }
    }
}
