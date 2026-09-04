using System;
using Sopro.Calculation.Equipment;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Maquinaria y Equipo - Con cálculo detallado de costo horario
    /// </summary>
    public class Maquinaria
    {
        public int Id { get; set; }
        
        // Identificación
        public string Clave { get; set; }
        public string Descripcion { get; set; }
        
        // Datos generales
        public decimal PotenciaNominal { get; set; } // en HP
        public TipoCombustible TipoCombustible { get; set; }
        
        // ═══════════════════════════════════════════════════════════
        // A. CARGOS FIJOS
        // ═══════════════════════════════════════════════════════════
        
        // Depreciación
        public decimal ValorAdquisicion { get; set; }
        public decimal ValorLlantas { get; set; }
        public decimal ValorPiezasEspeciales { get; set; }
        // ValorNeto = ValorAdquisicion - ValorLlantas - ValorPiezasEspeciales (calculado)
        public decimal FactorRescate { get; set; } // r (generalmente 0.10 = 10%)
        // ValorRescate = ValorNeto * FactorRescate (calculado)
        public decimal VidaEconomica { get; set; } // Ve (en horas)
        // Depreciacion = (ValorNeto - ValorRescate) / VidaEconomica (calculado)
        
        // Inversión
        public decimal TasaInteres { get; set; } // i (% anual, ej: 21.24)
        public decimal HorasEfectivasAnio { get; set; } // Hea (generalmente 1600 hrs/año)
        // Inversion = [(ValorNeto + ValorRescate)/2] * (TasaInteres/100) / HorasEfectivasAnio (calculado)
        
        // Seguros
        public decimal PrimaSeguro { get; set; } // s (% anual, ej: 3.00)
        // Seguros = [(ValorNeto + ValorRescate)/2] * (PrimaSeguro/100) / HorasEfectivasAnio (calculado)
        
        // Mantenimiento
        public decimal FactorMantenimiento { get; set; } // Ko (generalmente 0.20)
        // Mantenimiento = Ko * Depreciacion (calculado)
        
        // TOTAL CARGOS FIJOS (calculado)
        
        // ═══════════════════════════════════════════════════════════
        // B. CONSUMOS
        // ═══════════════════════════════════════════════════════════
        
        // Combustibles
        public decimal CantidadCombustible { get; set; } // Gh (lts/hr o gal/hr)
        public decimal PrecioCombustible { get; set; } // Pac ($/lt)
        // Combustibles = Gh * Pac (calculado)
        
        // Lubricantes
        public decimal CantidadAceite { get; set; } // Ah (lts/hr)
        public decimal PrecioAceite { get; set; } // ($/lt)
        // Lubricantes = Ah * PrecioAceite (calculado)
        
        // Llantas (si aplica)
        public int NumeroLlantas { get; set; }
        public decimal VidaEconomicaLlantas { get; set; } // Vn (hrs)
        // Llantas = ValorLlantas / VidaEconomicaLlantas (calculado)
        
        // Piezas Especiales (si aplica)
        public decimal VidaPiezasEspeciales { get; set; } // Va (hrs)
        // PiezasEspeciales = ValorPiezasEspeciales / VidaPiezasEspeciales (calculado)
        
        // TOTAL CONSUMOS (calculado)
        
        // ═══════════════════════════════════════════════════════════
        // C. OPERACIÓN
        // ═══════════════════════════════════════════════════════════
        
        public decimal SalarioOperador { get; set; } // Sn ($/turno)
        public decimal FactorSalarioReal { get; set; } // Fsr
        public decimal HorasEfectivasTurno { get; set; } // Ht (generalmente 8 hrs)
        // Operacion = (SalarioOperador * FactorSalarioReal) / HorasEfectivasTurno (calculado)
        
        // ═══════════════════════════════════════════════════════════
        // COSTO HORARIO TOTAL = CargosF ijos + Consumos + Operacion
        // ═══════════════════════════════════════════════════════════
        public decimal CostoHorario { get; set; } // Calculado o capturado manualmente
        
        // Indica si el costo horario fue calculado o capturado directamente
        public bool EsCostoCalculado { get; set; }
        
        // Origen
        public OrigenInsumo Origen { get; set; }
        public int? MaquinariaMaestraId { get; set; }
        
        // Relación con el proyecto
        public int? ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; }
        
        // Notas
        public string Notas { get; set; }
        
        // Control
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        public DateTime? FechaCalculoCosto { get; set; }
        
        public Maquinaria()
        {
            FechaCreacion = DateTime.Now;
            FechaModificacion = DateTime.Now;
            Origen = OrigenInsumo.Proyecto;
            EsCostoCalculado = true;
            
            // Valores por defecto comunes
            FactorRescate = 0.10m; // 10%
            TasaInteres = 21.24m; // % anual
            HorasEfectivasAnio = 1600m;
            PrimaSeguro = 3.00m; // % anual
            FactorMantenimiento = 0.20m;
            FactorSalarioReal = 1.6543m;
            HorasEfectivasTurno = 8m;
        }
        
        /// <summary>
        /// Calcula el costo horario total basándose en las fórmulas de OPUS
        /// </summary>
        public void CalcularCostoHorario()
        {
            var result = HourlyCostCalculator.Calculate(new HourlyCostInput
            {
                AcquisitionValue = ValorAdquisicion,
                TireValue = ValorLlantas,
                SpecialPartsValue = ValorPiezasEspeciales,
                SalvageFactor = FactorRescate,
                EconomicLifeHours = VidaEconomica,
                InterestRatePercentage = TasaInteres,
                EffectiveHoursPerYear = HorasEfectivasAnio,
                InsuranceRatePercentage = PrimaSeguro,
                MaintenanceFactor = FactorMantenimiento,
                FuelQuantity = CantidadCombustible,
                FuelPrice = PrecioCombustible,
                OilQuantity = CantidadAceite,
                OilPrice = PrecioAceite,
                TireLifeHours = VidaEconomicaLlantas,
                SpecialPartsLifeHours = VidaPiezasEspeciales,
                OperatorSalary = SalarioOperador,
                RealSalaryFactor = FactorSalarioReal,
                EffectiveHoursPerShift = HorasEfectivasTurno
            });

            CostoHorario = result.HourlyCost;
            EsCostoCalculado = true;
            FechaCalculoCosto = DateTime.Now;
        }

        /// <summary>
        /// [N7-10] Rendimiento del componente (unidades de obra por hora-máquina):
        /// 1/Cantidad con 5 decimales, o 0 si la cantidad no es positiva. Punto único
        /// de la fórmula antes duplicada en 5 rutas de Application.
        /// </summary>
        public static decimal CalcularRendimiento(decimal cantidad)
            => cantidad > 0m
                ? Math.Round(1m / cantidad, 5, MidpointRounding.AwayFromZero)
                : 0m;
    }
    
    /// <summary>
    /// Tipo de combustible de la maquinaria
    /// </summary>
    public enum TipoCombustible
    {
        Gasolina = 0,
        Diesel = 1,
        SinMotor = 2  // Para equipo que no consume combustible
    }
}
