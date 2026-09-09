using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sopro.Calculation.Financing;

/// <summary>
/// Period distribution of a single concept for the direct-cost preparation.
/// The period index is validated by
/// <see cref="FinancingPreparationCalculator.PrepareBaseSchedule"/> and must be
/// inside <c>[0, FinancingPreparationInput.PeriodCount)</c>.
/// </summary>
/// <param name="PeriodIndex">
/// Zero-based index of the target period inside the program order (see
/// <see cref="FinancingPreparationInput.PeriodCount"/>).
/// </param>
/// <param name="ProgrammedQuantity">Programmed quantity (<c>CantidadProgramada</c>) for the period.</param>
public sealed record FinancingConceptDistribution(int PeriodIndex, decimal ProgrammedQuantity);

/// <summary>
/// Estimate accumulation line coming from the stored <c>ImporteProgramado</c> of a
/// distribution. Order matters: the estimate is rounded after each addition.
/// </summary>
/// <param name="PeriodIndex">
/// Zero-based index of the target period inside the program order; validated by
/// <see cref="FinancingPreparationCalculator.PrepareBaseSchedule"/>.
/// </param>
/// <param name="ProgrammedImport">Programmed import (<c>ImporteProgramado</c>) to accumulate.</param>
public sealed record FinancingEstimateLine(int PeriodIndex, decimal ProgrammedImport);

/// <summary>
/// Immutable concept input of the direct-cost preparation. Mirrors the fields used
/// by the legacy flow to compute a concept CD total and its period distribution.
/// </summary>
public sealed class FinancingConceptInput
{
    /// <summary>Total quantity of the concept (<c>Cantidad</c>).</summary>
    public decimal QuantityTotal { get; }

    /// <summary>Unit direct cost of the concept (<c>CostoDirectoUnitario</c>).</summary>
    public decimal UnitDirectCost { get; }

    /// <summary>Total direct cost of the concept (<c>CostoDirectoTotal</c>).</summary>
    public decimal TotalDirectCost { get; }

    /// <summary>
    /// Ordered period shares of the concept. Exposed as a truly read-only
    /// collection (see <see cref="ReadOnlyCollection{T}"/>).
    /// </summary>
    public IReadOnlyList<FinancingConceptDistribution> Distributions { get; }

    /// <summary>
    /// Creates an immutable concept input, defensively copying the distributions.
    /// </summary>
    /// <param name="quantityTotal">Total quantity of the concept.</param>
    /// <param name="unitDirectCost">Unit direct cost of the concept.</param>
    /// <param name="totalDirectCost">Total direct cost of the concept.</param>
    /// <param name="distributions">Ordered period shares of the concept (may be null).</param>
    public FinancingConceptInput(
        decimal quantityTotal,
        decimal unitDirectCost,
        decimal totalDirectCost,
        IEnumerable<FinancingConceptDistribution>? distributions)
    {
        QuantityTotal = quantityTotal;
        UnitDirectCost = unitDirectCost;
        TotalDirectCost = totalDirectCost;
        Distributions = new ReadOnlyCollection<FinancingConceptDistribution>(
            (distributions ?? Enumerable.Empty<FinancingConceptDistribution>()).ToList());
    }
}

/// <summary>
/// Immutable input of the CD/CI preparation. Holds only the numeric preparation
/// data (no EF, entities or persistence concerns): the caller is responsible for
/// EF queries, filtering, grouping, ordering and any other application-level
/// concern.
/// </summary>
public sealed class FinancingPreparationInput
{
    /// <summary>Amount precision of the preparation.</summary>
    public int AmountDecimals { get; }

    /// <summary>Number of base periods of the program.</summary>
    public int PeriodCount { get; }

    /// <summary>
    /// Concepts whose direct cost is distributed across the periods. Exposed as
    /// a truly read-only collection.
    /// </summary>
    public IReadOnlyList<FinancingConceptInput> Concepts { get; }

