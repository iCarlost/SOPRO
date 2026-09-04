using System;
using SOPRO.Application.Services.Programacion;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  ProgramacionDistributionService — VERSIÓN CORREGIDA v1.0              ║
    // ║                                                                         ║
    // ║  CAMBIOS RESPECTO A LA VERSIÓN ORIGINAL:                                ║
    // ║  [FIX-1] DistributeUniform: el importe del último periodo usa           ║
    // ║           Ajuste de Residuo (importeTotal - importesAcumulados)         ║
    // ║           en lugar de recalcularse independientemente.                  ║
    // ║  [FIX-2] DistributeByPercentages: mismo ajuste de residuo en importe.  ║
    // ║  [FIX-3] Toda aritmética de importe delegada a SoproCalculationEngine. ║
    // ║  [FIX-4] Fallback hardcoded "2" eliminado — usa proyecto.Decimales*.   ║
    // ║                                                                         ║
    // ║  [N5-15] Motor migrado a SoproCalculationEngine: las 3 construcciones   ║
    // ║          (DistributeUniform, DistributeUniformBatch,                    ║
    // ║          DistributeByPercentages) y las 24 operaciones                  ║
    // ║          (Multiply ×6, RoundAmount ×3, RoundQuantity ×6,                ║
    // ║          RoundPercentage ×9) usan engine con las tres precisiones.      ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public sealed class ProgramacionDistributionService
    {
        // ════════════════════════════════════════════════════════════════════════
        // DISTRIBUCIÓN UNIFORME POR DÍAS HÁBILES
        // ════════════════════════════════════════════════════════════════════════

        public void DistributeUniform(SOPROContext context, int actividadId)
        {
            var actividad = context.ActividadesProgramadas
                .Include(a => a.ProgramaObra)
                    .ThenInclude(p => p.Proyecto)
                .Include(a => a.ProgramaObra)
                    .ThenInclude(p => p.CalendarioLaboral)
                        .ThenInclude(c => c.Excepciones)
                .Include(a => a.Distribuciones)
                .FirstOrDefault(a => a.Id == actividadId);

            if (actividad == null) return;

            var todosLosPeriodos = context.PeriodosPrograma
                .Where(p => p.ProgramaObraId == actividad.ProgramaObraId)
                .OrderBy(p => p.NumeroPeriodo)
                .ToList();

            if (!todosLosPeriodos.Any()) return;

            context.DistribucionesPeriodo.RemoveRange(actividad.Distribuciones);

            var proyecto    = actividad.ProgramaObra?.Proyecto;
            var calendario  = actividad.ProgramaObra?.CalendarioLaboral;

            // ── Motor con la configuración real del proyecto (sin fallbacks mágicos) ─
            var engine = proyecto != null
                ? CalculationEngineFactory.FromProyecto(proyecto)
                : new SoproCalculationEngine(4, 2, 4); // solo si proyecto es null (no debería ocurrir)

            var fechaInicio = actividad.FechaInicioProgramada?.Date;
            var fechaFin    = actividad.FechaFinProgramada?.Date ?? fechaInicio;

            var periodos = todosLosPeriodos;
            if (fechaInicio.HasValue && fechaFin.HasValue)
            {
                periodos = todosLosPeriodos
                    .Where(p => p.FechaInicio.Date <= fechaFin.Value && p.FechaFin.Date >= fechaInicio.Value)
                    .OrderBy(p => p.NumeroPeriodo)
                    .ToList();
            }

            if (!periodos.Any())
            {
                periodos = fechaInicio.HasValue
                    ? todosLosPeriodos
                        .Where(p => p.FechaInicio.Date <= fechaInicio.Value && p.FechaFin.Date >= fechaInicio.Value)
                        .ToList()
                    : todosLosPeriodos.Take(1).ToList();
            }

            if (!periodos.Any())
                periodos = todosLosPeriodos.Take(1).ToList();

            var tramos = BuildWorkingDayDistribution(periodos, calendario, fechaInicio, fechaFin);
            if (!tramos.Any())
            {
                tramos = periodos
                    .Select((periodo, index) => new DistributionSlice(periodo, index == 0 ? 1 : 0))
                    .Where(x => x.WorkingDays > 0)
                    .ToList();
            }

            var totalDias = tramos.Sum(x => x.WorkingDays);
            if (totalDias <= 0)
                totalDias = tramos.Count;

            // ── Calcular importe total de la actividad con precisión de pantalla ──
            decimal importeTotal = engine.Multiply(actividad.CantidadTotal, actividad.PrecioUnitario);

            decimal cantidadAcumulada  = 0m;
            decimal porcentajeAcumulado = 0m;
            decimal importeAcumulado   = 0m; // [FIX-1] trackear para Ajuste de Residuo

            for (int i = 0; i < tramos.Count; i++)
            {
                var tramo   = tramos[i];
                var esUltimo = i == tramos.Count - 1;

                // ── Cantidad (ya tenía ajuste de residuo ✅) ──────────────────────
                decimal cantidad;
                if (esUltimo)
                    cantidad = engine.RoundQuantity(actividad.CantidadTotal - cantidadAcumulada);
                else
                {
                    var proporcion = totalDias == 0 ? 0m : (decimal)tramo.WorkingDays / totalDias;
                    cantidad = engine.RoundQuantity(actividad.CantidadTotal * proporcion);
                }
                cantidadAcumulada += cantidad;

                // ── Porcentaje (ya tenía ajuste de residuo ✅) ────────────────────
                decimal porcentaje;
                if (esUltimo)
                    porcentaje = engine.RoundPercentage(100m - porcentajeAcumulado);
                else if (actividad.CantidadTotal == 0m)
                {
                    var proporcion = totalDias == 0 ? 0m : (decimal)tramo.WorkingDays / totalDias;
                    porcentaje = engine.RoundPercentage(proporcion * 100m);
                }
                else
                    porcentaje = engine.RoundPercentage((cantidad / actividad.CantidadTotal) * 100m);
                porcentajeAcumulado += porcentaje;

                // ── Importe [FIX-1]: Ajuste de Residuo en el último periodo ───────
                decimal importe;
                if (esUltimo)
                    // Absorbe el centavo pendiente para que SUM == importeTotal exactamente
                    importe = engine.RoundAmount(importeTotal - importeAcumulado);
                else
                    importe = engine.Multiply(cantidad, actividad.PrecioUnitario);
                importeAcumulado += importe;

                context.DistribucionesPeriodo.Add(new DistribucionPeriodo
                {
                    ActividadProgramadaId = actividad.Id,
                    PeriodoProgramaId     = tramo.Periodo.Id,
                    CantidadProgramada    = cantidad,
                    PorcentajeProgramado  = porcentaje,
                    PrecioUnitario        = actividad.PrecioUnitario,
                    ImporteProgramado     = importe
                });
            }

            actividad.MetodoDistribucion            = MetodoDistribucionActividad.Uniforme;
            actividad.CantidadProgramada            = actividad.CantidadTotal;
            actividad.ImporteProgramado             = importeTotal; // consistente con suma de periodos
            actividad.AvanceProgramadoPorcentaje    = actividad.CantidadTotal == 0m ? 0m : 100m;
            actividad.FechaModificacion             = DateTime.Now;
            context.SaveChanges();
        }

        /// <summary>
        /// Versión batch de DistributeUniform: distribuye uniformemente todas las actividades
        /// hoja de un programa en una sola transacción (1 SaveChanges al final).
        /// Mucho más rápido que llamar DistributeUniform por actividad cuando hay muchas.
        /// </summary>
        public void DistributeUniformBatch(SOPROContext context, int programaObraId)
        {
            // Carga todo en una sola query incluyendo las navegaciones necesarias
            var actividades = context.ActividadesProgramadas
                .Include(a => a.ProgramaObra)
                    .ThenInclude(p => p.Proyecto)
                .Include(a => a.ProgramaObra)
                    .ThenInclude(p => p.CalendarioLaboral)
                        .ThenInclude(c => c.Excepciones)
                .Include(a => a.Distribuciones)
                .Where(a => a.ProgramaObraId == programaObraId && !a.EsResumen)
                .ToList();

            if (!actividades.Any()) return;

            var todosLosPeriodos = context.PeriodosPrograma
                .Where(p => p.ProgramaObraId == programaObraId)
                .OrderBy(p => p.NumeroPeriodo)
                .ToList();

            if (!todosLosPeriodos.Any()) return;

            // Eliminar todas las distribuciones existentes en una sola operación
            var todasLasDistribuciones = actividades.SelectMany(a => a.Distribuciones).ToList();
            context.DistribucionesPeriodo.RemoveRange(todasLasDistribuciones);

            // Construir caché del calendario una sola vez para todo el batch
            var calendarioBatch = actividades.FirstOrDefault()?.ProgramaObra?.CalendarioLaboral;
            var rangoInicioBatch = actividades
                .Where(a => a.FechaInicioProgramada.HasValue)
                .Select(a => a.FechaInicioProgramada!.Value.Date)
                .DefaultIfEmpty(DateTime.Today)
                .Min();
            var rangoFinBatch = actividades
                .Where(a => a.FechaFinProgramada.HasValue)
                .Select(a => a.FechaFinProgramada!.Value.Date)
                .DefaultIfEmpty(DateTime.Today.AddYears(1))
                .Max();
            var cacheBatch = new CalendarioCache(calendarioBatch, rangoInicioBatch, rangoFinBatch);

            // Calcular y acumular distribuciones para cada actividad sin SaveChanges intermedio
            foreach (var actividad in actividades)
            {
                var proyecto   = actividad.ProgramaObra?.Proyecto;
                var engine = proyecto != null ? CalculationEngineFactory.FromProyecto(proyecto) : new SoproCalculationEngine(4, 2, 4);

                var fechaInicio = actividad.FechaInicioProgramada?.Date;
                var fechaFin    = actividad.FechaFinProgramada?.Date ?? fechaInicio;

                var periodos = todosLosPeriodos;
                if (fechaInicio.HasValue && fechaFin.HasValue)
                {
                    periodos = todosLosPeriodos
                        .Where(p => p.FechaInicio.Date <= fechaFin.Value && p.FechaFin.Date >= fechaInicio.Value)
                        .OrderBy(p => p.NumeroPeriodo)
                        .ToList();
                }

                if (!periodos.Any())
                {
                    periodos = fechaInicio.HasValue
                        ? todosLosPeriodos
                            .Where(p => p.FechaInicio.Date <= fechaInicio.Value && p.FechaFin.Date >= fechaInicio.Value)
                            .ToList()
                        : todosLosPeriodos.Take(1).ToList();
                }

                if (!periodos.Any())
                    periodos = todosLosPeriodos.Take(1).ToList();

                var tramos = BuildWorkingDayDistributionCached(periodos, cacheBatch, fechaInicio, fechaFin);
                if (!tramos.Any())
                {
                    tramos = periodos
                        .Select((periodo, index) => new DistributionSlice(periodo, index == 0 ? 1 : 0))
                        .Where(x => x.WorkingDays > 0)
                        .ToList();
                }

                var totalDias = tramos.Sum(x => x.WorkingDays);
                if (totalDias <= 0) totalDias = tramos.Count;

                decimal importeTotal        = engine.Multiply(actividad.CantidadTotal, actividad.PrecioUnitario);
                decimal cantidadAcumulada   = 0m;
                decimal porcentajeAcumulado = 0m;
                decimal importeAcumulado    = 0m;

                for (int i = 0; i < tramos.Count; i++)
                {
                    var tramo   = tramos[i];
                    var esUltimo = i == tramos.Count - 1;

                    decimal cantidad;
                    if (esUltimo)
                        cantidad = engine.RoundQuantity(actividad.CantidadTotal - cantidadAcumulada);
                    else
                    {
                        var proporcion = totalDias == 0 ? 0m : (decimal)tramo.WorkingDays / totalDias;
                        cantidad = engine.RoundQuantity(actividad.CantidadTotal * proporcion);
                    }
                    cantidadAcumulada += cantidad;

                    decimal porcentaje;
                    if (esUltimo)
                        porcentaje = engine.RoundPercentage(100m - porcentajeAcumulado);
                    else if (actividad.CantidadTotal == 0m)
                    {
                        var proporcion = totalDias == 0 ? 0m : (decimal)tramo.WorkingDays / totalDias;
                        porcentaje = engine.RoundPercentage(proporcion * 100m);
                    }
                    else
                        porcentaje = engine.RoundPercentage((cantidad / actividad.CantidadTotal) * 100m);
                    porcentajeAcumulado += porcentaje;

                    decimal importe;
                    if (esUltimo)
                        importe = engine.RoundAmount(importeTotal - importeAcumulado);
                    else
                        importe = engine.Multiply(cantidad, actividad.PrecioUnitario);
                    importeAcumulado += importe;

                    context.DistribucionesPeriodo.Add(new DistribucionPeriodo
                    {
                        ActividadProgramadaId = actividad.Id,
                        PeriodoProgramaId     = tramo.Periodo.Id,
                        CantidadProgramada    = cantidad,
                        PorcentajeProgramado  = porcentaje,
                        PrecioUnitario        = actividad.PrecioUnitario,
                        ImporteProgramado     = importe
                    });
                }

                actividad.MetodoDistribucion         = MetodoDistribucionActividad.Uniforme;
                actividad.CantidadProgramada         = actividad.CantidadTotal;
                actividad.ImporteProgramado          = importeTotal;
                actividad.AvanceProgramadoPorcentaje = actividad.CantidadTotal == 0m ? 0m : 100m;
                actividad.FechaModificacion          = DateTime.Now;
            }

            // Un solo SaveChanges para todo el batch
            context.SaveChanges();
        }

        // ════════════════════════════════════════════════════════════════════════
        // DISTRIBUCIÓN POR PORCENTAJES MANUALES
        // ════════════════════════════════════════════════════════════════════════

        public void DistributeByPercentages(SOPROContext context, int actividadId,
            List<PeriodDistributionInput> inputs)
        {
            var actividad = context.ActividadesProgramadas
                .Include(a => a.Distribuciones)
                .Include(a => a.ProgramaObra)
                    .ThenInclude(p => p.Proyecto)
                .FirstOrDefault(a => a.Id == actividadId);

            if (actividad == null) return;

            context.DistribucionesPeriodo.RemoveRange(actividad.Distribuciones);

            var proyecto = actividad.ProgramaObra?.Proyecto;
            var engine = proyecto != null ? CalculationEngineFactory.FromProyecto(proyecto) : new SoproCalculationEngine(4, 2, 4);

            var validInputs = inputs
                .Where(x => x.PorcentajeProgramado != 0 || x.CantidadProgramada != 0)
                .ToList();

            if (!validInputs.Any())
            {
                actividad.MetodoDistribucion         = MetodoDistribucionActividad.ManualPorPorcentaje;
                actividad.CantidadProgramada         = 0m;
                actividad.ImporteProgramado          = 0m;
                actividad.AvanceProgramadoPorcentaje = 0m;
                actividad.FechaModificacion          = DateTime.Now;
                context.SaveChanges();
                return;
            }

            decimal importeTotal = engine.Multiply(actividad.CantidadTotal, actividad.PrecioUnitario);

            decimal totalCantidad       = 0m;
            decimal totalImporte        = 0m;
            decimal porcentajeAcumulado = 0m;
            decimal importeAcumulado    = 0m; // [FIX-2]

            for (int i = 0; i < validInputs.Count; i++)
            {
                var input    = validInputs[i];
                var esUltimo = i == validInputs.Count - 1;

                decimal cantidad = input.CantidadProgramada;
                if (cantidad == 0m && actividad.CantidadTotal > 0m)
                {
                    cantidad = esUltimo
                        ? engine.RoundQuantity(actividad.CantidadTotal - totalCantidad)
                        : engine.RoundQuantity(
                            actividad.CantidadTotal * (input.PorcentajeProgramado / 100m));
                }

                decimal porcentaje = input.PorcentajeProgramado;
                if (actividad.CantidadTotal > 0m)
                {
                    porcentaje = esUltimo
                        ? engine.RoundPercentage(100m - porcentajeAcumulado)
                        : engine.RoundPercentage(
                            (cantidad / actividad.CantidadTotal) * 100m);
                }

                // [FIX-2]: Ajuste de Residuo en importe del último periodo
                decimal importe;
                if (esUltimo)
                    importe = engine.RoundAmount(importeTotal - importeAcumulado);
                else
                    importe = engine.Multiply(cantidad, actividad.PrecioUnitario);

                totalCantidad       += cantidad;
                totalImporte        += importe;
                porcentajeAcumulado += porcentaje;
                importeAcumulado    += importe;

                context.DistribucionesPeriodo.Add(new DistribucionPeriodo
                {
                    ActividadProgramadaId = actividadId,
                    PeriodoProgramaId     = input.PeriodoProgramaId,
                    CantidadProgramada    = cantidad,
                    PorcentajeProgramado  = porcentaje,
                    PrecioUnitario        = actividad.PrecioUnitario,
                    ImporteProgramado     = importe
                });
            }

            actividad.MetodoDistribucion            = MetodoDistribucionActividad.ManualPorPorcentaje;
            actividad.CantidadProgramada            = totalCantidad;
            actividad.ImporteProgramado             = importeTotal; // [FIX-2] usa el total exacto
            actividad.AvanceProgramadoPorcentaje    = actividad.CantidadTotal == 0
                ? 0m
                : engine.RoundPercentage(
                    (actividad.CantidadProgramada / actividad.CantidadTotal) * 100m);
            actividad.FechaModificacion             = DateTime.Now;
            context.SaveChanges();
        }

        // ════════════════════════════════════════════════════════════════════════
        // HELPERS DE CALENDARIO (sin cambios)
        // ════════════════════════════════════════════════════════════════════════

        private static List<DistributionSlice> BuildWorkingDayDistributionCached(
            List<PeriodoPrograma> periodos, CalendarioCache cache,
            DateTime? fechaInicio, DateTime? fechaFin)
        {
            var resultado = new List<DistributionSlice>();
            if (!fechaInicio.HasValue || !fechaFin.HasValue) return resultado;

            var inicioActividad = fechaInicio.Value.Date;
            var finActividad    = fechaFin.Value.Date;
            if (finActividad < inicioActividad) return resultado;

            foreach (var periodo in periodos)
            {
                var inicio = Max(inicioActividad, periodo.FechaInicio.Date);
                var fin    = Min(finActividad,    periodo.FechaFin.Date);
                if (fin < inicio) continue;

                var dias = cache.CountWorkingDays(inicio, fin);
                if (dias <= 0) continue;

                resultado.Add(new DistributionSlice(periodo, dias));
            }

            return resultado;
        }

        private static List<DistributionSlice> BuildWorkingDayDistribution(
            List<PeriodoPrograma> periodos, CalendarioLaboral? calendario,
            DateTime? fechaInicio, DateTime? fechaFin)
        {
            var resultado = new List<DistributionSlice>();
            if (!fechaInicio.HasValue || !fechaFin.HasValue) return resultado;

            var inicioActividad = fechaInicio.Value.Date;
            var finActividad    = fechaFin.Value.Date;
            if (finActividad < inicioActividad) return resultado;

            foreach (var periodo in periodos)
            {
                var inicio = Max(inicioActividad, periodo.FechaInicio.Date);
                var fin    = Min(finActividad,    periodo.FechaFin.Date);
                if (fin < inicio) continue;

                var dias = CountWorkingDays(calendario, inicio, fin);
                if (dias <= 0) continue;

                resultado.Add(new DistributionSlice(periodo, dias));
            }

            return resultado;
        }

        private static int CountWorkingDays(CalendarioLaboral? calendario, DateTime inicio, DateTime fin)
        {
            if (fin < inicio) return 0;
            int count = 0;
            for (var current = inicio.Date; current <= fin.Date; current = current.AddDays(1))
                if (IsWorkingDay(calendario, current))
                    count++;
            return count;
        }

        private static bool IsWorkingDay(CalendarioLaboral? calendario, DateTime date)
        {
            if (calendario == null)
                return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;

            var ex = calendario.Excepciones.FirstOrDefault(x => x.Fecha.Date == date.Date);
            if (ex != null)
                return ex.Tipo == TipoExcepcionCalendario.LaborableEspecial;

            return date.DayOfWeek switch
            {
                DayOfWeek.Monday    => calendario.Lunes,
                DayOfWeek.Tuesday   => calendario.Martes,
                DayOfWeek.Wednesday => calendario.Miercoles,
                DayOfWeek.Thursday  => calendario.Jueves,
                DayOfWeek.Friday    => calendario.Viernes,
                DayOfWeek.Saturday  => calendario.Sabado,
                DayOfWeek.Sunday    => calendario.Domingo,
                _ => false
            };
        }

        private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
        private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;

        private sealed record DistributionSlice(PeriodoPrograma Periodo, int WorkingDays);
    }
}
