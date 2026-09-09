using System.Collections.ObjectModel;

namespace Sopro.Calculation.Financing;

/// <summary>
/// Base used to compute the financing percentage and the advance base when no
/// total budget is supplied. Mirrors the legacy <c>ModoCalculoPorcentajes</c>/
/// <c>BaseCalculo</c> strings (<c>"SobreCD"</c> and <c>"Acumulable"</c>).
/// </summary>
public enum FinancingBaseCalculationMode
{
    /// <summary>Financing percentage applies over (direct + indirect) cost.</summary>
    Accumulative = 0,

    /// <summary>Financing percentage applies over direct cost only.</summary>
    OverDirectCost = 1
}

/// <summary>
/// Fixed rounding policy of the legacy financing flow. Amounts use the project
/// screen precision; rates, amortization, interest and the resulting percentage
/// use the historical fixed widths (8, 6, 4 and 5 decimals respectively), all
/// with <see cref="MidpointRounding.AwayFromZero"/>.
/// </summary>
public sealed record FinancingPrecision
{
    /// <summary>Decimals used for all monetary amounts (project precision).</summary>
    public int AmountDecimals { get; }

    /// <summary>Decimals used for the prorated period rate.</summary>
    public int RateDecimals { get; }

    /// <summary>Decimals used for advance amortization.</summary>
    public int AmortizationDecimals { get; }

    /// <summary>Decimals used for period interest and interest totals.</summary>
    public int InterestDecimals { get; }

    /// <summary>Decimals used for the calculated financing percentage.</summary>
    public int PercentageDecimals { get; }

    /// <summary>
    /// Creates a precision policy, normalizing negative values to zero
    /// following the package convention.
    /// </summary>
    /// <param name="amountDecimals">Decimals used for monetary amounts.</param>
    /// <param name="rateDecimals">Decimals used for the prorated period rate.</param>
    /// <param name="amortizationDecimals">Decimals used for advance amortization.</param>
    /// <param name="interestDecimals">Decimals used for interest values.</param>
    /// <param name="percentageDecimals">Decimals used for the financing percentage.</param>
    public FinancingPrecision(
        int amountDecimals,
        int rateDecimals = 8,
        int amortizationDecimals = 6,
        int interestDecimals = 4,
        int percentageDecimals = 5)
    {
        AmountDecimals = Math.Max(0, amountDecimals);
        RateDecimals = Math.Max(0, rateDecimals);
        AmortizationDecimals = Math.Max(0, amortizationDecimals);
        InterestDecimals = Math.Max(0, interestDecimals);
        PercentageDecimals = Math.Max(0, percentageDecimals);
    }

    /// <summary>
    /// Returns the fixed legacy rounding policy for a given amount precision.
    /// Equivalent to <c>new FinancingPrecision(amountDecimals)</c>.
    /// </summary>
    public static FinancingPrecision Legacy(int amountDecimals) => new(amountDecimals);
}

/// <summary>
/// Immutable prepared period for the financing flow. Carries the scalars already
/// resolved by the caller: direct and indirect cost, the collected estimate and
/// the working days/dates of the period.
/// </summary>
/// <remarks>
/// Critical divergence preserved from the legacy service:
/// <see cref="EstimatedAmount"/> is accumulated from the stored
/// <c>ImporteProgramado</c> of the distribution, NOT from the direct cost
/// adjusted by the distribution residue. The residue only reshapes
/// <see cref="DirectCost"/>; it must not leak into the collected estimate.
/// </remarks>
public sealed class FinancingPeriodInput
{
    /// <summary>Stable period number (program order).</summary>
    public int PeriodNumber { get; }

    /// <summary>Period label.</summary>
    public string Label { get; }

    /// <summary>Inclusive start date.</summary>
    public DateTime StartDate { get; }

    /// <summary>Inclusive end date.</summary>
    public DateTime EndDate { get; }

    /// <summary>Calendar days of the period (used to prorate the annual rate).</summary>
    public int Days { get; }

