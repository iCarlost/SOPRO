namespace SOPRO.Application.Models.Presupuesto
{
    public class BudgetExcelImportColumnMapping
    {
        public int? ColumnaClave { get; set; }
        public int ColumnaDescripcion { get; set; }
        public int? ColumnaUnidad { get; set; }
        public int? ColumnaCantidad { get; set; }
        public int? ColumnaTipo { get; set; }
        public bool PrimeraFilaEsEncabezado { get; set; } = true;
    }
}
