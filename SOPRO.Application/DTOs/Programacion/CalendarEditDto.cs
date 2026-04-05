using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class CalendarEditDto
    {
        public int? Id { get; set; }
        public int ProyectoId { get; set; }
        public string Nombre { get; set; } = "Calendario General";
        public bool Lunes { get; set; } = true;
        public bool Martes { get; set; } = true;
        public bool Miercoles { get; set; } = true;
        public bool Jueves { get; set; } = true;
        public bool Viernes { get; set; } = true;
        public bool Sabado { get; set; }
        public bool Domingo { get; set; }
        public TimeSpan HoraInicio { get; set; } = new TimeSpan(8, 0, 0);
        public TimeSpan HoraFin { get; set; } = new TimeSpan(18, 0, 0);
        public List<CalendarExceptionEditDto> Excepciones { get; set; } = new();
    }

    public sealed class CalendarExceptionEditDto
    {
        public int? Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Today;
        public string Descripcion { get; set; } = string.Empty;
        public TipoExcepcionCalendario Tipo { get; set; } = TipoExcepcionCalendario.Inhabil;
    }
}
