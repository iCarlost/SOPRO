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
            throw new InvalidOperationException(
                $"La matriz raíz {input.RootMatrixId} no existe en el grafo materializado.");

        var componentIds = new HashSet<int>();
        foreach (var node in nodes.Values)
        {
            ValidateNode(node, nodes);
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
                throw new InvalidOperationException(
                    $"Nodo de matriz {nodeId} no existe en el grafo materializado.");

            if (node.PrecomputedDirectCostTotal.HasValue)
            {
                var leaf = new MatrixNodeResult(
                    node.Id,
                    node.Type,
                    Array.Empty<MatrixComponentResult>(),
                    0m,
                    0m,
                    0m,
                    0m,
                    0m,
                    0m,
                    0m,
                    node.PrecomputedDirectCostTotal.Value);
                calculated.Add(node.Id, leaf);
                return leaf;
            }

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
                        var child = Evaluate(component.ReferencedMatrixId!.Value);
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

    private static void ValidateNode(MatrixNodeInput node, IReadOnlyDictionary<int, MatrixNodeInput> nodes)
    {
        if (!Enum.IsDefined(node.Type))
            throw new InvalidOperationException(
                $"Tipo de matriz no definido: {(int)node.Type} (matriz {node.Id}).");

        if (node.PrecomputedDirectCostTotal.HasValue)
        {
            if (node.Components.Count > 0)
                throw new InvalidOperationException(
                    $"Combinación inválida: la matriz {node.Id} no puede tener componentes y un total precalculado a la vez.");

            if (node.Type == MatrixType.Apu)
                throw new InvalidOperationException(
                    $"Combinación inválida: una matriz APU (nodo {node.Id}) no puede materializarse como hoja precalculada.");
            return;
        }

        foreach (var component in node.Components)
            ValidateComponent(node, component, nodes);
    }

    private static void ValidateComponent(
        MatrixNodeInput node,
        MatrixComponentInput component,
        IReadOnlyDictionary<int, MatrixNodeInput> nodes)
    {
        if (!Enum.IsDefined(component.Type))
            throw new InvalidOperationException(
                $"Tipo de componente no definido: {(int)component.Type} (matriz {node.Id}, componente {component.Id}).");

        if (component.Type == MatrixComponentType.Auxiliary)
        {
            if (component.IsPercentageOfLabor)
                throw new InvalidOperationException(
                    "Combinación inválida: un componente auxiliar no puede marcarse como porcentaje de mano de obra " +
                    $"(matriz {node.Id}, componente {component.Id}).");

            if (!component.ReferencedMatrixId.HasValue)
                throw new InvalidOperationException(
                    $"Referencia auxiliar nula en matriz {node.Id}, componente {component.Id}.");

            if (!nodes.ContainsKey(component.ReferencedMatrixId.Value))
                throw new InvalidOperationException(
                    $"Nodo de matriz faltante: {component.ReferencedMatrixId.Value} " +
                    $"(referido desde matriz {node.Id}, componente {component.Id}).");
            return;
        }

        if (component.IsPercentageOfLabor &&
            component.Type is not (MatrixComponentType.Labor or MatrixComponentType.Tool))
            throw new InvalidOperationException(
                "Combinación inválida: solo mano de obra y herramienta pueden ser porcentaje de mano de obra " +
                $"(matriz {node.Id}, componente {component.Id}, tipo {component.Type}).");
    }

    private static InvalidOperationException Cycle(IReadOnlyList<int> stack, int repeatedNodeId)
    {
        var start = stack.ToList().IndexOf(repeatedNodeId);
        var route = stack.Skip(start < 0 ? 0 : start).Append(repeatedNodeId);
        return new InvalidOperationException(
            "Ciclo de matrices detectado. Ruta: " + string.Join(" -> ", route));
    }

    private enum VisitState
    {
        Active,
        Done
    }
}
