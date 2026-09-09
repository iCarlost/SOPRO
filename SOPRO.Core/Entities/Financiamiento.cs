using System;
using System.Collections.Generic;

namespace SOPRO.Core.Entities
{
    /// <summary>
    /// Parámetros del contrato y resultado del cálculo de financiamiento.
    /// Un registro por proyecto. El porcentaje resultante se transfiere
    /// a Proyecto.PorcentajeFinanciamiento mediante el botón Transferir.
    /// </summary>
    public class ConfiguracionFinanciamiento
    {
        public int Id { get; set; }

        public int ProyectoId { get; set; }
        public virtual Proyecto Proyecto { get; set; } = null!;

        // ── Parámetros del contrato ───────────────────────────────────────────

        /// <summary>TIIE vigente (% anual). Fuente: Banxico / DOF.</summary>
        public decimal TasaTIIE { get; set; } = 11.0m;

        /// <summary>Puntos adicionales del banco (% anual).</summary>
        public decimal PuntosAdicionales { get; set; } = 3.0m;

        /// <summary>Tasa efectiva = TIIE + PuntosAdicionales.</summary>
        public decimal TasaEfectiva => TasaTIIE + PuntosAdicionales;

        /// <summary>Porcentaje de anticipo sobre el monto total del contrato (0 si no hay).</summary>
        public decimal PorcentajeAnticipo { get; set; } = 30.0m;

        /// <summary>
        /// Número de períodos del programa en que se amortiza el anticipo.
        /// El anticipo se descuenta proporcionalmente de las estimaciones
        /// hasta recuperar el monto total.
        /// </summary>
        public int PeriodosAmortizacionAnticipo { get; set; } = 1;

        /// <summary>
        /// Desfase de cobro en períodos del programa (cuántos períodos transcurren
        /// entre que se ejecuta el trabajo y se cobra la estimación).
        /// Valor típico: 1 período (estimación del período N se cobra en N+1).
        /// </summary>
        public int DesfaseCobro { get; set; } = 1;

        /// <summary>
        /// Base de cálculo del porcentaje de financiamiento:
        /// "SobreCD"  = F% se aplica sobre Costo Directo.
        /// "Acumulable" = F% se aplica sobre (CD + Indirectos).
        /// </summary>
        public string BaseCalculo { get; set; } = "Acumulable";

        // ── Resultado calculado ───────────────────────────────────────────────

        /// <summary>Suma de intereses negativos (costo financiero a pagar).</summary>
        public decimal InteresesNegativos { get; set; }

        /// <summary>Suma de intereses positivos (ingreso financiero, si aplica).</summary>
        public decimal InteresesPositivos { get; set; }

        /// <summary>
        /// Financiamiento neto = InteresesPositivos - InteresesNegativos (modelo
        /// dual). En el modelo clásico coincide con el costo y es positivo:
        /// InteresesNegativos. En el dual, un resultado negativo representa costo
        /// financiero neto (los intereses negativos superan a los positivos) y un
        /// resultado positivo, ingreso financiero neto.
        /// </summary>
        public decimal FinanciamientoNeto { get; set; }

        /// <summary>Porcentaje calculado = FinanciamientoNeto / BaseCalculo.</summary>
        public decimal PorcentajeCalculado { get; set; }

        /// <summary>Fecha y hora del último cálculo.</summary>
        public DateTime? FechaCalculo { get; set; }

        /// <summary>Filas del flujo de caja (persistidas para mostrar el detalle).</summary>
        public virtual ICollection<FilaFlujoCajaFinanciamiento> FilasFlujo { get; set; } = new List<FilaFlujoCajaFinanciamiento>();

        public ConfiguracionFinanciamiento()
        {
        }
    }

    /// <summary>
    /// Fila del flujo de caja para el cálculo de financiamiento.
    /// Una fila por período del programa de obra.
    /// </summary>
    public class FilaFlujoCajaFinanciamiento
    {
        public int Id { get; set; }

        public int ConfiguracionFinanciamientoId { get; set; }
        public virtual ConfiguracionFinanciamiento Configuracion { get; set; } = null!;

        public int NumeroPeriodo { get; set; }
        public string Etiqueta { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        /// <summary>Egresos del período (erogaciones del programa de obra).</summary>
        public decimal Egresos { get; set; }

        /// <summary>Anticipo recibido en este período.</summary>
        public decimal AnticipoRecibido { get; set; }

        /// <summary>Estimación cobrada en este período (egresos de período anterior con desfase).</summary>
        public decimal EstimacionCobrada { get; set; }

        /// <summary>Amortización de anticipo descontada en este período.</summary>
        public decimal AmortizacionAnticipo { get; set; }

        /// <summary>Flujo neto del período = AnticipoRecibido + EstimacionCobrada - AmortizacionAnticipo - Egresos.</summary>
        public decimal FlujoNeto { get; set; }

        /// <summary>Saldo acumulado al final del período.</summary>
        public decimal SaldoAcumulado { get; set; }

        /// <summary>
        /// Días del período.
        /// </summary>
        public int DiasPeriodo { get; set; }

        /// <summary>
        /// Interés del período = SaldoAcumulado × (TasaEfectiva/100/365) × DiasPeriodo.
        /// Negativo = costo; Positivo = ingreso.
        /// </summary>
        public decimal InteresPeriodo { get; set; }
    }
}
