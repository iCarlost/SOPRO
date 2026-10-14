using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Constructor puro del snapshot de columnas del reporte de Presupuesto.
///
/// Toma configuración persistida (columnas personalizadas + overlays de reporte)
/// y produce un <see cref="ReportColumnSnapshot"/> inmutable. NO toca grids, NO
/// lee de controles de UI y NO escribe configuración.
///
/// Precedencia: <c>ColumnaPersonalizada</c> es primaria; <c>ConfigColumnaReporte</c>
/// sólo aporta el estilo del encabezado cuando su nombre interno coincide.
/// </summary>
public sealed class PresupuestoReportSnapshotBuilder
{
    /// <summary>Tipo de reporte que produce este builder.</summary>
    public const string TipoReporte = "Presupuesto";

    /// <summary>
    /// Clave de vista canónica del reporte de Presupuesto. Es la vista por defecto
    /// (<c>""</c>), la MISMA que persiste el grid en
    /// <c>FormatoCeldaReporte.ClaveVista</c> (ver <c>FormPresupuesto.FormatoVistaPresupuesto</c>),
    /// de modo que el resolver de estilos coincide por igualdad exacta en PDF/Excel.
    /// </summary>
    public const string ClaveVista = "";

    /// <summary>
    /// Marca interna de las filas sintéticas de subtotal que los reportes de
    /// Presupuesto insertan tras cerrar cada agrupador (no son conceptos
    /// persistidos).
    /// </summary>
    public const string MarcaSubtotalSintetico = "__subtotal__";

    /// <summary>
    /// Construye el snapshot desde las columnas personalizadas del proyecto y su
    /// configuración de reporte (overlay de encabezados).
    /// </summary>
    /// <param name="proyectoId">Proyecto dueño del reporte.</param>
    /// <param name="titulo">Título visible del reporte, si aplica.</param>
    /// <param name="columnas">Columnas personalizadas persistidas.</param>
    /// <param name="configuracionReporte">Overlays de encabezado persistidos.</param>
    /// <param name="decimalesCantidad">Decimales de cantidad configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesImporte">Decimales de importe configurados en el proyecto (por defecto 2).</param>
    /// <param name="decimalesPorcentaje">Decimales de porcentaje configurados en el proyecto (por defecto 4).</param>
    public ReportColumnSnapshot Build(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaPersonalizada> columnas,
        IEnumerable<ConfigColumnaReporte>? configuracionReporte = null,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
    {
        ArgumentNullException.ThrowIfNull(columnas);

        var overlays = (configuracionReporte ?? Enumerable.Empty<ConfigColumnaReporte>())
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.NombreInterno))
            .GroupBy(c => ReportColumnDefinitionMapper.NormalizarIdentificador(c.NombreInterno), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var definiciones = columnas
            .Where(c => c != null && !ReportColumnDefinitionMapper.EsColumnaInterna(c.NombreInterno))
            .OrderBy(c => c.Orden)
            .Select(c =>
            {
                var identificador = ReportColumnDefinitionMapper.NormalizarIdentificador(c.NombreInterno);
                overlays.TryGetValue(identificador, out var overlay);
                return ReportColumnDefinitionMapper.MapearPresupuesto(c, overlay);
            })
            .ToList();

        return new ReportColumnSnapshot(
            TipoReporte: TipoReporte,
            ProyectoId: proyectoId,
            Titulo: titulo ?? string.Empty,
            Columnas: definiciones,
            EstiloTabla: ReportTableStyle.LegacyPresupuesto())
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };
    }

    /// <summary>
    /// Clave de fila ESTABLE de un renglón del reporte de Presupuesto, derivada del
    /// dominio (nunca del índice visual del grid): <c>C:{Id}</c> para conceptos
    /// terminales y <c>SUB:{Id}</c> para agrupadores persistidos (Capítulo/
    /// Subcapítulo/Nivel); las filas SINTÉTICAS de subtotal (sin Id persistido) usan
    /// <c>SUB:{Clave}</c>. Es EXACTAMENTE la clave que persiste el grid en
    /// <c>FormatoCeldaReporte.ClaveFila</c> (ver
    /// <c>FormPresupuesto.ObtenerClaveFilaFormato</c>), de modo que los overrides de
    /// celda sobreviven a reordenamientos, filtros y paginación, y el override de un
    /// agrupador NO se contamina con el de otro aunque compartan <c>Clave</c>.
    /// </summary>
    public static string ClaveFila(ConceptoPresupuesto concepto)
    {
        ArgumentNullException.ThrowIfNull(concepto);

        if (concepto.EsAgrupador)
        {
            // Un agrupador persistido tiene Id > 0 (clave única por fila). Las filas
            // sintéticas de subtotal (creadas por los generadores PDF/Excel) tienen
            // Id 0 y conservan la clave basada en Clave.
            return concepto.Id > 0
                ? ClaveFilaReporte.Agrupador(concepto.Id)
                : ClaveFilaReporte.Subtotal(concepto.Clave);
        }

        return ClaveFilaReporte.Concepto(concepto.Id);
    }

    /// <summary>
    /// Rol funcional de fila del renglón (correspondiente a
    /// <see cref="RolFilaReporte"/>), usado por el resolver para aplicar los
    /// estilos semánticos de <c>ConfiguracionEstiloFilaReporte</c>. Los agrupadores
    /// se clasifican por nivel (Capítulo/Subcapítulo/NivelN) y los conceptos
    /// terminales como <c>Concepto</c>; las filas sintéticas de subtotal como
    /// <c>Subtotal</c>.
    /// </summary>
    public static string RolFila(ConceptoPresupuesto concepto)
    {
        ArgumentNullException.ThrowIfNull(concepto);

        if (EsSubtotalSintetico(concepto))
            return RolFilaReporte.Subtotal;

        if (!concepto.EsAgrupador)
            return RolFilaReporte.Concepto;

        return concepto.Nivel switch
        {
            0 => RolFilaReporte.Capitulo,
            1 => RolFilaReporte.Subcapitulo,
            2 => RolFilaReporte.Nivel1,
            3 => RolFilaReporte.Nivel2,
            _ => RolFilaReporte.Nivel3,
        };
    }

    private static bool EsSubtotalSintetico(ConceptoPresupuesto concepto)
        => string.Equals(concepto.Notas, MarcaSubtotalSintetico, StringComparison.Ordinal);
}
