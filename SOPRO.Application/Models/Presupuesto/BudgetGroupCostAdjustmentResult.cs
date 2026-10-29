namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de un reajuste de costo agrupado: la clasificación de la operación
    /// y, solo si se confirmó, la instantánea inmutable de lo que quedó en la base.
    /// </summary>
    public sealed class BudgetGroupCostAdjustmentResult
    {
        internal BudgetGroupCostAdjustmentResult(
            BudgetOperationResult operation,
            BudgetGroupCostAdjustmentSnapshot? confirmed,
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
        public BudgetGroupCostAdjustmentSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por un reajuste agrupado. Incluye el agrupador y
    /// su importe, TODOS los conceptos del presupuesto afectados (también las hojas
    /// fuera del grupo que comparten alguna matriz reajustada), todos los agrupadores
    /// recalculados y el cierre completo de matrices y componentes para que la vista
    /// sincronice filas y totales sin releer la base. Las colecciones son inmutables.
    /// </summary>
    public sealed class BudgetGroupCostAdjustmentSnapshot
    {
        internal BudgetGroupCostAdjustmentSnapshot(
            int projectId,
            BudgetProjectCalculationBase project,
            BudgetConfirmedConceptRow group,
            IReadOnlyList<BudgetConfirmedConceptRow> affectedLeaves,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators,
            IReadOnlyList<BudgetConfirmedMatrixRow> affectedMatrices,
            IReadOnlyList<BudgetConfirmedMatrixComponentRow> affectedComponents,
            IReadOnlyList<int> adjustedMatrixIds,
            decimal requestedTargetAmount,
            decimal currentAmount,
            decimal achievedAmount,
            decimal minimumAmount,
            decimal factorApplied,
            int adjustableLeafCount,
            int fixedLeafCount)
        {
            ProjectId = projectId;
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Group = group ?? throw new ArgumentNullException(nameof(group));
            AffectedLeaves = ToReadOnly(affectedLeaves);
            Aggregators = ToReadOnly(aggregators);
            AffectedMatrices = ToReadOnly(affectedMatrices);
            AffectedComponents = ToReadOnly(affectedComponents);
            AdjustedMatrixIds = ToReadOnly(adjustedMatrixIds);
            RequestedTargetAmount = requestedTargetAmount;
            CurrentAmount = currentAmount;
            AchievedAmount = achievedAmount;
            MinimumAmount = minimumAmount;
            FactorApplied = factorApplied;
            AdjustableLeafCount = adjustableLeafCount;
            FixedLeafCount = fixedLeafCount;
        }

        private static IReadOnlyList<T> ToReadOnly<T>(IReadOnlyList<T>? items)
            => Array.AsReadOnly(items is null || items.Count == 0
                ? Array.Empty<T>()
                : items.ToArray());

        public int ProjectId { get; }

        public BudgetProjectCalculationBase Project { get; }

        /// <summary>Agrupador reajustado tal como quedó tras el recálculo.</summary>
        public BudgetConfirmedConceptRow Group { get; }

        /// <summary>
        /// Hojas del proyecto que referencian alguna matriz del cierre afectado,
        /// estén o no dentro del agrupador, ordenadas por Orden e Id.
        /// </summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> AffectedLeaves { get; }

        /// <summary>Todos los agrupadores del proyecto tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }

        /// <summary>Matrices del cierre afectado, incluida la unión de los cierres.</summary>
        public IReadOnlyList<BudgetConfirmedMatrixRow> AffectedMatrices { get; }

        /// <summary>Componentes de las matrices afectadas, por matriz, Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedMatrixComponentRow> AffectedComponents { get; }

        /// <summary>Matrices que efectivamente recibieron el factor, por Id.</summary>
        public IReadOnlyList<int> AdjustedMatrixIds { get; }

        /// <summary>Importe TOTAL objetivo capturado por el usuario.</summary>
        public decimal RequestedTargetAmount { get; }

        /// <summary>Importe total del agrupador antes del reajuste.</summary>
        public decimal CurrentAmount { get; }

        /// <summary>Importe total del agrupador después del reajuste.</summary>
        public decimal AchievedAmount { get; }

        /// <summary>
        /// Importe mínimo alcanzable con los rubros seleccionados: las hojas fijas y
        /// los componentes no ajustables permanecen.
        /// </summary>
        public decimal MinimumAmount { get; }

        /// <summary>Factor aplicado sobre rendimientos/cantidades de las matrices ajustadas.</summary>
        public decimal FactorApplied { get; }

        /// <summary>Hojas visibles cuyo importe puede cambiar con los rubros elegidos.</summary>
        public int AdjustableLeafCount { get; }

        /// <summary>Hojas visibles sin matriz o sin rubros ajustables (costo fijo).</summary>
        public int FixedLeafCount { get; }
    }
}
