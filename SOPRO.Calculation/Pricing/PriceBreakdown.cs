namespace Sopro.Calculation;

/// <summary>
/// Desglose completo de un precio unitario, con cada componente ya redondeado.
/// Equivale a <c>DesglosePrecios</c> del dominio legacy.
/// Garantia: CD + Indirectos + Financiamiento + Utilidad + Cargos == PrecioUnitario.
/// </summary>
public sealed record PriceBreakdown(
    decimal CostoDirecto,
    decimal Indirectos,
    decimal Financiamiento,
    decimal Utilidad,
    decimal CargosAdicionales,
    decimal PrecioUnitario,
    decimal PctIndirectosCentral = 0m,
    decimal PctIndirectosCampo = 0m)
{
    /// <summary>
    /// Importe de Indirectos OC (oficina central), prorrateado con precision fija de
    /// 6 decimales y <c>MidpointRounding.AwayFromZero</c> (N0, fila 4).
    /// </summary>
    public decimal IndirectosCentral =>
        (PctIndirectosCentral + PctIndirectosCampo) > 0m
            ? Math.Round(Indirectos * PctIndirectosCentral
                         / (PctIndirectosCentral + PctIndirectosCampo),
                         6, MidpointRounding.AwayFromZero)
            : 0m;

    /// <summary>Importe de Indirectos Campo.</summary>
    public decimal IndirectosCampo => Indirectos - IndirectosCentral;

    /// <summary>Subtotal CD + Indirectos.</summary>
    public decimal Subtotal1 => CostoDirecto + Indirectos;

    /// <summary>Subtotal CD + Ind + Financiamiento.</summary>
    public decimal Subtotal2 => Subtotal1 + Financiamiento;

    /// <summary>Subtotal CD + Ind + Fin + Utilidad.</summary>
    public decimal Subtotal3 => Subtotal2 + Utilidad;
}
