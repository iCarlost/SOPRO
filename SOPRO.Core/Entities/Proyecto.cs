using System;
using System.Collections.Generic;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Proyecto u Obra - Entidad principal que contiene todo el presupuesto
    /// </summary>
    public class Proyecto
    {
        public int Id { get; set; }
        
        // Datos generales
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Ubicacion { get; set; }
        
        // Participantes
        public string Convocante { get; set; }
        public string Contratista { get; set; }
        public string ApoderadoLegal { get; set; }
        
        // Fechas y plazo
        public DateTime FechaInicio { get; set; }
        public DateTime FechaTermino { get; set; }
        public int PlazoEjecucion { get; set; } // en días
        
        // Parámetros del proyecto
        public decimal PorcentajeIndirectosCentral { get; set; }
        public decimal PorcentajeIndirectosCampo { get; set; }
        public decimal PorcentajeFinanciamiento { get; set; }
        public decimal PorcentajeUtilidad { get; set; }
        public decimal PorcentajeCargosAdicionales { get; set; }
        public decimal PorcentajeIVA { get; set; }
        
        /// <summary>
        /// Modo de cálculo de porcentajes: "Acumulables" (cascada) o "SobreCD" (todos sobre CD)
        /// </summary>
        public string ModoCalculoPorcentajes { get; set; } = "Acumulables";
        
        // Configuración de formato numérico
        public int DecimalesCantidad { get; set; } = 2;
        public int DecimalesImporte { get; set; } = 2;
        public int DecimalesPorcentaje { get; set; } = 4;
        
        // Factor de Salario Real
        public decimal FactorSalarioReal { get; set; }
        public DateTime? FechaCalculoFSR { get; set; }
        /// <summary>JSON con los parámetros capturados en el FormFSR para restaurarlos al reabrir.</summary>
        public string? ParametrosFSR { get; set; }
        
        // Preferencias UI del proyecto
        public bool BarraLateralColapsada { get; set; }
        public string? NodosMenuExpandidos { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        
        // Relaciones
        public virtual ICollection<Material> Materiales { get; set; }
        public virtual ICollection<ManoDeObra> ManoDeObra { get; set; }
        public virtual ICollection<Maquinaria> Maquinaria { get; set; }
        public virtual ICollection<ConceptoPresupuesto> Conceptos { get; set; }
        public virtual ICollection<ProgramaObra> ProgramasObra { get; set; }
        public virtual ICollection<CalendarioLaboral> CalendariosLaborales { get; set; }
        
        public Proyecto()
        {
            Materiales = new List<Material>();
            ManoDeObra = new List<ManoDeObra>();
            Maquinaria = new List<Maquinaria>();
            Conceptos = new List<ConceptoPresupuesto>();
            ProgramasObra = new List<ProgramaObra>();
            CalendariosLaborales = new List<CalendarioLaboral>();
            
            FechaCreacion = DateTime.Now;
            FechaModificacion = DateTime.Now;
            
            // Valores por defecto
            PorcentajeIndirectosCentral = 5.00m;
            PorcentajeIndirectosCampo = 5.00m;
            PorcentajeUtilidad = 10.00m;
            PorcentajeIVA = 16.00m;
            FactorSalarioReal = 1.6543m;
        }
    }
}
