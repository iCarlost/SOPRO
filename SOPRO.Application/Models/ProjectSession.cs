using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Models
{
    /// <summary>
    /// Sesión de proyecto abierta: posee el contexto de datos y el candado de
    /// workspace (N4-2). El cierre se hace con Dispose: la sesión es la dueña de
    /// ambos recursos y nadie más debe acceder a ellos por fuera de ella.
    /// </summary>
    public sealed class ProjectSession : IDisposable
    {
        private readonly IWorkspaceLock? _workspaceLock;

        public ProjectSession(
            SOPROContext context,
            Proyecto project,
            string? databasePath = null,
            IWorkspaceLock? workspaceLock = null)
        {
            Context = context;
            Project = project;
            DatabasePath = databasePath ?? context.DatabasePath;
            _workspaceLock = workspaceLock;
        }

        public SOPROContext Context { get; }
        public Proyecto Project { get; }
        public string? DatabasePath { get; }

        public void Dispose()
        {
            _workspaceLock?.Dispose();
            Context.Dispose();
        }
    }
}