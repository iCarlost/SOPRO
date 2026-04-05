using System.Collections.Generic;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.ExternalProjects
{
    public sealed class ExternalInsumoImportResult
    {
        public List<ComponenteMatriz> ImportedComponents { get; set; } = new();
        public int ImportedMateriales { get; set; }
        public int ImportedManoDeObra { get; set; }
        public int ImportedMaquinaria { get; set; }
        public int ImportedHerramientas { get; set; }
        public int ImportedMatrices { get; set; }
    }
}
