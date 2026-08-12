namespace Sopro.Calculation;

/// <summary>
/// Motor de calculo del paquete: aritmetica con precision de pantalla.
/// Equivale a <c>MotorCalculoSopro</c> del dominio legacy, sin formato de cultura
/// (el formato queda en la fachada, N0 fila 9) y sin tipos del dominio SOPRO.
/// </summary>
public sealed class SoproCalculationEngine
{
    public CalculationPrecision Precision { get; }

    public SoproCalculationEngine(CalculationPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(precision);
        Precision = precision;
    }

    public SoproCalculationEngine(int decimalesCantidad, int decimalesImporte, int decimalesPorcentaje)
        : this(new CalculationPrecision(decimalesCantidad, decimalesImporte, decimalesPorcentaje))
    {
    }

    public int DecimalesCantidad => Precision.DecimalesCantidad;

    public int DecimalesImporte => Precision.DecimalesImporte;

    public int DecimalesPorcentaje => Precision.DecimalesPorcentaje;

    // ═══ Redondeo ═══

    public decimal RedondearCantidad(decimal valor)
        => Math.Round(valor, Precision.DecimalesCantidad, MidpointRounding.AwayFromZero);

    public decimal RedondearImporte(decimal valor)
        => Math.Round(valor, Precision.DecimalesImporte, MidpointRounding.AwayFromZero);

    public decimal RedondearPorcentaje(decimal valor)
        => Math.Round(valor, Precision.DecimalesPorcentaje, MidpointRounding.AwayFromZero);

    // ═══ Operacion de pantalla principal ═══

    /// <summary>
    /// Multiplicacion con precision de pantalla: redondea el P.U. visible,
    /// multiplica y redondea el resultado (N0, fila 2: la cantidad NO se
    /// redondea previamente).
    /// </summary>
    public decimal Multiplicar(decimal cantidad, decimal precioUnitario)
    {
        decimal puVisible = RedondearImporte(precioUnitario);
        return RedondearImporte(cantidad * puVisible);
    }

    public decimal CalcularImporteSobreBase(decimal factor, decimal baseImporte)
        => Multiplicar(factor, baseImporte);

    // ═══ Cascada de porcentajes ═══

    public PriceBreakdown CalcularPrecioUnitario(decimal costoDirecto, PricePercentageInput porcentajes)
        => UnitPriceCalculator.Calculate(costoDirecto, porcentajes, Precision);

    // ═══ Distribucion temporal ═══

    public IReadOnlyList<decimal> DistribuirImporte(decimal total, IReadOnlyList<decimal> pesos)
        => AmountDistributor.DistributeImporte(total, pesos, Precision);

    public IReadOnlyList<decimal> DistribuirCantidad(decimal total, IReadOnlyList<decimal> pesos)
        => AmountDistributor.DistributeCantidad(total, pesos, Precision);

    // ═══ Sumas de precision ═══

    public decimal SumarImportes(IEnumerable<decimal> valores)
    {
        if (valores == null) return 0m;
        decimal acc = 0m;
        foreach (var v in valores)
            acc += RedondearImporte(v);
        return RedondearImporte(acc);
    }

    public decimal SumarCantidades(IEnumerable<decimal> valores)
    {
        if (valores == null) return 0m;
        decimal acc = 0m;
        foreach (var v in valores)
            acc += RedondearCantidad(v);
        return RedondearCantidad(acc);
    }
}
