namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de un reordenamiento de filas del presupuesto: la clasificación de la
    /// operación y, solo si se confirmó, la instantánea inmutable de lo que quedó en la
    /// base (orden confirmado y agrupadores recalculados).
    /// </summary>
    public sealed class BudgetReorderResult
    {
        internal BudgetReorderResult(
            BudgetOperationResult operation,
            BudgetReorderSnapshot? confirmed,
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
        public BudgetReorderSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por un reordenamiento: el Orden asignado a cada fila
    /// y todos los agrupadores del proyecto recalculados en el mismo contexto, sin
    /// guardado intermedio, por <see cref="Services.PricePropagationService"/>.
    /// </summary>
    public sealed class BudgetReorderSnapshot
    {
        internal BudgetReorderSnapshot(
            int projectId,
            IReadOnlyList<BudgetConfirmedConceptOrder> order,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators)
        {
            ProjectId = projectId;
            Order = order ?? Array.Empty<BudgetConfirmedConceptOrder>();
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
        }

        public int ProjectId { get; }

        /// <summary>Orden confirmado, de menor a mayor posición.</summary>
        public IReadOnlyList<BudgetConfirmedConceptOrder> Order { get; }

        /// <summary>Agrupadores del proyecto tal como quedaron tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }
    }

    /// <summary>Posición persistida confirmada para un concepto.</summary>
    public sealed class BudgetConfirmedConceptOrder
    {
        internal BudgetConfirmedConceptOrder(int conceptId, int order)
        {
            ConceptId = conceptId;
            Order = order;
        }

        public int ConceptId { get; }

        public int Order { get; }
    }
}
