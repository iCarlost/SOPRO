using System;
using System.Collections.Generic;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Concepto del Presupuesto - Renglón en la hoja de presupuesto
    /// Soporta estructura jerárquica (agrupadores tipo WBS)
    /// </summary>
    public class ConceptoPresupuesto
    {
        public int Id { get; set; }
        
        // Relación con el proyecto
        public int ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }
        
        // Identificación
        public string Clave { get; set; }
        public string Descripcion { get; set; }
        public string Unidad { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // ESTRUCTURA JERÁRQUICA (Agrupadores)
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>
        /// Nivel jerárquico (0 = raíz, 1 = primer nivel, 2 = segundo nivel, etc.)
        /// </summary>
        public int Nivel { get; set; }
        
        /// <summary>
        /// Indica si es un agrupador o un concepto terminal
        /// </summary>
        public bool EsAgrupador { get; set; }
        
        /// <summary>
        /// ID del concepto padre (null si es nivel 0)
        /// </summary>
        public int? PadreId { get; set; }
        public virtual ConceptoPresupuesto Padre { get; set; }
        
        /// <summary>
        /// Hijos de este concepto (si es agrupador)
        /// </summary>
        public virtual ICollection<ConceptoPresupuesto> Hijos { get; set; }
        
        /// <summary>
        /// Orden de aparición dentro de su nivel
        /// </summary>
        public int Orden { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // DATOS DEL CONCEPTO (solo si NO es agrupador)
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>
        /// Cantidad de unidades a presupuestar
        /// </summary>
        public decimal Cantidad { get; set; }
        
        /// <summary>
        /// Referencia a la Matriz (APU) que define el costo
        /// </summary>
        public int? MatrizId { get; set; }
        public virtual Matriz Matriz { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // COSTOS Y CÁLCULOS
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>
        /// Costo Directo unitario (viene de la Matriz)
        /// </summary>
        public decimal CostoDirectoUnitario { get; set; }
        
        /// <summary>
        /// Costo Directo Total = Cantidad * CostoDirectoUnitario
        /// </summary>
        public decimal CostoDirectoTotal { get; set; }
        
        /// <summary>
        /// Indirectos aplicados
        /// </summary>
        public decimal Indirectos { get; set; }
        
        /// <summary>
        /// Financiamiento aplicado
        /// </summary>
        public decimal Financiamiento { get; set; }
        
        /// <summary>
        /// Utilidad aplicada
        /// </summary>
        public decimal Utilidad { get; set; }
        
        /// <summary>
        /// Cargos adicionales aplicados
        /// </summary>
        public decimal CargosAdicionales { get; set; }
        
        /// <summary>
        /// Precio Unitario = CostoDirecto + Indirectos + Financiamiento + Utilidad + Cargos
        /// </summary>
        public decimal PrecioUnitario { get; set; }
        
        /// <summary>
        /// Importe Total = Cantidad * PrecioUnitario
        /// </summary>
        public decimal ImporteTotal { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // COLUMNAS PERSONALIZADAS
        // ═══════════════════════════════════════════════════════════
        
        /// <summary>
        /// Almacena valores de columnas personalizadas en formato JSON
        /// Ejemplo: {"PU_Letra": "DOCE PESOS 02/100", "Incidencia": "5.23"}
        /// </summary>
        public string ColumnasPersonalizadasJSON { get; set; }
        
        // Notas
        public string Notas { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        
        public ConceptoPresupuesto()
        {
            Hijos = new List<ConceptoPresupuesto>();
            FechaCreacion = DateTime.Now;
            FechaModificacion = DateTime.Now;
            EsAgrupador = false;
            Nivel = 1;
            Cantidad = 1.0m;
            ColumnasPersonalizadasJSON = string.Empty; // Inicializar para evitar NOT NULL
            Notas = string.Empty; // Inicializar para evitar NOT NULL
            Unidad = string.Empty; // Inicializar para evitar NOT NULL
        }
        
        // CalcularPrecioUnitario() fue eliminado de la entidad.
        // Usar: MotorCalculoSopro.CalcularPrecioUnitario(costoDirecto, pctInput)
        // que redondea cada paso intermedio y respeta DecimalesImporte del proyecto.
        
        /// <summary>
        /// Si es agrupador, calcula el total sumando los hijos
        /// </summary>
        public decimal CalcularTotalAgrupador()
        {
            if (!EsAgrupador)
                return ImporteTotal;
            
            decimal total = 0;
            foreach (var hijo in Hijos)
            {
                if (hijo.EsAgrupador)
                    total += hijo.CalcularTotalAgrupador();
                else
                    total += hijo.ImporteTotal;
            }
            
            ImporteTotal = total;
            return total;
        }
    }
}
