using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Representa una herramienta o equipo menor de construcción.
    /// Puede ser herramienta física o porcentaje de MO (herramienta menor, cabo de oficio).
    /// </summary>
    [Table("Herramientas")]
    public class Herramienta
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProyectoId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Clave { get; set; }

        [Required]
        [MaxLength(500)]
        public string Descripcion { get; set; }

        /// <summary>
        /// Unidad de medida. Puede ser: hr, pza, %MO (porcentaje de mano de obra)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Unidad { get; set; }

        /// <summary>
        /// Para herramientas normales: costo horario o por pieza.
        /// Para %MO: el porcentaje (ej: 3.00 para 3%)
        /// </summary>
        public decimal PrecioUnitario { get; set; }

        public OrigenInsumo Origen { get; set; }
        public string? Notas { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime? FechaModificacion { get; set; }

        // Navigation property
        [ForeignKey("ProyectoId")]
        public virtual Proyecto Proyecto { get; set; }

        /// <summary>
        /// Indica si es un insumo basado en porcentaje de mano de obra
        /// </summary>
        [NotMapped]
        public bool EsPorcentajeMO => Unidad?.Trim().ToUpper() == "%MO";

        public Herramienta()
        {
            Origen = OrigenInsumo.Proyecto;
        }
    }
}
