using System;
using System.Collections.Generic;

namespace SOPRO.Reporting.Layout;

/// <summary>
/// Conversiones neutrales de ancho de columna entre píxeles (contrato
/// <c>ReportColumnDefinition.Ancho</c>, en px) y las unidades de los
/// renderizadores: centímetros para PDF/MigraDoc y "caracteres" para
/// Excel/ClosedXML.
///
/// Reproduce las fórmulas de los generadores legacy:
/// <list type="bullet">
/// <item>PDF: reparto proporcional al área útil de la página con la última
/// columna absorbiendo el resto (mismo criterio que
/// <c>GeneradorPdfCatalogoMateriales.AgregarColumnas</c> y
/// <c>GeneradorPdfCatalogoHerramientas.AgregarColumnas</c>).</item>
/// <item>Excel: <c>px / 7</c> con mínimo 4 (mismo criterio que
/// <c>GeneradorExcelPresupuesto.AplicarAnchos</c>).</item>
/// </list>
/// Es deliberadamente independiente de MigraDoc y de ClosedXML para poder
/// reutilizarse desde cualquier renderizador.
/// </summary>
public static class ReportColumnWidthConverter
{
    /// <summary>Ancho mínimo de columna en píxeles aplicado antes de convertir.</summary>
    public const int MinimumColumnWidthPx = 24;

    /// <summary>Ancho mínimo por defecto (cm) de una columna PDF.</summary>
    public const double DefaultPdfMinimumWidthCm = 1.2;

    /// <summary>Ancho mínimo por defecto de una columna Excel (unidades de carácter).</summary>
    public const double DefaultExcelMinimumWidth = 4.0;

    /// <summary>Píxeles por unidad de ancho de columna Excel (factor legacy 1/7).</summary>
    public const double ExcelPixelsPerWidthUnit = 7.0;

    /// <summary>Ancho de página Letter vertical (cm).</summary>
    public const double LetterPortraitWidthCm = 21.59;

    /// <summary>Ancho de página Letter horizontal (cm).</summary>
    public const double LetterLandscapeWidthCm = 27.94;

    /// <summary>Margen por defecto del renderer SOPRO (cm), izquierdo y derecho.</summary>
    public const double DefaultMarginCm = 1.0;

    private const int CmDecimals = 4;

    /// <summary>
    /// Ancho útil de una sección Letter (ancho de página menos márgenes),
    /// con el mismo resultado que <c>ReportPageLayoutHelper.GetLetterContentWidthCm</c>.
    /// </summary>
    /// <param name="landscape">True para Letter horizontal.</param>
    /// <param name="leftMarginCm">Margen izquierdo en cm.</param>
    /// <param name="rightMarginCm">Margen derecho en cm.</param>
    public static double GetLetterUsableWidthCm(
        bool landscape = false,
        double leftMarginCm = DefaultMarginCm,
        double rightMarginCm = DefaultMarginCm)
    {
        double pageWidth = landscape ? LetterLandscapeWidthCm : LetterPortraitWidthCm;
        return Math.Max(8.0, pageWidth - leftMarginCm - rightMarginCm);
    }

    /// <summary>
    /// Conversión proporcional de un único ancho px→cm (fórmula legacy:
    /// <c>usableCm * max(24, px) / totalPx</c>). No redistribuye resto; úsese
    /// para estimaciones o cuando ya se conoce el ancho total de la fila.
    /// </summary>
    /// <param name="px">Ancho de la columna en píxeles.</param>
    /// <param name="usableWidthCm">Área útil horizontal de la página en cm.</param>
    /// <param name="totalWidthPx">Suma de anchos (px, ya con el mínimo aplicado) de todas las columnas.</param>
    public static double PxToCm(int px, double usableWidthCm, double totalWidthPx)
    {
        double effectivePx = Math.Max(MinimumColumnWidthPx, px);
        double total = Math.Max(1.0, totalWidthPx);
        return usableWidthCm * effectivePx / total;
    }

    /// <summary>
    /// Reparte el ancho útil entre todas las columnas de forma proporcional a
    /// sus píxeles y garantiza que la SUMA sea exactamente el área útil: la
    /// última columna absorbe la diferencia de redondeo. Nunca excede el área
    /// útil y aplica el mínimo por columna cuando es factible.
    /// </summary>
    /// <param name="columnWidthsPx">Anchos de columna en píxeles, en orden.</param>
    /// <param name="usableWidthCm">Área útil horizontal de la página en cm.</param>
    /// <param name="minWidthCm">Ancho mínimo deseado por columna (cm).</param>
    /// <returns>Anchos en cm; su suma es <paramref name="usableWidthCm"/>.</returns>
    public static double[] PxToCm(
        IReadOnlyList<int> columnWidthsPx,
        double usableWidthCm,
        double minWidthCm = DefaultPdfMinimumWidthCm)
    {
        if (columnWidthsPx == null) throw new ArgumentNullException(nameof(columnWidthsPx));

        int count = columnWidthsPx.Count;
        if (count == 0) return Array.Empty<double>();

        var effective = new double[count];
        double totalPx = 0;
        for (int i = 0; i < count; i++)
        {
            effective[i] = Math.Max(MinimumColumnWidthPx, columnWidthsPx[i]);
            totalPx += effective[i];
        }
        if (totalPx <= 0) totalPx = count;

        var widths = new double[count];
        double assigned = 0;
        for (int i = 0; i < count - 1; i++)
        {
            double raw = usableWidthCm * effective[i] / totalPx;
            double width = Math.Max(minWidthCm, Math.Round(raw, CmDecimals, MidpointRounding.AwayFromZero));
            widths[i] = width;
            assigned += width;
        }

        double last = usableWidthCm - assigned;
        if (last < minWidthCm)
        {
            // El mínimo por columna no es factible para este reparto (demasiadas
            // columnas estrechas o redondeo): se reparte proporcionalmente sin
            // recortes para no exceder el área útil.
            assigned = 0;
            for (int i = 0; i < count - 1; i++)
            {
                double raw = usableWidthCm * effective[i] / totalPx;
                widths[i] = raw;
                assigned += raw;
            }
            last = usableWidthCm - assigned;
        }

        widths[count - 1] = Math.Max(0d, last);
        return widths;
    }

    /// <summary>
    /// Conversión px→ancho de columna Excel (unidades de carácter). Fórmula
    /// legacy: <c>max(px / 7, 4)</c>.
    /// </summary>
    /// <param name="px">Ancho de la columna en píxeles.</param>
    /// <param name="minWidth">Ancho mínimo en unidades Excel.</param>
    public static double PxToExcelWidth(int px, double minWidth = DefaultExcelMinimumWidth)
        => Math.Max(px / ExcelPixelsPerWidthUnit, minWidth);

    /// <summary>
    /// Convierte una lista de anchos px a anchos de columna Excel.
    /// </summary>
    /// <param name="columnWidthsPx">Anchos de columna en píxeles, en orden.</param>
    /// <param name="minWidth">Ancho mínimo en unidades Excel.</param>
    public static double[] PxToExcelWidths(
        IReadOnlyList<int> columnWidthsPx,
        double minWidth = DefaultExcelMinimumWidth)
    {
        if (columnWidthsPx == null) throw new ArgumentNullException(nameof(columnWidthsPx));

        var widths = new double[columnWidthsPx.Count];
        for (int i = 0; i < widths.Length; i++)
            widths[i] = PxToExcelWidth(columnWidthsPx[i], minWidth);
        return widths;
    }
}
