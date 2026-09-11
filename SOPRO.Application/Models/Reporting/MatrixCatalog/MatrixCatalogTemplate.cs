namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Snapshot neutral de la plantilla de reporte: zonas de encabezado/pie (con su
/// estilo), campos dinámicos del proyecto, elementos libres del diseñador PDF y
/// alturas de franja. Datos puros, sin entidades de EF.
///
/// Los elementos libres se materializan ya ordenados por (ZOrder, Id) legacy;
/// ese orden es definitivo (el renderer no vuelve a ordenar). Sus tokens aún no
/// están resueltos: el builder los resuelve con el reloj explícito.
/// </summary>
internal sealed record MatrixCatalogTemplate(
    MatrixCatalogZoneData EncabezadoIzq,
    MatrixCatalogZoneData EncabezadoCen,
    MatrixCatalogZoneData EncabezadoDer,
    MatrixCatalogZoneData PieIzq,
    MatrixCatalogZoneData PieCen,
    MatrixCatalogZoneData PieDer,
    string CampoElabaro,
    string CampoReviso,
    string CampoAutorizo,
    string CampoDependencia,
    string CampoNumeroContrato,
    string CampoLicitacion,
    string CampoUbicacion,
    string CampoFechaInicio,
    string CampoFechaTermino,
    string CampoTextoLibre1,
    string CampoTextoLibre2,
    IReadOnlyList<MatrixCatalogPageElement>? ElementosEncabezado = null,
    IReadOnlyList<MatrixCatalogPageElement>? ElementosPie = null,
    MatrixCatalogPageHeights? Heights = null)
{
    public static MatrixCatalogTemplate Vacia { get; } = new(
        EncabezadoIzq: MatrixCatalogZoneData.Vacia,
        EncabezadoCen: MatrixCatalogZoneData.Vacia,
        EncabezadoDer: MatrixCatalogZoneData.Vacia,
        PieIzq: MatrixCatalogZoneData.Vacia,
        PieCen: MatrixCatalogZoneData.Vacia,
        PieDer: MatrixCatalogZoneData.Vacia,
        CampoElabaro: "", CampoReviso: "", CampoAutorizo: "", CampoDependencia: "",
        CampoNumeroContrato: "", CampoLicitacion: "", CampoUbicacion: "",
        CampoFechaInicio: "", CampoFechaTermino: "", CampoTextoLibre1: "",
        CampoTextoLibre2: "",
        ElementosEncabezado: Array.Empty<MatrixCatalogPageElement>(),
        ElementosPie: Array.Empty<MatrixCatalogPageElement>(),
        Heights: MatrixCatalogPageHeights.Default);
}

/// <summary>
/// Una zona de la plantilla con su contenido crudo y estilo.
/// <c>Tipo</c> usa los valores legacy ("Texto"/"Imagen").
/// </summary>
internal sealed record MatrixCatalogZoneData(
    string Tipo,
    string Contenido,
    string Fuente,
    float Tamaño,
    bool Negrita,
    bool Cursiva,
    string Alineacion)
{
    public static MatrixCatalogZoneData Vacia { get; } = new(
        Tipo: "Texto", Contenido: "", Fuente: "Segoe UI", Tamaño: 9f,
        Negrita: false, Cursiva: false, Alineacion: "Izquierda");
}