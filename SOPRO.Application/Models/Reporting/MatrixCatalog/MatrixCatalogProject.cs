namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Snapshot neutral del proyecto para resolver campos dinámicos. Datos puros,
/// sin entidades de EF. (Necesita el sufijo internal para no formar parte del
/// contrato público de Application.)
/// </summary>
internal sealed record MatrixCatalogProject(
    string Nombre,
    string Descripcion,
    string Ubicacion,
    string Convocante,
    string Contratista,
    string ApoderadoLegal,
    DateTime? FechaInicio,
    DateTime? FechaTermino,
    int? PlazoEjecucion);