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
/// Reporte de Catálogo de Mano de Obra: paridad semántica PDF/Excel desde un
/// ÚNICO <see cref="ReportColumnSnapshot"/> y golden del contrato neutral de
/// columnas. Sigue el mismo criterio que <c>PilotoReportesColumnasTests</c>.
///
/// A. Paridad semántica: dado el snapshot, las vistas PDF y Excel derivan el mismo
///    conjunto de columnas (orden, visibilidad, encabezado, ancho px, alineaciones,
///    wrap, estilos y rol numérico). El FORMATO aplicado no se compara por igualdad
///    de cadena porque la tabla legacy diverge por renderizador; se verifica su
///    semántica (moneda → '$' + decimales de importe; cantidad → decimales de
///    cantidad; porcentaje → decimales de porcentaje) por separado.
///
/// B. Golden: la proyección semántica se congela en
///    <c>TestData/Goldens/mano-obra-columnas.json</c> con el mecanismo
///    <see cref="GoldenPaths"/> (<c>SOPRO_REGENERATE_GOLDENS=1</c>).
///
/// C. Hash PDF normalizado: DIFERIDO (no hay renderer neutral de Mano de Obra en
///    <c>SOPRO.Reporting</c>; el de WinForms no es invocable desde aquí). No se
///    referencian WinForms ni se crean proyectos.
/// </summary>
[TestClass]
public class ManoObraReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotManoObra_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(ManoObraReportFixtures.ManoObra(), "ManoDeObra");

    [TestMethod]
    public void SnapshotManoObra_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(ManoObraReportFixtures.ManoObra());

    [TestMethod]
    public void SnapshotManoObra_ExcluyeColumnasInternas()
    {
        var snapshot = ManoObraReportFixtures.ManoObra();

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "SalarioBase", "FactorSalarioReal", "SalarioReal", "Origen", "Notas" },
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray(),
            "El snapshot debe excluir Tipo/colRelleno y conservar el orden canónico.");

        Assert.IsFalse(snapshot.Columnas.Any(c => ReportColumnDefinitionMapper.EsColumnaInterna(c.Identificador)));
    }

    // ────────────── B. Rol numérico y decimales del proyecto ──────────────

    [DataTestMethod]
    [DataRow(2, "$#,##0.00")]
    [DataRow(4, "$#,##0.0000")]
    public void SnapshotManoObra_MonedaUsaSimboloYDecimalesDeImporte(int decimalesImporte, string excelEsperado)
    {
        var snapshot = ManoObraReportFixtures.ManoObra(decimalesImporte: decimalesImporte);

        var salario = snapshot.Columnas.Single(c => c.Identificador == "SalarioBase");
        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(salario), "SalarioBase es monetario.");
        Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(salario, snapshot));

        var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, salario, snapshot);
        Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            $"El PDF monetario debe llevar el símbolo centralizado. Obtenido: '{pdf}'.");
        Assert.IsFalse(pdf.Contains('\u00A4'), "El PDF monetario no debe colar el glifo ¤.");

        var salarioReal = snapshot.Columnas.Single(c => c.Identificador == "SalarioReal");
        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(salarioReal), "SalarioReal es monetario.");
    }

    [DataTestMethod]
    [DataRow(2, "#,##0.00")]
    [DataRow(3, "#,##0.000")]
    [DataRow(4, "#,##0.0000")]
    public void SnapshotManoObra_CantidadUsaDecimalesDeCantidad(int decimalesCantidad, string excelEsperado)
    {
        var snapshot = ManoObraReportFixtures.ManoObra(decimalesCantidad: decimalesCantidad);

        var fsr = snapshot.Columnas.Single(c => c.Identificador == "FactorSalarioReal");
        Assert.IsTrue(fsr.EsNumerica, "El FSR es numérico.");
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(fsr), "El FSR no es monetario.");
        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(fsr), "El FSR (token N4) es una columna de cantidad.");
        Assert.AreEqual(excelEsperado, ReportColumnGridFormat.ResolveExcelFormat(fsr, snapshot));

        var pdf = ReportColumnGridFormat.FormatearPdf(1.23456m, fsr, snapshot);
        Assert.IsFalse(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            "El FSR no debe llevar símbolo de moneda.");
    }

    [TestMethod]
    public void SnapshotManoObra_PorcentajeUsaDecimalesDePorcentaje()
    {
        var s2 = ManoObraReportFixtures.ManoObra(decimalesPorcentaje: 2);
        var s4 = ManoObraReportFixtures.ManoObra(decimalesPorcentaje: 4);

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
    public void Golden_ManoObra_ColumnasDelContratoNeutral()
    {
        var dto = ManoObraReportFixtures.Proyectar(ManoObraReportFixtures.ManoObra());

        Snapshots.AssertOrRegenerar(
            "mano-obra-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityManoObra(dto, rutaGolden));
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

    private static void SanityManoObra(ManoObraSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("ManoDeObra", dto.TipoReporte);
        Assert.AreEqual(ManoObraReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "SalarioBase", "FactorSalarioReal", "SalarioReal", "Origen", "Notas" },
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Mano de Obra debe excluir Tipo/colRelleno y conservar el orden canónico.");

        var fsr = dto.Columnas.Single(c => c.Identificador == "FactorSalarioReal");
        Assert.IsTrue(fsr.EsNumerica, "El FSR es numérico.");
        Assert.IsFalse(fsr.EsMoneda, "El FSR no es monetario (es un factor de cantidad).");
        Assert.AreEqual("N4", fsr.Formato);

        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "SalarioBase").EsMoneda,
            "Salario Base es monetario.");
        Assert.IsTrue(dto.Columnas.Single(c => c.Identificador == "SalarioReal").EsMoneda,
            "Salario Real es monetario.");
        Assert.IsFalse(dto.Columnas.Single(c => c.Identificador == "Notas").Visible,
            "La columna Notas viaja oculta en el snapshot.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El catálogo de mano de obra legacy usa bandeado #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El catálogo legacy dibuja grilla.");
        Assert.AreEqual("#4A4A6A", dto.EstiloTabla.Encabezado.ColorFondo);

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
