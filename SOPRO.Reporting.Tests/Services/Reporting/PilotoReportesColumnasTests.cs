using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Piloto de reportes (Presupuesto + Catálogo de Materiales): paridad semántica
/// PDF/Excel desde un ÚNICO <see cref="ReportColumnSnapshot"/> y goldens del
/// contrato neutral de columnas.
///
/// A. Paridad semántica: dado un snapshot, las vistas PDF y Excel derivan el
///    mismo conjunto de columnas (orden, visibilidad, encabezado, ancho px,
///    formato y estilos). La conversión de ancho se apoya en los helpers
///    neutrales <see cref="ReportColumnWidthConverter"/> y
///    <see cref="ReportNumberFormatMapper"/>, únicas piezas compartidas por los
///    renderizadores.
///
/// B. Goldens: la proyección semántica se congela en
///    <c>TestData/Goldens/piloto-*-columnas.json</c> usando el mecanismo
///    <see cref="GoldenPaths"/> (<c>SOPRO_REGENERATE_GOLDENS=1</c>). Son archivos
///    nuevos; NO se toca ni regenera ningún golden del catálogo de matrices.
///
/// C. Hash PDF normalizado: DIFERIDO. No existe un renderer neutral de
///    Presupuesto/Materiales en <c>SOPRO.Reporting</c> (sólo el del catálogo de
///    matrices) y los de WinForms no son invocables desde este proyecto. Se cubre
///    en verificación posterior sin referenciar WinForms ni crear proyectos.
/// </summary>
[TestClass]
public class PilotoReportesColumnasTests
{
    private const double Tolerancia = 1e-9;

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotPresupuesto_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(PilotoReportesFixtures.Presupuesto(), "Presupuesto");

    [TestMethod]
    public void SnapshotMateriales_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(PilotoReportesFixtures.Materiales(), "Materiales");

    [TestMethod]
    public void SnapshotPresupuesto_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(PilotoReportesFixtures.Presupuesto());

    [TestMethod]
    public void SnapshotMateriales_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(PilotoReportesFixtures.Materiales());

    [TestMethod]
    public void SnapshotPresupuesto_FormatosMapeadosCoincidenEntrePdfYExcel()
        => AssertFormatosCoinciden(PilotoReportesFixtures.Presupuesto());

    [TestMethod]
    public void SnapshotMateriales_FormatosMapeadosCoincidenEntrePdfYExcel()
        => AssertFormatosCoinciden(PilotoReportesFixtures.Materiales());

