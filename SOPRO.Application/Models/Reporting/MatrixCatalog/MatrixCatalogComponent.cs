namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Componente de una matriz (insumo dentro del análisis), con la aritmética
/// legacy ya resuelta y sin redondeo:
///   - <see cref="UnitCost"/>: costo unitario mostrado (base); para filas %MO es
///     el total de mano de obra de la matriz (N0-TABLA fila 23 / resolución del
///     generador legacy).
///   - <see cref="Amount"/>: importe crudo = Cantidad × UnitCost (o TotalMO ×
///     Cantidad para filas %MO), sin formato de celda.
/// </summary>
/// <param name="Kind">Tipo de insumo.</param>
/// <param name="Prefix">Prefijo visual legacy ("M", "H", "+" o vacío).</param>
/// <param name="Key">Clave del insumo.</param>
/// <param name="Description">Descripción del insumo.</param>
/// <param name="Unit">Unidad.</param>
/// <param name="Quantity">Cantidad.</param>
/// <param name="UnitCost">Costo unitario (o base mostrada para %MO).</param>
/// <param name="Amount">Importe crudo.</param>
public sealed record MatrixCatalogComponent(
    MatrixCatalogComponentKind Kind,
    string Prefix,
    string Key,
    string Description,
    string Unit,
    decimal Quantity,
    decimal UnitCost,
    decimal Amount);