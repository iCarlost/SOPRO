namespace SOPRO.Application.Models.Selector
{
    public sealed class ProjectIndexEntry
    {
        public string ProjectPath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime LastOpenedUtc { get; set; }
        public bool IsFavorite { get; set; }
    }
}
