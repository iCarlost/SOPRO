using System;
using System.Collections.Generic;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Resolución neutral de exportación (PDF/Excel) del reporte de APU (Análisis de
/// Precios Unitarios). Vive en Application para poder probarse sin formularios:
/// los generadores de la UI sólo renderizan.
///
/// Es también el punto único que construye el snapshot neutral de columnas
/// (<see cref="ReportColumnSnapshot"/>) del APU, de modo que PDF y Excel
/// consuman exactamente la misma lista, orden, anchos, alineaciones, roles y
/// formatos.
///
/// La resolución de formato NUMÉRICO (moneda <c>$</c>, decimales de importe/
/// cantidad) NO vive aquí: es responsabilidad de la capa de reportes
/// (<c>SOPRO.Reporting.Formatting.ReportColumnGridFormat</c>, único punto del
/// símbolo de moneda) porque Application no referencia Reporting. Este resolver
/// aporta el TEXTO de las columnas de APU y las señales de rol (cantidad, costo
/// unitario, importe, tipo de insumo) que ambos renderizadores comparten.
/// </summary>
public static class ApuExportResolver
{
    /// <summary>Identificador neutral de la columna de tipo de insumo.</summary>
    public const string IdentificadorTipo = "Tipo";

    /// <summary>Identificador neutral de la columna de clave.</summary>
    public const string IdentificadorClave = "Clave";

    /// <summary>Identificador neutral de la columna de descripción.</summary>
    public const string IdentificadorDescripcion = "Descripcion";

    /// <summary>Identificador neutral de la columna de unidad.</summary>
    public const string IdentificadorUnidad = "Unidad";

    /// <summary>Identificador neutral de la columna de cantidad.</summary>
    public const string IdentificadorCantidad = "Cantidad";

    /// <summary>Identificador neutral de la columna de costo unitario (P.U.).</summary>
    public const string IdentificadorPrecioUnitario = "PrecioUnitario";

    /// <summary>Identificador neutral de la columna de importe (alias canónico del APU).</summary>
    public const string IdentificadorImporte = "ImporteTotal";

    /// <summary>
    /// Construye el snapshot neutral de columnas del APU. Es la ÚNICA fuente que
    /// consumen PDF y Excel (misma visibilidad/orden/ancho/formato y mismos
    /// decimales del proyecto, para paridad grid↔export). Si no llegan columnas
    /// se usan los defaults en memoria (no se inicializa ni persiste configuración).
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas neutrales del APU (por defecto, los defaults en memoria).</param>
    /// <param name="decimalesCantidad">Decimales de cantidad del proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe del proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje del proyecto (por defecto 4).</param>
    public static ReportColumnSnapshot BuildSnapshot(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaPersonalizada>? columnas = null,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new ApuReportSnapshotBuilder().Build(
            proyectoId, titulo, columnas, decimalesCantidad, decimalesImporte, decimalesPorcentaje);

    /// <summary>
    /// Columnas neutrales por defecto del APU (sin proyecto/configuración).
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => ApuReportSnapshotBuilder.DefaultColumns();

    /// <summary>Indica si la columna es la de tipo de insumo.</summary>
    public static bool EsTipo(ReportColumnDefinition columna)
        => EsIdentificador(columna, IdentificadorTipo);

    /// <summary>Indica si la columna es la de cantidad (rol cantidad del proyecto).</summary>
    public static bool EsCantidad(ReportColumnDefinition columna)
        => EsIdentificador(columna, IdentificadorCantidad);

    /// <summary>Indica si la columna es la de costo unitario (P.U., rol monetario).</summary>
    public static bool EsPrecioUnitario(ReportColumnDefinition columna)
        => EsIdentificador(columna, IdentificadorPrecioUnitario);

    /// <summary>Indica si la columna es la de importe (rol monetario).</summary>
    public static bool EsImporte(ReportColumnDefinition columna)
        => EsIdentificador(columna, IdentificadorImporte);

    /// <summary>
    /// Resuelve el TEXTO de una celda de componente para las columnas de catálogo
    /// (Clave/Descripción/Unidad). Las columnas numéricas (Cantidad, Costo Unit.,
    /// Importe) y la de Tipo (abreviatura de sección) NO se resuelven aquí: su
    /// valor proviene del contexto de la fila/sección.
    /// </summary>
    /// <param name="columna">Columna neutral.</param>
    /// <param name="clave">Clave del insumo.</param>
    /// <param name="descripcion">Descripción del insumo.</param>
    /// <param name="unidad">Unidad del insumo.</param>
    public static string ResolveTexto(ReportColumnDefinition columna, string? clave, string? descripcion, string? unidad)
    {
        ArgumentNullException.ThrowIfNull(columna);

        return columna.Identificador switch
        {
            IdentificadorClave => clave ?? string.Empty,
            IdentificadorDescripcion => descripcion ?? string.Empty,
            IdentificadorUnidad => unidad ?? string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Abreviatura de la columna Tipo del APU para el título de una sección, con
    /// la MISMA semántica del grid legacy (MAT / M.O. / MAQ / HER / AUX).
    /// </summary>
    /// <param name="tituloGrupo">Título de la sección de insumos.</param>
    public static string AbreviarTipo(string? tituloGrupo)
    {
        var titulo = tituloGrupo ?? string.Empty;
        if (titulo.StartsWith("MATER", StringComparison.OrdinalIgnoreCase)) return "MAT";
        if (titulo.StartsWith("MANO", StringComparison.OrdinalIgnoreCase)) return "M.O.";
        if (titulo.StartsWith("MAQUI", StringComparison.OrdinalIgnoreCase)) return "MAQ";
        if (titulo.StartsWith("HER", StringComparison.OrdinalIgnoreCase)) return "HER";
        return "AUX";
    }

    private static bool EsIdentificador(ReportColumnDefinition? columna, string identificador)
        => columna != null
           && string.Equals(columna.Identificador, identificador, StringComparison.OrdinalIgnoreCase);
}
