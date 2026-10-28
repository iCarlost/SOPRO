using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Intención de edición de la cantidad de un concepto YA existente (ADR-0001).
    /// Lleva solo identidad, bases esperadas y la cantidad propuesta: los importes
    /// derivados los calcula Application con los servicios canónicos, nunca el llamador.
    /// Todos sus valores son de solo lectura para que un reintento repita exactamente
    /// la misma operación.
    /// </summary>
    public sealed class BudgetQuantityEditCommand
    {
        public BudgetQuantityEditCommand(
            int projectId,
            int conceptId,
            BudgetProjectCalculationBase expectedProject,
            BudgetConceptQuantityBase expectedConcept,
            decimal proposedQuantity)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));

            ProjectId = projectId;
            ConceptId = conceptId;
            ExpectedProject = expectedProject ?? throw new ArgumentNullException(nameof(expectedProject));
            ExpectedConcept = expectedConcept ?? throw new ArgumentNullException(nameof(expectedConcept));
            ProposedQuantity = proposedQuantity;
        }

        public int ProjectId { get; }

        public int ConceptId { get; }

        /// <summary>Base económica del proyecto con la que la vista calculó lo que muestra.</summary>
        public BudgetProjectCalculationBase ExpectedProject { get; }

        /// <summary>Base del concepto que la vista cargó antes de la edición.</summary>
        public BudgetConceptQuantityBase ExpectedConcept { get; }

        /// <summary>Cantidad propuesta por el usuario, sin redondear ni convertir a importes.</summary>
        public decimal ProposedQuantity { get; }
    }

    /// <summary>
    /// Parámetros del proyecto que determinan PrecioUnitario e ImporteTotal
    /// (BudgetPricingService / SoproCalculationEngine). Se comparan contra la base de
    /// escritura antes de aplicar: si cambiaron, los importes se calcularían sobre una
    /// base económica distinta de la que el usuario vio.
    /// </summary>
    public sealed class BudgetProjectCalculationBase
    {
        private BudgetProjectCalculationBase(
            decimal porcentajeIndirectosCentral,
            decimal porcentajeIndirectosCampo,
            decimal porcentajeFinanciamiento,
            decimal porcentajeUtilidad,
            decimal porcentajeCargosAdicionales,
            decimal porcentajeIva,
            string? modoCalculoPorcentajes,
            int decimalesCantidad,
            int decimalesImporte,
            int decimalesPorcentaje)
        {
            PorcentajeIndirectosCentral = porcentajeIndirectosCentral;
            PorcentajeIndirectosCampo = porcentajeIndirectosCampo;
            PorcentajeFinanciamiento = porcentajeFinanciamiento;
            PorcentajeUtilidad = porcentajeUtilidad;
            PorcentajeCargosAdicionales = porcentajeCargosAdicionales;
            PorcentajeIVA = porcentajeIva;
            ModoCalculoPorcentajes = modoCalculoPorcentajes;
            DecimalesCantidad = decimalesCantidad;
            DecimalesImporte = decimalesImporte;
            DecimalesPorcentaje = decimalesPorcentaje;
        }

        public decimal PorcentajeIndirectosCentral { get; }

        public decimal PorcentajeIndirectosCampo { get; }

        public decimal PorcentajeFinanciamiento { get; }

        public decimal PorcentajeUtilidad { get; }

        public decimal PorcentajeCargosAdicionales { get; }

        public decimal PorcentajeIVA { get; }

        public string? ModoCalculoPorcentajes { get; }

        public int DecimalesCantidad { get; }

        public int DecimalesImporte { get; }

        public int DecimalesPorcentaje { get; }

        /// <summary>Copia los parámetros de cálculo vigentes de la entidad leída por la vista.</summary>
        public static BudgetProjectCalculationBase FromProyecto(Proyecto proyecto)
        {
            ArgumentNullException.ThrowIfNull(proyecto);

            return new BudgetProjectCalculationBase(
                proyecto.PorcentajeIndirectosCentral,
                proyecto.PorcentajeIndirectosCampo,
                proyecto.PorcentajeFinanciamiento,
                proyecto.PorcentajeUtilidad,
                proyecto.PorcentajeCargosAdicionales,
                proyecto.PorcentajeIVA,
                proyecto.ModoCalculoPorcentajes,
                proyecto.DecimalesCantidad,
                proyecto.DecimalesImporte,
                proyecto.DecimalesPorcentaje);
        }
    }

    /// <summary>
    /// Base del concepto que interviene en el recálculo de una edición de cantidad:
    /// cantidad, costo directo unitario, matriz y condición de agrupador. Si alguna
    /// cambió en la base, el resultado ya no representaría lo que el usuario vio.
    /// </summary>
    public sealed class BudgetConceptQuantityBase
    {
        public BudgetConceptQuantityBase(
            decimal cantidad,
            decimal costoDirectoUnitario,
            int? matrizId,
            bool esAgrupador)
        {
            Cantidad = cantidad;
            CostoDirectoUnitario = costoDirectoUnitario;
            MatrizId = matrizId;
            EsAgrupador = esAgrupador;
        }

        public decimal Cantidad { get; }

        public decimal CostoDirectoUnitario { get; }

        public int? MatrizId { get; }

        public bool EsAgrupador { get; }

        /// <summary>Copia la base del concepto tal como la cargó la vista (antes de editar).</summary>
        public static BudgetConceptQuantityBase FromConcepto(ConceptoPresupuesto concepto)
        {
            ArgumentNullException.ThrowIfNull(concepto);

            return new BudgetConceptQuantityBase(
                concepto.Cantidad,
                concepto.CostoDirectoUnitario,
                concepto.MatrizId,
                concepto.EsAgrupador);
        }
    }
}
