using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de un pegado atómico: la clasificación de la operación y, solo si se
    /// confirmó, la instantánea inmutable de lo que quedó en la base.
    /// </summary>
    public sealed class BudgetPasteResult
    {
        internal BudgetPasteResult(
            BudgetOperationResult operation,
            BudgetPasteSnapshot? confirmed,
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
        public BudgetPasteSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por el pegado: todas las filas afectadas con sus
    /// valores finales y todos los agrupadores del proyecto recalculados por
    /// <c>PricePropagationService</c> en el mismo lote.
    /// </summary>
    public sealed class BudgetPasteSnapshot
    {
        internal BudgetPasteSnapshot(
            int projectId,
            IReadOnlyList<BudgetPasteRowSnapshot> rows,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators)
        {
            ProjectId = projectId;
            Rows = rows ?? Array.Empty<BudgetPasteRowSnapshot>();
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
        }

        public int ProjectId { get; }

        public IReadOnlyList<BudgetPasteRowSnapshot> Rows { get; }

        /// <summary>Agrupadores del proyecto tal como quedaron tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }
    }

    /// <summary>Valores persistidos de una fila pegada tras confirmar el lote (solo lectura).</summary>
    public sealed class BudgetPasteRowSnapshot
    {
        internal BudgetPasteRowSnapshot(
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
            bool esAgrupador,
            DateTime fechaModificacion)
        {
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
            EsAgrupador = esAgrupador;
            FechaModificacion = fechaModificacion;
        }

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

        public bool EsAgrupador { get; }

        public DateTime FechaModificacion { get; }

        internal static BudgetPasteRowSnapshot FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetPasteRowSnapshot(
                concepto.Id,
                concepto.Clave ?? string.Empty,
                concepto.Descripcion ?? string.Empty,
                concepto.Unidad ?? string.Empty,
                concepto.Cantidad,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal,
                concepto.MatrizId,
                concepto.EsAgrupador,
                concepto.FechaModificacion);
        }
    }
}
