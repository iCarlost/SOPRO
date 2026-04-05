using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Material - Puede ser del catálogo maestro o específico del proyecto
    /// </summary>
    public class Material
    {
        public int Id { get; set; }
        
        // Identificación
        public string Clave { get; set; }
        public string Descripcion { get; set; }
        public string Unidad { get; set; }
        
        // Precio
        public decimal PrecioUnitario { get; set; }
        
        // Origen del material
        public OrigenInsumo Origen { get; set; }
        
        // Si viene del catálogo maestro, referencia al ID del maestro
        public int? MaterialMaestroId { get; set; }
        
        // Relación con el proyecto (null si es catálogo maestro)
        public int? ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }
        
        // Notas adicionales
        public string Notas { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        
        public Material()
        {
            FechaCreacion = DateTime.Now;
            FechaModificacion = DateTime.Now;
            Origen = OrigenInsumo.Proyecto;
        }
    }
    
    /// <summary>
    /// Indica si el insumo proviene del catálogo maestro o es específico del proyecto
    /// </summary>
    public enum OrigenInsumo
    {
        Maestro = 0,    // 🌐 Catálogo global/maestro
        Proyecto = 1    // 📁 Específico del proyecto
    }
}
