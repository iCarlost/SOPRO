using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Selector
{
    public sealed class ProjectInsumoIndexEntry
    {
        public int ItemId { get; set; }
        public TipoComponenteMatriz TipoComponente { get; set; }
        public string? Tag { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public string PrecioMostrado { get; set; } = string.Empty;
    }
}
