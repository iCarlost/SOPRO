using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Financing;

namespace SOPRO.Tests.Calculation;

/// <summary>
/// Tests puros de <see cref="FinancingCalculator"/> contra los ocho escenarios
/// de caracterización del legado (N7-17a). Cada esperado fue calculado a mano
/// en la caracterización y corresponde 1:1 con el flujo legacy, de modo que
/// este calculador puede usarse como oráculo para el adaptador (N7-17c) y las
/// pruebas de paridad posteriores.
/// Tasa anual prorrateada: Round(Tasa x Dias / 365, 8). Amortización:
/// Round(Cobrada x % / 100, 6) con caps por pendiente y cobrada. Interés:
/// Round(Saldo x Tasa, 4). Porcentaje: Round(Neto / Base x 100, 5). Montos:
/// precisión de 2 decimales (AwayFromZero).
/// </summary>
[TestClass]
public sealed class FinancingCalculatorTests
{
    [TestMethod]
    public void Advance30_Delay1_Accumulative_MatchesLegacyGolden()
    {
        var result = Calculate(new Scenario(effectiveRate: 12m, tiieRate: 12m, advance: 30m, delay: 1));

        Assert.AreEqual(3, result.Rows.Count, "2 periodos base + 1 fila de desfase.");
        AssertFinancingResult(result, negativeInterest: 3.6822m, positiveInterest: 0m, net: 3.6822m, percentage: 0.16737m);

        AssertRow(result.Rows[0], 1, "P1", "2026-01-01", "2026-01-07", 7, 1100.00m, 600.00m, 0.00m, 0.00m, -500.00m, -500.00m, 1.1507m);
        AssertRow(result.Rows[1], 2, "P2", "2026-01-08", "2026-01-14", 7, 1100.00m, 0.00m, 1000.00m, 300.00m, -400.00m, -900.00m, 2.0712m);
        AssertRow(result.Rows[2], 3, "Período de desfase 1", "2026-01-15", "2026-01-21", 7, 0.00m, 0.00m, 1000.00m, 300.00m, 700.00m, -200.00m, 0.4603m);
    }

    [TestMethod]
    public void Advance5_NoDelay_AmortizationCapByPendingExhausts()
    {
        var result = Calculate(new Scenario(effectiveRate: 12m, tiieRate: 12m, advance: 5m, delay: 0));

        Assert.AreEqual(2, result.Rows.Count, "Sin desfase: solo los 2 periodos base.");
        AssertFinancingResult(result, negativeInterest: 0.5754m, positiveInterest: 0m, net: 0.5754m, percentage: 0.02615m);

        AssertRow(result.Rows[0], 1, "P1", "2026-01-01", "2026-01-07", 7, 1100.00m, 100.00m, 1000.00m, 50.00m, -50.00m, -50.00m, 0.1151m);
        AssertRow(result.Rows[1], 2, "P2", "2026-01-08", "2026-01-14", 7, 1100.00m, 0.00m, 1000.00m, 50.00m, -150.00m, -200.00m, 0.4603m);
    }

    [TestMethod]
    public void OverDirectCost_AppliesPercentageOverDirectCostOnly()
    {
        var result = Calculate(new Scenario(
            effectiveRate: 12m,
            tiieRate: 12m,
            advance: 30m,
            delay: 1,
            mode: FinancingBaseCalculationMode.OverDirectCost));

        AssertFinancingResult(result, negativeInterest: 3.6822m, positiveInterest: 0m, net: 3.6822m, percentage: 0.18411m);
        Assert.AreEqual(3, result.Rows.Count, "El flujo de caja no cambia; solo cambia la base del porcentaje.");
    }

    [TestMethod]
    public void DualModel_MixedBalances_DistinguishCostAndIncomeRates()
    {
        var result = Calculate(new Scenario(
            effectiveRate: 15m,
            tiieRate: 12m,
            advance: 80m,
            delay: 1,
            dual: true));

        AssertFinancingResult(result, negativeInterest: 1.7260m, positiveInterest: 1.1507m, net: -0.5753m, percentage: -0.02615m);

        AssertRow(result.Rows[0], 1, "P1", "2026-01-01", "2026-01-07", 7, 1100.00m, 1600.00m, 0.00m, 0.00m, 500.00m, 500.00m, 1.1507m);
        AssertRow(result.Rows[1], 2, "P2", "2026-01-08", "2026-01-14", 7, 1100.00m, 0.00m, 1000.00m, 800.00m, -900.00m, -400.00m, -1.1507m);
        AssertRow(result.Rows[2], 3, "Período de desfase 1", "2026-01-15", "2026-01-21", 7, 0.00m, 0.00m, 1000.00m, 800.00m, 200.00m, -200.00m, -0.5753m);
    }

