using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  ProgramacionCurvaSService — VERSIÓN CORREGIDA v1.0                    ║
    // ║                                                                         ║
    // ║  CAMBIO RESPECTO A LA VERSIÓN ORIGINAL:                                 ║
    // ║  [FIX-1] ImportePeriodo / ImporteAcumulado: eliminados los             ║
    // ║          decimal.Round(..., 2, ...) hardcodeados.                       ║
    // ║          Ahora usan engine.RoundAmount() que respeta                    ║
    // ║          proyecto.DecimalesImporte configurado por el usuario.          ║
    // ║  [FIX-2] CantidadPeriodo / CantidadAcumulada: mismo ajuste con         ║
    // ║          engine.RoundQuantity() en lugar de hardcoded 4.                ║
    // ║  [FIX-3] Porcentajes: usan engine.RoundPercentage() en lugar de        ║
    // ║          hardcoded 4. Consistente con resto del sistema.                ║
    // ║                                                                         ║
    // ║  [N5-14] Motor migrado a SoproCalculationEngine: los 10 redondeos de    ║
    // ║          BuildInternal (RoundAmount ×3, RoundQuantity ×3,               ║
    // ║          RoundPercentage ×4) más las dos construcciones del motor en    ║
    // ║          BuildFinancialCurve usan engine con las tres precisiones del   ║
    // ║          proyecto. Sin cambios de API ni de comportamiento observable.   ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public sealed class ProgramacionCurvaSService
    {
        /// <summary>
        /// Construye la Curva S financiera de un programa de obra.
        /// Recibe el proyecto para obtener la configuración de decimales.
        ///
        /// NOTA: Si el llamador actual no tiene acceso al proyecto,
        /// obtenerlo con:
        ///   var proyecto = context.ProgramasObra
        ///       .Include(p => p.Proyecto)
        ///       .Where(p => p.Id == programaObraId)
        ///       .Select(p => p.Proyecto)
        ///       .FirstOrDefault();
        /// </summary>
        public List<CurvaSRowDto> BuildFinancialCurve(
            SOPROContext context, int programaObraId,
            SOPRO.Core.Entities.Proyecto proyecto)
        {
            // Motor: respeta la configuración del usuario [FIX-1] [FIX-2] [FIX-3]
            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            return BuildInternal(context, programaObraId, engine);
        }

        /// <summary>
        /// Sobrecarga de compatibilidad cuando solo se tiene el programaObraId.
        /// Carga el proyecto internamente. Preferir la sobrecarga con proyecto
        /// explícito para evitar la consulta adicional.
        /// </summary>
        public List<CurvaSRowDto> BuildFinancialCurve(SOPROContext context, int programaObraId)
        {
            var proyecto = context.ProgramasObra
                .Include(p => p.Proyecto)
                .AsNoTracking()
                .Where(p => p.Id == programaObraId)
                .Select(p => p.Proyecto)
                .FirstOrDefault();

            // Fallback seguro si el proyecto no existe (2 decimales por defecto)
            var engine = proyecto != null
                ? CalculationEngineFactory.FromProyecto(proyecto)
                : new SoproCalculationEngine(2, 2, 4);

            return BuildInternal(context, programaObraId, engine);
        }

        // ════════════════════════════════════════════════════════════════════════
        // IMPLEMENTACIÓN INTERNA
        // ════════════════════════════════════════════════════════════════════════

        private static List<CurvaSRowDto> BuildInternal(
            SOPROContext context, int programaObraId, SoproCalculationEngine engine)
        {
            var periodos = context.PeriodosPrograma
                .AsNoTracking()
                .Where(p => p.ProgramaObraId == programaObraId)
                .OrderBy(p => p.NumeroPeriodo)
                .Select(p => new { p.Id, p.NumeroPeriodo, p.Etiqueta, p.FechaInicio, p.FechaFin })
                .ToList();

            if (periodos.Count == 0)
                return new List<CurvaSRowDto>();

            var actividadIds = context.ActividadesProgramadas
                .AsNoTracking()
                .Where(a => a.ProgramaObraId == programaObraId && !a.EsResumen)
                .Select(a => a.Id)
                .ToList();

            // SQLite no soporta SUM(decimal) traducido por EF Core —
            // traemos a memoria antes de agrupar y sumar.
            var distribuciones = context.DistribucionesPeriodo
                .AsNoTracking()
                .Where(d => actividadIds.Contains(d.ActividadProgramadaId))
                .AsEnumerable()
                .ToList();

            var importesPorPeriodo = distribuciones
                .GroupBy(d => d.PeriodoProgramaId)
                .ToDictionary(g => g.Key,
                              g => g.Sum(x => x.ImporteProgramado));

            var cantidadesPorPeriodo = distribuciones
                .GroupBy(d => d.PeriodoProgramaId)
                .ToDictionary(g => g.Key,
                              g => g.Sum(x => x.CantidadProgramada));

            // Totales generales — redondeados para que los porcentajes cierren al 100%
            decimal total         = engine.RoundAmount(importesPorPeriodo.Values.Sum());
            decimal totalCantidad = engine.RoundQuantity(cantidadesPorPeriodo.Values.Sum());

            decimal acumulado         = 0m;
            decimal acumuladoCantidad = 0m;

            var rows = new List<CurvaSRowDto>(periodos.Count);

            foreach (var p in periodos)
            {
                // ── Valores del periodo ─────────────────────────────────────────
                var importeRaw  = importesPorPeriodo.TryGetValue(p.Id, out var imp) ? imp : 0m;
                var cantidadRaw = cantidadesPorPeriodo.TryGetValue(p.Id, out var cant) ? cant : 0m;

                // [FIX-1] Redondear con la configuración del proyecto, no hardcoded
                decimal importe  = engine.RoundAmount(importeRaw);
                decimal cantidad = engine.RoundQuantity(cantidadRaw);

                acumulado         = engine.RoundAmount(acumulado + importe);
                acumuladoCantidad = engine.RoundQuantity(acumuladoCantidad + cantidad);

                // ── Porcentajes ─────────────────────────────────────────────────
                // [FIX-3] engine.RoundPercentage() — respeta DecimalesPorcentaje
                decimal pctPeriodo = total == 0m ? 0m
                    : engine.RoundPercentage(importe / total * 100m);
                decimal pctAcum = total == 0m ? 0m
                    : engine.RoundPercentage(acumulado / total * 100m);
                decimal pctFisPeriodo = totalCantidad == 0m ? 0m
                    : engine.RoundPercentage(cantidad / totalCantidad * 100m);
                decimal pctFisAcum = totalCantidad == 0m ? 0m
                    : engine.RoundPercentage(acumuladoCantidad / totalCantidad * 100m);

                rows.Add(new CurvaSRowDto
                {
                    NumeroPeriodo             = p.NumeroPeriodo,
                    Etiqueta                  = p.Etiqueta,
                    FechaInicio               = p.FechaInicio,
                    FechaFin                  = p.FechaFin,

                    // [FIX-2] Usa DecimalesCantidad del proyecto
                    CantidadPeriodo           = cantidad,
                    CantidadAcumulada         = acumuladoCantidad,

                    // [FIX-1] Usa DecimalesImporte del proyecto
                    ImportePeriodo            = importe,
                    ImporteAcumulado          = acumulado,

                    // [FIX-3] Usa DecimalesPorcentaje del proyecto
                    PorcentajePeriodo         = pctPeriodo,
                    PorcentajeAcumulado       = pctAcum,
                    PorcentajeFisicoPeriodo   = pctFisPeriodo,
                    PorcentajeFisicoAcumulado = pctFisAcum
                });
            }

            return rows;
        }
    }
}
