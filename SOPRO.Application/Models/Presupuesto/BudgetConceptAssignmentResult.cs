using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    public sealed class BudgetConceptAssignmentResult
    {
        public bool HasAssignment { get; set; }
        public bool RequiresConfirmation { get; set; }
        public string ConfirmationMessage { get; set; } = string.Empty;
        public string RestoreKeyValue { get; set; } = string.Empty;
        public int SourceRowIndex { get; set; } = -1;
        public ConceptoPresupuesto? SourceConcept { get; set; }
        public BudgetConceptAssignmentDraft? Draft { get; set; }
        public bool IsInvalidMatrixTypeSelection { get; set; }
        public string InvalidSelectionMessage { get; set; } = string.Empty;
    }
}
