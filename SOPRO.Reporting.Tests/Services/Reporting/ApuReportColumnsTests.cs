using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Reporte de APU (Análisis de Precios Unitarios): paridad semántica PDF/Excel
/// desde un ÚNICO <see cref="ReportColumnSnapshot"/> y golden del contrato neutral
/// de columnas.
///
/// A. Paridad semántica: dado un snapshot, las vistas PDF y Excel derivan el mismo
///    conjunto de columnas (orden, visibilidad, encabezado, ancho px,
///    alineaciones, wrap y estilos). Los roles numéricos (Cantidad con decimales
///    de cantidad; Costo Unit. e Importe monetarios con '$' + decimales de importe)
///    se verifican con <see cref="ReportColumnGridFormat"/>, la regla única
///    compartida por ambos medios.
///
/// B. Goldens: la proyección semántica se congela en
///    <c>TestData/Goldens/apu-columnas.json</c> usando <see cref="GoldenPaths"/>.
///    Es un archivo nuevo; NO se toca ningún golden del piloto ni de la Fase 1.
/// </summary>
[TestClass]
public class ApuReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotApu_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(ApuReportFixtures.Apu(), "APU");

    [TestMethod]
    public void SnapshotDefaults_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(ApuReportFixtures.Defaults(), "APU (defaults)");

    [TestMethod]
    public void SnapshotApu_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(ApuReportFixtures.Apu());

    [TestMethod]
    public void SnapshotApu_FormatosMapeadosCoincidenEntrePdfYExcel()
        => AssertFormatosCoinciden(ApuReportFixtures.Apu());

    // ─────────────────── B. Goldens del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_Apu_ColumnasDelContratoNeutral()
    {
        var dto = ApuReportFixtures.Proyectar(ApuReportFixtures.Apu());

        Snapshots.AssertOrRegenerar(
            "apu-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityApu(dto, rutaGolden));
    }

    // ─────────────────── C. Roles numéricos y precisiones ───────────────────

    [TestMethod]
    public void CostoUnitarioEImporteSonMonetarios_UsanSimboloYDecimalesImporte()
    {
        var snapshot = ApuReportFixtures.Apu();
        var costo = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");

        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(costo));
        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(importe));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(costo));

        var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, costo, snapshot);
        Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda), "El Costo Unitario debe llevar el '$' centralizado.");
        Assert.IsTrue(pdf.Contains("1,234.50"), "Debe usar los decimales de importe del proyecto (2).");
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(costo, snapshot));
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(importe, snapshot));
    }

    [TestMethod]
    public void CantidadUsaDecimalesDeCantidadDelProyecto()
    {
        var snapshot = ApuReportFixtures.Apu();
        var cantidad = snapshot.Columnas.Single(c => c.Identificador == "Cantidad");

        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(cantidad));
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(cantidad));
        Assert.AreEqual(5, snapshot.DecimalesCantidad);

        Assert.AreEqual(
            123.45678m.ToString("N5", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPdf(123.45678m, cantidad, snapshot));
        Assert.AreEqual("#,##0.00000", ReportColumnGridFormat.ResolveExcelFormat(cantidad, snapshot));
    }

    [TestMethod]
    public void Cantidad_DecimalesDeCeroAN()
    {
        var baseSnapshot = ApuReportFixtures.Apu();
        var cantidad = baseSnapshot.Columnas.Single(c => c.Identificador == "Cantidad");

        for (int decimales = 0; decimales <= 5; decimales++)
        {
            var snapshot = new ReportColumnSnapshot(
                baseSnapshot.TipoReporte, baseSnapshot.ProyectoId, baseSnapshot.Titulo,
                baseSnapshot.Columnas, baseSnapshot.EstiloTabla)
            {
                DecimalesCantidad = decimales,
                DecimalesImporte = baseSnapshot.DecimalesImporte,
                DecimalesPorcentaje = baseSnapshot.DecimalesPorcentaje,
            };

            Assert.AreEqual(
                12.5m.ToString($"N{decimales}", CultureInfo.CurrentCulture),
                ReportColumnGridFormat.FormatearPdf(12.5m, cantidad, snapshot));
            Assert.AreEqual(
                ReportColumnGridFormat.FormatoNumeroExcel(decimales),
                ReportColumnGridFormat.ResolveExcelFormat(cantidad, snapshot));
        }
    }

    [TestMethod]
    public void Monetario_CeroYNegativo()
    {
        var snapshot = ApuReportFixtures.Apu();
        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");

        var cero = ReportColumnGridFormat.FormatearPdf(0m, importe, snapshot);
        Assert.IsTrue(cero.Contains(ReportColumnGridFormat.SimboloMoneda));
        Assert.IsTrue(cero.Contains("0.00"));

        var negativo = ReportColumnGridFormat.FormatearPdf(-1234.5m, importe, snapshot);
        Assert.IsTrue(negativo.Contains(ReportColumnGridFormat.SimboloMoneda), "El negativo conserva el '$'.");
        Assert.IsTrue(negativo.Contains("1,234.50"));
        Assert.IsTrue(negativo.Contains("-"), "El negativo conserva el signo.");
    }

    [TestMethod]
    public void ColumnasTexto_SinFormatoNiRol_NoSonNumericas()
    {
        var snapshot = ApuReportFixtures.Apu();

        foreach (var id in new[] { "Tipo", "Clave", "Descripcion", "Unidad" })
        {
            var col = snapshot.Columnas.Single(c => c.Identificador == id);
            Assert.IsFalse(col.EsNumerica, $"'{id}' debe ser texto.");
            Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(col));
            Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(col));
        }
    }

    [TestMethod]
    public void Defaults_SinConfiguracion_ExponeElCatalogoDelApuConEstiloLegacyCatalogo()
    {
        var snapshot = ApuReportFixtures.Defaults();

        CollectionAssert.AreEqual(
            new[] { "Tipo", "Clave", "Descripcion", "Unidad", "Cantidad", "PrecioUnitario", "ImporteTotal" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
        Assert.AreEqual("APU", snapshot.TipoReporte);
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

    private static void SanityApu(ApuSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("APU", dto.TipoReporte);
        Assert.AreEqual(ApuReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            new[] { "Tipo", "Clave", "Descripcion", "Unidad", "Cantidad", "PrecioUnitario", "ImporteTotal" },
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden del APU debe exponer las siete columnas del reporte.");

        var cantidad = dto.Columnas.Single(c => c.Identificador == "Cantidad");
        Assert.IsTrue(cantidad.EsNumerica, "La Cantidad es numérica.");
        Assert.IsFalse(cantidad.EsMoneda, "La Cantidad no es monetaria.");

        var costo = dto.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(costo.EsMoneda, "El Costo Unitario es monetario.");

        var importe = dto.Columnas.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(importe.EsMoneda, "El Importe es monetario.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El estilo de catálogo usa bandeado #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El estilo de catálogo dibuja grilla.");

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
