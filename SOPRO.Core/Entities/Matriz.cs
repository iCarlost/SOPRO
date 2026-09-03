using System;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Matriz - Insumo compuesto (APU o Básico/Auxiliar)
    /// Es la "receta" que define cómo se integra un concepto
    /// </summary>
    public class Matriz
    {
        public int Id { get; set; }
        
        // Identificación
        public string Clave { get; set; }
        public string Descripcion { get; set; }
        public string Unidad { get; set; }
        
        // Tipo de matriz
        public TipoMatriz Tipo { get; set; }
        
        // Costo calculado (suma de todos los componentes)
        public decimal CostoDirecto { get; set; }
        
        // Origen
        public OrigenInsumo Origen { get; set; }
        public int? MatrizMaestraId { get; set; }
        
        // Relación con el proyecto
        public int? ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }
        
        // Componentes de la matriz (insumos que la integran)
        public virtual ICollection<ComponenteMatriz> Componentes { get; set; }
        
        // Notas
        public string Notas { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        public DateTime? FechaUltimoCalculo { get; set; }
        
        public Matriz()
        {
            Componentes = new List<ComponenteMatriz>();
            FechaCreacion = DateTime.Now;
            FechaModificacion = DateTime.Now;
            Origen = OrigenInsumo.Proyecto;
            Tipo = TipoMatriz.APU;
            Notas = string.Empty; // Inicializar para evitar NOT NULL constraint
        }
    }
    
    /// <summary>
    /// Tipo de matriz
    /// </summary>
    public enum TipoMatriz
    {
        APU = 0,        // Análisis de Precio Unitario (concepto completo)
        Basico = 1,     // Básico/Auxiliar (insumo compuesto intermedio)
        Cuadrilla = 2   // Cuadrilla de mano de obra (combinación de trabajadores)
    }
    
    /// <summary>
    /// Componente de una Matriz - Un insumo dentro del análisis
    /// </summary>
    public class ComponenteMatriz
    {
        public int Id { get; set; }
        
        // Relación con la matriz padre
        public int MatrizId { get; set; }
        public virtual Matriz Matriz { get; set; }
        
        // Tipo de componente
        public TipoComponenteMatriz TipoComponente { get; set; }
        
        // Referencias a los insumos (solo una estará llena según el tipo)
        public int? MaterialId { get; set; }
        public virtual Material Material { get; set; }
        
        public int? ManoDeObraId { get; set; }
        public virtual ManoDeObra ManoDeObra { get; set; }
        
        public int? MaquinariaId { get; set; }
        public virtual Maquinaria Maquinaria { get; set; }
        
        public int? AuxiliarId { get; set; }
        public virtual Matriz Auxiliar { get; set; } // Referencia a otra matriz (básico)
        
        public int? HerramientaId { get; set; }
        public virtual Herramienta Herramienta { get; set; }
        
        // Cantidad del insumo (horas por unidad de obra para Maquinaria)
        public decimal Cantidad { get; set; }

        /// <summary>
        /// Rendimiento del componente (unidades de obra por hora-máquina).
        /// Significativo solo para TipoComponente == Maquinaria.
        /// Equivale a 1/Cantidad. Se persiste para conservar el valor capturado.
        /// </summary>
        public decimal Rendimiento { get; set; }

        // Importe = Cantidad * PrecioUnitario (calculado)
        public decimal Importe { get; set; }
        
        // Orden de aparición en el análisis
        public int Orden { get; set; }
        
        // Notas específicas del componente
        public string Notas { get; set; }
        
        public ComponenteMatriz()
        {
            Cantidad = 1.0m;
            Notas = string.Empty; // Inicializar para evitar NOT NULL constraint
        }
    }
    
    /// <summary>
    /// Tipo de componente en una matriz
    /// </summary>
    public enum TipoComponenteMatriz
    {
        Material = 0,
        ManoDeObra = 1,
        Maquinaria = 2,
        Auxiliar = 3,      // Básico (otra matriz)
        Herramienta = 4    // Herramienta o equipo menor
    }
}
