using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Models
{
    public sealed class ProjectSession
    {
        public ProjectSession(SOPROContext context, Proyecto project, string? databasePath = null)
        {
            Context = context;
            Project = project;
            DatabasePath = databasePath ?? context.DatabasePath;
        }

        public SOPROContext Context { get; }
        public Proyecto Project { get; }
        public string? DatabasePath { get; }
    }
}
