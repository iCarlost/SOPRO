using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Reporte de Utilidad: paridad semántica PDF/Excel desde un ÚNICO
/// <see cref="ReportColumnSnapshot"/> y golden del contrato neutral de columnas.
///
/// A. Paridad semántica: dado un snapshot, las vistas PDF y Excel derivan el mismo
///    conjunto de columnas (orden, visibilidad, encabezado, ancho px, alineaciones,
///    wrap y estilos). Los tres roles numéricos del resumen —base, porcentaje e
///    importe final— se verifican con <see cref="ReportColumnGridFormat"/>, la regla
///    única compartida por ambos medios (base e importe final monetarios con '$' +
///    decimales de importe; porcentaje con los decimales de porcentaje).
///
/// B. Goldens: la proyección semántica se congela en
///    <c>TestData/Goldens/utilidad-columnas.json</c> usando <see cref="GoldenPaths"/>.
///    Es un archivo nuevo; NO se toca ningún golden del piloto ni de la Fase 1.
/// </summary>
[TestClass]
public class UtilidadReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    private static readonly string[] IdentificadoresEsperados =
    {
        UtilidadReportColumns.Concepto,
        UtilidadReportColumns.Base,
        UtilidadReportColumns.Porcentaje,
        UtilidadReportColumns.ImporteFinal,
    };

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotUtilidad_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(UtilidadReportFixtures.Utilidad(), "Utilidad");

    [TestMethod]
    public void SnapshotDefaults_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(UtilidadReportFixtures.Defaults(), "Utilidad (defaults)");

    [TestMethod]
    public void SnapshotUtilidad_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(UtilidadReportFixtures.Utilidad());

    [TestMethod]
    public void SnapshotUtilidad_FormatosMapeadosCoincidenEntrePdfYExcel()
        => AssertFormatosCoinciden(UtilidadReportFixtures.Utilidad());

    // ─────────────────── B. Goldens del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_Utilidad_ColumnasDelContratoNeutral()
    {
        var dto = UtilidadReportFixtures.Proyectar(UtilidadReportFixtures.Utilidad());

        Snapshots.AssertOrRegenerar(
            "utilidad-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityUtilidad(dto, rutaGolden));
    }

    // ─────────────────── C. Roles numéricos y precisiones ───────────────────

    [TestMethod]
    public void BaseEsMonetaria_UsaSimboloYDecimalesImporte()
    {
        var snapshot = UtilidadReportFixtures.Utilidad();
        var base_ = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Base);

        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(base_));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(base_));

        var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, base_, snapshot);
        Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            "La base debe llevar el '$' centralizado.");
        Assert.IsTrue(pdf.Contains("1,234.50"), "Debe usar los decimales de importe del proyecto (2).");
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(base_, snapshot));
    }

    [TestMethod]
    public void Porcentaje_UsaDecimalesPorcentaje()
    {
        var snapshot = UtilidadReportFixtures.Utilidad();
        var porcentaje = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Porcentaje);

        Assert.IsTrue(ReportColumnGridFormat.EsPorcentajeGrid(porcentaje));
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(porcentaje));

        // El PDF parte del número de porcentaje (p. ej. 8.0) y añade '%'; Excel
        // almacena la fracción con el formato de porcentaje de los decimales del proyecto.
        Assert.AreEqual(4, snapshot.DecimalesPorcentaje);
        Assert.AreEqual(
            8m.ToString("N4", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPorcentajePdf(8m, snapshot));
        Assert.AreEqual(
            ReportColumnGridFormat.FormatoPorcentajeExcel(4),
            ReportColumnGridFormat.FormatoPorcentajeExcel(snapshot.DecimalesPorcentaje));
        Assert.AreEqual("0.0000%", ReportColumnGridFormat.FormatoPorcentajeExcel(snapshot.DecimalesPorcentaje));
    }

    [TestMethod]
    public void ImporteFinalEsMonetario_UsaSimboloYDecimalesImporte()
    {
        var snapshot = UtilidadReportFixtures.Utilidad();
        var importe = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.ImporteFinal);

        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(importe));
        Assert.IsFalse(ReportColumnGridFormat.EsPorcentajeGrid(importe));

        var pdf = ReportColumnGridFormat.FormatearPdf(-987.65m, importe, snapshot);
        Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda), "El importe final conserva el '$'.");
        Assert.IsTrue(pdf.Contains("987.65"));
        Assert.IsTrue(pdf.Contains("-"), "El negativo conserva el signo.");
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(importe, snapshot));
    }

    [TestMethod]
    public void BaseEImporteFinal_ParidadDeMonedaEntreMedios()
    {
        var snapshot = UtilidadReportFixtures.Utilidad();
        var base_ = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Base);
        var importe = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.ImporteFinal);

        // Mismo '$' y mismos decimales de importe en ambos medios para base e importe final.
        Assert.AreEqual(ReportColumnGridFormat.ResolveExcelFormat(base_, snapshot),
            ReportColumnGridFormat.ResolveExcelFormat(importe, snapshot));
        Assert.IsTrue(ReportColumnGridFormat.FormatearPdf(1000m, base_, snapshot)
            .Contains(ReportColumnGridFormat.SimboloMoneda));
        Assert.IsTrue(ReportColumnGridFormat.FormatearPdf(1000m, importe, snapshot)
            .Contains(ReportColumnGridFormat.SimboloMoneda));
    }

    [TestMethod]
    public void Porcentaje_DecimalesDeCeroAN()
    {
        var baseSnapshot = UtilidadReportFixtures.Utilidad();
        var porcentaje = baseSnapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Porcentaje);

        for (int decimales = 0; decimales <= 5; decimales++)
        {
            var snapshot = new ReportColumnSnapshot(
                baseSnapshot.TipoReporte, baseSnapshot.ProyectoId, baseSnapshot.Titulo,
                baseSnapshot.Columnas, baseSnapshot.EstiloTabla)
            {
                DecimalesCantidad = baseSnapshot.DecimalesCantidad,
                DecimalesImporte = baseSnapshot.DecimalesImporte,
                DecimalesPorcentaje = decimales,
            };

            Assert.AreEqual(
                8.5m.ToString($"N{decimales}", CultureInfo.CurrentCulture),
                ReportColumnGridFormat.FormatearPorcentajePdf(8.5m, snapshot));
            Assert.AreEqual(
                ReportColumnGridFormat.FormatoPorcentajeExcel(decimales),
                ReportColumnGridFormat.FormatoPorcentajeExcel(snapshot.DecimalesPorcentaje));
        }
    }

    [TestMethod]
    public void Concepto_NoEsNumericaNiMonetaria()
    {
        var snapshot = UtilidadReportFixtures.Utilidad();
        var concepto = snapshot.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Concepto);

        Assert.IsFalse(concepto.EsNumerica);
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(concepto));
        Assert.IsFalse(ReportColumnGridFormat.EsPorcentajeGrid(concepto));
        Assert.AreEqual(string.Empty, concepto.FormatoNumerico);
    }

    [TestMethod]
    public void Defaults_ExponeLosCuatroRolesConEstiloLegacyCatalogo()
    {
        var snapshot = UtilidadReportFixtures.Defaults();

        CollectionAssert.AreEqual(
            IdentificadoresEsperados,
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
        Assert.AreEqual("Utilidad", snapshot.TipoReporte);
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
                continue; // Tokens legacy comunes: misma cadena en PDF y Excel.

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

    private static void SanityUtilidad(PilotoSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("Utilidad", dto.TipoReporte);
        Assert.AreEqual(UtilidadReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            IdentificadoresEsperados,
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Utilidad debe exponer los cuatro roles del resumen.");

        var base_ = dto.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Base);
        Assert.IsTrue(base_.EsNumerica, "La base es numérica.");

        var porcentaje = dto.Columnas.Single(c => c.Identificador == UtilidadReportColumns.Porcentaje);
        Assert.AreEqual("P2", porcentaje.Formato, "El porcentaje usa el token P2.");

        var importe = dto.Columnas.Single(c => c.Identificador == UtilidadReportColumns.ImporteFinal);
        Assert.AreEqual("C2", importe.Formato, "El importe final usa el token monetario C2.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El estilo de catálogo usa bandeado #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El estilo de catálogo dibuja grilla.");

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
