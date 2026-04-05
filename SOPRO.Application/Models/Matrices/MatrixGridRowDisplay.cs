using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Matrices
{
    public sealed class MatrixGridRowDisplay
    {
        public int MatrizId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public string TipoTexto { get; set; } = string.Empty;
        public decimal CostoDirecto { get; set; }
        public int NumInsumos { get; set; }
        public string OrigenDetalle { get; set; } = string.Empty;
        public TipoMatriz Tipo { get; set; }
        public Matriz Source { get; set; }
    }
}
