using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionPersistenceService
    {
        private readonly ProgramacionValidationService _validationService = new();
        private readonly ProgramacionCalculationService _calculationService = new();
        private readonly ProgramacionDistributionService _distributionService = new();

        public (bool Ok, string Error, int? ActividadId) SaveActivity(SOPROContext context, ActivityEditDto dto)
        {
            var validation = _validationService.ValidateActivity(context, dto);
            if (!validation.Ok)
                return (false, validation.Error, null);

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
            actividad.FechaModificacion = DateTime.Now;

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

            return (true, string.Empty, actividad.Id);
        }

        public (bool Ok, string Error) SaveDependencies(SOPROContext context, int actividadId, List<DependencyEditDto> deps)
        {
            var actividad = context.ActividadesProgramadas.FirstOrDefault(a => a.Id == actividadId);
            if (actividad == null)
                return (false, "La actividad no existe.");

            if (actividad.EsResumen)
                return (false, "Los agrupadores no admiten dependencias directas. Captura dependencias en conceptos.");

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
            return (true, string.Empty);
        }

        public (bool Ok, string Error, int? CalendarioId) SaveCalendar(SOPROContext context, CalendarEditDto dto)
        {
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
            return (true, string.Empty, calendario.Id);
        }
    }
}
