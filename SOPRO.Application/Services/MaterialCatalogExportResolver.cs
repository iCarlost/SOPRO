using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Resolución de valores de exportación (PDF/Excel) de un material desde su
/// DTO de presentación <see cref="MaterialListItem"/>. Vive en Application para
/// poder probarse sin formularios: el generador de PDF de la UI solo renderiza.
///
/// En esta fase también es el punto único que construye el snapshot neutral de
/// columnas (<see cref="ReportColumnSnapshot"/>) del catálogo de materiales, de
/// modo que PDF y Excel consuman exactamente la misma lista, orden, anchos,
/// formatos y estilos.
/// </summary>
public static class MaterialCatalogExportResolver
{
    /// <summary>Formato numérico de último recurso cuando la columna no define uno.</summary>
    public const string FormatoNumericoPredeterminado = "#,##0.0000";

    /// <summary>
    /// Columnas por defecto cuando no hay proyecto ni configuración persistida.
    /// Se expresan como <see cref="ColumnaMaterial"/> para que viajen por el mismo
    /// mapper neutral que las columnas reales.
    /// </summary>
    private static readonly IReadOnlyList<ColumnaMaterial> ColumnasPredeterminadas = new List<ColumnaMaterial>
    {
        new() { Nombre = "Clave", NombreInterno = "Clave", Visible = true, Orden = 1, AnchoColumna = 110, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Descripción", NombreInterno = "Descripcion", Visible = true, Orden = 2, AnchoColumna = 300, Alineacion = AlineacionColumna.Izquierda, WrapTexto = true },
        new() { Nombre = "Unidad", NombreInterno = "Unidad", Visible = true, Orden = 3, AnchoColumna = 80, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Precio Unitario", NombreInterno = "PrecioUnitario", Visible = true, Orden = 4, AnchoColumna = 140, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = FormatoNumericoPredeterminado },
        new() { Nombre = "Origen", NombreInterno = "Origen", Visible = true, Orden = 5, AnchoColumna = 80, Alineacion = AlineacionColumna.Centro },
    };

    /// <summary>
    /// Construye el snapshot neutral de columnas del catálogo de materiales. Es la
    /// ÚNICA fuente que consumen PDF y Excel (misma visibilidad/orden/ancho/formato).
    /// Si no llegan columnas se usan las predeterminadas.
    /// </summary>
    public static ReportColumnSnapshot BuildSnapshot(int proyectoId, string? titulo, IEnumerable<ColumnaMaterial>? columnas)
    {
        var fuente = columnas?.Where(c => c != null).ToList();
        if (fuente == null || fuente.Count == 0)
            fuente = ColumnasPredeterminadas.ToList();

        return new MaterialesReportSnapshotBuilder().Build(proyectoId, titulo, fuente);
    }

    /// <summary>
    /// Columnas neutrales por defecto (sin proyecto/configuración), ya filtradas
    /// de columnas internas y ordenadas por <c>Orden</c>.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearMaterial)
            .ToList();

    /// <summary>
    /// Resuelve el valor de una celda para una columna neutral. El formato
    /// numérico configurado en la columna (<c>N2</c>, <c>C4</c>, <c>#,##0.0000</c>, ...)
    /// se traduce por la ruta central <see cref="ReportNumberFormatMap"/> antes de
    /// render. Esta ruta es la del PDF, así que usa
    /// <see cref="ReportNumberFormatMap.ToPdfFormat"/> con
    /// <see cref="CultureInfo.InvariantCulture"/> (fidelidad legacy: los tokens
    /// monetarios <c>C0/C1/C3/C4</c> se mapean a su equivalente numérico sin símbolo,
    /// evitando el glifo ¤ de la cultura invariante; <c>C2</c> conserva '$').
    /// </summary>
    public static string ResolveValue(MaterialListItem material, ReportColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(column);

        return column.Identificador switch
        {
            "Clave" => material.Clave ?? string.Empty,
            "Descripcion" => material.Descripcion ?? string.Empty,
            "Unidad" => material.Unidad ?? string.Empty,
            "PrecioUnitario" => FormatearNumero(material.PrecioUnitario, column.FormatoNumerico),
            "Origen" => ResolveOrigin(material),
            _ => string.Empty
        };
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

    /// <summary>
    /// Compatibilidad legacy: resuelve el valor desde la entidad <see cref="ColumnaMaterial"/>.
    /// </summary>
    public static string ResolveValue(MaterialListItem material, ColumnaMaterial column)
    {
        return column.NombreInterno switch
        {
            "Clave" => material.Clave ?? string.Empty,
            "Descripcion" => material.Descripcion ?? string.Empty,
            "Unidad" => material.Unidad ?? string.Empty,
            "PrecioUnitario" => material.PrecioUnitario.ToString("#,##0.0000"),
            "Origen" => ResolveOrigin(material),
            _ => string.Empty
        };
    }

    public static string ResolveOrigin(MaterialListItem material)
        => material.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";

    private static string FormatearNumero(decimal valor, string? formato)
    {
        var token = string.IsNullOrWhiteSpace(formato) ? FormatoNumericoPredeterminado : formato!.Trim();
        // Ruta central única: el mismo mapeo token→formato .NET que usa Excel
        // (ReportNumberFormatMapper delega en ReportNumberFormatMap). InvariantCulture
        // garantiza separadores estables y, con los tokens monetarios ya mapeados a
        // su equivalente numérico, evita el glifo ¤ del passthrough de "C4".
        var fmt = ReportNumberFormatMap.ToPdfFormat(token);
        return valor.ToString(fmt, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Tabla canónica de traducción del token de formato numérico del contrato
/// ("N2", "C4", "P2", ...) a formato .NET. Vive en Application porque es la única
/// capa alcanzable tanto por el resolver de materiales (ruta PDF) como por
/// <c>SOPRO.Reporting.Formatting.ReportNumberFormatMapper</c> (ruta Excel, que
/// referencia Application). Así se comparte UNA sola tabla entre PDF y Excel sin
/// crear una referencia circular Application→Reporting ni duplicar el mapper:
/// el facade de Reporting delega aquí.
///
/// La traducción es POR RENDERIZADOR para reproducir con fidelidad el legacy de
/// la versión publicada (8afabde):
/// <list type="bullet">
///   <item><see cref="ToPdfFormat"/> reproduce la tabla de <c>FormatearDecimal</c>
///   del PDF legacy, ampliada para eliminar el glifo de moneda genérico ¤ que
///   producía el passthrough literal de los tokens monetarios sin símbolo:
///   N0/N2/N3/N4/N5 y C0/C1/C3/C4 mapeados a su equivalente numérico sin símbolo
///   (C2 conserva el <c>$#,##0.00</c> publicado), vacío → <c>#,##0.00</c> y
///   cualquier otro token (P2, N1, "#,##0.0000", "0.00", ...) en
///   <b>passthrough literal</b>.</item>
///   <item><see cref="ToExcelFormat"/> reproduce la tabla de <c>ConvertirFormato</c>
///   del Excel legacy: N0/N2/N3/N4/N5/C2/P2 mapeados, vacío → <c>#,##0.00</c>;
///   los formatos .NET explícitos (contienen '#','0','.',',','%') en passthrough
///   literal y el resto de tokens letra (C0,C1,C3,C4,N1,P0,P1,P3) caen al default
///   legacy <c>#,##0.00</c> (seguro para ClosedXML, SIN '$').</item>
/// </list>
/// </summary>
public static class ReportNumberFormatMap
{
    /// <summary>Formato por defecto para columnas numéricas sin formato ("N2").</summary>
    public const string DefaultFormat = "#,##0.00";

    /// <summary>
    /// Caracteres que delatan un formato .NET/Excel explícito (a diferencia de un
    /// token mnemónico del contrato como "C4" o "P2").
    /// </summary>
    private static readonly char[] SimbolosFormatoNet = { '#', '0', '.', ',', '%' };

    /// <summary>
    /// Método de conveniencia histórico: equivale a <see cref="ToPdfFormat"/>.
    /// Se prefiere <see cref="ToPdfFormat"/>/<see cref="ToExcelFormat"/> explícitos
    /// porque la tabla ya diverge por renderizador.
    /// </summary>
    /// <param name="formato">Token del contrato ("N0".."N5", "C2", "C4", "P2") o formato .NET explícito.</param>
    public static string Map(string? formato) => ToPdfFormat(formato);

    /// <summary>
    /// Traduce un token a la cadena .NET que aplica el renderizador PDF/MigraDoc.
    /// Reproduce la tabla legacy PDF con passthrough literal para el resto. Los
    /// tokens monetarios sin símbolo (C0/C1/C3/C4) se mapean a su equivalente
    /// numérico para no emitir el glifo ¤ de la cultura invariante; C2 conserva
    /// el <c>$#,##0.00</c> publicado.
    /// </summary>
    /// <param name="formato">Token del contrato ("N0".."N5", "C0".."C4", "P2") o formato .NET explícito.</param>
    /// <returns>Cadena de formato lista para <c>decimal.ToString</c>.</returns>
    public static string ToPdfFormat(string? formato)
    {
        if (string.IsNullOrWhiteSpace(formato))
            return DefaultFormat;

        string token = formato.Trim();
        return token.ToUpperInvariant() switch
        {
            "N0" => "#,##0",
            "N2" => "#,##0.00",
            "N3" => "#,##0.000",
            "N4" => "#,##0.0000",
            "N5" => "#,##0.00000",
            "C0" => "#,##0",
            "C1" => "#,##0.0",
            "C2" => "$#,##0.00",
            "C3" => "#,##0.000",
            "C4" => "#,##0.0000",
            // Token no reconocido: passthrough literal (fidelidad legacy PDF).
            _ => token,
        };
    }

    /// <summary>
    /// Traduce un token a la cadena de formato que aplica el renderizador
    /// Excel/ClosedXML. Reproduce la tabla legacy Excel, respeta los formatos .NET
    /// explícitos y cae al default seguro (sin '$') para tokens letra no soportados.
    /// </summary>
    /// <param name="formato">Token del contrato ("N0".."N5", "C2", "C4", "P2") o formato .NET explícito.</param>
    /// <returns>Cadena de formato lista para <c>NumberFormat.Format</c>.</returns>
    public static string ToExcelFormat(string? formato)
    {
        if (string.IsNullOrWhiteSpace(formato))
            return DefaultFormat;

        string token = formato.Trim();
        switch (token.ToUpperInvariant())
        {
            case "N0": return "#,##0";
            case "N2": return "#,##0.00";
            case "N3": return "#,##0.000";
            case "N4": return "#,##0.0000";
            case "N5": return "#,##0.00000";
            case "C2": return "$#,##0.00";
            case "P2": return "0.00%";
        }

        // Formato .NET/Excel explícito (p. ej. "#,##0.0000" de Materiales o "0.00"
        // de %): se respeta literalmente para que ClosedXML lo interprete. Se
        // excluyen antes los tokens mnemónicos (letra+dígitos: C0, P0, C4, N1...),
        // que aunque contengan '0' NO son formatos .NET válidos para Excel legacy.
        if (EsTokenMnemonic(token))
            return DefaultFormat;

        if (token.IndexOfAny(SimbolosFormatoNet) >= 0)
            return token;

        // Resto de tokens no reconocidos: default legacy Excel, sin símbolo de
        // moneda (evita el '$' que el legacy nunca introdujo aquí).
        return DefaultFormat;
    }

    /// <summary>
    /// Indica si el texto es un token mnemónico del contrato (una o más letras
    /// seguidas sólo de dígitos: "C0", "C4", "N1", "P2"...), a diferencia de un
    /// formato .NET/Excel explícito como "#,##0.0000" o "0.00".
    /// </summary>
    private static bool EsTokenMnemonic(string token)
    {
        if (token.Length < 2)
            return false;

        int i = 0;
        while (i < token.Length && char.IsLetter(token[i]))
            i++;

        if (i == 0 || i == token.Length)
            return false;

        for (; i < token.Length; i++)
        {
            if (!char.IsDigit(token[i]))
                return false;
        }

        return true;
    }
}
