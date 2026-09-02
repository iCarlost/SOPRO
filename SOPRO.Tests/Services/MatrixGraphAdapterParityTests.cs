using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;
using Sopro.Calculation.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services;

/// <summary>
/// Diferencial N7-1b: el servicio delegado al evaluador puro del grafo debe producir
/// los mismos totales y componentes que el oráculo legacy literal.
/// </summary>
[TestClass]
public class MatrixGraphAdapterParityTests
{
    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void RecalculateYGraphCalculator_ProducenMismosTotalesYComponentes(int decimals)
    {
        var material = Component(TipoComponenteMatriz.Material, 3m, material: new Material { PrecioUnitario = 11.115m });
        var manoObra = Component(TipoComponenteMatriz.ManoDeObra, 2m, manoDeObra: new ManoDeObra { Unidad = "jor", SalarioReal = 25.5m });
        var manoObraPct = Component(TipoComponenteMatriz.ManoDeObra, .10m, manoDeObra: new ManoDeObra { Unidad = "%MO", SalarioReal = 0m });
        var maquinaria = Component(TipoComponenteMatriz.Maquinaria, 1.5m, maquinaria: new Maquinaria { CostoHorario = 45.555m });
        var herramienta = Component(TipoComponenteMatriz.Herramienta, 2m, herramienta: new Herramienta { Unidad = "pza", PrecioUnitario = 7.777m });
        var herramientaPct = Component(TipoComponenteMatriz.Herramienta, .03m, herramienta: new Herramienta { Unidad = "%MO", PrecioUnitario = 0m });
        var cuadrilla = Component(TipoComponenteMatriz.Auxiliar, 1m, auxiliar: new Matriz { Tipo = TipoMatriz.Cuadrilla, CostoDirecto = 40.005m });
        var basico = Component(TipoComponenteMatriz.Auxiliar, 2m, auxiliar: new Matriz { Tipo = TipoMatriz.Basico, CostoDirecto = 17.777m });
        var componentes = new List<ComponenteMatriz> { material, manoObra, manoObraPct, maquinaria, herramienta, herramientaPct, cuadrilla, basico };

        var legacy = MatrixComponentCalculationLegacyOracle.Recalculate(componentes, decimals);

        var graph = new MatrixGraphCalculator(new CalculationPrecision(decimals, decimals, 4))
            .Calculate(new MatrixGraphInput(1, new[]
            {
                new MatrixNodeInput(1, MatrixType.Apu, new[]
                {
                    Component(101, 1, MatrixComponentType.Material, 3m, 11.115m),
                    Component(102, 2, MatrixComponentType.Labor, 2m, 25.5m),
                    Component(103, 3, MatrixComponentType.Labor, .10m, isPercentage: true),
                    Component(104, 4, MatrixComponentType.Machinery, 1.5m, 45.555m),
                    Component(105, 5, MatrixComponentType.Tool, 2m, 7.777m),
                    Component(106, 6, MatrixComponentType.Tool, .03m, isPercentage: true),
                    Component(107, 7, MatrixComponentType.Auxiliary, 1m, referencedMatrixId: 20),
                    Component(108, 8, MatrixComponentType.Auxiliary, 2m, referencedMatrixId: 30)
                }),
                new MatrixNodeInput(20, MatrixType.Crew, new[]
                {
                    Component(200, 1, MatrixComponentType.Labor, 1m, 40.005m)
                }),
                new MatrixNodeInput(30, MatrixType.Basic, new[]
                {
                    Component(300, 1, MatrixComponentType.Material, 1m, 17.777m)
                })
            }));

        var expectedByComponent = new[]
        {
            (101, material), (102, manoObra), (103, manoObraPct), (104, maquinaria),
            (105, herramienta), (106, herramientaPct), (107, cuadrilla), (108, basico)
        };
        foreach (var (id, componente) in expectedByComponent)
            Assert.AreEqual(componente.Importe, graph.ComponentAmounts[id], $"Importe componente {id} ({decimals} decimales)");

        Assert.AreEqual(legacy.TotalMaterial, graph.TotalMaterial, $"TotalMaterial ({decimals} decimales)");
        Assert.AreEqual(legacy.BaseManoObra, graph.BaseLabor, $"BaseManoObra ({decimals} decimales)");
        Assert.AreEqual(legacy.TotalManoObra, graph.TotalLabor, $"TotalManoObra ({decimals} decimales)");
        Assert.AreEqual(legacy.TotalMaquinaria, graph.TotalMachinery, $"TotalMaquinaria ({decimals} decimales)");
        Assert.AreEqual(legacy.TotalBasicos, graph.TotalBasics, $"TotalBasicos ({decimals} decimales)");
        Assert.AreEqual(legacy.TotalHerramientas, graph.TotalTools, $"TotalHerramientas ({decimals} decimales)");
        Assert.AreEqual(legacy.TotalManoObraResumen, graph.TotalLaborSummary, $"TotalManoObraResumen ({decimals} decimales)");
        Assert.AreEqual(legacy.CostoDirectoTotal, graph.DirectCostTotal, $"CostoDirectoTotal ({decimals} decimales)");
        Assert.IsTrue(legacy.TotalBasicos > 0m, "El escenario debe ejercer TotalBasicos con valor no nulo.");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void ServicioDelegadoYOraculo_ProducenMismosResultados(int decimals)
    {
        var referencia = new List<ComponenteMatriz>
        {
            Component(TipoComponenteMatriz.Material, 3m, material: new Material { PrecioUnitario = 11.115m }),
            Component(TipoComponenteMatriz.ManoDeObra, 2m, manoDeObra: new ManoDeObra { Unidad = "jor", SalarioReal = 25.5m }),
            Component(TipoComponenteMatriz.ManoDeObra, .10m, manoDeObra: new ManoDeObra { Unidad = "%MO", SalarioReal = 0m }),
            Component(TipoComponenteMatriz.Maquinaria, 1.5m, maquinaria: new Maquinaria { CostoHorario = 45.555m }),
            Component(TipoComponenteMatriz.Herramienta, 2m, herramienta: new Herramienta { Unidad = "pza", PrecioUnitario = 7.777m }),
            Component(TipoComponenteMatriz.Herramienta, .03m, herramienta: new Herramienta { Unidad = "%MO", PrecioUnitario = 0m }),
            Component(TipoComponenteMatriz.Auxiliar, 1m, auxiliar: new Matriz { Tipo = TipoMatriz.Cuadrilla, CostoDirecto = 40.005m }),
            Component(TipoComponenteMatriz.Auxiliar, 2m, auxiliar: new Matriz { Tipo = TipoMatriz.APU, CostoDirecto = 17.777m })
        };
        var servicio = new List<ComponenteMatriz>
        {
            Component(TipoComponenteMatriz.Material, 3m, material: new Material { PrecioUnitario = 11.115m }),
            Component(TipoComponenteMatriz.ManoDeObra, 2m, manoDeObra: new ManoDeObra { Unidad = "jor", SalarioReal = 25.5m }),
            Component(TipoComponenteMatriz.ManoDeObra, .10m, manoDeObra: new ManoDeObra { Unidad = "%MO", SalarioReal = 0m }),
            Component(TipoComponenteMatriz.Maquinaria, 1.5m, maquinaria: new Maquinaria { CostoHorario = 45.555m }),
            Component(TipoComponenteMatriz.Herramienta, 2m, herramienta: new Herramienta { Unidad = "pza", PrecioUnitario = 7.777m }),
            Component(TipoComponenteMatriz.Herramienta, .03m, herramienta: new Herramienta { Unidad = "%MO", PrecioUnitario = 0m }),
            Component(TipoComponenteMatriz.Auxiliar, 1m, auxiliar: new Matriz { Tipo = TipoMatriz.Cuadrilla, CostoDirecto = 40.005m }),
            Component(TipoComponenteMatriz.Auxiliar, 2m, auxiliar: new Matriz { Tipo = TipoMatriz.APU, CostoDirecto = 17.777m })
        };

        var legacy = MatrixComponentCalculationLegacyOracle.Recalculate(referencia, decimals);
        var nuevo = MatrixComponentCalculationService.Recalculate(servicio, decimals);

        Assert.AreEqual(legacy.TotalMaterial, nuevo.TotalMaterial, $"TotalMaterial ({decimals})");
        Assert.AreEqual(legacy.BaseManoObra, nuevo.BaseManoObra, $"BaseManoObra ({decimals})");
        Assert.AreEqual(legacy.TotalManoObra, nuevo.TotalManoObra, $"TotalManoObra ({decimals})");
        Assert.AreEqual(legacy.TotalMaquinaria, nuevo.TotalMaquinaria, $"TotalMaquinaria ({decimals})");
        Assert.AreEqual(legacy.TotalBasicos, nuevo.TotalBasicos, $"TotalBasicos ({decimals})");
        Assert.AreEqual(legacy.TotalHerramientas, nuevo.TotalHerramientas, $"TotalHerramientas ({decimals})");
        Assert.AreEqual(legacy.TotalManoObraResumen, nuevo.TotalManoObraResumen, $"TotalManoObraResumen ({decimals})");
        Assert.AreEqual(legacy.CostoDirectoTotal, nuevo.CostoDirectoTotal, $"CostoDirectoTotal ({decimals})");

        for (var i = 0; i < referencia.Count; i++)
            Assert.AreEqual(referencia[i].Importe, servicio[i].Importe, $"Importe posición {i} ({decimals})");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(3)]
    public void CostosAuxiliaresNegativos_MantienenParidadLegacy(int decimals)
    {
        var referencia = new List<ComponenteMatriz>
        {
            Component(TipoComponenteMatriz.Material, 2m, material: new Material { PrecioUnitario = 10m }),
            Component(TipoComponenteMatriz.ManoDeObra, 1m, manoDeObra: new ManoDeObra { Unidad = "jor", SalarioReal = 50m }),
            Component(TipoComponenteMatriz.Auxiliar, 1.5m, auxiliar: new Matriz { Tipo = TipoMatriz.Basico, CostoDirecto = -15.555m }),
            Component(TipoComponenteMatriz.Auxiliar, 1m, auxiliar: new Matriz { Tipo = TipoMatriz.Cuadrilla, CostoDirecto = -4.444m })
        };
        var servicio = new List<ComponenteMatriz>
        {
            Component(TipoComponenteMatriz.Material, 2m, material: new Material { PrecioUnitario = 10m }),
            Component(TipoComponenteMatriz.ManoDeObra, 1m, manoDeObra: new ManoDeObra { Unidad = "jor", SalarioReal = 50m }),
            Component(TipoComponenteMatriz.Auxiliar, 1.5m, auxiliar: new Matriz { Tipo = TipoMatriz.Basico, CostoDirecto = -15.555m }),
            Component(TipoComponenteMatriz.Auxiliar, 1m, auxiliar: new Matriz { Tipo = TipoMatriz.Cuadrilla, CostoDirecto = -4.444m })
        };

        var legacy = MatrixComponentCalculationLegacyOracle.Recalculate(referencia, decimals);
        var nuevo = MatrixComponentCalculationService.Recalculate(servicio, decimals);

        Assert.AreEqual(legacy.TotalMaterial, nuevo.TotalMaterial, $"TotalMaterial ({decimals})");
        Assert.AreEqual(legacy.BaseManoObra, nuevo.BaseManoObra, $"BaseManoObra ({decimals})");
        Assert.AreEqual(legacy.TotalManoObra, nuevo.TotalManoObra, $"TotalManoObra ({decimals})");
        Assert.AreEqual(legacy.TotalBasicos, nuevo.TotalBasicos, $"TotalBasicos ({decimals})");
        Assert.AreEqual(legacy.CostoDirectoTotal, nuevo.CostoDirectoTotal, $"CostoDirectoTotal ({decimals})");
        Assert.IsTrue(legacy.TotalBasicos < 0m, "El escenario debe ejercer TotalBasicos negativo.");

        for (var i = 0; i < referencia.Count; i++)
            Assert.AreEqual(referencia[i].Importe, servicio[i].Importe, $"Importe posición {i} ({decimals})");
    }

    private static ComponenteMatriz Component(
        TipoComponenteMatriz tipo,
        decimal cantidad,
        Material? material = null,
        ManoDeObra? manoDeObra = null,
        Maquinaria? maquinaria = null,
        Herramienta? herramienta = null,
        Matriz? auxiliar = null)
    {
        var componente = new ComponenteMatriz { TipoComponente = tipo, Cantidad = cantidad };
        if (material != null) componente.Material = material;
        if (manoDeObra != null) componente.ManoDeObra = manoDeObra;
        if (maquinaria != null) componente.Maquinaria = maquinaria;
        if (herramienta != null) componente.Herramienta = herramienta;
        if (auxiliar != null) componente.Auxiliar = auxiliar;
        return componente;
    }

    private static MatrixComponentInput Component(
        int id, int order, MatrixComponentType type, decimal quantity,
        decimal price = 0m, bool isPercentage = false, int? referencedMatrixId = null)
        => new(id, order, type, quantity, price, isPercentage, referencedMatrixId);
}
