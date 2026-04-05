using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.ExternalProjects
{
    public sealed class ExternalProjectMatrixOption
    {
        public int MatrixId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal CostoDirecto { get; set; }
        public TipoMatriz Tipo { get; set; }
    }
}
