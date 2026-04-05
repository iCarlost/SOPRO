using System.Collections.Generic;

namespace SOPRO.Application.Models.Explosion
{
    public sealed class ExplosionCalculationResult
    {
        public Dictionary<int, ExplosionInsumoAccumulated> Materiales { get; set; } = new();
        public Dictionary<int, ExplosionInsumoAccumulated> ManoObra { get; set; } = new();
        public Dictionary<int, ExplosionInsumoAccumulated> Maquinaria { get; set; } = new();
        public Dictionary<int, ExplosionInsumoAccumulated> Herramientas { get; set; } = new();
        public decimal CostoDirectoTotal { get; set; }
        public decimal CostoDirectoPresupuesto { get; set; }
        public List<ExplosionRowDisplay> Rows { get; set; } = new();
        public bool TieneConceptos => Materiales.Count > 0 || ManoObra.Count > 0 || Maquinaria.Count > 0 || Herramientas.Count > 0;
    }
}
