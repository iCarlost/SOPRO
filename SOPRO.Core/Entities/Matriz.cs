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
        
        /// <summary>
        /// Calcula el costo directo usando precisión de pantalla correcta.
        /// Prefiere CalcularCostoDirecto(int decimalesImporte) cuando el proyecto está disponible.
        /// </summary>
        [Obsolete(
            "Usar CalcularCostoDirecto(int decimalesImporte) pasando proyecto.DecimalesImporte, " +
            "o delegar a MatrixComponentCalculationService.Recalculate(). " +
            "Este overload usa 2 decimales como fallback, lo que puede introducir discrepancias " +
            "si el proyecto tiene otra configuración.",
            error: false)]
        public void CalcularCostoDirecto()
            => CalcularCostoDirecto(2);

        /// <summary>
        /// Calcula el costo directo aplicando la precisión de pantalla del proyecto.
        /// Implementa redondeo por paso (Cantidad × PU redondeado) igual que MotorCalculoSopro.
        /// SOPRO.Core no puede referenciar SOPRO.Application, por eso el algoritmo
        /// está duplicado aquí. La fuente canónica es MatrixComponentCalculationService.
        /// </summary>
        public void CalcularCostoDirecto(int decimalesImporte)
        {
            if (Componentes == null || Componentes.Count == 0)
            {
                CostoDirecto = 0m;
                FechaUltimoCalculo = DateTime.Now;
                return;
            }

            static decimal R(decimal v, int d) =>
                Math.Round(v, d, MidpointRounding.AwayFromZero);

            // Paso 1: calcular base MO (MO normal + cuadrillas)
            decimal baseMO = 0m;
            foreach (var comp in Componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra
                    && comp.ManoDeObra != null && !comp.ManoDeObra.EsPorcentajeMO)
                {
                    comp.Importe = R(comp.Cantidad * R(comp.ManoDeObra.SalarioReal, decimalesImporte), decimalesImporte);
                    baseMO += comp.Importe;
                }
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar
                    && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                {
                    comp.Importe = R(comp.Cantidad * R(comp.Auxiliar.CostoDirecto, decimalesImporte), decimalesImporte);
                    baseMO += comp.Importe;
                }
            }

            // Paso 2: calcular el resto de componentes
            decimal total = 0m;
            foreach (var comp in Componentes)
            {
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material when comp.Material != null:
                        comp.Importe = R(comp.Cantidad * R(comp.Material.PrecioUnitario, decimalesImporte), decimalesImporte);
                        break;
                    case TipoComponenteMatriz.Maquinaria when comp.Maquinaria != null:
                        comp.Importe = R(comp.Cantidad * R(comp.Maquinaria.CostoHorario, decimalesImporte), decimalesImporte);
                        break;
                    case TipoComponenteMatriz.Auxiliar when comp.Auxiliar != null
                                                         && comp.Auxiliar.Tipo != TipoMatriz.Cuadrilla:
                        comp.Importe = R(comp.Cantidad * R(comp.Auxiliar.CostoDirecto, decimalesImporte), decimalesImporte);
                        break;
                    case TipoComponenteMatriz.ManoDeObra when comp.ManoDeObra?.EsPorcentajeMO == true:
                        comp.Importe = R(comp.Cantidad * baseMO, decimalesImporte);
                        break;
                    case TipoComponenteMatriz.Herramienta when comp.Herramienta != null:
                        comp.Importe = comp.Herramienta.EsPorcentajeMO
                            ? R(comp.Cantidad * baseMO, decimalesImporte)
                            : R(comp.Cantidad * R(comp.Herramienta.PrecioUnitario, decimalesImporte), decimalesImporte);
                        break;
                }
                total += comp.Importe;
            }

            CostoDirecto = R(total, decimalesImporte);
            FechaUltimoCalculo = DateTime.Now;
        }
            
            // Primero calcular total de MO (necesario para herramientas y MO con %MO)
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
