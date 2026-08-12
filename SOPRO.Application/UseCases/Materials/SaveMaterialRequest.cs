namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Guardado (creación o actualización) de un material. <see cref="MaterialId"/>
/// nulo crea; con valor actualiza. <see cref="SaveToMaster"/> decide si persiste
/// en el catálogo maestro o en el proyecto.
/// </summary>
public sealed record SaveMaterialRequest(
    int? MaterialId,
    string Clave,
    string Descripcion,
    string Unidad,
    decimal PrecioUnitario,
    string? Notas = null,
    bool SaveToMaster = false,
    int? ProjectId = null);