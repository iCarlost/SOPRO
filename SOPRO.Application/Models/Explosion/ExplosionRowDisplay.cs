namespace SOPRO.Application.Models.Explosion
{
    public sealed class ExplosionRowDisplay
    {
        public ExplosionRowKind Kind { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public string CantidadTexto { get; set; } = string.Empty;
        public string PrecioUnitarioTexto { get; set; } = string.Empty;
        public string ImporteTexto { get; set; } = string.Empty;
        public decimal? Porcentaje { get; set; }
    }
}
