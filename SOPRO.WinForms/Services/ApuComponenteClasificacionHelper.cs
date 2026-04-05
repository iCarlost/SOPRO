using SOPRO.Core.Entities;

namespace SOPRO.WinForms.Services
{
    internal static class ApuComponenteClasificacionHelper
    {
        internal static bool EsCuadrilla(ComponenteMatriz comp)
            => comp.TipoComponente == TipoComponenteMatriz.Auxiliar
               && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla;

        internal static TipoComponenteMatriz ObtenerTipoSeccion(ComponenteMatriz comp)
            => EsCuadrilla(comp) ? TipoComponenteMatriz.ManoDeObra : comp.TipoComponente;

        internal static (string clave, string desc, string unidad, decimal pu) ObtenerDatosInsumo(ComponenteMatriz comp)
        {
            if (EsCuadrilla(comp))
            {
                return (
                    comp.Auxiliar?.Clave ?? string.Empty,
                    comp.Auxiliar?.Descripcion ?? string.Empty,
                    comp.Auxiliar?.Unidad ?? string.Empty,
                    comp.Auxiliar?.CostoDirecto ?? 0m);
            }

            return comp.TipoComponente switch
            {
                TipoComponenteMatriz.Material => (
                    comp.Material?.Clave ?? string.Empty,
                    comp.Material?.Descripcion ?? string.Empty,
                    comp.Material?.Unidad ?? string.Empty,
                    comp.Material?.PrecioUnitario ?? 0m),
                TipoComponenteMatriz.ManoDeObra => (
                    comp.ManoDeObra?.Clave ?? string.Empty,
                    comp.ManoDeObra?.Descripcion ?? string.Empty,
                    "JOR",
                    comp.ManoDeObra?.SalarioReal ?? 0m),
                TipoComponenteMatriz.Maquinaria => (
                    comp.Maquinaria?.Clave ?? string.Empty,
                    comp.Maquinaria?.Descripcion ?? string.Empty,
                    "HR",
                    comp.Maquinaria?.CostoHorario ?? 0m),
                TipoComponenteMatriz.Herramienta => (
                    comp.Herramienta?.Clave ?? string.Empty,
                    comp.Herramienta?.Descripcion ?? string.Empty,
                    "%",
                    comp.Herramienta?.PrecioUnitario ?? 0m),
                TipoComponenteMatriz.Auxiliar => (
                    comp.Auxiliar?.Clave ?? string.Empty,
                    comp.Auxiliar?.Descripcion ?? string.Empty,
                    comp.Auxiliar?.Unidad ?? string.Empty,
                    comp.Auxiliar?.CostoDirecto ?? 0m),
                _ => (string.Empty, string.Empty, string.Empty, 0m)
            };
        }
    }
}
