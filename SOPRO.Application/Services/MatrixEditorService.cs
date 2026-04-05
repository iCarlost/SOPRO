using SOPRO.Application.DTOs.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixEditorService
    {
        public static bool HasComponents(IReadOnlyCollection<ComponenteMatriz> components)
        {
            if (components == null) throw new ArgumentNullException(nameof(components));
            return components.Count > 0;
        }

        public static MatrixEditDto BuildEditDto(
            int proyectoId,
            string clave,
            string descripcion,
            string unidad,
            bool esCuadrilla,
            bool esApu,
            decimal costoDirectoTotal,
            IEnumerable<ComponenteMatriz> components)
        {
            if (components == null) throw new ArgumentNullException(nameof(components));

            return new MatrixEditDto
            {
                ProyectoId = proyectoId,
                Clave = (clave ?? string.Empty).Trim(),
                Descripcion = (descripcion ?? string.Empty).Trim(),
                Unidad = (unidad ?? string.Empty).Trim(),
                Tipo = esCuadrilla ? TipoMatriz.Cuadrilla : (esApu ? TipoMatriz.APU : TipoMatriz.Basico),
                CostoDirecto = costoDirectoTotal,
                Componentes = MatrixComponentCollectionService.ToEditDtos(components)
            };
        }
    }
}
