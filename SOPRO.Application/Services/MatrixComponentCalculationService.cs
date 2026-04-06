using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixComponentCalculationService
    {
        public static MatrixComponentTotals Recalculate(IList<ComponenteMatriz> componentes, int decimalesImporte)
        {
            if (componentes == null) throw new ArgumentNullException(nameof(componentes));

            var motor = new MotorCalculoSopro(decimalesImporte, decimalesImporte, 4);

            foreach (var comp in componentes)
            {
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        if (comp.Material != null)
                            comp.Importe = motor.Multiplicar(comp.Cantidad, comp.Material.PrecioUnitario);
                        break;

                    case TipoComponenteMatriz.Maquinaria:
                        if (comp.Maquinaria != null)
                            comp.Importe = motor.Multiplicar(comp.Cantidad, comp.Maquinaria.CostoHorario);
                        break;

                    case TipoComponenteMatriz.Auxiliar:
                        if (comp.Auxiliar != null)
                            comp.Importe = motor.Multiplicar(comp.Cantidad, comp.Auxiliar.CostoDirecto);
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
                        comp.Importe = motor.Multiplicar(comp.Cantidad, comp.ManoDeObra.SalarioReal);
                        baseManoObra = motor.RedondearImporte(baseManoObra + comp.Importe);
                    }
                }
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                {
                    baseManoObra = motor.RedondearImporte(baseManoObra + comp.Importe);
                }
            }

            foreach (var comp in componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null)
                {
                    if (comp.ManoDeObra.EsPorcentajeMO)
                        comp.Importe = motor.CalcularImporteSobreBase(comp.Cantidad, baseManoObra);
                }
                else if (comp.TipoComponente == TipoComponenteMatriz.Herramienta && comp.Herramienta != null)
                {
                    comp.Importe = comp.Herramienta.EsPorcentajeMO
                        ? motor.CalcularImporteSobreBase(comp.Cantidad, baseManoObra)
                        : motor.Multiplicar(comp.Cantidad, comp.Herramienta.PrecioUnitario);
                }
            }

            var totalHerramientaPorcentajeMo = motor.SumarImportes(
                componentes
                    .Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta && c.Herramienta?.EsPorcentajeMO == true)
                    .Select(c => c.Importe));

            var totals = new MatrixComponentTotals
            {
                TotalMaterial = motor.SumarImportes(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Material).Select(c => c.Importe)),
                BaseManoObra = motor.RedondearImporte(baseManoObra),
                TotalManoObra = motor.SumarImportes(
                    componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra ||
                                           (c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.Auxiliar?.Tipo == TipoMatriz.Cuadrilla))
                               .Select(c => c.Importe)),
                TotalMaquinaria = motor.SumarImportes(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Select(c => c.Importe)),
                TotalBasicos = motor.SumarImportes(
                    componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.Auxiliar?.Tipo != TipoMatriz.Cuadrilla)
                               .Select(c => c.Importe)),
                TotalHerramientas = motor.SumarImportes(componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta).Select(c => c.Importe)),
                TotalManoObraResumen = 0
            };

            totals.TotalManoObraResumen = motor.RedondearImporte(totals.TotalManoObra + totalHerramientaPorcentajeMo);
            totals.CostoDirectoTotal = motor.RedondearImporte(
                totals.TotalMaterial + totals.TotalManoObra + totals.TotalMaquinaria + totals.TotalBasicos + totals.TotalHerramientas);
            return totals;
        }
    }
}
