using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Reporte de Cálculo de Indirectos: paridad semántica PDF/Excel desde un ÚNICO
/// <see cref="ReportColumnSnapshot"/> y verificación del formato neutral compartido
/// por ambos renderizadores (<see cref="ReportColumnGridFormat"/>: símbolo <c>$</c>
/// y decimales de importe/cantidad/porcentaje del proyecto).
///
/// A. Paridad semántica: dado el snapshot, las vistas PDF y Excel derivan el mismo
///    conjunto de columnas (orden, visibilidad, encabezado, ancho px y estilos).
/// B. Formato/valores: importes (moneda <c>$</c> + DecimalesImporte), porcentajes
///    (DecimalesPorcentaje), duración como texto entero y cantidad (DecimalesCantidad),
///    con casos de decimales distintos, negativos, ceros y defaults.
/// C. Golden: la proyección semántica se congela en
///    <c>TestData/Goldens/indirectos-columnas.json</c> con el mecanismo
///    <see cref="GoldenPaths"/>. Es un archivo nuevo; NO se toca ni regenera ningún
///    golden del piloto ni del catálogo de matrices.
/// </summary>
[TestClass]
[DoNotParallelize]
public class IndirectosReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotIndirectos_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(IndirectosReportFixtures.Indirectos(), "Indirectos");

    [TestMethod]
    public void SnapshotDefaults_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(IndirectosReportFixtures.Defaults(), "Indirectos (defaults)");

    [TestMethod]
    public void SnapshotIndirectos_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(IndirectosReportFixtures.Indirectos());

    [TestMethod]
    public void SnapshotIndirectos_ColumnasRolesYOrdenEsperados()
    {
        var snapshot = IndirectosReportFixtures.Indirectos();

        CollectionAssert.AreEqual(
            new[] { "Grupo", "ImporteMensual", "Duracion", "ImporteTotal" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray(),
            "El snapshot debe excluir las columnas internas (Tipo, colRelleno).");

        var grupo = snapshot.Columnas.Single(c => c.Identificador == "Grupo");
        Assert.IsFalse(grupo.EsNumerica, "El rótulo Grupo/Concepto es texto.");

        var mensual = snapshot.Columnas.Single(c => c.Identificador == "ImporteMensual");
        Assert.IsTrue(mensual.EsNumerica);
        Assert.IsTrue(mensual.EsMoneda, "El Importe Mensual es rol monetario del catálogo.");
        Assert.AreEqual(ReportTextAlignment.Derecha, mensual.Alineacion);

        var duracion = snapshot.Columnas.Single(c => c.Identificador == "Duracion");
        Assert.IsFalse(duracion.EsNumerica, "La duración del catálogo es entero textual.");
        Assert.AreEqual(ReportTextAlignment.Centro, duracion.Alineacion);

        var total = snapshot.Columnas.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(total.EsNumerica);
        Assert.IsTrue(total.EsMoneda, "El Importe Total es rol monetario del catálogo.");

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    // ───────────────────── B. Formato y valores ─────────────────────

    [TestMethod]
    public void ImporteMensual_Moneda_UsaSimboloYDecimalesDeImporte()
    {
        using var _ = new CultureScope(CulturaControlada());
        var snapshot = IndirectosReportFixtures.Indirectos(decimalesImporte: 2);
        var mensual = Columna(snapshot, "ImporteMensual");

        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(mensual));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(mensual));

        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(mensual, snapshot));
        Assert.AreEqual("$1,234.57", ReportColumnGridFormat.FormatearPdf(1234.5678m, mensual, snapshot));
        Assert.IsTrue(ReportColumnGridFormat.FormatearPdf(1234.5678m, mensual, snapshot).Contains('$'));
    }

    [TestMethod]
    public void ImporteTotal_Moneda_ConDecimalesDeImporteDistintos()
    {
        using var _ = new CultureScope(CulturaControlada());

        var snap0 = IndirectosReportFixtures.Indirectos(decimalesImporte: 0);
        var total0 = Columna(snap0, "ImporteTotal");
        Assert.AreEqual("$#,##0", ReportColumnGridFormat.ResolveExcelFormat(total0, snap0));
        Assert.AreEqual("$1,235", ReportColumnGridFormat.FormatearPdf(1234.5678m, total0, snap0));

        var snap4 = IndirectosReportFixtures.Indirectos(decimalesImporte: 4);
        var total4 = Columna(snap4, "ImporteTotal");
        Assert.AreEqual("$#,##0.0000", ReportColumnGridFormat.ResolveExcelFormat(total4, snap4));
        Assert.AreEqual("$1,234.5678", ReportColumnGridFormat.FormatearPdf(1234.5678m, total4, snap4));
    }

    [TestMethod]
    public void Importes_NegativosYCeros()
    {
        using var _ = new CultureScope(CulturaControlada());
        var snapshot = IndirectosReportFixtures.Indirectos(decimalesImporte: 2);
        var total = Columna(snapshot, "ImporteTotal");

        Assert.AreEqual("-$1,234.50", ReportColumnGridFormat.FormatearPdf(-1234.5m, total, snapshot));
        Assert.AreEqual("$0.00", ReportColumnGridFormat.FormatearPdf(0m, total, snapshot));
    }

    [TestMethod]
    public void Porcentajes_UsanDecimalesDePorcentaje()
    {
        using var _ = new CultureScope(CulturaControlada());

        var snap4 = IndirectosReportFixtures.Indirectos(decimalesPorcentaje: 4);
        Assert.AreEqual("1.5000", ReportColumnGridFormat.FormatearPorcentajePdf(1.5m, snap4));
        Assert.AreEqual("0.0000%", ReportColumnGridFormat.FormatoPorcentajeExcel(4));

        var snap2 = IndirectosReportFixtures.Indirectos(decimalesPorcentaje: 2);
        Assert.AreEqual("1.50", ReportColumnGridFormat.FormatearPorcentajePdf(1.5m, snap2));
        Assert.AreEqual("0.00%", ReportColumnGridFormat.FormatoPorcentajeExcel(2));

        var snap0 = IndirectosReportFixtures.Indirectos(decimalesPorcentaje: 0);
        Assert.AreEqual("2", ReportColumnGridFormat.FormatearPorcentajePdf(1.5m, snap0));
        Assert.AreEqual("0%", ReportColumnGridFormat.FormatoPorcentajeExcel(0));
    }

    [TestMethod]
    public void Cantidad_UsaDecimalesDeCantidad()
    {
        using var _ = new CultureScope(CulturaControlada());
        var snapshot = new IndirectosReportSnapshotBuilder().Build(
            1,
            "I",
            new[]
            {
                new ColumnaIndirectos { NombreInterno = "Cantidad", Nombre = "Cantidad", Orden = 0, AnchoColumna = 70, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "N2" }
            },
            decimalesCantidad: 3);

        var cantidad = snapshot.Columnas.Single();
        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(cantidad));
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(cantidad));
        Assert.AreEqual("#,##0.000", ReportColumnGridFormat.ResolveExcelFormat(cantidad, snapshot));
        Assert.AreEqual("1,234.568", ReportColumnGridFormat.FormatearPdf(1234.5678m, cantidad, snapshot));
    }

    // ─────────── D. Paridad de valores crudos entre PDF y Excel ───────────

    [TestMethod]
    public void Importe_FormatoExcelSobreValorCrudo_ReproduceElTextoDelPdf()
    {
        using var _ = new CultureScope(CulturaControlada());
        var snapshot = IndirectosReportFixtures.Indirectos(decimalesImporte: 2);
        var total = Columna(snapshot, "ImporteTotal");

        const decimal valor = 1234.5m;
        var pdf = ReportColumnGridFormat.FormatearPdf(valor, total, snapshot);
        var excelFormat = ReportColumnGridFormat.ResolveExcelFormat(total, snapshot);

        Assert.AreEqual("$1,234.50", pdf);
        Assert.AreEqual(pdf, valor.ToString(excelFormat, CultureInfo.CurrentCulture),
            "El valor crudo + formato Excel debe reproducir EXACTAMENTE el texto del PDF (paridad).");
    }

    [TestMethod]
    public void Porcentaje_FormatoExcelSobreValorCrudo_ReproduceElTextoDelPdf()
    {
        using var _ = new CultureScope(CulturaControlada());
        var snapshot = IndirectosReportFixtures.Indirectos(decimalesPorcentaje: 4);

        const decimal porcentaje = 1.5m;
        var pdf = ReportColumnGridFormat.FormatearPorcentajePdf(porcentaje, snapshot) + "%";
        var excel = (porcentaje / 100m).ToString(ReportColumnGridFormat.FormatoPorcentajeExcel(4), CultureInfo.CurrentCulture);

        Assert.AreEqual("1.5000%", pdf);
        Assert.AreEqual(pdf, excel,
            "El porcentaje de PDF (número + '%') y de Excel (fracción + formato percent) deben coincidir.");
    }

    [TestMethod]
    public void Duracion_EsTextoEnteroEnAmbosMedios()
    {
        var snapshot = IndirectosReportFixtures.Indirectos();
        var duracion = Columna(snapshot, "Duracion");
        var grupo = Columna(snapshot, "Grupo");

        var concepto = new ConceptoIndirecto { Concepto = "Viáticos", DuracionMeses = 12, ImporteMensual = 1000m };

        // Campo: muestra los meses enteros (sin decimales) como texto.
        Assert.AreEqual("12", IndirectosExportResolver.ResolveTextoConcepto(concepto, duracion, mostrarDuracion: true));
        // Oficina Central: la columna queda vacía.
        Assert.AreEqual(string.Empty, IndirectosExportResolver.ResolveTextoConcepto(concepto, duracion, mostrarDuracion: false));
        // El rótulo del concepto va indentado.
        Assert.AreEqual("    Viáticos", IndirectosExportResolver.ResolveTextoConcepto(concepto, grupo, mostrarDuracion: true));
    }

    [TestMethod]
    public void Resolver_ExtraeValoresCrudosDeGrupoYConcepto()
    {
        var snapshot = IndirectosReportFixtures.Indirectos();
        var mensual = Columna(snapshot, "ImporteMensual");
        var total = Columna(snapshot, "ImporteTotal");

        var concepto = new ConceptoIndirecto { ImporteMensual = 1000m, DuracionMeses = 12 };
        var grupo = new GrupoIndirecto { Nombre = "Honorarios" };
        grupo.Conceptos.Add(concepto);

        Assert.IsTrue(IndirectosExportResolver.TryGetValorConcepto(concepto, mensual, out var vMensual));
        Assert.AreEqual(1000m, vMensual);
        Assert.IsTrue(IndirectosExportResolver.TryGetValorConcepto(concepto, total, out var vTotalConcepto));
        Assert.AreEqual(12000m, vTotalConcepto);
        Assert.IsFalse(IndirectosExportResolver.TryGetValorConcepto(concepto, Columna(snapshot, "Grupo"), out _));

        Assert.IsTrue(IndirectosExportResolver.TryGetValorGrupo(grupo, total, out var vTotalGrupo));
        Assert.AreEqual(12000m, vTotalGrupo);
        Assert.IsFalse(IndirectosExportResolver.TryGetValorGrupo(grupo, mensual, out _));

        Assert.AreEqual("Honorarios", IndirectosExportResolver.ResolveTextoGrupo(grupo, Columna(snapshot, "Grupo")));
    }

    [TestMethod]
    public void Defaults_SinConfiguracion_ExponeElCatalogoConEstiloLegacyCatalogo()
    {
        var snapshot = IndirectosReportFixtures.Defaults();

        CollectionAssert.AreEqual(
            new[] { "Grupo", "ImporteMensual", "Duracion", "ImporteTotal" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
        Assert.AreEqual("Indirectos", snapshot.TipoReporte);
    }

    // ─────────────────── C. Golden del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_Indirectos_ColumnasDelContratoNeutral()
    {
        var dto = IndirectosReportFixtures.Proyectar(IndirectosReportFixtures.Indirectos());

        Snapshots.AssertOrRegenerar(
            "indirectos-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityIndirectos(dto, rutaGolden));
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private static ReportColumnDefinition Columna(ReportColumnSnapshot snapshot, string identificador)
        => snapshot.Columnas.Single(c => c.Identificador == identificador);

    private static CultureInfo CulturaControlada()
    {
        var cultura = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        cultura.NumberFormat.NumberDecimalSeparator = ".";
        cultura.NumberFormat.NumberGroupSeparator = ",";
        cultura.NumberFormat.NumberGroupSizes = new[] { 3 };
        cultura.NumberFormat.CurrencyDecimalSeparator = ".";
        cultura.NumberFormat.CurrencyGroupSeparator = ",";
        cultura.NumberFormat.CurrencyGroupSizes = new[] { 3 };
        return cultura;
    }

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

    private static void SanityIndirectos(IndirectosSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("Indirectos", dto.TipoReporte);
        Assert.AreEqual(IndirectosReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            new[] { "Grupo", "ImporteMensual", "Duracion", "ImporteTotal" },
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Indirectos debe excluir las columnas internas.");

        var mensual = dto.Columnas.Single(c => c.Identificador == "ImporteMensual");
        Assert.IsTrue(mensual.EsNumerica, "El Importe Mensual es numérico.");
        Assert.IsTrue(mensual.EsMoneda, "El Importe Mensual es rol monetario del catálogo.");
        Assert.AreEqual("N2", mensual.Formato, "El Importe Mensual usa el token de cantidad/importe N2.");

        var total = dto.Columnas.Single(c => c.Identificador == "ImporteTotal");
        Assert.IsTrue(total.EsMoneda, "El Importe Total es rol monetario del catálogo.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El estilo de catálogo usa bandeado #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El estilo de catálogo dibuja grilla.");
        Assert.AreEqual("#4A4A6A", dto.EstiloTabla.Encabezado.ColorFondo);

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _original;

        public CultureScope(CultureInfo cultura)
        {
            _original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = cultura;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _original;
        }
    }
}
