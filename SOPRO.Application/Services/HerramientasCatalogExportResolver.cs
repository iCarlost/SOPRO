using System;
using System.Collections.Generic;
using System.Linq;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Resolución de valores de exportación (PDF/Excel) de una herramienta desde su
/// entidad de dominio <see cref="Herramienta"/>. Vive en Application para poder
/// probarse sin formularios: los generadores de la UI solo renderizan.
///
/// Es también el punto único que construye el snapshot neutral de columnas
/// (<see cref="ReportColumnSnapshot"/>) del catálogo de herramientas, para que PDF
/// y Excel consuman exactamente la misma lista, orden, anchos, formatos y estilos.
///
/// La resolución de formato NUMÉRICO (moneda <c>$</c>, decimales de importe/
/// cantidad/porcentaje) NO vive aquí: es responsabilidad de la capa de reportes
/// (<c>SOPRO.Reporting.Formatting.ReportColumnGridFormat</c>, único punto del
/// símbolo de moneda) porque Application no referencia Reporting. Este resolver
/// aporta el TEXTO de las columnas de catálogo y las señales de rol (precio,
/// porcentaje, color de celda) que ambos renderizadores comparten.
/// </summary>
public static class HerramientasCatalogExportResolver
{
    /// <summary>
    /// Columnas por defecto cuando no hay proyecto ni configuración persistida.
    /// Reproducen el fallback legacy del generador PDF (mismos nombres, anchos,
    /// alineaciones y wrap). Se expresan como <see cref="ColumnaHerramienta"/>
    /// para que viajen por el mismo mapper neutral que las columnas reales.
    /// </summary>
    private static readonly IReadOnlyList<ColumnaHerramienta> ColumnasPredeterminadas = new List<ColumnaHerramienta>
    {
        new() { Nombre = "Clave", NombreInterno = "Clave", Visible = true, Orden = 1, AnchoColumna = 110, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Descripción", NombreInterno = "Descripcion", Visible = true, Orden = 2, AnchoColumna = 300, Alineacion = AlineacionColumna.Izquierda, WrapTexto = true },
        new() { Nombre = "Unidad", NombreInterno = "Unidad", Visible = true, Orden = 3, AnchoColumna = 80, Alineacion = AlineacionColumna.Centro },
        new() { Nombre = "Precio/Porcentaje", NombreInterno = "PrecioUnitario", Visible = true, Orden = 4, AnchoColumna = 150, Alineacion = AlineacionColumna.Derecha },
        new() { Nombre = "Origen", NombreInterno = "OrigenDetalle", Visible = true, Orden = 5, AnchoColumna = 180, Alineacion = AlineacionColumna.Izquierda },
    };

    /// <summary>
    /// Construye el snapshot neutral de columnas del catálogo de herramientas. Es la
    /// ÚNICA fuente que consumen PDF y Excel (misma visibilidad/orden/ancho/formato
    /// y mismos decimales del proyecto, para paridad grid↔export). Si no llegan
    /// columnas se usan las predeterminadas.
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas de herramienta persistidas.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad del proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe del proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje del proyecto (por defecto 4).</param>
    public static ReportColumnSnapshot BuildSnapshot(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaHerramienta>? columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        var fuente = columnas?.Where(c => c != null).ToList();
        if (fuente == null || fuente.Count == 0)
            fuente = ColumnasPredeterminadas.ToList();

        return new HerramientasReportSnapshotBuilder().Build(
            proyectoId, titulo, fuente, decimalesCantidad, decimalesImporte, decimalesPorcentaje);
    }

    /// <summary>
    /// Columnas neutrales por defecto (sin proyecto/configuración), ya filtradas
    /// de columnas internas y ordenadas por <c>Orden</c>.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ColumnasPredeterminadas
            .Select(ReportColumnDefinitionMapper.MapearHerramienta)
            .ToList();

    /// <summary>
    /// Indica si la columna es el Precio/Porcentaje unitario de la herramienta.
    /// Es la columna cuyo valor alterna entre moneda (Precio) y porcentaje (%MO).
    /// </summary>
    public static bool EsPrecioUnitario(ReportColumnDefinition column)
        => column != null
           && string.Equals(column.Identificador, "PrecioUnitario", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Indica si el precio unitario de la herramienta es un porcentaje de mano de
    /// obra (<c>%MO</c>) y por lo tanto debe formatearse como porcentaje, no como
    /// moneda. El grid es la fuente de verdad visual de esta decisión.
    /// </summary>
    public static bool EsPrecioUnitarioPorcentual(Herramienta herramienta)
    {
        ArgumentNullException.ThrowIfNull(herramienta);
        return herramienta.EsPorcentajeMO;
    }

    /// <summary>
    /// Resuelve el TEXTO de una celda para una columna neutral con PARIDAD con el
    /// grid, para las columnas de catálogo (Clave/Descripción/Unidad/Origen).
    ///
    /// La columna Precio/Porcentaje NO se resuelve aquí: su valor es numérico y su
    /// formato (moneda <c>$</c> o porcentaje) lo aplica el renderizador con el
    /// helper neutral de formato, que es el único dueño del símbolo de moneda.
    /// </summary>
    /// <param name="herramienta">Fila del catálogo.</param>
    /// <param name="column">Columna neutral.</param>
    public static string ResolveValue(Herramienta herramienta, ReportColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(herramienta);
        ArgumentNullException.ThrowIfNull(column);

        return column.Identificador switch
        {
            "Clave" => herramienta.Clave ?? string.Empty,
            "Descripcion" => herramienta.Descripcion ?? string.Empty,
            "Unidad" => herramienta.Unidad ?? string.Empty,
            "OrigenDetalle" => ResolveOrigin(herramienta),
            "Origen" => ResolveOrigin(herramienta),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Resuelve el texto de origen con la MISMA semántica del grid
    /// (<c>DgvHerramientas_CellFormatting</c>): si la herramienta fue importada de
    /// otro proyecto muestra <c>Importado: {proyecto}</c>; en caso contrario el
    /// fallback (<c>Maestro</c> para insumos maestros, <c>Local</c> para el resto).
    /// </summary>
    public static string ResolveOrigin(Herramienta herramienta)
    {
        ArgumentNullException.ThrowIfNull(herramienta);
        return ImportOriginStampService.BuildOriginDisplay(
            herramienta.Notas,
            herramienta.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
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
}
