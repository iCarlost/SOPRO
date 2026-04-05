using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionGenerationService
    {
        private readonly ProgramacionCalculationService _calculationService = new();
        private readonly ProgramacionDistributionService _distributionService = new();

        public ProgramGenerationResult GenerateFromBudget(SOPROContext context, int proyectoId, DateTime fechaInicio, TipoPeriodoPrograma tipoPeriodo)
        {
            var proyecto = context.Proyectos.FirstOrDefault(p => p.Id == proyectoId);
            if (proyecto == null)
            {
                return new ProgramGenerationResult { Success = false, Message = "El proyecto no existe." };
            }

            var existente = context.ProgramasObra.FirstOrDefault(p => p.ProyectoId == proyectoId && p.Activo);
            if (existente != null)
            {
                return new ProgramGenerationResult
                {
                    Success = true,
                    Message = "El proyecto ya tiene un programa base generado.",
                    ProgramaObraId = existente.Id,
                    ActividadesGeneradas = context.ActividadesProgramadas.Count(a => a.ProgramaObraId == existente.Id),
                    PeriodosGenerados = context.PeriodosPrograma.Count(p => p.ProgramaObraId == existente.Id)
                };
            }

            var calendario = context.CalendariosLaborales
                .FirstOrDefault(c => c.ProyectoId == proyectoId && c.Activo);

            if (calendario == null)
            {
                calendario = new CalendarioLaboral
                {
                    ProyectoId = proyectoId,
                    Nombre = "Calendario General",
                    Lunes = true,
                    Martes = true,
                    Miercoles = true,
                    Jueves = true,
                    Viernes = true,
                    Sabado = false,
                    Domingo = false
                };
                context.CalendariosLaborales.Add(calendario);
                context.SaveChanges();
            }

            var programa = new ProgramaObra
            {
                ProyectoId = proyectoId,
                Nombre = "Programa Base",
                Descripcion = "Generado a partir del presupuesto",
                FechaInicioPrograma = fechaInicio.Date,
                TipoPeriodo = tipoPeriodo,
                DuracionPeriodoDias = GetDaysByPeriod(tipoPeriodo),
                CalendarioLaboralId = calendario.Id,
                GeneradoDesdePresupuesto = true,
                Activo = true
            };
            context.ProgramasObra.Add(programa);
            context.SaveChanges();

            var conceptos = context.ConceptosPresupuesto
                .AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Orden)
                .ToList();

            var actividadesPorConceptoId = new Dictionary<int, ActividadProgramada>();
            int orden = 1;

            foreach (var concepto in conceptos)
            {
                var actividad = new ActividadProgramada
                {
                    ProgramaObraId = programa.Id,
                    ConceptoPresupuestoId = concepto.Id,
                    Clave = concepto.Clave ?? string.Empty,
                    Descripcion = concepto.Descripcion ?? string.Empty,
                    Unidad = concepto.EsAgrupador ? string.Empty : (concepto.Unidad ?? string.Empty),
                    EsResumen = concepto.EsAgrupador,
                    EsManual = false,
                    Nivel = Math.Max(0, concepto.Nivel),
                    Orden = orden++,
                    CantidadTotal = concepto.EsAgrupador ? 0m : concepto.Cantidad,
                    PrecioUnitario = concepto.EsAgrupador ? 0m : concepto.PrecioUnitario,
                    ImporteTotal = concepto.EsAgrupador ? 0m : concepto.ImporteTotal,
                    FechaInicioProgramada = concepto.EsAgrupador ? null : fechaInicio.Date,
                    DuracionDiasHabiles = concepto.EsAgrupador ? 0 : EstimateDuration(concepto),
                    MetodoDistribucion = MetodoDistribucionActividad.Uniforme
                };

                context.ActividadesProgramadas.Add(actividad);
                actividadesPorConceptoId[concepto.Id] = actividad;
            }

            context.SaveChanges();

            foreach (var concepto in conceptos)
            {
                if (!actividadesPorConceptoId.TryGetValue(concepto.Id, out var actividad))
                    continue;

                var padreConceptoId = ResolveParentConceptId(conceptos, concepto);
                if (padreConceptoId.HasValue && actividadesPorConceptoId.TryGetValue(padreConceptoId.Value, out var padre))
                    actividad.ActividadPadreId = padre.Id;
                else
                    actividad.ActividadPadreId = null;
            }

            context.SaveChanges();

            _calculationService.RecalculateProgram(context, programa.Id);

            var periodosGenerados = RegeneratePeriodsFromProgramRange(context, programa.Id, tipoPeriodo);

            var actividadIds = context.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == programa.Id && !a.EsResumen)
                .Select(a => a.Id)
                .ToList();

            foreach (var actividadId in actividadIds)
            {
                _distributionService.DistributeUniform(context, actividadId);
            }

            _calculationService.RecalculateProgram(context, programa.Id);

            return new ProgramGenerationResult
            {
                Success = true,
                Message = "Programa de obra base generado correctamente con estructura jerárquica.",
                ProgramaObraId = programa.Id,
                ActividadesGeneradas = conceptos.Count,
                PeriodosGenerados = periodosGenerados
            };
        }

        public int GenerateDefaultPeriods(SOPROContext context, int programaObraId, DateTime fechaInicio, TipoPeriodoPrograma tipoPeriodo, int numeroPeriodos)
        {
            var fin = fechaInicio.Date;
            for (int i = 1; i < Math.Max(1, numeroPeriodos); i++)
            {
                fin = GetNextPeriodStart(fin, tipoPeriodo).AddDays(-1);
            }

            return ReplacePeriods(context, programaObraId, fechaInicio.Date, fin.Date, tipoPeriodo);
        }

        public int RegeneratePeriodsFromProgramRange(SOPROContext context, int programaObraId, TipoPeriodoPrograma tipoPeriodo)
        {
            var programa = context.ProgramasObra
                .Include(p => p.Actividades)
                .FirstOrDefault(p => p.Id == programaObraId);

            if (programa == null)
                return 0;

            var hojas = programa.Actividades
                .Where(a => !a.EsResumen && a.FechaInicioProgramada.HasValue && a.FechaFinProgramada.HasValue)
                .ToList();

            var inicio = hojas.Count > 0
                ? hojas.Min(a => a.FechaInicioProgramada!.Value.Date)
                : programa.FechaInicioPrograma.Date;

            var fin = hojas.Count > 0
                ? hojas.Max(a => a.FechaFinProgramada!.Value.Date)
                : inicio;

            programa.FechaInicioPrograma = inicio;
            programa.FechaFinPrograma = fin;
            programa.TipoPeriodo = tipoPeriodo;
            programa.DuracionPeriodoDias = GetDaysByPeriod(tipoPeriodo);
            programa.FechaModificacion = DateTime.Now;
            context.SaveChanges();

            return ReplacePeriods(context, programaObraId, inicio, fin, tipoPeriodo);
        }

        private int ReplacePeriods(SOPROContext context, int programaObraId, DateTime fechaInicio, DateTime fechaFin, TipoPeriodoPrograma tipoPeriodo)
        {
            var actividadIds = context.ActividadesProgramadas
                .Where(a => a.ProgramaObraId == programaObraId)
                .Select(a => a.Id)
                .ToList();

            if (actividadIds.Count > 0)
            {
                var distribuciones = context.DistribucionesPeriodo
                    .Where(d => actividadIds.Contains(d.ActividadProgramadaId))
                    .ToList();

                if (distribuciones.Any())
                {
                    context.DistribucionesPeriodo.RemoveRange(distribuciones);
                    context.SaveChanges();
                }
            }

            var existentes = context.PeriodosPrograma.Where(p => p.ProgramaObraId == programaObraId).ToList();
            if (existentes.Any())
            {
                context.PeriodosPrograma.RemoveRange(existentes);
                context.SaveChanges();
            }

            var periodos = GeneratePeriodsFromDateRange(programaObraId, fechaInicio, fechaFin, tipoPeriodo);
            context.PeriodosPrograma.AddRange(periodos);
            context.SaveChanges();
            return periodos.Count;
        }

        public List<PeriodoPrograma> GeneratePeriodsFromDateRange(int programaObraId, DateTime inicio, DateTime fin, TipoPeriodoPrograma tipoPeriodo)
        {
            var periodos = new List<PeriodoPrograma>();
            var actual = ProgramacionPeriodHelper.AlignStart(inicio, tipoPeriodo);
            fin = ProgramacionPeriodHelper.AlignEnd(fin, tipoPeriodo);
            var numero = 1;

            while (actual <= fin.Date)
            {
                var finPeriodo = ProgramacionPeriodHelper.GetPeriodEnd(actual, tipoPeriodo);
                if (finPeriodo > fin.Date)
                    finPeriodo = fin.Date;

                periodos.Add(new PeriodoPrograma
                {
                    ProgramaObraId = programaObraId,
                    NumeroPeriodo = numero,
                    Etiqueta = ProgramacionPeriodHelper.BuildLabel(numero, actual, finPeriodo, tipoPeriodo),
                    FechaInicio = actual,
                    FechaFin = finPeriodo,
                    EsCerrado = false
                });

                numero++;
                actual = ProgramacionPeriodHelper.GetNextPeriodStart(actual, tipoPeriodo);
            }

            if (periodos.Count == 0)
            {
                periodos.Add(new PeriodoPrograma
                {
                    ProgramaObraId = programaObraId,
                    NumeroPeriodo = 1,
                    Etiqueta = ProgramacionPeriodHelper.BuildLabel(1, actual, fin.Date, tipoPeriodo),
                    FechaInicio = inicio.Date,
                    FechaFin = fin.Date,
                    EsCerrado = false
                });
            }

            return periodos;
        }

        private static int? ResolveParentConceptId(List<ConceptoPresupuesto> conceptos, ConceptoPresupuesto concepto)
        {
            if (concepto.PadreId.HasValue && conceptos.Any(c => c.Id == concepto.PadreId.Value))
                return concepto.PadreId.Value;

            var ordenados = conceptos.OrderBy(c => c.Orden).ToList();
            var index = ordenados.FindIndex(c => c.Id == concepto.Id);
            if (index <= 0)
                return null;

            for (int i = index - 1; i >= 0; i--)
            {
                var candidato = ordenados[i];
                if (candidato.Nivel < concepto.Nivel)
                    return candidato.Id;
            }

            return null;
        }

        private static int EstimateDuration(ConceptoPresupuesto concepto)
        {
            if (concepto.Cantidad <= 0)
                return 0;
            if (concepto.Cantidad <= 1)
                return 1;
            if (concepto.Cantidad <= 10)
                return 3;
            if (concepto.Cantidad <= 100)
                return 5;
            return 10;
        }

        private static int GetDaysByPeriod(TipoPeriodoPrograma tipoPeriodo)
            => tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => 1,
                TipoPeriodoPrograma.Semana => 7,
                TipoPeriodoPrograma.Quincena => 15,
                TipoPeriodoPrograma.Mes => 30,
                _ => 7
            };

        private static DateTime GetPeriodEnd(DateTime start, TipoPeriodoPrograma tipoPeriodo)
            => tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => start.Date,
                TipoPeriodoPrograma.Semana => start.Date.AddDays(6),
                TipoPeriodoPrograma.Quincena => start.Day <= 15
                    ? new DateTime(start.Year, start.Month, 15)
                    : new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
                TipoPeriodoPrograma.Mes => new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
                _ => start.Date.AddDays(6)
            };

        private static DateTime GetNextPeriodStart(DateTime currentStart, TipoPeriodoPrograma tipoPeriodo)
            => tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => currentStart.Date.AddDays(1),
                TipoPeriodoPrograma.Semana => currentStart.Date.AddDays(7),
                TipoPeriodoPrograma.Quincena => currentStart.Day <= 15
                    ? new DateTime(currentStart.Year, currentStart.Month, 16)
                    : new DateTime(currentStart.Year, currentStart.Month, 1).AddMonths(1),
                TipoPeriodoPrograma.Mes => new DateTime(currentStart.Year, currentStart.Month, 1).AddMonths(1),
                _ => currentStart.Date.AddDays(7)
            };

        private static string BuildLabel(int numero, DateTime inicio, DateTime fin, TipoPeriodoPrograma tipoPeriodo)
            => tipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => $"Día {numero:00} - {inicio:dd/MM/yyyy}",
                TipoPeriodoPrograma.Semana => $"Semana {numero:00} - {inicio:dd/MM} a {fin:dd/MM}",
                TipoPeriodoPrograma.Quincena => $"Quincena {numero:00} - {inicio:dd/MM} a {fin:dd/MM}",
                TipoPeriodoPrograma.Mes => $"{inicio:MMMM yyyy}",
                _ => $"Periodo {numero:00}"
            };
    }
}
