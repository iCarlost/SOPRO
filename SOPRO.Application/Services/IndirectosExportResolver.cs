using System.Globalization;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Resolución de valores de exportación (PDF/Excel) del reporte de Cálculo de
/// Indirectos desde las entidades de dominio <see cref="GrupoIndirecto"/>/
/// <see cref="ConceptoIndirecto"/>. Vive en Application para poder probarse sin
/// formularios: los generadores de la UI solo renderizan.
///
/// Es también el punto único que construye el snapshot neutral de columnas
/// (<see cref="ReportColumnSnapshot"/>) del reporte de indirectos, para que PDF y
/// Excel consuman exactamente la misma lista, orden, anchos, formatos y estilos.
///
/// La resolución de formato NUMÉRICO (moneda <c>$</c>, decimales de importe/
/// cantidad/porcentaje) NO vive aquí: es responsabilidad de la capa de reportes
/// (<c>SOPRO.Reporting.Formatting.ReportColumnGridFormat</c>, único punto del
/// símbolo de moneda) porque Application no referencia Reporting. Este resolver
/// aporta el TEXTO de las columnas de grupo/duración y las señales de rol
/// (importe mensual, importe total, grupo) que ambos renderizadores comparten,
/// además del valor crudo de las columnas numéricas del dominio.
/// </summary>
public static class IndirectosExportResolver
{
    /// <summary>Identificador de la columna jerárquica Grupo/Concepto.</summary>
    public const string IdentificadorGrupo = "Grupo";

    /// <summary>Identificador de la columna de importe mensual.</summary>
    public const string IdentificadorImporteMensual = "ImporteMensual";

    /// <summary>Identificador de la columna de duración en meses.</summary>
    public const string IdentificadorDuracion = "Duracion";

    /// <summary>Identificador de la columna de importe total.</summary>
    public const string IdentificadorImporteTotal = "ImporteTotal";

    /// <summary>
    /// Columnas por defecto cuando no hay proyecto ni configuración persistida.
    /// Se expresan como <see cref="ColumnaIndirectos"/> para que viajen por el
    /// mismo mapper neutral que las columnas reales.
    /// </summary>
    private static readonly IReadOnlyList<ColumnaIndirectos> ColumnasPredeterminadas = new List<ColumnaIndirectos>
    {
        new() { Nombre = "Grupo / Concepto",  NombreInterno = "Grupo",          Visible = true, Orden = 1, AnchoColumna = 400, Alineacion = AlineacionColumna.Izquierda },
        new() { Nombre = "Importe Mensual $", NombreInterno = "ImporteMensual", Visible = true, Orden = 2, AnchoColumna = 180, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "N2" },
        new() { Nombre = "Duración (Meses)",  NombreInterno = "Duracion",       Visible = true, Orden = 3, AnchoColumna = 150, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Importe Total $",   NombreInterno = "ImporteTotal",   Visible = true, Orden = 4, AnchoColumna = 180, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "N2" },
    };

    /// <summary>
    /// Construye el snapshot neutral de columnas del reporte de indirectos. Es la
    /// ÚNICA fuente que consumen PDF y Excel (misma visibilidad/orden/ancho/formato
    /// y mismos decimales del proyecto, para paridad grid↔export). Si no llegan
    /// columnas se usan las predeterminadas.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de indirectos persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad del proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe del proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje del proyecto (por defecto 4).</param>
    public static ReportColumnSnapshot BuildSnapshot(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaIndirectos>? columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var fuente = columnas?.Where(c => c != null).ToList();
        if (fuente == null || fuente.Count == 0)
            fuente = ColumnasPredeterminadas.ToList();

        return new IndirectosReportSnapshotBuilder().Build(
            proyectoId, titulo, fuente, decimalesCantidad, decimalesImporte, decimalesPorcentaje);
    }

    /// <summary>
    /// Columnas neutrales por defecto (sin proyecto/configuración), ya filtradas
    /// de columnas internas y ordenadas por <c>Orden</c>.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearIndirectos)
            .ToList();

    /// <summary>Indica si la columna es el rótulo jerárquico Grupo/Concepto.</summary>
    public static bool EsColumnaGrupo(ReportColumnDefinition columna)
        => EsIdentificador(columna, IdentificadorGrupo);

    /// <summary>Indica si la columna es el Importe Mensual.</summary>
    public static bool EsColumnaImporteMensual(ReportColumnDefinition columna)
        => EsIdentificador(columna, IdentificadorImporteMensual);

    /// <summary>Indica si la columna es la Duración (meses).</summary>
    public static bool EsColumnaDuracion(ReportColumnDefinition columna)
        => EsIdentificador(columna, IdentificadorDuracion);

    /// <summary>Indica si la columna es el Importe Total.</summary>
    public static bool EsColumnaImporteTotal(ReportColumnDefinition columna)
        => EsIdentificador(columna, IdentificadorImporteTotal);

    /// <summary>
    /// Obtiene el valor numérico crudo de una fila de GRUPO para la columna dada.
    /// Devuelve <c>false</c> para las columnas de texto (Grupo) o no aplicables.
    /// </summary>
    public static bool TryGetValorGrupo(GrupoIndirecto grupo, ReportColumnDefinition columna, out decimal valor)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        ArgumentNullException.ThrowIfNull(columna);

        if (EsColumnaImporteTotal(columna))
        {
            valor = grupo.Total;
            return true;
        }

        valor = 0m;
        return false;
    }

