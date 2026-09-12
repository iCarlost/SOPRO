using SOPRO.Application.Models.Reporting.MatrixCatalog;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del modelo de "Catálogo de Matrices".
///
/// Reproduce fielmente la aritmética legacy (sin redondeo, sin cálculo canónico):
///   - totalMO: suma de (MO no-%MO × cantidad) + (auxiliar cuadrilla × cantidad).
///   - filas %MO: importe = totalMO × cantidad; base mostrada = totalMO.
///   - orden ordinal por clave de matriz (solver externo en N7-28).
///   - el costo directo se usa tal cual se leyó de la matriz (N0-TABLA fila 22).
///
/// Los textos de encabezado/pie incluyen tokens resueltos con el reloj explícito
/// {fecha_impresion}. {pagina} y {total_paginas} quedan para que el medio los
/// resuelva. Las zonas de tipo "Imagen" mantienen la ruta sin resolver.
/// Se valida que cada sustitución produzca exactamente la longitud esperada.
/// </summary>
internal sealed class MatrixCatalogReportModelBuilder
{
    private static readonly string DefaultFallbackTitle = "CATÁLOGO DE MATRICES";
    private const string DefaultFontName = "Segoe UI";
    private const double DefaultTitleSize = 14d;
    private const bool DefaultTitleBold = true;
    private const bool DefaultTitleItalic = false;
    private const string DefaultTextColorHex = "#FFFFFF";
    private const string DefaultZoneColorHex = "#000000";
    private const double DefaultZoneSize = 9d;

    public MatrixCatalogReportDocument Build(
        MatrixCatalogReportSettings settings,
        IReadOnlyCollection<MatrixCatalogSourceMatrix> matrices,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(matrices);

        var proyecto = settings.Project;
        var template = settings.Template ?? MatrixCatalogTemplate.Vacia;

        var title = ResolverTitulo(settings.FiltroTitulo, settings.TitleOptions);
        var metadataTitle = ResolverTituloDocumental(settings.FiltroTitulo, settings.TitleOptions);
        var titleStyle = BuildTitleStyle(settings.TitleOptions);

        var header = ResolverFranja(
            template.EncabezadoIzq,
            template.EncabezadoCen,
            template.EncabezadoDer,
            proyecto,
            template,
            now);

        var footer = ResolverFranja(
            template.PieIzq,
            template.PieCen,
            template.PieDer,
            proyecto,
            template,
            now);

        var headerElements = ResolverElementosLibres(template.ElementosEncabezado, proyecto, template, now);
        var footerElements = ResolverElementosLibres(template.ElementosPie, proyecto, template, now);

        var matricesModel = matrices
            .Where(m => m != null)
            .OrderBy(m => m.Clave, StringComparer.Ordinal)
            .Select(BuildMatriz)
            .ToList();

        return new MatrixCatalogReportDocument(
            title,
            titleStyle,
            header,
            footer,
            matricesModel,
            settings.Project.Nombre,
            metadataTitle,
            headerElements,
            footerElements,
            template.Heights ?? MatrixCatalogPageHeights.Default);
    }

    /// <summary>
    /// Resuelve los tokens de los elementos libres PDF con el reloj explícito,
    /// dejando únicamente {pagina} y {total_paginas} sin resolver (el medio de
    /// salida los convierte en campos reales). Los elementos de tipo imagen no
    /// resuelven nada (la imagen son sus bytes, no texto).
    /// </summary>
    private static List<MatrixCatalogPageElement> ResolverElementosLibres(
        IEnumerable<MatrixCatalogPageElement>? elementos,
        MatrixCatalogProject proyecto,
        MatrixCatalogTemplate plantilla,
        DateTime now)
    {
        if (elementos == null) return new List<MatrixCatalogPageElement>();

        return elementos
            .Select(e => e.Kind == MatrixCatalogPageElementKind.Imagen
                ? e
                : new MatrixCatalogPageElement(
                    e.Zone,
                    e.Kind,
                    e.X,
                    e.Y,
                    e.Width,
                    e.Height,
                    MatrixCatalogTokenResolver.Resolver(e.Content, proyecto, plantilla, now),
                    e.Style,
                    e.Alignment,
                    e.ImageBytes,
                    e.ImageFileName,
                    e.ImageMimeType))
            .ToList();
    }

    /// <summary>
    /// Resuelve el título visible replicando el legacy GeneradorPdf/ExcelCatalogoMatrices:
    /// el medio reaplica <c>ObtenerTexto</c> sobre el texto candidato, así que cuando hay un
    /// <c>TextoTitulo</c> configurado se muestra SOLO ese texto (el sufijo del filtro
    /// "(APU)"/"(BÁSICOS)"/"(CUADRILLAS)" se descarta); sin configuración, el sufijo se
    /// conserva sobre el fallback "CATÁLOGO DE MATRICES". (Dictamen Oracle N7-18c.)
    /// </summary>
    private static string ResolverTitulo(string? filtroTitulo, MatrixCatalogTitleOptions? options)
    {
        if (!string.IsNullOrWhiteSpace(options?.Text))
            return options!.Text!;

        return DefaultFallbackTitle + ResolverSufijoFiltro(filtroTitulo);
    }

    /// <summary>
    /// Título documental (metadatos, <c>Info.Title</c> del PDF): el legacy
    /// (<c>GeneradorPdfCatalogoMatrices.ObtenerTituloCatalogo</c>) conserva el sufijo del
    /// filtro sobre el texto base (configurado o fallback) SIEMPRE, incluso cuando el título
    /// visible lo descarta. (Dictamen Oracle N7-18c, 2º NO-GO.)
    /// </summary>
    private static string ResolverTituloDocumental(string? filtroTitulo, MatrixCatalogTitleOptions? options)
    {
        var baseTitle = string.IsNullOrWhiteSpace(options?.Text) ? DefaultFallbackTitle : options!.Text!;
        return baseTitle + ResolverSufijoFiltro(filtroTitulo);
    }

