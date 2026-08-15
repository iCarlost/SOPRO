using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Materials;

/// <summary>
/// Fila del catálogo de materiales, neutral y sin rastreo: la UI recibe DTOs,
/// nunca entidades de EF (PLAN-01 §12).
/// </summary>
public sealed record MaterialListItem(
    int Id,
    string Clave,
    string Descripcion,
    string Unidad,
    decimal PrecioUnitario,
    string Notas,
    OrigenInsumo Origen);