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
    /// Acciones principales: generar, recargar, recalcular, calendario y tipo de período.
    /// </summary>
    public partial class FormProgramaObra
    {

        private void btnGenerarPrograma_Click(object sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                var fechaInicio = _proyecto.FechaInicio == default ? DateTime.Today : _proyecto.FechaInicio.Date;
                var result = _generationService.GenerateFromBudget(_context, _proyecto.Id, fechaInicio, ObtenerTipoPeriodoSeleccionado());
                lblEstado.Text = result.Message;
                CargarPrograma();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnRecargar_Click(object sender, EventArgs e)
        {
            CargarPrograma(DataGridViewStateHelper.Capture(dgvActividades));
        }

        private async void btnRecalcular_Click(object sender, EventArgs e)
        {
            if (_programaActual == null)
            {
                try
                {
                    SetOcupado(true, "Sincronizando programa...");
                    var dbPath = _context.DatabasePath;
                    var proyectoIdSync = _proyecto.Id;
                    var resultSinPrograma = await Task.Run(() =>
                    {
                        using var ctx = _factory.Create(dbPath);
                        return _syncService.SyncFromBudget(ctx, proyectoIdSync);
                    });
                    lblEstado.Text = resultSinPrograma.Message;
                    CargarPrograma();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al sincronizar el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetOcupado(false);
                }
                return;
            }

            try
            {
                var state = DataGridViewStateHelper.Capture(dgvActividades);
                SetOcupado(true, "Recalculando programa...");
                var programaId = _programaActual.ProgramaObraId;
                var tipoPeriodo = ObtenerTipoPeriodoSeleccionado();
                var dbPathRecalc = _context.DatabasePath;
                await Task.Run(() =>
                {
                    using var ctx = _factory.Create(dbPathRecalc);
                    _calculationService.RecalculateProgram(ctx, programaId);
                    RegenerarPeriodosYDistribuciones(ctx, programaId, tipoPeriodo);
                });
                CargarPrograma(state);
                lblEstado.Text = "Programa recalculado correctamente.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al recalcular el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetOcupado(false);
            }
        }

        private async void btnCalendario_Click(object sender, EventArgs e)
        {
            if (_programaActual == null)
            {
                MessageBox.Show("Primero genera o carga un programa de obra.", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var frm = new FormCalendarioLaboral(_context, _proyecto, _programaActual.CalendarioLaboralId);
            if (frm.ShowDialog(this) == DialogResult.OK && frm.CalendarioActualizado)
            {
                if (!_programaActual.CalendarioLaboralId.HasValue)
                {
                    var programa = _context.ProgramasObra.FirstOrDefault(p => p.Id == _programaActual.ProgramaObraId);
                    var calendario = _context.CalendariosLaborales
                        .Where(c => c.ProyectoId == _proyecto.Id && c.Activo)
                        .OrderByDescending(c => c.Id)
                        .FirstOrDefault();
                    if (programa != null && calendario != null)
                    {
                        programa.CalendarioLaboralId = calendario.Id;
                        _context.SaveChanges();
                    }
                }

                var state = DataGridViewStateHelper.Capture(dgvActividades);
                var programaId2 = _programaActual.ProgramaObraId;
                var tipoPeriodo2 = ObtenerTipoPeriodoSeleccionado();
                var dbPathCalendario = _context.DatabasePath;
                SetOcupado(true, "Actualizando calendario...");
                try
                {
                    await Task.Run(() =>
                    {
                        using var ctx = _factory.Create(dbPathCalendario);
                        _calculationService.RecalculateProgram(ctx, programaId2);
                        RegenerarPeriodosYDistribuciones(ctx, programaId2, tipoPeriodo2);
                    });
                    CargarPrograma(state);
                    lblEstado.Text = "Calendario laboral actualizado.";
                }
                catch (Exception ex2)
                {
                    MessageBox.Show($"Error al actualizar el calendario: {ex2.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetOcupado(false);
                }
            }
        }

        private TipoPeriodoPrograma ObtenerTipoPeriodoSeleccionado()
        {
            return cmbTipoPeriodo.SelectedItem is TipoPeriodoPrograma tipo ? tipo : TipoPeriodoPrograma.Semana;
        }

        private void cmbVistaCurva_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando)
                return;

            AplicarVistaCurvaS();
            ActualizarDisponibilidadPieGantt();
            ActualizarGantt();
            SolicitarRedibujoCurvaS();
            GuardarLayoutPersistido();
        }

        private void cmbPieGantt_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando)
                return;

            ActualizarDisponibilidadPieGantt();
            SolicitarActualizacionVisualPrograma(true);
            GuardarLayoutPersistido();
        }

        private void cmbEtiquetaSegmentoGantt_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando)
                return;

            ActualizarDisponibilidadPieGantt();
            SolicitarActualizacionVisualPrograma(true);
            GuardarLayoutPersistido();
        }

        private async void cmbTipoPeriodo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando || _programaActual == null)
                return;

            try
            {
                var nuevoTipo = ObtenerTipoPeriodoSeleccionado();
                if (_programaActual.TipoPeriodo == nuevoTipo)
                    return;

                var state = DataGridViewStateHelper.Capture(dgvActividades);
                var programa = _context.ProgramasObra.FirstOrDefault(p => p.Id == _programaActual.ProgramaObraId);
                if (programa == null)
                    return;

                programa.TipoPeriodo = nuevoTipo;
                programa.DuracionPeriodoDias = nuevoTipo switch
                {
                    TipoPeriodoPrograma.Dia => 1,
                    TipoPeriodoPrograma.Semana => 7,
                    TipoPeriodoPrograma.Quincena => 15,
                    TipoPeriodoPrograma.Mes => 30,
                    _ => 7
                };
                programa.FechaModificacion = DateTime.Now;
                _context.SaveChanges();

                var programaId3 = programa.Id;
                var dbPathTipoPeriodo = _context.DatabasePath;
                SetOcupado(true, $"Regenerando periodos en modo {nuevoTipo}...");
                await Task.Run(() =>
                {
                    using var ctx = _factory.Create(dbPathTipoPeriodo);
                    _calculationService.RecalculateProgram(ctx, programaId3);
                    RegenerarPeriodosYDistribuciones(ctx, programaId3, nuevoTipo);
                });
                CargarPrograma(state);
                lblEstado.Text = $"Periodos regenerados en modo {nuevoTipo}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cambiar el tipo de periodo: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetOcupado(false);
            }
        }

        // ── Control de estado ocupado — deshabilita disparadores mientras corre async ──
        private void SetOcupado(bool ocupado, string mensaje = "")
        {
            UseWaitCursor  = ocupado;
            // Deshabilitar controles que disparan operaciones pesadas
            btnRecalcular.Enabled   = !ocupado;
            cmbTipoPeriodo.Enabled      = !ocupado;
            btnCalendario.Enabled       = !ocupado;
            btnSincronizar.Enabled      = !ocupado;
            btnReconstruir.Enabled      = !ocupado;
            if (!string.IsNullOrEmpty(mensaje))
                lblEstado.Text = mensaje;
        }

        private void RegenerarPeriodosYDistribuciones(SOPROContext ctx, int programaObraId, TipoPeriodoPrograma tipoPeriodo)
        {
            _generationService.RegeneratePeriodsFromProgramRange(ctx, programaObraId, tipoPeriodo);

            // Batch: distribuye todas las actividades hoja en una sola transacción
            // en lugar de N SaveChanges (uno por actividad).
            _distributionService.DistributeUniformBatch(ctx, programaObraId);

            _calculationService.RecalculateProgram(ctx, programaObraId);
        }
    }
}
