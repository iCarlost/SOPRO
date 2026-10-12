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
    private static readonly ReportTableStyle CatalogoDefault = ReportTableStyle.LegacyCatalogo();

    /// <summary>
    /// Nombres internos canónicos que denotan un rol monetario/importe en los
    /// catálogos, aunque su token no empiece por 'C'. Es el reconocimiento COMÚN
    /// que comparten los reportes de catálogo (Mano de Obra, Herramientas,
    /// Maquinaria/Costo Horario, Explosión, Indirectos, Financiamiento y
    /// Programas). Se compara sin distinguir mayúsculas y tolerando el prefijo de
    /// grid <c>col</c> de los Programas. El token "C*" sigue siendo la señal
    /// universal y tiene prioridad.
    /// </summary>
    private static readonly HashSet<string> NombresMonetariosCatalogo = new(StringComparer.OrdinalIgnoreCase)
    {
        "PrecioUnitario", "PrecioUnitarioFinal", "Importe", "ImporteTotal", "ImporteMensual",
        "SalarioBase", "SalarioReal", "CostoHorario", "CostoUnitario", "CostoDirecto",
        "CostoDirectoUnitario", "CostoTotal", "Total", "Subtotal",
    };

    /// <summary>
    /// Nombres internos canónicos que denotan un rol de porcentaje en los
    /// catálogos (el token "P*" sigue siendo la señal universal).
    /// </summary>
    private static readonly HashSet<string> NombresPorcentajeCatalogo = new(StringComparer.OrdinalIgnoreCase)
    {
        "Porcentaje", "PorcentajeIndirectos", "PorcentajeFinanciamiento", "PorcentajeUtilidad",
        "PctIndirectosCentral", "PctIndirectosCampo", "TasaPeriodo", "TasaInteres",
    };

    /// <summary>
    /// Nombres internos canónicos que denotan un rol de cantidad en los catálogos
    /// (el token "N*" sigue siendo la señal universal).
    /// </summary>
    private static readonly HashSet<string> NombresCantidadCatalogo = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cantidad", "Rendimiento", "RendimientoDiario",
    };

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
    /// Reconocimiento COMÚN de tokens de formato del contrato, compartido por el
    /// mapper (mapeo) y por <c>ReportColumnGridFormat</c> (resolución de formato).
    /// Un token es una letra inicial ("C", "N", "P") que marca el rol; los
    /// formatos .NET explícitos ("#,##0.0000", "0.00") no lo son.
    /// </summary>
    public static bool EsTokenMonetario(string? formato) => EsTokenInicial(formato, 'C');

    /// <summary>Token de cantidad ("N*").</summary>
    public static bool EsTokenCantidad(string? formato) => EsTokenInicial(formato, 'N');

    /// <summary>Token de porcentaje ("P*").</summary>
    public static bool EsTokenPorcentaje(string? formato) => EsTokenInicial(formato, 'P');

    /// <summary>
    /// Reconocimiento COMÚN del rol monetario/importe: token "C*" o nombre interno
    /// canónico del catálogo. Es la regla que las 11 migraciones reutilizan para
    /// decidir <c>ReportColumnDefinition.EsMoneda</c>.
    /// </summary>
    public static bool EsRolMonetario(string? nombreInterno, string? formatoNumerico)
        => EsTokenMonetario(formatoNumerico) || EsRolMonetarioPorNombre(nombreInterno);

    /// <summary>Reconocimiento COMÚN del rol de porcentaje: token "P*" o nombre canónico.</summary>
    public static bool EsRolPorcentaje(string? nombreInterno, string? formatoNumerico)
        => EsTokenPorcentaje(formatoNumerico) || EsRolPorcentajePorNombre(nombreInterno);

    /// <summary>Reconocimiento COMÚN del rol de cantidad: token "N*" o nombre canónico.</summary>
    public static bool EsRolCantidad(string? nombreInterno, string? formatoNumerico)
        => EsTokenCantidad(formatoNumerico) || EsRolCantidadPorNombre(nombreInterno);

    private static bool EsRolMonetarioPorNombre(string? nombreInterno)
        => NombresMonetariosCatalogo.Contains(SinPrefijoCol(nombreInterno));

    private static bool EsRolPorcentajePorNombre(string? nombreInterno)
        => NombresPorcentajeCatalogo.Contains(SinPrefijoCol(nombreInterno));

    private static bool EsRolCantidadPorNombre(string? nombreInterno)
        => NombresCantidadCatalogo.Contains(SinPrefijoCol(nombreInterno));

    /// <summary>
    /// Normaliza el nombre para el reconocimiento de rol: recorta espacios y
    /// descarta el prefijo de grid <c>col</c> (p. ej. <c>colImporte</c> →
    /// <c>Importe</c>), sin afectar el identificador neutral.
    /// </summary>
    private static string SinPrefijoCol(string? nombreInterno)
    {
        var id = (nombreInterno ?? string.Empty).Trim();
        if (id.Length > 3 && id.StartsWith("col", StringComparison.OrdinalIgnoreCase))
            id = id[3..];
        return id;
    }

    private static bool EsTokenInicial(string? formato, char inicial)
    {
        if (string.IsNullOrWhiteSpace(formato)) return false;
        var token = formato!.Trim();
        return token.Length > 0 && (token[0] == inicial || token[0] == char.ToLowerInvariant(inicial));
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
    /// El Precio Unitario es monetario aunque su token sea el default
    /// "#,##0.0000" (paridad con el grid). Se conserva EXACTAMENTE la semántica
    /// del piloto: token "C*" o <c>PrecioUnitario</c>.
    /// </summary>
    public static ReportColumnDefinition MapearMaterial(ColumnaMaterial columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno,
                nombre: columna.Nombre,
                visible: columna.Visible,
                orden: columna.Orden,
                ancho: columna.AnchoColumna,
                alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico,
                nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente,
                colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo,
                negrita: columna.Negrita,
                cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto,
                alineacionVertical: columna.AlineacionVertical),
            MaterialesDefault,
            EsRolMonetarioPorNombreMaterial);
    }

    // ─────────── Catálogos restantes (Fase 0): Mano de Obra, Herramientas, ───────────
    // ─────────── Maquinaria/Costo Horario, Explosión, Indirectos,          ───────────
    // ─────────── Financiamiento y Programas de Obra/Insumos.              ───────────
    // Todas las entidades de catálogo comparten la misma forma; comparten un único
    // núcleo de mapeo (<see cref="MapearCatalogo"/>) y el reconocimiento COMÚN de
    // roles (<see cref="EsRolMonetario"/>). El estilo neutral es
    // <see cref="ReportTableStyle.LegacyCatalogo"/> (línea base; cada reporte podrá
    // fijar su estilo legacy específico en su propia migración).

    /// <summary>Mapea una columna del catálogo de Mano de Obra.</summary>
    public static ReportColumnDefinition MapearManoObra(ColumnaManoObra columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno, nombre: columna.Nombre, visible: columna.Visible,
                orden: columna.Orden, ancho: columna.AnchoColumna, alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico, nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente, colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo, negrita: columna.Negrita, cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto, alineacionVertical: columna.AlineacionVertical),
            CatalogoDefault,
            EsRolMonetarioPorNombre);
    }

    /// <summary>Mapea una columna del catálogo de Herramientas.</summary>
    public static ReportColumnDefinition MapearHerramienta(ColumnaHerramienta columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno, nombre: columna.Nombre, visible: columna.Visible,
                orden: columna.Orden, ancho: columna.AnchoColumna, alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico, nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente, colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo, negrita: columna.Negrita, cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto, alineacionVertical: columna.AlineacionVertical),
            CatalogoDefault,
            EsRolMonetarioPorNombre);
    }

    /// <summary>Mapea una columna del catálogo de Maquinaria/Costo Horario.</summary>
    public static ReportColumnDefinition MapearMaquinaria(ColumnaMaquinaria columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno, nombre: columna.Nombre, visible: columna.Visible,
                orden: columna.Orden, ancho: columna.AnchoColumna, alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico, nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente, colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo, negrita: columna.Negrita, cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto, alineacionVertical: columna.AlineacionVertical),
            CatalogoDefault,
            EsRolMonetarioPorNombre);
    }

    /// <summary>Mapea una columna del módulo Explosión de Insumos (y reutilizable por APU).</summary>
    public static ReportColumnDefinition MapearExplosion(ColumnaExplosion columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno, nombre: columna.Nombre, visible: columna.Visible,
                orden: columna.Orden, ancho: columna.AnchoColumna, alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico, nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente, colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo, negrita: columna.Negrita, cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto, alineacionVertical: columna.AlineacionVertical),
            CatalogoDefault,
            EsRolMonetarioPorNombre);
    }

    /// <summary>Mapea una columna del módulo Cálculo de Indirectos.</summary>
    public static ReportColumnDefinition MapearIndirectos(ColumnaIndirectos columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno, nombre: columna.Nombre, visible: columna.Visible,
                orden: columna.Orden, ancho: columna.AnchoColumna, alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico, nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente, colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo, negrita: columna.Negrita, cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto, alineacionVertical: columna.AlineacionVertical),
            CatalogoDefault,
            EsRolMonetarioPorNombre);
    }

    /// <summary>Mapea una columna del módulo Financiamiento (y reutilizable por Utilidad).</summary>
    public static ReportColumnDefinition MapearFinanciamiento(ColumnaFinanciamiento columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno, nombre: columna.Nombre, visible: columna.Visible,
                orden: columna.Orden, ancho: columna.AnchoColumna, alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico, nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente, colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo, negrita: columna.Negrita, cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto, alineacionVertical: columna.AlineacionVertical),
            CatalogoDefault,
            EsRolMonetarioPorNombre);
    }

    /// <summary>Mapea una columna del Programa de Obra.</summary>
    public static ReportColumnDefinition MapearProgramaObra(ColumnaProgramaObra columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno, nombre: columna.Nombre, visible: columna.Visible,
                orden: columna.Orden, ancho: columna.AnchoColumna, alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico, nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente, colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo, negrita: columna.Negrita, cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto, alineacionVertical: columna.AlineacionVertical),
            CatalogoDefault,
            EsRolMonetarioPorNombre);
    }

    /// <summary>Mapea una columna del Programa de Insumos.</summary>
    public static ReportColumnDefinition MapearProgramaInsumos(ColumnaProgramaInsumos columna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        return MapearCatalogo(
            Proyectar(
                nombreInterno: columna.NombreInterno, nombre: columna.Nombre, visible: columna.Visible,
                orden: columna.Orden, ancho: columna.AnchoColumna, alineacion: columna.Alineacion,
                formatoNumerico: columna.FormatoNumerico, nombreFuente: columna.NombreFuente,
                tamanoFuente: columna.TamanoFuente, colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo, negrita: columna.Negrita, cursiva: columna.Cursiva,
                wrapTexto: columna.WrapTexto, alineacionVertical: columna.AlineacionVertical),
            CatalogoDefault,
            EsRolMonetarioPorNombre);
    }

    /// <summary>
    /// Núcleo compartido de mapeo de una columna de catálogo. Aplica la
    /// normalización neutral común y decide <c>EsMoneda</c> con el predicado de
    /// rol monetario del reporte. Es privado: la superficie pública son los
    /// overloads por entidad, para no acoplar a los reportes a una estructura.
    /// </summary>
    private static ReportColumnDefinition MapearCatalogo(
        ColumnaCatalogo columna,
        ReportTableStyle estilo,
        Func<string?, bool> esMonedaPorNombre)
    {
        var esNumerica = EsNumericaCatalogo(columna.FormatoNumerico, columna.Alineacion);
        var esMoneda = esMonedaPorNombre(columna.NombreInterno)
                       || EsTokenMonetario(columna.FormatoNumerico);

        return new ReportColumnDefinition(
            Identificador: NormalizarIdentificador(columna.NombreInterno),
            Encabezado: columna.Nombre ?? string.Empty,
            Visible: columna.Visible,
            Orden: columna.Orden,
            Ancho: columna.AnchoColumna,
            EstiloEncabezado: estilo.EstiloEncabezado,
            EstiloContenido: ConstruirEstiloContenido(
                fuente: columna.NombreFuente,
                tamano: columna.TamanoFuente,
                negrita: columna.Negrita,
                cursiva: columna.Cursiva,
                colorFuente: columna.ColorFuente,
                colorFondo: columna.ColorFondo,
                defecto: estilo.EstiloContenido),
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
    /// El Precio Unitario del catálogo de Materiales SIEMPRE es monetario (paridad
    /// con el grid), aunque su token sea el default "#,##0.0000".
    /// </summary>
    private static bool EsRolMonetarioPorNombreMaterial(string? nombreInterno)
        => string.Equals(nombreInterno, "PrecioUnitario", StringComparison.OrdinalIgnoreCase);

    /// <summary>Campos compartidos por todas las entidades de columna de catálogo.</summary>
    private readonly record struct ColumnaCatalogo(
        string? NombreInterno,
        string? Nombre,
        bool Visible,
        int Orden,
        int AnchoColumna,
        AlineacionColumna Alineacion,
        string? FormatoNumerico,
        string? NombreFuente,
        int TamanoFuente,
        string? ColorFuente,
        string? ColorFondo,
        bool Negrita,
        bool Cursiva,
        bool WrapTexto,
        int AlineacionVertical);

    private static ColumnaCatalogo Proyectar(
        string? nombreInterno,
        string? nombre,
        bool visible,
        int orden,
        int ancho,
        AlineacionColumna alineacion,
        string? formatoNumerico,
        string? nombreFuente,
        int tamanoFuente,
        string? colorFuente,
        string? colorFondo,
        bool negrita,
        bool cursiva,
        bool wrapTexto,
        int alineacionVertical)
        => new(
            NombreInterno: nombreInterno,
            Nombre: nombre,
            Visible: visible,
            Orden: orden,
            AnchoColumna: ancho,
            Alineacion: alineacion,
            FormatoNumerico: formatoNumerico,
            NombreFuente: nombreFuente,
            TamanoFuente: tamanoFuente,
            ColorFuente: colorFuente,
            ColorFondo: colorFondo,
            Negrita: negrita,
            Cursiva: cursiva,
            WrapTexto: wrapTexto,
            AlineacionVertical: alineacionVertical);

    /// <summary>
    /// Indica si el token de formato denota moneda: empieza por 'C' (p. ej. "C2",
    /// "C4"), sin distinguir mayúsculas. Delega en el reconocimiento común
    /// <see cref="EsTokenMonetario"/>.
    /// </summary>
    private static bool EsMonedaPorPrefijoToken(string? formato) => EsTokenMonetario(formato);

    private static bool EsNumerica(TipoDatoColumna tipo)
        => tipo is TipoDatoColumna.Numerico or TipoDatoColumna.Moneda or TipoDatoColumna.Porcentaje;

    /// <summary>
    /// Las columnas de catálogo no exponen <c>TipoDato</c>: se infiere lo numérico
    /// del formato persistido o de la alineación derecha.
    /// </summary>
    private static bool EsNumericaCatalogo(string? formatoNumerico, AlineacionColumna alineacion)
        => !string.IsNullOrWhiteSpace(formatoNumerico)
           || alineacion == AlineacionColumna.Derecha;

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
