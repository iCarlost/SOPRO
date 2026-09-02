using System.Collections.ObjectModel;

namespace Sopro.Calculation.Matrices;

/// <summary>Matrix kinds used to classify auxiliary components.</summary>
public enum MatrixType
{
    /// <summary>Analysis of unit price.</summary>
    Apu,
    /// <summary>Intermediate basic matrix.</summary>
    Basic,
    /// <summary>Labor crew matrix.</summary>
    Crew
}

/// <summary>One immutable node in a materialized matrix graph.</summary>
public sealed class MatrixNodeInput
{
    /// <summary>Stable node identifier.</summary>
    public int Id { get; }
    /// <summary>Node kind.</summary>
    public MatrixType Type { get; }
    /// <summary>Immutable component list.</summary>
    public IReadOnlyList<MatrixComponentInput> Components { get; }

    /// <summary>Creates a node and defensively copies its components.</summary>
    /// <param name="id">Stable node identifier.</param>
    /// <param name="type">Node kind.</param>
    /// <param name="components">Components belonging to the node.</param>
    public MatrixNodeInput(
        int id,
        MatrixType type,
        IEnumerable<MatrixComponentInput> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        Id = id;
        Type = type;
        Components = new ReadOnlyCollection<MatrixComponentInput>(components.ToList());
    }
}

/// <summary>
/// Immutable materialized graph and the node whose totals are requested.
/// </summary>
/// <remarks>
/// <para>
/// Invariant: component identifiers (<see cref="MatrixComponentInput.Id"/>) must be unique
/// across the whole graph — including nodes not reachable from <see cref="RootMatrixId"/> —
/// because <see cref="MatrixGraphResult.ComponentAmounts"/> is keyed by component id. The
/// value 0 is a valid identifier as long as it stays unique.
/// </para>
/// <para>
/// The Application adapter must assign temporary stable identifiers to unsaved components
/// before materializing the graph (for example negative ids mapped back after persistence).
/// Every node in <see cref="Nodes"/> is validated on evaluation, even when it is not
/// reachable from the root: undefined enum values, invalid combinations, null auxiliary
/// references and auxiliary references to nodes missing from the graph all fail before
/// any amount is produced.
/// </para>
/// </remarks>
public sealed class MatrixGraphInput
{
    /// <summary>Identifier of the node to evaluate.</summary>
    public int RootMatrixId { get; }
    /// <summary>Immutable materialized nodes.</summary>
    public IReadOnlyList<MatrixNodeInput> Nodes { get; }

    /// <summary>Creates a graph and defensively copies its nodes.</summary>
    /// <param name="rootMatrixId">Identifier of the requested root.</param>
    /// <param name="nodes">Materialized graph nodes.</param>
    public MatrixGraphInput(int rootMatrixId, IEnumerable<MatrixNodeInput> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        RootMatrixId = rootMatrixId;
        Nodes = new ReadOnlyCollection<MatrixNodeInput>(nodes.ToList());
    }
}
