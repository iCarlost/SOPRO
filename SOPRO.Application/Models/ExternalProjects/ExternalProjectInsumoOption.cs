using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.ExternalProjects
{
    public sealed class ExternalProjectInsumoOption
    {
        public int ItemId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public TipoComponenteMatriz TipoComponente { get; set; }
        public string? Tag { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public string PrecioMostrado { get; set; } = string.Empty;
    }
}
