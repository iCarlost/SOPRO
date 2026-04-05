using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class ProgramLoadDto
    {
        public int ProgramaObraId { get; set; }
        public int ProyectoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaInicioPrograma { get; set; }
        public DateTime? FechaFinPrograma { get; set; }
        public TipoPeriodoPrograma TipoPeriodo { get; set; }
        public int? CalendarioLaboralId { get; set; }
        public List<ActivityGridRowDto> Actividades { get; set; } = new();
        public List<PeriodEditDto> Periodos { get; set; } = new();
    }
}
