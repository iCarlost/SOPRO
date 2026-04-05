using System.Collections.Generic;

namespace SOPRO.Application.Models.Presupuesto
{
    public sealed class BudgetClipboardPasteRow
    {
        public int RowOffset { get; set; }
        public Dictionary<string, string> ValuesByColumn { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
