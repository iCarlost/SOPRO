using System;
using System.Collections.Generic;

namespace SOPRO.Application.DTOs.Programacion.Insumos
{
    public enum ProgramaInsumoTipo
    {
        Materiales = 0,
        ManoDeObra = 1,
        Maquinaria = 2,
        Herramienta = 3
    }

    public sealed class ProgramaInsumoPeriodoDto
    {
        public int PeriodoId { get; set; }
        public int Orden { get; set; }
        public string Etiqueta { get; set; } = string.Empty;
    }

    public sealed class ProgramaInsumoRowDto
    {
        public int InsumoId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string DondeSeUsa { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        /// <summary>
        /// Rendimiento ponderado del equipo (unidades de obra / hr-máquina).
        /// Solo significativo cuando el tipo de insumo es Maquinaria.
        /// Se calcula como promedio ponderado por cantidad acumulada entre todos los APUs.
        /// </summary>
        public decimal Rendimiento { get; set; }
        public decimal Total { get; set; }
        public decimal ImporteTotal { get; set; }
        public Dictionary<int, decimal> CantidadesPorPeriodo { get; set; } = new();
        public Dictionary<int, decimal> AcumuladosPorPeriodo { get; set; } = new();
        public Dictionary<int, decimal> ImportesPorPeriodo { get; set; } = new();
        public Dictionary<int, decimal> ImportesAcumuladosPorPeriodo { get; set; } = new();
    }

    public sealed class ProgramaInsumosResultDto
    {
        public string NombrePrograma { get; set; } = string.Empty;
        public ProgramaInsumoTipo Tipo { get; set; }
        public List<ProgramaInsumoPeriodoDto> Periodos { get; set; } = new();
        public List<ProgramaInsumoRowDto> Rows { get; set; } = new();
    }
}