    /// <summary>Direct cost of the period, already rounded by the caller.</summary>
    public decimal DirectCost { get; }

    /// <summary>Indirect cost of the period, already reconciled by the caller.</summary>
    public decimal IndirectCost { get; }

    /// <summary>
    /// Collected estimate of the period (accumulated <c>ImporteProgramado</c>),
    /// already rounded by the caller.
    /// </summary>
    public decimal EstimatedAmount { get; }

    /// <summary>Creates an immutable prepared period.</summary>
    public FinancingPeriodInput(
        int periodNumber,
        string label,
        DateTime startDate,
        DateTime endDate,
        int days,
        decimal directCost,
        decimal indirectCost,
        decimal estimatedAmount)
    {
        ArgumentNullException.ThrowIfNull(label);

        PeriodNumber = periodNumber;
        Label = label;
        StartDate = startDate;
        EndDate = endDate;
        Days = days;
        DirectCost = directCost;
        IndirectCost = indirectCost;
        EstimatedAmount = estimatedAmount;
    }
}

/// <summary>Immutable input of the pure financing calculation.</summary>
public sealed class FinancingInput
{
    /// <summary>Effective annual rate (TIIE + additional points), in percent.</summary>
    public decimal EffectiveAnnualRatePercentage { get; }

    /// <summary>TIIE annual rate, in percent (used for dual-model positive balances).</summary>
    public decimal TiieAnnualRatePercentage { get; }

    /// <summary>Advance percentage over the total contract amount (0 when none).</summary>
    public decimal AdvancePercentage { get; }

    /// <summary>
    /// Collection delay in program periods: estimate of period N is collected in
    /// period N + delay.
    /// </summary>
    public int CollectionDelayPeriods { get; }

    /// <summary>Base used for the financing percentage.</summary>
    public FinancingBaseCalculationMode BaseCalculationMode { get; }

    /// <summary>
    /// Total contract budget. When greater than zero it is used as the advance
    /// base; otherwise the base of the selected calculation mode is used (direct
    /// + indirect for <see cref="FinancingBaseCalculationMode.Accumulative"/>,
    /// direct cost only for <see cref="FinancingBaseCalculationMode.OverDirectCost"/>),
    /// exactly like the legacy fallback.
    /// </summary>
    public decimal TotalBudgetAmount { get; }

    /// <summary>
    /// Dual model: positive balances earn interest at the TIIE rate and negative
    /// balances pay interest at the effective rate. In the classic model all
    /// interest is financial cost.
    /// </summary>
    public bool IsDualModel { get; }

    /// <summary>Rounding policy of the calculation.</summary>
    public FinancingPrecision Precision { get; }

    /// <summary>
    /// Prepared base periods of the program. Delay ("collection lag") rows are
    /// appended by the calculator itself.
    /// </summary>
    public IReadOnlyList<FinancingPeriodInput> Periods { get; }

    /// <summary>Creates an immutable financing input, defensively copying the periods.</summary>
    public FinancingInput(
        decimal effectiveAnnualRatePercentage,
        decimal tiieAnnualRatePercentage,
        decimal advancePercentage,
        int collectionDelayPeriods,
        FinancingBaseCalculationMode baseCalculationMode,
        decimal totalBudgetAmount,
        bool isDualModel,
        FinancingPrecision precision,
        IEnumerable<FinancingPeriodInput> periods)
    {
        ArgumentNullException.ThrowIfNull(precision);
        ArgumentNullException.ThrowIfNull(periods);

        EffectiveAnnualRatePercentage = effectiveAnnualRatePercentage;
        TiieAnnualRatePercentage = tiieAnnualRatePercentage;
        AdvancePercentage = advancePercentage;
        CollectionDelayPeriods = collectionDelayPeriods;
        BaseCalculationMode = baseCalculationMode;
        TotalBudgetAmount = totalBudgetAmount;
        IsDualModel = isDualModel;
        Precision = precision;
        Periods = new ReadOnlyCollection<FinancingPeriodInput>(periods.ToList());
    }
}

