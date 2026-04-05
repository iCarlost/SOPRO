namespace SOPRO.Application.Models.Matrices
{
    public class MatrixComponentRowDisplayModel
    {
        public string Tipo { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Importe { get; set; }
    }
}
