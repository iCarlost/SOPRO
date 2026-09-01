using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;
using Sopro.Calculation.Matrices;

namespace SOPRO.Tests.Calculation;

[TestClass]
public class MatrixGraphCalculatorTests
{
    [TestMethod]
    public void ApuSimple_CalculaImportePorComponenteYTotal()
    {
        var result = Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Material, 652m, 13.3875m)));

        Assert.AreEqual(8730.28m, result.ComponentAmounts[10]);
        Assert.AreEqual(8730.28m, result.TotalMaterial);
        Assert.AreEqual(8730.28m, result.DirectCostTotal);
    }

    [TestMethod]
    public void ManoDeObraNormalYPorcentaje_UsanLaBaseMO()
    {
        var result = Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Labor, 2m, 500m),
                Component(11, 2, MatrixComponentType.Labor, .10m, isPercentage: true)));

        Assert.AreEqual(1000m, result.ComponentAmounts[10]);
        Assert.AreEqual(100m, result.ComponentAmounts[11]);
        Assert.AreEqual(1000m, result.BaseLabor);
        Assert.AreEqual(1100m, result.TotalLabor);
        Assert.AreEqual(1100m, result.DirectCostTotal);
    }

    [TestMethod]
    public void HerramientaPorcentaje_SeIncluyeEnResumenMOYEnCostoDirecto()
    {
        var result = Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Labor, 2m, 500m),
                Component(11, 2, MatrixComponentType.Tool, .03m, isPercentage: true)));

        Assert.AreEqual(30m, result.ComponentAmounts[11]);
        Assert.AreEqual(1000m, result.TotalLabor);
        Assert.AreEqual(30m, result.TotalTools);
        Assert.AreEqual(1030m, result.TotalLaborSummary);
        Assert.AreEqual(1030m, result.DirectCostTotal);
    }

    [TestMethod]
    public void Cuadrilla_AportaALaBaseYAlTotalDeManoDeObra()
    {
        var result = Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 2),
                Component(11, 2, MatrixComponentType.Labor, .10m, isPercentage: true)),
            Node(2, MatrixType.Crew,
                Component(20, 1, MatrixComponentType.Labor, 1m, 800m)));

        Assert.AreEqual(800m, result.ComponentAmounts[10]);
        Assert.AreEqual(80m, result.ComponentAmounts[11]);
        Assert.AreEqual(800m, result.BaseLabor);
        Assert.AreEqual(880m, result.TotalLabor);
        Assert.AreEqual(880m, result.DirectCostTotal);
    }

    [TestMethod]
    public void BasicoAnidado_SePropagaComoRubroBasicos()
    {
        var result = Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Auxiliary, 2m, referencedMatrixId: 2)),
            Node(2, MatrixType.Basic,
                Component(20, 1, MatrixComponentType.Material, 1m, 125.555m)));

        Assert.AreEqual(251.12m, result.ComponentAmounts[10]);
        Assert.AreEqual(251.12m, result.TotalBasics);
        Assert.AreEqual(251.12m, result.DirectCostTotal);
        Assert.AreEqual(125.56m, result.Nodes[2].DirectCostTotal);
    }

    [TestMethod]
    public void DagCompartido_EvaluaUnaReferenciaCompartidaSinDuplicarElNodo()
    {
        var result = Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 2),
                Component(11, 2, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 2)),
            Node(2, MatrixType.Basic,
                Component(20, 1, MatrixComponentType.Material, 1m, 10m)));

        Assert.AreEqual(20m, result.DirectCostTotal);
        Assert.AreEqual(2, result.Nodes.Count);
        Assert.AreEqual(10m, result.ComponentAmounts[20]);
    }

    [TestMethod]
    public void CicloDirecto_DevuelveRutaDiagnostica()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() => Calculate(
            Node(1, MatrixType.Basic,
                Component(10, 1, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 1))));

        StringAssert.Contains(exception.Message, "Ciclo");
        StringAssert.Contains(exception.Message, "1 -> 1");
    }

    [TestMethod]
    public void CicloIndirecto_DevuelveRutaCompleta()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() => Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 2)),
            Node(2, MatrixType.Basic,
                Component(20, 1, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 3)),
            Node(3, MatrixType.Basic,
                Component(30, 1, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 1))));

        StringAssert.Contains(exception.Message, "1 -> 2 -> 3 -> 1");
    }

    [TestMethod]
    public void NodoFaltante_IdentificaReferenciaComponenteYRuta()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() => Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 99))));

        StringAssert.Contains(exception.Message, "99");
        StringAssert.Contains(exception.Message, "componente 10");
        StringAssert.Contains(exception.Message, "1 -> 99");
    }

    [DataTestMethod]
    [DataRow(0, "13")]
    [DataRow(2, "13.39")]
    [DataRow(3, "13.388")]
    [DataRow(4, "13.3875")]
    public void Precision_UsaRedondeoDePrecioVisible(int decimals, string expectedText)
    {
        var expected = decimal.Parse(expectedText, System.Globalization.CultureInfo.InvariantCulture);
        var result = CalculateWithPrecision(decimals,
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Material, 1m, 13.3875m)));

        Assert.AreEqual(expected, result.DirectCostTotal);
        Assert.AreEqual(expected, result.ComponentAmounts[10]);
    }

    [TestMethod]
    public void Evaluacion_NoMutaLosInputs()
    {
        var component = Component(10, 2, MatrixComponentType.Material, 2m, 13.3875m);
        var input = new MatrixGraphInput(1, new[]
        {
            Node(1, MatrixType.Apu, component)
        });

        new MatrixGraphCalculator(new CalculationPrecision(2, 2, 4)).Calculate(input);

        Assert.AreEqual(component, input.Nodes[0].Components[0]);
        Assert.AreEqual(2, input.Nodes[0].Components[0].Quantity);
        Assert.AreEqual(2, input.Nodes[0].Components[0].Order);
    }

    private static MatrixGraphResult Calculate(params MatrixNodeInput[] nodes)
        => CalculateWithPrecision(2, nodes);

    private static MatrixGraphResult CalculateWithPrecision(
        int amountDecimals,
        params MatrixNodeInput[] nodes)
        => new MatrixGraphCalculator(new CalculationPrecision(2, amountDecimals, 4))
            .Calculate(new MatrixGraphInput(1, nodes));

    private static MatrixNodeInput Node(
        int id,
        MatrixType type,
        params MatrixComponentInput[] components)
        => new(id, type, components);

    private static MatrixComponentInput Component(
        int id,
        int order,
        MatrixComponentType type,
        decimal quantity,
        decimal price = 0m,
        bool isPercentage = false,
        int? referencedMatrixId = null)
        => new(id, order, type, quantity, price, isPercentage, referencedMatrixId);
}
