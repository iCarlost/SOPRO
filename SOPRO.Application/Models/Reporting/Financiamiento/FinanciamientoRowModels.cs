namespace SOPRO.Application.Models.Reporting.Financiamiento;

/// <summary>
/// Fila base neutral del programa de obra para el reporte de financiamiento:
/// costo directo e indirecto de un período. Reemplaza el tipo anidado
/// <c>BaseRowInfo</c> que vivía dentro del generador PDF y desacopla PDF/Excel de
/// cualquier estructura de la UI o del grid.
/// </summary>
/// <param name="NumeroPeriodo">Número de período del programa (clave de alineación).</param>
/// <param name="CostoDirecto">Costo directo del período.</param>
/// <param name="CostoIndirecto">Costo indirecto del período.</param>
public sealed record FinanciamientoBaseRow(
    int NumeroPeriodo,
    decimal CostoDirecto,
    decimal CostoIndirecto);

/// <summary>
/// Rol de formato neutral de un valor del reporte. La capa de reportes
/// (<c>SOPRO.Reporting.Formatting.ReportColumnGridFormat</c>) traduce este rol a
/// la cadena de formato concreta (símbolo de moneda <c>$</c>, decimales de
/// cantidad/importe/porcentaje del proyecto), de modo que PDF y Excel comparten
/// UNA sola regla.
/// </summary>
public enum FinanciamientoValorFormato
{
    /// <summary>Texto ya resuelto (sin formato numérico).</summary>
    Texto = 0,

    /// <summary>Importe monetario (símbolo <c>$</c> + decimales de importe).</summary>
    Moneda = 1,

    /// <summary>Porcentaje (valor fraccionario, decimales de porcentaje, sufijo '%').</summary>
    Porcentaje = 2,

    /// <summary>Cantidad (decimales de cantidad del proyecto).</summary>
    Cantidad = 3,

    /// <summary>Entero sin separadores (p. ej. desfase de cobro en períodos).</summary>
    Entero = 4,
}

/// <summary>
/// Valor neutral de una celda del reporte: o bien un número con su rol de formato,
/// o bien un texto ya resuelto. Es deliberadamente independiente de MigraDoc y de
/// ClosedXML; cada renderizador decide cómo materializarlo.
/// </summary>
public sealed record FinanciamientoValor
{
    private FinanciamientoValor(decimal? numero, string texto, FinanciamientoValorFormato formato)
    {
        Numero = numero;
        Texto = texto;
        Formato = formato;
    }

    /// <summary>Valor numérico, si el valor es numérico; en caso contrario <c>null</c>.</summary>
    public decimal? Numero { get; }

    /// <summary>Texto ya resuelto, si el valor es textual; en caso contrario vacío.</summary>
    public string Texto { get; }

    /// <summary>Rol de formato del valor.</summary>
    public FinanciamientoValorFormato Formato { get; }

    /// <summary>Indica si el valor es numérico (tiene <see cref="Numero"/>).</summary>
    public bool EsNumerico => Numero.HasValue;

    /// <summary>Crea un valor numérico con su rol de formato.</summary>
    public static FinanciamientoValor Numerico(decimal valor, FinanciamientoValorFormato formato)
        => new(valor, string.Empty, formato);

    /// <summary>Crea un valor textual ya resuelto.</summary>
    public static FinanciamientoValor Textual(string? texto)
        => new(null, texto ?? string.Empty, FinanciamientoValorFormato.Texto);
}

/// <summary>
/// Fila del bloque de datos del encabezado del reporte (etiqueta/valor a la
/// izquierda y etiqueta/valor a la derecha), con la misma forma en PDF y Excel.
/// </summary>
public sealed record FinanciamientoDatoRow(
    string EtiquetaIzquierda,
    FinanciamientoValor ValorIzquierda,
    string EtiquetaDerecha,
    string SubEtiquetaDerecha,
    FinanciamientoValor ValorDerecha);

/// <summary>Tipo de fila de la matriz de financiamiento.</summary>
public enum FinanciamientoMatrixRowKind
{
    /// <summary>Fila de valores (un valor por período).</summary>
    Valores = 0,

    /// <summary>Fila de título de sección (INGRESOS, EGRESOS).</summary>
    Seccion = 1,

    /// <summary>Fila espaciadora.</summary>
    Espaciador = 2,
}

/// <summary>
/// Fila neutral de la matriz de financiamiento. Las filas de valores llevan un
/// valor por período, el rol de formato y los identificadores de columna del
/// snapshot que gobiernan el estilo del concepto y de las celdas de valor.
/// </summary>
/// <param name="Kind">Tipo de fila.</param>
/// <param name="Concepto">Texto del concepto (fila de valores/sección).</param>
/// <param name="Valores">Valores por período (vacío en secciones/espaciadores).</param>
/// <param name="Formato">Rol de formato de los valores.</param>
/// <param name="ColumnaConcepto">Identificador del snapshot para el estilo del concepto.</param>
/// <param name="ColumnaValor">Identificador del snapshot para el estilo de las celdas de valor.</param>
public sealed record FinanciamientoMatrixRow(
    FinanciamientoMatrixRowKind Kind,
    string Concepto,
    IReadOnlyList<decimal> Valores,
    FinanciamientoValorFormato Formato,
    string ColumnaConcepto,
    string ColumnaValor)
{
    /// <summary>Indica si la fila es una fila de valores.</summary>
    public bool EsValores => Kind == FinanciamientoMatrixRowKind.Valores;

    /// <summary>Crea una fila de valores.</summary>
    public static FinanciamientoMatrixRow FilaValores(
        string concepto,
        IReadOnlyList<decimal> valores,
        FinanciamientoValorFormato formato,
        string columnaConcepto,
        string columnaValor)
        => new(FinanciamientoMatrixRowKind.Valores, concepto, valores, formato, columnaConcepto, columnaValor);

    /// <summary>Crea una fila de sección.</summary>
    public static FinanciamientoMatrixRow Seccion(string titulo, string columnaConcepto = "colPeriodo")
        => new(FinanciamientoMatrixRowKind.Seccion, titulo, Array.Empty<decimal>(),
            FinanciamientoValorFormato.Texto, columnaConcepto, columnaConcepto);

    /// <summary>Crea una fila espaciadora.</summary>
    public static FinanciamientoMatrixRow Espaciador(string columnaConcepto = "colPeriodo")
        => new(FinanciamientoMatrixRowKind.Espaciador, string.Empty, Array.Empty<decimal>(),
            FinanciamientoValorFormato.Texto, columnaConcepto, columnaConcepto);
}

/// <summary>
/// Modelo neutral y completo del reporte de financiamiento: etiquetas de período,
/// bloque de datos del encabezado, filas de la matriz y resultado. Se construye en
/// memoria (sin EF, sin UI) y lo consumen PDF y Excel, garantizando paridad de
/// filas base/períodos y de formatos entre ambos medios.
/// </summary>
/// <param name="EtiquetasPeriodo">Etiqueta de cada período (columna de la matriz).</param>
/// <param name="Datos">Filas del bloque de datos del encabezado.</param>
/// <param name="Matriz">Filas de la matriz (valores, secciones y espaciadores).</param>
/// <param name="PorcentajeCalculado">Porcentaje de financiamiento calculado.</param>
public sealed record FinanciamientoReportModel(
    IReadOnlyList<string> EtiquetasPeriodo,
    IReadOnlyList<FinanciamientoDatoRow> Datos,
    IReadOnlyList<FinanciamientoMatrixRow> Matriz,
    decimal PorcentajeCalculado);
