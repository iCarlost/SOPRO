namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Definición neutral de una columna de reporte. Es el contrato compartido entre
/// los reportes (Presupuesto, Catálogo de Materiales, ...) y no depende de
/// ninguna tecnología de UI, persistencia o renderizado.
/// </summary>
/// <param name="Identificador">Identificador estable de la columna (ej. "Descripcion", "ImporteTotal").</param>
/// <param name="Encabezado">Texto visible del encabezado.</param>
/// <param name="Visible">Si la columna se muestra en el reporte.</param>
/// <param name="Orden">Posición de la columna (menor = más a la izquierda).</param>
/// <param name="Ancho">Ancho en píxeles.</param>
/// <param name="EstiloEncabezado">Estilo del encabezado.</param>
/// <param name="EstiloContenido">Estilo del contenido.</param>
/// <param name="Alineacion">Alineación horizontal.</param>
/// <param name="AlineacionVertical">Alineación vertical.</param>
/// <param name="Wrap">Ajuste de texto en varias líneas.</param>
/// <param name="FormatoNumerico">Formato numérico legacy ("N2", "C2", "C4", "P2", ...).</param>
/// <param name="EsNumerica">Si la columna contiene valores numéricos.</param>
public sealed record ReportColumnDefinition(
    string Identificador,
    string Encabezado,
    bool Visible,
    int Orden,
    int Ancho,
    ReportTextStyle EstiloEncabezado,
    ReportTextStyle EstiloContenido,
    ReportTextAlignment Alineacion,
    ReportVerticalAlignment AlineacionVertical,
    bool Wrap,
    string FormatoNumerico,
    bool EsNumerica)
{
    /// <summary>
    /// Indica si la columna es monetaria (TipoDato <c>Moneda</c> o token de formato
    /// que empieza por "C"). El renderizador de Presupuesto la formatea con la misma
    /// semántica del grid: símbolo de moneda de la cultura actual y los decimales de
    /// importe del proyecto. Por defecto, no monetaria.
    /// </summary>
    public bool EsMoneda { get; init; }
}

/// <summary>
/// Snapshot inmutable del contrato de columnas: definiciones ordenadas, estilo de
/// tabla y metadatos mínimos del reporte. Se construye desde configuración
/// persistida pura; no toca grids ni escribe configuración.
/// </summary>
/// <param name="TipoReporte">Tipo de reporte (ej. "Presupuesto", "Materiales").</param>
/// <param name="ProyectoId">Proyecto al que pertenece el reporte.</param>
/// <param name="Titulo">Título visible del reporte, si aplica.</param>
/// <param name="Columnas">Definiciones de columna ordenadas.</param>
/// <param name="EstiloTabla">Estilo neutral de la tabla.</param>
public sealed record ReportColumnSnapshot(
    string TipoReporte,
    int ProyectoId,
    string Titulo,
    IReadOnlyList<ReportColumnDefinition> Columnas,
    ReportTableStyle EstiloTabla)
{
    /// <summary>Decimales de cantidad configurados en el proyecto (por defecto 2).</summary>
    public int DecimalesCantidad { get; init; } = 2;

    /// <summary>Decimales de importe configurados en el proyecto (por defecto 2).</summary>
    public int DecimalesImporte { get; init; } = 2;

    /// <summary>Decimales de porcentaje configurados en el proyecto (por defecto 4).</summary>
    public int DecimalesPorcentaje { get; init; } = 4;
}
