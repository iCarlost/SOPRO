using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Definición de una columna personalizada en el presupuesto
    /// Permite agregar columnas calculadas con fórmulas o columnas de captura libre
    /// </summary>
    public class ColumnaPersonalizada
    {
        public int Id { get; set; }
        
        // Relación con el proyecto (null = columna global/maestra)
        public int? ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }
        
        // Identificación
        public string Nombre { get; set; }
        public string NombreInterno { get; set; } // Para referencias en fórmulas
        
        // Tipo de columna
        public TipoColumnaPersonalizada TipoColumna { get; set; }
        
        // Tipo de dato resultante
        public TipoDatoColumna TipoDato { get; set; }
        
        // Fórmula (solo para columnas calculadas)
        // Ejemplo: "NumeroALetra(PrecioUnitario)"
        // Ejemplo: "(Importe / TotalPresupuesto) * 100"
        public string Formula { get; set; }
        
        // Configuración de visualización
        public bool Visible { get; set; }
        public bool Imprimible { get; set; }
        public int AnchoColumna { get; set; }
        public int Orden { get; set; }
        
        // Totalización
        public bool Totalizar { get; set; }
        public string CondicionTotalizacion { get; set; } // Fórmula condicional
        
        // Formato de presentación
        public string FormatoNumerico { get; set; } // Ej: "#,##0.00", "0.00%"
        public string FormatoFecha { get; set; } // Ej: "dd/MM/yyyy"
        
        // Alineación
        public AlineacionColumna Alineacion { get; set; }
        
        // Estilo
        public string NombreFuente { get; set; }
        public int TamanoFuente { get; set; }
        public string ColorFuente { get; set; }
        public string ColorFondo { get; set; }
        public bool Negrita { get; set; }
        public bool Cursiva { get; set; }
        public bool WrapTexto { get; set; }
        public int AlineacionVertical { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        
        public ColumnaPersonalizada()
        {
            FechaCreacion = DateTime.Now;
            WrapTexto        = false;
            AlineacionVertical = 1;
            FechaModificacion = DateTime.Now;
            Visible = true;
            Imprimible = true;
            AnchoColumna = 100;
            TipoDato = TipoDatoColumna.Texto;
            Alineacion = AlineacionColumna.Izquierda;
            TamanoFuente = 9;
            NombreFuente = "Segoe UI";
            ColorFuente = "#000000";
            ColorFondo = "#FFFFFF";
        }
    }
    
    /// <summary>
    /// Tipo de columna personalizada
    /// </summary>
    public enum TipoColumnaPersonalizada
    {
        Calculada = 0,  // Se calcula con fórmula
        Personal = 1    // Captura manual
    }
    
    /// <summary>
    /// Tipo de dato de la columna
    /// </summary>
    public enum TipoDatoColumna
    {
        Texto = 0,
        Numerico = 1,
        Moneda = 2,
        Porcentaje = 3,
        Fecha = 4,
        Booleano = 5
    }
    
    /// <summary>
    /// Alineación del contenido
    /// </summary>
    public enum AlineacionColumna
    {
        Izquierda = 0,
        Centro = 1,
        Derecha = 2,
        Justificado = 3
    }
    
    /// <summary>
    /// Vista personalizada del presupuesto
    /// Agrupa conjunto de columnas visibles
    /// </summary>
    public class VistaPresupuesto
    {
        public int Id { get; set; }
        
        // Relación con el proyecto
        public int ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }
        
        // Identificación
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        
        // Indica si es la vista por defecto
        public bool EsVistaPorDefecto { get; set; }
        
        // Configuración de columnas visibles (JSON)
        // Ejemplo: [{"ColumnaId": 1, "Visible": true, "Orden": 1}, ...]
        public string ConfiguracionColumnasJSON { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        
        public VistaPresupuesto()
        {
            FechaCreacion = DateTime.Now;
            FechaModificacion = DateTime.Now;
            EsVistaPorDefecto = false;
        }
    }
}
