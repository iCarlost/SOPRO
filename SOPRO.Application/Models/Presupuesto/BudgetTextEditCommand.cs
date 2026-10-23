using System;

namespace SOPRO.Application.Models.Presupuesto
{
    /// <summary>Campo de texto editable de un concepto YA persistido.</summary>
    public enum BudgetTextEditField
    {
        Descripcion,
        Unidad
    }

    /// <summary>
    /// Intención de edición de un campo de texto (Descripción o Unidad) de un concepto
    /// existente (ADR-0001). Lleva solo identidad, el campo afectado, el valor que la
    /// vista cargó y el valor propuesto; no transporta importes ni columnas calculadas.
    /// Todos sus valores son de solo lectura para que un reintento repita exactamente
    /// la misma operación.
    /// </summary>
    public sealed class BudgetTextEditCommand
    {
        public BudgetTextEditCommand(
            int projectId,
            int conceptId,
            BudgetTextEditField field,
            string expectedValue,
            string proposedValue)
        {
            if (projectId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectId));
            if (conceptId <= 0)
                throw new ArgumentOutOfRangeException(nameof(conceptId));
            if (!Enum.IsDefined(typeof(BudgetTextEditField), field))
                throw new ArgumentOutOfRangeException(nameof(field));

            ProjectId = projectId;
            ConceptId = conceptId;
            Field = field;
            ExpectedValue = expectedValue ?? throw new ArgumentNullException(nameof(expectedValue));
            ProposedValue = proposedValue ?? throw new ArgumentNullException(nameof(proposedValue));
        }

        public int ProjectId { get; }

        public int ConceptId { get; }

        public BudgetTextEditField Field { get; }

        /// <summary>Valor del campo que la vista cargó antes de la edición.</summary>
        public string ExpectedValue { get; }

        /// <summary>Valor propuesto por el usuario, sin normalizar ni convertir.</summary>
        public string ProposedValue { get; }
    }
}
