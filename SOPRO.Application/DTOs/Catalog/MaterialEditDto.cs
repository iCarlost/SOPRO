namespace SOPRO.Application.DTOs.Catalog
{
    public sealed class MaterialEditDto
    {
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public string Notas { get; set; } = string.Empty;
        public bool GuardarEnMaestro { get; set; }
        public int? ProyectoId { get; set; }
    }
}
