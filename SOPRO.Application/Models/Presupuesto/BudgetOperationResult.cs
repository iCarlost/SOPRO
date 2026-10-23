using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>Resultado de un comando de escritura del presupuesto (ADR-0001).</summary>
    public enum BudgetOperationStatus
    {
        /// <summary>La operación completa se confirmó en la base.</summary>
        Committed,

        /// <summary>La base no coincide con las bases esperadas o un Id no pertenece al proyecto; no se escribió nada.</summary>
        Conflict,

        /// <summary>La operación se rechazó o falló y se revirtió; se puede reintentar sin reconciliar.</summary>
        Failed,

        /// <summary>No se sabe si el commit ocurrió; reconciliar con la base antes de reintentar.</summary>
        OutcomeUnknown
    }

    /// <summary>
    /// Resultado tipado de un comando del presupuesto. Solo <see cref="BudgetOperationStatus.Committed"/>
    /// autoriza a la vista a dar el trabajo por guardado. <see cref="Message"/> es siempre
    /// apto para mostrar al usuario; el detalle técnico viaja por separado en
    /// <see cref="Diagnostic"/> y no debe mostrarse.
    /// </summary>
    public sealed class BudgetOperationResult
    {
        private static readonly IReadOnlyList<int> SinConceptos = Array.Empty<int>();

        private BudgetOperationResult(BudgetOperationStatus status, IReadOnlyList<int> confirmedConceptIds, string? message)
        {
            Status = status;
            ConfirmedConceptIds = confirmedConceptIds;
            Message = message;
        }

        public BudgetOperationStatus Status { get; }

        /// <summary>
        /// Ids de los conceptos que el comando pidió y quedaron confirmados; vacío si la
        /// operación no se confirmó. Los efectos derivados (agrupadores) viajan en la
        /// instantánea del comando, no en esta lista.
        /// </summary>
        public IReadOnlyList<int> ConfirmedConceptIds { get; }

        /// <summary>Mensaje seguro para el usuario (sin rutas, SQL ni nombres de trigger).</summary>
        public string? Message { get; }

        public bool IsCommitted => Status == BudgetOperationStatus.Committed;

        public static BudgetOperationResult Committed(IReadOnlyList<int> confirmedConceptIds)
            => new(BudgetOperationStatus.Committed, confirmedConceptIds ?? SinConceptos, null);

        public static BudgetOperationResult Conflict(string message)
            => new(BudgetOperationStatus.Conflict, SinConceptos, message);

        public static BudgetOperationResult Failed(string message)
            => new(BudgetOperationStatus.Failed, SinConceptos, message);

        public static BudgetOperationResult OutcomeUnknown(string message)
            => new(BudgetOperationStatus.OutcomeUnknown, SinConceptos, message);
    }

    /// <summary>
    /// Resultado de una edición de cantidad: la clasificación de la operación y, solo si
    /// se confirmó, la instantánea inmutable de lo que quedó en la base.
    /// </summary>
    public sealed class BudgetQuantityEditResult
    {
        internal BudgetQuantityEditResult(
            BudgetOperationResult operation,
            BudgetQuantityEditSnapshot? confirmed,
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
        public BudgetQuantityEditSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por una edición de cantidad: el concepto editado y
    /// todos los agrupadores del proyecto recalculados por PricePropagationService.
    /// </summary>
    public sealed class BudgetQuantityEditSnapshot
    {
        internal BudgetQuantityEditSnapshot(
            int projectId,
            BudgetConfirmedConceptRow concept,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators)
        {
            ProjectId = projectId;
            Concept = concept ?? throw new ArgumentNullException(nameof(concept));
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
        }

        public int ProjectId { get; }

        public BudgetConfirmedConceptRow Concept { get; }

        /// <summary>Agrupadores del proyecto tal como quedaron tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }
    }

    /// <summary>
    /// Resultado de una edición de texto: la clasificación de la operación y, solo si
    /// se confirmó, la instantánea inmutable de lo que quedó en la base.
    /// </summary>
    public sealed class BudgetTextEditResult
    {
        internal BudgetTextEditResult(
            BudgetOperationResult operation,
            BudgetTextEditSnapshot? confirmed,
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
        public BudgetTextEditSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por una edición de texto: únicamente el campo
    /// editado y la marca de modificación. No incluye importes porque esta operación
    /// no los recalcula.
    /// </summary>
    public sealed class BudgetTextEditSnapshot
    {
        internal BudgetTextEditSnapshot(
            int projectId,
            int conceptId,
            BudgetTextEditField field,
            string value,
            DateTime fechaModificacion)
        {
            ProjectId = projectId;
            ConceptId = conceptId;
            Field = field;
            Value = value ?? throw new ArgumentNullException(nameof(value));
            FechaModificacion = fechaModificacion;
        }

        public int ProjectId { get; }

        public int ConceptId { get; }

        public BudgetTextEditField Field { get; }

        public string Value { get; }

        public DateTime FechaModificacion { get; }
    }

    /// <summary>Valores persistidos de un concepto tras confirmar el comando (solo lectura).</summary>
    public sealed class BudgetConfirmedConceptRow
    {
        internal BudgetConfirmedConceptRow(
            int conceptId,
            decimal cantidad,
            decimal costoDirectoUnitario,
            decimal costoDirectoTotal,
            decimal precioUnitario,
            decimal importeTotal,
            DateTime fechaModificacion)
        {
            ConceptId = conceptId;
            Cantidad = cantidad;
            CostoDirectoUnitario = costoDirectoUnitario;
            CostoDirectoTotal = costoDirectoTotal;
            PrecioUnitario = precioUnitario;
            ImporteTotal = importeTotal;
            FechaModificacion = fechaModificacion;
        }

        public int ConceptId { get; }

        public decimal Cantidad { get; }

        public decimal CostoDirectoUnitario { get; }

        public decimal CostoDirectoTotal { get; }

        public decimal PrecioUnitario { get; }

        public decimal ImporteTotal { get; }

        public DateTime FechaModificacion { get; }

        internal static BudgetConfirmedConceptRow FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetConfirmedConceptRow(
                concepto.Id,
                concepto.Cantidad,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal,
                concepto.FechaModificacion);
        }
    }
}
