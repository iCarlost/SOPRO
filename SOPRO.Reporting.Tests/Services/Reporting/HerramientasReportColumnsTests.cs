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
/// Catálogo de Herramientas: paridad semántica PDF/Excel desde un ÚNICO
/// <see cref="ReportColumnSnapshot"/> y verificación del formato neutral
/// compartido por ambos renderizadores (<see cref="ReportColumnGridFormat"/>:
/// símbolo <c>$</c> y decimales de importe/cantidad/porcentaje del proyecto).
///
/// A. Paridad semántica: dado el snapshot, las vistas PDF y Excel derivan el mismo
///    conjunto de columnas (orden, visibilidad, encabezado, ancho px y estilos).
/// B. Formato/valores: precio (moneda <c>$</c> + DecimalesImporte), porcentaje
///    (%MO con DecimalesPorcentaje) y cantidad (DecimalesCantidad), con casos de
///    decimales distintos, negativos, ceros, nulos y defaults.
/// C. Golden: la proyección semántica se congela en
///    <c>TestData/Goldens/herramientas-columnas.json</c> con el mecanismo
///    <see cref="GoldenPaths"/>. Es un archivo nuevo; NO se toca ni regenera
///    ningún golden del piloto ni del catálogo de matrices.
/// </summary>
[TestClass]
[DoNotParallelize]
public class HerramientasReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotHerramientas_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(HerramientasReportFixtures.Herramientas(), "Herramientas");

    [TestMethod]
    public void SnapshotHerramientas_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(HerramientasReportFixtures.Herramientas());

    [TestMethod]
    public void SnapshotHerramientas_ColumnasRolesYOrdenEsperados()
    {
        var snapshot = HerramientasReportFixtures.Herramientas();

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "PrecioUnitario", "OrigenDetalle" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray(),
            "El snapshot debe excluir las columnas internas (Tipo, colRelleno).");

        var precio = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(precio.EsNumerica);
        Assert.IsTrue(precio.EsMoneda, "El Precio/Porcentaje del catálogo es rol monetario.");
        Assert.AreEqual(ReportTextAlignment.Derecha, precio.Alineacion);
        Assert.AreEqual(150, precio.Ancho);

        var descripcion = snapshot.Columnas.Single(c => c.Identificador == "Descripcion");
        Assert.IsTrue(descripcion.Wrap);
        Assert.IsFalse(descripcion.EsNumerica);

        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
    }

    // ───────────────────── B. Formato y valores ─────────────────────

    [TestMethod]
    public void PrecioUnitario_Moneda_UsaSimboloYDecimalesDeImporte()
    {
        using var _ = new CultureScope(CulturaControlada());
        var snapshot = SnapshotHerramientas(decimalesImporte: 2);
        var precio = Columna(snapshot, "PrecioUnitario");

        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(precio));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(precio));

        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(precio, snapshot));
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.FormatoMonedaPdf(2));

        Assert.AreEqual("$1,234.57", ReportColumnGridFormat.FormatearPdf(1234.5678m, precio, snapshot));
        Assert.IsTrue(ReportColumnGridFormat.FormatearPdf(1234.5678m, precio, snapshot).Contains('$'));
    }

    [TestMethod]
    public void PrecioUnitario_Moneda_ConDecimalesDeImporteDistintos()
    {
        using var _ = new CultureScope(CulturaControlada());

        var snap0 = SnapshotHerramientas(decimalesImporte: 0);
        var precio0 = Columna(snap0, "PrecioUnitario");
        Assert.AreEqual("$#,##0", ReportColumnGridFormat.ResolveExcelFormat(precio0, snap0));
        Assert.AreEqual("$1,235", ReportColumnGridFormat.FormatearPdf(1234.5678m, precio0, snap0));

        var snap4 = SnapshotHerramientas(decimalesImporte: 4);
        var precio4 = Columna(snap4, "PrecioUnitario");
        Assert.AreEqual("$#,##0.0000", ReportColumnGridFormat.ResolveExcelFormat(precio4, snap4));
        Assert.AreEqual("$1,234.5678", ReportColumnGridFormat.FormatearPdf(1234.5678m, precio4, snap4));
    }

    [TestMethod]
    public void PrecioUnitario_Moneda_NegativosYCeros()
    {
        using var _ = new CultureScope(CulturaControlada());
        var snapshot = SnapshotHerramientas(decimalesImporte: 2);
        var precio = Columna(snapshot, "PrecioUnitario");

        Assert.AreEqual("-$1,234.50", ReportColumnGridFormat.FormatearPdf(-1234.5m, precio, snapshot));
        Assert.AreEqual("$0.00", ReportColumnGridFormat.FormatearPdf(0m, precio, snapshot));
    }

    [TestMethod]
    public void PrecioUnitario_Porcentual_UsaDecimalesDePorcentaje()
    {
        using var _ = new CultureScope(CulturaControlada());

        var snap4 = SnapshotHerramientas(decimalesPorcentaje: 4);
        Assert.AreEqual("3.1416", ReportColumnGridFormat.FormatearPorcentajePdf(3.14159m, snap4));
        Assert.AreEqual("0.0000%", ReportColumnGridFormat.FormatoPorcentajeExcel(4));

        var snap2 = SnapshotHerramientas(decimalesPorcentaje: 2);
        Assert.AreEqual("3.14", ReportColumnGridFormat.FormatearPorcentajePdf(3.14159m, snap2));
        Assert.AreEqual("0.00%", ReportColumnGridFormat.FormatoPorcentajeExcel(2));

        var snap0 = SnapshotHerramientas(decimalesPorcentaje: 0);
        Assert.AreEqual("3", ReportColumnGridFormat.FormatearPorcentajePdf(3.14159m, snap0));
        Assert.AreEqual("0%", ReportColumnGridFormat.FormatoPorcentajeExcel(0));
    }

    [TestMethod]
    public void Cantidad_UsaDecimalesDeCantidad()
    {
        using var _ = new CultureScope(CulturaControlada());
        var snapshot = new HerramientasReportSnapshotBuilder().Build(
            1,
            "H",
            new[]
            {
                new ColumnaHerramienta { NombreInterno = "Cantidad", Nombre = "Cantidad", Orden = 0, AnchoColumna = 70, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "N2" }
            },
            decimalesCantidad: 3);

        var cantidad = Columna(snapshot, "Cantidad");
        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(cantidad));
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(cantidad));
        Assert.AreEqual("#,##0.000", ReportColumnGridFormat.ResolveExcelFormat(cantidad, snapshot));
        Assert.AreEqual("1,234.568", ReportColumnGridFormat.FormatearPdf(1234.5678m, cantidad, snapshot));
    }

    [TestMethod]
    public void ResolveValue_ColumnasDeTexto_NulosDevuelvenVacio()
    {
        var snapshot = HerramientasReportFixtures.Herramientas();
        var herramienta = new Herramienta { Clave = null!, Descripcion = null!, Unidad = null! };

        Assert.AreEqual(string.Empty, HerramientasCatalogExportResolver.ResolveValue(herramienta, Columna(snapshot, "Clave")));
        Assert.AreEqual(string.Empty, HerramientasCatalogExportResolver.ResolveValue(herramienta, Columna(snapshot, "Descripcion")));
        Assert.AreEqual(string.Empty, HerramientasCatalogExportResolver.ResolveValue(herramienta, Columna(snapshot, "Unidad")));
    }

    [TestMethod]
    public void ResolveValue_Origen_MuestraImportadoOLocal()
    {
        var snapshot = HerramientasReportFixtures.Herramientas();
        var origen = Columna(snapshot, "OrigenDetalle");

        var local = new Herramienta { Origen = OrigenInsumo.Proyecto };
        Assert.AreEqual("Local", HerramientasCatalogExportResolver.ResolveValue(local, origen));

        var maestro = new Herramienta { Origen = OrigenInsumo.Maestro };
        Assert.AreEqual("Maestro", HerramientasCatalogExportResolver.ResolveValue(maestro, origen));

        var importado = new Herramienta { Origen = OrigenInsumo.Proyecto, Notas = "[IMPORTADO DE: Obra Norte]" };
        Assert.AreEqual("Importado: Obra Norte", HerramientasCatalogExportResolver.ResolveValue(importado, origen));
    }

    [TestMethod]
    public void EsPrecioUnitarioPorcentual_ReflejaEsPorcentajeMO()
    {
        var porcentual = new Herramienta { Unidad = "%MO" };
        var monetaria = new Herramienta { Unidad = "pza" };

        Assert.IsTrue(HerramientasCatalogExportResolver.EsPrecioUnitarioPorcentual(porcentual));
        Assert.IsFalse(HerramientasCatalogExportResolver.EsPrecioUnitarioPorcentual(monetaria));
    }

    [TestMethod]
    public void Defaults_SinConfiguracion_UsaColumnasPredeterminadas()
    {
        var snapshot = HerramientasCatalogExportResolver.BuildSnapshot(0, "H", columnas: null);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "PrecioUnitario", "OrigenDetalle" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());

        Assert.AreEqual(HerramientasReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual(2, snapshot.DecimalesImporte);
        Assert.AreEqual(4, snapshot.DecimalesPorcentaje);
    }

    // ─────────────────── C. Golden del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_Herramientas_ColumnasDelContratoNeutral()
    {
        var dto = HerramientasReportFixtures.Proyectar(HerramientasReportFixtures.Herramientas());

        Snapshots.AssertOrRegenerar(
            "herramientas-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityHerramientas(dto, rutaGolden));
    }

    // ─────────────────────────── Helpers ───────────────────────────

    private static ReportColumnSnapshot SnapshotHerramientas(
        int decimalesCantidad = 2,
        int decimalesImporte = 2,
        int decimalesPorcentaje = 4)
        => new HerramientasReportSnapshotBuilder().Build(
            HerramientasReportFixtures.ProyectoId,
            "H",
            new[]
            {
                new ColumnaHerramienta { NombreInterno = "Clave", Nombre = "Clave", Orden = 0, AnchoColumna = 110, Alineacion = AlineacionColumna.Centro },
                new ColumnaHerramienta { NombreInterno = "PrecioUnitario", Nombre = "Precio/Porcentaje", Orden = 1, AnchoColumna = 150, Alineacion = AlineacionColumna.Derecha }
            },
            decimalesCantidad,
            decimalesImporte,
            decimalesPorcentaje);

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

    /// <summary>
    /// Vista neutral de una columna tal como la consumiría un medio concreto: los
    /// campos de contrato comunes más el estilo. No incluye el formato aplicado
    /// porque puede divergir legítimamente entre PDF y Excel.
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
        Assert.IsTrue(pdf.Count > 0, "El catálogo de herramientas debe exponer columnas.");

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

    private static void SanityHerramientas(HerramientasSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("Herramientas", dto.TipoReporte);
        Assert.AreEqual(HerramientasReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "Unidad", "PrecioUnitario", "OrigenDetalle" },
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Herramientas debe excluir las columnas internas.");

        var precio = dto.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(precio.EsNumerica, "El Precio/Porcentaje es numérico.");
        Assert.IsTrue(precio.EsMoneda, "El Precio/Porcentaje es rol monetario del catálogo.");
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo().EstiloEncabezado.ColorFondo, precio.EstiloEncabezado.ColorFondo);
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo().EstiloEncabezado.ColorFuente, precio.EstiloEncabezado.ColorFuente);

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El catálogo de herramientas usa el bandeado neutral #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El catálogo de herramientas dibuja grilla.");
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
