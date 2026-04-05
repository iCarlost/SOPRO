namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetPercentagePreviewResult
    {
        public decimal CostoDirecto { get; set; }
        public decimal MontoIndirectosCentral { get; set; }
        public decimal MontoIndirectosCampo { get; set; }
        public decimal Subtotal1 { get; set; }
        public decimal MontoFinanciamiento { get; set; }
        public decimal Subtotal2 { get; set; }
        public decimal MontoUtilidad { get; set; }
        public decimal Subtotal3 { get; set; }
        public decimal MontoCargosAdicionales { get; set; }
        public decimal PrecioUnitarioFinal { get; set; }
        public bool CalculadoDesdeConceptos { get; set; }
        public int ConceptosProcesados { get; set; }
    }
}
