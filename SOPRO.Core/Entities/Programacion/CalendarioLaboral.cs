using System;
using System.Collections.Generic;

namespace SOPRO.Core.Entities
{
    public class CalendarioLaboral
    {
        public int Id { get; set; }

        public int ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; } = null!;

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

        public bool Activo { get; set; } = true;

        public virtual ICollection<ExcepcionCalendario> Excepciones { get; set; } = new List<ExcepcionCalendario>();
    }
}
