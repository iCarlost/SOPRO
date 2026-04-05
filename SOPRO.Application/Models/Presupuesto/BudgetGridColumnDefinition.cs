using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetGridColumnDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string HeaderText { get; set; } = string.Empty;
        public string NombreInterno { get; set; } = string.Empty;
        public int Width { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsReadOnly { get; set; }
        public bool IsTypeSelector { get; set; }
        public bool IsFillColumn { get; set; }
        public bool AlignRight { get; set; }
        public bool AlignCenter { get; set; }
        public bool UseCalculatedBackColor { get; set; }
        public int? DisplayIndex { get; set; }
        public TipoDatoColumna TipoDato { get; set; } = TipoDatoColumna.Texto;
        public ColumnaPersonalizada? SourceColumn { get; set; }
    }
}
