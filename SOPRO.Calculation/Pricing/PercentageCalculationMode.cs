namespace Sopro.Calculation;

/// <summary>
/// Modo de calculo de porcentajes de la cascada de precio unitario.
/// </summary>
/// <remarks>
/// Compatibilidad (N0, fila 6): el legacy compara el texto con
/// <c>"SobreCD"</c> usando <c>OrdinalIgnoreCase</c>; cualquier otro valor
/// (incluido <c>null</c> y textos desconocidos) cae en <see cref="Acumulables"/>.
/// Usar <see cref="PercentageCalculationModes.Parse"/> para conservar esa semantica.
/// </remarks>
public enum PercentageCalculationMode
{
    /// <summary>Cascada: cada porcentaje aplica sobre el subtotal anterior.</summary>
    Acumulables,

    /// <summary>Porcentajes directos: todos aplican sobre el costo directo.</summary>
    SobreCD,
}

/// <summary>
/// Convierte el texto legacy de <c>Proyecto.ModoCalculoPorcentajes</c> /
/// <c>BudgetPercentageInput.ModoCalculoPorcentajes</c> al enum del paquete.
/// </summary>
public static class PercentageCalculationModes
{
    /// <summary>
    /// <c>"SobreCD"</c> (cualquier casing) se convierte en <see cref="PercentageCalculationMode.SobreCD"/>;
    /// todo lo demas (<c>null</c>, vacio, <c>"Acumulables"</c>, desconocidos) en
    /// <see cref="PercentageCalculationMode.Acumulables"/>.
    /// </summary>
    public static PercentageCalculationMode Parse(string? modo)
        => string.Equals(modo, "SobreCD", StringComparison.OrdinalIgnoreCase)
            ? PercentageCalculationMode.SobreCD
            : PercentageCalculationMode.Acumulables;
}
