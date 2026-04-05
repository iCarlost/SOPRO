using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Matrices
{
    public class MatrixListItemDto
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal CostoDirecto { get; set; }
        public TipoMatriz Tipo { get; set; }
    }
}