    [TestMethod]
    public void DistributionResidue_OnlyReshapesDirectCost_NotTheCollectedEstimate()
    {
        var result = Calculate(new Scenario(
            effectiveRate: 12m,
            tiieRate: 12m,
            advance: 0m,
            delay: 0,
            periods: Periods(
                (1050m, 105m, 1050m),
                (950m, 95m, 1000m))));

        AssertFinancingResult(result, negativeInterest: 0.5868m, positiveInterest: 0m, net: 0.5868m, percentage: 0.02667m);

        // Crítico: la estimación cobrada usa el ImporteProgramado (1050/1000),
        // mientras el CD de la fila 2 absorbió el residuo -50 (950).
        AssertRow(result.Rows[0], 1, "P1", "2026-01-01", "2026-01-07", 7, 1155.00m, 0.00m, 1050.00m, 0.00m, -105.00m, -105.00m, 0.2416m);
        AssertRow(result.Rows[1], 2, "P2", "2026-01-08", "2026-01-14", 7, 1045.00m, 0.00m, 1000.00m, 0.00m, -45.00m, -150.00m, 0.3452m);
    }

    [TestMethod]
    public void AdvanceBase_FallsBackToAccumulatedBase_WhenTotalBudgetIsZero()
    {
        var result = Calculate(new Scenario(
            effectiveRate: 12m,
            tiieRate: 12m,
            advance: 30m,
            delay: 0,
            totalBudget: 0m,
            periods: Periods((1000m, 0m, 1000m))));

        Assert.AreEqual(300.00m, result.Rows[0].AdvanceReceived, "Base del anticipo = CD 1000 (sin presupuesto total).");
        Assert.AreEqual(1000.00m, result.Rows[0].CollectedEstimate, "Cobra su propia estimación con desfase 0.");
        Assert.AreEqual(0.00m, result.Rows[0].NetFlow, "Adv 300 + cobrada 1000 - amortización 300 - egreso 1000.");
        Assert.AreEqual(0.00m, result.Rows[0].AccumulatedBalance);
    }

    [TestMethod]
    public void ZeroBase_ReturnsEmptyResultWithoutRows()
    {
        var result = Calculate(new Scenario(
            effectiveRate: 12m,
            tiieRate: 12m,
            advance: 30m,
            delay: 1,
            periods: Periods((0m, 0m, 0m), (0m, 0m, 0m))));

        Assert.AreEqual(0, result.Rows.Count);
        Assert.AreEqual(0m, result.NegativeInterest);
        Assert.AreEqual(0m, result.PositiveInterest);
        Assert.AreEqual(0m, result.NetFinancing);
        Assert.AreEqual(0m, result.Percentage);
    }

    [TestMethod]
    public void NoPeriods_ReturnsEmptyResult()
    {
        var result = FinancingCalculator.Calculate(new FinancingInput(
            effectiveAnnualRatePercentage: 12m,
            tiieAnnualRatePercentage: 12m,
            advancePercentage: 30m,
            collectionDelayPeriods: 1,
            baseCalculationMode: FinancingBaseCalculationMode.Accumulative,
            totalBudgetAmount: 2000m,
            isDualModel: false,
            precision: FinancingPrecision.Legacy(2),
            periods: Array.Empty<FinancingPeriodInput>()));

        Assert.AreEqual(0, result.Rows.Count);
        Assert.AreEqual(0m, result.Percentage);
    }

