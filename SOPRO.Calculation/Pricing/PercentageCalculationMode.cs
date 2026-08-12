namespace Sopro.Calculation;

/// <summary>
/// Percentage calculation mode of the unit price cascade.
/// </summary>
/// <remarks>
/// Compatibility (N0, decision 6): the legacy compares the text with
/// <c>"SobreCD"</c> using <c>OrdinalIgnoreCase</c>; anything else (including
/// <c>null</c> and unknown texts) falls into <see cref="Accumulative"/>.
/// Mapping of the legacy text to this enum belongs to the N2 facade.
/// </remarks>
public enum PercentageCalculationMode
{
    /// <summary>Cascade: each percentage applies over the previous subtotal.</summary>
    Accumulative,

    /// <summary>Direct percentages: all apply over the direct cost.</summary>
    OverDirectCost,
}
