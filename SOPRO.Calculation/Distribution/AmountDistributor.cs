namespace Sopro.Calculation;

/// <summary>
/// Distribucion temporal con ajuste de residuo en el ultimo periodo.
/// Equivale a <c>MotorCalculoSopro.DistribuirImporte</c>/<c>DistribuirCantidad</c>
/// del dominio legacy (N0, filas 1, 5, 10, 11).
/// </summary>
public static class AmountDistributor
{
    /// <summary>
    /// Distribuye un importe total entre N periodos proporcionalmente a sus pesos,
    /// redondeando cada parte a <c>precision.DecimalesImporte</c>.
    /// Garantia critica: <c>Sum(resultado) == total</c>. El ultimo periodo absorbe el
    /// residuo (puede quedar negativo, N0 fila 5).
    /// Colecciones nulas o vacias devuelven vacio; suma de pesos cero devuelve ceros
    /// (N0 fila 11).
    /// </summary>
    public static IReadOnlyList<decimal> DistributeImporte(
        decimal total,
        IReadOnlyList<decimal> pesos,
        CalculationPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(precision);
        return Distribute(total, pesos, precision.DecimalesImporte);
    }

    /// <summary>
    /// Idem <see cref="DistributeImporte"/> usando <c>precision.DecimalesCantidad</c>.
    /// </summary>
    public static IReadOnlyList<decimal> DistributeCantidad(
        decimal total,
        IReadOnlyList<decimal> pesos,
        CalculationPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(precision);
        return Distribute(total, pesos, precision.DecimalesCantidad);
    }

    private static IReadOnlyList<decimal> Distribute(decimal total, IReadOnlyList<decimal>? pesos, int decimales)
    {
        if (pesos == null || pesos.Count == 0) return Array.Empty<decimal>();

        decimal sumaPesos = pesos.Sum();
        if (sumaPesos == 0m)
            return pesos.Select(_ => 0m).ToList();

        var resultado = new decimal[pesos.Count];
        decimal acumulado = 0m;

        for (int i = 0; i < pesos.Count - 1; i++)
        {
            decimal proporcion = pesos[i] / sumaPesos;
            resultado[i] = Math.Round(total * proporcion, decimales, MidpointRounding.AwayFromZero);
            acumulado += resultado[i];
        }

        resultado[^1] = Math.Round(total - acumulado, decimales, MidpointRounding.AwayFromZero);
        return resultado;
    }
}
