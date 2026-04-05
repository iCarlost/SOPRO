using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class ActivityEditDto
    {
        public int? Id { get; set; }
        public int ProgramaObraId { get; set; }
        public int? ConceptoPresupuestoId { get; set; }
        public int? ActividadPadreId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal CantidadTotal { get; set; }
        public decimal PrecioUnitario { get; set; }
        public DateTime? FechaInicioProgramada { get; set; }
        public DateTime? FechaFinProgramada { get; set; }
        public int DuracionDiasHabiles { get; set; }
        public decimal RendimientoDiario { get; set; }
        public int FrentesTrabajo { get; set; } = 1;
        public bool EsResumen { get; set; }
        public bool EsManual { get; set; }
        public int Nivel { get; set; } = 1;
        public int Orden { get; set; }
        public MetodoDistribucionActividad MetodoDistribucion { get; set; } = MetodoDistribucionActividad.Uniforme;
    }
}
