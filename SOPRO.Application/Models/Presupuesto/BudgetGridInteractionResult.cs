namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetGridInteractionResult
    {
        public bool Handled { get; set; }
        public bool SuppressKeyPress { get; set; }
        public bool ShouldOpenApuSelector { get; set; }
        public bool ShouldBeginEdit { get; set; }
        public bool ShouldInsertConceptRow { get; set; }
        public int? InsertRowIndex { get; set; }
        public int? FocusRowIndex { get; set; }
        public bool HasDeletionPlan => RowsToDelete.Count > 0;
        public List<int> RowsToDelete { get; set; } = new();
        public string ConfirmationMessage { get; set; } = string.Empty;
    }
}
