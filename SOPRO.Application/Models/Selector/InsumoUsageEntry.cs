using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Selector
{
    public sealed class InsumoUsageEntry
    {
        public string ProjectPath { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public TipoComponenteMatriz TipoComponente { get; set; }
        public string? Tag { get; set; }
        public int UsageCount { get; set; }
        public DateTime LastUsedUtc { get; set; }
    }
}