/// <summary>Immutable calculated row of the financing flow.</summary>
public sealed class FinancingRowResult
{
    /// <summary>Stable period number, including appended delay rows.</summary>
    public int PeriodNumber { get; }

    /// <summary>Period label.</summary>
    public string Label { get; }

    /// <summary>Inclusive start date.</summary>
    public DateTime StartDate { get; }

    /// <summary>Inclusive end date.</summary>
    public DateTime EndDate { get; }

    /// <summary>Calendar days of the period.</summary>
    public int Days { get; }

    /// <summary>Expenditure of the period (direct + indirect), rounded to amount precision.</summary>
    public decimal Expenditure { get; }

    /// <summary>Advance received in the period, rounded to amount precision.</summary>
    public decimal AdvanceReceived { get; }

    /// <summary>Collected estimate, rounded to amount precision.</summary>
    public decimal CollectedEstimate { get; }

    /// <summary>Advance amortization discounted in the period, rounded to amount precision.</summary>
    public decimal AdvanceAmortization { get; }

    /// <summary>Net flow = advance + collected - amortization - expenditure.</summary>
    public decimal NetFlow { get; }

    /// <summary>Accumulated balance at the end of the period.</summary>
    public decimal AccumulatedBalance { get; }

    /// <summary>
    /// Period interest. Positive = income (dual model), negative = cost
    /// (dual model). In the classic model it is always stored positive and the
    /// result totals classify it by balance sign.
    /// </summary>
    public decimal PeriodInterest { get; }

    internal FinancingRowResult(
        int periodNumber,
        string label,
        DateTime startDate,
        DateTime endDate,
        int days,
        decimal expenditure,
        decimal advanceReceived,
        decimal collectedEstimate,
        decimal advanceAmortization,
        decimal netFlow,
        decimal accumulatedBalance,
        decimal periodInterest)
    {
        PeriodNumber = periodNumber;
        Label = label;
        StartDate = startDate;
        EndDate = endDate;
        Days = days;
        Expenditure = expenditure;
        AdvanceReceived = advanceReceived;
        CollectedEstimate = collectedEstimate;
        AdvanceAmortization = advanceAmortization;
        NetFlow = netFlow;
        AccumulatedBalance = accumulatedBalance;
        PeriodInterest = periodInterest;
    }
}

/// <summary>Immutable result of the pure financing calculation.</summary>
public sealed class FinancingResult
{
    /// <summary>Total interest classified as financial cost.</summary>
    public decimal NegativeInterest { get; }

    /// <summary>Total interest classified as financial income (dual model).</summary>
    public decimal PositiveInterest { get; }

    /// <summary>
    /// Net financing = PositiveInterest - NegativeInterest (dual), or the
    /// negative interest alone (classic model).
    /// </summary>
    public decimal NetFinancing { get; }

    /// <summary>Financing percentage = NetFinancing / Base x 100.</summary>
    public decimal Percentage { get; }

    /// <summary>Calculated flow rows in period order.</summary>
    public IReadOnlyList<FinancingRowResult> Rows { get; }

    internal FinancingResult(
        decimal negativeInterest,
        decimal positiveInterest,
        decimal netFinancing,
        decimal percentage,
        IEnumerable<FinancingRowResult> rows)
    {
        NegativeInterest = negativeInterest;
        PositiveInterest = positiveInterest;
        NetFinancing = netFinancing;
        Percentage = percentage;
        Rows = new ReadOnlyCollection<FinancingRowResult>(rows.ToList());
    }
}

