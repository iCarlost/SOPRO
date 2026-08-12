using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using Sopro.Calculation;

namespace SOPRO.Tests.Calculation;

/// <summary>
/// Oracle legacy independiente: valores EXACTOS congelados, calculados una sola
/// vez contra el motor legacy actual (N1). Cada caso afirma TANTO el motor legacy
/// como el motor nuevo contra la misma constante.
///
/// Cuando la fachada N2 delegue al paquete, esta prueba deja de ser tautologica:
/// los goldens no dependen de ninguna implementacion y detectan cualquier cambio
/// de resultados en cualquiera de los tres componentes (legacy, paquete, fachada).
/// </summary>
[TestClass]
public class LegacyOracleGoldenTests
{
    private static MotorCalculoSopro Legacy(int c, int i, int p)
        => new MotorCalculoSopro(c, i, p);

    private static SoproCalculationEngine Nuevo(int c, int i, int p)
        => new SoproCalculationEngine(c, i, p);

    [TestMethod]
    public void Golden_Multiply_ConPrecisionPorDefecto()
    {
        decimal esperado = 8730.28m; // 652 × 13.39 (P.U. visible redondeado)

        Assert.AreEqual(esperado, Legacy(2, 2, 4).Multiplicar(652m, 13.3875m));
        Assert.AreEqual(esperado, Nuevo(2, 2, 4).Multiply(652m, 13.3875m));
    }

    [TestMethod]
    public void Golden_Multiply_CasoDiscriminante60_005()
    {
        Assert.AreEqual(600.10m, Legacy(2, 2, 4).Multiplicar(10m, 60.005m));
        Assert.AreEqual(600.10m, Nuevo(2, 2, 4).Multiply(10m, 60.005m));

        Assert.AreEqual(600.05m, Legacy(3, 3, 3).Multiplicar(10m, 60.005m));
        Assert.AreEqual(600.05m, Nuevo(3, 3, 3).Multiply(10m, 60.005m));
    }

    [TestMethod]
    public void Golden_CalculateUnitPrice_Acumulables()
    {
        var entrada = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 5m,
            Financiamiento = 6m,
            Utilidad = 8m,
            CargosAdicionales = 3m,
            ModoCalculoPorcentajes = "Acumulables",
        };

        var pct = new PricePercentageInput
        {
            CentralIndirectsPercentage = 5m,
            FieldIndirectsPercentage = 5m,
            FinancingPercentage = 6m,
            ProfitPercentage = 8m,
            AdditionalChargesPercentage = 3m,
            Mode = PercentageCalculationMode.Accumulative,
        };

        var legacy = Legacy(2, 2, 4).CalcularPrecioUnitario(1000m, entrada);
        var nuevo = Nuevo(2, 2, 4).CalculateUnitPrice(1000m, pct);

