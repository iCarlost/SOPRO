using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Matrices
{
    public class MatrixEditDto
    {
        public int ProyectoId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public TipoMatriz Tipo { get; set; }
        public decimal CostoDirecto { get; set; }
        public List<MatrixComponentEditDto> Componentes { get; set; } = new();
    }
}
