namespace SOPRO.Application.Models.Selector
{
    public sealed class SelectorContextState
    {
        public string SelectorKey { get; set; } = string.Empty;
        public string? LastExternalProjectPath { get; set; }
        public string? LastFilterText { get; set; }
        public string? LastSearchMode { get; set; }
        public DateTime LastUsedUtc { get; set; }
    }
}
