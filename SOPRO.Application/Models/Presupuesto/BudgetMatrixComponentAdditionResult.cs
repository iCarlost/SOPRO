using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de una alta en lote de componentes de matriz: la clasificación de la
    /// operación y, solo si se confirmó, la instantánea inmutable de lo que quedó en
    /// la base.
    /// </summary>
    public sealed class BudgetMatrixComponentAdditionResult
    {
        internal BudgetMatrixComponentAdditionResult(
            BudgetOperationResult operation,
            BudgetMatrixComponentAdditionSnapshot? confirmed,
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
        public BudgetMatrixComponentAdditionSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por el alta en lote: los Ids generados para las
    /// filas nuevas, la matriz destino, TODAS las matrices afectadas por el cierre
    /// (la destino y sus ascendientes auxiliares), sus componentes restantes y nuevos
    /// (incluidas las notas), las hojas del presupuesto que las referencian y todos
    /// los agrupadores del proyecto. Es lo que la vista necesita para rehacer la
    /// jerarquía y los totales sin releer la base ni recalcular por su cuenta.
    /// </summary>
    public sealed class BudgetMatrixComponentAdditionSnapshot
    {
        internal BudgetMatrixComponentAdditionSnapshot(
            int projectId,
            BudgetProjectCalculationBase project,
            BudgetConfirmedMatrixRow matrix,
            IReadOnlyList<int> addedComponentIds,
            IReadOnlyList<BudgetConfirmedMatrixRow> affectedMatrices,
            IReadOnlyList<BudgetConfirmedMatrixComponentStateRow> affectedComponents,
            IReadOnlyList<BudgetConfirmedConceptRow> leaves,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators)
        {
            ProjectId = projectId;
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
            AddedComponentIds = addedComponentIds ?? Array.Empty<int>();
            AffectedMatrices = affectedMatrices ?? Array.Empty<BudgetConfirmedMatrixRow>();
            AffectedComponents = affectedComponents ?? Array.Empty<BudgetConfirmedMatrixComponentStateRow>();
            Leaves = leaves ?? Array.Empty<BudgetConfirmedConceptRow>();
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
        }

        public int ProjectId { get; }

        public BudgetProjectCalculationBase Project { get; }

        /// <summary>Matriz destino tal como quedó tras el recálculo.</summary>
        public BudgetConfirmedMatrixRow Matrix { get; }

        /// <summary>Ids generados por la base para las filas nuevas, en el orden del lote.</summary>
        public IReadOnlyList<int> AddedComponentIds { get; }

        /// <summary>
        /// Matriz destino más sus ascendientes auxiliares recalculados, por Id. La
        /// matriz destino aparece también aquí.
        /// </summary>
        public IReadOnlyList<BudgetConfirmedMatrixRow> AffectedMatrices { get; }

        /// <summary>
        /// Componentes de las matrices afectadas (persistidos y nuevos), por matriz,
        /// Orden e Id, con sus notas.
        /// </summary>
        public IReadOnlyList<BudgetConfirmedMatrixComponentStateRow> AffectedComponents { get; }

        /// <summary>Hojas del proyecto que referencian alguna matriz afectada, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Leaves { get; }

        /// <summary>Todos los agrupadores del proyecto tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }
    }

    /// <summary>
    /// Valores persistidos de un componente de matriz tras confirmar el comando,
    /// incluida la nota del componente.
    /// </summary>
    public sealed class BudgetConfirmedMatrixComponentStateRow
    {
        internal BudgetConfirmedMatrixComponentStateRow(
            int componentId,
            int matrixId,
            TipoComponenteMatriz tipoComponente,
            int? materialId,
            int? manoDeObraId,
            int? maquinariaId,
            int? auxiliarId,
            int? herramientaId,
            decimal cantidad,
            decimal rendimiento,
            decimal importe,
            int orden,
            string notas)
        {
            ComponentId = componentId;
            MatrixId = matrixId;
            TipoComponente = tipoComponente;
            MaterialId = materialId;
            ManoDeObraId = manoDeObraId;
            MaquinariaId = maquinariaId;
            AuxiliarId = auxiliarId;
            HerramientaId = herramientaId;
            Cantidad = cantidad;
            Rendimiento = rendimiento;
            Importe = importe;
            Orden = orden;
            Notas = notas ?? string.Empty;
        }

        public int ComponentId { get; }

        public int MatrixId { get; }

        public TipoComponenteMatriz TipoComponente { get; }

        public int? MaterialId { get; }

        public int? ManoDeObraId { get; }

        public int? MaquinariaId { get; }

        public int? AuxiliarId { get; }

        public int? HerramientaId { get; }

        public decimal Cantidad { get; }

        public decimal Rendimiento { get; }

        public decimal Importe { get; }

        public int Orden { get; }

        public string Notas { get; }

        public static BudgetConfirmedMatrixComponentStateRow FromComponente(
            ComponenteMatriz componente)
        {
            ArgumentNullException.ThrowIfNull(componente);

            return new BudgetConfirmedMatrixComponentStateRow(
                componente.Id,
                componente.MatrizId,
                componente.TipoComponente,
                componente.MaterialId,
                componente.ManoDeObraId,
                componente.MaquinariaId,
                componente.AuxiliarId,
                componente.HerramientaId,
                componente.Cantidad,
                componente.Rendimiento,
                componente.Importe,
                componente.Orden,
                componente.Notas);
        }
    }
}
