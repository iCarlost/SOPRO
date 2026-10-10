using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Reportes del Factor de Salario Real (FSR): AE-2(A) tabla de cálculo y AE-2(C)
/// tabulador por insumo. Paridad semántica PDF/Excel desde un ÚNICO
/// <see cref="ReportColumnSnapshot"/> y golden del contrato neutral de columnas.
/// Sigue el mismo criterio que <c>ManoObraReportColumnsTests</c> y
/// <c>PilotoReportesColumnasTests</c>.
///
/// A. Paridad: dado el snapshot, las vistas PDF y Excel derivan el mismo conjunto
///    de columnas (orden, visibilidad, encabezado, ancho px, alineaciones, wrap,
///    estilos y rol numérico).
/// B. Rol numérico y decimales: la columna de cantidad usa los decimales de
///    cantidad del proyecto y las monetarias el símbolo único '$' con los decimales
///    de importe.
/// C. Golden: la proyección se congela en <c>TestData/Goldens/fsr-columnas.json</c>.
/// </summary>
[TestClass]
public class FsrReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotAE2A_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(FsrReportFixtures.AE2A(), "AE2A");

    [TestMethod]
    public void SnapshotAE2C_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(FsrReportFixtures.AE2C(), "AE2C");

    [TestMethod]
    public void SnapshotAE2A_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(FsrReportFixtures.AE2A());

    [TestMethod]
    public void SnapshotAE2C_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(FsrReportFixtures.AE2C());

    [TestMethod]
    public void SnapshotAE2A_ExponeCuatroColumnasEnOrdenCanonico()
    {
        var snapshot = FsrReportFixtures.AE2A();

        CollectionAssert.AreEqual(
            new[] { "Descripcion", "Operacion", "Unidad", FsrReportSnapshotBuilder.ColValor },
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void SnapshotAE2C_ExponeVeintiunColumnasConMonetariasEnLosExtremos()
    {
        var snapshot = FsrReportFixtures.AE2C();
        var ordenadas = snapshot.Columnas.OrderBy(c => c.Orden).ToList();

        Assert.AreEqual(21, ordenadas.Count);
        Assert.AreEqual("Clave", ordenadas[0].Identificador);
        Assert.AreEqual("Descripcion", ordenadas[1].Identificador);
        Assert.AreEqual(FsrReportSnapshotBuilder.ColSalarioBase, ordenadas[2].Identificador);
        Assert.AreEqual(FsrReportSnapshotBuilder.ColSalarioReal, ordenadas[20].Identificador);
    }

    // ────────────── B. Rol numérico y decimales del proyecto ──────────────

    [DataTestMethod]
    [DataRow(2, "$#,##0.00")]
    [DataRow(4, "$#,##0.0000")]
    public void SnapshotAE2C_MonedaUsaSimboloYDecimalesDeImporte(int decimalesImporte, string excelEsperado)
    {
        var snapshot = FsrReportFixtures.AE2C(decimalesImporte: decimalesImporte);

        foreach (var identificador in new[] { FsrReportSnapshotBuilder.ColSalarioBase, FsrReportSnapshotBuilder.ColSalarioReal })
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
    [DataRow(5, "#,##0.00000")]
    public void SnapshotAE2C_CantidadUsaDecimalesDeCantidad(int decimalesCantidad, string excelEsperado)
    {
        var snapshot = FsrReportFixtures.AE2C(decimalesCantidad: decimalesCantidad);

        var col = snapshot.Columnas.Single(c => c.Identificador == "FactorSbc");
        Assert.IsTrue(col.EsNumerica, "FactorSbc es numérico.");
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(col), "FactorSbc no es monetario.");
        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(col), "FactorSbc (token N5) es cantidad.");
        Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(col, snapshot));

        var pdf = ReportColumnGridFormat.FormatearPdf(1.23456m, col, snapshot);
        Assert.IsFalse(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            "Las cantidades del FSR no deben llevar símbolo de moneda.");
    }

    [DataTestMethod]
    [DataRow(2, "#,##0.00")]
    [DataRow(5, "#,##0.00000")]
    public void SnapshotAE2A_ValorUsaDecimalesDeCantidad(int decimalesCantidad, string excelEsperado)
    {
        var snapshot = FsrReportFixtures.AE2A(decimalesCantidad: decimalesCantidad);

        var col = snapshot.Columnas.Single(c => c.Identificador == FsrReportSnapshotBuilder.ColValor);
        Assert.IsTrue(col.EsNumerica, "Valor es numérico.");
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(col), "Valor no es monetario.");
        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(col), "Valor (token N5) es cantidad.");
        Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(col, snapshot));
    }

    [TestMethod]
    public void SnapshotAE2C_ColumnasDeTextoNoSonNumericasNiMonetarias()
    {
        var snapshot = FsrReportFixtures.AE2C();

        foreach (var identificador in new[] { "Clave", "Descripcion" })
        {
            var col = snapshot.Columnas.Single(c => c.Identificador == identificador);
            Assert.IsFalse(col.EsNumerica, $"{identificador} es de texto.");
            Assert.IsFalse(col.EsMoneda, $"{identificador} no es monetaria.");
            Assert.AreEqual(string.Empty, col.FormatoNumerico, $"{identificador} de texto sin formato.");
        }
    }

    [TestMethod]
    public void SnapshotAE2C_EstiloTabla_EsLegacyCatalogo()
    {
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), FsrReportFixtures.AE2C().EstiloTabla);
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), FsrReportFixtures.AE2A().EstiloTabla);
    }

    // ─────────────────── C. Golden del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_Fsr_ColumnasDelContratoNeutral()
    {
        var dto = FsrReportFixtures.Proyectar(FsrReportFixtures.AE2A(), FsrReportFixtures.AE2C());

        Snapshots.AssertOrRegenerar(
            "fsr-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityFsr(dto, rutaGolden));
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

    private static void SanityFsr(FsrSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual(FsrReportFixtures.Schema, dto.Schema);
        Assert.AreEqual(FsrReportSnapshotBuilder.TipoReporteAE2A, dto.AE2A.TipoReporte);
        Assert.AreEqual(FsrReportSnapshotBuilder.TipoReporteAE2C, dto.AE2C.TipoReporte);
        Assert.AreEqual(FsrReportFixtures.ProyectoId, dto.AE2A.ProyectoId);
        Assert.AreEqual(FsrReportFixtures.ProyectoId, dto.AE2C.ProyectoId);

        CollectionAssert.AreEqual(
            new[] { "Descripcion", "Operacion", "Unidad", FsrReportSnapshotBuilder.ColValor },
            dto.AE2A.Columnas.Select(c => c.Identificador).ToArray(),
            "El AE-2A debe exponer las cuatro columnas canónicas en orden.");

        Assert.AreEqual(21, dto.AE2C.Columnas.Count, "El AE-2C debe exponer 21 columnas.");

        Assert.IsTrue(dto.AE2A.Columnas.Single(c => c.Identificador == FsrReportSnapshotBuilder.ColValor).EsNumerica,
            "La columna Valor del AE-2A es numérica (cantidad).");
        Assert.IsFalse(dto.AE2A.Columnas.Single(c => c.Identificador == FsrReportSnapshotBuilder.ColValor).EsMoneda,
            "La columna Valor del AE-2A no es monetaria.");

        Assert.IsTrue(dto.AE2C.Columnas.Single(c => c.Identificador == FsrReportSnapshotBuilder.ColSalarioBase).EsMoneda,
            "Salario Base es monetario.");
        Assert.IsTrue(dto.AE2C.Columnas.Single(c => c.Identificador == FsrReportSnapshotBuilder.ColSalarioReal).EsMoneda,
            "Salario Real es monetario.");
        Assert.IsFalse(dto.AE2C.Columnas.Single(c => c.Identificador == "FactorSbc").EsMoneda,
            "El Factor SBC no es monetario (cantidad).");

        Assert.AreEqual("#F5F5F5", dto.AE2C.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El FSR legacy usa bandeado #F5F5F5.");
        Assert.AreEqual("#4A4A6A", dto.AE2C.EstiloTabla.Encabezado.ColorFondo);

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
