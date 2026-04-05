using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Insumos
{
    public sealed class InsumoSearchResultDto
    {
        public string RutaProyecto { get; set; } = string.Empty;
        public string NombreProyecto { get; set; } = string.Empty;
        public int ElementoId { get; set; }
        public TipoComponenteMatriz TipoComponente { get; set; }
        public string? Tag { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public string PrecioMostrado { get; set; } = string.Empty;
        public DateTime? FechaReferencia { get; set; }
        public bool EsActual { get; set; }
        public bool EsFavorito { get; set; }
        public bool EsReciente { get; set; }
        public int FrecuenciaUso { get; set; }
        public decimal Score { get; set; }
    }
}
