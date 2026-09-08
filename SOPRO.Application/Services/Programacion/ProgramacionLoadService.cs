using Microsoft.EntityFrameworkCore;
using Sopro.Calculation.Calendar;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionLoadService
    {
        public List<ProgramSummaryDto> GetPrograms(SOPROContext context, int proyectoId)
        {
            return context.ProgramasObra
                .Where(p => p.ProyectoId == proyectoId)
                .Select(p => new ProgramSummaryDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    FechaInicioPrograma = p.FechaInicioPrograma,
                    FechaFinPrograma = p.FechaFinPrograma,
                    Actividades = p.Actividades.Count()
                })
                .OrderBy(p => p.Nombre)
                .ToList();
        }

        public ProgramLoadDto? LoadProgramById(SOPROContext context, int programaObraId)
        {
            // Recalcular antes de cargar para que importes y acumulados respeten la precisión de pantalla vigente.
            new ProgramacionCalculationService().RecalculateProgram(context, programaObraId);

            var programa = context.ProgramasObra
                .AsNoTracking()
                .Where(p => p.Id == programaObraId)
                .Select(p => new ProgramLoadDto
                {
                    ProgramaObraId = p.Id,
                    ProyectoId = p.ProyectoId,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    FechaInicioPrograma = p.FechaInicioPrograma,
                    FechaFinPrograma = p.FechaFinPrograma,
                    TipoPeriodo = p.TipoPeriodo,
                    CalendarioLaboralId = p.CalendarioLaboralId,
                    Actividades = p.Actividades
                        .OrderBy(a => a.Orden)
                        .Select(a => new ActivityGridRowDto
                        {
                            Id = a.Id,
                            ActividadPadreId = a.ActividadPadreId,
                            ConceptoPresupuestoId = a.ConceptoPresupuestoId,
                            Clave = a.Clave,
                            Descripcion = a.Descripcion,
                            Unidad = a.Unidad,
                            CantidadTotal = a.CantidadTotal,
                            FechaInicioProgramada = a.FechaInicioProgramada,
                            FechaFinProgramada = a.FechaFinProgramada,
                            DuracionDiasHabiles = a.DuracionDiasHabiles,
                            RendimientoDiario = a.RendimientoDiario,
                            FrentesTrabajo = a.FrentesTrabajo,
                            PrecioUnitario = a.PrecioUnitario,
                            ImporteTotal = a.ImporteTotal,
                            RutaCritica = a.RutaCritica,
                            Nivel = a.Nivel,
                            EsResumen = a.EsResumen,
                            Orden = a.Orden
                        }).ToList(),
                    Periodos = p.Periodos
                        .OrderBy(x => x.NumeroPeriodo)
                        .Select(x => new PeriodEditDto
                        {
                            Id = x.Id,
                            NumeroPeriodo = x.NumeroPeriodo,
                            Etiqueta = x.Etiqueta,
                            FechaInicio = x.FechaInicio,
                            FechaFin = x.FechaFin,
                            EsCerrado = x.EsCerrado
                        }).ToList()
                })
                .FirstOrDefault();

            if (programa == null)
                return null;

            var deps = (from d in context.DependenciasActividad.AsNoTracking()
                        join origen in context.ActividadesProgramadas.AsNoTracking() on d.ActividadOrigenId equals origen.Id
                        where origen.ProgramaObraId == programaObraId
                        select new
                        {
                            d.ActividadDestinoId,
                            origen.Clave,
                            d.TipoDependencia,
                            d.DesfaseDias
                        })
                        .ToList()
                        .GroupBy(x => x.ActividadDestinoId)
                        .ToDictionary(
                            g => g.Key,
                            g => string.Join(", ", g.OrderBy(x => x.Clave).Select(x => $"{x.Clave} {x.TipoDependencia}" + (x.DesfaseDias != 0 ? $" (+{x.DesfaseDias})" : string.Empty)))
                        );

            foreach (var actividad in programa.Actividades)
            {
                if (deps.TryGetValue(actividad.Id, out var resumen))
                    actividad.PredecesoraResumen = resumen;
            }

            var calendario = programa.CalendarioLaboralId.HasValue
                ? context.CalendariosLaborales
                    .AsNoTracking()
                    .Include(c => c.Excepciones)
                    .FirstOrDefault(c => c.Id == programa.CalendarioLaboralId.Value)
                : null;

            CalcularResumenesEnMemoria(programa, calendario);
            return programa;
        }


        private static void CalcularResumenesEnMemoria(ProgramLoadDto programa, CalendarioLaboral? calendario)
        {
            if (programa.Actividades == null || programa.Actividades.Count == 0)
                return;

            var ordenadas = programa.Actividades
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .ToList();

            for (int i = ordenadas.Count - 1; i >= 0; i--)
            {
                var resumen = ordenadas[i];
                if (!resumen.EsResumen)
                    continue;

                var descendientes = ObtenerDescendientesDelBloque(ordenadas, i);
                var programables = descendientes.Where(x => !x.EsResumen).ToList();

                if (programables.Count == 0)
                {
                    resumen.FechaInicioProgramada = null;
                    resumen.FechaFinProgramada = null;
                    resumen.DuracionDiasHabiles = 0;
                    resumen.ImporteTotal = 0m;
                    continue;
                }

                var inicios = programables.Where(x => x.FechaInicioProgramada.HasValue).Select(x => x.FechaInicioProgramada!.Value.Date).ToList();
                var fines = programables.Where(x => x.FechaFinProgramada.HasValue).Select(x => x.FechaFinProgramada!.Value.Date).ToList();

                resumen.FechaInicioProgramada = inicios.Count > 0 ? inicios.Min() : null;
                resumen.FechaFinProgramada = fines.Count > 0 ? fines.Max() : null;
                resumen.DuracionDiasHabiles = CalcularDiasHabiles(calendario, resumen.FechaInicioProgramada, resumen.FechaFinProgramada);
                resumen.ImporteTotal = programables.Sum(x => x.ImporteTotal);
            }
        }

        private static List<ActivityGridRowDto> ObtenerDescendientesDelBloque(List<ActivityGridRowDto> ordenadas, int indiceAgrupador)
        {
            var resultado = new List<ActivityGridRowDto>();
            if (indiceAgrupador < 0 || indiceAgrupador >= ordenadas.Count)
                return resultado;

            var agrupador = ordenadas[indiceAgrupador];
            for (int i = indiceAgrupador + 1; i < ordenadas.Count; i++)
            {
                var candidata = ordenadas[i];
                if (candidata.Nivel <= agrupador.Nivel)
                    break;

                resultado.Add(candidata);
            }

            return resultado;
        }

        private static int CalcularDiasHabiles(CalendarioLaboral? calendario, DateTime? inicio, DateTime? fin)
        {
            if (!inicio.HasValue || !fin.HasValue)
                return 0;

            var desde = inicio.Value.Date;
            var hasta = fin.Value.Date;
            if (hasta < desde)
                return 0;

            return WorkingCalendarCalculator.CountWorkingDays(
                WorkingCalendarAdapter.ToWorkingCalendar(calendario), desde, hasta);
        }

        public ProgramLoadDto? LoadProgram(SOPROContext context, int proyectoId)
        {
            var programaId = context.ProgramasObra
                .Where(p => p.ProyectoId == proyectoId && p.Activo)
                .OrderBy(p => p.Id)
                .Select(p => (int?)p.Id)
                .FirstOrDefault();

            return programaId.HasValue ? LoadProgramById(context, programaId.Value) : null;
        }

        public CalendarEditDto LoadCalendar(SOPROContext context, int proyectoId, int? calendarioId = null)
        {
            var query = context.CalendariosLaborales
                .AsNoTracking()
                .Include(c => c.Excepciones)
                .Where(c => c.ProyectoId == proyectoId && c.Activo);

            CalendarioLaboral? calendario = null;
            if (calendarioId.HasValue)
            {
                calendario = query.FirstOrDefault(c => c.Id == calendarioId.Value);
            }

            calendario ??= query.OrderBy(c => c.Id).FirstOrDefault();

            if (calendario == null)
            {
                return new CalendarEditDto
                {
                    ProyectoId = proyectoId,
                    Nombre = "Calendario General",
                    HoraInicio = new TimeSpan(8, 0, 0),
                    HoraFin = new TimeSpan(18, 0, 0)
                };
            }

            return new CalendarEditDto
            {
                Id = calendario.Id,
                ProyectoId = calendario.ProyectoId,
                Nombre = calendario.Nombre,
                Lunes = calendario.Lunes,
                Martes = calendario.Martes,
                Miercoles = calendario.Miercoles,
                Jueves = calendario.Jueves,
                Viernes = calendario.Viernes,
                Sabado = calendario.Sabado,
                Domingo = calendario.Domingo,
                HoraInicio = calendario.HoraInicio,
                HoraFin = calendario.HoraFin,
                Excepciones = calendario.Excepciones
                    .OrderBy(e => e.Fecha)
                    .Select(e => new CalendarExceptionEditDto
                    {
                        Id = e.Id,
                        Fecha = e.Fecha,
                        Descripcion = e.Descripcion,
                        Tipo = e.Tipo
                    }).ToList()
            };
        }
    }
}
