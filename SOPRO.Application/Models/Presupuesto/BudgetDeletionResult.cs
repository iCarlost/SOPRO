namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de una eliminación de filas del presupuesto: la clasificación de la
    /// operación y, solo si se confirmó, la instantánea inmutable de lo que quedó en
    /// la base (Ids eliminados y agrupadores recalculados).
    /// </summary>
    public sealed class BudgetDeletionResult
    {
        internal BudgetDeletionResult(
            BudgetOperationResult operation,
            BudgetDeletionSnapshot? confirmed,
            string? diagnostic)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Confirmed = confirmed;
            Diagnostic = diagnostic;
        }

        public BudgetOperationResult Operation { get; }

        public BudgetOperationStatus Status => Operation.Status;

        public bool IsCommitted => Operation.IsCommitted;

        /// <summary>Mensaje seguro para el usuario.</summary>
        public string? Message => Operation.Message;

        /// <summary>Detalle técnico para registro y pruebas; nunca se muestra en la UI.</summary>
        public string? Diagnostic { get; }

        /// <summary>Valores confirmados; null si la operación no se confirmó.</summary>
        public BudgetDeletionSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por una eliminación: los Ids que dejaron de existir
    /// y todos los agrupadores del proyecto que permanecen, recalculados sin guardado
    /// intermedio por <see cref="Services.PricePropagationService"/>.
    /// </summary>
    public sealed class BudgetDeletionSnapshot
    {
        internal BudgetDeletionSnapshot(
            int projectId,
            IReadOnlyList<int> deletedConceptIds,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators)
        {
            ProjectId = projectId;
            DeletedConceptIds = deletedConceptIds ?? Array.Empty<int>();
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
        }

        public int ProjectId { get; }

        public IReadOnlyList<int> DeletedConceptIds { get; }

        /// <summary>Agrupadores que permanecen en el proyecto, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }
    }
}
