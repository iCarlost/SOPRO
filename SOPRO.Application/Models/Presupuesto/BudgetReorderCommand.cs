using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de reordenar las filas YA persistidas de un presupuesto (ADR-0001).
    /// Lleva el proyecto propietario y la posición final de cada fila, junto con la
    /// base que la vista tenía antes del arrastre. El comando revalida pertenencia e
    /// identidad antes de escribir; los borradores Id=0 no forman parte de esta
    /// intención y conservan su posición solo en la vista.
    /// </summary>
    public sealed class BudgetReorderCommand
    {
        public BudgetReorderCommand(int projectId, IEnumerable<BudgetReorderTarget> targets)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            ArgumentNullException.ThrowIfNull(targets);

            var lista = targets.ToArray();
            if (lista.Length == 0)
                throw new ArgumentException("El reordenamiento requiere al menos una fila persistida.", nameof(targets));

            if (lista.Any(t => t is null))
                throw new ArgumentException("El reordenamiento no admite filas nulas.", nameof(targets));

            if (lista.Select(t => t.ConceptId).Distinct().Count() != lista.Length)
                throw new ArgumentException("El reordenamiento no admite conceptos repetidos.", nameof(targets));

            if (lista.Select(t => t.ProposedOrder).Distinct().Count() != lista.Length)
                throw new ArgumentException("El reordenamiento no admite posiciones finales repetidas.", nameof(targets));

            // La vista contiene todas las filas persistidas del proyecto: las
            // posiciones finales deben ser exactamente 0..n-1, sin huecos, para que
            // el Orden persistido siga siendo contiguo.
            var posiciones = lista.Select(t => t.ProposedOrder).OrderBy(o => o).ToArray();
            for (int i = 0; i < posiciones.Length; i++)
            {
                if (posiciones[i] != i)
                    throw new ArgumentException("El reordenamiento requiere posiciones finales contiguas desde cero.", nameof(targets));
            }

            ProjectId = projectId;
            Targets = lista;
        }

        public int ProjectId { get; }

        /// <summary>Filas reordenadas, con su base esperada y su posición final.</summary>
        public IReadOnlyList<BudgetReorderTarget> Targets { get; }
    }

    /// <summary>
    /// Identidad y base de una fila que la vista propone mover: Id, orden actual, nivel,
    /// condición de agrupador y posición final. Si cualquiera de los valores esperados
    /// cambió en la base, el reordenamiento no representa lo que el usuario arrastró y
    /// el comando se detiene sin escribir.
    /// </summary>
    public sealed class BudgetReorderTarget
    {
        public BudgetReorderTarget(
            int conceptId,
            int expectedOrder,
            int proposedOrder,
            int expectedLevel,
            bool expectedIsAggregator)
        {
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (proposedOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(proposedOrder));

            ConceptId = conceptId;
            ExpectedOrder = expectedOrder;
            ProposedOrder = proposedOrder;
            ExpectedLevel = expectedLevel;
            ExpectedIsAggregator = expectedIsAggregator;
        }

        public int ConceptId { get; }

        public int ExpectedOrder { get; }

        public int ProposedOrder { get; }

        public int ExpectedLevel { get; }

        public bool ExpectedIsAggregator { get; }

        public static BudgetReorderTarget FromConcepto(ConceptoPresupuesto concepto, int proposedOrder)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetReorderTarget(
                concepto.Id,
                concepto.Orden,
                proposedOrder,
                concepto.Nivel,
                concepto.EsAgrupador);
        }
    }
}
