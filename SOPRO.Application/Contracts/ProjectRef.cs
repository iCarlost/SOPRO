using System;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Contracts;

/// <summary>
/// Identidad neutral de un proyecto (o del catálogo maestro) para la frontera
/// de Application. No expone entidades de dominio ni EF. Incluye los metadatos
/// que los reportes usan en el encabezado (sin consultar la base desde la UI).
/// </summary>
/// <param name="ProjectId">Id del proyecto; <c>null</c> = catálogo maestro.</param>
/// <param name="Name">Nombre para presentación.</param>
public sealed record ProjectRef(
    int? ProjectId,
    string Name,
    string Descripcion = "",
    string Ubicacion = "",
    string Convocante = "",
    string Contratista = "",
    string ApoderadoLegal = "",
    DateTime FechaInicio = default,
    DateTime FechaTermino = default,
    int PlazoEjecucion = 0)
{
    public static ProjectRef Master { get; } = new(null, "Catálogo Maestro");

    public bool IsMasterCatalog => ProjectId == null;

    public static ProjectRef FromEntity(Proyecto proyecto)
        => new(
            proyecto.Id,
            proyecto.Nombre ?? string.Empty,
            proyecto.Descripcion ?? string.Empty,
            proyecto.Ubicacion ?? string.Empty,
            proyecto.Convocante ?? string.Empty,
            proyecto.Contratista ?? string.Empty,
            proyecto.ApoderadoLegal ?? string.Empty,
            proyecto.FechaInicio,
            proyecto.FechaTermino,
            proyecto.PlazoEjecucion);
}
