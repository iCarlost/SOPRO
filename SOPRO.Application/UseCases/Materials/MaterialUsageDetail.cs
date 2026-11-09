namespace SOPRO.Application.UseCases.Materials;

public sealed record MaterialUsageDetail(
    IReadOnlyList<MaterialUsageInMatrix> Matrices,
    IReadOnlyList<MaterialMachineryUsage> MachineryUsages);
