using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Presupuesto
{
    public class BudgetConceptRowDto
    {
        public int? ExistingConceptId { get; set; }
        public string Tipo { get; set; } = "Concepto";
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public int? MatrizId { get; set; }
        public decimal CostoDirectoUnitario { get; set; }
        public decimal CostoDirectoTotal { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal ImporteTotal { get; set; }
        public int Orden { get; set; }

        public bool HasContent => !string.IsNullOrWhiteSpace(Descripcion);
        public bool EsAgrupador => !string.Equals(Tipo, "Concepto", StringComparison.OrdinalIgnoreCase);
    }
}
