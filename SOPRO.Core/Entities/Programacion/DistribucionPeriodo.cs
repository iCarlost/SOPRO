using System;

namespace SOPRO.Core.Entities
{
    public class DistribucionPeriodo
    {
        public int Id { get; set; }

        public int ActividadProgramadaId { get; set; }
        public virtual ActividadProgramada ActividadProgramada { get; set; } = null!;

        public int PeriodoProgramaId { get; set; }
        public virtual PeriodoPrograma PeriodoPrograma { get; set; } = null!;

        public decimal CantidadProgramada { get; set; }
        public decimal PorcentajeProgramado { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal ImporteProgramado { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime FechaModificacion { get; set; } = DateTime.Now;
    }
}
