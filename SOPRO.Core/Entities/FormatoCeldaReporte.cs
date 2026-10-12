using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Override de estilo para una celda concreta de un reporte.
    ///
    /// Persistencia relacional "dispersa": solo se guardan las celdas con
    /// override explícito. Las propiedades de estilo son anulables: null =
    /// heredar el estilo base de la columna/reporte (no "sin estilo").
    ///
    /// Claves estables de dominio (nunca índices visuales):
    ///   - TipoReporte: "Presupuesto" | "APU" | "Explosion" | ...
    ///   - ClaveVista:   identificador de vista; "" = vista por defecto.
    ///                   NO nula: SQLite permite duplicados con NULL en un
    ///                   índice único, así que "" es la clave canónica.
    ///   - ClaveFila:    clave estable de la fila (no su posición visual).
    ///   - NombreColumna: nombre interno de la columna.
    ///
    /// Los colores son hex "#RRGGBB" (string), nunca System.Drawing.Color:
    /// SOPRO.Core no debe depender de System.Drawing.
    /// </summary>
    public class FormatoCeldaReporte
    {
        public int Id { get; set; }

        public int ProyectoId { get; set; }

        public string TipoReporte { get; set; } = string.Empty;

        /// <summary>Clave de vista; "" = vista por defecto. Nunca NULL.</summary>
        public string ClaveVista { get; set; } = string.Empty;

        /// <summary>Clave estable de fila del dominio (no un índice visual).</summary>
        public string ClaveFila { get; set; } = string.Empty;

        public string NombreColumna { get; set; } = string.Empty;

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
