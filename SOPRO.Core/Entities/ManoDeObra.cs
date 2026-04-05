using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Mano de Obra - Personal para los análisis
    /// </summary>
    public class ManoDeObra
    {
        public int Id { get; set; }
        
        // Identificación
        public string Clave { get; set; }
        public string Descripcion { get; set; }
        public string Unidad { get; set; } // jor (jornada), hora, etc.
        
        // Salarios
        public decimal SalarioBase { get; set; }
        public decimal FactorSalarioReal { get; set; } // FSR aplicado
        public decimal SalarioReal { get; set; } // Calculado = SalarioBase * FSR
        
        // Origen
        public OrigenInsumo Origen { get; set; }
        public int? ManoDeObraMaestraId { get; set; }
        
        // Relación con el proyecto
        public int? ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }
        
        // Notas
        public string Notas { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        
        public ManoDeObra()
        {
            FechaCreacion = DateTime.Now;
            FechaModificacion = DateTime.Now;
            Origen = OrigenInsumo.Proyecto;
            Unidad = "jor"; // jornada por defecto
            FactorSalarioReal = 1.6543m; // Default FSR
        }
        
        /// <summary>
        /// Recalcula el salario real basado en el base y el FSR
        /// </summary>
        public void CalcularSalarioReal()
        {
            SalarioReal = SalarioBase * FactorSalarioReal;
        }
        
        /// <summary>
        /// Indica si es un insumo basado en porcentaje de mano de obra (Cabo de Oficio)
        /// </summary>
        public bool EsPorcentajeMO => Unidad?.Trim().ToUpper() == "%MO";
    }
}
