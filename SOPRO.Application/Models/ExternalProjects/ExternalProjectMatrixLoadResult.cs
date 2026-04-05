using System.Collections.Generic;

namespace SOPRO.Application.Models.ExternalProjects
{
    public sealed class ExternalProjectMatrixLoadResult
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public List<ExternalProjectMatrixOption> Matrices { get; set; } = new();
    }
}
