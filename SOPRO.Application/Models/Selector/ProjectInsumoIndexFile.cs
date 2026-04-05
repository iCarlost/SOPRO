namespace SOPRO.Application.Models.Selector
{
    public sealed class ProjectInsumoIndexFile
    {
        public string ProjectPath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime IndexedAtUtc { get; set; }
        public DateTime ProjectLastWriteUtc { get; set; }
        public List<ProjectInsumoIndexEntry> Insumos { get; set; } = new();
    }
}
