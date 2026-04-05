using System.Collections.Generic;

namespace SOPRO.Application.Models.ExternalProjects
{
    public sealed class ExternalProjectInsumoLoadResult
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public List<ExternalProjectInsumoOption> Items { get; set; } = new();
    }
}
