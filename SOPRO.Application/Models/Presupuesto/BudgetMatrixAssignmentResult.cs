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
            BudgetConfirmedConceptDetail concept,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators,
            IReadOnlyList<BudgetConfirmedConceptDetail> rows,
            bool createdNewRow)
        {
            ProjectId = projectId;
            Concept = concept ?? throw new ArgumentNullException(nameof(concept));
            ConceptId = concept.ConceptId;
            Clave = concept.Clave;
            Descripcion = concept.Descripcion;
            Unidad = concept.Unidad;
            Cantidad = concept.Cantidad;
            CostoDirectoUnitario = concept.CostoDirectoUnitario;
            CostoDirectoTotal = concept.CostoDirectoTotal;
            PrecioUnitario = concept.PrecioUnitario;
            ImporteTotal = concept.ImporteTotal;
            MatrizId = concept.MatrizId;
            FechaModificacion = concept.FechaModificacion;
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
            Rows = rows ?? Array.Empty<BudgetConfirmedConceptDetail>();
            CreatedNewRow = createdNewRow;
        }

        public int ProjectId { get; }

        /// <summary>Detalle completo del concepto asignado o creado.</summary>
        public BudgetConfirmedConceptDetail Concept { get; }

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

        /// <summary>
        /// TODAS las filas persistidas del proyecto tras confirmar, ordenadas por Orden
        /// e Id. Incluye el concepto creado y las filas cuyo Orden se desplazó.
        /// </summary>
        public IReadOnlyList<BudgetConfirmedConceptDetail> Rows { get; }

        /// <summary><c>true</c> si el comando creó la fila (Id 0/Tag null).</summary>
        public bool CreatedNewRow { get; }
    }
}
