using SOPRO.Application.DTOs.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixComponentCollectionService
    {
        public static void LoadInto(IList<ComponenteMatriz> target, IEnumerable<ComponenteMatriz> source)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (source == null) return;

            target.Clear();
            foreach (var component in source)
            {
                target.Add(component);
            }
        }

        public static void AddRange(IList<ComponenteMatriz> target, IEnumerable<ComponenteMatriz> components)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (components == null) return;

            foreach (var component in components)
            {
                target.Add(component);
            }
        }

        public static bool RemoveAt(IList<ComponenteMatriz> target, int index)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (index < 0 || index >= target.Count) return false;

            target.RemoveAt(index);
            return true;
        }

        public static bool TryGetAt(IReadOnlyList<ComponenteMatriz> source, int index, out ComponenteMatriz? component)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (index < 0 || index >= source.Count)
            {
                component = null;
                return false;
            }

            component = source[index];
            return component != null;
        }

        public static List<MatrixComponentEditDto> ToEditDtos(IEnumerable<ComponenteMatriz> components)
        {
            if (components == null) throw new ArgumentNullException(nameof(components));

            return components
                .Select((comp, index) => new MatrixComponentEditDto
                {
                    TipoComponente = comp.TipoComponente,
                    MaterialId = comp.MaterialId,
                    ManoDeObraId = comp.ManoDeObraId,
                    MaquinariaId = comp.MaquinariaId,
                    AuxiliarId = comp.AuxiliarId,
                    HerramientaId = comp.HerramientaId,
                    Cantidad = comp.Cantidad,
                    Importe = comp.Importe,
                    Orden = index + 1,
                    Notas = comp.Notas ?? string.Empty
                })
                .ToList();
        }
    }
}
