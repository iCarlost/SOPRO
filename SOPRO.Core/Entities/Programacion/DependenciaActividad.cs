using System;

namespace SOPRO.Core.Entities
{
    public class DependenciaActividad
    {
        public int Id { get; set; }

        public int ActividadOrigenId { get; set; }
        public virtual ActividadProgramada ActividadOrigen { get; set; } = null!;

        public int ActividadDestinoId { get; set; }
        public virtual ActividadProgramada ActividadDestino { get; set; } = null!;

        public TipoDependenciaActividad TipoDependencia { get; set; } = TipoDependenciaActividad.FS;
        public int DesfaseDias { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
