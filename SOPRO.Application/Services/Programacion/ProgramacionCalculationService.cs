using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services.Programacion;
using Sopro.Calculation;
using Sopro.Calculation.Calendar;
using Sopro.Calculation.Scheduling;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionCalculationService
    {
        private const int MaxDuracionDiasHabiles = 36500;
        private readonly TimeProvider _timeProvider;

        public ProgramacionCalculationService(TimeProvider? timeProvider = null)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
        }
        public void RecalculateProgram(SOPROContext context, int programaObraId)
        {
            var programa = context.ProgramasObra
                .Include(p => p.CalendarioLaboral)
                    .ThenInclude(c => c!.Excepciones)
                .Include(p => p.Actividades)
                    .ThenInclude(a => a.Predecesoras)
                .Include(p => p.Proyecto)
                .FirstOrDefault(p => p.Id == programaObraId);

            if (programa == null)
                return;

            var calendario = programa.CalendarioLaboral;
            var actividades = programa.Actividades.ToList();
            var actividadesNoResumen = actividades.Where(a => !a.EsResumen).OrderBy(a => a.Orden).ToList();
            var fechaBasePrograma = CalendarioCache.SanitizarFecha(programa.FechaInicioPrograma, _timeProvider);

            // Construir caché del calendario una sola vez para el recálculo de actividad y agrupadores.
            // Evita los loops día a día en CalculateFinishDate, CalculateBusinessDaysInclusive, etc.
            var rangoInicio = actividadesNoResumen
                .Where(a => a.FechaInicioProgramada.HasValue)
                .Select(a => CalendarioCache.SanitizarFecha(a.FechaInicioProgramada!.Value.Date, _timeProvider))
                .DefaultIfEmpty(fechaBasePrograma)
                .Min();
            var rangoFin = actividadesNoResumen
                .Where(a => a.FechaFinProgramada.HasValue)
                .Select(a => CalendarioCache.SanitizarFecha(a.FechaFinProgramada!.Value.Date, _timeProvider))
                .DefaultIfEmpty(fechaBasePrograma.AddYears(2))
                .Max();
            var cache = new CalendarioCache(calendario, rangoInicio, rangoFin);

            // Recalcular primero duración y fecha final de cada actividad (pueden cambiar por
            // rendimiento diario o por captura). Debe ocurrir antes de la red para que la ruta
            // crítica y las fechas que se persisten no se basen en duraciones/fechas obsoletas.
            foreach (var actividad in actividadesNoResumen)
            {
                var proyectoPresente = programa.Proyecto != null;
                var engine = proyectoPresente
                    ? CalculationEngineFactory.FromProyecto(programa.Proyecto!)
                    : new SoproCalculationEngine(2, 2, 4);
                RecalculateActivityInternal(actividad, cache, engine, proyectoPresente, _timeProvider);
            }

            // Delegar la red de actividades y la ruta crítica al calculador puro.
            // Rechaza ciclos, IDs duplicados, referencias desconocidas y tipos inválidos.
            var resultadoRed = ActivityNetworkCalculator.Calculate(
                ActivityNetworkAdapter.ToNetworkInput(
                    fechaBasePrograma,
                    actividadesNoResumen,
                    WorkingCalendarAdapter.ToWorkingCalendar(calendario)));

            foreach (var resultado in resultadoRed.Activities)
            {
                var actividad = actividades.First(a => a.Id == resultado.Id);
                actividad.FechaInicioProgramada = resultado.EarlyStartDate;
                actividad.FechaFinProgramada = resultado.EarlyFinishDate;
                actividad.DuracionDiasHabiles = Math.Max(1, resultado.DurationWorkingDays);
                actividad.FechaInicioTemprana = resultado.EarlyStartDate;
                actividad.FechaFinTemprana = resultado.EarlyFinishDate;
                actividad.FechaInicioTardia = resultado.LateStartDate;
                actividad.FechaFinTardia = resultado.LateFinishDate;
                actividad.HolguraDias = resultado.SlackDays;
                actividad.RutaCritica = resultado.IsCritical;
                actividad.FechaModificacion = _timeProvider.GetLocalNow().DateTime;
            }

            RecalcularAgrupadores(actividades, cache, _timeProvider);

            programa.FechaInicioPrograma = programa.Actividades
                .Where(a => !a.EsResumen && a.FechaInicioProgramada.HasValue)
                .OrderBy(a => a.FechaInicioProgramada)
                .Select(a => CalendarioCache.SanitizarFecha(a.FechaInicioProgramada!.Value.Date, _timeProvider))
                .DefaultIfEmpty(fechaBasePrograma)
                .First();
            programa.FechaFinPrograma = programa.Actividades
                .Where(a => !a.EsResumen && a.FechaFinProgramada.HasValue)
                .OrderByDescending(a => a.FechaFinProgramada)
                .Select(a => CalendarioCache.SanitizarFecha(a.FechaFinProgramada!.Value.Date, _timeProvider))
                .DefaultIfEmpty(fechaBasePrograma)
                .First();
            programa.FechaModificacion = _timeProvider.GetLocalNow().DateTime;

            context.SaveChanges();
        }

        public void RecalculateActivity(SOPROContext context, int actividadId)
        {
            var actividad = context.ActividadesProgramadas
                .Include(a => a.ProgramaObra)
                    .ThenInclude(p => p.CalendarioLaboral)
                        .ThenInclude(c => c!.Excepciones)
                .Include(a => a.ProgramaObra)
                    .ThenInclude(p => p.Proyecto)
                .FirstOrDefault(a => a.Id == actividadId);
            if (actividad == null)
                return;

            var calAct = actividad.ProgramaObra?.CalendarioLaboral;
            var fechaIniAct = CalendarioCache.SanitizarFecha(actividad.FechaInicioProgramada?.Date ?? _timeProvider.GetLocalNow().Date, _timeProvider);
            var fechaFinAct = CalendarioCache.SanitizarFecha(actividad.FechaFinProgramada?.Date ?? fechaIniAct.AddYears(1), _timeProvider);
            if (fechaFinAct < fechaIniAct)
                fechaFinAct = fechaIniAct;
            var cacheAct = new CalendarioCache(calAct, fechaIniAct, fechaFinAct);
            var proyecto = actividad.ProgramaObra?.Proyecto;
            var proyectoPresente = proyecto != null;
            var engine = proyectoPresente
                ? CalculationEngineFactory.FromProyecto(proyecto!)
                : new SoproCalculationEngine(2, 2, 4);
            RecalculateActivityInternal(actividad, cacheAct, engine, proyectoPresente, _timeProvider);
            context.SaveChanges();
        }

        public DateTime? CalculateFinishDate(SOPROContext context, int programaObraId, DateTime? start, int duracionDiasHabiles)
        {
            if (!start.HasValue)
                return null;

            var programa = context.ProgramasObra
                .AsNoTracking()
                .Include(p => p.CalendarioLaboral)
                    .ThenInclude(c => c!.Excepciones)
                .FirstOrDefault(p => p.Id == programaObraId);

            return CalculateFinishDate(programa?.CalendarioLaboral, start, duracionDiasHabiles);
        }

        public int CalculateBusinessDaysInclusive(SOPROContext context, int programaObraId, DateTime? start, DateTime? end)
        {
            var programa = context.ProgramasObra
                .AsNoTracking()
                .Include(p => p.CalendarioLaboral)
                    .ThenInclude(c => c!.Excepciones)
                .FirstOrDefault(p => p.Id == programaObraId);

            return CalculateBusinessDaysInclusive(programa?.CalendarioLaboral, start, end);
        }

        public DateTime? CalculateStartDate(SOPROContext context, int programaObraId, DateTime? finish, int duracionDiasHabiles)
        {
            if (!finish.HasValue)
                return null;

            var programa = context.ProgramasObra
                .AsNoTracking()
                .Include(p => p.CalendarioLaboral)
                    .ThenInclude(c => c!.Excepciones)
                .FirstOrDefault(p => p.Id == programaObraId);

            return CalculateStartDate(programa?.CalendarioLaboral, finish, duracionDiasHabiles);
        }

        public bool IsWorkingDay(SOPROContext context, int programaObraId, DateTime? date)
        {
            if (!date.HasValue)
                return false;

            var programa = context.ProgramasObra
                .AsNoTracking()
                .Include(p => p.CalendarioLaboral)
                    .ThenInclude(c => c!.Excepciones)
                .FirstOrDefault(p => p.Id == programaObraId);

            return IsWorkingDay(programa?.CalendarioLaboral, date.Value.Date);
        }

        private static void RecalcularAgrupadores(List<ActividadProgramada> actividades, CalendarioCache cache, TimeProvider timeProvider)
        {
            if (actividades.Count == 0)
                return;

            var ordenadas = actividades.OrderBy(a => a.Orden).ThenBy(a => a.Id).ToList();

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
                    resumen.ImporteProgramado = 0m;
                    resumen.CantidadTotal = 0m;
                    resumen.CantidadProgramada = 0m;
                    resumen.PrecioUnitario = 0m;
                    resumen.RendimientoDiario = 0m;
                    resumen.FrentesTrabajo = 0;
                    continue;
                }

                var inicio = programables
                    .Where(x => x.FechaInicioProgramada.HasValue)
                    .Select(x => x.FechaInicioProgramada!.Value.Date)
                    .Cast<DateTime?>()
                    .DefaultIfEmpty(null)
                    .Min();

                var fin = programables
                    .Where(x => x.FechaFinProgramada.HasValue)
                    .Select(x => x.FechaFinProgramada!.Value.Date)
                    .Cast<DateTime?>()
                    .DefaultIfEmpty(null)
                    .Max();

                resumen.FechaInicioProgramada = inicio;
                resumen.FechaFinProgramada = fin;
                resumen.DuracionDiasHabiles = cache.CountWorkingDays( inicio, fin);
                resumen.ImporteTotal = programables.Sum(x => x.ImporteTotal);
                resumen.ImporteProgramado = programables.Sum(x => x.ImporteProgramado);
                resumen.CantidadTotal = 0m;
                resumen.CantidadProgramada = 0m;
                resumen.PrecioUnitario = 0m;
                resumen.RendimientoDiario = 0m;
                resumen.FrentesTrabajo = 0;
                resumen.AvanceProgramadoPorcentaje = 0m;
                resumen.FechaModificacion = timeProvider.GetLocalNow().DateTime;
            }
        }

        private static List<ActividadProgramada> ObtenerDescendientesDelBloque(List<ActividadProgramada> ordenadas, int indiceAgrupador)
        {
            var resultado = new List<ActividadProgramada>();
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

        private static DateTime? CalculateStartDate(CalendarioLaboral? calendario, DateTime? finish, int duracionDiasHabiles)
        {
            return WorkingCalendarCalculator.CalculateStartDate(
                WorkingCalendarAdapter.ToWorkingCalendar(calendario), finish, duracionDiasHabiles);
        }

        internal static void RecalculateActivityInternal(ActividadProgramada actividad, CalendarioCache cache, SoproCalculationEngine engine, bool proyectoPresente, TimeProvider? timeProvider = null)
        {
            if (actividad.CantidadTotal > 0)
            {
                actividad.ImporteTotal = proyectoPresente
                    ? engine.Multiply(actividad.CantidadTotal, actividad.PrecioUnitario)
                    : engine.RoundAmount(actividad.CantidadTotal * actividad.PrecioUnitario);
            }

            var frentes = Math.Max(1, actividad.FrentesTrabajo);

            if (actividad.EsHito)
            {
                actividad.DuracionDiasHabiles = 0;
            }

            if (actividad.FechaInicioProgramada.HasValue && actividad.FechaFinProgramada.HasValue && actividad.DuracionDiasHabiles <= 0)
            {
                actividad.DuracionDiasHabiles = cache.CountWorkingDays(actividad.FechaInicioProgramada, actividad.FechaFinProgramada);
            }

            if (actividad.DuracionDiasHabiles > 0 && actividad.CantidadTotal > 0)
            {
                actividad.RendimientoDiario = Math.Round(actividad.CantidadTotal / (actividad.DuracionDiasHabiles * frentes), 4, MidpointRounding.AwayFromZero);
            }
            else if (actividad.RendimientoDiario > 0 && actividad.CantidadTotal > 0)
            {
                var divisor = actividad.RendimientoDiario * frentes;
                if (divisor > 0)
                    actividad.DuracionDiasHabiles = (int)Math.Ceiling(actividad.CantidadTotal / divisor);
            }

            if (actividad.FechaInicioProgramada.HasValue)
            {
                actividad.FechaFinProgramada = cache.CalculateFinishDate(actividad.FechaInicioProgramada, actividad.DuracionDiasHabiles);
            }

            actividad.CantidadProgramada = actividad.CantidadTotal;
            actividad.ImporteProgramado = actividad.ImporteTotal;
            actividad.AvanceProgramadoPorcentaje = actividad.CantidadTotal <= 0 ? 0 : 100;
            actividad.FechaModificacion = (timeProvider ?? TimeProvider.System).GetLocalNow().DateTime;
        }

        private static DateTime? CalculateFinishDate(CalendarioLaboral? calendario, DateTime? start, int duracionDiasHabiles)
        {
            if (!start.HasValue)
                return null;

            if (duracionDiasHabiles > MaxDuracionDiasHabiles)
                throw new InvalidOperationException($"La duración calculada ({duracionDiasHabiles:N0} días hábiles) es demasiado grande. Revisa el rendimiento diario, los frentes o las fechas capturadas.");

            return WorkingCalendarCalculator.CalculateFinishDate(
                WorkingCalendarAdapter.ToWorkingCalendar(calendario), start, duracionDiasHabiles);
        }

        private static int CalculateBusinessDaysInclusive(CalendarioLaboral? calendario, DateTime? start, DateTime? end)
        {
            return WorkingCalendarCalculator.CountWorkingDays(
                WorkingCalendarAdapter.ToWorkingCalendar(calendario), start, end);
        }

        private static bool IsWorkingDay(CalendarioLaboral? calendario, DateTime date)
        {
            return WorkingCalendarCalculator.IsWorkingDay(
                WorkingCalendarAdapter.ToWorkingCalendar(calendario), date);
        }
    }
}
