using System.Collections.Generic;
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
    public void MaquinariaYHerramientaNormal_UsanPrecioResuelto()
    {
        var result = Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Machinery, 1.5m, 45.555m),
                Component(11, 2, MatrixComponentType.Tool, 2m, 7.777m)));

        Assert.AreEqual(68.34m, result.ComponentAmounts[10]);
        Assert.AreEqual(68.34m, result.TotalMachinery);
        Assert.AreEqual(15.56m, result.ComponentAmounts[11]);
        Assert.AreEqual(15.56m, result.TotalTools);
        Assert.AreEqual(83.90m, result.DirectCostTotal);
    }

    [TestMethod]
    public void GrafoMixtoCompleto_SumaTodosLosRubros()
    {
        var result = Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Material, 3m, 11.115m),
                Component(11, 2, MatrixComponentType.Labor, 2m, 25.5m),
                Component(12, 3, MatrixComponentType.Labor, .10m, isPercentage: true),
                Component(13, 4, MatrixComponentType.Machinery, 1.5m, 45.555m),
                Component(14, 5, MatrixComponentType.Tool, 2m, 7.777m),
                Component(15, 6, MatrixComponentType.Tool, .03m, isPercentage: true),
                Component(16, 7, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 2)),
            Node(2, MatrixType.Crew,
                Component(20, 1, MatrixComponentType.Labor, 1m, 40.005m)));

        Assert.AreEqual(33.36m, result.TotalMaterial);
        Assert.AreEqual(91.01m, result.BaseLabor);
        Assert.AreEqual(100.11m, result.TotalLabor);
        Assert.AreEqual(68.34m, result.TotalMachinery);
        Assert.AreEqual(0m, result.TotalBasics);
        Assert.AreEqual(18.29m, result.TotalTools);
        Assert.AreEqual(102.84m, result.TotalLaborSummary);
        Assert.AreEqual(220.10m, result.DirectCostTotal);
    }

    [TestMethod]
    public void TipoComponenteNoDefinido_SeRechaza()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() => Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, (MatrixComponentType)99, 1m, 10m))));

        StringAssert.Contains(exception.Message, "Tipo de componente no definido: 99");
        StringAssert.Contains(exception.Message, "matriz 1, componente 10");
    }

    [TestMethod]
    public void TipoMatrizNoDefinido_SeRechazaInclusoSinReferencias()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() => Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Material, 1m, 10m)),
            Node(2, (MatrixType)99,
                Component(20, 1, MatrixComponentType.Material, 1m, 10m))));

        StringAssert.Contains(exception.Message, "Tipo de matriz no definido: 99");
        StringAssert.Contains(exception.Message, "matriz 2");
    }

    [TestMethod]
    public void AuxiliarMarcadoComoPorcentaje_SeRechaza()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() => Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Auxiliary, 1m, isPercentage: true, referencedMatrixId: 2)),
            Node(2, MatrixType.Basic,
                Component(20, 1, MatrixComponentType.Material, 1m, 10m))));

        StringAssert.Contains(exception.Message, "Combinación inválida");
        StringAssert.Contains(exception.Message, "matriz 1, componente 10");
    }

    [TestMethod]
    public void PorcentajeEnMaterialOMaquinaria_SeRechaza()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() => Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Material, 1m, 10m, isPercentage: true))));

        StringAssert.Contains(exception.Message, "Combinación inválida");
        StringAssert.Contains(exception.Message, "tipo Material");
    }

    [TestMethod]
    public void ReferenciaAuxiliarNula_SeDistingueDeNodoFaltante()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() => Calculate(
            Node(1, MatrixType.Apu,
                Component(10, 1, MatrixComponentType.Auxiliary, 1m))));

        StringAssert.Contains(exception.Message, "Referencia auxiliar nula");
        StringAssert.Contains(exception.Message, "matriz 1, componente 10");
        StringAssert.DoesNotMatch(exception.Message, new System.Text.RegularExpressions.Regex("Nodo de matriz faltante"));
    }

    [TestMethod]
    public void Evaluacion_NoMutaLosInputs_YCopiaDefensivamente()
    {
        var componentList = new List<MatrixComponentInput>
        {
            Component(10, 2, MatrixComponentType.Material, 2m, 13.3875m)
        };
        var nodeList = new List<MatrixNodeInput> { new(1, MatrixType.Apu, componentList) };
        var input = new MatrixGraphInput(1, nodeList);

        var result = new MatrixGraphCalculator(new CalculationPrecision(2, 2, 4)).Calculate(input);

        componentList.Add(Component(11, 3, MatrixComponentType.Material, 1m, 5m));
        nodeList.Add(Node(2, MatrixType.Basic, Component(20, 1, MatrixComponentType.Material, 1m, 5m)));

        Assert.IsInstanceOfType(input.Nodes[0].Components, typeof(System.Collections.ObjectModel.ReadOnlyCollection<MatrixComponentInput>));
        Assert.AreEqual(1, input.Nodes.Count);
        Assert.AreEqual(1, input.Nodes[0].Components.Count);
        Assert.AreEqual(2m, input.Nodes[0].Components[0].Quantity);
        Assert.AreEqual(2, input.Nodes[0].Components[0].Order);
        Assert.AreEqual(1, result.Nodes.Count);
        Assert.AreEqual(26.78m, result.ComponentAmounts[10]);
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
