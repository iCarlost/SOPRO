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
    /// Todas las mutaciones del programa pasan por las rutas atómicas con fábrica y
    /// programa activo esperado; la vista se publica releyendo desde un contexto NUEVO
    /// y de sólo lectura (nunca SaveChanges sobre el contexto compartido).
    /// </summary>
    public partial class FormProgramaObra
    {

        private void btnGenerarPrograma_Click(object sender, EventArgs e)
        {
            EjecutarGenerarPrograma();
        }

        /// <summary>
        /// Ruta real del botón Generar (también seam de pruebas): genera el programa base
        /// con la ruta atómica y publica una lectura fresca. Un no-op confirmado
        /// (<see cref="ProgramSyncResult.SinCambios"/>) no invalida nada.
        /// </summary>
        internal bool EjecutarGenerarPrograma()
        {
            if (!PuedeMutarPrograma("generar el programa"))
                return false;

            try
            {
                UseWaitCursor = true;
                var fechaInicio = _proyecto.FechaInicio == default ? DateTime.Today : _proyecto.FechaInicio.Date;
                var result = _syncService.GenerateProgram(
                    _factory,
                    _context.DatabasePath,
                    _proyecto.Id,
                    _programaActual?.ProgramaObraId,
                    fechaInicio,
                    ObtenerTipoPeriodoSeleccionado());

                if (!ProcesarResultadoPrograma(result, descartarFormato: false))
                    return false;

                return RecargarProgramaDesdeContextoFresco();
            }
            catch (Exception ex)
            {
                Notificador.Error($"Error al generar el programa: {ex.Message}");
                return false;
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

        private void btnRecalcular_Click(object sender, EventArgs e)
        {
            EjecutarRecalculoPrograma();
        }

        /// <summary>
        /// Recalcula el programa con la ruta atómica (recálculo, periodos, distribución y
        /// recálculo final en UN commit). Si aún no hay programa, sincroniza desde el
        /// presupuesto en la misma ruta segura. Devuelve true sólo si se publicó una
        /// lectura fresca (commit con cambios).
        /// </summary>
        internal bool EjecutarRecalculoPrograma()
        {
            if (!PuedeMutarPrograma("recalcular el programa"))
                return false;

            try
            {
                UseWaitCursor = true;
                var state = DataGridViewStateHelper.Capture(dgvActividades);

                var result = _programaActual == null
                    ? _syncService.SyncFromBudget(
                        _factory,
                        _context.DatabasePath,
                        _proyecto.Id,
                        expectedActiveProgramaId: null)
                    : _syncService.RecalculateProgram(
                        _factory,
                        _context.DatabasePath,
                        _proyecto.Id,
                        _programaActual.ProgramaObraId,
                        tipoPeriodo: null);

                if (!ProcesarResultadoPrograma(result, descartarFormato: false))
                    return false;

                return RecargarProgramaDesdeContextoFresco(state);
            }
            catch (Exception ex)
            {
                Notificador.Error($"Error al recalcular el programa: {ex.Message}");
                return false;
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnCalendario_Click(object sender, EventArgs e)
        {
            if (!PuedeMutarPrograma("actualizar el calendario"))
                return;

            if (_programaActual == null)
            {
                Notificador.Informar("Primero genera o carga un programa de obra.");
                return;
            }

            using var frm = new FormCalendarioLaboral(_context, _proyecto, _programaActual.CalendarioLaboralId);
            if (frm.ShowDialog(this) != DialogResult.OK || !frm.CalendarioActualizado)
                return;

            var state = DataGridViewStateHelper.Capture(dgvActividades);
            var expected = _programaActual.ProgramaObraId;

            // Si el programa aún no tenía calendario, se enlaza el activo más reciente
            // DENTRO de la transacción del recálculo (no hay SaveChanges previo suelto).
            var calendarioParaEnlazar = _programaActual.CalendarioLaboralId is null
                ? ObtenerCalendarioActivoId()
                : null;

            var result = _syncService.RecalculateProgram(
                _factory,
                _context.DatabasePath,
                _proyecto.Id,
                expected,
                tipoPeriodo: null,
                calendarioLaboralId: calendarioParaEnlazar);

            if (ProcesarResultadoPrograma(result, descartarFormato: false))
                RecargarProgramaDesdeContextoFresco(state);
        }

        /// <summary>
        /// Id del calendario activo más reciente del proyecto (sólo lectura). Es el que
        /// la vista enlaza al programa cuando este no tenía calendario; el enlace lo
        /// aplica la ruta atómica de recálculo dentro de su transacción.
        /// </summary>
        private int? ObtenerCalendarioActivoId()
            => _context.CalendariosLaborales
                .AsNoTracking()
                .Where(c => c.ProyectoId == _proyecto.Id && c.Activo)
                .OrderByDescending(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefault();

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

        private void cmbTipoPeriodo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cargando || _programaActual == null)
                return;

            var nuevoTipo = ObtenerTipoPeriodoSeleccionado();
            if (_programaActual.TipoPeriodo == nuevoTipo)
                return;

            AplicarTipoPeriodoPrograma(nuevoTipo);
        }

        /// <summary>
        /// Cambia el tipo de período con la ruta atómica: el servicio actualiza
        /// TipoPeriodo/Duración y regenera periodos/distribuciones en un solo commit,
        /// revalidando el programa activo esperado. Un conflicto restaura el combo.
        /// </summary>
        internal bool AplicarTipoPeriodoPrograma(TipoPeriodoPrograma nuevoTipo)
        {
            if (_programaActual == null)
                return false;

            if (!PuedeMutarPrograma("cambiar el tipo de período"))
            {
                RestaurarTipoPeriodoEnCombo();
                return false;
            }

            try
            {
                UseWaitCursor = true;
                var state = DataGridViewStateHelper.Capture(dgvActividades);
                var result = _syncService.RecalculateProgram(
                    _factory,
                    _context.DatabasePath,
                    _proyecto.Id,
                    _programaActual.ProgramaObraId,
                    nuevoTipo);

                if (!ProcesarResultadoPrograma(result, descartarFormato: false))
                {
                    RestaurarTipoPeriodoEnCombo();
                    return false;
                }

                return RecargarProgramaDesdeContextoFresco(state);
            }
            catch (Exception ex)
            {
                Notificador.Error($"Error al cambiar el tipo de periodo: {ex.Message}");
                RestaurarTipoPeriodoEnCombo();
                return false;
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        /// <summary>Revierte el combo al tipo del programa publicado (sin re-disparar).</summary>
        private void RestaurarTipoPeriodoEnCombo()
        {
            if (_programaActual == null)
                return;

            var anterior = _cargando;
            _cargando = true;
            try
            {
                cmbTipoPeriodo.SelectedItem = _programaActual.TipoPeriodo;
            }
            finally
            {
                _cargando = anterior;
            }
        }

        // ── Control de estado ocupado — deshabilita disparadores mientras corre async ──
        private void SetOcupado(bool ocupado, string mensaje = "")
        {
            _ocupado = ocupado;
            UseWaitCursor  = ocupado;
            ActualizarHabilitacionPrograma();
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