    /// <summary>
    /// Estimate accumulation lines in the exact order of the legacy call flow
    /// (rounding is applied after each addition). Exposed as a truly read-only
    /// collection.
    /// </summary>
    public IReadOnlyList<FinancingEstimateLine> Estimates { get; }

    /// <summary>
    /// Creates an immutable preparation input, defensively copying the lists.
    /// </summary>
    /// <param name="amountDecimals">Amount precision of the preparation.</param>
    /// <param name="periodCount">Number of base periods of the program.</param>
    /// <param name="concepts">Concepts whose direct cost is distributed across the periods (may be null).</param>
    /// <param name="estimates">Estimate accumulation lines (may be null).</param>
    public FinancingPreparationInput(
        int amountDecimals,
        int periodCount,
        IEnumerable<FinancingConceptInput>? concepts,
        IEnumerable<FinancingEstimateLine>? estimates)
    {
        if (periodCount < 0)
            throw new ArgumentOutOfRangeException(nameof(periodCount), "The number of periods cannot be negative.");

        AmountDecimals = Math.Max(0, amountDecimals);
        PeriodCount = periodCount;
        Concepts = new ReadOnlyCollection<FinancingConceptInput>(
            (concepts ?? Enumerable.Empty<FinancingConceptInput>()).ToList());
        Estimates = new ReadOnlyCollection<FinancingEstimateLine>(
            (estimates ?? Enumerable.Empty<FinancingEstimateLine>()).ToList());
    }
}

/// <summary>
/// Immutable prepared period output of the CD/CI preparation.
/// </summary>
/// <param name="DirectCost">
/// Direct cost of the period, already rounded and with the distribution residue
/// absorbed in the last period that has amount.
/// </param>
/// <param name="IndirectCost">
/// Indirect cost of the period, already reconciled against the official amount.
/// </param>
/// <param name="Expenditure">
/// Total expenditure of the period: rounded direct + indirect cost.
/// </param>
/// <param name="EstimatedAmount">
/// Collected estimate of the period: accumulated <c>ImporteProgramado</c> rounded
/// after each addition. Never receives the direct-cost residue.
/// </param>
public sealed record FinancingPreparedPeriod(
    decimal DirectCost,
    decimal IndirectCost,
    decimal Expenditure,
    decimal EstimatedAmount);

