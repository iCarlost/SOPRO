using Sopro.Calculation;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  BudgetConceptAssignmentService — VERSIÓN CORREGIDA v2.0               ║
    // ║                                                                         ║
    // ║  [N5-4] Toda aritmética delegada directo a SoproCalculationEngine      ║
    // ║         (SOPRO.Calculation), sin pasar por la fachada legacy.          ║
    // ║                                                                         ║
    // ║  CAMBIOS RESPECTO A LA VERSIÓN ORIGINAL:                                ║
    // ║  [FIX-1] BuildDraftFromMatrix: CostoDirectoTotal usa Multiplicar        ║
    // ║          en lugar de  matriz.CostoDirecto * cantidad  sin redondear.    ║
    // ║  [FIX-2] BuildDraftFromMatrix: CostoDirectoUnitario pasa por            ║
    // ║          RoundAmount antes de persistir.                                ║
    // ║  [FIX-3] BuildDraftFromConcept: CostoDirectoTotal recalculado con       ║
    // ║          Multiplicar en lugar de copiar el valor sin actualizar.        ║
    // ║  [FIX-4] MultiplyUsingDisplayPrecision de BudgetPricingService          ║
    // ║          reemplazado por Multiplicar en ambos métodos Build*.           ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public static class BudgetConceptAssignmentService
    {
        public static BudgetConceptAssignmentResult ResolveByKey(
            SOPROContext context,
            Proyecto proyecto,
            int currentRowIndex,
            string key,
            ConceptoPresupuesto? existingConcept,
            string? currentQuantityText,
            IEnumerable<BudgetConceptKeyRowSnapshot> rows)
        {
            if (context == null)  throw new ArgumentNullException(nameof(context));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (rows == null)     throw new ArgumentNullException(nameof(rows));

            string normalizedKey      = (key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
                return new BudgetConceptAssignmentResult();

            string normalizedKeyUpper = normalizedKey.ToUpper();

            // ── Buscar en el grid actual (copia desde otra fila) ─────────────────
            var sourceRow = rows.FirstOrDefault(r =>
                r.RowIndex != currentRowIndex
                && !string.IsNullOrWhiteSpace(r.Key)
                && string.Equals(r.Key.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase)
                && r.Concept != null);

            if (sourceRow?.Concept != null)
            {
                var draft = BuildDraftFromConcept(proyecto, normalizedKey, sourceRow.Concept);
                bool requiresConfirmation = existingConcept != null && existingConcept.Id > 0;
                string sourceDescription = string.IsNullOrWhiteSpace(sourceRow.Description)
                    ? sourceRow.Concept.Descripcion ?? string.Empty
                    : sourceRow.Description;

                return new BudgetConceptAssignmentResult
                {
                    HasAssignment        = true,
                    RequiresConfirmation = requiresConfirmation,
                    RestoreKeyValue      = existingConcept?.Clave ?? string.Empty,
                    SourceRowIndex       = sourceRow.RowIndex,
                    SourceConcept        = sourceRow.Concept,
                    Draft                = draft,
                    ConfirmationMessage  = requiresConfirmation
                        ? $"La clave '{normalizedKey}' ya está asignada a:\n\n  → {sourceDescription}\n\n" +
                          $"¿Desea REEMPLAZAR este concepto con los datos del concepto que tiene clave '{normalizedKey}'?\n\n" +
                          "• SÍ = Copiar todos los datos (descripción, unidad, P.U., matriz)\n• NO = Cancelar (mantener clave anterior)"
                        : string.Empty
                };
            }

            // ── Buscar en el catálogo de matrices ────────────────────────────────
            var matriz = context.Matrices
                .FirstOrDefault(m => m.ProyectoId == proyecto.Id
                    && m.Clave != null
                    && m.Clave.Trim().ToUpper() == normalizedKeyUpper);

            if (matriz == null)
                return new BudgetConceptAssignmentResult();

            if (matriz.Tipo != TipoMatriz.APU)
            {
                return new BudgetConceptAssignmentResult
                {
                    IsInvalidMatrixTypeSelection = true,
                    InvalidSelectionMessage =
                        "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. " +
                        "Seleccione una matriz de tipo APU."
                };
            }

            decimal cantidad = ParseCantidadOrDefault(currentQuantityText, 1m);
            return new BudgetConceptAssignmentResult
            {
                HasAssignment = true,
                Draft = BuildDraftFromMatrix(proyecto, normalizedKey, matriz, cantidad)
            };
        }

        public static BudgetConceptAssignmentDraft BuildDraftFromSelectedMatrix(
            Proyecto proyecto,
            Matriz matriz,
            decimal cantidad,
            string? claveActual,
            string? descripcionActual,
            string? unidadActual)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (matriz == null)   throw new ArgumentNullException(nameof(matriz));

            string clave = string.IsNullOrWhiteSpace(claveActual)
                ? (matriz.Clave ?? string.Empty) : claveActual.Trim();
            var draft = BuildDraftFromMatrix(proyecto, clave, matriz, cantidad);

            if (!string.IsNullOrWhiteSpace(descripcionActual))
                draft.Descripcion = descripcionActual.Trim();
            if (!string.IsNullOrWhiteSpace(unidadActual))
                draft.Unidad = unidadActual.Trim();

            return draft;
        }

        public static ConceptoPresupuesto ApplyDraft(
            SOPROContext context,
            int proyectoId,
            int order,
            ConceptoPresupuesto? existingConcept,
            BudgetConceptAssignmentDraft draft)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (draft == null)   throw new ArgumentNullException(nameof(draft));

            ConceptoPresupuesto concept;
            if (existingConcept != null && existingConcept.Id > 0)
            {
                concept = existingConcept;
            }
            else
            {
                concept = new ConceptoPresupuesto
                {
                    ProyectoId                = proyectoId,
                    EsAgrupador               = false,
                    Nivel                     = 5,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas                     = string.Empty
                };
                context.ConceptosPresupuesto.Add(concept);
            }

            concept.Orden                = order;
            concept.EsAgrupador          = false;
            concept.Nivel                = 5;
            concept.Clave                = draft.Clave        ?? string.Empty;
            concept.Descripcion          = draft.Descripcion  ?? string.Empty;
            concept.Unidad               = draft.Unidad       ?? string.Empty;
            concept.Cantidad             = draft.Cantidad;
            concept.CostoDirectoUnitario = draft.CostoDirectoUnitario;
            concept.CostoDirectoTotal    = draft.CostoDirectoTotal;   // ya redondeado [FIX-1]
            concept.PrecioUnitario       = draft.PrecioUnitario;
            concept.ImporteTotal         = draft.ImporteTotal;
            concept.MatrizId             = draft.MatrizId;
            concept.Matriz               = draft.Matriz;
            concept.FechaModificacion    = DateTime.Now;

            context.SaveChanges();
            return concept;
        }

        // ════════════════════════════════════════════════════════════════════════
        // MÉTODOS BUILD PRIVADOS
        // ════════════════════════════════════════════════════════════════════════

        private static BudgetConceptAssignmentDraft BuildDraftFromConcept(
            Proyecto proyecto, string key, ConceptoPresupuesto sourceConcept)
        {
            var engine         = BuildEngine(proyecto);
            decimal cdUnit     = engine.RoundAmount(sourceConcept.CostoDirectoUnitario); // [FIX-2]
            decimal pu         = BudgetPricingService.CalculateUnitPrice(proyecto, cdUnit);
            decimal cantidad   = sourceConcept.Cantidad;
            decimal cdTotal    = engine.Multiply(cantidad, cdUnit);  // [FIX-3] recalculado
            decimal importe    = engine.Multiply(cantidad, pu);      // [FIX-4]

            return new BudgetConceptAssignmentDraft
            {
                Clave                = key,
                Descripcion          = sourceConcept.Descripcion ?? string.Empty,
                Unidad               = sourceConcept.Unidad      ?? string.Empty,
                Cantidad             = cantidad,
                CostoDirectoUnitario = cdUnit,
                CostoDirectoTotal    = cdTotal,
                PrecioUnitario       = pu,
                ImporteTotal         = importe,
                MatrizId             = sourceConcept.MatrizId,
                Matriz               = sourceConcept.Matriz
            };
        }

        private static BudgetConceptAssignmentDraft BuildDraftFromMatrix(
            Proyecto proyecto, string key, Matriz matriz, decimal cantidad)
        {
            var engine      = BuildEngine(proyecto);
            decimal cdUnit = engine.RoundAmount(matriz.CostoDirecto); // [FIX-2]
            decimal pu     = BudgetPricingService.CalculateUnitPrice(proyecto, cdUnit);
            decimal cdTotal = engine.Multiply(cantidad, cdUnit);         // [FIX-1] con Round
            decimal importe = engine.Multiply(cantidad, pu);             // [FIX-4]

            return new BudgetConceptAssignmentDraft
            {
                Clave                = key,
                Descripcion          = matriz.Descripcion ?? string.Empty,
                Unidad               = matriz.Unidad      ?? string.Empty,
                Cantidad             = cantidad,
                CostoDirectoUnitario = cdUnit,
                CostoDirectoTotal    = cdTotal,   // [FIX-1] era: matriz.CostoDirecto * cantidad sin Round
                PrecioUnitario       = pu,
                ImporteTotal         = importe,
                MatrizId             = matriz.Id,
                Matriz               = matriz
            };
        }

        private static decimal ParseCantidadOrDefault(string? raw, decimal defaultValue)
        {
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
            return decimal.TryParse(raw, out decimal cantidad) && cantidad > 0
                ? cantidad : defaultValue;
        }

        private static SoproCalculationEngine BuildEngine(Proyecto proyecto)
            => new SoproCalculationEngine(proyecto.DecimalesCantidad,
                                          proyecto.DecimalesImporte,
                                          proyecto.DecimalesPorcentaje);
    }
}
