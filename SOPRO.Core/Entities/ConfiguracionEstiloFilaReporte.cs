using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Estilo semántico de una fila de reporte identificada por su rol
    /// (p. ej. "Titulo", "Subtotal", "Total", "Agrupador", "Concepto"),
    /// en lugar de colores hardcodeados en el código de reportes.
    ///
    /// Igual que <see cref="FormatoCeldaReporte"/>, las propiedades de estilo
    /// son anulables: null = heredar el estilo base.
    /// </summary>
    public class ConfiguracionEstiloFilaReporte
    {
        public int Id { get; set; }

        public int ProyectoId { get; set; }

        public string TipoReporte { get; set; } = string.Empty;

        /// <summary>Rol semántico de la fila (no la posición visual).</summary>
        public string RolFila { get; set; } = string.Empty;

        // ── Overrides de estilo (null = heredar) ─────────────────────────────
        public string? Fuente { get; set; }
        public float? TamanoFuente { get; set; }
        public bool? Negrita { get; set; }
        public bool? Cursiva { get; set; }
        public string? ColorFuente { get; set; }      // #RRGGBB
        public string? ColorFondo { get; set; }       // #RRGGBB
        public string? AlineacionHorizontal { get; set; }
        public string? AlineacionVertical { get; set; }
        public bool? Wrap { get; set; }
        public string? FormatoNumerico { get; set; }

        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        public virtual Proyecto? Proyecto { get; set; }
    }
}
