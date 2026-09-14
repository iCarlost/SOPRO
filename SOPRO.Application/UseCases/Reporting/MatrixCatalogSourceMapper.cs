using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Proyección pura de entidades de EF a snapshots neutrales. La carga (EF,
/// AsNoTracking) y la proyección están separadas: ambas se prueban de forma
/// independiente. El builder nunca ve entidades de Core.
/// </summary>
internal static class MatrixCatalogSourceMapper
{
    public static MatrixCatalogMatrixKind MapearTipo(TipoMatriz tipo) => tipo switch
    {
        TipoMatriz.Basico => MatrixCatalogMatrixKind.Basico,
        TipoMatriz.Cuadrilla => MatrixCatalogMatrixKind.Cuadrilla,
        _ => MatrixCatalogMatrixKind.Apu
    };

    public static MatrixCatalogComponentKind MapearTipoComponente(TipoComponenteMatriz tipo) => tipo switch
    {
        TipoComponenteMatriz.ManoDeObra => MatrixCatalogComponentKind.ManoDeObra,
        TipoComponenteMatriz.Maquinaria => MatrixCatalogComponentKind.Maquinaria,
        TipoComponenteMatriz.Auxiliar => MatrixCatalogComponentKind.Auxiliar,
        TipoComponenteMatriz.Herramienta => MatrixCatalogComponentKind.Herramienta,
        _ => MatrixCatalogComponentKind.Material
    };

    public static MatrixCatalogSourceComponent MapearComponente(ComponenteMatriz comp)
    {
        var kind = MapearTipoComponente(comp.TipoComponente);
        var (clave, descripcion, unidad, cu, esPct, esCuadrilla) = comp.TipoComponente switch
        {
            TipoComponenteMatriz.Material => (
                comp.Material?.Clave ?? "",
                comp.Material?.Descripcion ?? "",
                comp.Material?.Unidad ?? "",
                comp.Material?.PrecioUnitario ?? 0m,
                false,
                false),
            TipoComponenteMatriz.ManoDeObra => (
                comp.ManoDeObra?.Clave ?? "",
                comp.ManoDeObra?.Descripcion ?? "",
                "jor",
                comp.ManoDeObra?.SalarioReal ?? 0m,
                comp.ManoDeObra?.EsPorcentajeMO == true,
                false),
            TipoComponenteMatriz.Maquinaria => (
                comp.Maquinaria?.Clave ?? "",
                comp.Maquinaria?.Descripcion ?? "",
                "hora",
                comp.Maquinaria?.CostoHorario ?? 0m,
                false,
                false),
            TipoComponenteMatriz.Herramienta => (
                comp.Herramienta?.Clave ?? "",
                comp.Herramienta?.Descripcion ?? "",
                comp.Herramienta?.Unidad ?? "%",
                comp.Herramienta?.PrecioUnitario ?? 0m,
                comp.Herramienta?.EsPorcentajeMO == true,
                false),
            TipoComponenteMatriz.Auxiliar => (
                comp.Auxiliar?.Clave ?? "",
                comp.Auxiliar?.Descripcion ?? "",
                comp.Auxiliar?.Unidad ?? "",
                comp.Auxiliar?.CostoDirecto ?? 0m,
                false,
                comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla),
            _ => ("", "", "", 0m, false, false)
        };

        return new MatrixCatalogSourceComponent(
            kind,
            comp.Orden,
            clave,
            descripcion,
            unidad,
            comp.Cantidad,
            cu,
            esPct,
            esCuadrilla);
    }

    public static MatrixCatalogSourceMatrix MapearMatriz(Matriz m) => new(
        m.Clave ?? "",
        m.Descripcion ?? "",
        m.Unidad ?? "",
        MapearTipo(m.Tipo),
        m.CostoDirecto,
        m.Componentes.OrderBy(c => c.Orden).Select(MapearComponente).ToList());

    public static MatrixCatalogProject MapearProyecto(Proyecto? p) => new(
        p?.Nombre ?? "",
        p?.Descripcion ?? "",
        p?.Ubicacion ?? "",
        p?.Convocante ?? "",
        p?.Contratista ?? "",
        p?.ApoderadoLegal ?? "",
        p?.FechaInicio,
        p?.FechaTermino,
        p?.PlazoEjecucion);

    public static MatrixCatalogTemplate MapearPlantilla(PlantillaReporte? t, IEnumerable<PlantillaReporteElemento>? elementos = null)
    {
        if (t == null) return MatrixCatalogTemplate.Vacia;

        var (encabezado, pie) = MapearElementosLibres(elementos);

        return new MatrixCatalogTemplate(
            Zona(t.EncabezadoIzqTipo, t.EncabezadoIzqContenido, t.EncabezadoIzqFuente, t.EncabezadoIzqTamaño, t.EncabezadoIzqNegrita, t.EncabezadoIzqCursiva, t.EncabezadoIzqAlineacion),
            Zona(t.EncabezadoCenTipo, t.EncabezadoCenContenido, t.EncabezadoCenFuente, t.EncabezadoCenTamaño, t.EncabezadoCenNegrita, t.EncabezadoCenCursiva, t.EncabezadoCenAlineacion),
            Zona(t.EncabezadoDerTipo, t.EncabezadoDerContenido, t.EncabezadoDerFuente, t.EncabezadoDerTamaño, t.EncabezadoDerNegrita, t.EncabezadoDerCursiva, t.EncabezadoDerAlineacion),
            Zona(t.PiePaginaIzqTipo, t.PiePaginaIzqContenido, t.PiePaginaIzqFuente, t.PiePaginaIzqTamaño, t.PiePaginaIzqNegrita, t.PiePaginaIzqCursiva, t.PiePaginaIzqAlineacion),
            Zona(t.PiePaginaCenTipo, t.PiePaginaCenContenido, t.PiePaginaCenFuente, t.PiePaginaCenTamaño, t.PiePaginaCenNegrita, t.PiePaginaCenCursiva, t.PiePaginaCenAlineacion),
            Zona(t.PiePaginaDerTipo, t.PiePaginaDerContenido, t.PiePaginaDerFuente, t.PiePaginaDerTamaño, t.PiePaginaDerNegrita, t.PiePaginaDerCursiva, t.PiePaginaDerAlineacion),
            t.CampoElabaro ?? "",
            t.CampoReviso ?? "",
            t.CampoAutorizo ?? "",
            t.CampoDependencia ?? "",
            t.CampoNumeroContrato ?? "",
            t.CampoLicitacion ?? "",
            t.CampoUbicacion ?? "",
            t.CampoFechaInicio ?? "",
            t.CampoFechaTermino ?? "",
            t.CampoTextoLibre1 ?? "",
            t.CampoTextoLibre2 ?? "",
            encabezado,
            pie,
            new MatrixCatalogPageHeights(
                t.EncabezadoAltura,
                t.AlturaEncabezadoDmm,
                t.PiePaginaAltura,
                t.AlturaPieDmm));
    }

    /// <summary>
    /// Proyección de los elementos libres del diseñador PDF. Se materializan
    /// SIEMPRE ordenados por (ZOrder, Id) legacy y separados por zona; ese orden
    /// es definitivo (el renderer no vuelve a ordenarlos ni ve los IDs).
    /// Los tokens NO se resuelven aquí: los resuelve el builder con el reloj.
    /// </summary>
    private static (IReadOnlyList<MatrixCatalogPageElement> Encabezado, IReadOnlyList<MatrixCatalogPageElement> Pie) MapearElementosLibres(
        IEnumerable<PlantillaReporteElemento>? elementos)
    {
        var ordenados = (elementos ?? Enumerable.Empty<PlantillaReporteElemento>())
            .OrderBy(e => e.ZOrder)
            .ThenBy(e => e.Id)
            .ToList();

        var encabezado = Array.AsReadOnly(ordenados
            .Where(e => !EsPieDePagina(e))
            .Select(MapearElementoLibre)
            .ToArray());
        var pie = Array.AsReadOnly(ordenados
            .Where(EsPieDePagina)
            .Select(MapearElementoLibre)
            .ToArray());

        return (encabezado, pie);
    }

    private static bool EsPieDePagina(PlantillaReporteElemento e)
        => string.Equals(e.Zona, "PieDePagina", StringComparison.OrdinalIgnoreCase);

    private static MatrixCatalogPageElement MapearElementoLibre(PlantillaReporteElemento e) => new(
        zone: EsPieDePagina(e) ? MatrixCatalogPageZone.PieDePagina : MatrixCatalogPageZone.Encabezado,
        kind: e.Tipo?.ToUpperInvariant() switch
        {
            "IMAGEN" => MatrixCatalogPageElementKind.Imagen,
            "ETIQUETADINAMICA" => MatrixCatalogPageElementKind.EtiquetaDinamica,
            _ => MatrixCatalogPageElementKind.TextoLibre
        },
        x: e.X,
        y: e.Y,
        width: e.Ancho,
        height: e.Alto,
        content: e.Contenido ?? "",
        style: new MatrixCatalogPageElementStyle(
            FontName: string.IsNullOrWhiteSpace(e.Fuente) ? "Segoe UI" : e.Fuente,
            Size: e.TamanoFuente,
            Bold: e.Negrita,
            Italic: e.Cursiva,
            ColorHex: e.ColorTextoHex ?? ""),
        alignment: e.Alineacion ?? "MiddleLeft",
        imageBytes: e.ImagenBytes,
        imageFileName: e.ImagenNombreOrigen ?? "",
        imageMimeType: e.ImagenMimeType ?? "");

    private static MatrixCatalogZoneData Zona(string tipo, string contenido, string fuente, float tamaño, bool negrita, bool cursiva, string alineacion)
        => new(
            Tipo: tipo ?? "Texto",
            Contenido: contenido ?? "",
            Fuente: string.IsNullOrWhiteSpace(fuente) ? "Segoe UI" : fuente,
            Tamaño: tamaño,
            Negrita: negrita,
            Cursiva: cursiva,
            Alineacion: string.IsNullOrEmpty(alineacion) ? "Izquierda" : alineacion);
}
