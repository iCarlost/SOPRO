namespace Sopro.Calculation;

/// <summary>
/// Precision numerica de pantalla: decimales para cantidades, importes y porcentajes.
/// Equivale a las propiedades de precision de <c>Proyecto</c> del dominio legacy
/// (<c>DecimalesCantidad</c>, <c>DecimalesImporte</c>, <c>DecimalesPorcentaje</c>).
/// </summary>
/// <remarks>
/// Compatibilidad (N0, fila 7): los decimales negativos se normalizan a cero con
/// <c>Math.Max(0, valor)</c>, igual que el motor legacy.
/// </remarks>
public sealed record CalculationPrecision
{
    public int DecimalesCantidad { get; }

    public int DecimalesImporte { get; }

    public int DecimalesPorcentaje { get; }

    public CalculationPrecision(int decimalesCantidad, int decimalesImporte, int decimalesPorcentaje)
    {
        DecimalesCantidad = Math.Max(0, decimalesCantidad);
        DecimalesImporte = Math.Max(0, decimalesImporte);
        DecimalesPorcentaje = Math.Max(0, decimalesPorcentaje);
    }
}
