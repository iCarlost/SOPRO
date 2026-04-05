namespace SOPRO.Application.Models.Selector
{
    public sealed class ProjectCatalogIndexFile
    {
        public string ProjectPath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime IndexedAtUtc { get; set; }
        public DateTime ProjectLastWriteUtc { get; set; }
        public List<ProjectCatalogIndexEntry> Matrices { get; set; } = new();
    }
}
