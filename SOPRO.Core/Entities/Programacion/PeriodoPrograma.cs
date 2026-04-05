using System;
using System.Collections.Generic;

namespace SOPRO.Core.Entities
{
    public class PeriodoPrograma
    {
        public int Id { get; set; }

        public int ProgramaObraId { get; set; }
        public virtual ProgramaObra ProgramaObra { get; set; } = null!;

        public int NumeroPeriodo { get; set; }
        public string Etiqueta { get; set; } = string.Empty;

        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        public bool EsCerrado { get; set; }

        public virtual ICollection<DistribucionPeriodo> Distribuciones { get; set; } = new List<DistribucionPeriodo>();
    }
}
