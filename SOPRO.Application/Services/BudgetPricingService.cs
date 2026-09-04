using Sopro.Calculation;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  BudgetPricingService — WRAPPER DELEGADOR v3.0                         ║
    // ║                                                                         ║
    // ║  Esta clase ya NO contiene lógica de cálculo propia.                    ║
    // ║  Toda operación numérica se delega a SoproCalculationEngine             ║
    // ║  (SOPRO.Calculation), sin pasar por la fachada legacy                   ║
    // ║  (N5-1, PLAN-01 §14). Se mantiene por compatibilidad                    ║
    // ║  con llamadores existentes.                                             ║
    // ║                                                                         ║
    // ║  ConvertirALetras, BuildHeaderInfo y RefreshProjectPercentages          ║
    // ║  son utilidades puras que no implican aritmética de precisión.          ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public static class BudgetPricingService
    {
        // ── Utilidad: refrescar porcentajes del proyecto desde BD ────────────────
        public static Proyecto RefreshProjectPercentages(SOPROContext context, Proyecto proyecto)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));

            var refreshed = context.Proyectos.Find(proyecto.Id);
            if (refreshed == null) return proyecto;

            proyecto.PorcentajeIndirectosCentral = refreshed.PorcentajeIndirectosCentral;
            proyecto.PorcentajeIndirectosCampo   = refreshed.PorcentajeIndirectosCampo;
            proyecto.PorcentajeFinanciamiento    = refreshed.PorcentajeFinanciamiento;
            proyecto.PorcentajeUtilidad          = refreshed.PorcentajeUtilidad;
            proyecto.PorcentajeCargosAdicionales = refreshed.PorcentajeCargosAdicionales;
            proyecto.ModoCalculoPorcentajes      = refreshed.ModoCalculoPorcentajes;
            proyecto.DecimalesCantidad           = refreshed.DecimalesCantidad;
            proyecto.DecimalesImporte            = refreshed.DecimalesImporte;
            proyecto.DecimalesPorcentaje         = refreshed.DecimalesPorcentaje;
            return proyecto;
        }

        // ── Factor de precios (sin redondeo — es un multiplicador, no un importe) ─
        public static decimal CalculateFactor(Proyecto proyecto)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            return CalculateFactor(BudgetPercentageInput.FromProyecto(proyecto));
        }

        public static decimal CalculateFactor(BudgetPercentageInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            decimal pInd   = input.IndirectosCentral + input.IndirectosCampo;
            decimal pFin   = input.Financiamiento;
            decimal pUtil  = input.Utilidad;
            decimal pCargos = input.CargosAdicionales;

            if (string.Equals(input.ModoCalculoPorcentajes, "SobreCD", StringComparison.OrdinalIgnoreCase))
                return 1m + (pInd + pFin + pUtil + pCargos) / 100m;

            decimal f = 1m;
            f *= (1m + pInd    / 100m);
            f *= (1m + pFin    / 100m);
            f *= (1m + pUtil   / 100m);
            f *= (1m + pCargos / 100m);
            return f;
        }

        // ── Delegadores → SoproCalculationEngine ─────────────────────────────

        /// <summary>Delega a SoproCalculationEngine.CalculateUnitPrice().</summary>
        public static decimal CalculateUnitPrice(Proyecto proyecto, decimal costoDirecto)
        {
            var engine = CalculationEngineFactory.FromProyecto(proyecto);
            var pct    = BudgetPercentageInput.FromProyecto(proyecto);
            return engine.CalculateUnitPrice(costoDirecto, pct.ToPricePercentage()).UnitPrice;
        }

        /// <summary>Delega a SoproCalculationEngine.CalculateUnitPrice().</summary>
        public static decimal CalculateUnitPrice(BudgetPercentageInput input, decimal costoDirecto)
        {
            // Sin proyecto no hay configuración de decimales; usar 2 como fallback seguro
            var engine = new SoproCalculationEngine(2, 2, 4);
            return engine.CalculateUnitPrice(costoDirecto, input.ToPricePercentage()).UnitPrice;
        }

        /// <summary>Delega a SoproCalculationEngine.Multiply().</summary>
        public static decimal MultiplyUsingDisplayPrecision(Proyecto proyecto, decimal cantidad, decimal precioUnitario)
            => CalculationEngineFactory.FromProyecto(proyecto).Multiply(cantidad, precioUnitario);

        /// <summary>Delega a SoproCalculationEngine.RoundQuantity().</summary>
        public static decimal RoundQuantity(Proyecto proyecto, decimal valor)
            => CalculationEngineFactory.FromProyecto(proyecto).RoundQuantity(valor);

        /// <summary>Delega a SoproCalculationEngine.RoundAmount().</summary>
        public static decimal RoundImporte(Proyecto proyecto, decimal valor)
            => CalculationEngineFactory.FromProyecto(proyecto).RoundAmount(valor);

        /// <summary>Delega a SoproCalculationEngine.SumDirectCost().</summary>
        public static decimal SumDirectCost(Proyecto proyecto, IEnumerable<ConceptoPresupuesto> conceptos)
        {
            if (conceptos == null) return 0m;

            return CalculationEngineFactory.FromProyecto(proyecto).SumDirectCost(conceptos.Select(c => new DirectCostLine(
                c.Cantidad,
                c.CostoDirectoUnitario,
                c.EsAgrupador,
                c.MatrizId.HasValue)));
        }

        // ── Utilidades puras (sin cálculo numérico de precisión) ────────────────

        public static string ConvertirALetras(decimal numero, string moneda = "PESOS", string centavos = "M.N.")
        {
            if (numero == 0)
                return $"CERO {moneda} 00/100 {centavos}";

            long parteEntera  = (long)Math.Floor(numero);
            int  parteDecimal = (int)Math.Round((numero - parteEntera) * 100);
            string letras     = ConvertirEnteroALetras(parteEntera);
            return $"{letras} {moneda} {parteDecimal:00}/100 {centavos}";
        }

        public static BudgetHeaderInfo BuildHeaderInfo(Proyecto proyecto)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));

            decimal totalInd = proyecto.PorcentajeIndirectosCentral + proyecto.PorcentajeIndirectosCampo;
            bool hayPorc = totalInd > 0 || proyecto.PorcentajeFinanciamiento > 0
                        || proyecto.PorcentajeUtilidad > 0 || proyecto.PorcentajeCargosAdicionales > 0;
            decimal factor = CalculateFactor(proyecto);

            return new BudgetHeaderInfo
            {
                ProyectoNombre       = proyecto.Nombre ?? string.Empty,
                UbicacionTexto       = $"📍 {proyecto.Ubicacion}",
                FactorPrecioUnitario = factor,
                TienePorcentajesActivos = hayPorc,
                PorcentajesTexto     = hayPorc
                    ? $"📊  Ind.OC {proyecto.PorcentajeIndirectosCentral:N2}%  Campo {proyecto.PorcentajeIndirectosCampo:N2}%  Fin {proyecto.PorcentajeFinanciamiento:N2}%  Util {proyecto.PorcentajeUtilidad:N2}%  → Factor {factor:N6}×"
                    : "⚠️  Sin porcentajes configurados  (usa Porcentajes o Módulo Indirectos)"
            };
        }

        // ── Helper privado ───────────────────────────────────────────────────────
        // [N7-9] Wrappers BuildEngine/BuildEnginePercentages/BuildPercentageInput
        // retirados: los callsites usan CalculationEngineFactory.FromProyecto,
        // BudgetPercentageInput.FromProyecto y ToPricePercentage directamente.

        private static string ConvertirEnteroALetras(long numero)
        {
            if (numero == 0) return "CERO";
            if (numero < 0)  return "MENOS " + ConvertirEnteroALetras(Math.Abs(numero));
            if (numero <= 15)
                return numero switch
                {
                    1  => "UNO",   2  => "DOS",    3  => "TRES",    4  => "CUATRO",  5  => "CINCO",
                    6  => "SEIS",  7  => "SIETE",  8  => "OCHO",    9  => "NUEVE",   10 => "DIEZ",
                    11 => "ONCE",  12 => "DOCE",   13 => "TRECE",   14 => "CATORCE", 15 => "QUINCE",
                    _ => string.Empty
                };
            if (numero < 20)  return "DIECI"  + ConvertirEnteroALetras(numero - 10).ToLower();
            if (numero == 20) return "VEINTE";
            if (numero < 30)  return "VEINTI"  + ConvertirEnteroALetras(numero - 20).ToLower();
            if (numero < 100)
            {
                long decenas  = numero / 10;
                long unidades = numero % 10;
                string textoDecenas = decenas switch
                {
                    3 => "TREINTA", 4 => "CUARENTA", 5 => "CINCUENTA", 6 => "SESENTA",
                    7 => "SETENTA", 8 => "OCHENTA",  9 => "NOVENTA",   _ => string.Empty
                };
                return unidades == 0 ? textoDecenas : textoDecenas + " Y " + ConvertirEnteroALetras(unidades);
            }
            if (numero == 100) return "CIEN";
            if (numero < 200)  return "CIENTO " + ConvertirEnteroALetras(numero - 100);
            if (numero < 1000)
            {
                long centenas = numero / 100;
                long resto    = numero % 100;
                string textoCentenas = centenas switch
                {
                    2 => "DOSCIENTOS",    3 => "TRESCIENTOS",   4 => "CUATROCIENTOS",
                    5 => "QUINIENTOS",    6 => "SEISCIENTOS",   7 => "SETECIENTOS",
                    8 => "OCHOCIENTOS",   9 => "NOVECIENTOS",   _ => string.Empty
                };
                return resto == 0 ? textoCentenas : textoCentenas + " " + ConvertirEnteroALetras(resto);
            }
            if (numero == 1000) return "MIL";
            if (numero < 2000)  return "MIL " + ConvertirEnteroALetras(numero - 1000);
            if (numero < 1_000_000)
            {
                long miles = numero / 1000;
                long resto = numero % 1000;
                string textoMiles = ConvertirEnteroALetras(miles) + " MIL";
                return resto == 0 ? textoMiles : textoMiles + " " + ConvertirEnteroALetras(resto);
            }
            if (numero == 1_000_000) return "UN MILLON";
            if (numero < 2_000_000)  return "UN MILLON " + ConvertirEnteroALetras(numero - 1_000_000);
            if (numero < 1_000_000_000_000)
            {
                long millones = numero / 1_000_000;
                long resto    = numero % 1_000_000;
                string textoMillones = ConvertirEnteroALetras(millones) + " MILLONES";
                return resto == 0 ? textoMillones : textoMillones + " " + ConvertirEnteroALetras(resto);
            }
            return numero.ToString();
        }
    }
}
