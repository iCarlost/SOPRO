// External consumer validation for the SOPRO.Calculation package (Gate N6).
// Restores from a local feed, NOT from a project reference: this proves the
// package is self-contained and compiles outside the solution.
// Exits non-zero when any expected value differs.

using Sopro.Calculation;

var engine = new SoproCalculationEngine(2, 2, 4);

void Check(string name, decimal actual, decimal expected)
{
    if (actual != expected)
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
}

// Screen-precision multiplication (README example).
Check("Multiply", engine.Multiply(652m, 13.3875m), 8730.28m);

// Rounding.
Check("RoundAmount", engine.RoundAmount(1.005m), 1.01m);
Check("RoundQuantity", engine.RoundQuantity(2.345m), 2.35m);

// Percentage cascade (accumulative): 1000 -> CD 1000, Ind 100, Fin 66, Util 93.28, PU 1209.9480? (computed by package; verify sum invariant).
var breakdown = engine.CalculateUnitPrice(1000m, new PricePercentageInput
{
    CentralIndirectsPercentage = 5m,
    FieldIndirectsPercentage = 5m,
    FinancingPercentage = 6m,
    ProfitPercentage = 8m,
    AdditionalChargesPercentage = 3m,
    Mode = PercentageCalculationMode.Accumulative,
});
Check("Cascade invariant", breakdown.DirectCost + breakdown.IndirectCosts + breakdown.Financing
    + breakdown.Profit + breakdown.AdditionalCharges, breakdown.UnitPrice);

// Distribution with residue absorption in the last period.
var parts = engine.DistributeAmount(1000m, new[] { 33m, 33m, 34m });
Check("Distribution total", parts[0] + parts[1] + parts[2], 1000m);

// Direct cost sum with grouping and matrix filters.
var directCost = engine.SumDirectCost(new[]
{
    new DirectCostLine(10m, 25m, IsGrouping: false, HasMatrix: true),
    new DirectCostLine(99m, 99m, IsGrouping: true, HasMatrix: false),
    new DirectCostLine(2m, 50m, IsGrouping: false, HasMatrix: true),
});
Check("SumDirectCost", directCost, 350m);

Console.WriteLine("External consumer validation passed.");
