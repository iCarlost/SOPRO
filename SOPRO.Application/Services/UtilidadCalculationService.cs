using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  UtilidadCalculationService — VERSIÓN CORREGIDA v1.0                   ║
    // ║                                                                         ║
    // ║  CAMBIOS RESPECTO A LA VERSIÓN ORIGINAL:                                ║
    // ║  [FIX-1] baseUtilidad: decimal.Round(..., 2) → motor.RedondearImporte  ║
    // ║  [FIX-2] importeUtilidad, importeIsr, importePtu, utilidadNeta:        ║
    // ║          decimal.Round(..., 2) → motor.RedondearImporte                 ║
    // ║          Ahora respetan proyecto.DecimalesImporte en lugar de           ║
    // ║          estar hardcodeados a 2 decimales.                              ║
    // ║  NOTA: porcentajeBruto y porcentajeNeto conservan precision 5 porque    ║
    // ║        son coeficientes intermedios de cálculo, no valores visibles.    ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public class UtilidadCalculationService
    {
        public UtilidadCalculationResult Calcular(
            SOPROContext context, Proyecto proyecto, UtilidadCalculationInput input)
        {
            if (context == null)  throw new ArgumentNullException(nameof(context));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (input == null)    throw new ArgumentNullException(nameof(input));

            // Motor: respeta DecimalesImporte del proyecto [FIX-1] [FIX-2]
            var motor = new MotorCalculoSopro(proyecto);

            var preview = BudgetPreviewCalculationService.BuildPreview(context, proyecto,
                new BudgetPercentageInput
                {
                    CostoDirectoReferencia = input.CostoDirectoReferencia,
                    IndirectosCentral      = input.IndirectosCentral,
                    IndirectosCampo        = input.IndirectosCampo,
                    Financiamiento         = input.Financiamiento,
                    Utilidad               = 0m,
                    CargosAdicionales      = 0m,
                    ModoCalculoPorcentajes = input.ModoCalculoPorcentajes
                });

            // [FIX-1] Usar motor en lugar de decimal.Round(..., 2)
            decimal baseUtilidad = motor.RedondearImporte(preview.Subtotal2);

            decimal porcentajeBruto;
            decimal porcentajeNeto;

            if (input.ModoAsistido)
            {
                porcentajeNeto = Math.Max(0m, input.UtilidadNetaDeseada);
                decimal factor = 1m - ((Math.Max(0m, input.Isr) + Math.Max(0m, input.Ptu)) / 100m);
                // porcentajeBruto es un coeficiente intermedio — mantener 5 decimales de precisión
                porcentajeBruto = factor > 0m
                    ? Math.Round(porcentajeNeto / factor, 5, MidpointRounding.AwayFromZero)
                    : 0m;
            }
            else
            {
                porcentajeBruto = Math.Max(0m, input.UtilidadDirecta);
                decimal factor  = 1m - ((Math.Max(0m, input.Isr) + Math.Max(0m, input.Ptu)) / 100m);
                // ídem — coeficiente intermedio
                porcentajeNeto  = Math.Round(porcentajeBruto * factor, 5, MidpointRounding.AwayFromZero);
            }

            // [FIX-2] Importes visibles: usar motor.RedondearImporte (respeta DecimalesImporte)
            decimal importeUtilidad = motor.RedondearImporte(baseUtilidad * porcentajeBruto / 100m);
            decimal importeIsr      = motor.RedondearImporte(importeUtilidad * Math.Max(0m, input.Isr) / 100m);
            decimal importePtu      = motor.RedondearImporte(importeUtilidad * Math.Max(0m, input.Ptu) / 100m);
            decimal utilidadNeta    = motor.RedondearImporte(importeUtilidad - importeIsr - importePtu);

            return new UtilidadCalculationResult
            {
                BaseUtilidad           = baseUtilidad,
                PorcentajeUtilidadBruta = porcentajeBruto,
                PorcentajeUtilidadNeta  = porcentajeNeto,
                Isr                    = input.Isr,
                Ptu                    = input.Ptu,
                ImporteUtilidad        = importeUtilidad,
                ImporteIsr             = importeIsr,
                ImportePtu             = importePtu,
                UtilidadNetaEstimada   = utilidadNeta,
                Modo                   = input.ModoAsistido ? "Asistido" : "Directo"
            };
        }
    }
}