/// <summary>
/// Pure deterministic financing flow: advance, amortization with pending and
/// collected caps, collection delay, net flow, accumulated balance, prorated
/// period interest and the resulting financing percentage.
/// </summary>
/// <remarks>
/// Exact legacy semantics (N7-17a characterization): rates are prorated by a
/// 365-day year and rounded to <see cref="FinancingPrecision.RateDecimals"/>;
/// amortization rounds to <see cref="FinancingPrecision.AmortizationDecimals"/>
/// and is capped by the pending advance and the collected estimate; interest
/// rounds to <see cref="FinancingPrecision.InterestDecimals"/>; row amounts and
/// the advance round to <see cref="FinancingPrecision.AmountDecimals"/>. When
/// there are no periods or the base is not positive, the result is empty (no
/// rows, zero totals), matching the legacy early returns before any persistence.
/// </remarks>
public static class FinancingCalculator
{
    /// <summary>Calculates the financing flow deterministically.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
    public static FinancingResult Calculate(FinancingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var precision = input.Precision;
        var basePeriods = input.Periods;

        decimal cdTotal = basePeriods.Sum(period => period.DirectCost);
        decimal ciTotal = basePeriods.Sum(period => period.IndirectCost);
        decimal baseAmount = input.BaseCalculationMode == FinancingBaseCalculationMode.OverDirectCost
            ? cdTotal
            : cdTotal + ciTotal;

        if (basePeriods.Count == 0 || baseAmount <= 0m)
            return new FinancingResult(0m, 0m, 0m, 0m, Array.Empty<FinancingRowResult>());

        decimal advanceBase = input.TotalBudgetAmount > 0m ? input.TotalBudgetAmount : baseAmount;
        decimal advanceTotal = RoundAmount(precision, advanceBase * (input.AdvancePercentage / 100m));
        decimal pendingAmortization = advanceTotal;
        decimal amortizationRate = input.AdvancePercentage / 100m;
        decimal negativeAnnualRate = input.EffectiveAnnualRatePercentage / 100m;
        decimal positiveAnnualRate = input.TiieAnnualRatePercentage / 100m;

        var flowRows = BuildFlowRows(input, basePeriods, precision);

        var rows = new List<FinancingRowResult>(flowRows.Count);
        decimal accumulatedBalance = 0m;

        for (int i = 0; i < flowRows.Count; i++)
        {
            var period = flowRows[i];

            decimal advanceReceived = i == 0 ? advanceTotal : 0m;

            decimal collectedEstimate = 0m;
            int collectedIndex = i - input.CollectionDelayPeriods;
            if (collectedIndex >= 0 && collectedIndex < flowRows.Count)
                collectedEstimate = flowRows[collectedIndex].EstimatedAmount;

            decimal amortization = 0m;
            if (collectedEstimate > 0m && pendingAmortization > 0m)
            {
                amortization = Math.Round(
                    collectedEstimate * amortizationRate,
                    precision.AmortizationDecimals,
                    MidpointRounding.AwayFromZero);
                amortization = Math.Min(amortization, pendingAmortization);
                amortization = Math.Min(amortization, collectedEstimate);
                pendingAmortization -= amortization;
            }

            decimal netFlow = advanceReceived + collectedEstimate - amortization - period.Expenditure;
            accumulatedBalance += netFlow;

            decimal periodInterest = 0m;
            if (accumulatedBalance < 0m)
            {
                decimal interest = Math.Round(
                    Math.Abs(accumulatedBalance) * GetPeriodRate(precision, negativeAnnualRate, period.Days),
                    precision.InterestDecimals,
                    MidpointRounding.AwayFromZero);
                periodInterest = input.IsDualModel ? -interest : interest;
            }
            else if (input.IsDualModel && accumulatedBalance > 0m)
            {
                periodInterest = Math.Round(
                    accumulatedBalance * GetPeriodRate(precision, positiveAnnualRate, period.Days),
                    precision.InterestDecimals,
                    MidpointRounding.AwayFromZero);
            }

            rows.Add(new FinancingRowResult(
                period.PeriodNumber,
                period.Label,
                period.StartDate,
                period.EndDate,
                period.Days,
                RoundAmount(precision, period.Expenditure),
                RoundAmount(precision, advanceReceived),
                RoundAmount(precision, collectedEstimate),
                RoundAmount(precision, amortization),
                RoundAmount(precision, netFlow),
                RoundAmount(precision, accumulatedBalance),
                periodInterest));
        }

        decimal negativeInterest;
        decimal positiveInterest;
        decimal netFinancing;

        if (input.IsDualModel)
        {
            negativeInterest = Math.Round(
                rows.Where(row => row.PeriodInterest < 0m).Sum(row => Math.Abs(row.PeriodInterest)),
                precision.InterestDecimals,
                MidpointRounding.AwayFromZero);
            positiveInterest = Math.Round(
                rows.Where(row => row.PeriodInterest > 0m).Sum(row => row.PeriodInterest),
                precision.InterestDecimals,
                MidpointRounding.AwayFromZero);
            netFinancing = Math.Round(
                positiveInterest - negativeInterest,
                precision.InterestDecimals,
                MidpointRounding.AwayFromZero);
        }
        else
        {
            negativeInterest = Math.Round(
                rows.Where(row => row.AccumulatedBalance < 0m).Sum(row => Math.Abs(row.PeriodInterest)),
                precision.InterestDecimals,
                MidpointRounding.AwayFromZero);
            positiveInterest = 0m;
            netFinancing = Math.Round(negativeInterest, precision.InterestDecimals, MidpointRounding.AwayFromZero);
        }

        decimal percentage = Math.Round(
            netFinancing / baseAmount * 100m,
            precision.PercentageDecimals,
            MidpointRounding.AwayFromZero);

        return new FinancingResult(negativeInterest, positiveInterest, netFinancing, percentage, rows);
    }

