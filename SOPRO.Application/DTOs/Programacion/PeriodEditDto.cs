namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class PeriodEditDto
    {
        public int? Id { get; set; }
        public int NumeroPeriodo { get; set; }
        public string Etiqueta { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public bool EsCerrado { get; set; }
    }
}
