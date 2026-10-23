using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de asignar una matriz APU a un concepto YA persistido del presupuesto
    /// (ADR-0001). Lleva solo identidad, bases esperadas, el Id de la matriz de origen
    /// y los textos/cantidad propuestos: los importes derivados se calculan en
    /// Application con los servicios canónicos, nunca en el llamador. Todos sus
    /// valores son de solo lectura para que un reintento repita exactamente la misma
    /// operación.
    /// </summary>
    public sealed class BudgetMatrixAssignmentCommand
    {
        public BudgetMatrixAssignmentCommand(
            int projectId,
            int conceptId,
            BudgetProjectCalculationBase expectedProject,
            BudgetConceptAssignmentBase expectedConcept,
            int matrixId,
            string proposedKey,
            string? descriptionOverride,
            string? unitOverride,
            decimal proposedQuantity)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (matrixId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matrixId));
            if (proposedQuantity <= 0m)
                throw new ArgumentOutOfRangeException(nameof(proposedQuantity));
            if (string.IsNullOrWhiteSpace(proposedKey))
                throw new ArgumentException("La clave propuesta es requerida.", nameof(proposedKey));

            ProjectId = projectId;
            ConceptId = conceptId;
            ExpectedProject = expectedProject ?? throw new ArgumentNullException(nameof(expectedProject));
            ExpectedConcept = expectedConcept ?? throw new ArgumentNullException(nameof(expectedConcept));
            MatrixId = matrixId;
            ProposedKey = proposedKey.Trim();
            DescriptionOverride = string.IsNullOrWhiteSpace(descriptionOverride)
                ? null : descriptionOverride.Trim();
            UnitOverride = string.IsNullOrWhiteSpace(unitOverride)
                ? null : unitOverride.Trim();
            ProposedQuantity = proposedQuantity;
        }

        public int ProjectId { get; }

        public int ConceptId { get; }

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Base del concepto que la vista cargó antes de la asignación.</summary>
        public BudgetConceptAssignmentBase ExpectedConcept { get; }

        /// <summary>Identidad de la matriz APU de origen; el comando la recarga y revalida.</summary>
        public int MatrixId { get; }

        /// <summary>Clave que se escribirá en el concepto (la que el usuario editó).</summary>
        public string ProposedKey { get; }

        /// <summary>Descripción explícita; <c>null</c> para tomar la de la matriz.</summary>
        public string? DescriptionOverride { get; }

        /// <summary>Unidad explícita; <c>null</c> para tomar la de la matriz.</summary>
        public string? UnitOverride { get; }

        /// <summary>Cantidad propuesta por el usuario, sin convertirla a importes.</summary>
        public decimal ProposedQuantity { get; }
    }

    /// <summary>
    /// Base del concepto que interviene en la asignación de matriz: identidad, textos,
    /// cantidad, matriz vigente y valores derivados. Si alguno cambió en la base, el
    /// resultado ya no representaría lo que el usuario vio.
    /// </summary>
    public sealed class BudgetConceptAssignmentBase
    {
        public BudgetConceptAssignmentBase(
            string clave,
            string descripcion,
            string unidad,
            decimal cantidad,
            int? matrizId,
            decimal costoDirectoUnitario,
            decimal costoDirectoTotal,
            decimal precioUnitario,
            decimal importeTotal,
            bool esAgrupador)
        {
            Clave = clave ?? string.Empty;
            Descripcion = descripcion ?? string.Empty;
            Unidad = unidad ?? string.Empty;
            Cantidad = cantidad;
            MatrizId = matrizId;
            CostoDirectoUnitario = costoDirectoUnitario;
            CostoDirectoTotal = costoDirectoTotal;
            PrecioUnitario = precioUnitario;
            ImporteTotal = importeTotal;
            EsAgrupador = esAgrupador;
        }

        public string Clave { get; }

        public string Descripcion { get; }

        public string Unidad { get; }

        public decimal Cantidad { get; }

        public int? MatrizId { get; }

        public decimal CostoDirectoUnitario { get; }

        public decimal CostoDirectoTotal { get; }

        public decimal PrecioUnitario { get; }

        public decimal ImporteTotal { get; }

        public bool EsAgrupador { get; }

        /// <summary>Copia la base del concepto tal como la cargó la vista (antes de asignar).</summary>
        public static BudgetConceptAssignmentBase FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetConceptAssignmentBase(
                concepto.Clave,
                concepto.Descripcion,
                concepto.Unidad,
                concepto.Cantidad,
                concepto.MatrizId,
                concepto.CostoDirectoUnitario,
                concepto.CostoDirectoTotal,
                concepto.PrecioUnitario,
                concepto.ImporteTotal,
                concepto.EsAgrupador);
        }
    }
}
