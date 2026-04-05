namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Concepto de indirecto dentro de un grupo
    /// Representa un rubro específico de gasto (ej: "Gerente General", "Renta de oficina")
    /// </summary>
    public class ConceptoIndirecto
    {
        public int Id { get; set; }
        
        public int GrupoIndirectoId { get; set; }
        public GrupoIndirecto GrupoIndirecto { get; set; }
        
        /// <summary>
        /// Nombre del concepto (ej: "Gerente General", "Renta de oficina")
        /// </summary>
        public string Concepto { get; set; } = string.Empty;
        
        /// <summary>
        /// Tipo: Oficina Central o Campo
        /// </summary>
        public TipoIndirecto Tipo { get; set; }
        
        /// <summary>
        /// Importe mensual del concepto
        /// </summary>
        public decimal ImporteMensual { get; set; }
        
        /// <summary>
        /// Duración en meses (para indirectos de campo)
        /// </summary>
        public int DuracionMeses { get; set; } = 1;
        
        /// <summary>
        /// Importe total = ImporteMensual × DuracionMeses
        /// </summary>
        public decimal ImporteTotal => ImporteMensual * DuracionMeses;
        
        /// <summary>
        /// Orden de presentación
        /// </summary>
        public int Orden { get; set; }
        
        /// <summary>
        /// Indica si está activo
        /// </summary>
        public bool Activo { get; set; } = true;
    }
    
    /// <summary>
    /// Grupo de conceptos de indirectos
    /// Ejemplos: "Honorarios y Sueldos", "Alquileres y Depreciaciones"
    /// </summary>
    public class GrupoIndirecto
    {
        public int Id { get; set; }
        
        public int ProyectoId { get; set; }
        public Proyecto Proyecto { get; set; }
        
        /// <summary>
        /// Nombre del grupo (ej: "HONORARIOS, SUELDOS Y PRESTACIONES")
        /// </summary>
        public string Nombre { get; set; } = string.Empty;
        
        /// <summary>
        /// Tipo: Oficina Central o Campo
        /// </summary>
        public TipoIndirecto Tipo { get; set; }
        
        /// <summary>
        /// Orden de presentación
        /// </summary>
        public int Orden { get; set; }
        
        /// <summary>
        /// Conceptos dentro de este grupo
        /// </summary>
        public List<ConceptoIndirecto> Conceptos { get; set; } = new List<ConceptoIndirecto>();
        
        /// <summary>
        /// Total del grupo
        /// </summary>
        public decimal Total => Conceptos.Where(c => c.Activo).Sum(c => c.ImporteTotal);
    }
    
    /// <summary>
    /// Tipo de indirecto
    /// </summary>
    public enum TipoIndirecto
    {
        OficinaCentral = 1,
        Campo = 2
    }
    
    /// <summary>
    /// Configuración de indirectos del proyecto
    /// Almacena los porcentajes calculados
    /// </summary>
    public class ConfiguracionIndirectos
    {
        public int Id { get; set; }
        
        public int ProyectoId { get; set; }
        public Proyecto Proyecto { get; set; }
        
        /// <summary>
        /// Volumen anual de obra estimado a costo directo (para prorratear oficina central)
        /// </summary>
        public decimal VolumenAnualObra { get; set; }
        
        /// <summary>
        /// Costo directo de esta obra
        /// </summary>
        public decimal CostoDirectoObra { get; set; }
        
        /// <summary>
        /// Total de gastos de oficina central anual
        /// </summary>
        public decimal TotalOficinaCentralAnual { get; set; }
        
        /// <summary>
        /// Total de gastos de campo para esta obra
        /// </summary>
        public decimal TotalCampo { get; set; }
        
        /// <summary>
        /// Porcentaje de oficina central calculado
        /// = (TotalOficinaCentralAnual / VolumenAnualObra) × 100
        /// </summary>
        public decimal PorcentajeOficinaCentral { get; set; }
        
        /// <summary>
        /// Porcentaje de campo calculado
        /// = (TotalCampo / CostoDirectoObra) × 100
        /// </summary>
        public decimal PorcentajeCampo { get; set; }
        
        /// <summary>
        /// Porcentaje total de indirectos
        /// = PorcentajeOficinaCentral + PorcentajeCampo
        /// </summary>
        public decimal PorcentajeTotal => PorcentajeOficinaCentral + PorcentajeCampo;
        
        /// <summary>
        /// Fecha de última actualización
        /// </summary>
        public DateTime FechaActualizacion { get; set; } = DateTime.Now;
    }
}
