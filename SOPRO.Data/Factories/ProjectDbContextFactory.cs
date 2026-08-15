using SOPRO.Data.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SOPRO.Data.Factories;

/// <summary>
/// Puerto de construcción de contextos (N4): los casos de uso y servicios de
/// Application NUNCA construyen <see cref="SOPROContext"/> directamente;
/// dependen de esta abstracción y reciben la instancia por inyección
/// (transient por uso: una operación, un contexto).
///
/// El puerto vive en Data, junto al tipo que produce: ubicarlo en Application
/// exigiría que Data referencie Application (ciclo de dependencia, hoy
/// Application → Data). Application consume la abstracción; la inyección la
/// aporta quien compone (WinForms o las pruebas).
/// </summary>
public interface IProjectDbContextFactory
{
    /// <summary>Crea un contexto para la base indicada (proyecto, maestro o base externa).</summary>
    SOPROContext Create(string databasePath);

    /// <summary>Variante asíncrona de la frontera; no realiza I/O (SQLite abre la conexión de forma perezosa).</summary>
    Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementación por defecto (transient): una instancia nueva por llamada.
/// La creación no abre conexión ni consulta el esquema.
/// </summary>
public sealed class ProjectDbContextFactory : IProjectDbContextFactory
{
    public SOPROContext Create(string databasePath)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        return new SOPROContext(databasePath);
    }

    public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
        => Task.FromResult(Create(databasePath));
}