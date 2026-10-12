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
/// Reporte de Explosión de Insumos: paridad semántica PDF/Excel desde un ÚNICO
/// <see cref="ReportColumnSnapshot"/> y golden del contrato neutral de columnas.
/// Sigue el mismo criterio que <c>ManoObraReportColumnsTests</c>.
///
/// A. Paridad semántica: dado el snapshot, las vistas PDF y Excel derivan el mismo
///    conjunto de columnas (orden, visibilidad, encabezado, ancho px, alineaciones,
///    wrap, estilos y rol numérico). El reporte cubre las cuatro familias de insumo
///    y sus totales, pero el CONTRATO de columnas es único y se verifica aquí.
///
/// B. Golden: la proyección semántica se congela en
///    <c>TestData/Goldens/explosion-columnas.json</c> con el mecanismo
///    <see cref="GoldenPaths"/> (<c>SOPRO_REGENERATE_GOLDENS=1</c>).
///
/// C. Hash PDF normalizado: DIFERIDO (no hay renderer neutral de Explosión en
///    <c>SOPRO.Reporting</c>; el de WinForms no es invocable desde aquí).
/// </summary>
[TestClass]
public class ExplosionReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    private static readonly string[] IdentificadoresCanonicos =
    {
        "Clave", "Descripcion", "Unidad", "Cantidad", "PrecioUnitario", "ImporteTotal", "Porcentaje", "Observaciones"
    };

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotExplosion_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(ExplosionReportFixtures.Explosion(), "Explosion");

    [TestMethod]
    public void SnapshotExplosion_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(ExplosionReportFixtures.Explosion());

    [TestMethod]
    public void SnapshotExplosion_ExcluyeColumnasInternas()
    {
        var snapshot = ExplosionReportFixtures.Explosion();

        CollectionAssert.AreEqual(
            IdentificadoresCanonicos,
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray(),
            "El snapshot debe excluir Tipo/colRelleno y conservar el orden canónico (Importe→ImporteTotal).");

        Assert.IsFalse(snapshot.Columnas.Any(c => ReportColumnDefinitionMapper.EsColumnaInterna(c.Identificador)));
    }

    // ────────────── B. Rol numérico y decimales del proyecto ──────────────

    [DataTestMethod]
    [DataRow(2, "$#,##0.00")]
    [DataRow(4, "$#,##0.0000")]
    public void SnapshotExplosion_MonedaUsaSimboloYDecimalesDeImporte(int decimalesImporte, string excelEsperado)
    {
        var snapshot = ExplosionReportFixtures.Explosion(decimalesImporte: decimalesImporte);

        var pu = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(pu), "El Precio Unitario es monetario.");
        Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(pu, snapshot));

        var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, pu, snapshot);
        Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            $"El PDF monetario debe llevar el símbolo centralizado. Obtenido: '{pdf}'.");
        Assert.IsFalse(pdf.Contains('\u00A4'), "El PDF monetario no debe colar el glifo ¤.");

        var importe = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(importe), "El Importe es monetario.");
        Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(importe, snapshot));
    }

    [DataTestMethod]
    [DataRow(2, "#,##0.00")]
    [DataRow(3, "#,##0.000")]
    [DataRow(4, "#,##0.0000")]
    public void SnapshotExplosion_CantidadUsaDecimalesDeCantidad(int decimalesCantidad, string excelEsperado)
    {
        var snapshot = ExplosionReportFixtures.Explosion(decimalesCantidad: decimalesCantidad);

        var cantidad = snapshot.Columnas.Single(c => c.Identificador == "Cantidad");
        Assert.IsTrue(cantidad.EsNumerica, "La Cantidad es numérica.");
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(cantidad), "La Cantidad no es monetaria.");
        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(cantidad), "La Cantidad (token N4) es una columna de cantidad.");
        Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(cantidad, snapshot));

        var pdf = ReportColumnGridFormat.FormatearPdf(1.23456m, cantidad, snapshot);
        Assert.IsFalse(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            "La Cantidad no debe llevar símbolo de moneda.");
    }

    [TestMethod]
    public void SnapshotExplosion_PorcentajeUsaDecimalesDePorcentaje()
    {
        var s2 = ExplosionReportFixtures.Explosion(decimalesPorcentaje: 2);
        var s4 = ExplosionReportFixtures.Explosion(decimalesPorcentaje: 4);

        var porcentaje = s2.Columnas.Single(c => c.Identificador == "Porcentaje");
        Assert.IsTrue(ReportColumnGridFormat.EsPorcentajeGrid(porcentaje), "La columna % es de porcentaje.");

        var pdf2 = ReportColumnGridFormat.FormatearPorcentajePdf(3.14159m, s2);
        var pdf4 = ReportColumnGridFormat.FormatearPorcentajePdf(3.14159m, s4);

        // Mismo valor, distinto número de decimales → la cadena crece 2 caracteres.
        Assert.AreEqual(pdf2.Length + 2, pdf4.Length,
            $"DecimalesPorcentaje debe mandar en el PDF. 2:'{pdf2}' 4:'{pdf4}'.");

        Assert.AreEqual("0.00%", ReportColumnGridFormat.FormatoPorcentajeExcel(2));
        Assert.AreEqual("0.0000%", ReportColumnGridFormat.FormatoPorcentajeExcel(4));
    }

    // ─────────────────── C. Golden del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_Explosion_ColumnasDelContratoNeutral()
    {
        var dto = ExplosionReportFixtures.Proyectar(ExplosionReportFixtures.Explosion());

        Snapshots.AssertOrRegenerar(
            "explosion-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityExplosion(dto, rutaGolden));
    }

    // ─────────────────────────── Helpers ───────────────────────────

    /// <summary>
    /// Vista neutral de una columna tal como la consumiría un medio concreto: los
    /// campos de contrato comunes más el estilo. No incluye el formato aplicado
    /// (diverge legítimamente entre PDF y Excel) ni la unidad de ancho (se verifica
    /// aparte).
    /// </summary>
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
        AssertAnchosNoExcedenElAreaUtil(snapshot);
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

    private static void SanityExplosion(ExplosionSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("Explosion", dto.TipoReporte);
        Assert.AreEqual(ExplosionReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            IdentificadoresCanonicos,
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Explosión debe excluir Tipo/colRelleno y conservar el orden canónico.");

        var cantidad = dto.Columnas.Single(c => c.Identificador == "Cantidad");
        Assert.IsTrue(cantidad.EsNumerica, "La Cantidad es numérica.");
        Assert.IsFalse(cantidad.EsMoneda, "La Cantidad no es monetaria.");
        Assert.AreEqual("N4", cantidad.Formato);

        var pu = dto.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(pu.EsMoneda, "El Precio Unitario es monetario (token C4).");
        Assert.AreEqual("C4", pu.Formato);

        var importe = dto.Columnas.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(importe.EsMoneda, "El Importe es monetario (token C2).");
        Assert.AreEqual("C2", importe.Formato);

        var porcentaje = dto.Columnas.Single(c => c.Identificador == "Porcentaje");
        Assert.IsTrue(porcentaje.EsNumerica, "El Porcentaje es numérico.");
        Assert.IsFalse(porcentaje.EsMoneda, "El Porcentaje no es monetario.");
        Assert.AreEqual("P2", porcentaje.Formato);

        Assert.IsFalse(dto.Columnas.Single(c => c.Identificador == "Observaciones").Visible,
            "La columna Observaciones viaja oculta en el snapshot.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El catálogo de explosión legacy usa bandeado #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El catálogo legacy dibuja grilla.");
        Assert.AreEqual("#4A4A6A", dto.EstiloTabla.Encabezado.ColorFondo);

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
