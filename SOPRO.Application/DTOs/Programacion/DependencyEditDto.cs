using SOPRO.Core.Entities;

namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class DependencyEditDto
    {
        public int ActividadOrigenId { get; set; }
        public int ActividadDestinoId { get; set; }
        public TipoDependenciaActividad TipoDependencia { get; set; }
        public int DesfaseDias { get; set; }
    }
}
