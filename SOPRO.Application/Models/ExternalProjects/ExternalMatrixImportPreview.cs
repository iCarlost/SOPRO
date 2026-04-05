namespace SOPRO.Application.Models.ExternalProjects
{
    public sealed class ExternalMatrixImportPreview
    {
        public string MatrixKey { get; set; } = string.Empty;
        public string MatrixDescription { get; set; } = string.Empty;
        public string SourceProjectName { get; set; } = string.Empty;
        public int TotalMatrices { get; set; }
        public int TotalMateriales { get; set; }
        public int TotalManoDeObra { get; set; }
        public int TotalMaquinaria { get; set; }
        public int TotalHerramientas { get; set; }
        public int TotalBasicos { get; set; }
        public int TotalCuadrillas { get; set; }
        public int MaxDepth { get; set; }
        public int MatrixConflicts { get; set; }
        public int MaterialConflicts { get; set; }
        public int ManoDeObraConflicts { get; set; }
        public int MaquinariaConflicts { get; set; }
        public int HerramientaConflicts { get; set; }
        public System.Collections.Generic.List<string> DependencyTreeLines { get; set; } = new();
        public System.Collections.Generic.List<string> ConflictKeyLines { get; set; } = new();
        public System.Collections.Generic.List<string> ActionSummaryLines { get; set; } = new();
        public System.Collections.Generic.List<string> ImpactSummaryLines { get; set; } = new();

        public int TotalConflicts => MatrixConflicts + MaterialConflicts + ManoDeObraConflicts + MaquinariaConflicts + HerramientaConflicts;
    }
}
