using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Resolución de valores de exportación (PDF/Excel) de un material desde su
/// DTO de presentación <see cref="MaterialListItem"/>. Vive en Application para
/// poder probarse sin formularios: el generador de PDF de la UI solo renderiza.
/// </summary>
public static class MaterialCatalogExportResolver
{
    public static string ResolveValue(MaterialListItem material, ColumnaMaterial column)
    {
        return column.NombreInterno switch
        {
            "Clave" => material.Clave ?? string.Empty,
            "Descripcion" => material.Descripcion ?? string.Empty,
            "Unidad" => material.Unidad ?? string.Empty,
            "PrecioUnitario" => material.PrecioUnitario.ToString("#,##0.0000"),
            "Origen" => ResolveOrigin(material),
            _ => string.Empty
        };
    }

    public static string ResolveOrigin(MaterialListItem material)
        => material.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";
}