namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetHeaderInfo
    {
        public string ProyectoNombre { get; set; } = string.Empty;
        public string UbicacionTexto { get; set; } = string.Empty;
        public string PorcentajesTexto { get; set; } = string.Empty;
        public decimal FactorPrecioUnitario { get; set; }
        public bool TienePorcentajesActivos { get; set; }
    }
}
