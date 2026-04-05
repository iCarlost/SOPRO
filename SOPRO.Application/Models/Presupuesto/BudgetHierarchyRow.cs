namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetHierarchyRow
    {
        public int RowIndex { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public decimal Importe { get; set; }
        public bool HasContent { get; set; }
        public bool IsConcept { get; set; }
    }
}
