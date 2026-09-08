using System;
using System.Collections.Generic;
using System.Linq;
using Sopro.Calculation.Calendar;
using Sopro.Calculation.Scheduling;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services.Programacion
{
    /// <summary>Bridges legacy scheduling entities to the pure activity-network contract.</summary>
    internal static class ActivityNetworkAdapter
    {
        /// <summary>
        /// Creates an immutable <see cref="ActivityNetworkInput"/> from the non-summary
        /// activities of a program. Summary rows are excluded from the network and
        /// dependencies referencing excluded activities are ignored, matching the
        /// legacy schedule that skipped unknown predecessors.
        /// </summary>
        public static ActivityNetworkInput ToNetworkInput(
            DateTime programStartDate,
            IEnumerable<ActividadProgramada> actividadesNoResumen,
            WorkingCalendar? calendar)
        {
            ArgumentNullException.ThrowIfNull(actividadesNoResumen);

            var actividades = actividadesNoResumen
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .ToList();
            var ids = actividades.Select(a => a.Id).ToHashSet();

            var inputs = actividades
                .Select(a => new ActivityNetworkActivityInput(
                    a.Id,
                    a.Orden,
                    Math.Max(1, a.DuracionDiasHabiles),
                    a.FechaInicioProgramada.HasValue
                        ? CalendarioCache.SanitizarFecha(a.FechaInicioProgramada.Value.Date)
                        : (DateTime?)null))
                .ToList();

            var dependencies = actividades
                .SelectMany(a => a.Predecesoras ?? Enumerable.Empty<DependenciaActividad>())
                .Where(d => ids.Contains(d.ActividadOrigenId) && ids.Contains(d.ActividadDestinoId))
                .Select(d => new ActivityNetworkDependencyInput(
                    d.ActividadOrigenId,
                    d.ActividadDestinoId,
                    MapType(d.TipoDependencia),
                    d.DesfaseDias))
                .ToList();

            return new ActivityNetworkInput(programStartDate, inputs, dependencies, calendar);
        }

        private static ActivityDependencyType MapType(TipoDependenciaActividad tipo) => tipo switch
        {
            TipoDependenciaActividad.FS => ActivityDependencyType.FinishToStart,
            TipoDependenciaActividad.SS => ActivityDependencyType.StartToStart,
            TipoDependenciaActividad.FF => ActivityDependencyType.FinishToFinish,
            TipoDependenciaActividad.SF => ActivityDependencyType.StartToFinish,
            _ => throw new ArgumentOutOfRangeException(
                nameof(tipo), tipo, "Dependency type is not supported.")
        };
    }
}