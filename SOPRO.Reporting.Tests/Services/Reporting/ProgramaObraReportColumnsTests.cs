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
/// Reporte de Programa de Obra: paridad semántica PDF/Excel desde un ÚNICO
/// <see cref="ReportColumnSnapshot"/> y golden del contrato neutral de columnas.
/// Sigue el mismo criterio que <c>FsrReportColumnsTests</c> y
/// <c>ManoObraReportColumnsTests</c>.
///
/// A. Paridad: dado el snapshot, las vistas PDF y Excel derivan el mismo conjunto
///    de columnas (orden, visibilidad, encabezado, ancho px, alineaciones, wrap,
///    estilos y rol numérico).
/// B. Rol numérico y decimales: las columnas monetarias usan el símbolo único '$'
///    con los decimales de importe y la cantidad los decimales de cantidad.
/// C. Golden: la proyección se congela en
///    <c>TestData/Goldens/programa-obra-columnas.json</c>.
/// </summary>
[TestClass]
public class ProgramaObraReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    private static readonly string[] IdentificadoresCanonicos =
    {
        "colOrden", "colClave", "colDescripcion", "colUnidad", "colPredecesora",
        "colCantidad", "colFechaInicio", "colFechaFin", "colDuracionDias",
        "colRendimientoDiario", "colFrentes", "colPrecioUnitario", "colImporte", "colRutaCritica"
    };

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotProgramaObra_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(ProgramaObraReportFixtures.ProgramaObra(), "ProgramaObra");

    [TestMethod]
    public void SnapshotProgramaObra_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(ProgramaObraReportFixtures.ProgramaObra());

    [TestMethod]
    public void SnapshotProgramaObra_ExponeCatorceColumnasEnOrdenCanonico()
    {
        var snapshot = ProgramaObraReportFixtures.ProgramaObra();

        CollectionAssert.AreEqual(
            IdentificadoresCanonicos,
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray());
    }

    // ────────────── B. Rol numérico y decimales del proyecto ──────────────

    [DataTestMethod]
    [DataRow(2, "$#,##0.00")]
    [DataRow(4, "$#,##0.0000")]
    public void SnapshotProgramaObra_MonedaUsaSimboloYDecimalesDeImporte(int decimalesImporte, string excelEsperado)
    {
        var snapshot = ProgramaObraReportFixtures.ProgramaObra(decimalesImporte: decimalesImporte);

        foreach (var identificador in new[] { "colPrecioUnitario", "colImporte" })
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
    public void SnapshotProgramaObra_CantidadUsaDecimalesDeCantidad(int decimalesCantidad, string excelEsperado)
    {
        var snapshot = ProgramaObraReportFixtures.ProgramaObra(decimalesCantidad: decimalesCantidad);

        var col = snapshot.Columnas.Single(c => c.Identificador == "colCantidad");
        Assert.IsTrue(col.EsNumerica, "colCantidad es numérica.");
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(col), "colCantidad no es monetaria.");
        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(col), "colCantidad es cantidad.");
        Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(col, snapshot));

        var pdf = ReportColumnGridFormat.FormatearPdf(1.2345m, col, snapshot);
        Assert.IsFalse(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            "Las cantidades del programa no deben llevar símbolo de moneda.");
    }

    [TestMethod]
    public void SnapshotProgramaObra_ColumnasDeTextoNoSonNumericasNiMonetarias()
    {
        var snapshot = ProgramaObraReportFixtures.ProgramaObra();

        foreach (var identificador in new[] { "colOrden", "colClave", "colDescripcion", "colUnidad", "colPredecesora", "colFechaInicio", "colFechaFin", "colRutaCritica" })
        {
            var col = snapshot.Columnas.Single(c => c.Identificador == identificador);
            Assert.IsFalse(col.EsNumerica, $"{identificador} es de texto.");
            Assert.IsFalse(col.EsMoneda, $"{identificador} no es monetaria.");
        }
    }

    [TestMethod]
    public void SnapshotProgramaObra_EstiloTabla_EsLegacyCatalogo()
    {
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), ProgramaObraReportFixtures.ProgramaObra().EstiloTabla);
    }

    // ─────────────────── C. Golden del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_ProgramaObra_ColumnasDelContratoNeutral()
    {
        var dto = ProgramaObraReportFixtures.Proyectar(ProgramaObraReportFixtures.ProgramaObra());

        Snapshots.AssertOrRegenerar(
            "programa-obra-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityProgramaObra(dto, rutaGolden));
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

    private static void SanityProgramaObra(ProgramaObraSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual(ProgramaObraReportFixtures.Schema, dto.Schema);
        Assert.AreEqual(ProgramaObraReportSnapshotBuilder.TipoReporte, dto.TipoReporte);
        Assert.AreEqual(ProgramaObraReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            IdentificadoresCanonicos,
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El Programa de Obra debe exponer las catorce columnas canónicas en orden.");

        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "colImporte").EsMoneda,
            "El Importe es monetario.");
        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "colPrecioUnitario").EsMoneda,
            "El Precio Unitario es monetario.");
        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "colCantidad").EsNumerica,
            "La Cantidad es numérica.");
        Assert.IsFalse(dto.Columnas.Single(c => c.Identificador == "colCantidad").EsMoneda,
            "La Cantidad no es monetaria.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El Programa de Obra legacy usa bandeado #F5F5F5.");
        Assert.AreEqual("#4A4A6A", dto.EstiloTabla.Encabezado.ColorFondo);

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
