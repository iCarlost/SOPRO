using System.Collections.ObjectModel;

namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Matriz del catálogo con valores ya resueltos (inmutable):
///   - <see cref="DirectCost"/>: costo directo persistido de la matriz.
///   - <see cref="TotalMo"/>: total de mano de obra crudo (calculado como el
///     generador legacy, sin redondeo); es la base mostrada en filas %MO.
/// Los componentes se materializan en una copia defensiva ordenada por Orden.
/// </summary>
public sealed class MatrixCatalogMatrix
{
    private readonly ReadOnlyCollection<MatrixCatalogComponent> _components;

    /// <summary>Clave de la matriz.</summary>
    public string Key { get; }

    /// <summary>Descripción de la matriz.</summary>
    public string Description { get; }

    /// <summary>Unidad de la matriz.</summary>
    public string Unit { get; }

    /// <summary>Clasificación de la matriz (APU, básico o cuadrilla).</summary>
    public MatrixCatalogMatrixKind Kind { get; }

    /// <summary>Costo directo persistido de la matriz.</summary>
    public decimal DirectCost { get; }

    /// <summary>Total de mano de obra crudo de la matriz.</summary>
    public decimal TotalMo { get; }

    /// <summary>Componentes ordenados por Orden.</summary>
    public IReadOnlyList<MatrixCatalogComponent> Components => _components;

    public MatrixCatalogMatrix(
        string key,
        string description,
        string unit,
        MatrixCatalogMatrixKind kind,
        decimal directCost,
        decimal totalMo,
        IEnumerable<MatrixCatalogComponent> components)
    {
        Key = key ?? string.Empty;
        Description = description ?? string.Empty;
        Unit = unit ?? string.Empty;
        Kind = kind;
        DirectCost = directCost;
        TotalMo = totalMo;
        _components = new ReadOnlyCollection<MatrixCatalogComponent>(
            (components ?? throw new ArgumentNullException(nameof(components))).ToList());
    }
}