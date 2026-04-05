using System.Collections.Generic;

namespace SOPRO.Application.Models.Presupuesto
{
    public sealed class BudgetClipboardPastePlan
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<BudgetClipboardPasteRow> Rows { get; set; } = new();
    }
}
