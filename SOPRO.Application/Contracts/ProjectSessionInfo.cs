using SOPRO.Application.Models;
using SOPRO.Data.Context;

namespace SOPRO.Application.Contracts;

/// <summary>
/// Información de sesión neutral para los casos de uso de la frontera de
/// Application: identidad del proyecto y rutas de la base de datos.
///
/// REGLA N3: esta clase NO expone ni recibe <see cref="SOPROContext"/> en su
/// superficie pública; el contexto se resuelve de forma interna (puente temporal
/// hasta que N4 sustituya el ciclo de vida del contexto por operación).
/// </summary>
public sealed record ProjectSessionInfo : IDisposable
{
    /// <summary>Proyecto activo (o catálogo maestro).</summary>
    public ProjectRef Project { get; }

    /// <summary>Ruta de la base de datos de la sesión.</summary>
    public string DatabasePath { get; }

    /// <summary>Ruta de la base del catálogo maestro (CatalogoMaestro.db).</summary>
    public string MasterDatabasePath { get; }

    /// <summary>Decimales de importe del proyecto (null en catálogo maestro).</summary>
    public int? DecimalesImporte { get; }

    /// <summary>Puente interno: contexto de la sesión. Visible solo dentro de Application.</summary>
    internal SOPROContext Context { get; }

    private ProjectSessionInfo(
        ProjectRef project,
        string databasePath,
        string masterDatabasePath,
        int? decimalesImporte,
        SOPROContext context)
    {
        Project = project;
        DatabasePath = databasePath;
        MasterDatabasePath = masterDatabasePath;
        DecimalesImporte = decimalesImporte;
        Context = context;
    }

    /// <summary>
    /// Crea la información de sesión a partir de un proyecto y la ruta de su
    /// base de datos. Si <paramref name="projectId"/> es <c>null</c>, la sesión
    /// corresponde al catálogo maestro.
    /// </summary>
    /// <remarks>
    /// El contexto se resuelve de la ruta (una operación, un contexto a partir
    /// de N4). La sesión es la dueña del contexto: el consumidor debe disponerla.
    /// </remarks>
    public static ProjectSessionInfo Create(
        int? projectId,
        string databasePath,
        string? masterDatabasePath = null)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        var context = new SOPROContext(databasePath);

        ProjectRef project;
        int? decimalesImporte = null;
        if (projectId.HasValue)
        {
            var proyecto = context.Proyectos.Find(projectId.Value);
            project = proyecto != null ? ProjectRef.FromEntity(proyecto) : new ProjectRef(projectId.Value, string.Empty);
            decimalesImporte = proyecto?.DecimalesImporte;
        }
        else
        {
            project = ProjectRef.Master;
        }

        return new ProjectSessionInfo(
            project,
            databasePath,
            masterDatabasePath ?? WorkspacePaths.MasterDatabasePath,
            decimalesImporte,
            context);
    }

    /// <summary>Libera el contexto interno de la sesión.</summary>
    public void Dispose() => Context.Dispose();
}