using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de cambio de tipo de una fila del presupuesto (ADR-0001). Si
    /// <see cref="ConceptId"/> tiene valor, la fila ya existe y solo se cambia su
    /// estado jerárquico (agrupador/nivel); si es <c>null</c>, la fila aún no se ha
    /// persistido y el comando crea el concepto. Los textos propuestos se usan para
    /// el alta; en una fila existente los campos de texto siguen siendo propiedad de
    /// <see cref="BudgetTextEditCommand"/>. Todos sus valores son de solo lectura
    /// para que un reintento repita exactamente la misma operación.
    /// </summary>
    public sealed class BudgetTypeChangeCommand
    {
        public BudgetTypeChangeCommand(
            int projectId,
            int? conceptId,
            BudgetConceptTypeBase? expectedConcept,
            string tipo,
            string? clave,
            string? descripcion,
            string? unidad)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            if (conceptId.HasValue && conceptId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (conceptId.HasValue && expectedConcept == null)
                throw new ArgumentNullException(nameof(expectedConcept),
                    "Una fila existente requiere la base esperada del concepto.");
            if (!conceptId.HasValue && expectedConcept != null)
                throw new ArgumentException(
                    "Una fila nueva no debe declarar una base esperada de concepto.",
                    nameof(expectedConcept));
            if (string.IsNullOrWhiteSpace(tipo))
                throw new ArgumentException("El tipo propuesto es obligatorio.", nameof(tipo));

            ProjectId = projectId;
            ConceptId = conceptId;
            ExpectedConcept = expectedConcept;
            Tipo = tipo.Trim();
            Clave = clave ?? string.Empty;
            Descripcion = descripcion ?? string.Empty;
            Unidad = unidad ?? string.Empty;
        }

        public int ProjectId { get; }

        /// <summary>Id del concepto existente, o <c>null</c> si la fila aún no se persistió.</summary>
        public int? ConceptId { get; }

        /// <summary>Estado que la vista cargó antes del cambio; obligatorio si la fila existe.</summary>
        public BudgetConceptTypeBase? ExpectedConcept { get; }

        /// <summary>Tipo propuesto por el usuario, ya recortado.</summary>
        public string Tipo { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }
    }

    /// <summary>
    /// Base jerárquica de un concepto existente que interviene en un cambio de tipo.
    /// Si el nivel o la condición de agrupador cambiaron en la base, el comando no
    /// puede aplicar el cambio sobre lo que el usuario vio.
    /// </summary>
    public sealed class BudgetConceptTypeBase
    {
        public BudgetConceptTypeBase(int nivel, bool esAgrupador)
        {
            Nivel = nivel;
            EsAgrupador = esAgrupador;
        }

        public int Nivel { get; }

        public bool EsAgrupador { get; }

        public static BudgetConceptTypeBase FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetConceptTypeBase(concepto.Nivel, concepto.EsAgrupador);
        }
    }

    /// <summary>
    /// Resultado de un cambio de tipo: la clasificación de la operación y, solo si
    /// se confirmó, la instantánea inmutable de lo que quedó en la base.
    /// </summary>
    public sealed class BudgetTypeChangeResult
    {
        internal BudgetTypeChangeResult(
            BudgetOperationResult operation,
            BudgetTypeChangeSnapshot? confirmed,
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
        public BudgetTypeChangeSnapshot? Confirmed { get; }
    }

    /// <summary>
    /// Instantánea de lo confirmado por un cambio de tipo: el concepto (con Id y
    /// todos sus campos persistidos) y todos los agrupadores del proyecto
    /// recalculados por PricePropagationService.
    /// </summary>
    public sealed class BudgetTypeChangeSnapshot
    {
        internal BudgetTypeChangeSnapshot(
            int projectId,
            BudgetConfirmedConceptDetail concept,
            IReadOnlyList<BudgetConfirmedConceptRow> aggregators,
            bool stateAlreadyCorrect)
        {
            ProjectId = projectId;
            Concept = concept ?? throw new ArgumentNullException(nameof(concept));
            Aggregators = aggregators ?? Array.Empty<BudgetConfirmedConceptRow>();
            StateAlreadyCorrect = stateAlreadyCorrect;
        }

        public int ProjectId { get; }

        public BudgetConfirmedConceptDetail Concept { get; }

        /// <summary>Agrupadores del proyecto tal como quedaron tras el recálculo, por Orden e Id.</summary>
        public IReadOnlyList<BudgetConfirmedConceptRow> Aggregators { get; }

        /// <summary>
        /// <c>true</c> cuando el estado ya era el propuesto y no se escribió nada; el
        /// resultado igualmente se clasifica como confirmado.
        /// </summary>
        public bool StateAlreadyCorrect { get; }
    }

    /// <summary>Valores persistidos completos de un concepto tras confirmar el comando.</summary>
    public sealed class BudgetConfirmedConceptDetail
    {
        internal BudgetConfirmedConceptDetail(
            int conceptId,
            int projectId,
            string clave,
            string descripcion,
            string unidad,
            bool esAgrupador,
            int nivel,
            int orden,
            decimal cantidad,
            int? matrizId,
            decimal costoDirectoUnitario,
            decimal costoDirectoTotal,
            decimal precioUnitario,
            decimal importeTotal,
            string columnasPersonalizadasJson,
            string notas,
            DateTime fechaCreacion,
            DateTime fechaModificacion)
        {
            ConceptId = conceptId;
            ProjectId = projectId;
            Clave = clave;
            Descripcion = descripcion;
            Unidad = unidad;
            EsAgrupador = esAgrupador;
            Nivel = nivel;
            Orden = orden;
            Cantidad = cantidad;
            MatrizId = matrizId;
            CostoDirectoUnitario = costoDirectoUnitario;
            CostoDirectoTotal = costoDirectoTotal;
            PrecioUnitario = precioUnitario;
            ImporteTotal = importeTotal;
            ColumnasPersonalizadasJSON = columnasPersonalizadasJson;
            Notas = notas;
            FechaCreacion = fechaCreacion;
            FechaModificacion = fechaModificacion;
        }

        public int ConceptId { get; }

        public int ProjectId { get; }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public bool EsAgrupador { get; }

        public int Nivel { get; }

        public int Orden { get; }

        public decimal Cantidad { get; }

        public int? MatrizId { get; }

        public decimal CostoDirectoUnitario { get; }

        public decimal CostoDirectoTotal { get; }

        public decimal PrecioUnitario { get; }

        public decimal ImporteTotal { get; }

        public string ColumnasPersonalizadasJSON { get; }

        public string Notas { get; }

        public DateTime FechaCreacion { get; }

        public DateTime FechaModificacion { get; }

        internal static BudgetConfirmedConceptDetail FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetConfirmedConceptDetail(
                concepto.Id,
                concepto.ProyectoId,
                concepto.Clave,
                concepto.Descripcion,
                concepto.Unidad,
                concepto.EsAgrupador,
                concepto.Nivel,
                concepto.Orden,
                concepto.Cantidad,
                concepto.MatrizId,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal,
                concepto.ColumnasPersonalizadasJSON,
                concepto.Notas,
                concepto.FechaCreacion,
                concepto.FechaModificacion);
        }
    }
}
