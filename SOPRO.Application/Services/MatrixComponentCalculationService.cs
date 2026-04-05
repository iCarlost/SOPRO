using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixComponentCalculationService
    {
        public static MatrixComponentTotals Recalculate(IList<ComponenteMatriz> componentes, int decimalesImporte)
        {
            if (componentes == null) throw new ArgumentNullException(nameof(componentes));

            foreach (var comp in componentes)
            {
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        if (comp.Material != null)
                            comp.Importe = MultiplyWithDisplayPrecision(comp.Cantidad, comp.Material.PrecioUnitario, decimalesImporte);
                        break;

                    case TipoComponenteMatriz.Maquinaria:
                        if (comp.Maquinaria != null)
                            comp.Importe = MultiplyWithDisplayPrecision(comp.Cantidad, comp.Maquinaria.CostoHorario, decimalesImporte);
                        break;

                    case TipoComponenteMatriz.Auxiliar:
                        if (comp.Auxiliar != null)
                            comp.Importe = MultiplyWithDisplayPrecision(comp.Cantidad, comp.Auxiliar.CostoDirecto, decimalesImporte);
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
                        comp.Importe = MultiplyWithDisplayPrecision(comp.Cantidad, comp.ManoDeObra.SalarioReal, decimalesImporte);
                        baseManoObra += comp.Importe;
                    }
                }
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                {
                    baseManoObra += comp.Importe;
                }
            }

            foreach (var comp in componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null)
                {
                    if (comp.ManoDeObra.EsPorcentajeMO)
                        comp.Importe = MultiplyWithDisplayPrecision(comp.Cantidad, baseManoObra, decimalesImporte);
                }
                else if (comp.TipoComponente == TipoComponenteMatriz.Herramienta && comp.Herramienta != null)
                {
                    comp.Importe = comp.Herramienta.EsPorcentajeMO
                        ? MultiplyWithDisplayPrecision(comp.Cantidad, baseManoObra, decimalesImporte)
                        : MultiplyWithDisplayPrecision(comp.Cantidad, comp.Herramienta.PrecioUnitario, decimalesImporte);
                }
            }

            var totalHerramientaPorcentajeMo = componentes
                .Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta && c.Herramienta?.EsPorcentajeMO == true)
                .Sum(c => (decimal?)c.Importe) ?? 0;

            var totals = new MatrixComponentTotals
            {
                TotalMaterial = componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Material).Sum(c => (decimal?)c.Importe) ?? 0,
                BaseManoObra = baseManoObra,
                TotalManoObra = componentes
                    .Where(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra ||
                               (c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.Auxiliar?.Tipo == TipoMatriz.Cuadrilla))
                    .Sum(c => (decimal?)c.Importe) ?? 0,
                TotalMaquinaria = componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Sum(c => (decimal?)c.Importe) ?? 0,
                TotalBasicos = componentes
                    .Where(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.Auxiliar?.Tipo != TipoMatriz.Cuadrilla)
                    .Sum(c => (decimal?)c.Importe) ?? 0,
                TotalHerramientas = componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta).Sum(c => (decimal?)c.Importe) ?? 0,
                TotalManoObraResumen = 0
            };

            totals.TotalManoObraResumen = totals.TotalManoObra + totalHerramientaPorcentajeMo;
            totals.CostoDirectoTotal = totals.TotalMaterial + totals.TotalManoObra + totals.TotalMaquinaria + totals.TotalBasicos + totals.TotalHerramientas;
            return totals;
        }

        private static decimal MultiplyWithDisplayPrecision(decimal cantidad, decimal precioUnitario, int decimalesImporte)
        {
            var puRedondeado = Round(precioUnitario, decimalesImporte);
            return Round(cantidad * puRedondeado, decimalesImporte);
        }

        private static decimal Round(decimal value, int decimals)
        {
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }
    }
}
