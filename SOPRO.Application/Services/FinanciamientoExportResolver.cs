using System;
using System.Collections.Generic;
using System.Linq;
using SOPRO.Application.Models.Reporting.Financiamiento;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Resolución en memoria del reporte de Financiamiento para su exportación a
/// PDF/Excel. Vive en Application para poder probarse sin formularios: los
/// generadores de la UI solo renderizan.
///
/// Es el punto único que:
/// <list type="bullet">
///   <item>construye el snapshot neutral de columnas (<see cref="ReportColumnSnapshot"/>)
///   del catálogo de financiamiento, para que PDF y Excel consuman exactamente la
///   misma lista, orden, anchos, formatos y estilos; y</item>
///   <item>calcula el <see cref="FinanciamientoReportModel"/> (bloque de datos y
///   matriz de períodos) con UNA sola aritmética, garantizando paridad de filas
///   base/períodos entre PDF y Excel.</item>
/// </list>
///
/// La resolución del formato NUMÉRICO (moneda <c>$</c>, decimales de importe/
/// cantidad/porcentaje) NO vive aquí: es responsabilidad de la capa de reportes
/// (<c>SOPRO.Reporting.Formatting.ReportColumnGridFormat</c>, único punto del
/// símbolo de moneda) porque Application no referencia Reporting. Este resolver
/// aporta los valores crudos y su rol de formato neutro.
/// </summary>
public static class FinanciamientoExportResolver
{
    /// <summary>
    /// Construye el snapshot neutral de columnas del reporte de financiamiento. Es
    /// la ÚNICA fuente que consumen PDF y Excel (misma visibilidad/orden/ancho/
    /// formato y mismos decimales del proyecto). Si no llegan columnas se usan las
    /// predeterminadas del catálogo.
    /// </summary>
    public static ReportColumnSnapshot BuildSnapshot(
        int proyectoId,
        string? titulo,
        IEnumerable<ColumnaFinanciamiento>? columnas,
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new FinanciamientoReportSnapshotBuilder().Build(
            proyectoId, titulo, columnas, decimalesCantidad, decimalesImporte, decimalesPorcentaje);

    /// <summary>
    /// Columnas neutrales por defecto (sin proyecto/configuración), ya filtradas de
    /// columnas internas y ordenadas por <c>Orden</c>.
    /// </summary>
    public static IReadOnlyList<ReportColumnDefinition> DefaultColumns()
        => FinanciamientoReportSnapshotBuilder.DefaultColumns();

    /// <summary>
    /// Construye el modelo neutral del reporte: bloque de datos del encabezado y
    /// matriz de períodos, con la MISMA aritmética para PDF y Excel. Las filas base
    /// se alinean por <c>NumeroPeriodo</c> con las filas del flujo; los totales de
    /// costo directo/indirecto suman TODAS las filas base (incluidos los períodos de
    /// desfase), igual que el reporte legacy.
    /// </summary>
    /// <param name="proyecto">Proyecto (decimales y % de indirectos).</param>
    /// <param name="config">Configuración de financiamiento.</param>
    /// <param name="filas">Filas del flujo de caja persistidas.</param>
    /// <param name="baseRows">Filas base (costo directo/indirecto por período).</param>
    /// <param name="esModeloDual">Indica si el cálculo usa el modelo dual.</param>
    public static FinanciamientoReportModel BuildModel(
        Proyecto proyecto,
        ConfiguracionFinanciamiento config,
        IReadOnlyList<FilaFlujoCajaFinanciamiento> filas,
        IReadOnlyList<FinanciamientoBaseRow> baseRows,
        bool esModeloDual = false)
    {
        ArgumentNullException.ThrowIfNull(proyecto);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(baseRows);

        var filasOrdenadas = filas.OrderBy(f => f.NumeroPeriodo).ToList();
        var baseRowsMap = baseRows
            .GroupBy(b => b.NumeroPeriodo)
            .ToDictionary(g => g.Key, g => g.First());

        decimal totalCD = baseRows.Sum(b => b.CostoDirecto);
        decimal totalCI = baseRows.Sum(b => b.CostoIndirecto);
        decimal totalBase = totalCD + totalCI;

        var etiquetas = filasOrdenadas.Select(f => f.Etiqueta ?? string.Empty).ToList();
        var datos = ConstruirDatos(proyecto, config, filasOrdenadas, totalCD, totalCI, esModeloDual);
        var matriz = ConstruirMatriz(proyecto, config, filasOrdenadas, baseRowsMap, totalBase, esModeloDual);

        return new FinanciamientoReportModel(
            EtiquetasPeriodo: etiquetas,
            Datos: datos,
            Matriz: matriz,
            PorcentajeCalculado: config.PorcentajeCalculado);
    }

    /// <summary>
    /// Tasa del período (en porcentaje, p. ej. 0.4384) con la MISMA semántica del
    /// grid, que es la fuente de verdad visual: tasa efectiva anual (TIIE + puntos)
    /// prorrateada por los días del período; en modelo dual, cuando el saldo es
    /// positivo se usa sólo la TIIE.
    /// </summary>
    public static decimal CalcularTasaPeriodo(
        ConfiguracionFinanciamiento config,
        bool esModeloDual,
        int diasPeriodo,
        decimal saldoAcumulado)
    {
        ArgumentNullException.ThrowIfNull(config);

        decimal tasaAnual = (config.TasaTIIE + config.PuntosAdicionales) / 100m;
        if (esModeloDual && saldoAcumulado > 0m)
            tasaAnual = config.TasaTIIE / 100m;

        if (tasaAnual <= 0m || diasPeriodo <= 0)
            return 0m;

        return decimal.Round(tasaAnual * diasPeriodo / 365m * 100m, 4, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<FinanciamientoDatoRow> ConstruirDatos(
        Proyecto proyecto,
        ConfiguracionFinanciamiento config,
        IReadOnlyList<FilaFlujoCajaFinanciamiento> filas,
        decimal totalCD,
        decimal totalCI,
        bool esModeloDual)
    {
        var f0 = filas.Count > 0 ? filas[0] : null;
        decimal tasaBase = f0 == null
            ? 0m
            : CalcularTasaPeriodo(config, esModeloDual, f0.DiasPeriodo, f0.SaldoAcumulado);

        string pctIndirectos = (proyecto.PorcentajeIndirectosCentral + proyecto.PorcentajeIndirectosCampo)
            .ToString("N2", System.Globalization.CultureInfo.CurrentCulture);

        return new List<FinanciamientoDatoRow>
        {
            new("COSTO DIRECTO",
                FinanciamientoValor.Numerico(totalCD, FinanciamientoValorFormato.Moneda),
                "INDICADOR ECONÓMICO", "TIIE",
                FinanciamientoValor.Numerico(config.TasaTIIE / 100m, FinanciamientoValorFormato.Porcentaje)),
            new($"COSTO INDIRECTO = {pctIndirectos}%",
                FinanciamientoValor.Numerico(totalCI, FinanciamientoValorFormato.Moneda),
                "TASA DE INTERÉS ANUAL", string.Empty,
                FinanciamientoValor.Numerico(config.TasaEfectiva / 100m, FinanciamientoValorFormato.Porcentaje)),
            new("% ANTICIPO",
                FinanciamientoValor.Numerico(config.PorcentajeAnticipo / 100m, FinanciamientoValorFormato.Porcentaje),
                "TASA DE INTERÉS PERIODO BASE", string.Empty,
                FinanciamientoValor.Numerico(tasaBase / 100m, FinanciamientoValorFormato.Porcentaje)),
            new("DESFASE DE COBRO",
                FinanciamientoValor.Numerico(config.DesfaseCobro, FinanciamientoValorFormato.Entero),
                "BASE DE CÁLCULO", string.Empty,
                FinanciamientoValor.Textual(config.BaseCalculo)),
        };
    }

    private static IReadOnlyList<FinanciamientoMatrixRow> ConstruirMatriz(
        Proyecto proyecto,
        ConfiguracionFinanciamiento config,
        IReadOnlyList<FilaFlujoCajaFinanciamiento> filas,
        IReadOnlyDictionary<int, FinanciamientoBaseRow> baseRowsMap,
        decimal totalBase,
        bool esModeloDual)
    {
        var avance = filas.Select(f =>
        {
            decimal basePeriodo = baseRowsMap.TryGetValue(f.NumeroPeriodo, out var b)
                ? b.CostoDirecto + b.CostoIndirecto
                : 0m;
            return totalBase > 0m
                ? decimal.Round(basePeriodo / totalBase, 4, MidpointRounding.AwayFromZero)
                : 0m;
        }).ToList();

        var ingresosAcum = new List<decimal>(filas.Count);
        var egresosAcum = new List<decimal>(filas.Count);
        decimal ingresoAcum = 0m;
        decimal egresoAcum = 0m;
        foreach (var f in filas)
        {
            ingresoAcum += f.AnticipoRecibido + f.EstimacionCobrada - f.AmortizacionAnticipo;
            egresoAcum += f.Egresos;
            ingresosAcum.Add(BudgetPricingService.RoundImporte(proyecto, ingresoAcum));
            egresosAcum.Add(BudgetPricingService.RoundImporte(proyecto, egresoAcum));
        }

        var tasas = filas
            .Select(f => CalcularTasaPeriodo(config, esModeloDual, f.DiasPeriodo, f.SaldoAcumulado) / 100m)
            .ToList();

        var interesAcum = new List<decimal>(filas.Count);
        decimal acumuladoInteres = 0m;
        foreach (var f in filas)
        {
            acumuladoInteres += f.InteresPeriodo;
            interesAcum.Add(acumuladoInteres);
        }

        decimal Base(int i) => baseRowsMap.TryGetValue(filas[i].NumeroPeriodo, out var b) ? b.CostoDirecto : 0m;
        decimal BaseInd(int i) => baseRowsMap.TryGetValue(filas[i].NumeroPeriodo, out var b) ? b.CostoIndirecto : 0m;

        return new List<FinanciamientoMatrixRow>
        {
            FinanciamientoMatrixRow.FilaValores("AVANCE PROGRAMADO", avance,
                FinanciamientoValorFormato.Porcentaje, "colPeriodo", "colEgresos"),
            FinanciamientoMatrixRow.Espaciador(),

            FinanciamientoMatrixRow.Seccion("INGRESOS"),
            FinanciamientoMatrixRow.FilaValores("ESTIMACIONES DE OBRA (CD + CI)",
                filas.Select(f => f.EstimacionCobrada).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colEstim"),
            FinanciamientoMatrixRow.FilaValores("AMORTIZACIÓN ANTICIPO",
                filas.Select(f => f.AmortizacionAnticipo).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colAmort"),
            FinanciamientoMatrixRow.FilaValores("COBRO NETO",
                filas.Select(f => f.EstimacionCobrada - f.AmortizacionAnticipo).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colCobro"),
            FinanciamientoMatrixRow.FilaValores("ANTICIPOS (CD + CI)",
                filas.Select(f => f.AnticipoRecibido).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colAnticipo"),
            FinanciamientoMatrixRow.FilaValores("INGRESOS ACUMULADOS", ingresosAcum,
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colCobro"),
            FinanciamientoMatrixRow.Espaciador(),

            FinanciamientoMatrixRow.Seccion("EGRESOS"),
            FinanciamientoMatrixRow.FilaValores("COSTO DIRECTO",
                Enumerable.Range(0, filas.Count).Select(Base).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colCD"),
            FinanciamientoMatrixRow.FilaValores("COSTO INDIRECTO",
                Enumerable.Range(0, filas.Count).Select(BaseInd).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colCI"),
            FinanciamientoMatrixRow.FilaValores("C.D. + C.I.",
                filas.Select(f => f.Egresos).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colEgresos"),
            FinanciamientoMatrixRow.FilaValores("EGRESOS ACUMULADOS", egresosAcum,
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colEgresos"),
            FinanciamientoMatrixRow.Espaciador(),

            FinanciamientoMatrixRow.FilaValores("EGRESOS ACUM - INGRESOS ACUM",
                Enumerable.Range(0, filas.Count).Select(i => egresosAcum[i] - ingresosAcum[i]).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colSaldo"),
            FinanciamientoMatrixRow.FilaValores("TASA PERÍODO", tasas,
                FinanciamientoValorFormato.Porcentaje, "colPeriodo", "colTasa"),
            FinanciamientoMatrixRow.FilaValores("COSTO FINANC. PARCIAL (INTERESES)",
                filas.Select(f => f.InteresPeriodo).ToList(),
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colInteres"),
            FinanciamientoMatrixRow.FilaValores("COSTO FINANC. ACUMULADO", interesAcum,
                FinanciamientoValorFormato.Moneda, "colPeriodo", "colInteres"),
            FinanciamientoMatrixRow.Espaciador(),
        };
    }
}
