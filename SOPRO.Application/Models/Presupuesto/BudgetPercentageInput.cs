using System;
using Sopro.Calculation;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Legacy percentage input from Budget/Preview services.
    /// </summary>
    public class BudgetPercentageInput
    {
        public decimal CostoDirectoReferencia { get; set; }
        public decimal IndirectosCentral { get; set; }
        public decimal IndirectosCampo { get; set; }
        public decimal Financiamiento { get; set; }
        public decimal Utilidad { get; set; }
        public decimal CargosAdicionales { get; set; }
        public string ModoCalculoPorcentajes { get; set; } = "Acumulables";

        /// <summary>
        /// [N7-7] Single construction point from Proyecto, replacing the three
        /// equivalent inline builders (BudgetPricingService, RecalculoGlobalService,
        /// BudgetLoadService). Preserves the legacy `?? "Acumulables"` default.
        /// </summary>
        public static BudgetPercentageInput FromProyecto(Proyecto proyecto)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            return new BudgetPercentageInput
            {
                IndirectosCentral      = proyecto.PorcentajeIndirectosCentral,
                IndirectosCampo        = proyecto.PorcentajeIndirectosCampo,
                Financiamiento         = proyecto.PorcentajeFinanciamiento,
                Utilidad               = proyecto.PorcentajeUtilidad,
                CargosAdicionales      = proyecto.PorcentajeCargosAdicionales,
                ModoCalculoPorcentajes = proyecto.ModoCalculoPorcentajes ?? "Acumulables"
            };
        }

        /// <summary>
        /// Maps the seven legacy budget percentage fields to the canonical package
        /// type <see cref="PricePercentageInput"/>. SobreCD comparison is
        /// case-insensitive as in the original legacy logic.
        /// </summary>
        public PricePercentageInput ToPricePercentage() => new()
        {
            ReferenceDirectCost         = CostoDirectoReferencia,
            CentralIndirectsPercentage  = IndirectosCentral,
            FieldIndirectsPercentage    = IndirectosCampo,
            FinancingPercentage         = Financiamiento,
            ProfitPercentage            = Utilidad,
            AdditionalChargesPercentage = CargosAdicionales,
            Mode = string.Equals(ModoCalculoPorcentajes, "SobreCD",
                                 StringComparison.OrdinalIgnoreCase)
                ? PercentageCalculationMode.OverDirectCost
                : PercentageCalculationMode.Accumulative
        };
    }
}
