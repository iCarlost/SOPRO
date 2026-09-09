using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sopro.Calculation.Financing;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.Models.Presupuesto;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Puente entre el dominio (EF + programa de obra) y los calculadores puros
    /// <see cref="FinancingCalculator"/> (N7-17c) y
    /// <see cref="FinancingPreparationCalculator"/> (N7-17d).
    ///
    /// Retiene las consultas EF, el filtro/agrupación/orden de conceptos y
    /// distribuciones, el cómputo del total de indirectos oficiales desde el
    /// preview de costos de referencia, la persistencia de filas y la mutación de
    /// la configuración. La preparación numérica CD/CI (acumulación CD con
    /// absorción de residuo, estimación cobrable e importes período a período) y
    /// la reconciliación del total oficial de indirectos viven en el componente
    /// puro; aquí solo hay mapeo, escalares y persistencia.
    /// </summary>
    /// <remarks>
    /// Guardas preservadas exactamente como el legado: sin programa activo, sin
    /// periodos o base no positiva el servicio devuelve 0 SIN eliminar filas previas
    /// ni mutar la configuración. El residuo de CD siempre se absorbe en el último
    /// período con monto y la estimación cobrable proviene únicamente de
    /// <c>ImporteProgramado</c> (el residuo no la contamina). El total de indirectos
    /// oficiales usa la única implementación del preview de referencia del
    /// presupuesto en Application (no se duplica en el paquete).
    /// </remarks>
    public static class FinancingCalculationAdapter
    {
        /// <summary>
        /// Calcula el flujo de financiamiento delegando la aritmética a
        /// <see cref="FinancingCalculator"/> y persiste filas + resultado en la
        /// configuración.
        /// </summary>
        /// <returns>Porcentaje calculado (0 si aplica alguna guarda).</returns>
        public static decimal Calcular(
            SOPROContext context,
            ConfiguracionFinanciamiento config,
            Proyecto proyecto,
            bool modeloDual)
        {
            var programa = context.ProgramasObra
                .AsNoTracking()
                .FirstOrDefault(p => p.ProyectoId == proyecto.Id && p.Activo);

            if (programa == null)
                return 0m;

            var periodos = context.PeriodosPrograma
                .AsNoTracking()
                .Where(p => p.ProgramaObraId == programa.Id)
                .OrderBy(p => p.NumeroPeriodo)
                .ToList();

            if (periodos.Count == 0)
                return 0m;

            var distribuciones = context.DistribucionesPeriodo
                .AsNoTracking()
                .Include(d => d.ActividadProgramada)
                    .ThenInclude(a => a.ConceptoPresupuesto)
                .Where(d => d.PeriodoPrograma.ProgramaObraId == programa.Id)
                .ToList();

            var periodosCalc = BuildPeriodRows(
                proyecto,
                periodos,
                distribuciones,
                proyecto.PorcentajeIndirectosCentral,
                proyecto.PorcentajeIndirectosCampo);
            if (periodosCalc.Count == 0)
                return 0m;

            // Guarda del legado: base no positiva se comprueba ANTES de sumar el
            // presupuesto (misma semántica: no hay trabajo adicional y no se tocan
            // filas previas ni configuración). La suma replica exactamente la del
            // calculador para paridad decimal: CD y CI se acumulan por separado y
            // el total (Acumulable) se obtiene como cdTotal + ciTotal.
            decimal cdTotal = periodosCalc.Sum(p => p.CostoDirecto);
            decimal ciTotal = periodosCalc.Sum(p => p.CostoIndirecto);
            decimal basePrevia = config.BaseCalculo == "SobreCD" ? cdTotal : cdTotal + ciTotal;

            if (basePrevia <= 0m)
                return 0m;

            decimal totalPresupuesto = BudgetPricingService.RoundImporte(proyecto,
                context.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyecto.Id && !c.EsAgrupador && c.MatrizId != null)
                    .AsEnumerable()
                    .Sum(c => c.ImporteTotal));

            var baseModo = config.BaseCalculo == "SobreCD"
                ? FinancingBaseCalculationMode.OverDirectCost
                : FinancingBaseCalculationMode.Accumulative;

            var input = new FinancingInput(
                effectiveAnnualRatePercentage: config.TasaEfectiva,
                tiieAnnualRatePercentage: config.TasaTIIE,
                advancePercentage: config.PorcentajeAnticipo,
                collectionDelayPeriods: config.DesfaseCobro,
                baseCalculationMode: baseModo,
                totalBudgetAmount: totalPresupuesto,
                isDualModel: modeloDual,
                precision: FinancingPrecision.Legacy(proyecto.DecimalesImporte),
                periods: periodosCalc
                    .Select(p => new FinancingPeriodInput(
                        p.NumeroPeriodo,
                        p.Etiqueta,
                        p.FechaInicio,
                        p.FechaFin,
                        p.DiasPeriodo,
                        p.CostoDirecto,
                        p.CostoIndirecto,
                        p.EstimacionTotal))
                    .ToList());

            var result = FinancingCalculator.Calculate(input);

            // Guarda del legado: base no positiva produce resultado vacío; se retorna 0
            // sin eliminar filas previas ni mutar la configuración.
            if (result.Rows.Count == 0)
                return 0m;

            context.FilasFlujoCajaFinanciamiento.RemoveRange(
                context.FilasFlujoCajaFinanciamiento.Where(f => f.ConfiguracionFinanciamientoId == config.Id));

            context.FilasFlujoCajaFinanciamiento.AddRange(
                result.Rows.Select(row => new FilaFlujoCajaFinanciamiento
                {
                    ConfiguracionFinanciamientoId = config.Id,
                    NumeroPeriodo = row.PeriodNumber,
                    Etiqueta = row.Label,
                    FechaInicio = row.StartDate,
                    FechaFin = row.EndDate,
                    Egresos = row.Expenditure,
                    AnticipoRecibido = row.AdvanceReceived,
                    EstimacionCobrada = row.CollectedEstimate,
                    AmortizacionAnticipo = row.AdvanceAmortization,
                    FlujoNeto = row.NetFlow,
                    SaldoAcumulado = row.AccumulatedBalance,
                    DiasPeriodo = row.Days,
                    InteresPeriodo = row.PeriodInterest
                }));

            config.InteresesNegativos = result.NegativeInterest;
            config.InteresesPositivos = result.PositiveInterest;
            config.FinanciamientoNeto = result.NetFinancing;
            config.PorcentajeCalculado = result.Percentage;
            config.FechaCalculo = DateTime.Now;

            context.SaveChanges();
            return result.Percentage;
        }

        private static List<PeriodoFinanciamientoCalc> BuildPeriodRows(
            Proyecto proyecto,
            List<PeriodoPrograma> periodos,
            List<DistribucionPeriodo> distribuciones,
            decimal porcentajeIndirectosCentral,
            decimal porcentajeIndirectosCampo)
        {
            var baseRows = periodos
                .Select(p => new PeriodoFinanciamientoCalc
                {
                    NumeroPeriodo = p.NumeroPeriodo,
                    Etiqueta = p.Etiqueta,
                    FechaInicio = p.FechaInicio,
                    FechaFin = p.FechaFin,
                    DiasPeriodo = Math.Max(1, (p.FechaFin.Date - p.FechaInicio.Date).Days + 1)
                })
                .ToList();

            var periodoIndex = periodos
                .Select((p, i) => (Id: p.Id, Index: i))
                .ToDictionary(x => x.Id, x => x.Index);

            // Solo los periodos base: las filas de desfase de cobro las construye el
            // calculador puro (FinancingCalculator) con el mismo criterio (etiqueta,
            // fechas y días del último período).
            var distribucionesConcepto = distribuciones
                .Where(d => d.ActividadProgramada?.ConceptoPresupuesto != null
                         && !d.ActividadProgramada.ConceptoPresupuesto.EsAgrupador
                         && d.ActividadProgramada.ConceptoPresupuesto.MatrizId.HasValue)
                .ToList();

            var conceptos = distribucionesConcepto
                .GroupBy(d => d.ActividadProgramada!.ConceptoPresupuestoId!.Value)
                .Select(g =>
                {
                    var concepto = g.First().ActividadProgramada!.ConceptoPresupuesto!;
                    var shares = g
                        .Where(d => periodoIndex.ContainsKey(d.PeriodoProgramaId))
                        .OrderBy(d => periodoIndex[d.PeriodoProgramaId])
                        .Select(d => new FinancingConceptDistribution(periodoIndex[d.PeriodoProgramaId], d.CantidadProgramada))
                        .ToList();

                    return new FinancingConceptInput(
                        concepto.Cantidad,
                        concepto.CostoDirectoUnitario,
                        concepto.CostoDirectoTotal,
                        shares);
                })
                .ToList();

            var estimaciones = distribucionesConcepto
                .Where(d => periodoIndex.ContainsKey(d.PeriodoProgramaId))
                .Select(d => new FinancingEstimateLine(periodoIndex[d.PeriodoProgramaId], d.ImporteProgramado))
                .ToList();

            var input = new FinancingPreparationInput(
                proyecto.DecimalesImporte,
                periodos.Count,
                conceptos,
                estimaciones);

            // Preparación numérica CD + estimaciones (residuo absorbido en el último
            // período con monto). El total de indirectos oficiales proviene de la única
            // implementación del preview de referencia del presupuesto en Application.
            var baseSchedule = FinancingPreparationCalculator.PrepareBaseSchedule(input);

            decimal totalCd = baseSchedule.Sum(p => p.DirectCost);
            var preview = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(totalCd, new BudgetPercentageInput
            {
                CostoDirectoReferencia = totalCd,
                IndirectosCentral = porcentajeIndirectosCentral,
                IndirectosCampo = porcentajeIndirectosCampo,
                Financiamiento = 0m,
                Utilidad = 0m,
                CargosAdicionales = 0m,
                ModoCalculoPorcentajes = proyecto.ModoCalculoPorcentajes
            });

            decimal totalCiOficial = BudgetPricingService.RoundImporte(proyecto, preview.Subtotal1 - preview.CostoDirecto);

            var preparado = FinancingPreparationCalculator.ReconcileIndirectCost(
                baseSchedule,
                totalCiOficial,
                proyecto.DecimalesImporte);

            for (int i = 0; i < baseRows.Count; i++)
            {
                baseRows[i].CostoDirecto = preparado[i].DirectCost;
                baseRows[i].CostoIndirecto = preparado[i].IndirectCost;
                baseRows[i].EgresoTotal = preparado[i].Expenditure;
                baseRows[i].EstimacionTotal = preparado[i].EstimatedAmount;
            }

            return baseRows;
        }

        private sealed class PeriodoFinanciamientoCalc
        {
            public int NumeroPeriodo { get; set; }
            public string Etiqueta { get; set; } = string.Empty;
            public DateTime FechaInicio { get; set; }
            public DateTime FechaFin { get; set; }
            public int DiasPeriodo { get; set; }
            public decimal CostoDirecto { get; set; }
            public decimal CostoIndirecto { get; set; }
            public decimal EgresoTotal { get; set; }
            public decimal EstimacionTotal { get; set; }
        }
    }
}