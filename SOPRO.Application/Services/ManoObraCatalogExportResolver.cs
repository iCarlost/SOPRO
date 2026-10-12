using System.Collections.Generic;
using System.Linq;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Resolución de valores de exportación (PDF/Excel) de una fila del catálogo de
/// Mano de Obra a partir de la entidad <see cref="ManoDeObra"/>. Vive en
/// Application para poder probarse sin formularios ni renderizadores: los
/// generadores de la UI solo materializan lo que este resolver expone.
///
/// Es el punto único que construye el snapshot neutral de columnas
/// (<see cref="ReportColumnSnapshot"/>) del catálogo, de modo que PDF y Excel
/// consuman exactamente la misma lista, orden, anchos, formatos y estilos. Los
/// defaults se construyen EN MEMORIA (nunca con <c>ColumnasManoObraHelper</c>,
/// que persistiría) para no escribir configuración desde la ruta de exportación.
///
/// La decisión de formato numérico del contrato (monetario vs. cantidad vs.
/// token legacy) NO vive aquí sino en <c>SOPRO.Reporting.Formatting.ReportColumnGridFormat</c>,
/// que es la única regla compartida por PDF y Excel; este resolver sólo aporta el
/// valor crudo (decimal) o el texto.
/// </summary>
public static class ManoObraCatalogExportResolver
{
    /// <summary>Formato numérico por defecto del FSR cuando la columna no define uno.</summary>
    public const string FormatoFactorSalarioRealPredeterminado = "N4";

    /// <summary>Formato monetario por defecto de los salarios.</summary>
    public const string FormatoMonetarioPredeterminado = "C2";

