using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Reporte de Programa de Insumos: paridad semántica PDF/Excel desde un ÚNICO
/// <see cref="ReportColumnSnapshot"/> y golden del contrato neutral de columnas.
/// Sigue el mismo criterio que <c>ProgramaObraReportColumnsTests</c>.
///
/// A. Paridad: dado el snapshot, las vistas PDF y Excel derivan el mismo conjunto
///    de columnas (estáticas y series dinámicas por período), con el mismo orden,
///    visibilidad, encabezado, ancho px, alineaciones, wrap, estilos y rol numérico.
/// B. Rol numérico y decimales: las columnas de importe usan el símbolo único '$'
///    con los decimales de importe y las de cantidad los decimales de cantidad.
/// C. Golden: la proyección se congela en
///    <c>TestData/Goldens/programa-insumos-columnas.json</c>.
/// </summary>
[TestClass]
public class ProgramaInsumosReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    private static readonly string[] IdentificadoresCanonicos =
    {
        "colClave", "colDescripcion", "colUnidad", "colInicio", "colTermino", "colDondeSeUsa",
        "colPU", "colTotal", "colImporteTotal",
        "per_501", "acu_501", "imp_501", "iacu_501",
        "per_502", "acu_502", "imp_502", "iacu_502"
    };

    private static readonly string[] ColumnasMonetarias = { "colPU", "colImporteTotal", "imp_501", "imp_502", "iacu_501", "iacu_502" };
    private static readonly string[] ColumnasCantidad = { "colTotal", "per_501", "per_502", "acu_501", "acu_502" };

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotProgramaInsumos_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(ProgramaInsumosReportFixtures.ProgramaInsumos(), "ProgramaInsumos");

    [TestMethod]
    public void SnapshotProgramaInsumos_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(ProgramaInsumosReportFixtures.ProgramaInsumos());

    [TestMethod]
    public void SnapshotProgramaInsumos_ExponeLasColumnasEnOrdenCanonico()
    {
        var snapshot = ProgramaInsumosReportFixtures.ProgramaInsumos();

        CollectionAssert.AreEqual(
            IdentificadoresCanonicos,
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void SnapshotProgramaInsumos_IncluyeSeriesDinamicasDePeriodo()
    {
        var snapshot = ProgramaInsumosReportFixtures.ProgramaInsumos();

        foreach (var identificador in new[] { "per_501", "acu_501", "imp_501", "iacu_501", "per_502", "acu_502", "imp_502", "iacu_502" })
            Assert.IsTrue(snapshot.Columnas.Any(c => c.Identificador == identificador), $"Falta la columna dinámica {identificador}.");
    }

    // ────────────── B. Rol numérico y decimales del proyecto ──────────────

    [DataTestMethod]
    [DataRow(2, "$#,##0.00")]
    [DataRow(4, "$#,##0.0000")]
    public void SnapshotProgramaInsumos_ImportesUsanSimboloYDecimalesDeImporte(int decimalesImporte, string excelEsperado)
    {
        var snapshot = ProgramaInsumosReportFixtures.ProgramaInsumos(decimalesImporte: decimalesImporte);

        foreach (var identificador in ColumnasMonetarias)
        {
            var col = snapshot.Columnas.Single(c => c.Identificador == identificador);
            Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(col), $"{identificador} es monetario.");
            Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(col, snapshot));

            var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, col, snapshot);
            Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
                $"El PDF monetario debe llevar el símbolo centralizado. Obtenido: '{pdf}'.");
            Assert.IsFalse(pdf.Contains('\u00A4'), "El PDF monetario no debe colar el glifo ¤.");
        }
    }

    [DataTestMethod]
    [DataRow(2, "#,##0.00")]
    [DataRow(4, "#,##0.0000")]
    public void SnapshotProgramaInsumos_CantidadesUsanDecimalesDeCantidad(int decimalesCantidad, string excelEsperado)
    {
        var snapshot = ProgramaInsumosReportFixtures.ProgramaInsumos(decimalesCantidad: decimalesCantidad);

        foreach (var identificador in ColumnasCantidad)
        {
            var col = snapshot.Columnas.Single(c => c.Identificador == identificador);
            Assert.IsTrue(col.EsNumerica, $"{identificador} es numérica.");
            Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(col), $"{identificador} no es monetaria.");
            Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(col), $"{identificador} es cantidad.");
            Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(col, snapshot));

            var pdf = ReportColumnGridFormat.FormatearPdf(1.2345m, col, snapshot);
            Assert.IsFalse(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
                "Las cantidades del programa de insumos no deben llevar símbolo de moneda.");
        }
    }

    [TestMethod]
    public void SnapshotProgramaInsumos_ColumnasDeTextoNoSonNumericasNiMonetarias()
    {
        var snapshot = ProgramaInsumosReportFixtures.ProgramaInsumos();

        foreach (var identificador in new[] { "colClave", "colDescripcion", "colUnidad", "colInicio", "colTermino", "colDondeSeUsa" })
        {
            var col = snapshot.Columnas.Single(c => c.Identificador == identificador);
            Assert.IsFalse(col.EsNumerica, $"{identificador} es de texto.");
            Assert.IsFalse(col.EsMoneda, $"{identificador} no es monetaria.");
        }
    }

    [TestMethod]
    public void SnapshotProgramaInsumos_EstiloTabla_EsLegacyCatalogo()
    {
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), ProgramaInsumosReportFixtures.ProgramaInsumos().EstiloTabla);
    }

    // ─────────────────── C. Golden del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_ProgramaInsumos_ColumnasDelContratoNeutral()
    {
        var dto = ProgramaInsumosReportFixtures.Proyectar(ProgramaInsumosReportFixtures.ProgramaInsumos());

        Snapshots.AssertOrRegenerar(
            "programa-insumos-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityProgramaInsumos(dto, rutaGolden));
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private sealed record ColumnaVista(
        string Identificador,
        string Encabezado,
        bool Visible,
        int Orden,
        int AnchoPx,
        string FormatoNumerico,
        bool EsNumerica,
        bool EsMoneda,
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
                EsMoneda: c.EsMoneda,
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

        Assert.IsTrue(pdf.Count > 0, $"{reporte}: el reporte debe exponer columnas.");
        AssertAnchosNoExcedenElAreaUtil(snapshot);
    }

    private static void AssertAnchosNoExcedenElAreaUtil(ReportColumnSnapshot snapshot)
    {
        double areaUtilCm = ReportColumnWidthConverter.GetLetterUsableWidthCm(landscape: true);
        int[] anchosPx = snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Ancho).ToArray();

        double[] anchosCm = ReportColumnWidthConverter.PxToCm(anchosPx, areaUtilCm);

        Assert.AreEqual(anchosPx.Length, anchosCm.Length,
            "La conversión PDF debe producir un ancho por columna.");
        Assert.IsTrue(anchosCm.All(cm => cm >= 0d),
            "Ningún ancho convertido debe ser negativo.");
        Assert.IsTrue(anchosCm.Sum() <= areaUtilCm + Tolerancia,
            $"La suma de anchos convertidos ({anchosCm.Sum()}) no debe exceder el área útil ({areaUtilCm} cm).");
    }

    private static void SanityProgramaInsumos(ProgramaInsumosSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual(ProgramaInsumosReportFixtures.Schema, dto.Schema);
        Assert.AreEqual(ProgramaInsumosReportSnapshotBuilder.TipoReporte, dto.TipoReporte);
        Assert.AreEqual(ProgramaInsumosReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            IdentificadoresCanonicos,
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El Programa de Insumos debe exponer las columnas canónicas en orden.");

        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "colImporteTotal").EsMoneda,
            "El Importe total es monetario.");
        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "colPU").EsMoneda,
            "El Precio Unitario es monetario.");
        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "imp_501").EsMoneda,
            "El importe por período es monetario.");
        Assert.IsFalse(dto.Columnas.Single(c => c.Identificador == "colTotal").EsMoneda,
            "El Total (cantidad) no es monetario.");
        Assert.IsFalse(dto.Columnas.Single(c => c.Identificador == "per_501").EsMoneda,
            "La cantidad por período no es monetaria.");
        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "per_501").EsNumerica,
            "La cantidad por período es numérica.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El Programa de Insumos legacy usa bandeado #F5F5F5.");
        Assert.AreEqual("#4A4A6A", dto.EstiloTabla.Encabezado.ColorFondo);

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
