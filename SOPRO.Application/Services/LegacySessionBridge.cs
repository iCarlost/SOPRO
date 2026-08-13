using SOPRO.Application.Contracts;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services;

/// <summary>
/// Puente N3→N4 desde los componentes legacy (UI, servicios): construye la
/// información de sesión a partir de una ruta de base de datos y el Id de
/// proyecto. No expone el contexto: la sesión lo resuelve de forma interna.
/// </summary>
public static class LegacySessionBridge
{
    public static ProjectSessionInfo FromLegacy(SOPROContext context, int? projectId)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ProjectSessionInfo.Create(projectId, context.DatabasePath);
    }
}