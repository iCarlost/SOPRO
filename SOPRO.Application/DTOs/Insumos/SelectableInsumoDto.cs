using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Insumos
{
    public class SelectableInsumoDto
    {
        public int Id { get; set; }
        public TipoComponenteMatriz TipoComponente { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public string PrecioMostrado { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }
}
