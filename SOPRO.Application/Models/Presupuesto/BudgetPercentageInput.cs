using System;
using Sopro.Calculation;

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
