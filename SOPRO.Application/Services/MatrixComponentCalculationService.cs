using Sopro.Calculation;
using Sopro.Calculation.Matrices;
using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  MatrixComponentCalculationService — delegador al evaluador puro (N7-1b).║
    // ║  Toda la aritmética y la clasificación de rubros viven en                ║
    // ║  MatrixGraphCalculator (SOPRO.Calculation); esta clase materializa el    ║
    // ║  snapshot de entidades (MatrixGraphSnapshotAdapter), reescribe los       ║
    // ║  importes por componente y traduce los totales al modelo Application.    ║
    // ║  Ya no pasa por la fachada legacy MotorCalculoSopro (N5-2).              ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public static class MatrixComponentCalculationService
    {
        public static MatrixComponentTotals Recalculate(IList<ComponenteMatriz> componentes, int decimalesImporte)
        {
            if (componentes == null) throw new ArgumentNullException(nameof(componentes));

            var engine = new SoproCalculationEngine(decimalesImporte, decimalesImporte, 4);
            var snapshot = MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, componentes);
            var result = new MatrixGraphCalculator(engine).Calculate(snapshot.Graph);

            foreach (var entry in snapshot.ComponentIds)
                entry.Key.Importe = result.ComponentAmounts[entry.Value];

            var root = result.Nodes[snapshot.RootMatrixId];
            return new MatrixComponentTotals
            {
                TotalMaterial = root.TotalMaterial,
                BaseManoObra = root.BaseLabor,
                TotalManoObra = root.TotalLabor,
                TotalMaquinaria = root.TotalMachinery,
                TotalBasicos = root.TotalBasics,
                TotalHerramientas = root.TotalTools,
                TotalManoObraResumen = root.TotalLaborSummary,
                CostoDirectoTotal = root.DirectCostTotal
            };
        }
    }
}
