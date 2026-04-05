using System;
using System.Collections.Generic;

namespace SOPRO.Core.Entities
{
    public class ActividadProgramada
    {
        public int Id { get; set; }

        public int ProgramaObraId { get; set; }
        public virtual ProgramaObra ProgramaObra { get; set; } = null!;

        public int? ConceptoPresupuestoId { get; set; }
        public virtual ConceptoPresupuesto? ConceptoPresupuesto { get; set; }

        public int? ActividadPadreId { get; set; }
        public virtual ActividadProgramada? ActividadPadre { get; set; }
        public virtual ICollection<ActividadProgramada> Hijas { get; set; } = new List<ActividadProgramada>();

        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;

        public bool EsResumen { get; set; }
        public bool EsHito { get; set; }
        public bool EsManual { get; set; }

        public int Nivel { get; set; } = 1;
        public int Orden { get; set; }

        public decimal CantidadTotal { get; set; }
        public decimal CantidadProgramada { get; set; }
        public decimal AvanceProgramadoPorcentaje { get; set; }

        public decimal PrecioUnitario { get; set; }
        public decimal ImporteTotal { get; set; }
        public decimal ImporteProgramado { get; set; }

        public DateTime? FechaInicioTemprana { get; set; }
        public DateTime? FechaFinTemprana { get; set; }
        public DateTime? FechaInicioTardia { get; set; }
        public DateTime? FechaFinTardia { get; set; }

        public DateTime? FechaInicioProgramada { get; set; }
        public DateTime? FechaFinProgramada { get; set; }

        public int DuracionDiasNaturales { get; set; }
        public int DuracionDiasHabiles { get; set; }

        public decimal RendimientoDiario { get; set; }
        public int FrentesTrabajo { get; set; } = 1;

        public TipoRestriccionActividad TipoRestriccion { get; set; } = TipoRestriccionActividad.LoAntesPosible;
        public DateTime? FechaRestriccion { get; set; }

        public MetodoDistribucionActividad MetodoDistribucion { get; set; } = MetodoDistribucionActividad.Uniforme;

        public bool RutaCritica { get; set; }
        public int HolguraDias { get; set; }

        public string Notas { get; set; } = string.Empty;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        public virtual ICollection<DependenciaActividad> Predecesoras { get; set; } = new List<DependenciaActividad>();
        public virtual ICollection<DependenciaActividad> Sucesoras { get; set; } = new List<DependenciaActividad>();
        public virtual ICollection<DistribucionPeriodo> Distribuciones { get; set; } = new List<DistribucionPeriodo>();
    }
}
