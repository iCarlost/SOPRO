using System;
using System.Collections.Generic;

namespace SOPRO.Core.Entities
{
    public class ProgramaObra
    {
        public int Id { get; set; }
        public int ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; } = null!;

        public string Nombre { get; set; } = "Programa Base";
        public string Descripcion { get; set; } = string.Empty;

        public DateTime FechaInicioPrograma { get; set; } = DateTime.Today;
        public DateTime? FechaFinPrograma { get; set; }

        public TipoPeriodoPrograma TipoPeriodo { get; set; } = TipoPeriodoPrograma.Semana;
        public int DuracionPeriodoDias { get; set; } = 7;

        public int? CalendarioLaboralId { get; set; }
        public virtual CalendarioLaboral? CalendarioLaboral { get; set; }

        public bool Activo { get; set; } = true;
        public bool GeneradoDesdePresupuesto { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        public virtual ICollection<ActividadProgramada> Actividades { get; set; } = new List<ActividadProgramada>();
        public virtual ICollection<PeriodoPrograma> Periodos { get; set; } = new List<PeriodoPrograma>();
    }
}
