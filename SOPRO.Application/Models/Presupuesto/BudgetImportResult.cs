using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Resultado de una importación por lote: la clasificación de la operación y, solo
    /// si se confirmó, la instantánea inmutable de lo que quedó en la base.
    /// </summary>
    public sealed class BudgetImportResult
    {
        internal BudgetImportResult(
            BudgetOperationResult operation,
            BudgetImportSnapshot? confirmed,
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
        public BudgetImportSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por la importación: cada renglón con el Id y el
    /// Orden que le asignó la base (mapeado a su fila de origen) y los agrupadores del
    /// proyecto recalculados sin guardar sobre la lista final.
    /// </summary>
    public sealed class BudgetImportSnapshot
    {
        internal BudgetImportSnapshot(
            int projectId,
            IReadOnlyList<BudgetConfirmedImportedRow> rows,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators)
        {
            ProjectId = projectId;
            Rows = rows ?? Array.Empty<BudgetConfirmedImportedRow>();
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
        }

        public int ProjectId { get; }

        /// <summary>Renglones confirmados, en el mismo orden en que se enviaron.</summary>
        public IReadOnlyList<BudgetConfirmedImportedRow> Rows { get; }

        /// <summary>Agrupadores del proyecto tal como quedaron tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }
    }

    /// <summary>Renglón persistido por la importación, con su Id y posición generados.</summary>
    public sealed class BudgetConfirmedImportedRow
    {
        internal BudgetConfirmedImportedRow(
            int? sourceRowNumber,
            int conceptId,
            int orden,
            string clave,
            string descripcion,
            string unidad,
            decimal cantidad,
            int nivel,
            bool esAgrupador,
            decimal costoDirectoUnitario,
            decimal costoDirectoTotal,
            decimal precioUnitario,
            decimal importeTotal,
            DateTime fechaModificacion)
        {
            SourceRowNumber = sourceRowNumber;
            ConceptId = conceptId;
            Orden = orden;
            Clave = clave;
            Descripcion = descripcion;
            Unidad = unidad;
            Cantidad = cantidad;
            Nivel = nivel;
            EsAgrupador = esAgrupador;
            CostoDirectoUnitario = costoDirectoUnitario;
            CostoDirectoTotal = costoDirectoTotal;
            PrecioUnitario = precioUnitario;
            ImporteTotal = importeTotal;
            FechaModificacion = fechaModificacion;
        }

        public int? SourceRowNumber { get; }

        public int ConceptId { get; }

        public int Orden { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public decimal Cantidad { get; }

        public int Nivel { get; }

        public bool EsAgrupador { get; }

        public decimal CostoDirectoUnitario { get; }

        public decimal CostoDirectoTotal { get; }

        public decimal PrecioUnitario { get; }

        public decimal ImporteTotal { get; }

        public DateTime FechaModificacion { get; }

        internal static BudgetConfirmedImportedRow FromConcepto(
            ConceptoPresupuesto concepto,
            int? sourceRowNumber)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetConfirmedImportedRow(
                sourceRowNumber,
                concepto.Id,
                concepto.Orden,
                concepto.Clave,
                concepto.Descripcion,
                concepto.Unidad,
                concepto.Cantidad,
                concepto.Nivel,
                concepto.EsAgrupador,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal,
                concepto.FechaModificacion);
        }
    }
}
