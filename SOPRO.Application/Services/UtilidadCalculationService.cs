using Sopro.Calculation;
using Sopro.Calculation.Pricing;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Utility calculation service. Delegates core math to
    /// <see cref="UtilityCalculator"/> (N7-14b, motor puro).
    /// </summary>
    public class UtilidadCalculationService
    {
        public UtilidadCalculationResult Calcular(
            SOPROContext context, Proyecto proyecto, UtilidadCalculationInput input)
        {
            if (context == null)  throw new ArgumentNullException(nameof(context));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (input == null)    throw new ArgumentNullException(nameof(input));

            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            var preview = BudgetPreviewCalculationService.BuildPreview(context, proyecto,
                new BudgetPercentageInput
                {
                    CostoDirectoReferencia = input.CostoDirectoReferencia,
                    IndirectosCentral      = input.IndirectosCentral,
                    IndirectosCampo        = input.IndirectosCampo,
                    Financiamiento         = input.Financiamiento,
                    Utilidad               = 0m,
                    CargosAdicionales      = 0m,
                    ModoCalculoPorcentajes = input.ModoCalculoPorcentajes
                });

            decimal baseUtilidad = engine.RoundAmount(preview.Subtotal2);

            var pure = UtilityCalculator.Calculate(new UtilityInput
            {
                BaseUtilidad = baseUtilidad,
                GrossUtilidadPercentage = input.UtilidadDirecta,
                NetUtilidadPercentage = input.UtilidadNetaDeseada,
                IsrPercentage = input.Isr,
                PtuPercentage = input.Ptu,
                IsAssistedMode = input.ModoAsistido,
                AmountDecimals = proyecto.DecimalesImporte
            });

            return new UtilidadCalculationResult
            {
                BaseUtilidad            = baseUtilidad,
                PorcentajeUtilidadBruta = pure.GrossUtilidadPercentage,
                PorcentajeUtilidadNeta  = pure.NetUtilidadPercentage,
                Isr                     = input.Isr,
                Ptu                     = input.Ptu,
                ImporteUtilidad         = pure.ImporteUtilidad,
                ImporteIsr              = pure.ImporteIsr,
                ImportePtu              = pure.ImportePtu,
                UtilidadNetaEstimada    = pure.UtilidadNetaEstimada,
                Modo                    = input.ModoAsistido ? "Asistido" : "Directo"
            };
        }
    }
}
