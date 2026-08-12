using SOPRO.Application.Models;
using SOPRO.Data.Context;

namespace SOPRO.Application.Contracts;

/// <summary>
/// Información de sesión neutral para los casos de uso de la frontera de
/// Application: identidad del proyecto y ruta de la base de datos.
///
/// REGLA N3: esta clase NO expone <see cref="SOPROContext"/> en su superficie
/// pública; el contexto se resuelve de forma interna (puente temporal hasta
/// que N4 sustituya el ciclo de vida del contexto por operación).
/// </summary>
public sealed record ProjectSessionInfo
{
    /// <summary>Proyecto activo (o catálogo maestro).</summary>
    public ProjectRef Project { get; }

    /// <summary>Ruta de la base de datos de la sesión.</summary>
    public string? DatabasePath { get; }

    /// <summary>Puente interno: contexto de la sesión. Visible solo dentro de Application.</summary>
    internal SOPROContext Context { get; }

    private ProjectSessionInfo(ProjectRef project, SOPROContext context, string? databasePath)
    {
        Project = project;
        Context = context;
        DatabasePath = databasePath ?? context.DatabasePath;
    }

    /// <summary>Crea la información de sesión desde una sesión de proyecto existente.</summary>
    public static ProjectSessionInfo From(ProjectSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new ProjectSessionInfo(
            new ProjectRef(session.Project.Id, session.Project.Nombre ?? string.Empty),
            session.Context,
            session.DatabasePath);
    }

    /// <summary>
    /// Puente temporal desde los componentes legacy (UI, servicios): construye la
    /// información de sesión a partir del contexto compartido y del Id de proyecto.
    /// Si <paramref name="projectId"/> es <c>null</c>, la sesión corresponde al
    /// catálogo maestro.
    /// </summary>
    /// <remarks>Puente N3→N4: será innecesario cuando el ciclo de vida del contexto
    /// quede por operación (PLAN-01 §13).</remarks>
    public static ProjectSessionInfo FromLegacy(SOPROContext context, int? projectId, string? databasePath = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new ProjectSessionInfo(
            projectId.HasValue
                ? new ProjectRef(projectId, context.Proyectos.Find(projectId.Value)?.Nombre ?? string.Empty)
                : ProjectRef.Master,
            context,
            databasePath);
    }
}