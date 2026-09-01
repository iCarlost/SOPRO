using System.Collections.ObjectModel;

namespace Sopro.Calculation.Matrices;

/// <summary>Calculated amount for one immutable input component.</summary>
public sealed record MatrixComponentResult(
    int ComponentId,
    int Order,
    decimal Amount);

/// <summary>Calculated totals for one matrix node.</summary>
public sealed class MatrixNodeResult
{
    internal MatrixNodeResult(
        int id,
        MatrixType type,
        IReadOnlyList<MatrixComponentResult> components,
        decimal totalMaterial,
        decimal baseLabor,
        decimal totalLabor,
        decimal totalMachinery,
        decimal totalBasics,
        decimal totalTools,
        decimal totalLaborSummary,
        decimal directCostTotal)
    {
        Id = id;
        Type = type;
        Components = components;
        TotalMaterial = totalMaterial;
        BaseLabor = baseLabor;
        TotalLabor = totalLabor;
        TotalMachinery = totalMachinery;
        TotalBasics = totalBasics;
        TotalTools = totalTools;
        TotalLaborSummary = totalLaborSummary;
        DirectCostTotal = directCostTotal;
    }

    /// <summary>Stable node identifier.</summary>
    public int Id { get; }
    /// <summary>Node kind.</summary>
    public MatrixType Type { get; }
    /// <summary>Calculated components in stable order.</summary>
    public IReadOnlyList<MatrixComponentResult> Components { get; }
    /// <summary>Total for material components.</summary>
    public decimal TotalMaterial { get; }
    /// <summary>Base made from normal labor and crew components.</summary>
    public decimal BaseLabor { get; }
    /// <summary>Total for labor and crew components.</summary>
    public decimal TotalLabor { get; }
    /// <summary>Total for machinery components.</summary>
    public decimal TotalMachinery { get; }
    /// <summary>Total for non-crew auxiliary components.</summary>
    public decimal TotalBasics { get; }
    /// <summary>Total for tool components.</summary>
    public decimal TotalTools { get; }
    /// <summary>Labor total plus percentage-of-labor tools.</summary>
    public decimal TotalLaborSummary { get; }
    /// <summary>Direct cost total for this node.</summary>
    public decimal DirectCostTotal { get; }
}

/// <summary>
/// Immutable graph result. The scalar totals refer to <see cref="RootMatrixId"/>.
/// </summary>
public sealed class MatrixGraphResult
{
    private readonly MatrixNodeResult _root;

    internal MatrixGraphResult(
        int rootMatrixId,
        IReadOnlyDictionary<int, MatrixNodeResult> nodes,
        IReadOnlyDictionary<int, decimal> componentAmounts,
        MatrixNodeResult root)
    {
        RootMatrixId = rootMatrixId;
        Nodes = nodes;
        ComponentAmounts = componentAmounts;
        _root = root;
    }

    /// <summary>Identifier of the node whose scalar totals are exposed.</summary>
    public int RootMatrixId { get; }
    /// <summary>Calculated reachable nodes keyed by stable node identifier.</summary>
    public IReadOnlyDictionary<int, MatrixNodeResult> Nodes { get; }
    /// <summary>Calculated component amounts keyed by stable component identifier.</summary>
    public IReadOnlyDictionary<int, decimal> ComponentAmounts { get; }

    /// <summary>Root material total.</summary>
    public decimal TotalMaterial => _root.TotalMaterial;
    /// <summary>Root labor base.</summary>
    public decimal BaseLabor => _root.BaseLabor;
    /// <summary>Root labor total.</summary>
    public decimal TotalLabor => _root.TotalLabor;
    /// <summary>Root machinery total.</summary>
    public decimal TotalMachinery => _root.TotalMachinery;
    /// <summary>Root basics total.</summary>
    public decimal TotalBasics => _root.TotalBasics;
    /// <summary>Root tools total.</summary>
    public decimal TotalTools => _root.TotalTools;
    /// <summary>Root labor summary total.</summary>
    public decimal TotalLaborSummary => _root.TotalLaborSummary;
    /// <summary>Root direct cost total.</summary>
    public decimal DirectCostTotal => _root.DirectCostTotal;

    // Spanish aliases keep the result directly mappable to MatrixComponentTotals
    // while the calculation package's canonical API remains English.
    /// <summary>Alias of <see cref="BaseLabor"/> for the existing matrix model.</summary>
    public decimal BaseManoObra => BaseLabor;
    /// <summary>Alias of <see cref="TotalLabor"/> for the existing matrix model.</summary>
    public decimal TotalManoObra => TotalLabor;
    /// <summary>Alias of <see cref="TotalLaborSummary"/> for the existing matrix model.</summary>
    public decimal TotalManoObraResumen => TotalLaborSummary;
    /// <summary>Alias of <see cref="TotalBasics"/> for the existing matrix model.</summary>
    public decimal TotalBasicos => TotalBasics;
    /// <summary>Alias of <see cref="TotalTools"/> for the existing matrix model.</summary>
    public decimal TotalHerramientas => TotalTools;
    /// <summary>Alias of <see cref="DirectCostTotal"/> for the existing matrix model.</summary>
    public decimal CostoDirectoTotal => DirectCostTotal;
}