    // ─────────────────── B. Goldens del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_Presupuesto_ColumnasDelContratoNeutral()
    {
        var dto = PilotoReportesFixtures.Proyectar(PilotoReportesFixtures.Presupuesto());

        Snapshots.AssertOrRegenerar(
            "piloto-presupuesto-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityPresupuesto(dto, rutaGolden));
    }

    [TestMethod]
    public void Golden_Materiales_ColumnasDelContratoNeutral()
    {
        var dto = PilotoReportesFixtures.Proyectar(PilotoReportesFixtures.Materiales());

        Snapshots.AssertOrRegenerar(
            "piloto-materiales-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityMateriales(dto, rutaGolden));
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private enum Medio
    {
        Pdf,
        Excel,
    }

    /// <summary>
    /// Vista neutral de una columna tal como la consumiría un medio concreto: los
    /// campos de contrato comunes más el formato ya traducido por el helper del
    /// medio. La única diferencia legítima entre PDF y Excel es la unidad de
    /// ancho, que se verifica aparte por no ser comparable campo a campo.
    /// </summary>
    private sealed record ColumnaVista(
        string Identificador,
        string Encabezado,
        bool Visible,
        int Orden,
        int AnchoPx,
        string FormatoNumerico,
        string FormatoAplicado,
        bool EsNumerica,
        string Alineacion,
        string AlineacionVertical,
        bool Wrap,
        ReportTextStyle EstiloEncabezado,
        ReportTextStyle EstiloContenido);

    private static IReadOnlyList<ColumnaVista> DerivarVista(ReportColumnSnapshot snapshot, Medio medio)
        => snapshot.Columnas
            .OrderBy(c => c.Orden)
            .Select(c => new ColumnaVista(
                Identificador: c.Identificador,
                Encabezado: c.Encabezado,
                Visible: c.Visible,
                Orden: c.Orden,
                AnchoPx: c.Ancho,
                FormatoNumerico: c.FormatoNumerico,
                FormatoAplicado: medio == Medio.Pdf
                    ? ReportNumberFormatMapper.ToPdfFormat(c.FormatoNumerico)
                    : ReportNumberFormatMapper.ToExcelFormat(c.FormatoNumerico),
                EsNumerica: c.EsNumerica,
                Alineacion: c.Alineacion.ToString(),
                AlineacionVertical: c.AlineacionVertical.ToString(),
                Wrap: c.Wrap,
                EstiloEncabezado: c.EstiloEncabezado,
                EstiloContenido: c.EstiloContenido))
            .ToList();

    private static void AssertParidadPdfExcel(ReportColumnSnapshot snapshot, string reporte)
    {
        var pdf = DerivarVista(snapshot, Medio.Pdf);
        var excel = DerivarVista(snapshot, Medio.Excel);

        // Mismo conjunto de columnas: mismo orden, visibilidad, encabezado, ancho
        // px, FormatoNumerico, alineaciones, wrap y estilos.
        CollectionAssert.AreEqual(
            pdf.ToArray(), excel.ToArray(),
            $"{reporte}: PDF y Excel deben derivar el mismo conjunto de columnas del snapshot único.");

        // Refuerzo explícito contra un snapshot vacío o desordenado.
        CollectionAssert.AreEqual(
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray(),
            pdf.Select(c => c.Identificador).ToArray(),
            $"{reporte}: las vistas deben exponer el orden canónico del snapshot.");
        Assert.IsTrue(pdf.Count > 0, $"{reporte}: el piloto debe exponer columnas visibles/definidas.");

        AssertFormatosCoinciden(snapshot);
        AssertAnchosNoExcedenElAreaUtil(snapshot);
    }

    private static void AssertFormatosCoinciden(ReportColumnSnapshot snapshot)
    {
        foreach (var columna in snapshot.Columnas)
        {
            Assert.AreEqual(
                ReportNumberFormatMapper.ToPdfFormat(columna.FormatoNumerico),
                ReportNumberFormatMapper.ToExcelFormat(columna.FormatoNumerico),
                $"El formato de '{columna.Identificador}' debe coincidir entre PDF y Excel.");
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

    private static void SanityPresupuesto(PilotoSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("Presupuesto", dto.TipoReporte);
        Assert.AreEqual(PilotoReportesFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "Cantidad", "PrecioUnitario", "ImporteTotal" },
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Presupuesto debe excluir las columnas internas y resolver el alias Importe→ImporteTotal.");

        var precio = dto.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.AreEqual("Precio Unitario", precio.Encabezado, "El encabezado lo aporta la entidad, no el overlay.");
        Assert.AreEqual("Arial", precio.EstiloEncabezado.Fuente, "El overlay debe aportar el estilo del encabezado.");
        Assert.AreEqual("#0D47A1", precio.EstiloEncabezado.ColorFondo);
        Assert.AreEqual("#FFEB3B", precio.EstiloEncabezado.ColorFuente);

        Assert.IsNull(dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El Presupuesto legacy no usa bandeado.");
        Assert.IsFalse(dto.EstiloTabla.Bordes.Visible, "El Presupuesto legacy no dibuja grilla.");

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }

    private static void SanityMateriales(PilotoSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("Materiales", dto.TipoReporte);
        Assert.AreEqual(PilotoReportesFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "Cantidad", "PrecioUnitario", "Total" },
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Materiales debe exponer las seis columnas del piloto.");

        Assert.IsTrue(
            dto.Columnas.Any(c => c.Formato == "C4" && c.EsNumerica),
            "El catálogo de materiales del piloto debe incluir el formato C4 (monetario de 4 decimales).");
        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El catálogo de materiales legacy usa bandeado #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El catálogo de materiales legacy dibuja grilla.");

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
