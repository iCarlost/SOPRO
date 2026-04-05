using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    public sealed class BudgetConceptKeyRowSnapshot
    {
        public int RowIndex { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConceptoPresupuesto? Concept { get; set; }
    }
}
