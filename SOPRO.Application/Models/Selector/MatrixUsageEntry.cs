namespace SOPRO.Application.Models.Selector
{
    public sealed class MatrixUsageEntry
    {
        public string ProjectPath { get; set; } = string.Empty;
        public int MatrixId { get; set; }
        public int UsageCount { get; set; }
        public DateTime LastUsedUtc { get; set; }
    }
}
