namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de asignar una matriz APU a un concepto existente: la clasificación
    /// de la operación y, solo si se confirmó, la instantánea inmutable de lo que
    /// quedó en la base.
    /// </summary>
    public sealed class BudgetMatrixAssignmentResult
    {
        internal BudgetMatrixAssignmentResult(
            BudgetOperationResult operation,
            BudgetMatrixAssignmentSnapshot? confirmed,
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
        public BudgetMatrixAssignmentSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por la asignación: el concepto completo (para
    /// sincronizar Tag y celdas visibles) y todos los agrupadores del proyecto
    /// recalculados por <c>PricePropagationService</c>.
    /// </summary>
    public sealed class BudgetMatrixAssignmentSnapshot
    {
        internal BudgetMatrixAssignmentSnapshot(
            int projectId,
            int conceptId,
            string clave,
            string descripcion,
            string unidad,
            decimal cantidad,
            decimal costoDirectoUnitario,
            decimal costoDirectoTotal,
            decimal precioUnitario,
            decimal importeTotal,
            int? matrizId,
            DateTime fechaModificacion,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators)
        {
            ProjectId = projectId;
            ConceptId = conceptId;
            Clave = clave;
            Descripcion = descripcion;
            Unidad = unidad;
            Cantidad = cantidad;
            CostoDirectoUnitario = costoDirectoUnitario;
            CostoDirectoTotal = costoDirectoTotal;
            PrecioUnitario = precioUnitario;
            ImporteTotal = importeTotal;
            MatrizId = matrizId;
            FechaModificacion = fechaModificacion;
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
        }

        public int ProjectId { get; }

        public int ConceptId { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public decimal Cantidad { get; }

        public decimal CostoDirectoUnitario { get; }

        public decimal CostoDirectoTotal { get; }

        public decimal PrecioUnitario { get; }

        public decimal ImporteTotal { get; }

        public int? MatrizId { get; }

        public DateTime FechaModificacion { get; }

        /// <summary>Agrupadores del proyecto tal como quedaron tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }
    }
}
