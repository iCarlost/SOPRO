using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixEditorInitializationService
    {
        public static MatrixEditorLoadState BuildLoadState(Matriz matriz, IEnumerable<ComponenteMatriz>? componentes)
        {
            if (matriz == null) throw new ArgumentNullException(nameof(matriz));

            return new MatrixEditorLoadState
            {
                Clave = matriz.Clave ?? string.Empty,
                Descripcion = matriz.Descripcion ?? string.Empty,
                Unidad = matriz.Unidad ?? string.Empty,
                Tipo = matriz.Tipo,
                Componentes = componentes?.ToList() ?? new List<ComponenteMatriz>()
            };
        }

        public static MatrixTypeUiConfiguration BuildTypeConfiguration(TipoMatriz tipo, string? currentUnit)
        {
            var configuration = new MatrixTypeUiConfiguration
            {
                ShowMaterialButton = tipo != TipoMatriz.Cuadrilla,
                ShowManoObraButton = true,
                ShowMaquinariaButton = tipo != TipoMatriz.Cuadrilla,
                ShowBasicoButton = tipo != TipoMatriz.Cuadrilla,
                ShowHerramientaButton = true,
                SuggestedUnit = string.IsNullOrWhiteSpace(currentUnit) && tipo == TipoMatriz.Cuadrilla
                    ? "jor"
                    : (currentUnit ?? string.Empty)
            };

            return configuration;
        }

        public static TipoMatriz ResolveSelectedType(bool isCuadrillaChecked, bool isApuChecked)
        {
            if (isCuadrillaChecked) return TipoMatriz.Cuadrilla;
            return isApuChecked ? TipoMatriz.APU : TipoMatriz.Basico;
        }
    }
}
