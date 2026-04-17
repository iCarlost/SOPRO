using System;

namespace SOPRO.Application.DTOs.Presupuesto
{
    public class BudgetExcelImportRowDto
    {
        public int SourceRowNumber { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal? Cantidad { get; set; }
        public string TipoTexto { get; set; } = string.Empty;
        public bool EsAgrupadorDetectado { get; set; }
    }
}