    [TestMethod]
    public void BaseRows_PreserveTimeComponent_WhileDelayRowsStayDateOnly()
    {
        var inicio1 = new DateTime(2026, 1, 1, 8, 30, 0);
        var fin1 = new DateTime(2026, 1, 7, 18, 0, 0);
        var inicio2 = new DateTime(2026, 1, 8, 9, 15, 0);
        var fin2 = new DateTime(2026, 1, 14, 17, 30, 0);

        // Regresión funcional corregida en NO-GO N7-17c: el legado persistía los
        // DateTime originales; el calculador NO debe truncar la hora de filas base.
        var result = Calculate(new Scenario(
            effectiveRate: 12m,
            tiieRate: 12m,
            advance: 30m,
            delay: 1,
            periods: new[]
            {
                new FinancingPeriodInput(1, "P1", inicio1, fin1, 7, 1000m, 100m, 1000m),
                new FinancingPeriodInput(2, "P2", inicio2, fin2, 7, 1000m, 100m, 1000m)
            }));

        Assert.AreEqual(3, result.Rows.Count);
        Assert.AreEqual(inicio1, result.Rows[0].StartDate);
        Assert.AreEqual(fin1, result.Rows[0].EndDate);
        Assert.AreEqual(inicio2, result.Rows[1].StartDate);
        Assert.AreEqual(fin2, result.Rows[1].EndDate);
        // La fila de desfase se construye a medianoche, igual que el legado.
        Assert.AreEqual(new DateTime(2026, 1, 15), result.Rows[2].StartDate);
        Assert.AreEqual(new DateTime(2026, 1, 21), result.Rows[2].EndDate);
    }

    [TestMethod]
    public void LegacyPrecision_AppliesFixedLegacyWidths()
    {
        var precision = FinancingPrecision.Legacy(2);

        Assert.AreEqual(2, precision.AmountDecimals);
        Assert.AreEqual(8, precision.RateDecimals);
        Assert.AreEqual(6, precision.AmortizationDecimals);
        Assert.AreEqual(4, precision.InterestDecimals);
        Assert.AreEqual(5, precision.PercentageDecimals);
    }

    [TestMethod]
    public void CustomPrecision_AppliesCustomWidthsAtEveryStage_AndNormalizesNegatives()
    {
        var precision = new FinancingPrecision(
            amountDecimals: 1,
            rateDecimals: 4,
            amortizationDecimals: 1,
            interestDecimals: 2,
            percentageDecimals: 2);

        // Entradas ya redondeadas a la precisión de montos (1 decimal), como exige el contrato.
        var result = Calculate(new Scenario(
            effectiveRate: 12m,
            tiieRate: 12m,
            advance: 33m,
            delay: 1,
            periods: Periods((1000.7m, 100.4m, 1000.7m), (1000.7m, 100.4m, 1000.7m)),
            precision: precision));

        AssertRow(result.Rows[0], 1, "P1", "2026-01-01", "2026-01-07", 7, 1101.1m, 660.0m, 0.0m, 0.0m, -441.1m, -441.1m, 1.01m);
        AssertRow(result.Rows[1], 2, "P2", "2026-01-08", "2026-01-14", 7, 1101.1m, 0.0m, 1000.7m, 330.2m, -430.6m, -871.7m, 2.00m);
        AssertRow(result.Rows[2], 3, "Período de desfase 1", "2026-01-15", "2026-01-21", 7, 0.0m, 0.0m, 1000.7m, 329.8m, 670.9m, -200.8m, 0.46m);

        // Tasa a 4 decimales: 0.00230137 -> 0.0023. Interés a 2: 1.01+2.00+0.46 = 3.47.
        AssertFinancingResult(result, negativeInterest: 3.47m, positiveInterest: 0m, net: 3.47m, percentage: 0.16m);

        var negative = new FinancingPrecision(amountDecimals: -1, rateDecimals: -8, amortizationDecimals: -6, interestDecimals: -4, percentageDecimals: -5);
        Assert.AreEqual(0, negative.AmountDecimals);
        Assert.AreEqual(0, negative.RateDecimals);
        Assert.AreEqual(0, negative.AmortizationDecimals);
        Assert.AreEqual(0, negative.InterestDecimals);
        Assert.AreEqual(0, negative.PercentageDecimals);
    }

    [TestMethod]
    public void Input_DefensivelyCopiesPeriods_AgainstCallerMutation()
    {
        var periods = Periods((1000m, 100m, 1000m), (1000m, 100m, 1000m));
        var input = new FinancingInput(
            12m,
            12m,
            30m,
            1,
            FinancingBaseCalculationMode.Accumulative,
            2000m,
            false,
            FinancingPrecision.Legacy(2),
            periods);

        periods[0] = new FinancingPeriodInput(99, "tampered", DateTime.MinValue, DateTime.MinValue, 99, 1m, 1m, 1m);

        Assert.AreEqual(2, input.Periods.Count);
        Assert.AreEqual("P1", input.Periods[0].Label);
        Assert.AreEqual(1000m, input.Periods[0].DirectCost);
    }

