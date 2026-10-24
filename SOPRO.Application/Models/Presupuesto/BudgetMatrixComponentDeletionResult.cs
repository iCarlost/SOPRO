namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de una eliminación de componente de matriz: la clasificación de la
    /// operación y, solo si se confirmó, la instantánea inmutable de lo que quedó en
    /// la base.
    /// </summary>
    public sealed class BudgetMatrixComponentDeletionResult
    {
        internal BudgetMatrixComponentDeletionResult(
            BudgetOperationResult operation,
            BudgetMatrixComponentDeletionSnapshot? confirmed,
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
        public BudgetMatrixComponentDeletionSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por la eliminación: la base económica del proyecto,
    /// la matriz editada, TODAS las matrices afectadas por el cierre (la editada y sus
    /// ascendientes auxiliares), los componentes recalculados de esas matrices, las
    /// hojas del presupuesto que las referencian, todos los agrupadores del proyecto y
    /// el Id del componente eliminado. Es lo que la vista necesita para rehacer la
    /// jerarquía y los totales sin releer la base ni recalcular por su cuenta.
    /// </summary>
    public sealed class BudgetMatrixComponentDeletionSnapshot
    {
        internal BudgetMatrixComponentDeletionSnapshot(
            int projectId,
            BudgetProjectCalculationBase project,
            BudgetConfirmedMatrixRow matrix,
            IReadOnlyList<BudgetConfirmedMatrixRow> affectedMatrices,
            IReadOnlyList<BudgetConfirmedMatrixComponentRow> affectedComponents,
            IReadOnlyList<BudgetConfirmedConceptRow> leaves,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators,
            int deletedComponentId)
        {
            ProjectId = projectId;
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
            AffectedMatrices = affectedMatrices ?? Array.Empty<BudgetConfirmedMatrixRow>();
            AffectedComponents = affectedComponents ?? Array.Empty<BudgetConfirmedMatrixComponentRow>();
            Leaves = leaves ?? Array.Empty<BudgetConfirmedConceptRow>();
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
            DeletedComponentId = deletedComponentId;
        }

        public int ProjectId { get; }

        public BudgetProjectCalculationBase Project { get; }

        /// <summary>Matriz editada tal como quedó tras el recálculo.</summary>
        public BudgetConfirmedMatrixRow Matrix { get; }

        /// <summary>
        /// Matriz editada más sus ascendientes auxiliares recalculados, por Id. La
        /// matriz editada aparece también aquí.
        /// </summary>
        public IReadOnlyList<BudgetConfirmedMatrixRow> AffectedMatrices { get; }

        /// <summary>Componentes de las matrices afectadas, por matriz, Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedMatrixComponentRow> AffectedComponents { get; }

        /// <summary>Hojas del proyecto que referencian alguna matriz afectada, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Leaves { get; }

        /// <summary>Todos los agrupadores del proyecto tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }

        /// <summary>Id del componente que dejó de existir. No hay Id generado: es una eliminación.</summary>
        public int DeletedComponentId { get; }
    }
}
