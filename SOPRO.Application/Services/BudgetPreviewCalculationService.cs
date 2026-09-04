using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  BudgetPreviewCalculationService — VERSIÓN CORREGIDA v2.0              ║
    // ║                                                                         ║
    // ║  [N5-3] Toda aritmética delegada directo a SoproCalculationEngine      ║
    // ║         (SOPRO.Calculation), sin pasar por la fachada legacy.          ║
    // ║                                                                         ║
    // ║  CAMBIOS RESPECTO A LA VERSIÓN ORIGINAL:                                ║
    // ║  [FIX-1] Eliminado MultiplyUsingDisplayPrecision privado duplicado.    ║
    // ║  [FIX-2] Eliminado RoundImporte privado duplicado.                     ║
    // ║  [FIX-3] BuildPreview: desglose por concepto usa CalcularPrecioUnitario ║
    // ║          para redondear cada paso intermedio — igual que                ║
    // ║          BudgetLoadService. Los totales acumulan importes ya            ║
    // ║          redondeados por concepto.                                      ║
    // ║  [FIX-4] BuildPreviewFromReferenceCost: pasos sin Round → con motor.   ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public static class BudgetPreviewCalculationService
    {
        public static BudgetPercentagePreviewResult BuildPreview(
            SOPROContext context, Proyecto proyecto, BudgetPercentageInput input)
        {
            if (context == null)  throw new ArgumentNullException(nameof(context));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (input == null)    throw new ArgumentNullException(nameof(input));

            var conceptos = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == proyecto.Id && !c.EsAgrupador && c.MatrizId.HasValue)
                .AsNoTracking()
                .AsEnumerable()
                .ToList();

            if (conceptos.Count == 0)
                return BuildPreviewFromReferenceCost(input.CostoDirectoReferencia, input, proyecto);

            // [N5-3] Motor del paquete directo: fuente única de aritmética
            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            decimal totalCd     = 0m;
            decimal totalOc     = 0m;
            decimal totalCampo  = 0m;
            decimal totalSub1   = 0m;
            decimal totalFin    = 0m;
            decimal totalSub2   = 0m;
            decimal totalUtil   = 0m;
            decimal totalSub3   = 0m;
            decimal totalCargos = 0m;
            decimal totalFinal  = 0m;

            foreach (var concepto in conceptos)
            {
                decimal cdUnit   = engine.RoundAmount(concepto.CostoDirectoUnitario);
                decimal cantidad = concepto.Cantidad;

                // [FIX-3] Desglose con redondeo en cada paso — igual que BudgetLoadService
                var desglose = engine.CalculateUnitPrice(cdUnit, input.ToPricePercentage());

                totalCd     += engine.Multiply(cantidad, cdUnit);
                totalOc     += engine.Multiply(cantidad, desglose.CentralIndirectCosts);
                totalCampo  += engine.Multiply(cantidad, desglose.FieldIndirectCosts);
                totalSub1   += engine.Multiply(cantidad, desglose.Subtotal1);
                totalFin    += engine.Multiply(cantidad, desglose.Financing);
                totalSub2   += engine.Multiply(cantidad, desglose.Subtotal2);
                totalUtil   += engine.Multiply(cantidad, desglose.Profit);
                totalSub3   += engine.Multiply(cantidad, desglose.Subtotal3);
                totalCargos += engine.Multiply(cantidad, desglose.AdditionalCharges);
                totalFinal  += engine.Multiply(cantidad, desglose.UnitPrice);
            }

            return new BudgetPercentagePreviewResult
            {
                CostoDirecto           = totalCd,
                MontoIndirectosCentral = totalOc,
                MontoIndirectosCampo   = totalCampo,
                Subtotal1              = totalSub1,
                MontoFinanciamiento    = totalFin,
                Subtotal2              = totalSub2,
                MontoUtilidad          = totalUtil,
                Subtotal3              = totalSub3,
                MontoCargosAdicionales = totalCargos,
                PrecioUnitarioFinal    = totalFinal,
                CalculadoDesdeConceptos = true,
                ConceptosProcesados    = conceptos.Count
            };
        }

        /// <summary>
        /// Preview basado en un costo directo de referencia (cuando no hay conceptos).
        /// [FIX-4] Ahora recibe Proyecto para respetar DecimalesImporte.
        /// </summary>
        public static BudgetPercentagePreviewResult BuildPreviewFromReferenceCost(
            decimal costoDirecto, BudgetPercentageInput input, Proyecto? proyecto = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            // Si hay proyecto, usar motor para redondear; si no, valores de alta precisión
            // (este path solo se llega cuando no hay conceptos — impacto mínimo)
            var engine = proyecto != null
                ? CalculationEngineFactory.FromProyecto(proyecto)
                : null;

            decimal R(decimal v) => engine != null ? engine.RoundAmount(v) : v;

            decimal mOC, mCampo, sub1, mFin, sub2, mUtil, sub3, mCarg, puFinal;
            decimal cd = R(costoDirecto);

            bool sobreCD = string.Equals(input.ModoCalculoPorcentajes, "SobreCD",
                                         StringComparison.OrdinalIgnoreCase);
            if (!sobreCD)
            {
                mOC    = R(cd * (input.IndirectosCentral / 100m));
                mCampo = R(cd * (input.IndirectosCampo   / 100m));
                sub1   = R(cd + mOC + mCampo);
                mFin   = R(sub1 * (input.Financiamiento / 100m));
                sub2   = R(sub1 + mFin);
                mUtil  = R(sub2 * (input.Utilidad / 100m));
                sub3   = R(sub2 + mUtil);
                mCarg  = R(sub3 * (input.CargosAdicionales / 100m));
                puFinal = R(sub3 + mCarg);
            }
            else
            {
                mOC    = R(cd * (input.IndirectosCentral  / 100m));
                mCampo = R(cd * (input.IndirectosCampo    / 100m));
                mFin   = R(cd * (input.Financiamiento     / 100m));
                mUtil  = R(cd * (input.Utilidad           / 100m));
                mCarg  = R(cd * (input.CargosAdicionales  / 100m));
                sub1   = R(cd + mOC + mCampo);
                sub2   = R(sub1 + mFin);
                sub3   = R(sub2 + mUtil);
                puFinal = R(sub3 + mCarg);
            }

            return new BudgetPercentagePreviewResult
            {
                CostoDirecto           = cd,
                MontoIndirectosCentral = mOC,
                MontoIndirectosCampo   = mCampo,
                Subtotal1              = sub1,
                MontoFinanciamiento    = mFin,
                Subtotal2              = sub2,
                MontoUtilidad          = mUtil,
                Subtotal3              = sub3,
                MontoCargosAdicionales = mCarg,
                PrecioUnitarioFinal    = puFinal,
                CalculadoDesdeConceptos = false,
                ConceptosProcesados    = 0
            };
        }

        // [FIX-1][FIX-2][N5-3] Eliminados:
        //   private static decimal MultiplyUsingDisplayPrecision(...)
        //   private static decimal RoundImporte(...)
        // Reemplazados por SoproCalculationEngine.Multiply() y .RoundAmount()
        // [N7-9] Wrapper BuildEnginePercentages retirado: el callsite usa
        // input.ToPricePercentage() directamente.
    }
}