    private static string ResolverSufijoFiltro(string? filtroTitulo) => filtroTitulo switch
    {
        "APU" => " (APU)",
        "Básicos" => " (BÁSICOS)",
        "Cuadrillas" => " (CUADRILLAS)",
        _ => string.Empty
    };

    private static MatrixCatalogTitleStyle BuildTitleStyle(MatrixCatalogTitleOptions? options)
    {
        var size = options?.Size is double s && s > 0 ? s : DefaultTitleSize;

        return new MatrixCatalogTitleStyle(
            FontName: !string.IsNullOrWhiteSpace(options?.FontName) ? options!.FontName! : DefaultFontName,
            Size: size,
            Bold: options?.Bold ?? DefaultTitleBold,
            Italic: options?.Italic ?? DefaultTitleItalic,
            TextColorHex: !string.IsNullOrWhiteSpace(options?.TextColorHex) ? options!.TextColorHex! : DefaultTextColorHex);
    }

    private static MatrixCatalogZoneSet ResolverFranja(
        MatrixCatalogZoneData izq,
        MatrixCatalogZoneData cen,
        MatrixCatalogZoneData der,
        MatrixCatalogProject proyecto,
        MatrixCatalogTemplate plantilla,
        DateTime now)
    {
        return new MatrixCatalogZoneSet(
            ResolverZona(izq, proyecto, plantilla, now),
            ResolverZona(cen, proyecto, plantilla, now),
            ResolverZona(der, proyecto, plantilla, now));
    }

    private static MatrixCatalogZone ResolverZona(MatrixCatalogZoneData data, MatrixCatalogProject proyecto, MatrixCatalogTemplate plantilla, DateTime now)
    {
        var kind = string.Equals(data.Tipo, "Imagen", StringComparison.OrdinalIgnoreCase)
            ? MatrixCatalogZoneKind.Imagen
            : MatrixCatalogZoneKind.Texto;

        var contenido = kind == MatrixCatalogZoneKind.Texto
            ? MatrixCatalogTokenResolver.Resolver(data.Contenido, proyecto, plantilla, now)
            : data.Contenido;

        return new MatrixCatalogZone(kind, contenido, BuildZoneStyle(data));
    }

    private static MatrixCatalogZoneStyle BuildZoneStyle(MatrixCatalogZoneData data)
    {
        return new MatrixCatalogZoneStyle(
            FontName: string.IsNullOrWhiteSpace(data.Fuente) ? DefaultFontName : data.Fuente,
            Size: data.Tamaño > 0 ? data.Tamaño : DefaultZoneSize,
            Bold: data.Negrita,
            Italic: data.Cursiva,
            ColorHex: DefaultZoneColorHex,
            Alignment: MapAlineacion(data.Alineacion));
    }

    private static MatrixCatalogTextAlignment MapAlineacion(string? alineacion) => alineacion switch
    {
        "Centro" => MatrixCatalogTextAlignment.Centro,
        "Derecha" => MatrixCatalogTextAlignment.Derecha,
        _ => MatrixCatalogTextAlignment.Izquierda
    };

    private static MatrixCatalogMatrix BuildMatriz(MatrixCatalogSourceMatrix src)
    {
        var totalMo = CalcularTotalMo(src.Componentes);
        var componentes = src.Componentes
            .OrderBy(c => c.Orden)
            .Select(c => BuildComponente(c, totalMo))
            .ToList();

        return new MatrixCatalogMatrix(
            src.Clave,
            src.Descripcion,
            src.Unidad,
            src.Kind,
            src.CostoDirecto,
            totalMo,
            componentes);
    }

    private static decimal CalcularTotalMo(IReadOnlyList<MatrixCatalogSourceComponent> componentes)
    {
        decimal totalMo = 0;

        foreach (var comp in componentes)
        {
            if (comp.Kind == MatrixCatalogComponentKind.ManoDeObra && !comp.EsPorcentajeMo)
                totalMo += comp.Cantidad * comp.CostoUnitario;
            else if (comp.Kind == MatrixCatalogComponentKind.Auxiliar && comp.EsCuadrillaAuxiliar)
                totalMo += comp.Cantidad * comp.CostoUnitario;
        }

        return totalMo;
    }

    private static MatrixCatalogComponent BuildComponente(MatrixCatalogSourceComponent src, decimal totalMo)
    {
        string prefijo = MapearPrefijo(src.Kind);

        decimal importe;
        decimal pesoBase;

        if (src.EsPorcentajeMo && (src.Kind == MatrixCatalogComponentKind.ManoDeObra || src.Kind == MatrixCatalogComponentKind.Herramienta))
        {
            importe = totalMo * src.Cantidad;
            pesoBase = totalMo;
        }
        else
        {
            importe = src.Cantidad * src.CostoUnitario;
            pesoBase = src.CostoUnitario;
        }

        return new MatrixCatalogComponent(
            src.Kind,
            prefijo,
            src.Clave,
            src.Descripcion,
            src.Unidad,
            src.Cantidad,
            pesoBase,
            importe);
    }

    private static string MapearPrefijo(MatrixCatalogComponentKind kind) => kind switch
    {
        MatrixCatalogComponentKind.Material => "M",
        MatrixCatalogComponentKind.Maquinaria => "H",
        MatrixCatalogComponentKind.Herramienta => "H",
        MatrixCatalogComponentKind.Auxiliar => "+",
        _ => ""
    };
}