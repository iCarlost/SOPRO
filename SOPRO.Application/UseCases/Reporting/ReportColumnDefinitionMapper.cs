using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Proyección pura de entidades persistidas (<c>ColumnaPersonalizada</c>,
/// <c>ConfigColumnaReporte</c>, <c>ColumnaMaterial</c>) al contrato neutral
/// <see cref="ReportColumnDefinition"/>.
///
/// Precedencia (crítica):
///   - Presupuesto: <c>ColumnaPersonalizada</c> es la fuente PRIMARIA (visible,
///     orden, ancho, encabezado, contenido, wrap, formato, alineación).
///     <c>ConfigColumnaReporte</c> es SOLO overlay de estilo de encabezado, y
///     solamente cuando el nombre interno (ya normalizado) coincide.
///   - Materiales: <c>ColumnaMaterial</c> es la ÚNICA fuente; el encabezado usa
///     el default neutral de <see cref="ReportTableStyle.LegacyMateriales"/>.
///
/// No depende de ninguna tecnología de UI, persistencia o renderizado.
/// </summary>
public static class ReportColumnDefinitionMapper
{
    /// <summary>Nombre interno de la columna interna de tipo de concepto.</summary>
    public const string TipoInterno = "Tipo";

    /// <summary>Nombre interno de la columna de relleno del grid.</summary>
    public const string ColRellenoInterno = "colRelleno";

    /// <summary>Alias legacy del importe.</summary>
    public const string AliasImporte = "Importe";

    /// <summary>Identificador canónico del importe.</summary>
    public const string ImporteTotal = "ImporteTotal";

    private static readonly ReportTableStyle PresupuestoDefault = ReportTableStyle.LegacyPresupuesto();
    private static readonly ReportTableStyle MaterialesDefault = ReportTableStyle.LegacyMateriales();

    /// <summary>
    /// Indica si un nombre interno corresponde a una columna interna que nunca
    /// debe incluirse en un reporte.
    /// </summary>
    public static bool EsColumnaInterna(string? nombreInterno)
        => string.Equals(nombreInterno, TipoInterno, StringComparison.OrdinalIgnoreCase)
           || string.Equals(nombreInterno, ColRellenoInterno, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Normaliza el nombre interno: el alias legacy <c>Importe</c> se resuelve al
    /// identificador canónico <c>ImporteTotal</c>. El resto se conserva.
    /// </summary>
    public static string NormalizarIdentificador(string? nombreInterno)
    {
        var id = (nombreInterno ?? string.Empty).Trim();
        return string.Equals(id, AliasImporte, StringComparison.OrdinalIgnoreCase) ? ImporteTotal : id;
    }

    /// <summary>
    /// Normaliza el formato numérico: se conserva el valor persistido; si no hay
    /// formato y la columna es numérica, aplica "N2"; si es de texto, vacío.
    /// </summary>
    public static string NormalizarFormato(string? formato, bool esNumerica)
    {
        if (!string.IsNullOrWhiteSpace(formato)) return formato!.Trim();
        return esNumerica ? "N2" : string.Empty;
    }

    /// <summary>
    /// Mapea una columna de Presupuesto. La entidad es primaria; el overlay sólo
    /// aporta el estilo del encabezado.
    /// </summary>
    public static ReportColumnDefinition MapearPresupuesto(ColumnaPersonalizada columna, ConfigColumnaReporte? overlay)
    {
        ArgumentNullException.ThrowIfNull(columna);

        var esNumerica = EsNumerica(columna.TipoDato);
        var esMoneda = columna.TipoDato == TipoDatoColumna.Moneda
                       || EsMonedaPorPrefijoToken(columna.FormatoNumerico);

        return new ReportColumnDefinition(
            Identificador: NormalizarIdentificador(columna.NombreInterno),
            Encabezado: columna.Nombre ?? string.Empty,
            Visible: columna.Visible,
            Orden: columna.Orden,
            Ancho: columna.AnchoColumna,
            EstiloEncabezado: overlay == null
                ? PresupuestoDefault.EstiloEncabezado
                : ConstruirEstiloEncabezado(overlay, PresupuestoDefault.EstiloEncabezado),
            EstiloContenido: ConstruirEstiloContenido(
                fuente: columna.NombreFuente,
                tamano: columna.TamanoFuente,
                negrita: columna.Negrita,
                cursiva: columna.Cursiva,
                colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo,
                defecto: PresupuestoDefault.EstiloContenido),
            Alineacion: NormalizarAlineacion(columna.Alineacion, esNumerica),
            AlineacionVertical: NormalizarAlineacionVertical(columna.AlineacionVertical),
            Wrap: columna.WrapTexto,
            FormatoNumerico: NormalizarFormato(columna.FormatoNumerico, esNumerica),
            EsNumerica: esNumerica)
        {
            EsMoneda = esMoneda
        };
    }

    /// <summary>
    /// Mapea una columna del Catálogo de Materiales. La entidad es la única
    /// fuente; el encabezado usa el default neutral <see cref="ReportTableStyle.LegacyMateriales"/>.
    /// </summary>
    public static ReportColumnDefinition MapearMaterial(ColumnaMaterial columna)
    {
        ArgumentNullException.ThrowIfNull(columna);

        var esNumerica = EsNumericaColumnaMaterial(columna);
        // ColumnaMaterial no expone TipoDato: la moneda se infiere del token ("C*").
        // El default de Materiales "#,##0.0000" NO es moneda.
        var esMoneda = EsMonedaPorPrefijoToken(columna.FormatoNumerico);

        return new ReportColumnDefinition(
            Identificador: NormalizarIdentificador(columna.NombreInterno),
            Encabezado: columna.Nombre ?? string.Empty,
            Visible: columna.Visible,
            Orden: columna.Orden,
            Ancho: columna.AnchoColumna,
            EstiloEncabezado: MaterialesDefault.EstiloEncabezado,
            EstiloContenido: ConstruirEstiloContenido(
                fuente: columna.NombreFuente,
                tamano: columna.TamanoFuente,
                negrita: columna.Negrita,
                cursiva: columna.Cursiva,
                colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo,
                defecto: MaterialesDefault.EstiloContenido),
            Alineacion: NormalizarAlineacion(columna.Alineacion, esNumerica),
            AlineacionVertical: NormalizarAlineacionVertical(columna.AlineacionVertical),
            Wrap: columna.WrapTexto,
            FormatoNumerico: NormalizarFormato(columna.FormatoNumerico, esNumerica),
            EsNumerica: esNumerica)
        {
            EsMoneda = esMoneda
        };
    }

    /// <summary>
    /// Indica si el token de formato denota moneda: empieza por 'C' (p. ej. "C2",
    /// "C4"), sin distinguir mayúsculas. Los formatos numéricos explícitos
    /// ("#,##0.0000", "0.00") y los tokens "N*"/"P*" no son moneda.
    /// </summary>
    private static bool EsMonedaPorPrefijoToken(string? formato)
    {
        if (string.IsNullOrWhiteSpace(formato)) return false;
        var token = formato!.Trim();
        return token[0] == 'C' || token[0] == 'c';
    }

    private static bool EsNumerica(TipoDatoColumna tipo)
        => tipo is TipoDatoColumna.Numerico or TipoDatoColumna.Moneda or TipoDatoColumna.Porcentaje;

    /// <summary>
    /// Las columnas de material no exponen <c>TipoDato</c>: se infiere lo numérico
    /// del formato persistido o de la alineación derecha.
    /// </summary>
    private static bool EsNumericaColumnaMaterial(ColumnaMaterial columna)
        => !string.IsNullOrWhiteSpace(columna.FormatoNumerico)
           || columna.Alineacion == AlineacionColumna.Derecha;

    private static ReportTextStyle ConstruirEstiloContenido(
        string? fuente,
        int tamano,
        bool negrita,
        bool cursiva,
        string? colorFuente,
        string? colorFondo,
        ReportTextStyle defecto)
    {
        return new ReportTextStyle(
            Fuente: string.IsNullOrWhiteSpace(fuente) ? defecto.Fuente : fuente!,
            Tamano: tamano > 0 ? tamano : defecto.Tamano,
            Negrita: negrita,
            Cursiva: cursiva,
            ColorFuente: string.IsNullOrWhiteSpace(colorFuente) ? defecto.ColorFuente : colorFuente!,
            ColorFondo: string.IsNullOrWhiteSpace(colorFondo) ? defecto.ColorFondo : colorFondo);
    }

    private static ReportTextStyle ConstruirEstiloEncabezado(ConfigColumnaReporte overlay, ReportTextStyle defecto)
    {
        var colorTexto = string.IsNullOrWhiteSpace(overlay.EncColorTexto) ? defecto.ColorFuente : overlay.EncColorTexto!;
        var colorFondo = string.IsNullOrWhiteSpace(overlay.EncColorFondo)
            ? (defecto.ColorFondo ?? "#FFFFFF")
            : overlay.EncColorFondo!;

        return new ReportTextStyle(
            Fuente: string.IsNullOrWhiteSpace(overlay.EncFuente) ? defecto.Fuente : overlay.EncFuente!,
            Tamano: overlay.EncTamaño > 0 ? overlay.EncTamaño : defecto.Tamano,
            Negrita: overlay.EncNegrita,
            Cursiva: overlay.EncCursiva,
            ColorFuente: colorTexto,
            ColorFondo: colorFondo,
            ColorFuenteEncabezado: colorTexto,
            ColorFondoEncabezado: colorFondo);
    }

    /// <summary>
    /// Normaliza la alineación replicando el legacy: Centro/Derecha/Justificado
    /// se respetan; cualquier otro valor (incluido el default Izquierda) cae a
    /// Derecha si la columna es numérica y a Izquierda si es de texto.
    /// </summary>
    private static ReportTextAlignment NormalizarAlineacion(AlineacionColumna alineacion, bool esNumerica)
        => alineacion switch
        {
            AlineacionColumna.Centro => ReportTextAlignment.Centro,
            AlineacionColumna.Derecha => ReportTextAlignment.Derecha,
            AlineacionColumna.Justificado => ReportTextAlignment.Justificado,
            _ => esNumerica ? ReportTextAlignment.Derecha : ReportTextAlignment.Izquierda
        };

    private static ReportVerticalAlignment NormalizarAlineacionVertical(int valor)
        => valor switch
        {
            0 => ReportVerticalAlignment.Superior,
            2 => ReportVerticalAlignment.Inferior,
            _ => ReportVerticalAlignment.Medio
        };
}
