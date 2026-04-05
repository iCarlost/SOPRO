using System;
using System.Collections.Generic;
using SOPRO.Core.Entities;

namespace SOPRO.WinForms.Services
{
    internal static class ApuPrecioUnitarioIntegracionHelper
    {
        internal sealed class LineaIntegracion
        {
            public string Etiqueta { get; init; } = string.Empty;
            public string PorcentajeTexto { get; init; } = string.Empty;
            public decimal Monto { get; init; }
        }

        internal sealed class ResultadoIntegracion
        {
            public decimal CostoDirecto { get; init; }
            public decimal PorcentajeIndirectos { get; init; }
            public decimal PorcentajeFinanciamiento { get; init; }
            public decimal PorcentajeUtilidad { get; init; }
            public decimal PorcentajeCargos { get; init; }
            public decimal MontoIndirectos { get; init; }
            public decimal MontoFinanciamiento { get; init; }
            public decimal MontoUtilidad { get; init; }
            public decimal MontoCargos { get; init; }
            public decimal PrecioUnitario { get; init; }
            public bool SobreCostoDirecto { get; init; }
            public IReadOnlyList<LineaIntegracion> Lineas { get; init; } = Array.Empty<LineaIntegracion>();
        }

        public static ResultadoIntegracion Calcular(Proyecto proyecto, ConceptoPresupuesto concepto, decimal costoDirecto)
        {
            decimal pctInd = proyecto.PorcentajeIndirectosCentral + proyecto.PorcentajeIndirectosCampo;
            decimal pctFin = proyecto.PorcentajeFinanciamiento;
            decimal pctUtil = proyecto.PorcentajeUtilidad;
            decimal pctCargos = proyecto.PorcentajeCargosAdicionales;

            decimal cd = costoDirecto;
            bool sobreCD = string.Equals(proyecto.ModoCalculoPorcentajes, "SobreCD", StringComparison.OrdinalIgnoreCase);

            decimal mtoInd = cd * (pctInd / 100m);
            decimal mtoFin = sobreCD
                ? cd * (pctFin / 100m)
                : (cd + mtoInd) * (pctFin / 100m);
            decimal mtoUtil = sobreCD
                ? cd * (pctUtil / 100m)
                : (cd + mtoInd + mtoFin) * (pctUtil / 100m);
            decimal mtoCargos = sobreCD
                ? cd * (pctCargos / 100m)
                : (cd + mtoInd + mtoFin + mtoUtil) * (pctCargos / 100m);

            decimal cdMasCargas = cd + mtoInd + mtoFin + mtoUtil + mtoCargos;
            decimal pu = concepto.PrecioUnitario > 0 ? concepto.PrecioUnitario : cdMasCargas;

            string labelFin = sobreCD ? $"{pctFin:N2}% de CD" : $"{pctFin:N2}% de (CD+CI)";
            string labelUtil = sobreCD ? $"{pctUtil:N2}% de CD" : $"{pctUtil:N2}% de (CD+CI+CF)";
            string labelCarg = sobreCD ? $"{pctCargos:N2}% de CD" : $"{pctCargos:N2}% de (CD+CI+CF+U)";

            var lineas = new List<LineaIntegracion>
            {
                new() { Etiqueta = "Costo Directo (CD)", PorcentajeTexto = string.Empty, Monto = cd },
                new() { Etiqueta = "Indirectos", PorcentajeTexto = $"{pctInd:N2}% de CD", Monto = mtoInd },
                new() { Etiqueta = "Financiamiento", PorcentajeTexto = labelFin, Monto = mtoFin },
                new() { Etiqueta = "Utilidad", PorcentajeTexto = labelUtil, Monto = mtoUtil }
            };
            if (mtoCargos > 0)
                lineas.Add(new LineaIntegracion { Etiqueta = "Cargos Adicionales", PorcentajeTexto = labelCarg, Monto = mtoCargos });

            return new ResultadoIntegracion
            {
                CostoDirecto = cd,
                PorcentajeIndirectos = pctInd,
                PorcentajeFinanciamiento = pctFin,
                PorcentajeUtilidad = pctUtil,
                PorcentajeCargos = pctCargos,
                MontoIndirectos = mtoInd,
                MontoFinanciamiento = mtoFin,
                MontoUtilidad = mtoUtil,
                MontoCargos = mtoCargos,
                PrecioUnitario = pu,
                SobreCostoDirecto = sobreCD,
                Lineas = lineas
            };
        }
    }
}
