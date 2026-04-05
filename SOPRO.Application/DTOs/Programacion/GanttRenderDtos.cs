using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Programacion
{
    public enum GanttViewMode
    {
        ProgramaObra = 1,
        Erogaciones = 2,
        Mixto = 3
    }

    public sealed class GanttRowDto
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime? Inicio { get; set; }
        public DateTime? Fin { get; set; }
        public bool EsResumen { get; set; }
        public bool EsCritica { get; set; }
        public int Nivel { get; set; }
        public int Orden { get; set; }
        public decimal ImporteProgramadoTotal { get; set; }
        public bool TieneImporteProgramado { get; set; }
        public List<GanttPeriodSegmentDto> SegmentosFinancieros { get; set; } = new();
    }

    public sealed class GanttPeriodSegmentDto
    {
        public int PeriodoProgramaId { get; set; }
        public string EtiquetaPeriodo { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal CantidadProgramada { get; set; }
        public decimal PorcentajeProgramado { get; set; }
        public decimal ImporteProgramado { get; set; }
        public decimal PorcentajeFisicoGlobal { get; set; }
        public decimal PorcentajeFinancieroGlobal { get; set; }
    }

    public sealed class GanttScaleCellDto
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Etiqueta { get; set; } = string.Empty;
        public string GrupoEtiqueta { get; set; } = string.Empty;
    }




    public enum GanttSegmentLabelPosition
    {
        Arriba = 0,
        Abajo = 1
    }

    public enum GanttFooterDisplayMode
    {
        Ninguno = 0,
        ImportePeriodo = 1,
        ImporteAcumulado = 2,
        PorcentajePeriodo = 3,
        PorcentajeAcumulado = 4
    }

    public sealed class GanttFooterPeriodDto
    {
        public string Etiqueta { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal ImportePeriodo { get; set; }
        public decimal ImporteAcumulado { get; set; }
        public decimal PorcentajePeriodo { get; set; }
        public decimal PorcentajeAcumulado { get; set; }
    }

    public sealed class GanttRenderModel
    {
        public GanttViewMode ViewMode { get; set; } = GanttViewMode.ProgramaObra;
        public GanttSegmentLabelPosition SegmentLabelPosition { get; set; } = GanttSegmentLabelPosition.Arriba;
        public TipoPeriodoPrograma TipoPeriodo { get; set; }
        public DateTime FechaInicioRango { get; set; }
        public DateTime FechaFinRango { get; set; }
        public List<GanttScaleCellDto> Escala { get; set; } = new();
        public List<GanttRowDto> Filas { get; set; } = new();
        public List<GanttFooterPeriodDto> FooterPeriodos { get; set; } = new();
    }
}
