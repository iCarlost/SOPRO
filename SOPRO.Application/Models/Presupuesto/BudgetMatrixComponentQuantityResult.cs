using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de una edición de cantidad de componente de matriz: la clasificación
    /// de la operación y, solo si se confirmó, la instantánea inmutable de lo que
    /// quedó en la base.
    /// </summary>
    public sealed class BudgetMatrixComponentQuantityResult
    {
        internal BudgetMatrixComponentQuantityResult(
            BudgetOperationResult operation,
            BudgetMatrixComponentQuantitySnapshot? confirmed,
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
        public BudgetMatrixComponentQuantitySnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por la edición: la base económica del proyecto,
    /// la matriz editada, TODAS las matrices afectadas por el cierre (la editada y
    /// sus ascendientes auxiliares), los componentes recalculados de esas matrices,
    /// las hojas del presupuesto que las referencian y todos los agrupadores del
    /// proyecto. Es lo que la vista necesita para rehacer la jerarquía y los totales
    /// sin releer la base ni recalcular por su cuenta.
    /// </summary>
    public sealed class BudgetMatrixComponentQuantitySnapshot
    {
        internal BudgetMatrixComponentQuantitySnapshot(
            int projectId,
            BudgetProjectCalculationBase project,
            BudgetConfirmedMatrixRow matrix,
            IReadOnlyList<BudgetConfirmedMatrixRow> affectedMatrices,
            IReadOnlyList<BudgetConfirmedMatrixComponentRow> affectedComponents,
            IReadOnlyList<BudgetConfirmedConceptRow> leaves,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators)
        {
            ProjectId = projectId;
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
            AffectedMatrices = affectedMatrices ?? Array.Empty<BudgetConfirmedMatrixRow>();
            AffectedComponents = affectedComponents ?? Array.Empty<BudgetConfirmedMatrixComponentRow>();
            Leaves = leaves ?? Array.Empty<BudgetConfirmedConceptRow>();
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
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
    }

    /// <summary>Valores persistidos de una matriz tras confirmar el comando (solo lectura).</summary>
    public sealed class BudgetConfirmedMatrixRow
    {
        internal BudgetConfirmedMatrixRow(int matrixId, decimal costoDirecto, DateTime fechaModificacion)
        {
            MatrixId = matrixId;
            CostoDirecto = costoDirecto;
            FechaModificacion = fechaModificacion;
        }

        public int MatrixId { get; }

        public decimal CostoDirecto { get; }

        public DateTime FechaModificacion { get; }

        public static BudgetConfirmedMatrixRow FromMatriz(Matriz matriz)
        {
            ArgumentNullException.ThrowIfNull(matriz);

            return new BudgetConfirmedMatrixRow(
                matriz.Id,
                matriz.CostoDirecto,
                matriz.FechaModificacion);
        }
    }

    /// <summary>Valores persistidos de un componente de matriz tras confirmar el comando.</summary>
    public sealed class BudgetConfirmedMatrixComponentRow
    {
        internal BudgetConfirmedMatrixComponentRow(
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
            int orden)
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

        public static BudgetConfirmedMatrixComponentRow FromComponente(ComponenteMatriz componente)
        {
            ArgumentNullException.ThrowIfNull(componente);

            return new BudgetConfirmedMatrixComponentRow(
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
                componente.Orden);
        }
    }
}
