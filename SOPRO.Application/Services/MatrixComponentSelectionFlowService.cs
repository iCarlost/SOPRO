using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixComponentSelectionFlowService
    {
        public static MatrixComponentSelectionResult AddSelectedComponents(
            IList<ComponenteMatriz> target,
            IEnumerable<ComponenteMatriz>? selectedComponents)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var components = selectedComponents?.Where(c => c != null).ToList() ?? new List<ComponenteMatriz>();
            if (components.Count == 0)
            {
                return new MatrixComponentSelectionResult
                {
                    HasComponents = false,
                    AddedCount = 0
                };
            }

            MatrixComponentCollectionService.AddRange(target, components);

            return new MatrixComponentSelectionResult
            {
                HasComponents = true,
                AddedCount = components.Count
            };
        }
    }
}
