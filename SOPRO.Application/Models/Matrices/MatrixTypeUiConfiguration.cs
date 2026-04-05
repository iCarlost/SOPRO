namespace SOPRO.Application.Models.Matrices
{
    public sealed class MatrixTypeUiConfiguration
    {
        public bool ShowMaterialButton { get; set; }
        public bool ShowManoObraButton { get; set; }
        public bool ShowMaquinariaButton { get; set; }
        public bool ShowBasicoButton { get; set; }
        public bool ShowHerramientaButton { get; set; }
        public string SuggestedUnit { get; set; } = string.Empty;
    }
}
