using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    public sealed class BudgetRowTypeChangeResult
    {
        public bool Handled { get; set; }
        public bool IgnoredBecauseStateIsAlreadyCorrect { get; set; }
        public bool IsAggregator { get; set; }
        public int Level { get; set; }
        public bool RequiresRowInvalidate { get; set; }
        public bool RequiresReassignSequence { get; set; }
        public bool QuantityReadOnly { get; set; }
        public bool UnitReadOnly { get; set; }
        public bool ClearCalculatedCells { get; set; }
        public string UnitValue { get; set; } = string.Empty;
        public string QuantityValue { get; set; } = string.Empty;
        public string UnitPriceValue { get; set; } = string.Empty;
        public string AmountValue { get; set; } = string.Empty;
        public ConceptoPresupuesto? Concept { get; set; }
    }
}
