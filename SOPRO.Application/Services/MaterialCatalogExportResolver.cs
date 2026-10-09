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
        new() { Nombre = "Precio Unitario", NombreInterno = "PrecioUnitario", Visible = true, Orden = 4, AnchoColumna = 140, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "C4" },
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
    /// se propaga tal cual al render.
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
        var fmt = string.IsNullOrWhiteSpace(formato) ? FormatoNumericoPredeterminado : formato!.Trim();
        return valor.ToString(fmt, CultureInfo.CurrentCulture);
    }
}
