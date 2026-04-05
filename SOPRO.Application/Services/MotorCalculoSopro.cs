using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║          MOTOR DE CÁLCULO SOPRO — v2.0  (Auditado 2026-03)             ║
    // ║          Fuente única de verdad para aritmética con precisión           ║
    // ║          de pantalla. TODOS los servicios deben usar esta clase.        ║
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
    // ║                                                                         ║
    // ║  DEUDA TÉCNICA DOCUMENTADA (no urgente):                                ║
    // ║    ⚠ Matriz.CalcularCostoDirecto(int) — algoritmo duplicado por        ║
    // ║      dependencia circular entre SOPRO.Core y SOPRO.Application.         ║
    // ║      Funciona correctamente; fuente canónica: MatrixComponentCalc...    ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Motor centralizado de cálculo aritmético con "Precisión de Pantalla".
    ///
    /// PRINCIPIO FUNDAMENTAL:
    ///   Lo que el usuario VE en pantalla es exactamente lo que se PROCESA.
    ///   Antes de multiplicar, el P.U. se redondea a los decimales visibles.
    ///   El resultado se redondea igual. Nunca se acumulan valores sin redondear.
    ///
    /// REGLA DE ORO:
    ///   Redondear cada importe individual ANTES de acumular en totales.
    ///   El último elemento de una distribución absorbe el residuo de redondeo
    ///   para que SUM(partes) == total exactamente.
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
        // ── Acceso interno para que RecalculoGlobalService pueda componer ──────
        internal int DecimalesCantidad  { get; }
        internal int DecimalesImporte   { get; }
        internal int DecimalesPorcentaje { get; }

        public MotorCalculoSopro(Proyecto proyecto)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            DecimalesCantidad   = Math.Max(0, proyecto.DecimalesCantidad);
            DecimalesImporte    = Math.Max(0, proyecto.DecimalesImporte);
            DecimalesPorcentaje = Math.Max(0, proyecto.DecimalesPorcentaje);
        }

        /// <summary>Constructor de pruebas / sin proyecto EF.</summary>
        public MotorCalculoSopro(int decimalesCantidad, int decimalesImporte, int decimalesPorcentaje)
        {
            DecimalesCantidad   = Math.Max(0, decimalesCantidad);
            DecimalesImporte    = Math.Max(0, decimalesImporte);
            DecimalesPorcentaje = Math.Max(0, decimalesPorcentaje);
        }

        // ════════════════════════════════════════════════════════════════════════
        // PRIMITIVAS DE REDONDEO
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Redondea una cantidad al número de decimales configurado para cantidades.</summary>
        public decimal RedondearCantidad(decimal valor)
            => Math.Round(valor, DecimalesCantidad, MidpointRounding.AwayFromZero);

        /// <summary>Redondea un importe/precio al número de decimales configurado para importes.</summary>
        public decimal RedondearImporte(decimal valor)
            => Math.Round(valor, DecimalesImporte, MidpointRounding.AwayFromZero);

        /// <summary>Redondea un porcentaje al número de decimales configurado para porcentajes.</summary>
        public decimal RedondearPorcentaje(decimal valor)
            => Math.Round(valor, DecimalesPorcentaje, MidpointRounding.AwayFromZero);

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
        {
            decimal puVisible = RedondearImporte(precioUnitario);
            return RedondearImporte(cantidad * puVisible);
        }

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
        /// </summary>
        public DesglosePrecios CalcularPrecioUnitario(decimal costoDirecto, BudgetPercentageInput pct)
        {
            if (pct == null) throw new ArgumentNullException(nameof(pct));

            bool sobreCD = string.Equals(pct.ModoCalculoPorcentajes, "SobreCD",
                                         StringComparison.OrdinalIgnoreCase);
            decimal cd = RedondearImporte(costoDirecto);

            // ── Indirectos ──────────────────────────────────────────────────────
            decimal pInd = pct.IndirectosCentral + pct.IndirectosCampo;
            decimal mInd = RedondearImporte(cd * pInd / 100m);
            decimal sub1 = RedondearImporte(cd + mInd);

            // ── Financiamiento ──────────────────────────────────────────────────
            decimal baseFin = sobreCD ? cd : sub1;
            decimal mFin    = RedondearImporte(baseFin * pct.Financiamiento / 100m);
            decimal sub2    = RedondearImporte(sub1 + mFin);

            // ── Utilidad ────────────────────────────────────────────────────────
            decimal baseUtil = sobreCD ? cd : sub2;
            decimal mUtil    = RedondearImporte(baseUtil * pct.Utilidad / 100m);
            decimal sub3     = RedondearImporte(sub2 + mUtil);

            // ── Cargos Adicionales ──────────────────────────────────────────────
            decimal baseCargos = sobreCD ? cd : sub3;
            decimal mCargos    = RedondearImporte(baseCargos * pct.CargosAdicionales / 100m);

            // ── P.U. final ──────────────────────────────────────────────────────
            // Se construye como suma de partes ya redondeadas para garantizar cuadre
            decimal pu = RedondearImporte(sub3 + mCargos);

            return new DesglosePrecios(cd, mInd, mFin, mUtil, mCargos, pu,
                                       pct.IndirectosCentral, pct.IndirectosCampo);
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
        {
            if (pesos == null || pesos.Count == 0) return Array.Empty<decimal>();

            decimal sumaPesos = pesos.Sum();
            if (sumaPesos == 0m)
                return pesos.Select(_ => 0m).ToList();

            var resultado = new decimal[pesos.Count];
            decimal acumulado = 0m;

            for (int i = 0; i < pesos.Count - 1; i++)
            {
                decimal proporcion = pesos[i] / sumaPesos;
                resultado[i]  = RedondearImporte(total * proporcion);
                acumulado     += resultado[i];
            }

            // ✅ Ajuste de Residuo: último periodo = exactamente lo que falta
            resultado[^1] = RedondearImporte(total - acumulado);
            return resultado;
        }

        /// <summary>
        /// Distribuye una cantidad total entre N periodos con Ajuste de Residuo.
        /// Usa DecimalesCantidad en lugar de DecimalesImporte.
        /// </summary>
        public IReadOnlyList<decimal> DistribuirCantidad(decimal total, IReadOnlyList<decimal> pesos)
        {
            if (pesos == null || pesos.Count == 0) return Array.Empty<decimal>();

            decimal sumaPesos = pesos.Sum();
            if (sumaPesos == 0m)
                return pesos.Select(_ => 0m).ToList();

            var resultado = new decimal[pesos.Count];
            decimal acumulado = 0m;

            for (int i = 0; i < pesos.Count - 1; i++)
            {
                decimal proporcion = pesos[i] / sumaPesos;
                resultado[i]  = RedondearCantidad(total * proporcion);
                acumulado     += resultado[i];
            }

            resultado[^1] = RedondearCantidad(total - acumulado);
            return resultado;
        }

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
        {
            if (valores == null) return 0m;
            decimal acc = 0m;
            foreach (var v in valores)
                acc += RedondearImporte(v);
            return RedondearImporte(acc);
        }

        /// <summary>
        /// Suma una colección de cantidades ya redondeadas.
        /// </summary>
        public decimal SumarCantidades(IEnumerable<decimal> valores)
        {
            if (valores == null) return 0m;
            decimal acc = 0m;
            foreach (var v in valores)
                acc += RedondearCantidad(v);
            return RedondearCantidad(acc);
        }

        /// <summary>
        /// Suma el Costo Directo de todos los conceptos hoja de un presupuesto
        /// aplicando la precisión del motor: Multiplicar(Cantidad, CostoDirectoUnitario).
        /// Reemplaza FormatoHelper.CalcularCostoDirectoConPrecision().
        /// </summary>
        public decimal SumarCostoDirecto(IEnumerable<SOPRO.Core.Entities.ConceptoPresupuesto> conceptos)
        {
            if (conceptos == null) return 0m;
            decimal total = 0m;
            foreach (var c in conceptos)
            {
                if (c.EsAgrupador || !c.MatrizId.HasValue) continue;
                total += Multiplicar(c.Cantidad, c.CostoDirectoUnitario);
            }
            return RedondearImporte(total);
        }

        // ════════════════════════════════════════════════════════════════════════
        // FORMATEO
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
