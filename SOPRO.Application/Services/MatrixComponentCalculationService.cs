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
        /// <summary>
        /// [N7-3] Importes unitarios por componente calculados por el evaluador canónico
        /// del grafo (misma ruta que <see cref="Recalculate"/>), sin mutar entidades.
        /// Explosión y Programa de Insumos consumen este método en lugar de duplicar
        /// el cálculo: cada auxiliar entra como hoja con su costo directo almacenado
        /// (el postorden del propagador/importador ya garantizó que ese costo es el
        /// definitivo) y los `%MO` se resuelven sobre la base de mano de obra.
        /// Los componentes con navegación incompleta de su tipo quedan fuera del mapa
        /// (comportamiento heredado de explosión/programación, que no los acumulaba);
        /// a diferencia de <see cref="Recalculate"/>, que lanza.
        /// </summary>
        public static Dictionary<ComponenteMatriz, decimal> ImportesUnitarios(
            IEnumerable<ComponenteMatriz> componentes,
            int decimalesImporte)
        {
            if (componentes == null) throw new ArgumentNullException(nameof(componentes));

            var evaluables = componentes.Where(TieneNavegacionCompleta).ToList();
            if (evaluables.Count == 0) return new Dictionary<ComponenteMatriz, decimal>();

            var engine = new SoproCalculationEngine(decimalesImporte, decimalesImporte, 4);
            var snapshot = MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, evaluables);
            var result = new MatrixGraphCalculator(engine).Calculate(snapshot.Graph);

            return snapshot.ComponentIds.ToDictionary(
                entry => entry.Key,
                entry => result.ComponentAmounts[entry.Value]);
        }

        private static bool TieneNavegacionCompleta(ComponenteMatriz comp)
            => comp.TipoComponente switch
            {
                TipoComponenteMatriz.Material => comp.Material != null,
                TipoComponenteMatriz.ManoDeObra => comp.ManoDeObra != null,
                TipoComponenteMatriz.Maquinaria => comp.Maquinaria != null,
                TipoComponenteMatriz.Herramienta => comp.Herramienta != null,
                TipoComponenteMatriz.Auxiliar => comp.Auxiliar != null,
                _ => false
            };

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
