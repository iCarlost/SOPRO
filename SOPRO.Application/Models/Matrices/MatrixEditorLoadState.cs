using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Matrices
{
    public sealed class MatrixEditorLoadState
    {
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public TipoMatriz Tipo { get; set; } = TipoMatriz.APU;
        public List<ComponenteMatriz> Componentes { get; set; } = new();
    }
}
