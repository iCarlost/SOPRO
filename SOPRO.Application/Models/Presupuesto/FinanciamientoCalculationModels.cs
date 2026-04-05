using System;
using System.Collections.Generic;

namespace SOPRO.Application.Models.Presupuesto
{
    public class FinanciamientoCalculationInput
    {
        public int ProyectoId { get; set; }
        public int? ProgramaObraId { get; set; }
        public decimal PorcentajeAnticipo { get; set; }
        public decimal PorcentajeAmortizacion { get; set; }
        public decimal TasaInteresAnual { get; set; }
        public decimal TasaInteresMensual { get; set; }
        public int PeriodoEntregaAnticipo { get; set; } = 1;
        public int PeriodosDesfaseCobro { get; set; } = 1;
        public decimal OtrosEgresos { get; set; }
        public string IndicadorEconomico { get; set; } = "TIIE 28";
        public bool ConsiderarInteresAFavor { get; set; } = true;
    }

    public class FinanciamientoPeriodRow
    {
        public int NumeroPeriodo { get; set; }
        public string Etiqueta { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal ObraEjecutada { get; set; }
        public decimal Anticipo { get; set; }
        public decimal Estimacion { get; set; }
        public decimal AmortizacionAnticipo { get; set; }
        public decimal Cobros { get; set; }
        public decimal Gastos { get; set; }
        public decimal DiferenciaPeriodo { get; set; }
        public decimal DiferenciaAcumulada { get; set; }
        public decimal InteresAPagar { get; set; }
        public decimal InteresAFavor { get; set; }
    }

    public class FinanciamientoCalculationResult
    {
        public int? ProgramaObraIdUsado { get; set; }
        public string ProgramaNombre { get; set; } = string.Empty;
        public string TipoPeriodoTexto { get; set; } = string.Empty;
        public List<FinanciamientoPeriodRow> Rows { get; set; } = new();
        public decimal TotalObraEjecutada { get; set; }
        public decimal TotalAnticipo { get; set; }
        public decimal TotalEstimacion { get; set; }
        public decimal TotalAmortizacion { get; set; }
        public decimal TotalCobros { get; set; }
        public decimal TotalGastos { get; set; }
        public decimal TotalInteresAPagar { get; set; }
        public decimal TotalInteresAFavor { get; set; }
        public decimal InteresNeto => TotalInteresAPagar - TotalInteresAFavor;
        public decimal PorcentajeFinanciamiento { get; set; }
        public decimal TasaInteresMensualUtilizada { get; set; }
        public decimal TasaInteresAnualUtilizada { get; set; }
        public string IndicadorEconomico { get; set; } = string.Empty;
        public bool CalculadoConPrograma { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
