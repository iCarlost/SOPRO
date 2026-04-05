namespace SOPRO.Application.Models.Presupuesto
{
    public sealed class BudgetQuantityChangeResult
    {
        public bool HasChanges { get; set; }
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Importe { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string PrecioUnitarioLetra { get; set; } = string.Empty;
        public string TotalLetra { get; set; } = string.Empty;
    }
}
