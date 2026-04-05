namespace SOPRO.Application.DTOs.Catalog
{
    public sealed class ManoDeObraEditDto
    {
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal SalarioBase { get; set; }
        public decimal FactorSalarioReal { get; set; }
        public decimal SalarioReal { get; set; }
        public string Notas { get; set; } = string.Empty;
        public bool GuardarEnMaestro { get; set; }
        public int? ProyectoId { get; set; }
    }
}