        // cd=1000; ind=100; fin=66; util=93.28; cargos=37.78; pu=1297.06
        Assert.AreEqual(1000m, legacy.CostoDirecto);
        Assert.AreEqual(1000m, nuevo.DirectCost);
        Assert.AreEqual(100m, legacy.Indirectos);
        Assert.AreEqual(100m, nuevo.IndirectCosts);
        Assert.AreEqual(66m, legacy.Financiamiento);
        Assert.AreEqual(66m, nuevo.Financing);
        Assert.AreEqual(93.28m, legacy.Utilidad);
        Assert.AreEqual(93.28m, nuevo.Profit);
        Assert.AreEqual(37.78m, legacy.CargosAdicionales);
        Assert.AreEqual(37.78m, nuevo.AdditionalCharges);
        Assert.AreEqual(1297.06m, legacy.PrecioUnitario);
        Assert.AreEqual(1297.06m, nuevo.UnitPrice);
        Assert.AreEqual(50m, legacy.IndirectosCentral);
        Assert.AreEqual(50m, nuevo.CentralIndirectCosts);
        Assert.AreEqual(50m, legacy.IndirectosCampo);
        Assert.AreEqual(50m, nuevo.FieldIndirectCosts);
    }

    [TestMethod]
    public void Golden_CalculateUnitPrice_SobreCD()
    {
        var entrada = new BudgetPercentageInput
        {
            IndirectosCentral = 5m,
            IndirectosCampo = 5m,
            Financiamiento = 6m,
            Utilidad = 8m,
            CargosAdicionales = 3m,
            ModoCalculoPorcentajes = "SobreCD",
        };

        var pct = new PricePercentageInput
        {
            CentralIndirectsPercentage = 5m,
            FieldIndirectsPercentage = 5m,
            FinancingPercentage = 6m,
            ProfitPercentage = 8m,
            AdditionalChargesPercentage = 3m,
            Mode = PercentageCalculationMode.OverDirectCost,
        };

        var legacy = Legacy(2, 2, 4).CalcularPrecioUnitario(1000m, entrada);
        var nuevo = Nuevo(2, 2, 4).CalculateUnitPrice(1000m, pct);

        // todos sobre CD: 1000 + 100 + 60 + 80 + 30 = 1270
        Assert.AreEqual(100m, legacy.Indirectos);
        Assert.AreEqual(100m, nuevo.IndirectCosts);
        Assert.AreEqual(60m, legacy.Financiamiento);
        Assert.AreEqual(60m, nuevo.Financing);
        Assert.AreEqual(80m, legacy.Utilidad);
        Assert.AreEqual(80m, nuevo.Profit);
        Assert.AreEqual(30m, legacy.CargosAdicionales);
        Assert.AreEqual(30m, nuevo.AdditionalCharges);
        Assert.AreEqual(1270m, legacy.PrecioUnitario);
        Assert.AreEqual(1270m, nuevo.UnitPrice);
    }

    [TestMethod]
    public void Golden_DistributeAmount_ConResiduoEnUltimoPeriodo()
    {
        decimal[] esperado = { 33.33m, 33.33m, 33.35m }; // suma 100.01 = Round(100.005, 2)

        CollectionAssert.AreEqual(esperado, Legacy(2, 2, 4).DistribuirImporte(100.005m, new[] { 1m, 1m, 1m }).ToArray());
        CollectionAssert.AreEqual(esperado, Nuevo(2, 2, 4).DistributeAmount(100.005m, new[] { 1m, 1m, 1m }).ToArray());
    }

    [TestMethod]
    public void Golden_DistributeAmount_ConResiduoNegativo()
    {
        decimal[] esperado = { 0.01m, 0.01m, 0.01m, -0.01m }; // ultimo periodo negativo (fila 5)

        CollectionAssert.AreEqual(esperado, Legacy(2, 2, 4).DistribuirImporte(0.02m, new[] { 3m, 3m, 3m, 3m }).ToArray());
        CollectionAssert.AreEqual(esperado, Nuevo(2, 2, 4).DistributeAmount(0.02m, new[] { 3m, 3m, 3m, 3m }).ToArray());
    }

    [TestMethod]
    public void Golden_DistributeQuantity_ConTresDecimales()
    {
        decimal[] esperado = { 3.333m, 3.333m, 3.334m };

        CollectionAssert.AreEqual(esperado, Legacy(3, 3, 3).DistribuirCantidad(10m, new[] { 1m, 1m, 1m }).ToArray());
        CollectionAssert.AreEqual(esperado, Nuevo(3, 3, 3).DistributeQuantity(10m, new[] { 1m, 1m, 1m }).ToArray());
    }

    [TestMethod]
    public void Golden_SumAmounts_RedondeaCadaElemento()
    {
        decimal esperado = 6.03m; // 1.01 + 2.01 + 3.01

        Assert.AreEqual(esperado, Legacy(2, 2, 4).SumarImportes(new[] { 1.005m, 2.005m, 3.005m }));
        Assert.AreEqual(esperado, Nuevo(2, 2, 4).SumAmounts(new[] { 1.005m, 2.005m, 3.005m }));
    }

    [TestMethod]
    public void Golden_SumDirectCost_ConLineasMixtas()
    {
        // 10 × 60.01 = 600.10 + 2 × 100.25 = 200.50 + 3 × 2.50 = 7.50 → 808.10;
        // agrupador y sin matriz omitidos; MatrizId = 0 participa (HasValue)
        decimal esperado = 808.10m;

        var concepto = new SOPRO.Core.Entities.ConceptoPresupuesto { Cantidad = 10m, CostoDirectoUnitario = 60.005m, MatrizId = 1 };
        Assert.AreEqual(esperado, Legacy(2, 2, 4).SumarCostoDirecto(new[]
        {
            concepto,
            new SOPRO.Core.Entities.ConceptoPresupuesto { Cantidad = 2m, CostoDirectoUnitario = 100.25m, MatrizId = 2 },
            new SOPRO.Core.Entities.ConceptoPresupuesto { Cantidad = 3m, CostoDirectoUnitario = 2.5m, MatrizId = 0 },
            new SOPRO.Core.Entities.ConceptoPresupuesto { Cantidad = 999m, CostoDirectoUnitario = 1m, EsAgrupador = true },
            new SOPRO.Core.Entities.ConceptoPresupuesto { Cantidad = 999m, CostoDirectoUnitario = 1m },
        }));

        Assert.AreEqual(esperado, Nuevo(2, 2, 4).SumDirectCost(new[]
        {
            new DirectCostLine(10m, 60.005m, IsGrouping: false, HasMatrix: true),
            new DirectCostLine(2m, 100.25m, IsGrouping: false, HasMatrix: true),
            new DirectCostLine(3m, 2.5m, IsGrouping: false, HasMatrix: true),
            new DirectCostLine(999m, 1m, IsGrouping: true, HasMatrix: true),
            new DirectCostLine(999m, 1m, IsGrouping: false, HasMatrix: false),
        }));
    }

    [TestMethod]
    public void Golden_SumasNulasYDistribucionesVacias()
    {
        Assert.AreEqual(0m, Legacy(2, 2, 4).SumarImportes(null));
        Assert.AreEqual(0m, Nuevo(2, 2, 4).SumAmounts(null));
        Assert.AreEqual(0m, Legacy(2, 2, 4).SumarCostoDirecto(null));
        Assert.AreEqual(0m, Nuevo(2, 2, 4).SumDirectCost(null));

        CollectionAssert.AreEqual(new decimal[0], Legacy(2, 2, 4).DistribuirImporte(100m, null).ToArray());
        CollectionAssert.AreEqual(new decimal[0], Nuevo(2, 2, 4).DistributeAmount(100m, null).ToArray());
    }
}