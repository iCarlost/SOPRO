using System.ComponentModel.DataAnnotations.Schema;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Configuración visual de una columna dentro de un reporte.
    /// Cada reporte (Presupuesto, APU, Explosion, etc.) tiene su propio
    /// conjunto de columnas configuradas independientemente.
    /// </summary>
    public class ConfigColumnaReporte
    {
        public int    Id           { get; set; }
        public int    ProyectoId   { get; set; }

        /// <summary>
        /// Tipo de reporte: "Presupuesto" | "APU" | "Explosion" |
        ///                  "ManoObra" | "Materiales" | "Maquinaria" | "Indirectos"
        /// </summary>
        public string TipoReporte  { get; set; }

        /// <summary>
        /// Nombre interno de la columna, ej: "Clave", "Descripcion", "Unidad",
        /// "Cantidad", "PrecioUnitario", "ImporteTotal"
        /// </summary>
        public string NombreInterno { get; set; }

        /// <summary>Encabezado visible que se muestra en el reporte</summary>
        public string Encabezado   { get; set; }

        public bool   Visible      { get; set; } = true;
        public int    Orden        { get; set; } = 0;
        public int    Ancho        { get; set; } = 100;  // puntos en Excel / px en pantalla

        // Formato del encabezado de columna
        public string EncFuente    { get; set; } = "Segoe UI";
        public float  EncTamaño    { get; set; } = 9f;
        public bool   EncNegrita   { get; set; } = true;
        public string EncAlineacion { get; set; } = "Centro";  // Izquierda|Centro|Derecha
        public string EncColorFondo { get; set; } = "#1565C0"; // azul por defecto
        public string EncColorTexto { get; set; } = "#FFFFFF";

        [NotMapped]
        public bool   EncCursiva   { get; set; } = false;

        // Formato del contenido de la columna
        public string ConFuente    { get; set; } = "Segoe UI";
        public float  ConTamaño    { get; set; } = 9f;
        public bool   ConNegrita   { get; set; } = false;
        [NotMapped]
        public bool   ConCursiva   { get; set; } = false;
        public string ConAlineacion { get; set; } = "Izquierda";
        public string ConColorFondo { get; set; } = "#FFFFFF";
        public string ConColorTexto { get; set; } = "#000000";


        [NotMapped]
        public bool   WrapTexto    { get; set; } = false;

        /// <summary>
        /// Formato numérico, ej: "N2", "C2", "P2", "" (sin formato)
        /// </summary>
        public string FormatoNumero { get; set; } = "";

        // Navegación
        public virtual Proyecto Proyecto { get; set; }
    }
}
