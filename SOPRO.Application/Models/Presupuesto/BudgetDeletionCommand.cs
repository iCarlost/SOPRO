using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de eliminar una o varias filas YA persistidas del presupuesto
    /// (ADR-0001). Lleva el proyecto propietario y TODAS las filas que se eliminarán
    /// con la base que la vista tenía al construir el plan de borrado. El comando
    /// revalida pertenencia e identidad antes de escribir; los borradores Id=0 no
    /// forman parte de esta intención y se resuelven solo en la vista.
    /// </summary>
    public sealed class BudgetDeletionCommand
    {
        public BudgetDeletionCommand(int projectId, IEnumerable<BudgetDeletionTarget> targets)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            ArgumentNullException.ThrowIfNull(targets);

            var lista = targets.ToArray();
            if (lista.Length == 0)
                throw new ArgumentException("La eliminación requiere al menos una fila persistida.", nameof(targets));

            if (lista.Any(t => t is null))
                throw new ArgumentException("La eliminación no admite filas nulas.", nameof(targets));

            if (lista.Select(t => t.ConceptId).Distinct().Count() != lista.Length)
                throw new ArgumentException("La eliminación no admite conceptos repetidos.", nameof(targets));

            ProjectId = projectId;
            Targets = lista;
        }

        public int ProjectId { get; }

        /// <summary>Filas a eliminar, con la base esperada de cada una.</summary>
        public IReadOnlyList<BudgetDeletionTarget> Targets { get; }
    }

    /// <summary>
    /// Identidad y base de una fila que la vista propone eliminar: Id, orden, nivel y
    /// condición de agrupador. Si cualquiera cambió en la base, la eliminación no
    /// representa lo que el usuario confirmó y el comando se detiene sin escribir.
    /// </summary>
    public sealed class BudgetDeletionTarget
    {
        public BudgetDeletionTarget(int conceptId, int expectedOrder, int expectedLevel, bool expectedIsAggregator)
        {
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));

            ConceptId = conceptId;
            ExpectedOrder = expectedOrder;
            ExpectedLevel = expectedLevel;
            ExpectedIsAggregator = expectedIsAggregator;
        }

        public int ConceptId { get; }

        public int ExpectedOrder { get; }

        public int ExpectedLevel { get; }

        public bool ExpectedIsAggregator { get; }

        public static BudgetDeletionTarget FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetDeletionTarget(
                concepto.Id,
                concepto.Orden,
                concepto.Nivel,
                concepto.EsAgrupador);
        }
    }
}
