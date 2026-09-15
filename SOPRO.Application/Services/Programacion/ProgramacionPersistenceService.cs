using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionPersistenceService
    {
        private readonly TimeProvider _timeProvider;
        private readonly ProgramacionValidationService _validationService = new();
        private readonly ProgramacionCalculationService _calculationService;
        private readonly ProgramacionDistributionService _distributionService;

        public ProgramacionPersistenceService(TimeProvider? timeProvider = null)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            _calculationService = new ProgramacionCalculationService(_timeProvider);
            _distributionService = new ProgramacionDistributionService(_timeProvider);
        }

        public (bool Ok, string Error, int? ActividadId) SaveActivity(SOPROContext context, ActivityEditDto dto)
        {
            var validation = _validationService.ValidateActivity(context, dto);
            if (!validation.Ok)
                return (false, validation.Error, null);

            using var transaction = context.Database.BeginTransaction();
            ActividadProgramada actividad;
            if (dto.Id.HasValue)
            {
                actividad = context.ActividadesProgramadas.FirstOrDefault(a => a.Id == dto.Id.Value)
                    ?? new ActividadProgramada();
                if (actividad.Id == 0)
                {
                    context.ActividadesProgramadas.Add(actividad);
                }
            }
            else
            {
                actividad = new ActividadProgramada();
                context.ActividadesProgramadas.Add(actividad);
            }

            actividad.ProgramaObraId = dto.ProgramaObraId;
            actividad.ConceptoPresupuestoId = dto.ConceptoPresupuestoId;
            actividad.ActividadPadreId = dto.ActividadPadreId;
            actividad.Clave = dto.Clave?.Trim() ?? string.Empty;
            actividad.Descripcion = dto.Descripcion?.Trim() ?? string.Empty;
            actividad.Unidad = dto.Unidad?.Trim() ?? string.Empty;
            actividad.CantidadTotal = dto.CantidadTotal;
            actividad.PrecioUnitario = dto.PrecioUnitario;
            actividad.FechaInicioProgramada = dto.FechaInicioProgramada;
            actividad.FechaFinProgramada = dto.FechaFinProgramada;
            actividad.DuracionDiasHabiles = dto.DuracionDiasHabiles;
            actividad.RendimientoDiario = dto.RendimientoDiario;
            actividad.FrentesTrabajo = dto.FrentesTrabajo;
            actividad.EsResumen = dto.EsResumen;
            actividad.EsManual = dto.EsManual;
            actividad.Nivel = dto.Nivel;
            actividad.Orden = dto.Orden;
            actividad.MetodoDistribucion = dto.MetodoDistribucion;
            actividad.FechaModificacion = _timeProvider.GetLocalNow().DateTime;

            context.SaveChanges();

            // Recalcular el programa completo para propagar cambios a dependencias directas
            // e indirectas. Esto permite que al mover una fecha o duración se actualice toda la red.
            _calculationService.RecalculateProgram(context, actividad.ProgramaObraId);

            var actividadesPrograma = context.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == actividad.ProgramaObraId && !a.EsResumen)
                .Select(a => new { a.Id, a.MetodoDistribucion })
                .ToList();

            foreach (var act in actividadesPrograma)
            {
                var requiereDistribucionUniforme = act.MetodoDistribucion == MetodoDistribucionActividad.Uniforme
                    || !context.DistribucionesPeriodo.Any(d => d.ActividadProgramadaId == act.Id);

                if (requiereDistribucionUniforme)
                {
                    _distributionService.DistributeUniform(context, act.Id);
                }
            }

            _calculationService.RecalculateProgram(context, actividad.ProgramaObraId);

            transaction.Commit();
            return (true, string.Empty, actividad.Id);
        }

        public (bool Ok, string Error) SaveDependencies(SOPROContext context, int actividadId, List<DependencyEditDto> deps)
        {
            var actividad = context.ActividadesProgramadas.FirstOrDefault(a => a.Id == actividadId);
            if (actividad == null)
                return (false, "La actividad no existe.");

            if (actividad.EsResumen)
                return (false, "Los agrupadores no admiten dependencias directas. Captura dependencias en conceptos.");

            var idsPrograma = context.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == actividad.ProgramaObraId)
                .Select(a => a.Id)
                .ToHashSet();
            if (deps.Any(d => d.ActividadDestinoId != actividadId
                || !idsPrograma.Contains(d.ActividadOrigenId)
                || d.ActividadOrigenId == d.ActividadDestinoId))
                return (false, "Todas las dependencias deben pertenecer al mismo programa y apuntar a la actividad seleccionada.");
            if (deps.GroupBy(d => d.ActividadOrigenId).Any(g => g.Count() > 1))
                return (false, "No se puede capturar más de una dependencia del mismo origen.");

            using var transaction = context.Database.BeginTransaction();
            var actuales = context.DependenciasActividad.Where(d => d.ActividadDestinoId == actividadId).ToList();
            context.DependenciasActividad.RemoveRange(actuales);

            foreach (var dep in deps)
            {
                if (dep.ActividadOrigenId == dep.ActividadDestinoId)
                    continue;

                context.DependenciasActividad.Add(new DependenciaActividad
                {
                    ActividadOrigenId = dep.ActividadOrigenId,
                    ActividadDestinoId = dep.ActividadDestinoId,
                    TipoDependencia = dep.TipoDependencia,
                    DesfaseDias = dep.DesfaseDias
                });
            }

            context.SaveChanges();

            var programaObraId = actividad.ProgramaObraId;
            _calculationService.RecalculateProgram(context, programaObraId);

            foreach (var act in context.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == programaObraId && !a.EsResumen)
                .Select(a => a.Id)
                .ToList())
            {
                _distributionService.DistributeUniform(context, act);
            }

            _calculationService.RecalculateProgram(context, programaObraId);
            transaction.Commit();
            return (true, string.Empty);
        }

        public (bool Ok, string Error, int? CalendarioId) SaveCalendar(SOPROContext context, CalendarEditDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                return (false, "El nombre del calendario es obligatorio.", null);
            if (!dto.Lunes && !dto.Martes && !dto.Miercoles && !dto.Jueves
                && !dto.Viernes && !dto.Sabado && !dto.Domingo
                && !dto.Excepciones.Any(x => x.Tipo == TipoExcepcionCalendario.LaborableEspecial && x.Fecha != default))
                return (false, "El calendario debe tener al menos un día laborable.", null);
            if (dto.HoraFin <= dto.HoraInicio)
                return (false, "La hora fin debe ser posterior a la hora inicio.", null);

            using var transaction = context.Database.BeginTransaction();
            CalendarioLaboral calendario;
            if (dto.Id.HasValue)
            {
                calendario = context.CalendariosLaborales.FirstOrDefault(c => c.Id == dto.Id.Value)
                    ?? new CalendarioLaboral();
                if (calendario.Id == 0)
                    context.CalendariosLaborales.Add(calendario);
            }
            else
            {
                calendario = new CalendarioLaboral();
                context.CalendariosLaborales.Add(calendario);
            }

            calendario.ProyectoId = dto.ProyectoId;
            calendario.Nombre = dto.Nombre;
            calendario.Lunes = dto.Lunes;
            calendario.Martes = dto.Martes;
            calendario.Miercoles = dto.Miercoles;
            calendario.Jueves = dto.Jueves;
            calendario.Viernes = dto.Viernes;
            calendario.Sabado = dto.Sabado;
            calendario.Domingo = dto.Domingo;
            calendario.HoraInicio = dto.HoraInicio;
            calendario.HoraFin = dto.HoraFin;

            context.SaveChanges();

            var actuales = context.ExcepcionesCalendario.Where(x => x.CalendarioLaboralId == calendario.Id).ToList();
            context.ExcepcionesCalendario.RemoveRange(actuales);

            foreach (var ex in dto.Excepciones
                .Where(x => x.Fecha != default)
                .GroupBy(x => x.Fecha.Date)
                .Select(g => g.First())
                .OrderBy(x => x.Fecha))
            {
                context.ExcepcionesCalendario.Add(new ExcepcionCalendario
                {
                    CalendarioLaboralId = calendario.Id,
                    Fecha = ex.Fecha.Date,
                    Descripcion = ex.Descripcion?.Trim() ?? string.Empty,
                    Tipo = ex.Tipo
                });
            }

            context.SaveChanges();
            transaction.Commit();
            return (true, string.Empty, calendario.Id);
        }
    }
}