    /// <summary>
    /// Obtiene el valor numérico crudo de una fila de CONCEPTO para la columna
    /// dada. Devuelve <c>false</c> para las columnas de texto (Grupo, Duración) o
    /// no aplicables.
    /// </summary>
    public static bool TryGetValorConcepto(ConceptoIndirecto concepto, ReportColumnDefinition columna, out decimal valor)
    {
        ArgumentNullException.ThrowIfNull(concepto);
        ArgumentNullException.ThrowIfNull(columna);

        switch (columna.Identificador)
        {
            case IdentificadorImporteMensual:
                valor = concepto.ImporteMensual;
                return true;
            case IdentificadorImporteTotal:
                valor = concepto.ImporteTotal;
                return true;
            default:
                valor = 0m;
                return false;
        }
    }

    /// <summary>
    /// Resuelve el TEXTO de una celda de fila de GRUPO: el nombre del grupo en la
    /// columna jerárquica; vacío en cualquier otra (las numéricas se formatean con
    /// <c>ReportColumnGridFormat</c> en el renderizador).
    /// </summary>
    public static string ResolveTextoGrupo(GrupoIndirecto grupo, ReportColumnDefinition columna)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        ArgumentNullException.ThrowIfNull(columna);

        return EsColumnaGrupo(columna) ? grupo.Nombre ?? string.Empty : string.Empty;
    }

    /// <summary>
    /// Resuelve el TEXTO de una celda de fila de CONCEPTO: nombre del concepto
    /// indentado en la columna jerárquica y los meses (enteros, sin decimales) en
    /// la columna Duración cuando la sección los muestra. El resto es vacío.
    /// </summary>
    /// <param name="concepto">Concepto de dominio.</param>
    /// <param name="columna">Columna neutral.</param>
    /// <param name="mostrarDuracion">True en la sección de Campo.</param>
    public static string ResolveTextoConcepto(ConceptoIndirecto concepto, ReportColumnDefinition columna, bool mostrarDuracion)
    {
        ArgumentNullException.ThrowIfNull(concepto);
        ArgumentNullException.ThrowIfNull(columna);

        if (EsColumnaGrupo(columna))
            return "    " + (concepto.Concepto ?? string.Empty);

        if (EsColumnaDuracion(columna))
            return mostrarDuracion ? concepto.DuracionMeses.ToString(CultureInfo.InvariantCulture) : string.Empty;

        return string.Empty;
    }

    /// <summary>
    /// Resuelve el color de fondo (hex) de una celda respetando el color por
    /// columna definido en la entidad y, en su defecto, el bandeado de la tabla.
    /// Es neutral (no depende de UI) para que PDF y Excel compartan la regla.
    /// </summary>
    public static string ResolveCellBackground(ReportColumnDefinition columna, ReportTableStyle tabla, bool esFilaAlterna)
    {
        ArgumentNullException.ThrowIfNull(columna);
        ArgumentNullException.ThrowIfNull(tabla);

        var porColumna = columna.EstiloContenido.ColorFondo;
        var porDefectoTabla = tabla.EstiloContenido.ColorFondo ?? "#FFFFFF";
        var esOverrideColumna = !string.IsNullOrWhiteSpace(porColumna)
            && !string.Equals(porColumna, porDefectoTabla, StringComparison.OrdinalIgnoreCase);

        if (esOverrideColumna)
            return porColumna!;

        if (esFilaAlterna && !string.IsNullOrWhiteSpace(tabla.FilaAlterna.ColorFondoAlterno))
            return tabla.FilaAlterna.ColorFondoAlterno!;

        return string.IsNullOrWhiteSpace(porColumna) ? porDefectoTabla : porColumna!;
    }

    private static bool EsIdentificador(ReportColumnDefinition? columna, string identificador)
        => columna != null
           && string.Equals(columna.Identificador, identificador, StringComparison.OrdinalIgnoreCase);
}
