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

        /// <summary>
        /// [N7-4] Distribución proporcional canónica de un importe base entre los
        /// componentes de una matriz, compartida por Explosión y Programa de Insumos
        /// (antes duplicada tres veces). Para cada componente con importe unitario no
        /// nulo: importe = Round(importeBase × impUnit / cdUnitario); los componentes
        /// sin importe unitario o con resultado cero se excluyen. El residuo
        /// Round(importeBase − Σ asignados) se absorbe en el último componente no
        /// auxiliar (o en el último si todos son auxiliares).
        /// Comportamiento heredado a documentar: la lista puede quedar vacía cuando
        /// todas las asignaciones proporcionales redondean a cero (entonces no hay
        /// absorbedor y Σ devuelto es 0, no importeBase); y la absorción puede anular
        /// justo al absorbedor, dejando una entrada cero que cada consumidor decide si
        /// re-filtrar. Σ == importeBase solo se garantiza con lista no vacía y
        /// absorbedor no anulado.
        /// Requiere cdUnitario != 0; el orden de <paramref name="componentes"/> es
        /// el orden de iteración y de absorción.
        /// </summary>
        public static IReadOnlyList<(ComponenteMatriz Componente, decimal Importe)> DistribuirImporteProporcional(
            SoproCalculationEngine engine,
            IReadOnlyDictionary<ComponenteMatriz, decimal> importesUnitarios,
            IEnumerable<ComponenteMatriz> componentes,
            decimal importeBase,
            decimal cdUnitario)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(importesUnitarios);
            ArgumentNullException.ThrowIfNull(componentes);
            if (cdUnitario == 0m)
                throw new ArgumentException(
                    "El costo directo unitario no puede ser cero al distribuir proporcionalmente.",
                    nameof(cdUnitario));

            var distribComp = new List<(ComponenteMatriz Componente, decimal Importe)>();
            decimal sumaDistribuida = 0m;
            foreach (var comp in componentes)
            {
                if (!importesUnitarios.TryGetValue(comp, out decimal impUnit)) continue;
                if (impUnit == 0m) continue;
                decimal impComp = engine.RoundAmount(importeBase * impUnit / cdUnitario);
                if (impComp == 0m) continue;
                distribComp.Add((comp, impComp));
                sumaDistribuida += impComp;
            }

            decimal residuo = engine.RoundAmount(importeBase - sumaDistribuida);
            if (residuo != 0m && distribComp.Count > 0)
            {
                int idxAjuste = distribComp.FindLastIndex(
                    t => t.Componente.TipoComponente != TipoComponenteMatriz.Auxiliar);
                if (idxAjuste < 0) idxAjuste = distribComp.Count - 1;
                var (compAjuste, impAjuste) = distribComp[idxAjuste];
                distribComp[idxAjuste] = (compAjuste, impAjuste + residuo);
            }

            return distribComp;
        }

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