/// <summary>
/// Pure CD/CI preparation of the financing flow (N7-17d). Centralizes the numeric
/// preparation that the legacy application performed inline: distribution of the
/// direct cost with residue absorption, the collected-estimate accumulation and
/// the reconciliation of the official indirect-cost total across the periods.
///
/// The official indirect-cost total itself is NOT computed here: it comes from the
/// budget reference-cost preview (<c>ModoCalculoPorcentajes</c>), whose single
/// implementation lives in the application. This component only receives that total
/// and distributes it following the legacy reconciliation rules.
///
/// Semantics preserved from the legacy flow 1:1:
/// <list type="bullet">
/// <item>the multiplication of quantity and unit price rounds the visible unit
/// price first and then the result (project amount precision, AwayFromZero);</item>
/// <item>the residue (expected minus distributed) is absorbed in the last period
/// whose programmed quantity or computed import is non-zero (when the total is
/// positive and every quantity is zero, the whole residue lands in the last
/// distribution, exactly like the legacy fallback);</item>
/// <item>the collected estimate is accumulated from the stored
/// <c>ImporteProgramado</c> and never receives the direct-cost residue;</item>
/// <item>when the current indirect total differs from the official total, the
/// legacy reconciliation applies: proportional distribution over the periods that
/// have amount, or over the direct-cost base when the current total is zero
/// (assigning the whole official total to the last period when the base is zero too).</item>
/// </list>
/// </summary>
/// <remarks>
/// Contract:
/// <list type="bullet">
/// <item>Every <see cref="FinancingConceptDistribution.PeriodIndex"/> and
/// <see cref="FinancingEstimateLine.PeriodIndex"/> must be inside
/// <c>[0, PeriodCount)</c>; anything else throws
/// <see cref="ArgumentOutOfRangeException"/> (fail-fast, no silent loss). The
/// check runs in an initial pass over the whole input, BEFORE any semantic guard
/// (quantity, expected total or empty distributions), so omitted concepts cannot
/// hide an invalid index.</item>
/// <item>Inputs and results are exposed through truly read-only collections
/// (<see cref="ReadOnlyCollection{T}"/>); they cannot be mutated through casts.</item>
/// <item>Null inputs throw <see cref="ArgumentNullException"/>.</item>
/// </list>
/// </remarks>
public static class FinancingPreparationCalculator
{
    /// <summary>
    /// Prepares the base schedule: distributes the concept direct cost across the
    /// periods (absorbing each residue in the last period with amount) and
    /// accumulates the collected estimates. The returned rows carry no indirect
    /// cost; <see cref="ReconcileIndirectCost"/> must be applied afterwards.
    /// </summary>
    /// <param name="input">Preparation input.</param>
    /// <returns>
    /// One prepared period per <see cref="FinancingPreparationInput.PeriodCount"/>,
    /// in program order, exposed through an immutable collection. Direct cost is
    /// rounded; expenditure equals the direct cost at this stage and is recomputed
    /// after the indirect-cost reconciliation.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A period index of a distribution or an estimate is outside <c>[0, PeriodCount)</c>.
    /// </exception>
    public static IReadOnlyList<FinancingPreparedPeriod> PrepareBaseSchedule(FinancingPreparationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Pasada inicial de validación: todo PeriodIndex del input debe estar en
        // rango ANTES de cualquier guarda semántica (cantidad/total/distribuciones),
        // para que el contrato fail-fast no admita huecos en conceptos omitidos.
        foreach (var concept in input.Concepts)
        {
            foreach (var distribution in concept.Distributions)
                ValidarIndice(distribution.PeriodIndex, input.PeriodCount, nameof(FinancingConceptDistribution));
        }

        foreach (var estimate in input.Estimates)
            ValidarIndice(estimate.PeriodIndex, input.PeriodCount, nameof(FinancingEstimateLine));

        var directCost = new decimal[input.PeriodCount];
        var estimated = new decimal[input.PeriodCount];

        foreach (var concept in input.Concepts)
        {
            if (concept.QuantityTotal <= 0m)
                continue;

            decimal cdUnit = concept.UnitDirectCost;
            if (cdUnit <= 0m && concept.TotalDirectCost > 0m)
                cdUnit = concept.TotalDirectCost / concept.QuantityTotal;

            if (concept.Distributions.Count == 0)
                continue;

            decimal totalEsperado = Multiply(concept.QuantityTotal, cdUnit, input.AmountDecimals);
            if (totalEsperado == 0m)
                continue;

            var importes = new decimal[concept.Distributions.Count];
            decimal suma = 0m;
            int ultimoIndiceConMonto = -1;

            for (int i = 0; i < concept.Distributions.Count; i++)
            {
                var distribution = concept.Distributions[i];
                decimal importe = Multiply(distribution.ProgrammedQuantity, cdUnit, input.AmountDecimals);
                importes[i] = importe;
                suma += importe;
                if (distribution.ProgrammedQuantity != 0m || importe != 0m)
                    ultimoIndiceConMonto = i;
            }

            if (ultimoIndiceConMonto < 0)
                ultimoIndiceConMonto = concept.Distributions.Count - 1;

            importes[ultimoIndiceConMonto] += totalEsperado - suma;

            for (int i = 0; i < concept.Distributions.Count; i++)
                directCost[concept.Distributions[i].PeriodIndex] += importes[i];
        }

        foreach (var estimate in input.Estimates)
        {
            estimated[estimate.PeriodIndex] = Round(
                estimated[estimate.PeriodIndex] + estimate.ProgrammedImport, input.AmountDecimals);
        }

        var result = new FinancingPreparedPeriod[input.PeriodCount];
        for (int i = 0; i < input.PeriodCount; i++)
        {
            decimal cd = Round(directCost[i], input.AmountDecimals);
            result[i] = new FinancingPreparedPeriod(cd, 0m, cd, estimated[i]);
        }

        return new ReadOnlyCollection<FinancingPreparedPeriod>(result);
    }

