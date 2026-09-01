using System.Collections.ObjectModel;

namespace Sopro.Calculation.Matrices;

/// <summary>
/// Pure evaluator for a materialized matrix graph. It never changes the input graph.
/// </summary>
public sealed class MatrixGraphCalculator
{
    private readonly SoproCalculationEngine _engine;

    /// <summary>Creates an evaluator from explicit screen precision.</summary>
    /// <param name="precision">Precision used by all monetary operations.</param>
    public MatrixGraphCalculator(CalculationPrecision precision)
        : this(new SoproCalculationEngine(precision))
    {
    }

    /// <summary>Creates an evaluator using an existing calculation engine.</summary>
    /// <param name="engine">Engine whose precision is used by the evaluator.</param>
    public MatrixGraphCalculator(SoproCalculationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <summary>Evaluates the root and all reachable auxiliary nodes.</summary>
    public MatrixGraphResult Calculate(MatrixGraphInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var nodes = input.Nodes.ToDictionary(node => node.Id);
        if (!nodes.ContainsKey(input.RootMatrixId))
            throw MissingNode(input.RootMatrixId, input.RootMatrixId, null, new[] { input.RootMatrixId });

        var componentIds = new HashSet<int>();
        foreach (var node in nodes.Values)
        {
            foreach (var component in node.Components)
            {
                if (!componentIds.Add(component.Id))
                    throw new InvalidOperationException($"El componente de matriz {component.Id} está duplicado.");
            }
        }

        var states = new Dictionary<int, VisitState>();
        var stack = new List<int>();
        var calculated = new Dictionary<int, MatrixNodeResult>();
        var componentAmounts = new Dictionary<int, decimal>();

        MatrixNodeResult Evaluate(int nodeId)
        {
            if (calculated.TryGetValue(nodeId, out var cached))
                return cached;

            if (!nodes.TryGetValue(nodeId, out var node))
                throw MissingNode(nodeId, stack.Count == 0 ? nodeId : stack[^1], null, stack.Append(nodeId));

            if (states.TryGetValue(nodeId, out var state) && state == VisitState.Active)
                throw Cycle(stack, nodeId);

            states[nodeId] = VisitState.Active;
            stack.Add(nodeId);

            try
            {
                var ordered = node.Components
                    .OrderBy(component => component.Order)
                    .ThenBy(component => component.Id)
                    .ToList();
                var amounts = new Dictionary<int, decimal>();

                foreach (var component in ordered)
                {
                    decimal amount;
                    if (component.Type == MatrixComponentType.Auxiliary)
                    {
                        if (!component.ReferencedMatrixId.HasValue ||
                            !nodes.ContainsKey(component.ReferencedMatrixId.Value))
                        {
                            var referenceId = component.ReferencedMatrixId;
                            throw MissingNode(
                                referenceId ?? 0,
                                node.Id,
                                component.Id,
                                stack.Append(referenceId ?? 0));
                        }

                        var child = Evaluate(component.ReferencedMatrixId.Value);
                        amount = _engine.Multiply(component.Quantity, child.DirectCostTotal);
                    }
                    else if (IsPercentageComponent(component))
                    {
                        amount = 0m;
                    }
                    else
                    {
                        amount = _engine.Multiply(component.Quantity, component.ResolvedUnitPrice);
                    }

                    amounts[component.Id] = amount;
                }

                var baseLabor = 0m;
                foreach (var component in ordered)
                {
                    if ((component.Type == MatrixComponentType.Labor && !component.IsPercentageOfLabor) ||
                        (component.Type == MatrixComponentType.Auxiliary &&
                         nodes[component.ReferencedMatrixId!.Value].Type == MatrixType.Crew))
                    {
                        baseLabor = _engine.RoundAmount(baseLabor + amounts[component.Id]);
                    }
                }

                foreach (var component in ordered)
                {
                    if (IsPercentageComponent(component))
                        amounts[component.Id] = _engine.CalculateAmountOverBase(component.Quantity, baseLabor);
                }

                var laborPercentageTools = _engine.SumAmounts(
                    ordered
                        .Where(component => component.Type == MatrixComponentType.Tool && component.IsPercentageOfLabor)
                        .Select(component => amounts[component.Id]));

                var totalMaterial = SumByType(ordered, amounts, MatrixComponentType.Material);
                var totalLabor = _engine.SumAmounts(
                    ordered
                        .Where(component => component.Type == MatrixComponentType.Labor ||
                            (component.Type == MatrixComponentType.Auxiliary &&
                             nodes[component.ReferencedMatrixId!.Value].Type == MatrixType.Crew))
                        .Select(component => amounts[component.Id]));
                var totalMachinery = SumByType(ordered, amounts, MatrixComponentType.Machinery);
                var totalBasics = _engine.SumAmounts(
                    ordered
                        .Where(component => component.Type == MatrixComponentType.Auxiliary &&
                            nodes[component.ReferencedMatrixId!.Value].Type != MatrixType.Crew)
                        .Select(component => amounts[component.Id]));
                var totalTools = SumByType(ordered, amounts, MatrixComponentType.Tool);
                var totalLaborSummary = _engine.RoundAmount(totalLabor + laborPercentageTools);
                var directCostTotal = _engine.RoundAmount(
                    totalMaterial + totalLabor + totalMachinery + totalBasics + totalTools);

                var componentResults = new ReadOnlyCollection<MatrixComponentResult>(
                    ordered.Select(component => new MatrixComponentResult(
                        component.Id,
                        component.Order,
                        amounts[component.Id])).ToList());
                var result = new MatrixNodeResult(
                    node.Id,
                    node.Type,
                    componentResults,
                    totalMaterial,
                    baseLabor,
                    totalLabor,
                    totalMachinery,
                    totalBasics,
                    totalTools,
                    totalLaborSummary,
                    directCostTotal);

                foreach (var amount in amounts)
                    componentAmounts.Add(amount.Key, amount.Value);

                calculated.Add(node.Id, result);
                states[node.Id] = VisitState.Done;
                return result;
            }
            finally
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }

        var root = Evaluate(input.RootMatrixId);
        return new MatrixGraphResult(
            input.RootMatrixId,
            new ReadOnlyDictionary<int, MatrixNodeResult>(calculated),
            new ReadOnlyDictionary<int, decimal>(componentAmounts),
            root);
    }

    private decimal SumByType(
        IReadOnlyList<MatrixComponentInput> components,
        IReadOnlyDictionary<int, decimal> amounts,
        MatrixComponentType type)
        => _engine.SumAmounts(
            components.Where(component => component.Type == type).Select(component => amounts[component.Id]));

    private static bool IsPercentageComponent(MatrixComponentInput component)
        => (component.Type == MatrixComponentType.Labor || component.Type == MatrixComponentType.Tool) &&
           component.IsPercentageOfLabor;

    private static InvalidOperationException Cycle(IReadOnlyList<int> stack, int repeatedNodeId)
    {
        var start = stack.ToList().IndexOf(repeatedNodeId);
        var route = stack.Skip(start < 0 ? 0 : start).Append(repeatedNodeId);
        return new InvalidOperationException(
            "Ciclo de matrices detectado. Ruta: " + string.Join(" -> ", route));
    }

    private static InvalidOperationException MissingNode(
        int missingNodeId,
        int ownerNodeId,
        int? componentId,
        IEnumerable<int> route)
    {
        var component = componentId.HasValue ? $", componente {componentId.Value}" : string.Empty;
        return new InvalidOperationException(
            $"Nodo de matriz faltante: {missingNodeId} (referido desde matriz {ownerNodeId}{component}). " +
            "Ruta: " + string.Join(" -> ", route));
    }

    private enum VisitState
    {
        Active,
        Done
    }
}
