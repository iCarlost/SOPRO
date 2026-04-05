using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetGridRowDisplay
    {
        public ConceptoPresupuesto Concepto { get; set; } = null!;
        public Dictionary<string, object?> ValuesByInternalName { get; set; } = new();
    }
}
