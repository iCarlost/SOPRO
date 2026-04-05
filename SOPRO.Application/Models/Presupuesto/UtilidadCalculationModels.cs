namespace SOPRO.Application.Models.Presupuesto
{
    public class UtilidadCalculationInput
    {
        public decimal CostoDirectoReferencia { get; set; }
        public decimal IndirectosCentral { get; set; }
        public decimal IndirectosCampo { get; set; }
        public decimal Financiamiento { get; set; }
        public decimal UtilidadDirecta { get; set; }
        public decimal UtilidadNetaDeseada { get; set; }
        public decimal Isr { get; set; }
        public decimal Ptu { get; set; }
        public string ModoCalculoPorcentajes { get; set; } = "Acumulables";
        public bool ModoAsistido { get; set; }
    }

    public class UtilidadCalculationResult
    {
        public decimal BaseUtilidad { get; set; }
        public decimal PorcentajeUtilidadBruta { get; set; }
        public decimal PorcentajeUtilidadNeta { get; set; }
        public decimal Isr { get; set; }
        public decimal Ptu { get; set; }
        public decimal ImporteUtilidad { get; set; }
        public decimal ImporteIsr { get; set; }
        public decimal ImportePtu { get; set; }
        public decimal UtilidadNetaEstimada { get; set; }
        public string Modo { get; set; } = string.Empty;
    }
}
