using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sopro.Calculation;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║          MOTOR DE CÁLCULO SOPRO — v2.0  (Auditado 2026-03)             ║
    // ║          FACHADA LEGACY (Fase N2, PLAN-01 §11)                           ║
    // ║          La aritmética con precisión de pantalla vive en                 ║
    // ║          SOPRO.Calculation (SoproCalculationEngine). Esta clase          ║
    // ║          conserva la API, las normalizaciones y el formato legacy.      ║
    // ║                                                                         ║
    // ║  ESTADO DE AUDITORÍA INTEGRAL (dictamen externo):                       ║
    // ║    ✅ Motor único — BudgetPricingService delega aquí                    ║
    // ║    ✅ IVA dinámico — proyecto.PorcentajeIVA / 100m                     ║
    // ║    ✅ Importes sin cálculo crudo — PricePropagationService,             ║
    // ║       PanelMatricesEmbebido, BudgetRowEditFlowService usan Multiplicar  ║
    // ║    ✅ Cantidad normalizada al capturar — RedondearCantidad en input      ║
    // ║    ✅ Recálculo global conectado al cambio de decimales                  ║
    // ║    ✅ Programación persiste en NUMERIC (migración automática)            ║
    // ║    ✅ Distribución con ajuste de residuo en último periodo               ║
    // ║    ✅ FormatoHelper sin segunda vía de cálculo (lanza excepción)         ║
    // ║    ✅ N2 — aritmética delegada a SOPRO.Calculation (Gate N1:            ║
    // ║       diferenciales exactos + oráculo dorado independiente)             ║
    // ║                                                                         ║
    // ║  DEUDA TÉCNICA DOCUMENTADA (no urgente):                                ║
    // ║    ⚠ Matriz.CalcularCostoDirecto(int) — algoritmo duplicado por        ║
    // ║      dependencia circular entre SOPRO.Core y SOPRO.Application.         ║
    // ║      Funciona correctamente; fuente canónica: MatrixComponentCalc...    ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Fachada legacy del motor de cálculo aritmético con "Precisión de Pantalla".
    ///
    /// Desde la Fase N2 (PLAN-01 §11) TODA la aritmética está delegada en
    /// <see cref="SoproCalculationEngine"/> (paquete <c>SOPRO.Calculation</c>):
    /// esta clase conserva el assembly, el namespace, la API pública, los parámetros
    /// y los comportamientos legacy documentados, y no contiene una segunda cascada
    /// de cálculo (Gate N2).
    ///
    /// Lo que queda aquí (decidido en N0):
    ///   - Los cuatro métodos de formato con la cultura actual (filas 9).
    ///   - La normalización de entrada y el guard legacy de argumentos.
    ///   - El mapeo dominio ↔ paquete:
    ///       * "SobreCD" (casing-insensitive) → OverDirectCost; resto → Acumulables.
    ///       * ConceptoPresupuesto → DirectCostLine (HasMatrix == MatrizId.HasValue).
    ///
    /// PRINCIPIO FUNDAMENTAL:
    ///   Lo que el usuario VE en pantalla es exactamente lo que se PROCESA.
    ///
    /// USO:
    ///   var motor = new MotorCalculoSopro(proyecto);
    ///   decimal importe = motor.Multiplicar(concepto.Cantidad, concepto.CostoDirectoUnitario);
    ///
    /// PROHIBIDO usar directamente:
    ///   Math.Round(...)               ← usar motor.RedondearImporte()
    ///   BudgetPricingService.Round... ← usar motor.*
    ///   FormatoHelper.Multiplicar...  ← usar motor.Multiplicar()
    /// </summary>
    public sealed class MotorCalculoSopro
    {
        private readonly SoproCalculationEngine _engine;

        // ── Acceso interno para que RecalculoGlobalService pueda componer ──────
        internal int DecimalesCantidad  => _engine.QuantityDecimals;
        internal int DecimalesImporte   => _engine.AmountDecimals;
        internal int DecimalesPorcentaje => _engine.PercentageDecimals;

        public MotorCalculoSopro(Proyecto proyecto)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            _engine = new SoproCalculationEngine(proyecto.DecimalesCantidad,
                                                 proyecto.DecimalesImporte,
                                                 proyecto.DecimalesPorcentaje);
        }

        /// <summary>Constructor de pruebas / sin proyecto EF.</summary>
        public MotorCalculoSopro(int decimalesCantidad, int decimalesImporte, int decimalesPorcentaje)
        {
            _engine = new SoproCalculationEngine(decimalesCantidad, decimalesImporte, decimalesPorcentaje);
        }

        // ════════════════════════════════════════════════════════════════════════
        // PRIMITIVAS DE REDONDEO
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Redondea una cantidad al número de decimales configurado para cantidades.</summary>
        public decimal RedondearCantidad(decimal valor)
            => _engine.RoundQuantity(valor);

        /// <summary>Redondea un importe/precio al número de decimales configurado para importes.</summary>
        public decimal RedondearImporte(decimal valor)
            => _engine.RoundAmount(valor);

        /// <summary>Redondea un porcentaje al número de decimales configurado para porcentajes.</summary>
        public decimal RedondearPorcentaje(decimal valor)
            => _engine.RoundPercentage(valor);

        // ════════════════════════════════════════════════════════════════════════
        // OPERACIÓN DE PANTALLA PRINCIPAL
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Multiplicación con Precisión de Pantalla.
        ///
        /// Algoritmo:
        ///   1. Redondear P.U. a los decimales visibles  → puVisible
        ///   2. Multiplicar cantidad × puVisible
        ///   3. Redondear resultado
        ///
        /// Esto emula exactamente lo que el usuario ve: el P.U. mostrado
        /// multiplicado por la cantidad — sin ruido de decimales ocultos.
        ///
        /// Ejemplo con DecimalesImporte=2:
        ///   652 × 13.3875  →  652 × 13.39 = 8,730.28  ✅
        ///   (NO 652 × 13.3875 = 8,728.65 que el usuario nunca vería)
        /// </summary>
        public decimal Multiplicar(decimal cantidad, decimal precioUnitario)
            => _engine.Multiply(cantidad, precioUnitario);

        /// <summary>
        /// Calcula un importe sobre una base monetaria usando la misma política de
        /// precisión visible del motor. Útil para %MO y herramienta porcentual.
        /// </summary>
        public decimal CalcularImporteSobreBase(decimal factor, decimal baseImporte)
            => _engine.CalculateAmountOverBase(factor, baseImporte);

        // ════════════════════════════════════════════════════════════════════════
        // CASCADA DE PORCENTAJES CON REDONDEO EN CADA PASO VISIBLE
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calcula el desglose completo de precio unitario aplicando la cascada
        /// de porcentajes, redondeando CADA paso intermedio visible en reportes.
        ///
        /// Garantía: CD + Indirectos + Financiamiento + Utilidad + Cargos == P.U.
        ///            (al número de decimales configurado, sin error de centavo).
        ///
        /// Modos:
        ///   "Acumulables" (default): cada porcentaje aplica sobre el subtotal anterior.
        ///   "SobreCD":               todos los porcentajes aplican directamente sobre CD.
        ///
        /// La cascada completa se ejecuta en SOPRO.Calculation; aquí solo se
        /// mapea el texto legacy y se reconstruye <see cref="DesglosePrecios"/>.
        /// </summary>
        public DesglosePrecios CalcularPrecioUnitario(decimal costoDirecto, BudgetPercentageInput pct)
        {
            if (pct == null) throw new ArgumentNullException(nameof(pct));

            var desglose = _engine.CalculateUnitPrice(costoDirecto, new PricePercentageInput
            {
                ReferenceDirectCost             = pct.CostoDirectoReferencia,
                CentralIndirectsPercentage      = pct.IndirectosCentral,
                FieldIndirectsPercentage        = pct.IndirectosCampo,
                FinancingPercentage             = pct.Financiamiento,
                ProfitPercentage                = pct.Utilidad,
                AdditionalChargesPercentage     = pct.CargosAdicionales,
                Mode                            = string.Equals(pct.ModoCalculoPorcentajes, "SobreCD",
                                                                 StringComparison.OrdinalIgnoreCase)
                                                    ? PercentageCalculationMode.OverDirectCost
                                                    : PercentageCalculationMode.Accumulative,
            });

            return new DesglosePrecios(
                desglose.DirectCost,
                desglose.IndirectCosts,
                desglose.Financing,
                desglose.Profit,
                desglose.AdditionalCharges,
                desglose.UnitPrice,
                desglose.CentralIndirectsPercentage,
                desglose.FieldIndirectsPercentage);
        }

        // ════════════════════════════════════════════════════════════════════════
        // DISTRIBUCIÓN TEMPORAL CON AJUSTE DE RESIDUO
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Distribuye un importe total entre N periodos proporcionalmente a sus pesos.
        ///
        /// GARANTÍA CRÍTICA (Ajuste de Residuo):
        ///   Sum(resultado) == total exactamente.
        ///   El último periodo recibe (total - acumulado_previo) para absorber
        ///   cualquier diferencia de redondeo. Esto elimina la discrepancia
        ///   entre Explosión de Insumos y Programa de Erogaciones.
        ///
        /// Ejemplo con 3 periodos y total = 1000.00, pesos=[33,33,34]:
        ///   P1 = Round(1000 * 33/100) = 330.00
        ///   P2 = Round(1000 * 33/100) = 330.00
        ///   P3 = 1000.00 - 330.00 - 330.00 = 340.00  ← residuo absorbido ✅
        /// </summary>
        public IReadOnlyList<decimal> DistribuirImporte(decimal total, IReadOnlyList<decimal> pesos)
            => _engine.DistributeAmount(total, pesos);

        /// <summary>
        /// Distribuye una cantidad total entre N periodos con Ajuste de Residuo.
        /// Usa DecimalesCantidad en lugar de DecimalesImporte.
        /// </summary>
        public IReadOnlyList<decimal> DistribuirCantidad(decimal total, IReadOnlyList<decimal> pesos)
            => _engine.DistributeQuantity(total, pesos);

        // ════════════════════════════════════════════════════════════════════════
        // SUMA DE PRECISIÓN
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Suma una colección de importes ya redondeados.
        ///
        /// NOTA: Redondea cada elemento antes de acumular para eliminar cualquier
        /// ruido binario que haya sobrevivido desde SQLite REAL → decimal.
        /// El total final también se redondea.
        /// </summary>
        public decimal SumarImportes(IEnumerable<decimal> valores)
            => _engine.SumAmounts(valores);

        /// <summary>
        /// Suma una colección de cantidades ya redondeadas.
        /// </summary>
        public decimal SumarCantidades(IEnumerable<decimal> valores)
            => _engine.SumQuantities(valores);

        /// <summary>
        /// Suma el Costo Directo de todos los conceptos hoja de un presupuesto
        /// aplicando la precisión del motor: Multiplicar(Cantidad, CostoDirectoUnitario).
        /// Reemplaza FormatoHelper.CalcularCostoDirectoConPrecision().
        ///
        /// Mapeo (Gate N1): HasMatrix == MatrizId.HasValue (no la navegación
        /// cargada ni un Id mayor que cero); los agrupadores se omiten.
        /// </summary>
        public decimal SumarCostoDirecto(IEnumerable<ConceptoPresupuesto> conceptos)
        {
            if (conceptos == null) return 0m;

            return _engine.SumDirectCost(conceptos.Select(c => new DirectCostLine(
                c.Cantidad,
                c.CostoDirectoUnitario,
                c.EsAgrupador,
                c.MatrizId.HasValue)));
        }

        // ════════════════════════════════════════════════════════════════════════
        // FORMATEO (permanece en la fachada: N0, fila 9)
        // ════════════════════════════════════════════════════════════════════════

        public string FormatCantidad(decimal valor)
            => valor.ToString($"N{DecimalesCantidad}", CultureInfo.CurrentCulture);

        public string FormatImporte(decimal valor)
            => valor.ToString($"C{DecimalesImporte}", CultureInfo.CurrentCulture);

        public string FormatPorcentaje(decimal valor)
            => valor.ToString($"N{DecimalesPorcentaje}", CultureInfo.CurrentCulture);

        public string FormatNumero(decimal valor, int decimales)
            => valor.ToString($"N{decimales}", CultureInfo.CurrentCulture);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // RECORD RESULTADO DE DESGLOSE
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Desglose completo de un precio unitario, con cada componente ya redondeado.
    /// Garantía: CD + Indirectos + Financiamiento + Utilidad + Cargos == PrecioUnitario
    /// </summary>
    public sealed record DesglosePrecios(
        decimal CostoDirecto,
        decimal Indirectos,
        decimal Financiamiento,
        decimal Utilidad,
        decimal CargosAdicionales,
        decimal PrecioUnitario,
        decimal PctIndirectosCentral = 0m,
        decimal PctIndirectosCampo  = 0m)
    {
        /// <summary>Importe de Indirectos OC (oficina central), proporcional al total de indirectos.</summary>
        public decimal IndirectosCentral =>
            (PctIndirectosCentral + PctIndirectosCampo) > 0m
                ? Math.Round(Indirectos * PctIndirectosCentral
                             / (PctIndirectosCentral + PctIndirectosCampo),
                             6, MidpointRounding.AwayFromZero)
                : 0m;

        /// <summary>Importe de Indirectos Campo.</summary>
        public decimal IndirectosCampo => Indirectos - IndirectosCentral;

        /// <summary>Subtotal CD + Indirectos.</summary>
        public decimal Subtotal1 => CostoDirecto + Indirectos;

        /// <summary>Subtotal CD + Ind + Financiamiento.</summary>
        public decimal Subtotal2 => Subtotal1 + Financiamiento;

        /// <summary>Subtotal CD + Ind + Fin + Utilidad.</summary>
        public decimal Subtotal3 => Subtotal2 + Utilidad;
    }
}