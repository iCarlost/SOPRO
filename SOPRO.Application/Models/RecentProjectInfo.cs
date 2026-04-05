using System;

namespace SOPRO.Application.Models
{
    public sealed class RecentProjectInfo
    {
        public string Name { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public DateTime LastModified { get; init; }
    }
}
