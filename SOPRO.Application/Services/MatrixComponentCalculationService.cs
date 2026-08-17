using Sopro.Calculation;
using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  MatrixComponentCalculationService — delegador directo al motor          ║
    // ║  (N5-2, PLAN-01 §14): toda aritmética vive en SoproCalculationEngine;   ║
    // ║  esta clase ya no pasa por la fachada legacy MotorCalculoSopro.          ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public static class MatrixComponentCalculationService
    {
        public static MatrixComponentTotals Recalculate(IList<ComponenteMatriz> componentes, int decimalesImporte)
        {
            if (componentes == null) throw new ArgumentNullException(nameof(componentes));

            var engine = new SoproCalculationEngine(decimalesImporte, decimalesImporte, 4);

            foreach (var comp in componentes)
            {
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        if (comp.Material != null)
                            comp.Importe = engine.Multiply(comp.Cantidad, comp.Material.PrecioUnitario);
                        break;

                    case TipoComponenteMatriz.Maquinaria:
                        if (comp.Maquinaria != null)
                            comp.Importe = engine.Multiply(comp.Cantidad, comp.Maquinaria.CostoHorario);
                        break;

                    case TipoComponenteMatriz.Auxiliar:
                        if (comp.Auxiliar != null)
                            comp.Importe = engine.Multiply(comp.Cantidad, comp.Auxiliar.CostoDirecto);
                        break;
                }
            }

            var baseManoObra = 0m;
            foreach (var comp in componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null)
                {
                    if (!comp.ManoDeObra.EsPorcentajeMO)
                    {
                        comp.Importe = engine.Multiply(comp.Cantidad, comp.ManoDeObra.SalarioReal);
                        baseManoObra = engine.RoundAmount(baseManoObra + comp.Importe);
                    }
                }
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                {
                    baseManoObra = engine.RoundAmount(baseManoObra + comp.Importe);
                }
            }

            foreach (var comp in componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null)
                {
                    if (comp.ManoDeObra.EsPorcentajeMO)
                        comp.Importe = engine.CalculateAmountOverBase(comp.Cantidad, baseManoObra);
                }
                else if (comp.TipoComponente == TipoComponenteMatriz.Herramienta && comp.Herramienta != null)
                {
                    comp.Importe = comp.Herramienta.EsPorcentajeMO
                        ? engine.CalculateAmountOverBase(comp.Cantidad, baseManoObra)
                        : engine.Multiply(comp.Cantidad, comp.Herramienta.PrecioUnitario);
                }
            }

            var totalHerramientaPorcentajeMo = engine.SumAmounts(
                componentes
                    .Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta && c.Herramienta?.EsPorcentajeMO == true)
                    .Select(c => c.Importe));

            var totals = new MatrixComponentTotals
            {
                TotalMaterial = engine.SumAmounts(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Material).Select(c => c.Importe)),
                BaseManoObra = engine.RoundAmount(baseManoObra),
                TotalManoObra = engine.SumAmounts(
                    componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra ||
                                           (c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.Auxiliar?.Tipo == TipoMatriz.Cuadrilla))
                               .Select(c => c.Importe)),
                TotalMaquinaria = engine.SumAmounts(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Select(c => c.Importe)),
                TotalBasicos = engine.SumAmounts(
                    componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.Auxiliar?.Tipo != TipoMatriz.Cuadrilla)
                               .Select(c => c.Importe)),
                TotalHerramientas = engine.SumAmounts(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta).Select(c => c.Importe)),
                TotalManoObraResumen = 0
            };

            totals.TotalManoObraResumen = engine.RoundAmount(totals.TotalManoObra + totalHerramientaPorcentajeMo);
            totals.CostoDirectoTotal = engine.RoundAmount(
                totals.TotalMaterial + totals.TotalManoObra + totals.TotalMaquinaria + totals.TotalBasicos + totals.TotalHerramientas);
            return totals;
        }
    }
}