    /// <summary>
    /// Columnas por defecto cuando no hay proyecto ni configuración persistida.
    /// Reproduce la configuración de catálogo de mano de obra (mismos
    /// identificadores, orden, anchos, alineación y tokens) y se expresa como
    /// <see cref="ColumnaManoObra"/> para viajar por el mismo mapper neutral que
    /// las columnas reales.
    /// </summary>
    private static readonly IReadOnlyList<ColumnaManoObra> ColumnasPredeterminadas = new List<ColumnaManoObra>
    {
        new() { Nombre = "Clave", NombreInterno = "Clave", Visible = true, Orden = 1, AnchoColumna = 110, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Descripción", NombreInterno = "Descripcion", Visible = true, Orden = 2, AnchoColumna = 350, Alineacion = AlineacionColumna.Izquierda, WrapTexto = true },
        new() { Nombre = "Unidad", NombreInterno = "Unidad", Visible = true, Orden = 3, AnchoColumna = 70, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Salario Base", NombreInterno = "SalarioBase", Visible = true, Orden = 4, AnchoColumna = 120, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = FormatoMonetarioPredeterminado },
        new() { Nombre = "FSR", NombreInterno = "FactorSalarioReal", Visible = true, Orden = 5, AnchoColumna = 80, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = FormatoFactorSalarioRealPredeterminado },
        new() { Nombre = "Salario Real", NombreInterno = "SalarioReal", Visible = true, Orden = 6, AnchoColumna = 120, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = FormatoMonetarioPredeterminado },
        new() { Nombre = "Origen", NombreInterno = "Origen", Visible = true, Orden = 7, AnchoColumna = 80, Alineacion = AlineacionColumna.Centro },
    };

    /// <summary>
    /// Construye el snapshot neutral de columnas del catálogo de mano de obra. Es
    /// la ÚNICA fuente que consumen PDF y Excel (misma visibilidad/orden/ancho/
    /// formato y mismos decimales del proyecto, para paridad grid↔export). Si no
    /// llegan columnas se usan las predeterminadas EN MEMORIA.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de mano de obra persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad del proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe del proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje del proyecto (por defecto 4).</param>
    public static ReportColumnSnapshot BuildSnapshot(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaManoObra>? columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var fuente = columnas?.Where(c => c != null).ToList();
        if (fuente == null || fuente.Count == 0)
            fuente = ColumnasPredeterminadas.ToList();

        return new ManoObraReportSnapshotBuilder().Build(
            proyectoId, titulo, fuente, decimalesCantidad, decimalesImporte, decimalesPorcentaje);
    }

    /// <summary>
    /// Columnas neutrales por defecto (sin proyecto/configuración), ya filtradas
    /// de columnas internas y ordenadas por <c>Orden</c>.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearManoObra)
            .ToList();

    /// <summary>
    /// Resuelve el valor de TEXTO de una celda para una columna neutral con
    /// paridad con el grid. Las columnas numéricas (salarios, FSR) NO se formatean
    /// aquí: su valor crudo se obtiene con <see cref="TryResolveNumber"/> y el
    /// formato lo decide la regla compartida del contrato. Las columnas de texto
    /// desconocidas devuelven cadena vacía (fidelidad legacy).
    /// </summary>
    /// <param name="manoDeObra">Fila del catálogo.</param>
    /// <param name="column">Columna neutral.</param>
    public static string ResolveValue(ManoDeObra manoDeObra, ReportColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(manoDeObra);
        ArgumentNullException.ThrowIfNull(column);

        return NormalizarIdentificador(column.Identificador).ToUpperInvariant() switch
        {
            "CLAVE" => manoDeObra.Clave ?? string.Empty,
            "DESCRIPCION" => manoDeObra.Descripcion ?? string.Empty,
            "UNIDAD" => manoDeObra.Unidad ?? string.Empty,
            "ORIGEN" => ResolveOrigin(manoDeObra),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Intenta obtener el valor NUMÉRICO crudo de una fila para una columna
    /// neutral. Devuelve <c>false</c> para columnas de texto o columnas numéricas
    /// sin campo de respaldo en la entidad (fidelidad legacy: antes esas columnas
    /// se exportaban vacías).
    /// </summary>
    /// <param name="manoDeObra">Fila del catálogo.</param>
    /// <param name="column">Columna neutral.</param>
    /// <param name="valor">Valor decimal resuelto.</param>
    public static bool TryResolveNumber(ManoDeObra manoDeObra, ReportColumnDefinition column, out decimal valor)
    {
        ArgumentNullException.ThrowIfNull(manoDeObra);
        ArgumentNullException.ThrowIfNull(column);

        switch (NormalizarIdentificador(column.Identificador).ToUpperInvariant())
        {
            case "SALARIOBASE":
                valor = manoDeObra.SalarioBase;
                return true;
            case "FACTORSALARIOREAL":
                valor = manoDeObra.FactorSalarioReal;
                return true;
            case "SALARIOREAL":
                valor = manoDeObra.SalarioReal;
                return true;
            default:
                valor = 0m;
                return false;
        }
    }

    /// <summary>Origen del insumo como texto visible ("Maestro"/"Proyecto").</summary>
    public static string ResolveOrigin(ManoDeObra manoDeObra)
        => manoDeObra.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";

    /// <summary>
    /// Resuelve el color de fondo (hex) de una celda respetando el color por
    /// columna definido en la entidad y, en su defecto, el bandeado de la tabla.
    /// Es neutral (no depende de UI) para que PDF y Excel compartan la regla.
    /// </summary>
    /// <param name="columna">Columna neutral.</param>
    /// <param name="tabla">Estilo neutral de la tabla.</param>
    /// <param name="esFilaAlterna">Si la fila es la alterna del bandeado.</param>
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
    /// Normaliza el identificador para el reconocimiento de campo de respaldo:
    /// recorta espacios, descarta un prefijo de grid <c>col</c> (p. ej.
    /// <c>colSalarioBase</c> → <c>SalarioBase</c>) y resuelve el alias legacy
    /// <c>Importe</c> a <c>ImporteTotal</c>.
    /// </summary>
    private static string NormalizarIdentificador(string? identificador)
    {
        var id = (identificador ?? string.Empty).Trim();
        if (id.Length > 3 && id.StartsWith("col", StringComparison.OrdinalIgnoreCase))
            id = id[3..];
        return ReportColumnDefinitionMapper.NormalizarIdentificador(id);
    }
}
