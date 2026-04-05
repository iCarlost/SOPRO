namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class PeriodDistributionInput
    {
        public int PeriodoProgramaId { get; set; }
        public decimal CantidadProgramada { get; set; }
        public decimal PorcentajeProgramado { get; set; }
    }
}
