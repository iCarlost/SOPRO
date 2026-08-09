using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class ProgramacionCalculationService
    {
        private const int MaxDuracionDiasHabiles = 36500;
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
            var fechasBaseSinDependencias = new Dictionary<int, DateTime?>();
            var fechaBasePrograma = CalendarioCache.SanitizarFecha(programa.FechaInicioPrograma);

            // Construir caché del calendario una sola vez para todo el recálculo
            // Evita los loops día a día en CalculateFinishDate, CalculateBusinessDaysInclusive, etc.
            var rangoInicio = actividadesNoResumen
                .Where(a => a.FechaInicioProgramada.HasValue)
                .Select(a => CalendarioCache.SanitizarFecha(a.FechaInicioProgramada!.Value.Date))
                .DefaultIfEmpty(fechaBasePrograma)
                .Min();
            var rangoFin = actividadesNoResumen
                .Where(a => a.FechaFinProgramada.HasValue)
                .Select(a => CalendarioCache.SanitizarFecha(a.FechaFinProgramada!.Value.Date))
                .DefaultIfEmpty(fechaBasePrograma.AddYears(2))
                .Max();
            var cache = new CalendarioCache(calendario, rangoInicio, rangoFin);

            foreach (var actividad in actividadesNoResumen)
            {
                RecalculateActivityInternal(programa.Proyecto, actividad, cache);
                fechasBaseSinDependencias[actividad.Id] = actividad.FechaInicioProgramada.HasValue
                    ? CalendarioCache.SanitizarFecha(actividad.FechaInicioProgramada.Value.Date)
                    : fechaBasePrograma;
                actividad.FechaInicioTemprana = null;
                actividad.FechaFinTemprana = null;
                actividad.FechaInicioTardia = null;
                actividad.FechaFinTardia = null;
                actividad.HolguraDias = 0;
                actividad.RutaCritica = false;
            }

            var cambios = true;
            var intentos = 0;
            var maxIntentos = Math.Max(4, actividadesNoResumen.Count * 6);
            while (cambios && intentos < maxIntentos)
            {
                cambios = false;
                intentos++;

                foreach (var actividad in actividadesNoResumen)
                {
                    var inicioAnterior = actividad.FechaInicioProgramada?.Date;
                    var finAnterior = actividad.FechaFinProgramada?.Date;

                    var rango = CalcularRangoSegunDependencias(actividad, actividadesNoResumen, cache, fechasBaseSinDependencias.GetValueOrDefault(actividad.Id));

                    actividad.FechaInicioProgramada = rango.Inicio;
                    actividad.FechaFinProgramada = rango.Fin;
                    actividad.DuracionDiasHabiles = Math.Max(1, rango.DuracionDiasHabiles);
                    actividad.FechaInicioTemprana = rango.Inicio;
                    actividad.FechaFinTemprana = rango.Fin;
                    actividad.FechaModificacion = DateTime.Now;

                    if (inicioAnterior != actividad.FechaInicioProgramada?.Date || finAnterior != actividad.FechaFinProgramada?.Date)
                    {
                        cambios = true;
                    }
                }
            }

            RecalcularAgrupadores(actividades, cache);

            CalcularRutaCritica(actividadesNoResumen, cache);

            programa.FechaInicioPrograma = programa.Actividades
                .Where(a => !a.EsResumen && a.FechaInicioProgramada.HasValue)
                .OrderBy(a => a.FechaInicioProgramada)
                .Select(a => CalendarioCache.SanitizarFecha(a.FechaInicioProgramada!.Value.Date))
                .DefaultIfEmpty(fechaBasePrograma)
                .First();
            programa.FechaFinPrograma = programa.Actividades
                .Where(a => !a.EsResumen && a.FechaFinProgramada.HasValue)
                .OrderByDescending(a => a.FechaFinProgramada)
                .Select(a => CalendarioCache.SanitizarFecha(a.FechaFinProgramada!.Value.Date))
                .DefaultIfEmpty(fechaBasePrograma)
                .First();
            programa.FechaModificacion = DateTime.Now;

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
            var fechaIniAct = CalendarioCache.SanitizarFecha(actividad.FechaInicioProgramada?.Date ?? DateTime.Today);
            var fechaFinAct = CalendarioCache.SanitizarFecha(actividad.FechaFinProgramada?.Date ?? fechaIniAct.AddYears(1));
            if (fechaFinAct < fechaIniAct)
                fechaFinAct = fechaIniAct;
            var cacheAct = new CalendarioCache(calAct, fechaIniAct, fechaFinAct);
            RecalculateActivityInternal(actividad.ProgramaObra?.Proyecto, actividad, cacheAct);
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

        private static (DateTime? Inicio, DateTime? Fin, int DuracionDiasHabiles) CalcularRangoSegunDependencias(ActividadProgramada actividad, List<ActividadProgramada> actividades, CalendarioCache cache, DateTime? fechaBase)
        {
            DateTime? minimoInicio = null;
            DateTime? minimoFin = null;

            foreach (var dep in actividad.Predecesoras ?? Enumerable.Empty<DependenciaActividad>())
            {
                var origen = actividades.FirstOrDefault(a => a.Id == dep.ActividadOrigenId);
                if (origen == null)
                    continue;

                switch (dep.TipoDependencia)
                {
                    case TipoDependenciaActividad.FS when origen.FechaFinProgramada.HasValue:
                        AcumularMax(ref minimoInicio, cache.AddWorkingDaysExclusive( origen.FechaFinProgramada.Value, dep.DesfaseDias));
                        break;
                    case TipoDependenciaActividad.SS when origen.FechaInicioProgramada.HasValue:
                        AcumularMax(ref minimoInicio, cache.AddWorkingDaysInclusive( origen.FechaInicioProgramada.Value, dep.DesfaseDias));
                        break;
                    case TipoDependenciaActividad.FF when origen.FechaFinProgramada.HasValue:
                        AcumularMax(ref minimoFin, cache.AddWorkingDaysInclusive( origen.FechaFinProgramada.Value, dep.DesfaseDias));
                        break;
                    case TipoDependenciaActividad.SF when origen.FechaInicioProgramada.HasValue:
                        AcumularMax(ref minimoFin, cache.AddWorkingDaysInclusive( origen.FechaInicioProgramada.Value, dep.DesfaseDias));
                        break;
                }
            }

            var duracionActual = Math.Max(1, actividad.DuracionDiasHabiles);
            var tieneDeps = (actividad.Predecesoras?.Count ?? 0) > 0;
            DateTime? inicio = !tieneDeps ? fechaBase?.Date : minimoInicio;
            DateTime? fin = null;
            var duracionFinal = duracionActual;

            if (inicio.HasValue)
            {
                fin = cache.CalculateFinishDate( inicio, duracionActual);
            }

            if (inicio.HasValue && minimoFin.HasValue)
            {
                // Cuando la actividad tiene una restricción de inicio (FS/SS) y otra de fin (FF/SF),
                // el rango completo queda definido por ambas. No debemos conservar la duración previa,
                // porque eso impide que el plazo se reduzca cuando la fecha fin requerida retrocede.
                fin = minimoFin.Value.Date;

                if (fin.Value.Date < inicio.Value.Date)
                {
                    // Si por una combinación inválida de dependencias el fin quedara antes del inicio,
                    // dejamos el inicio dominante y colapsamos el rango a un solo día hábil.
                    fin = inicio.Value.Date;
                }

                duracionFinal = Math.Max(1, cache.CountWorkingDays( inicio, fin));
            }
            else if (!inicio.HasValue && minimoFin.HasValue)
            {
                fin = minimoFin.Value.Date;
                inicio = cache.CalculateStartDate( fin, duracionActual);
            }

            if (!inicio.HasValue)
            {
                inicio = fechaBase?.Date;
            }

            if (inicio.HasValue && !fin.HasValue)
            {
                fin = cache.CalculateFinishDate( inicio, duracionFinal);
            }

            if (inicio.HasValue && fin.HasValue)
            {
                // Si hubo restricciones solo de fin y el inicio se calculó hacia atrás, respetamos la duración actual.
                // Si hubo ambos límites, la duración ya fue extendida arriba según el rango real.
                duracionFinal = Math.Max(1, cache.CountWorkingDays( inicio, fin));
            }

            return (inicio?.Date, fin?.Date, duracionFinal);
        }

        private static void AcumularMax(ref DateTime? destino, DateTime? candidato)
        {
            if (!candidato.HasValue)
                return;
            if (!destino.HasValue || candidato.Value.Date > destino.Value.Date)
                destino = candidato.Value.Date;
        }


        private static void RecalcularAgrupadores(List<ActividadProgramada> actividades, CalendarioCache cache)
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
                resumen.FechaModificacion = DateTime.Now;
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

        private static void CalcularRutaCritica(List<ActividadProgramada> actividades, CalendarioCache cache)
        {
            var programadas = actividades
                .Where(a => a.FechaInicioProgramada.HasValue && a.FechaFinProgramada.HasValue)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Id)
                .ToList();

            if (programadas.Count == 0)
                return;

            foreach (var actividad in programadas)
            {
                actividad.FechaInicioTemprana = actividad.FechaInicioProgramada?.Date;
                actividad.FechaFinTemprana = actividad.FechaFinProgramada?.Date;
                actividad.FechaInicioTardia = null;
                actividad.FechaFinTardia = null;
                actividad.HolguraDias = 0;
                actividad.RutaCritica = false;
            }

            var fechaFinalPrograma = programadas.Max(a => a.FechaFinProgramada)!.Value.Date;
            var deps = programadas
                .SelectMany(a => a.Predecesoras ?? Enumerable.Empty<DependenciaActividad>())
                .Where(d => programadas.Any(a => a.Id == d.ActividadOrigenId) && programadas.Any(a => a.Id == d.ActividadDestinoId))
                .ToList();

            var sucesorasPorOrigen = deps
                .GroupBy(d => d.ActividadOrigenId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var terminales = programadas.Where(a => !sucesorasPorOrigen.ContainsKey(a.Id) || sucesorasPorOrigen[a.Id].Count == 0).ToList();
            foreach (var actividad in terminales)
            {
                actividad.FechaFinTardia = fechaFinalPrograma;
                actividad.FechaInicioTardia = cache.CalculateStartDate( fechaFinalPrograma, Math.Max(1, actividad.DuracionDiasHabiles));
            }

            var cambios = true;
            var intentos = 0;
            var maxIntentos = Math.Max(8, programadas.Count * 10);
            while (cambios && intentos < maxIntentos)
            {
                cambios = false;
                intentos++;

                for (int i = programadas.Count - 1; i >= 0; i--)
                {
                    var actividad = programadas[i];
                    if (!sucesorasPorOrigen.TryGetValue(actividad.Id, out var sucesoras) || sucesoras.Count == 0)
                        continue;

                    DateTime? maxInicioTardio = null;
                    DateTime? maxFinTardio = null;
                    DateTime? ultimoInicioPermitido = null;
                    DateTime? ultimoFinPermitido = null;

                    foreach (var dep in sucesoras)
                    {
                        var sucesora = programadas.FirstOrDefault(a => a.Id == dep.ActividadDestinoId);
                        if (sucesora == null)
                            continue;

                        switch (dep.TipoDependencia)
                        {
                            case TipoDependenciaActividad.FS when sucesora.FechaInicioTardia.HasValue:
                                AcumularMin(ref ultimoFinPermitido, cache.SubtractWorkingDaysExclusive( sucesora.FechaInicioTardia.Value, dep.DesfaseDias));
                                break;
                            case TipoDependenciaActividad.SS when sucesora.FechaInicioTardia.HasValue:
                                AcumularMin(ref ultimoInicioPermitido, cache.SubtractWorkingDaysInclusive( sucesora.FechaInicioTardia.Value, dep.DesfaseDias));
                                break;
                            case TipoDependenciaActividad.FF when sucesora.FechaFinTardia.HasValue:
                                AcumularMin(ref ultimoFinPermitido, cache.SubtractWorkingDaysInclusive( sucesora.FechaFinTardia.Value, dep.DesfaseDias));
                                break;
                            case TipoDependenciaActividad.SF when sucesora.FechaFinTardia.HasValue:
                                AcumularMin(ref ultimoInicioPermitido, cache.SubtractWorkingDaysInclusive( sucesora.FechaFinTardia.Value, dep.DesfaseDias));
                                break;
                        }
                    }

                    // Resolver rango tardío de forma coherente con ambos límites, si existen.
                    if (ultimoInicioPermitido.HasValue && ultimoFinPermitido.HasValue)
                    {
                        var inicio = ultimoInicioPermitido.Value.Date;
                        var fin = ultimoFinPermitido.Value.Date;
                        if (fin < inicio)
                            fin = inicio;

                        maxInicioTardio = inicio;
                        maxFinTardio = fin;
                    }
                    else if (ultimoInicioPermitido.HasValue)
                    {
                        maxInicioTardio = ultimoInicioPermitido.Value.Date;
                        maxFinTardio = cache.CalculateFinishDate( maxInicioTardio, Math.Max(1, actividad.DuracionDiasHabiles));
                    }
                    else if (ultimoFinPermitido.HasValue)
                    {
                        maxFinTardio = ultimoFinPermitido.Value.Date;
                        maxInicioTardio = cache.CalculateStartDate( maxFinTardio, Math.Max(1, actividad.DuracionDiasHabiles));
                    }

                    var cambioLocal = false;
                    if (maxInicioTardio.HasValue)
                    {
                        if (!actividad.FechaInicioTardia.HasValue || actividad.FechaInicioTardia.Value.Date != maxInicioTardio.Value.Date)
                        {
                            actividad.FechaInicioTardia = maxInicioTardio.Value.Date;
                            cambioLocal = true;
                        }
                    }
                    if (maxFinTardio.HasValue)
                    {
                        if (!actividad.FechaFinTardia.HasValue || actividad.FechaFinTardia.Value.Date != maxFinTardio.Value.Date)
                        {
                            actividad.FechaFinTardia = maxFinTardio.Value.Date;
                            cambioLocal = true;
                        }
                    }

                    if (cambioLocal)
                        cambios = true;
                }
            }

            foreach (var actividad in programadas)
            {
                if (!actividad.FechaInicioTemprana.HasValue || !actividad.FechaInicioTardia.HasValue)
                {
                    actividad.HolguraDias = 0;
                    actividad.RutaCritica = false;
                    continue;
                }

                var holgura = cache.CountWorkingDays( actividad.FechaInicioTemprana, actividad.FechaInicioTardia) - 1;
                actividad.HolguraDias = Math.Max(0, holgura);
                actividad.RutaCritica = actividad.HolguraDias == 0;
            }
        }

        private static void AcumularMin(ref DateTime? destino, DateTime? candidato)
        {
            if (!candidato.HasValue)
                return;
            if (!destino.HasValue || candidato.Value.Date < destino.Value.Date)
                destino = candidato.Value.Date;
        }

        private static DateTime SubtractWorkingDaysInclusive(CalendarioLaboral? calendario, DateTime date, int days)
        {
            var current = date.Date;
            while (!IsWorkingDay(calendario, current))
                current = current.AddDays(-1);

            if (days <= 0)
                return current;

            var remaining = days;
            while (remaining > 0)
            {
                current = current.AddDays(-1);
                while (!IsWorkingDay(calendario, current))
                    current = current.AddDays(-1);
                remaining--;
            }

            return current;
        }

        private static DateTime SubtractWorkingDaysExclusive(CalendarioLaboral? calendario, DateTime date, int days)
        {
            var current = date.Date.AddDays(-1);
            while (!IsWorkingDay(calendario, current))
                current = current.AddDays(-1);

            if (days <= 0)
                return current;

            return SubtractWorkingDaysInclusive(calendario, current, days);
        }

        private static DateTime AddWorkingDaysInclusive(CalendarioLaboral? calendario, DateTime date, int days)
        {
            var current = date.Date;
            while (!IsWorkingDay(calendario, current))
                current = current.AddDays(1);

            if (days <= 0)
                return current;

            var remaining = days;
            while (remaining > 0)
            {
                current = current.AddDays(1);
                while (!IsWorkingDay(calendario, current))
                    current = current.AddDays(1);
                remaining--;
            }

            return current;
        }

        private static DateTime AddWorkingDaysExclusive(CalendarioLaboral? calendario, DateTime date, int days)
        {
            var current = date.Date.AddDays(1);
            while (!IsWorkingDay(calendario, current))
                current = current.AddDays(1);

            if (days <= 0)
                return current;

            return AddWorkingDaysInclusive(calendario, current, days);
        }

        private static DateTime? CalculateStartDate(CalendarioLaboral? calendario, DateTime? finish, int duracionDiasHabiles)
        {
            if (!finish.HasValue)
                return null;

            var current = finish.Value.Date;
            while (!IsWorkingDay(calendario, current))
                current = current.AddDays(-1);

            if (duracionDiasHabiles <= 1)
                return current;

            var remaining = duracionDiasHabiles - 1;
            while (remaining > 0)
            {
                current = current.AddDays(-1);
                while (!IsWorkingDay(calendario, current))
                    current = current.AddDays(-1);
                remaining--;
            }

            return current;
        }

        private static void RecalculateActivityInternal(Proyecto? proyecto, ActividadProgramada actividad, CalendarioCache cache)
        {
            if (actividad.CantidadTotal > 0)
            {
                actividad.ImporteTotal = proyecto != null
                    ? new MotorCalculoSopro(proyecto).Multiplicar(actividad.CantidadTotal, actividad.PrecioUnitario)
                    : Math.Round(actividad.CantidadTotal * actividad.PrecioUnitario, 2, MidpointRounding.AwayFromZero);
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
            actividad.FechaModificacion = DateTime.Now;
        }

        private static DateTime? CalculateFinishDate(CalendarioLaboral? calendario, DateTime? start, int duracionDiasHabiles)
        {
            if (!start.HasValue)
                return null;

            if (duracionDiasHabiles > MaxDuracionDiasHabiles)
                throw new InvalidOperationException($"La duración calculada ({duracionDiasHabiles:N0} días hábiles) es demasiado grande. Revisa el rendimiento diario, los frentes o las fechas capturadas.");

            var current = start.Value.Date;
            if (duracionDiasHabiles <= 1)
            {
                if (duracionDiasHabiles <= 0)
                    return current;

                while (!IsWorkingDay(calendario, current))
                {
                    if (current >= DateTime.MaxValue.Date)
                        throw new InvalidOperationException("La fecha calculada rebasa el rango permitido. Revisa el rendimiento diario, los frentes o las fechas capturadas.");
                    current = current.AddDays(1);
                }

                return current;
            }

            var remaining = duracionDiasHabiles;
            while (remaining > 0)
            {
                if (IsWorkingDay(calendario, current))
                {
                    remaining--;
                    if (remaining == 0)
                        break;
                }

                if (current >= DateTime.MaxValue.Date)
                    throw new InvalidOperationException("La fecha calculada rebasa el rango permitido. Revisa el rendimiento diario, los frentes o las fechas capturadas.");

                current = current.AddDays(1);
            }

            return current;
        }

        private static int CalculateBusinessDaysInclusive(CalendarioLaboral? calendario, DateTime? start, DateTime? end)
        {
            if (!start.HasValue || !end.HasValue)
                return 0;

            var inicio = start.Value.Date;
            var fin = end.Value.Date;
            if (fin < inicio)
                return 0;

            var count = 0;
            for (var current = inicio; current <= fin; current = current.AddDays(1))
            {
                if (IsWorkingDay(calendario, current))
                    count++;
            }

            return count;
        }

        private static bool IsWorkingDay(CalendarioLaboral? calendario, DateTime date)
        {
            if (calendario == null)
                return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;

            var ex = calendario.Excepciones.FirstOrDefault(x => x.Fecha.Date == date.Date);
            if (ex != null)
            {
                if (ex.Tipo == TipoExcepcionCalendario.LaborableEspecial)
                    return true;

                return false;
            }

            return date.DayOfWeek switch
            {
                DayOfWeek.Monday => calendario.Lunes,
                DayOfWeek.Tuesday => calendario.Martes,
                DayOfWeek.Wednesday => calendario.Miercoles,
                DayOfWeek.Thursday => calendario.Jueves,
                DayOfWeek.Friday => calendario.Viernes,
                DayOfWeek.Saturday => calendario.Sabado,
                DayOfWeek.Sunday => calendario.Domingo,
                _ => false
            };
        }
    }
}
