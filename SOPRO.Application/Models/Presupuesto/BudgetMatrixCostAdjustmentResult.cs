using SOPRO.Application.Services;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de un reajuste de costo de matriz: la clasificación de la operación
    /// y, solo si se confirmó, la instantánea inmutable de lo que quedó en la base.
    /// </summary>
    public sealed class BudgetMatrixCostAdjustmentResult
    {
        internal BudgetMatrixCostAdjustmentResult(
            BudgetOperationResult operation,
            BudgetMatrixCostAdjustmentSnapshot? confirmed,
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
        public BudgetMatrixCostAdjustmentSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por el reajuste: la base económica del proyecto,
    /// la matriz reajustada, TODAS las matrices afectadas por el cierre (la reajustada
    /// y sus ascendientes auxiliares), sus componentes recalculados, las hojas que las
    /// referencian, todos los agrupadores del proyecto y el resultado del objetivo
    /// (monto actual, objetivo convertido, logrado y factor aplicado). Es lo que la
    /// vista necesita para sincronizar la fila seleccionada y los totales sin releer
    /// la base ni recalcular por su cuenta. Las colecciones son inmutables: un
    /// llamador no puede alterar la instantánea que otros consumidores observan.
    /// </summary>
    public sealed class BudgetMatrixCostAdjustmentSnapshot
    {
        internal BudgetMatrixCostAdjustmentSnapshot(
            int projectId,
            BudgetProjectCalculationBase project,
            BudgetConfirmedMatrixRow matrix,
            IReadOnlyList<BudgetConfirmedMatrixRow> affectedMatrices,
            IReadOnlyList<BudgetConfirmedMatrixComponentRow> affectedComponents,
            IReadOnlyList<BudgetConfirmedConceptRow> leaves,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators,
            int selectedLeafConceptId,
            BudgetMatrixCostAdjustmentTargetKind targetKind,
            decimal requestedTargetValue,
            decimal currentTargetValue,
            decimal achievedTargetValue,
            decimal currentCost,
            decimal targetCost,
            decimal achievedCost,
            decimal factorApplied)
        {
            ProjectId = projectId;
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
            AffectedMatrices = ToReadOnly(affectedMatrices);
            AffectedComponents = ToReadOnly(affectedComponents);
            Leaves = ToReadOnly(leaves);
            Aggregators = ToReadOnly(aggregators);
            SelectedLeafConceptId = selectedLeafConceptId;
            TargetKind = targetKind;
            RequestedTargetValue = requestedTargetValue;
            CurrentTargetValue = currentTargetValue;
            AchievedTargetValue = achievedTargetValue;
            CurrentCost = currentCost;
            TargetCost = targetCost;
            AchievedCost = achievedCost;
            FactorApplied = factorApplied;
        }

        private static IReadOnlyList<T> ToReadOnly<T>(IReadOnlyList<T>? items)
            => Array.AsReadOnly(items is null || items.Count == 0
                ? Array.Empty<T>()
                : items.ToArray());

        public int ProjectId { get; }

        public BudgetProjectCalculationBase Project { get; }

        /// <summary>Matriz reajustada tal como quedó tras el cierre.</summary>
        public BudgetConfirmedMatrixRow Matrix { get; }

        /// <summary>
        /// Matriz reajustada más sus ascendientes auxiliares recalculados, por Id. La
        /// matriz reajustada aparece también aquí.
        /// </summary>
        public IReadOnlyList<BudgetConfirmedMatrixRow> AffectedMatrices { get; }

        /// <summary>Componentes de las matrices afectadas, por matriz, Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedMatrixComponentRow> AffectedComponents { get; }

        /// <summary>Hojas del proyecto que referencian alguna matriz afectada, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Leaves { get; }

        /// <summary>Todos los agrupadores del proyecto tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }

        /// <summary>Concepto hoja cuyo P.U. o importe era el objetivo.</summary>
        public int SelectedLeafConceptId { get; }

        public BudgetMatrixCostAdjustmentTargetKind TargetKind { get; }

        /// <summary>Monto objetivo capturado por el usuario.</summary>
        public decimal RequestedTargetValue { get; }

        /// <summary>P.U. o importe del concepto hoja antes del reajuste, con precisión de pantalla.</summary>
        public decimal CurrentTargetValue { get; }

        /// <summary>P.U. o importe del concepto hoja después del reajuste, con precisión de pantalla.</summary>
        public decimal AchievedTargetValue { get; }

        /// <summary>Costo directo de la matriz antes del reajuste.</summary>
        public decimal CurrentCost { get; }

        /// <summary>Costo directo objetivo convertido desde el monto capturado.</summary>
        public decimal TargetCost { get; }

        /// <summary>Costo directo logrado por el reajuste.</summary>
        public decimal AchievedCost { get; }

        /// <summary>Factor aplicado sobre rendimientos/cantidades de la matriz.</summary>
        public decimal FactorApplied { get; }
    }
}
