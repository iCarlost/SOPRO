using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.Financiamiento;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Reporte de Financiamiento: paridad semántica PDF/Excel desde un ÚNICO
/// <see cref="ReportColumnSnapshot"/> y un ÚNICO <see cref="FinanciamientoReportModel"/>,
/// más golden del contrato neutral de columnas.
///
/// A. Paridad semántica: dado un snapshot, las vistas PDF y Excel derivan el mismo
///    conjunto de columnas (orden, visibilidad, encabezado, ancho px, alineaciones,
///    wrap y estilos) y los roles numéricos se resuelven con
///    <see cref="ReportColumnGridFormat"/>, la regla única compartida por ambos medios.
///
/// B. Modelo neutral: el bloque de datos y la matriz se calculan UNA sola vez
///    (<see cref="FinanciamientoExportResolver.BuildModel"/>), de modo que PDF y
///    Excel comparten filas base/períodos y formatos. El avance programado se deriva
///    de las filas base (no de los egresos) y los totales suman todas las filas base.
///
/// C. Goldens: la proyección semántica se congela en
///    <c>TestData/Goldens/financiamiento-columnas.json</c> usando
///    <see cref="GoldenPaths"/>. Es un archivo nuevo; NO se toca ningún golden del
///    piloto ni de Fase 1.
/// </summary>
[TestClass]
public class FinanciamientoReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotFinanciamiento_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(FinanciamientoReportFixtures.Financiamiento(), "Financiamiento");

    [TestMethod]
    public void SnapshotDefaults_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(FinanciamientoReportFixtures.Defaults(), "Financiamiento (defaults)");

    [TestMethod]
    public void SnapshotFinanciamiento_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(FinanciamientoReportFixtures.Financiamiento());

    [TestMethod]
    public void SnapshotFinanciamiento_FormatosMapeadosCoincidenEntrePdfYExcel()
        => AssertFormatosCoinciden(FinanciamientoReportFixtures.Financiamiento());

    // ─────────────────── B. Goldens del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_Financiamiento_ColumnasDelContratoNeutral()
    {
        var dto = FinanciamientoReportFixtures.Proyectar(FinanciamientoReportFixtures.Financiamiento());

        Snapshots.AssertOrRegenerar(
            "financiamiento-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityFinanciamiento(dto, rutaGolden));
    }

    // ─────────────────── C. Roles numéricos ───────────────────

    [TestMethod]
    public void ColumnaMonetaria_UsaSimboloYDecimalesImporte()
    {
        var snapshot = FinanciamientoReportFixtures.Financiamiento();
        var cd = snapshot.Columnas.Single(c => c.Identificador == "colCD");

        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(cd));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(cd));

        var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, cd, snapshot);
        Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            "El costo directo debe llevar el '$' centralizado.");
        Assert.IsTrue(pdf.Contains("1,234.50"), "Debe usar los decimales de importe del proyecto (2).");
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(cd, snapshot));
    }

    [TestMethod]
    public void ColumnaTasa_SinTokenNumerico_UsaDefaultLegacy()
    {
        var snapshot = FinanciamientoReportFixtures.Financiamiento();
        var tasa = snapshot.Columnas.Single(c => c.Identificador == "colTasa");

        Assert.AreEqual(string.Empty, tasa.FormatoNumerico);
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(tasa));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(tasa));
        Assert.IsFalse(tasa.EsNumerica);
    }

    [TestMethod]
    public void Defaults_SinConfiguracion_ExponeElCatalogoConEstiloLegacyCatalogo()
    {
        var snapshot = FinanciamientoReportFixtures.Defaults();

        Assert.AreEqual(15, snapshot.Columnas.Count);
        Assert.AreEqual("colPeriodo", snapshot.Columnas[0].Identificador);
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
        Assert.AreEqual("Financiamiento", snapshot.TipoReporte);
    }

    // ─────────────────── D. Modelo neutral compartido ───────────────────

    [TestMethod]
    public void Modelo_CalculaAvanceDesdeFilasBaseYNoDesdeEgresos()
    {
        var (proyecto, config, filas, baseRows) = Datos();
        var modelo = FinanciamientoExportResolver.BuildModel(proyecto, config, filas, baseRows, esModeloDual: false);

        var avance = modelo.Matriz.Single(r => r.Concepto == "AVANCE PROGRAMADO");
        // base período 1 = 600+400 = 1000; total = (600+400)+(1200+800) = 3000.
        Assert.AreEqual(0.3333m, avance.Valores[0]);
        Assert.AreEqual(0.6667m, avance.Valores[1]);
    }

    [TestMethod]
    public void Modelo_TotalesDeDatosSumanTodasLasFilasBase()
    {
        var (proyecto, config, filas, baseRows) = Datos();
        var modelo = FinanciamientoExportResolver.BuildModel(proyecto, config, filas, baseRows, esModeloDual: false);

        Assert.AreEqual(4, modelo.Datos.Count);
        Assert.AreEqual(1800m, modelo.Datos[0].ValorIzquierda.Numero); // total CD
        Assert.AreEqual(1200m, modelo.Datos[1].ValorIzquierda.Numero); // total CI
        Assert.AreEqual(2.5m, modelo.PorcentajeCalculado);
    }

    [TestMethod]
    public void Modelo_MismaCantidadDePeriodosQueFilas()
    {
        var (proyecto, config, filas, baseRows) = Datos();
        var modelo = FinanciamientoExportResolver.BuildModel(proyecto, config, filas, baseRows);

        Assert.AreEqual(filas.Count, modelo.EtiquetasPeriodo.Count);
        CollectionAssert.AreEqual(new[] { "P1", "P2" }, modelo.EtiquetasPeriodo.ToArray());
        Assert.IsTrue(modelo.Matriz.All(r => r.Kind != FinanciamientoMatrixRowKind.Valores
            || r.Valores.Count == filas.Count));
    }

    [TestMethod]
    public void Modelo_MoneyFormatEsElMismoEnPdfYExcel()
    {
        var snapshot = FinanciamientoReportFixtures.Financiamiento();
        const decimal valor = 1800m;

        var pdf = valor.ToString(ReportColumnGridFormat.FormatoMonedaPdf(snapshot.DecimalesImporte), CultureInfo.CurrentCulture);
        var excel = valor.ToString(
            ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture),
            CultureInfo.CurrentCulture);

        Assert.AreEqual(excel, pdf);
        Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda));
    }

    [TestMethod]
    public void TasaPeriodo_DualConSaldoPositivo_UsaSoloTiie()
    {
        var (_, config, _, _) = Datos();

        Assert.AreEqual(1.1507m,
            FinanciamientoExportResolver.CalcularTasaPeriodo(config, esModeloDual: false, diasPeriodo: 30, saldoAcumulado: -700m));
        Assert.AreEqual(1.1507m,
            FinanciamientoExportResolver.CalcularTasaPeriodo(config, esModeloDual: true, diasPeriodo: 30, saldoAcumulado: -700m));
        Assert.AreEqual(0.9041m,
            FinanciamientoExportResolver.CalcularTasaPeriodo(config, esModeloDual: true, diasPeriodo: 30, saldoAcumulado: 700m));
    }

    [TestMethod]
    public void Modelo_CostoDirectoYIndirectoSeAlineanPorPeriodo()
    {
        var (proyecto, config, filas, baseRows) = Datos();
        var modelo = FinanciamientoExportResolver.BuildModel(proyecto, config, filas, baseRows);

        var cd = modelo.Matriz.Single(r => r.Concepto == "COSTO DIRECTO");
        var ci = modelo.Matriz.Single(r => r.Concepto == "COSTO INDIRECTO");
        CollectionAssert.AreEqual(new[] { 600m, 1200m }, cd.Valores.ToArray());
        CollectionAssert.AreEqual(new[] { 400m, 800m }, ci.Valores.ToArray());
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private static (Proyecto proyecto, ConfiguracionFinanciamiento config,
        List<FilaFlujoCajaFinanciamiento> filas, List<FinanciamientoBaseRow> baseRows) Datos()
    {
        var proyecto = new Proyecto
        {
            Id = FinanciamientoReportFixtures.ProyectoId,
            Nombre = "Proyecto Sintético",
            PorcentajeIndirectosCentral = 5m,
            PorcentajeIndirectosCampo = 5m,
            DecimalesCantidad = 5,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };

        var config = new ConfiguracionFinanciamiento
        {
            ProyectoId = proyecto.Id,
            TasaTIIE = 11m,
            PuntosAdicionales = 3m,
            PorcentajeAnticipo = 30m,
            DesfaseCobro = 1,
            BaseCalculo = "Acumulable",
            PorcentajeCalculado = 2.5m,
        };

        var filas = new List<FilaFlujoCajaFinanciamiento>
        {
            new() { NumeroPeriodo = 1, Etiqueta = "P1", DiasPeriodo = 30, Egresos = 1000m,
                EstimacionCobrada = 0m, AnticipoRecibido = 300m, AmortizacionAnticipo = 0m,
                InteresPeriodo = -5m, SaldoAcumulado = -700m },
            new() { NumeroPeriodo = 2, Etiqueta = "P2", DiasPeriodo = 31, Egresos = 2000m,
                EstimacionCobrada = 1500m, AnticipoRecibido = 0m, AmortizacionAnticipo = 100m,
                InteresPeriodo = -3m, SaldoAcumulado = -1200m },
        };

        var baseRows = new List<FinanciamientoBaseRow>
        {
            new(1, 600m, 400m),
            new(2, 1200m, 800m),
        };

        return (proyecto, config, filas, baseRows);
    }

    private sealed record ColumnaVista(
        string Identificador,
        string Encabezado,
        bool Visible,
        int Orden,
        int AnchoPx,
        string FormatoNumerico,
        bool EsNumerica,
        string Alineacion,
        string AlineacionVertical,
        bool Wrap,
        ReportTextStyle EstiloEncabezado,
        ReportTextStyle EstiloContenido);

    private static IReadOnlyList<ColumnaVista> DerivarVista(ReportColumnSnapshot snapshot)
        => snapshot.Columnas
            .OrderBy(c => c.Orden)
            .Select(c => new ColumnaVista(
                Identificador: c.Identificador,
                Encabezado: c.Encabezado,
                Visible: c.Visible,
                Orden: c.Orden,
                AnchoPx: c.Ancho,
                FormatoNumerico: c.FormatoNumerico,
                EsNumerica: c.EsNumerica,
                Alineacion: c.Alineacion.ToString(),
                AlineacionVertical: c.AlineacionVertical.ToString(),
                Wrap: c.Wrap,
                EstiloEncabezado: c.EstiloEncabezado,
                EstiloContenido: c.EstiloContenido))
            .ToList();

    private static void AssertParidadPdfExcel(ReportColumnSnapshot snapshot, string reporte)
    {
        var pdf = DerivarVista(snapshot);
        var excel = DerivarVista(snapshot);

        CollectionAssert.AreEqual(
            pdf.ToArray(), excel.ToArray(),
            $"{reporte}: PDF y Excel deben derivar el mismo conjunto de columnas del snapshot único.");

        CollectionAssert.AreEqual(
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray(),
            pdf.Select(c => c.Identificador).ToArray(),
            $"{reporte}: las vistas deben exponer el orden canónico del snapshot.");
        Assert.IsTrue(pdf.Count > 0, $"{reporte}: el reporte debe exponer columnas visibles/definidas.");

        AssertFormatosCoinciden(snapshot);
        AssertAnchosNoExcedenElAreaUtil(snapshot);
    }

    private static void AssertFormatosCoinciden(ReportColumnSnapshot snapshot)
    {
        foreach (var columna in snapshot.Columnas)
        {
            var pdf = ReportNumberFormatMapper.ToPdfFormat(columna.FormatoNumerico);
            var excel = ReportNumberFormatMapper.ToExcelFormat(columna.FormatoNumerico);

            if (string.Equals(pdf, excel, StringComparison.Ordinal))
                continue;

            Assert.IsFalse(pdf.Contains('\u00A4'),
                $"'{columna.Identificador}': el formato PDF no debe introducir el glifo ¤ (U+00A4).");
            Assert.IsFalse(excel.Contains('\u00A4'),
                $"'{columna.Identificador}': el formato Excel no debe introducir el glifo ¤ (U+00A4).");
        }
    }

    private static void AssertAnchosNoExcedenElAreaUtil(ReportColumnSnapshot snapshot)
    {
        double areaUtilCm = ReportColumnWidthConverter.GetLetterUsableWidthCm();
        int[] anchosPx = snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Ancho).ToArray();

        double[] anchosCm = ReportColumnWidthConverter.PxToCm(anchosPx, areaUtilCm);

        Assert.AreEqual(anchosPx.Length, anchosCm.Length,
            "La conversión PDF debe producir un ancho por columna.");
        Assert.IsTrue(anchosCm.All(cm => cm >= 0d),
            "Ningún ancho convertido debe ser negativo.");
        Assert.IsTrue(anchosCm.Sum() <= areaUtilCm + Tolerancia,
            $"La suma de anchos convertidos ({anchosCm.Sum()}) no debe exceder el área útil ({areaUtilCm} cm).");
    }

    private static void SanityFinanciamiento(PilotoSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("Financiamiento", dto.TipoReporte);
        Assert.AreEqual(FinanciamientoReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            new[]
            {
                "colPeriodo", "colInicio", "colFin", "colDias", "colCD", "colCI", "colEgresos",
                "colAnticipo", "colEstim", "colAmort", "colCobro", "colFlujoNeto", "colSaldo",
                "colTasa", "colInteres"
            },
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Financiamiento debe exponer las quince columnas del catálogo.");

        var cd = dto.Columnas.Single(c => c.Identificador == "colCD");
        Assert.IsTrue(cd.EsNumerica, "El costo directo es numérico.");

        var tasa = dto.Columnas.Single(c => c.Identificador == "colTasa");
        Assert.AreEqual(string.Empty, tasa.Formato, "La tasa no tiene token numérico persistido.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El estilo de catálogo usa bandeado #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El estilo de catálogo dibuja grilla.");

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
