namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class CurvaSRowDto
    {
        public int NumeroPeriodo { get; set; }
        public string Etiqueta { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal CantidadPeriodo { get; set; }
        public decimal CantidadAcumulada { get; set; }
        public decimal PorcentajeFisicoPeriodo { get; set; }
        public decimal PorcentajeFisicoAcumulado { get; set; }
        public decimal ImportePeriodo { get; set; }
        public decimal ImporteAcumulado { get; set; }
        public decimal PorcentajePeriodo { get; set; }
        public decimal PorcentajeAcumulado { get; set; }
    }
}
