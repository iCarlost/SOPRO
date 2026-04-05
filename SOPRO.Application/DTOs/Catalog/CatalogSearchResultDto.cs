using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Catalog
{
    public sealed class CatalogSearchResultDto
    {
        public string TipoElemento { get; set; } = string.Empty;
        public string RutaProyecto { get; set; } = string.Empty;
        public string NombreProyecto { get; set; } = string.Empty;
        public int ElementoId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal PrecioOCosto { get; set; }
        public DateTime? FechaReferencia { get; set; }
        public bool EsActual { get; set; }
        public bool EsFavorito { get; set; }
        public bool EsReciente { get; set; }
        public int FrecuenciaUso { get; set; }
        public decimal Score { get; set; }
        public TipoMatriz TipoMatriz { get; set; }
    }
}
