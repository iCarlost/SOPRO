using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Financing;

namespace SOPRO.Tests.Calculation;

/// <summary>
/// Tests puros de <see cref="FinancingPreparationCalculator"/> (N7-17d): la
/// preparación numérica CD/CI que el legado hacía inline en el adaptador.
///
/// Semántica replicada del legado 1:1:
/// - Precio unitario visible redondeado primero y resultado después
///   (precisión de importe del proyecto, AwayFromZero).
/// - El residuo (esperado - distribuido) se absorbe en el último período cuya
///   cantidad programada o importe calculado es distinto de cero.
/// - La estimación cobrable se acumula desde ImporteProgramado redondeando tras
///   cada suma y NUNCA recibe el residuo de CD.
/// - Reconciliación de CI: reparto proporcional sobre las filas con monto o
///   sobre la base de CD cuando el actual es cero (asignando el total oficial a
///   la última fila cuando la base también es cero).
/// </summary>
[TestClass]
public sealed class FinancingPreparationCalculatorTests
{
    [TestMethod]
    public void ResiduoPositivoSeAbsorbeEnUltimoPeriodoConMonto()
    {
        // Cantidad 2 x 100 = 200; distribuidos 0.7 + 0.7 = 140; residuo 60 → [70, 130]
        var result = Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                Distribution(0, 0.7m), Distribution(1, 0.7m))
        });

        Assert.AreEqual(2, result.Count);
        AssertPrepared(result[0], directCost: 70.00m, indirectCost: 0m, expenditure: 70.00m, estimated: 0m);
        AssertPrepared(result[1], directCost: 130.00m, indirectCost: 0m, expenditure: 130.00m, estimated: 0m);
    }

    [TestMethod]
    public void ResiduoNegativoSeAbsorbeEnUltimoPeriodoConMonto()
    {
        // Cantidad 2 x 100 = 200; distribuidos 1.5 + 1.5 = 300; residuo -100 → [150, 50]
        var result = Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                Distribution(0, 1.5m), Distribution(1, 1.5m))
        });

        Assert.AreEqual(2, result.Count);
        AssertPrepared(result[0], directCost: 150.00m, indirectCost: 0m, expenditure: 150.00m, estimated: 0m);
        AssertPrepared(result[1], directCost: 50.00m, indirectCost: 0m, expenditure: 50.00m, estimated: 0m);
    }

    [TestMethod]
    public void CerosFinalesResiduoSeAbsorbeEnUltimoPeriodoConMonto()
    {
        // Cantidad 2 x 100 = 200; distribuidos 0.5+0.5 = 100; residuo 100 se queda
        // en el último índice CON monto (índice 1), no en las filas de cero.
        var result = Prepare(amountDecimals: 2, periodCount: 4, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                Distribution(0, 0.5m), Distribution(1, 0.5m), Distribution(2, 0m), Distribution(3, 0m))
        });

        Assert.AreEqual(4, result.Count);
        AssertPrepared(result[0], directCost: 50.00m, indirectCost: 0m, expenditure: 50.00m, estimated: 0m);
        AssertPrepared(result[1], directCost: 150.00m, indirectCost: 0m, expenditure: 150.00m, estimated: 0m);
        AssertPrepared(result[2], directCost: 0m, indirectCost: 0m, expenditure: 0m, estimated: 0m);
        AssertPrepared(result[3], directCost: 0m, indirectCost: 0m, expenditure: 0m, estimated: 0m);
    }

    [TestMethod]
    public void ConceptoSinTotalEsperadoSeOmite()
    {
        // cdUnit 0 y total 0 → total esperado 0 → no acumula nada.
        var result = Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 0m, totalCd: 0m,
                Distribution(0, 1m), Distribution(1, 1m))
        });

        AssertPrepared(result[0], directCost: 0m, indirectCost: 0m, expenditure: 0m, estimated: 0m);
        AssertPrepared(result[1], directCost: 0m, indirectCost: 0m, expenditure: 0m, estimated: 0m);
    }

    [TestMethod]
    public void PrecioUnitarioFalloUsaTotalEntreCantidad()
    {
        // cdUnit 0 y total 300 con cantidad 2 → cdUnit = 150.
        var result = Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 0m, totalCd: 300m,
                Distribution(0, 1m), Distribution(1, 1m))
        });

        AssertPrepared(result[0], directCost: 150.00m, indirectCost: 0m, expenditure: 150.00m, estimated: 0m);
        AssertPrepared(result[1], directCost: 150.00m, indirectCost: 0m, expenditure: 150.00m, estimated: 0m);
    }

    [TestMethod]
    public void PrecisionVariableSeRespetaEnRedondeos()
    {
        AssertPrepared(Prepare(amountDecimals: 0, periodCount: 1, concepts: new[]
        {
            Concept(quantity: 1m, unitCd: 1.2345m, totalCd: 0m, Distribution(0, 1m))
        })[0], directCost: 1m, indirectCost: 0m, expenditure: 1m, estimated: 0m);

        AssertPrepared(Prepare(amountDecimals: 1, periodCount: 1, concepts: new[]
        {
            Concept(quantity: 1m, unitCd: 1.2345m, totalCd: 0m, Distribution(0, 1m))
        })[0], directCost: 1.2m, indirectCost: 0m, expenditure: 1.2m, estimated: 0m);

        AssertPrepared(Prepare(amountDecimals: 3, periodCount: 1, concepts: new[]
        {
            Concept(quantity: 1m, unitCd: 1.2345m, totalCd: 0m, Distribution(0, 1m))
        })[0], directCost: 1.235m, indirectCost: 0m, expenditure: 1.235m, estimated: 0m);
    }

    [TestMethod]
    public void EstimacionSeAcumulaRedondeandoPorIncremento()
    {
        // 10.555 → 10.56; 10.56 + 10.555 = 21.115 → 21.12 (no 21.11 de la suma global).
        var result = Prepare(amountDecimals: 2, periodCount: 1, estimates: new[]
        {
            Estimate(0, 10.555m),
            Estimate(0, 10.555m)
        });

        AssertPrepared(result[0], directCost: 0m, indirectCost: 0m, expenditure: 0m, estimated: 21.12m);
    }

    [TestMethod]
    public void ResiduoNoContaminaLaEstimacion()
    {
        // CD con residuo [70, 130], pero la estimación proviene solo de
        // ImporteProgramado [200, 300].
        var result = Prepare(
            amountDecimals: 2,
            periodCount: 2,
            concepts: new[]
            {
                Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                    Distribution(0, 0.7m), Distribution(1, 0.7m))
            },
            estimates: new[]
            {
                Estimate(0, 200m),
                Estimate(1, 300m)
            });

        AssertPrepared(result[0], directCost: 70.00m, indirectCost: 0m, expenditure: 70.00m, estimated: 200.00m);
        AssertPrepared(result[1], directCost: 130.00m, indirectCost: 0m, expenditure: 130.00m, estimated: 300.00m);
    }

    [TestMethod]
    public void TotalActualCeroConBaseCDReconciliaCIProporcional()
    {
        // CD [100, 100]; CI oficial 30 → 30*100/200 = 15 por fila; egreso 115.
        var result = Reconcile(
            Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
            {
                Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                    Distribution(0, 1m), Distribution(1, 1m))
            }),
            officialIndirectCost: 30m,
            amountDecimals: 2);

        AssertPrepared(result[0], directCost: 100.00m, indirectCost: 15.00m, expenditure: 115.00m, estimated: 0m);
        AssertPrepared(result[1], directCost: 100.00m, indirectCost: 15.00m, expenditure: 115.00m, estimated: 0m);
    }

    [TestMethod]
    public void TotalActualCeroSinBaseCDAsignaCITotalALaUltimaFila()
    {
        // CD 0 en todas las filas → la última fila absorbe el total oficial de CI.
        var result = Reconcile(
            Prepare(amountDecimals: 2, periodCount: 3),
            officialIndirectCost: 25m,
            amountDecimals: 2);

        AssertPrepared(result[0], directCost: 0m, indirectCost: 0m, expenditure: 0m, estimated: 0m);
        AssertPrepared(result[1], directCost: 0m, indirectCost: 0m, expenditure: 0m, estimated: 0m);
        AssertPrepared(result[2], directCost: 0m, indirectCost: 25.00m, expenditure: 25.00m, estimated: 0m);
    }

    [TestMethod]
    public void ReconciliacionCIProporcionalAbsorbeResiduoDeRedondeoEnLaUltimaFila()
    {
        // 33.37 / 3: las dos primeras redondean a 11.12 y la última absorbe el
        // sobrante → [11.12, 11.12, 11.13].
        var result = Reconcile(
            Prepare(amountDecimals: 2, periodCount: 3, concepts: new[]
            {
                Concept(quantity: 3m, unitCd: 100m, totalCd: 0m,
                    Distribution(0, 1m), Distribution(1, 1m), Distribution(2, 1m))
            }),
            officialIndirectCost: 33.37m,
            amountDecimals: 2);

        AssertPrepared(result[0], directCost: 100.00m, indirectCost: 11.12m, expenditure: 111.12m, estimated: 0m);
        AssertPrepared(result[1], directCost: 100.00m, indirectCost: 11.12m, expenditure: 111.12m, estimated: 0m);
        AssertPrepared(result[2], directCost: 100.00m, indirectCost: 11.13m, expenditure: 111.13m, estimated: 0m);
    }

    [TestMethod]
    public void TotalActualIgualAlOficialNoModificaNada()
    {
        var result = Reconcile(
            Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
            {
                Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                    Distribution(0, 1m), Distribution(1, 1m))
            }),
            officialIndirectCost: 0m,
            amountDecimals: 2);

        AssertPrepared(result[0], directCost: 100.00m, indirectCost: 0m, expenditure: 100.00m, estimated: 0m);
        AssertPrepared(result[1], directCost: 100.00m, indirectCost: 0m, expenditure: 100.00m, estimated: 0m);
    }

    [TestMethod]
    public void ReconcileNoModificaLasFilasDeEntrada()
    {
        var baseSchedule = Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                Distribution(0, 1m), Distribution(1, 1m))
        });

        Reconcile(baseSchedule, officialIndirectCost: 30m, amountDecimals: 2);

        AssertPrepared(baseSchedule[0], directCost: 100.00m, indirectCost: 0m, expenditure: 100.00m, estimated: 0m);
        AssertPrepared(baseSchedule[1], directCost: 100.00m, indirectCost: 0m, expenditure: 100.00m, estimated: 0m);
    }

    [TestMethod]
    public void TodasCantidadesCero_ResiduoVaALaUltimaDistribucion()
    {
        // Cantidad 2 x 100 = 200; distribuciones 0 + 0; sin fila con monto → el
        // fallback del legado manda el total a la última distribución → [0, 200].
        var result = Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                Distribution(0, 0m), Distribution(1, 0m))
        });

        AssertPrepared(result[0], directCost: 0m, indirectCost: 0m, expenditure: 0m, estimated: 0m);
        AssertPrepared(result[1], directCost: 200.00m, indirectCost: 0m, expenditure: 200.00m, estimated: 0m);
    }

    [TestMethod]
    public void PeriodIndexFueraDeRango_EnDistribucion_LanzaArgumentOutOfRange()
    {
        Action calcular = () => Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                Distribution(0, 1m), Distribution(2, 1m))
        });

        Assert.ThrowsException<ArgumentOutOfRangeException>(calcular);
    }

    [TestMethod]
    public void PeriodIndexFueraDeRango_EnEstimacion_LanzaArgumentOutOfRange()
    {
        Action calcular = () => Prepare(amountDecimals: 2, periodCount: 2, estimates: new[]
        {
            Estimate(1, 10m),
            Estimate(3, 20m)
        });

        Assert.ThrowsException<ArgumentOutOfRangeException>(calcular);
    }

    [TestMethod]
    public void PeriodIndexFueraDeRango_EnConceptoOmitidoPorCantidadCero_Lanza()
    {
        // La pasada inicial valida ANTES de la guarda de cantidad, así un concepto
        // omitido no puede esconder un índice inválido (contrato fail-fast).
        Action calcular = () => Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 0m, unitCd: 100m, totalCd: 0m,
                Distribution(0, 1m), Distribution(5, 1m))
        });

        Assert.ThrowsException<ArgumentOutOfRangeException>(calcular);
    }

    [TestMethod]
    public void PeriodIndexFueraDeRango_EnConceptoOmitidoPorTotalEsperadoCero_Lanza()
    {
        // Mismo contrato para un concepto omitido porque su total esperado es cero
        // (cdUnit y total ambos cero).
        Action calcular = () => Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 0m, totalCd: 0m,
                Distribution(-1, 1m))
        });

        Assert.ThrowsException<ArgumentOutOfRangeException>(calcular);
    }

    [TestMethod]
    public void PrepareBaseScheduleConNull_LanzaArgumentNull()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => FinancingPreparationCalculator.PrepareBaseSchedule(null!));
    }

    [TestMethod]
    public void ReconcileIndirectCostConNull_LanzaArgumentNull()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => FinancingPreparationCalculator.ReconcileIndirectCost(null!, officialIndirectCost: 1m, amountDecimals: 2));
    }

    [TestMethod]
    public void ReconcileConCIPrevioDistintoDeCero_DistribuyeProporcional()
    {
        // CI previo [10, 0, 10] suman 20 ≠ 30 oficial → reparto sobre las filas con
        // monto (índices 0 y 2), cada una recibe 30*10/20 = 15 → [15, 0, 15].
        var rows = new[]
        {
            new FinancingPreparedPeriod(100m, 10m, 110m, 0m),
            new FinancingPreparedPeriod(100m, 0m, 100m, 0m),
            new FinancingPreparedPeriod(100m, 10m, 110m, 0m)
        };

        var result = Reconcile(rows, officialIndirectCost: 30m, amountDecimals: 2);

        AssertPrepared(result[0], directCost: 100.00m, indirectCost: 15.00m, expenditure: 115.00m, estimated: 0m);
        AssertPrepared(result[1], directCost: 100.00m, indirectCost: 0m, expenditure: 100.00m, estimated: 0m);
        AssertPrepared(result[2], directCost: 100.00m, indirectCost: 15.00m, expenditure: 115.00m, estimated: 0m);
    }

    [TestMethod]
    public void LasEntradasNoSonMutablesPorCast()
    {
        var input = new FinancingPreparationInput(2, 2,
            new[] { Concept(quantity: 2m, unitCd: 100m, totalCd: 0m, Distribution(0, 1m), Distribution(1, 1m)) },
            new[] { Estimate(0, 10m) });

        InteropAssertNoSePuedeMutar(input.Concepts, () => new FinancingConceptInput(0m, 0m, 0m, null));
        InteropAssertNoSePuedeMutar(input.Estimates, () => new FinancingEstimateLine(0, 0m));
        InteropAssertNoSePuedeMutar(input.Concepts[0].Distributions, () => new FinancingConceptDistribution(0, 0m));
    }

    [TestMethod]
    public void LosResultadosNoSonMutablesPorCast()
    {
        var preparado = Prepare(amountDecimals: 2, periodCount: 2, concepts: new[]
        {
            Concept(quantity: 2m, unitCd: 100m, totalCd: 0m,
                Distribution(0, 1m), Distribution(1, 1m))
        });

        var reconciliado = Reconcile(preparado, officialIndirectCost: 20m, amountDecimals: 2);

        InteropAssertNoSePuedeMutar(preparado, () => new FinancingPreparedPeriod(0m, 0m, 0m, 0m));
        InteropAssertNoSePuedeMutar(reconciliado, () => new FinancingPreparedPeriod(0m, 0m, 0m, 0m));
    }

    [TestMethod]
    public void ReconcileConFilasVaciasDevuelveListaVacia()
    {
        Assert.AreEqual(0, FinancingPreparationCalculator.ReconcileIndirectCost(
            new FinancingPreparedPeriod[0], officialIndirectCost: 1m, amountDecimals: 2).Count);
    }

    private static FinancingConceptInput Concept(
        decimal quantity,
        decimal unitCd,
        decimal totalCd,
        params FinancingConceptDistribution[] distributions)
        => new(quantity, unitCd, totalCd, distributions);

    private static FinancingConceptDistribution Distribution(int periodIndex, decimal programmedQuantity)
        => new(periodIndex, programmedQuantity);

    private static FinancingEstimateLine Estimate(int periodIndex, decimal programmedImport)
        => new(periodIndex, programmedImport);

    private static System.Collections.Generic.IReadOnlyList<FinancingPreparedPeriod> Prepare(
        int amountDecimals,
        int periodCount,
        System.Collections.Generic.IEnumerable<FinancingConceptInput>? concepts = null,
        System.Collections.Generic.IEnumerable<FinancingEstimateLine>? estimates = null)
    {
        var input = new FinancingPreparationInput(amountDecimals, periodCount, concepts, estimates);
        return FinancingPreparationCalculator.PrepareBaseSchedule(input);
    }

    private static System.Collections.Generic.IReadOnlyList<FinancingPreparedPeriod> Reconcile(
        System.Collections.Generic.IReadOnlyList<FinancingPreparedPeriod> rows,
        decimal officialIndirectCost,
        int amountDecimals)
        => FinancingPreparationCalculator.ReconcileIndirectCost(rows, officialIndirectCost, amountDecimals);

    private static void InteropAssertNoSePuedeMutar<T>(System.Collections.Generic.IReadOnlyList<T> readOnly, System.Func<T> factory)
    {
        // Una lista mutable backante sería reconvertible a List<T>; ReadOnlyCollection no lo permite.
        Assert.ThrowsException<System.InvalidCastException>(() => (System.Collections.Generic.List<T>)readOnly);
        Assert.ThrowsException<System.NotSupportedException>(
            () => ((System.Collections.Generic.ICollection<T>)readOnly).Add(factory()));
    }

    private static void AssertPrepared(
        FinancingPreparedPeriod period,
        decimal directCost,
        decimal indirectCost,
        decimal expenditure,
        decimal estimated)
    {
        Assert.AreEqual(directCost, period.DirectCost, "CostoDirecto");
        Assert.AreEqual(indirectCost, period.IndirectCost, "CostoIndirecto");
        Assert.AreEqual(expenditure, period.Expenditure, "EgresoTotal");
        Assert.AreEqual(estimated, period.EstimatedAmount, "EstimacionTotal");
    }
}