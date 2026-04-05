using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Matrices
{
    public class MatrixComponentEditDto
    {
        public TipoComponenteMatriz TipoComponente { get; set; }
        public int? MaterialId { get; set; }
        public int? ManoDeObraId { get; set; }
        public int? MaquinariaId { get; set; }
        public int? AuxiliarId { get; set; }
        public int? HerramientaId { get; set; }
        public decimal Cantidad { get; set; }
        public decimal Importe { get; set; }
        public int Orden { get; set; }
        public string Notas { get; set; } = string.Empty;
    }
}