    [TestMethod]
    public void ResultRows_AreReadOnly()
    {
        var result = Calculate(new Scenario(effectiveRate: 12m, tiieRate: 12m, advance: 30m, delay: 1));

        Assert.ThrowsException<NotSupportedException>(() => ((IList<FinancingRowResult>)result.Rows).Add(null!));
    }

    [TestMethod]
    public void NullInput_IsRejected()
    {
        Assert.ThrowsException<ArgumentNullException>(() => FinancingCalculator.Calculate(null!));
    }

    private static FinancingResult Calculate(Scenario scenario) =>
        FinancingCalculator.Calculate(new FinancingInput(
            scenario.EffectiveRate,
            scenario.TiieRate,
            scenario.Advance,
            scenario.Delay,
            scenario.Mode,
            scenario.TotalBudget,
            scenario.Dual,
            scenario.Precision,
            scenario.Periods));

    private static void AssertFinancingResult(
        FinancingResult result,
        decimal negativeInterest,
        decimal positiveInterest,
        decimal net,
        decimal percentage)
    {
        Assert.AreEqual(negativeInterest, result.NegativeInterest);
        Assert.AreEqual(positiveInterest, result.PositiveInterest);
        Assert.AreEqual(net, result.NetFinancing);
        Assert.AreEqual(percentage, result.Percentage);
    }

    private static void AssertRow(
        FinancingRowResult row,
        int periodNumber,
        string label,
        string startDate,
        string endDate,
        int days,
        decimal expenditure,
        decimal advanceReceived,
        decimal collectedEstimate,
        decimal amortization,
        decimal netFlow,
        decimal balance,
        decimal interest)
    {
        Assert.AreEqual(periodNumber, row.PeriodNumber);
        Assert.AreEqual(label, row.Label);
        Assert.AreEqual(Date(startDate), row.StartDate);
        Assert.AreEqual(Date(endDate), row.EndDate);
        Assert.AreEqual(days, row.Days);
        Assert.AreEqual(expenditure, row.Expenditure);
        Assert.AreEqual(advanceReceived, row.AdvanceReceived);
        Assert.AreEqual(collectedEstimate, row.CollectedEstimate);
        Assert.AreEqual(amortization, row.AdvanceAmortization);
        Assert.AreEqual(netFlow, row.NetFlow);
        Assert.AreEqual(balance, row.AccumulatedBalance);
        Assert.AreEqual(interest, row.PeriodInterest);
    }

    private static DateTime Date(string value) => DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static FinancingPeriodInput[] Periods(params (decimal cd, decimal ci, decimal estimate)[] values) =>
        values.Select((value, index) => new FinancingPeriodInput(
                index + 1,
                $"P{index + 1}",
                new DateTime(2026, 1, 1 + index * 7),
                new DateTime(2026, 1, 7 + index * 7),
                7,
                value.cd,
                value.ci,
                value.estimate))
            .ToArray();

    private sealed class Scenario
    {
        public decimal EffectiveRate { get; }
        public decimal TiieRate { get; }
        public decimal Advance { get; }
        public int Delay { get; }
        public FinancingBaseCalculationMode Mode { get; }
        public decimal TotalBudget { get; }
        public bool Dual { get; }
        public FinancingPrecision Precision { get; }
        public FinancingPeriodInput[] Periods { get; }

        public Scenario(
            decimal effectiveRate,
            decimal tiieRate,
            decimal advance,
            int delay,
            FinancingBaseCalculationMode mode = FinancingBaseCalculationMode.Accumulative,
            decimal totalBudget = 2000m,
            bool dual = false,
            FinancingPrecision? precision = null,
            params FinancingPeriodInput[] periods)
        {
            EffectiveRate = effectiveRate;
            TiieRate = tiieRate;
            Advance = advance;
            Delay = delay;
            Mode = mode;
            TotalBudget = totalBudget;
            Dual = dual;
            Precision = precision ?? FinancingPrecision.Legacy(2);
            Periods = periods.Length > 0
                ? periods
                : Periods((1000m, 100m, 1000m), (1000m, 100m, 1000m));
        }
    }
}