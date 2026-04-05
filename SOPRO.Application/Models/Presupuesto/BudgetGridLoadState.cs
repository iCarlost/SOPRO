namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetGridLoadState
    {
        public List<BudgetGridColumnDefinition> ColumnDefinitions { get; set; } = new();
        public List<BudgetGridRowDisplay> RowDisplays { get; set; } = new();
        public bool UsesTemporaryBaseColumns { get; set; }
        public int EmptyRowsToAppend { get; set; } = 100;
    }
}
