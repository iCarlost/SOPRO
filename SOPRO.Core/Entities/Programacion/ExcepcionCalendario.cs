using System;

namespace SOPRO.Core.Entities
{
    public class ExcepcionCalendario
    {
        public int Id { get; set; }

        public int CalendarioLaboralId { get; set; }
        public virtual CalendarioLaboral CalendarioLaboral { get; set; } = null!;

        public DateTime Fecha { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public TipoExcepcionCalendario Tipo { get; set; } = TipoExcepcionCalendario.Inhabil;
    }
}