    /// <summary>
    /// Reconciles the indirect cost of the prepared rows against the official
    /// total and rebuilds the expenditure. Replicates the legacy reconciliation
    /// exactly: when the current indirect total equals the official one nothing
    /// changes; otherwise the official total is distributed proportionally over
    /// the periods that have indirect amount, over the direct-cost base when the
    /// current indirect total is zero (last unrounded period absorbs the rounding
    /// residue), or assigned whole to the last period when the base is zero too.
    /// </summary>
    /// <param name="rows">Prepared base rows (see <see cref="PrepareBaseSchedule"/>).</param>
    /// <param name="officialIndirectCost">
    /// Official indirect-cost total already computed by the caller (budget
    /// reference-cost preview) and already rounded.
    /// </param>
    /// <param name="amountDecimals">Amount precision of the reconciliation.</param>
    /// <returns>
    /// A new immutable list of prepared periods with indirect cost, rounded
    /// expenditure and estimates.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public static IReadOnlyList<FinancingPreparedPeriod> ReconcileIndirectCost(
        IReadOnlyList<FinancingPreparedPeriod> rows,
        decimal officialIndirectCost,
        int amountDecimals)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
            return new ReadOnlyCollection<FinancingPreparedPeriod>(Array.Empty<FinancingPreparedPeriod>());

        var indirectCost = new decimal[rows.Count];
        for (int i = 0; i < rows.Count; i++)
            indirectCost[i] = rows[i].IndirectCost;

        decimal totalActual = indirectCost.Sum();
        if (totalActual != officialIndirectCost)
        {
            var filasConMonto = new List<int>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (indirectCost[i] != 0m)
                    filasConMonto.Add(i);
            }

            if (filasConMonto.Count == 0)
                filasConMonto.AddRange(Enumerable.Range(0, rows.Count));

            if (totalActual == 0m)
            {
                decimal totalBase = rows.Sum(r => r.DirectCost);
                if (totalBase > 0m)
                {
                    decimal acumulado = 0m;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        decimal nuevo = i == rows.Count - 1
                            ? officialIndirectCost - acumulado
                            : Round(officialIndirectCost * rows[i].DirectCost / totalBase, amountDecimals);
                        indirectCost[i] = nuevo;
                        acumulado += nuevo;
                    }
                }
                else
                {
                    indirectCost[filasConMonto[^1]] = officialIndirectCost;
                }
            }
            else
            {
                decimal acumuladoDistrib = 0m;
                for (int i = 0; i < filasConMonto.Count; i++)
                {
                    int index = filasConMonto[i];
                    decimal nuevo = i == filasConMonto.Count - 1
                        ? officialIndirectCost - acumuladoDistrib
                        : Round(officialIndirectCost * indirectCost[index] / totalActual, amountDecimals);
                    indirectCost[index] = nuevo;
                    acumuladoDistrib += nuevo;
                }
            }
        }

        var result = new FinancingPreparedPeriod[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            decimal cd = Round(rows[i].DirectCost, amountDecimals);
            decimal ci = Round(indirectCost[i], amountDecimals);
            result[i] = new FinancingPreparedPeriod(
                cd,
                ci,
                Round(cd + ci, amountDecimals),
                Round(rows[i].EstimatedAmount, amountDecimals));
        }

        return new ReadOnlyCollection<FinancingPreparedPeriod>(result);
    }

    private static void ValidarIndice(int index, int periodCount, string itemName)
    {
        if (index < 0 || index >= periodCount)
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"{itemName}.PeriodIndex ({index}) is outside the range [0, {periodCount}).");
    }

    private static decimal Round(decimal value, int decimals)
        => Math.Round(value, Math.Max(0, decimals), MidpointRounding.AwayFromZero);

    private static decimal Multiply(decimal quantity, decimal unitPrice, int amountDecimals)
        => Round(Round(unitPrice, amountDecimals) * quantity, amountDecimals);
}