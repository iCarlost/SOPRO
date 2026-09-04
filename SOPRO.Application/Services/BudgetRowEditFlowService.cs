using Sopro.Calculation;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  BudgetRowEditFlowService — delegador directo al motor                   ║
    // ║  [N5-5] Toda aritmética delegada a SoproCalculationEngine                ║
    // ║         (SOPRO.Calculation), sin pasar por la fachada legacy.            ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public static class BudgetRowEditFlowService
    {
        public static BudgetRowTypeChangeResult HandleTypeCellChange(SOPROContext context, Proyecto proyecto, BudgetRowTypeChangeInput input)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (input == null) throw new ArgumentNullException(nameof(input));

            string tipo = input.Tipo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tipo))
                return new BudgetRowTypeChangeResult();

            bool esAgrupador = !string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase);
            int level = BudgetHierarchyService.GetLevelFromType(tipo);
            ConceptoPresupuesto? concept = input.ExistingConcept;

            if (concept != null)
            {
                if (concept.Nivel == level && concept.EsAgrupador == esAgrupador)
                {
                    return new BudgetRowTypeChangeResult
                    {
                        Handled = true,
                        IgnoredBecauseStateIsAlreadyCorrect = true,
                        Level = level,
                        IsAggregator = esAgrupador,
                        Concept = concept
                    };
                }

                BudgetConceptCreationService.UpdateExistingTypeState(context, concept, esAgrupador, level);
            }
            else
            {
                concept = BudgetConceptCreationService.CreateNewForTypeChange(context, proyecto.Id, input, esAgrupador, level);
            }

            return new BudgetRowTypeChangeResult
            {
                Handled = true,
                Level = level,
                IsAggregator = esAgrupador,
                Concept = concept,
                RequiresRowInvalidate = true,
                RequiresReassignSequence = true,
                UnitReadOnly = esAgrupador,
                QuantityReadOnly = esAgrupador,
                ClearCalculatedCells = esAgrupador,
                UnitValue = esAgrupador ? string.Empty : input.Unidad ?? string.Empty,
                QuantityValue = esAgrupador ? string.Empty : string.Empty,
                UnitPriceValue = esAgrupador ? string.Empty : string.Empty,
                AmountValue = esAgrupador ? string.Empty : string.Empty
            };
        }

        public static BudgetQuantityChangeResult HandleQuantityCellChange(Proyecto proyecto, ConceptoPresupuesto? concepto, string? cantidadTexto)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (concepto == null || !concepto.MatrizId.HasValue)
                return new BudgetQuantityChangeResult();

            if (string.IsNullOrWhiteSpace(cantidadTexto) || !decimal.TryParse(cantidadTexto, out decimal cantidad))
                return new BudgetQuantityChangeResult();

            var engine = CalculationEngineFactory.FromProyecto(proyecto);
            // Normalizar la cantidad inmediatamente a la precisión visible del proyecto
            // para evitar fugas de precisión: lo que el usuario ve = lo que se calcula
            cantidad = engine.RoundQuantity(cantidad);

            decimal puFinal = BudgetPricingService.CalculateUnitPrice(proyecto, concepto.CostoDirectoUnitario);
            decimal importe  = engine.Multiply(cantidad, puFinal);
            decimal subtotal = importe;
            // PorcentajeIVA del proyecto (default 16 en el constructor de Proyecto).
            // Si el usuario lo puso en 0 = sin IVA. No aplicar fallback implícito.
            decimal tasaIva  = proyecto.PorcentajeIVA / 100m;
            decimal iva      = engine.RoundAmount(subtotal * tasaIva);
            decimal total    = engine.RoundAmount(subtotal + iva);

            return new BudgetQuantityChangeResult
            {
                HasChanges = true,
                Cantidad = cantidad,
                PrecioUnitario = puFinal,
                Importe = importe,
                Subtotal = subtotal,
                Iva = iva,
                Total = total,
                PrecioUnitarioLetra = BudgetPricingService.ConvertirALetras(puFinal),
                TotalLetra = BudgetPricingService.ConvertirALetras(total)
            };
        }
    }
}
