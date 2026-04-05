namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class ProgramSummaryDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaInicioPrograma { get; set; }
        public DateTime? FechaFinPrograma { get; set; }
        public int Actividades { get; set; }
    }
}
