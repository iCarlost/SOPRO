using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Catalogs
{
    public sealed class MatrixCatalogFilterInput
    {
        public int ProyectoId { get; set; }
        public string? SearchText { get; set; }
        public TipoMatriz? Tipo { get; set; }
    }
}
