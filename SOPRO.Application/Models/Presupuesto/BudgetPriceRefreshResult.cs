namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado del refresco de precios: la clasificación de la operación y, solo
    /// si se confirmó, la instantánea inmutable de lo que quedó en la base.
    /// </summary>
    public sealed class BudgetPriceRefreshResult
    {
        internal BudgetPriceRefreshResult(
            BudgetOperationResult operation,
            BudgetPriceRefreshSnapshot? confirmed,
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
        public BudgetPriceRefreshSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por el refresco de precios: la base económica
    /// del proyecto y TODAS las filas del presupuesto tal como quedaron en la base,
    /// ordenadas por Orden e Id, con sus totales recalculados.
    /// </summary>
    public sealed class BudgetPriceRefreshSnapshot
    {
        internal BudgetPriceRefreshSnapshot(
            int projectId,
            BudgetProjectCalculationBase project,
            IReadOnlyList<BudgetConfirmedConceptRow> rows)
        {
            ProjectId = projectId;
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Rows = rows ?? Array.Empty<BudgetConfirmedConceptRow>();
        }

        public int ProjectId { get; }

        public BudgetProjectCalculationBase Project { get; }

        /// <summary>Todas las filas del proyecto tras el refresco, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Rows { get; }
    }
}
