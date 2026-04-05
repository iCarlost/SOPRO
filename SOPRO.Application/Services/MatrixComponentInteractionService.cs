using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixComponentInteractionService
    {
        public static bool RequiresRendimientoDialog(ComponenteMatriz component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            if (component.TipoComponente == TipoComponenteMatriz.ManoDeObra && component.ManoDeObra?.EsPorcentajeMO == true)
            {
                return false;
            }

            return component.TipoComponente == TipoComponenteMatriz.ManoDeObra
                || component.TipoComponente == TipoComponenteMatriz.Maquinaria
                || IsCuadrilla(component);
        }

        public static bool CanEditQuantityInline(ComponenteMatriz component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            return !RequiresRendimientoDialog(component);
        }

        public static bool CanEditUnitPriceInline(ComponenteMatriz component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            return component.TipoComponente == TipoComponenteMatriz.Material;
        }

        public static string GetRendimientoDialogName(ComponenteMatriz component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            if (component.TipoComponente == TipoComponenteMatriz.ManoDeObra)
                return component.ManoDeObra?.Descripcion ?? "M.O.";

            if (component.TipoComponente == TipoComponenteMatriz.Maquinaria)
                return component.Maquinaria?.Descripcion ?? "Maquinaria";

            if (IsCuadrilla(component))
                return component.Auxiliar?.Descripcion ?? "Cuadrilla";

            return string.Empty;
        }

        public static bool IsCuadrilla(ComponenteMatriz component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            return component.TipoComponente == TipoComponenteMatriz.Auxiliar && component.Auxiliar?.Tipo == TipoMatriz.Cuadrilla;
        }
    }
}
