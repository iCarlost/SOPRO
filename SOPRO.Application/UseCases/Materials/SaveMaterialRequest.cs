using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Guardado (creación o actualización) de un material. <see cref="MaterialId"/>
/// nulo crea; con valor actualiza. <see cref="SaveToMaster"/> decide si persiste
/// en el catálogo maestro o en el proyecto.
///
/// Al guardar "en maestro" desde un proyecto, la identidad de la fila maestra NO
/// se deduce del <see cref="MaterialId"/> local (los Ids no se comparten entre
/// bases): se resuelve dentro del caso de uso a partir del material de origen
/// (su <see cref="Material.MaterialMaestroId"/>, si existe). Sin fila maestra
/// asociada se crea una fila nueva; nunca se sobrescribe un registro maestro
/// ajeno por colisión numérica de Ids.
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
