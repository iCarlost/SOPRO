namespace Sopro.Calculation;

/// <summary>
/// Cascada de porcentajes con redondeo en cada paso visible.
/// Equivale a <c>MotorCalculoSopro.CalcularPrecioUnitario</c> del dominio legacy
/// (N0, fila 3: indirectos central + campo se suman antes del redondeo monetario).
/// </summary>
public static class UnitPriceCalculator
{
    /// <summary>
    /// Calcula el desglose completo redondeando CADA paso intermedio a
    /// <c>precision.DecimalesImporte</c> con <c>MidpointRounding.AwayFromZero</c>.
    /// El P.U. se construye como suma de partes ya redondeadas para garantizar cuadre.
    /// </summary>
    public static PriceBreakdown Calculate(
        decimal costoDirecto,
        PricePercentageInput porcentajes,
        CalculationPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(porcentajes);
        ArgumentNullException.ThrowIfNull(precision);

        bool sobreCD = porcentajes.ModoCalculoPorcentajes == PercentageCalculationMode.SobreCD;
        decimal cd = Round(precision, costoDirecto);

        decimal pInd = porcentajes.IndirectosCentral + porcentajes.IndirectosCampo;
        decimal mInd = Round(precision, cd * pInd / 100m);
        decimal sub1 = Round(precision, cd + mInd);

        decimal baseFin = sobreCD ? cd : sub1;
        decimal mFin = Round(precision, baseFin * porcentajes.Financiamiento / 100m);
        decimal sub2 = Round(precision, sub1 + mFin);

        decimal baseUtil = sobreCD ? cd : sub2;
        decimal mUtil = Round(precision, baseUtil * porcentajes.Utilidad / 100m);
        decimal sub3 = Round(precision, sub2 + mUtil);

        decimal baseCargos = sobreCD ? cd : sub3;
        decimal mCargos = Round(precision, baseCargos * porcentajes.CargosAdicionales / 100m);

        decimal pu = Round(precision, sub3 + mCargos);

        return new PriceBreakdown(
            cd,
            mInd,
            mFin,
            mUtil,
            mCargos,
            pu,
            porcentajes.IndirectosCentral,
            porcentajes.IndirectosCampo);
    }

    private static decimal Round(CalculationPrecision precision, decimal valor)
        => Math.Round(valor, precision.DecimalesImporte, MidpointRounding.AwayFromZero);
}
