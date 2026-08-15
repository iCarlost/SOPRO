using SOPRO.Application.Contracts;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services;

/// <summary>
/// Puente N4 desde los componentes legacy (UI, servicios): construye la
/// información de sesión a partir de un contexto ya abierto y el Id de
/// proyecto. No retiene el contexto: la sesión es datos puros y la operación
/// que la consuma abre su propio contexto (IProjectDbContextFactory).
/// </summary>
public static class LegacySessionBridge
{
    public static ProjectSessionInfo FromLegacy(SOPROContext context, int? projectId)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (projectId.HasValue)
        {
            var proyecto = context.Proyectos.Find(projectId.Value);
            if (proyecto != null)
            {
                return ProjectSessionInfo.Create(
                    ProjectRef.FromEntity(proyecto),
                    context.DatabasePath,
                    decimalesImporte: proyecto.DecimalesImporte);
            }

            return ProjectSessionInfo.Create(
                new ProjectRef(projectId.Value, string.Empty),
                context.DatabasePath);
        }

        return ProjectSessionInfo.Create(ProjectRef.Master, context.DatabasePath);
    }
}