    private static List<FlowPeriod> BuildFlowRows(
        FinancingInput input,
        IReadOnlyList<FinancingPeriodInput> basePeriods,
        FinancingPrecision precision)
    {
        var flowRows = basePeriods
            .Select(period => new FlowPeriod(period, RoundAmount(precision, period.DirectCost + period.IndirectCost)))
            .ToList();

        if (input.CollectionDelayPeriods > 0 && flowRows.Count > 0)
        {
            var last = flowRows[^1];
            for (int extra = 1; extra <= input.CollectionDelayPeriods; extra++)
            {
                DateTime start = last.EndDate.Date.AddDays(1 + last.Days * (extra - 1));
                DateTime end = start.AddDays(last.Days - 1);
                flowRows.Add(new FlowPeriod(
                    last.PeriodNumber + extra,
                    $"Período de desfase {extra}",
                    start,
                    end,
                    last.Days,
                    0m,
                    0m));
            }
        }

        return flowRows;
    }

    private static decimal GetPeriodRate(FinancingPrecision precision, decimal annualRate, int days) =>
        annualRate <= 0m || days <= 0
            ? 0m
            : Math.Round(annualRate * days / 365m, precision.RateDecimals, MidpointRounding.AwayFromZero);

    private static decimal RoundAmount(FinancingPrecision precision, decimal value) =>
        Math.Round(value, precision.AmountDecimals, MidpointRounding.AwayFromZero);

    private sealed class FlowPeriod
    {
        public int PeriodNumber { get; }
        public string Label { get; }
        public DateTime StartDate { get; }
        public DateTime EndDate { get; }
        public int Days { get; }
        public decimal Expenditure { get; }
        public decimal EstimatedAmount { get; }

        public FlowPeriod(FinancingPeriodInput period, decimal expenditure)
            : this(period.PeriodNumber, period.Label, period.StartDate, period.EndDate, period.Days, expenditure, period.EstimatedAmount)
        {
        }

        public FlowPeriod(
            int periodNumber,
            string label,
            DateTime startDate,
            DateTime endDate,
            int days,
            decimal expenditure,
            decimal estimatedAmount)
        {
            PeriodNumber = periodNumber;
            Label = label;
            StartDate = startDate;
            EndDate = endDate;
            Days = days;
            Expenditure = expenditure;
            EstimatedAmount = estimatedAmount;
        }
    }
}