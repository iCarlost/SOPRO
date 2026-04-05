using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Definición de una columna del módulo Explosión de Insumos.
    /// Tabla propia, independiente de ColumnasPersonalizadas (Presupuesto).
    /// </summary>
    public class ColumnaExplosion
    {
        public int Id { get; set; }

        public int ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }

        public string Nombre { get; set; }
        public string NombreInterno { get; set; }

        public bool Visible { get; set; }
        public int Orden { get; set; }
        public int AnchoColumna { get; set; }
        public AlineacionColumna Alineacion { get; set; }
        public string FormatoNumerico { get; set; }

        // Estilo
        public string NombreFuente { get; set; }
        public int TamanoFuente { get; set; }
        public string ColorFuente { get; set; }
        public string ColorFondo { get; set; }
        public bool Negrita { get; set; }
        public bool Cursiva { get; set; }
        public bool WrapTexto { get; set; }
        public int AlineacionVertical { get; set; }

        public DateTime FechaModificacion { get; set; }

        public ColumnaExplosion()
        {
            Visible           = true;
            AnchoColumna      = 100;
            Alineacion        = AlineacionColumna.Izquierda;
            TamanoFuente      = 9;
            NombreFuente      = "Segoe UI";
            ColorFuente       = "#000000";
            ColorFondo        = "#FFFFFF";
            FormatoNumerico   = string.Empty;
            WrapTexto        = false;
            AlineacionVertical = 1;
            FechaModificacion = DateTime.Now;
        }
    }
}
