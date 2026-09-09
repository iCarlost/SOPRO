using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using Sopro.Calculation.Financing;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.Models.Presupuesto;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Puente entre el dominio (EF + programa de obra) y el calculador puro
    /// <see cref="FinancingCalculator"/> (N7-17c).
    ///
    /// Retiene las consultas, la preparación de escalares por período (acumulación de
    /// CD distribuido, reconciliación de indirectos oficiales, la estimación cobrable
    /// desde <c>ImporteProgramado</c>), la persistencia de filas y la mutación de la
    /// configuración. La aritmética del flujo vive en el calculador; aquí solo hay
    /// mapeo y persistencia.
    /// </summary>
    /// <remarks>
    /// Guardas preservadas exactamente como el legado: sin programa activo, sin
    /// periodos o base no positiva el servicio devuelve 0 SIN eliminar filas previas
    /// ni mutar la configuración. La preparación CD/CI (con absorción de residuo en
    /// el último período) sigue viviendo en Application; su posible centralización se
    /// evalúa por separado.
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

            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            var distribuciones = context.DistribucionesPeriodo
                .AsNoTracking()
                .Include(d => d.ActividadProgramada)
                    .ThenInclude(a => a.ConceptoPresupuesto)
                .Where(d => d.PeriodoPrograma.ProgramaObraId == programa.Id)
                .ToList();

            // Solo los periodos base: las filas de desfase de cobro las construye el
            // calculador puro con el mismo criterio (etiqueta, fechas y días del último período).
            var periodosCalc = BuildPeriodRows(
                proyecto,
                periodos,
                distribuciones,
                proyecto.PorcentajeIndirectosCentral,
                proyecto.PorcentajeIndirectosCampo,
                desfaseCobro: 0,
                engine);
            if (periodosCalc.Count == 0)
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
            decimal porcentajeIndirectosCampo,
            int desfaseCobro,
            SoproCalculationEngine engine)
        {
            var periodOrder = periodos.ToDictionary(p => p.Id, p => p.NumeroPeriodo);
            var acumulados = periodos.ToDictionary(
                p => p.Id,
                p => new PeriodoFinanciamientoCalc
                {
                    NumeroPeriodo = p.NumeroPeriodo,
                    Etiqueta = p.Etiqueta,
                    FechaInicio = p.FechaInicio,
                    FechaFin = p.FechaFin,
                    DiasPeriodo = Math.Max(1, (p.FechaFin.Date - p.FechaInicio.Date).Days + 1),
                    CostoDirecto = 0m,
                    CostoIndirecto = 0m,
                    EgresoTotal = 0m,
                    EstimacionTotal = 0m
                });

            var gruposConcepto = distribuciones
                .Where(d => d.ActividadProgramada?.ConceptoPresupuesto != null
                         && !d.ActividadProgramada.ConceptoPresupuesto.EsAgrupador
                         && d.ActividadProgramada.ConceptoPresupuesto.MatrizId.HasValue)
                .GroupBy(d => d.ActividadProgramada!.ConceptoPresupuestoId!.Value);

            foreach (var grupo in gruposConcepto)
            {
                var concepto = grupo.First().ActividadProgramada!.ConceptoPresupuesto!;
                decimal cantidadConcepto = concepto.Cantidad;
                if (cantidadConcepto <= 0m)
                    continue;

                decimal cdUnit = concepto.CostoDirectoUnitario;
                if (cdUnit <= 0m && concepto.CostoDirectoTotal > 0m)
                    cdUnit = concepto.CostoDirectoTotal / cantidadConcepto;

                decimal totalCdConcepto = engine.Multiply(cantidadConcepto, cdUnit);

                var distribucionesConcepto = grupo
                    .OrderBy(d => periodOrder.TryGetValue(d.PeriodoProgramaId, out var orden) ? orden : int.MaxValue)
                    .ToList();

                AplicarAcumuladoPorDistribucion(proyecto, distribucionesConcepto, cdUnit, totalCdConcepto, acumulados, true, engine);
            }

            foreach (var distribucion in distribuciones.Where(d => d.ActividadProgramada?.ConceptoPresupuesto != null
                                                                && !d.ActividadProgramada.ConceptoPresupuesto.EsAgrupador
                                                                && d.ActividadProgramada.ConceptoPresupuesto.MatrizId.HasValue))
            {
                if (acumulados.TryGetValue(distribucion.PeriodoProgramaId, out var rowEstim))
                    rowEstim.EstimacionTotal = BudgetPricingService.RoundImporte(proyecto, rowEstim.EstimacionTotal + distribucion.ImporteProgramado);
            }

            var rows = periodos
                .Select(p =>
                {
                    var row = acumulados[p.Id];
                    row.CostoDirecto = BudgetPricingService.RoundImporte(proyecto, row.CostoDirecto);
                    row.CostoIndirecto = 0m;
                    row.EgresoTotal = row.CostoDirecto;
                    return row;
                })
                .ToList();

            // Reconciliar indirectos oficiales del proyecto y fijar egreso = CD + CI
            ReconciliarIndirectosYEgresos(proyecto, rows);

            if (rows.Count > 0 && desfaseCobro > 0)
            {
                var ultimo = rows[^1];
                int diasBase = Math.Max(1, ultimo.DiasPeriodo);
                for (int extra = 1; extra <= desfaseCobro; extra++)
                {
                    DateTime inicio = ultimo.FechaFin.Date.AddDays(1 + diasBase * (extra - 1));
                    DateTime fin = inicio.AddDays(diasBase - 1);
                    rows.Add(new PeriodoFinanciamientoCalc
                    {
                        NumeroPeriodo = ultimo.NumeroPeriodo + extra,
                        Etiqueta = $"Período de desfase {extra}",
                        FechaInicio = inicio,
                        FechaFin = fin,
                        DiasPeriodo = diasBase,
                        CostoDirecto = 0m,
                        CostoIndirecto = 0m,
                        EgresoTotal = 0m,
                        EstimacionTotal = 0m
                    });
                }
            }

            return rows;
        }

        private static void ReconciliarIndirectosYEgresos(Proyecto proyecto, List<PeriodoFinanciamientoCalc> rows)
        {
            if (rows.Count == 0)
                return;

            decimal totalCd = rows.Sum(x => x.CostoDirecto);
            var preview = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(totalCd, new BudgetPercentageInput
            {
                CostoDirectoReferencia = totalCd,
                IndirectosCentral = proyecto.PorcentajeIndirectosCentral,
                IndirectosCampo = proyecto.PorcentajeIndirectosCampo,
                Financiamiento = 0m,
                Utilidad = 0m,
                CargosAdicionales = 0m,
                ModoCalculoPorcentajes = proyecto.ModoCalculoPorcentajes
            });

            decimal totalCiOficial = BudgetPricingService.RoundImporte(proyecto, preview.Subtotal1 - preview.CostoDirecto);
            ReconciliarDistribucion(rows, proyecto, totalCiOficial, esCostoDirecto: false);

            foreach (var row in rows)
            {
                row.CostoDirecto = BudgetPricingService.RoundImporte(proyecto, row.CostoDirecto);
                row.CostoIndirecto = BudgetPricingService.RoundImporte(proyecto, row.CostoIndirecto);
                row.EgresoTotal = BudgetPricingService.RoundImporte(proyecto, row.CostoDirecto + row.CostoIndirecto);
                row.EstimacionTotal = BudgetPricingService.RoundImporte(proyecto, row.EstimacionTotal);
            }
        }

        private static void ReconciliarDistribucion(List<PeriodoFinanciamientoCalc> rows, Proyecto proyecto, decimal totalOficial, bool esCostoDirecto)
        {
            decimal totalActual = rows.Sum(x => esCostoDirecto ? x.CostoDirecto : x.CostoIndirecto);
            if (totalActual == totalOficial)
                return;

            var filasConMonto = rows
                .Where(x => (esCostoDirecto ? x.CostoDirecto : x.CostoIndirecto) != 0m)
                .ToList();

            if (filasConMonto.Count == 0)
            {
                filasConMonto = rows.ToList();
                if (filasConMonto.Count == 0)
                    return;
            }

            if (totalActual == 0m)
            {
                decimal totalBase = rows.Sum(x => x.CostoDirecto);
                if (totalBase > 0m)
                {
                    decimal acumulado = 0m;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var fila = rows[i];
                        decimal nuevo = i == rows.Count - 1
                            ? totalOficial - acumulado
                            : BudgetPricingService.RoundImporte(proyecto, totalOficial * fila.CostoDirecto / totalBase);
                        if (esCostoDirecto)
                            fila.CostoDirecto = nuevo;
                        else
                            fila.CostoIndirecto = nuevo;
                        acumulado += nuevo;
                    }
                }
                else
                {
                    var ultima = filasConMonto[^1];
                    if (esCostoDirecto)
                        ultima.CostoDirecto = totalOficial;
                    else
                        ultima.CostoIndirecto = totalOficial;
                }
                return;
            }

            decimal acumuladoDistrib = 0m;
            for (int i = 0; i < filasConMonto.Count; i++)
            {
                var fila = filasConMonto[i];
                decimal actual = esCostoDirecto ? fila.CostoDirecto : fila.CostoIndirecto;
                decimal nuevo = i == filasConMonto.Count - 1
                    ? totalOficial - acumuladoDistrib
                    : BudgetPricingService.RoundImporte(proyecto, totalOficial * actual / totalActual);
                if (esCostoDirecto)
                    fila.CostoDirecto = nuevo;
                else
                    fila.CostoIndirecto = nuevo;
                acumuladoDistrib += nuevo;
            }
        }

        private static void AplicarAcumuladoPorDistribucion(
            Proyecto proyecto,
            List<DistribucionPeriodo> distribucionesConcepto,
            decimal precioUnitario,
            decimal totalEsperado,
            Dictionary<int, PeriodoFinanciamientoCalc> acumulados,
            bool esCostoDirecto,
            SoproCalculationEngine engine)
        {
            if (distribucionesConcepto.Count == 0 || totalEsperado == 0m)
                return;

            decimal suma = 0m;
            int ultimoIndiceConMonto = -1;
            var importes = new decimal[distribucionesConcepto.Count];

            for (int i = 0; i < distribucionesConcepto.Count; i++)
            {
                var distribucion = distribucionesConcepto[i];
                decimal importe = engine.Multiply(distribucion.CantidadProgramada, precioUnitario);
                importes[i] = importe;
                suma += importe;
                if (distribucion.CantidadProgramada != 0m || importe != 0m)
                    ultimoIndiceConMonto = i;
            }

            if (ultimoIndiceConMonto < 0)
                ultimoIndiceConMonto = distribucionesConcepto.Count - 1;

            importes[ultimoIndiceConMonto] += totalEsperado - suma;

            for (int i = 0; i < distribucionesConcepto.Count; i++)
            {
                var distribucion = distribucionesConcepto[i];
                if (!acumulados.TryGetValue(distribucion.PeriodoProgramaId, out var row))
                    continue;

                if (esCostoDirecto)
                    row.CostoDirecto += importes[i];
                else
                    row.CostoIndirecto += importes[i];
            }
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