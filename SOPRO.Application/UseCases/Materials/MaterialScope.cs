using SOPRO.Application.Contracts;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Regla de alcance de la sesión: un material solo es alcanzable por el caso de
/// uso si pertenece al proyecto de la sesión (o al catálogo maestro cuando la
/// sesión es el maestro). Aísla proyectos entre sí y del catálogo maestro.
/// </summary>
internal static class MaterialScope
{
    public static bool IsInSessionScope(ProjectSessionInfo session, Material? material)
    {
        if (material == null) return false;

        return material.ProyectoId == session.Project.ProjectId;
    }
}