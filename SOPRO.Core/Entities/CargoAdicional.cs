using System;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Cargo Adicional - Conceptos adicionales al presupuesto
    /// Ejemplos: Seguros, Fianzas, Garantías, etc.
    /// </summary>
    public class CargoAdicional
    {
        public int Id { get; set; }
        
        // Relación con el proyecto
        public int ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }
        
        // Identificación
        public string Descripcion { get; set; }
        
        // Base de cálculo
        public BaseCalculoCargo BaseCalculo { get; set; }
        
        // Tipo de cargo
        public TipoCargo TipoCargo { get; set; }
        
        // Valor del cargo
        public decimal Valor { get; set; } // Porcentaje o monto fijo según TipoCargo
        
        // Orden de aplicación (para cargos en cascada)
        public int Orden { get; set; }
        
        // Activo/Inactivo
        public bool Activo { get; set; }
        
        // Notas
        public string Notas { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        
        public CargoAdicional()
        {
            FechaCreacion = DateTime.Now;
            FechaModificacion = DateTime.Now;
            Activo = true;
            TipoCargo = TipoCargo.Porcentaje;
            BaseCalculo = BaseCalculoCargo.CostoDirectoMasIndirectos;
        }
        
        /// <summary>
        /// Calcula el monto del cargo basándose en la base de cálculo
        /// </summary>
        public decimal CalcularMonto(
            decimal costoDirecto,
            decimal indirectos,
            decimal financiamiento,
            decimal subtotalAntesUtilidad,
            decimal totalContrato)
        {
            if (!Activo)
                return 0;
            
            decimal baseCalculo = 0;
            
            switch (BaseCalculo)
            {
                case BaseCalculoCargo.CostoDirecto:
                    baseCalculo = costoDirecto;
                    break;
                    
                case BaseCalculoCargo.CostoDirectoMasIndirectos:
                    baseCalculo = costoDirecto + indirectos;
                    break;
                    
                case BaseCalculoCargo.CostoDirectoMasIndirectosMasFinanciamiento:
                    baseCalculo = costoDirecto + indirectos + financiamiento;
                    break;
                    
                case BaseCalculoCargo.SubtotalAntesUtilidad:
                    baseCalculo = subtotalAntesUtilidad;
                    break;
                    
                case BaseCalculoCargo.TotalContrato:
                    baseCalculo = totalContrato;
                    break;
            }
            
            if (TipoCargo == TipoCargo.Porcentaje)
                return baseCalculo * (Valor / 100m);
            else
                return Valor; // Monto fijo
        }
    }
    
    /// <summary>
    /// Base sobre la cual se calcula el cargo adicional
    /// </summary>
    public enum BaseCalculoCargo
    {
        CostoDirecto = 0,                                      // C.D.
        CostoDirectoMasIndirectos = 1,                        // C.D. + C.I.
        CostoDirectoMasIndirectosMasFinanciamiento = 2,       // C.D. + C.I. + Financ.
        SubtotalAntesUtilidad = 3,                            // Subtotal antes de utilidad
        TotalContrato = 4                                      // Total del contrato
    }
    
    /// <summary>
    /// Tipo de cargo: porcentaje o monto fijo
    /// </summary>
    public enum TipoCargo
    {
        Porcentaje = 0,  // Se aplica como % de la base
        MontoFijo = 1    // Monto fijo en pesos
    }
}
