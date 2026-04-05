using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    public sealed class BudgetConceptAssignmentDraft
    {
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public decimal CostoDirectoUnitario { get; set; }
        public decimal CostoDirectoTotal { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal ImporteTotal { get; set; }
        public int? MatrizId { get; set; }
        public Matriz? Matriz { get; set; }
    }
}
