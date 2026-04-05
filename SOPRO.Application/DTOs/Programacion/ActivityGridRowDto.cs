namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class ActivityGridRowDto
    {
        public int Id { get; set; }
        public int? ActividadPadreId { get; set; }
        public int? ConceptoPresupuestoId { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Unidad { get; set; } = string.Empty;
        public decimal CantidadTotal { get; set; }
        public DateTime? FechaInicioProgramada { get; set; }
        public DateTime? FechaFinProgramada { get; set; }
        public int DuracionDiasHabiles { get; set; }
        public decimal RendimientoDiario { get; set; }
        public int FrentesTrabajo { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal ImporteTotal { get; set; }
        public bool RutaCritica { get; set; }
        public string PredecesoraResumen { get; set; } = string.Empty;
        public int Nivel { get; set; }
        public bool EsResumen { get; set; }
        public int Orden { get; set; }
    }
}
