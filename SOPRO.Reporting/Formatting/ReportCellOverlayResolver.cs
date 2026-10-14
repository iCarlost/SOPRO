using System;
using SOPRO.Application.Models.Reporting.ReportColumns;

namespace SOPRO.Reporting.Formatting;

/// <summary>
/// Aísla las propiedades que un override de celda o un estilo semántico de fila
/// REALMENTE aportaron sobre la cascada base (tabla + columna, sin celda ni rol).
///
/// Es el punto ÚNICO que los renderizadores (PDF/Excel) y el grid de los
/// catálogos usan para decidir "qué pisa" un override respecto del estilo
/// heredado. Con <paramref name="snapshotBase"/> sin estilos, TODAS las
/// propiedades quedan <c>null</c>, de modo que el render conserva EXACTAMENTE el
/// comportamiento legacy cuando no hay overrides (retrocompatibilidad total).
///
/// No depende de UI ni de renderizado: sólo del contrato neutral y de
/// <see cref="ReportCellStyleResolver"/>.
/// </summary>
public static class ReportCellOverlayResolver
{
    /// <summary>
    /// Devuelve el overlay EFECTIVO (sólo las propiedades no nulas) que el estilo
    /// resuelto de la celda aporta por encima de la cascada base. Una propiedad que
    /// coincide con la base (incluido "ambas null") se devuelve como <c>null</c>.
    /// </summary>
    /// <param name="snapshot">Snapshot con overrides de celda y estilos de rol.</param>
    /// <param name="snapshotBase">Mismo snapshot SIN estilos (línea base).</param>
    /// <param name="claveVista">Vista lógica (comodín si va vacía).</param>
    /// <param name="claveFila">Clave estable de la fila.</param>
    /// <param name="nombreColumna">Identificador de la columna.</param>
    /// <param name="rolFila">Rol funcional de la fila, o <c>null</c> si no aplica.</param>
    public static ReportStyleOverlay OverrideEfectivo(
        ReportColumnSnapshot snapshot,
        ReportColumnSnapshot snapshotBase,
        string claveVista,
        string claveFila,
        string nombreColumna,
        string? rolFila = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshotBase);

        var completo = ReportCellStyleResolver.Resolve(
            snapshot, claveVista, claveFila, nombreColumna, rolFila, esFilaAlterna: false);
        var baseStyle = ReportCellStyleResolver.Resolve(
            snapshotBase, claveVista, claveFila, nombreColumna, rolFila: null, esFilaAlterna: false);

        return new ReportStyleOverlay(
            Fuente: completo.Fuente != baseStyle.Fuente ? completo.Fuente : null,
            TamanoFuente: completo.TamanoFuente != baseStyle.TamanoFuente ? completo.TamanoFuente : null,
            Negrita: completo.Negrita != baseStyle.Negrita ? completo.Negrita : null,
            Cursiva: completo.Cursiva != baseStyle.Cursiva ? completo.Cursiva : null,
            ColorFuente: completo.ColorFuente != baseStyle.ColorFuente ? completo.ColorFuente : null,
            ColorFondo: completo.ColorFondo != baseStyle.ColorFondo ? completo.ColorFondo : null,
            AlineacionHorizontal: completo.AlineacionHorizontal != baseStyle.AlineacionHorizontal
                ? completo.AlineacionHorizontal : null,
            AlineacionVertical: completo.AlineacionVertical != baseStyle.AlineacionVertical
                ? completo.AlineacionVertical : null,
            Wrap: completo.Wrap != baseStyle.Wrap ? completo.Wrap : null,
            FormatoNumerico: completo.FormatoNumerico != baseStyle.FormatoNumerico
                ? completo.FormatoNumerico : null);
    }

    /// <summary>
    /// Snapshot SIN estilos (celda y rol): la línea base contra la que
    /// <see cref="OverrideEfectivo"/> mide el delta de un override.
    /// </summary>
    public static ReportColumnSnapshot SinEstilos(ReportColumnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot with { EstilosCelda = null, EstilosFila = null };
    }
}
