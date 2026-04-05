namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetPercentageInput
    {
        public decimal CostoDirectoReferencia { get; set; }
        public decimal IndirectosCentral { get; set; }
        public decimal IndirectosCampo { get; set; }
        public decimal Financiamiento { get; set; }
        public decimal Utilidad { get; set; }
        public decimal CargosAdicionales { get; set; }
        public string ModoCalculoPorcentajes { get; set; } = "Acumulables";
    }
}
