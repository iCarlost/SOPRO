using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    public sealed class BudgetRowTypeChangeInput
    {
        public int RowIndex { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public ConceptoPresupuesto? ExistingConcept { get; set; }
    }
}
