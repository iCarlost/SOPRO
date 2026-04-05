using System.Collections.Generic;

namespace SOPRO.Application.Models.ExternalProjects
{
    public sealed class ExternalInsumoImportPreview
    {
        public string SourceProjectName { get; set; } = string.Empty;
        public int SelectedItems { get; set; }
        public int MatrixDependencies { get; set; }
        public int MaterialConflicts { get; set; }
        public int ManoDeObraConflicts { get; set; }
        public int MaquinariaConflicts { get; set; }
        public int HerramientaConflicts { get; set; }
        public int MatrizConflicts { get; set; }
        public List<string> Items { get; set; } = new();
        public List<string> ConflictLines { get; set; } = new();
        public int TotalConflicts => MaterialConflicts + ManoDeObraConflicts + MaquinariaConflicts + HerramientaConflicts + MatrizConflicts;
    }
}
