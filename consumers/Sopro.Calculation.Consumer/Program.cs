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

// Percentage cascade (accumulative): CD 1000, Ind 100, Fin 66, Util 93.28,
// Cargos 37.78 -> PU 1297.06; the sum invariant is verified below.
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
Check("Cascade unit price", breakdown.UnitPrice, 1297.06m);

// Distribution with residue absorption in the last period.
// Exact division: 330 / 330 / 340.
var parts = engine.DistributeAmount(1000m, new[] { 33m, 33m, 34m });
Check("Distribution exact", parts[0], 330m);
Check("Distribution exact", parts[1], 330m);
Check("Distribution residue", parts[2], 340m);
Check("Distribution total", parts[0] + parts[1] + parts[2], 1000m);

// Repeating decimals force residue absorption: 33.33 + 33.33 + 33.34 = 100.
var residueParts = engine.DistributeAmount(100m, new[] { 1m, 1m, 1m });
Check("Residue part 1", residueParts[0], 33.33m);
Check("Residue part 2", residueParts[1], 33.33m);
Check("Residue last absorbs", residueParts[2], 33.34m);
Check("Residue total", residueParts[0] + residueParts[1] + residueParts[2], 100m);

// Direct cost sum with grouping and matrix filters.
var directCost = engine.SumDirectCost(new[]
{
    new DirectCostLine(10m, 25m, IsGrouping: false, HasMatrix: true),
    new DirectCostLine(99m, 99m, IsGrouping: true, HasMatrix: false),
    new DirectCostLine(2m, 50m, IsGrouping: false, HasMatrix: true),
});
Check("SumDirectCost", directCost, 350m);

Console.WriteLine("External consumer validation passed.");
