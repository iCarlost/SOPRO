using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>
    /// Fotografía de UNA fila del grid al momento de pegar: tipo, clave/cantidad
    /// visibles y la entidad persistida del Tag (solo lectura). Permite decidir si el
    /// lote es representable como comando sin mutar la vista.
    /// </summary>
    public sealed class BudgetClipboardPasteTarget
    {
        public BudgetClipboardPasteTarget(
            int rowIndex,
            string tipo,
            string currentKey,
            string currentQuantityText,
            ConceptoPresupuesto? concept)
        {
            RowIndex = rowIndex;
            Tipo = tipo ?? string.Empty;
            CurrentKey = currentKey ?? string.Empty;
            CurrentQuantityText = currentQuantityText ?? string.Empty;
            Concept = concept;
        }

        public int RowIndex { get; }

        public string Tipo { get; }

        public string CurrentKey { get; }

        public string CurrentQuantityText { get; }

        /// <summary>Entidad persistida del Tag; <c>null</c> si la fila aún no se guardó.</summary>
        public ConceptoPresupuesto? Concept { get; }
    }

    /// <summary>
    /// Operación normalizada de pegado para una fila: qué columnas se aceptaron y con
    /// qué valor normalizado. Los <c>null</c> significan "la columna no participó";
    /// las cadenas vacías sí son valores pegados (limpian la celda).
    /// </summary>
    public sealed class BudgetClipboardPasteOperation
    {
        public int RowIndex { get; init; }

        public string Tipo { get; init; } = string.Empty;

        public BudgetClipboardPasteTarget Target { get; init; } = null!;

        public string? Clave { get; set; }

        public string? Descripcion { get; set; }

        public string? Unidad { get; set; }

        public decimal? Cantidad { get; set; }

        /// <summary>
        /// <c>true</c> si la fila aporta al menos un valor normalizado. Una fila cuyas
        /// celdas se omitieron por completo no debe representarse como operación: un
        /// comando "vacío" solo sellaría FechaModificacion sin cambiar nada.
        /// </summary>
        public bool HasValues
            => Clave is not null || Descripcion is not null || Unidad is not null || Cantidad.HasValue;
    }

    /// <summary>
    /// Resultado de simular el pegado sobre las filas de destino: operaciones
    /// normalizadas, celdas omitidas por restricción/valor inválido y, si el lote no es
    /// representable, el motivo accionable ANTES de tocar la vista o escribir nada.
    /// </summary>
    public sealed class BudgetClipboardPasteOperationPlan
    {
        public bool IsValid { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;

        public IReadOnlyList<BudgetClipboardPasteOperation> Operations { get; set; }
            = Array.Empty<BudgetClipboardPasteOperation>();

        public int SkippedCells { get; set; }
    }
}
