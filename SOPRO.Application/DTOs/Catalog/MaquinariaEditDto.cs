using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Catalog
{
    public sealed class MaquinariaEditDto
    {
        public string Clave { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal PotenciaNominal { get; set; }
        public TipoCombustible TipoCombustible { get; set; }
        public decimal CostoHorario { get; set; }
        public bool EsCostoCalculado { get; set; }
        public string Notas { get; set; } = string.Empty;
        public bool GuardarEnMaestro { get; set; }
        public int? ProyectoId { get; set; }
    }
}
