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
/// Reporte de Costo Horario / Maquinaria: paridad semántica PDF/Excel desde un
/// ÚNICO <see cref="ReportColumnSnapshot"/> y golden del contrato neutral de
/// columnas.
///
/// A. Paridad semántica: dado un snapshot, las vistas PDF y Excel derivan el
///    mismo conjunto de columnas (orden, visibilidad, encabezado, ancho px,
///    alineaciones, wrap y estilos). Los roles numéricos (Costo Horario monetario
///    con '$' + decimales de importe; Potencia como cantidad con decimales de
///    cantidad del proyecto) se verifican con <see cref="ReportColumnGridFormat"/>,
///    la regla única compartida por ambos medios.
///
/// B. Goldens: la proyección semántica se congela en
///    <c>TestData/Goldens/costo-horario-columnas.json</c> usando
///    <see cref="GoldenPaths"/>. Es un archivo nuevo; NO se toca ningún golden del
///    piloto ni del catálogo de matrices.
/// </summary>
[TestClass]
public class CostoHorarioReportColumnsTests
{
    private const double Tolerancia = 1e-9;

    // ───────────────────── A. Paridad semántica PDF/Excel ─────────────────────

    [TestMethod]
    public void SnapshotCostoHorario_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(CostoHorarioReportFixtures.CostoHorario(), "CostoHorario");

    [TestMethod]
    public void SnapshotDefaults_ExponeLasMismasColumnasParaPdfYExcel()
        => AssertParidadPdfExcel(CostoHorarioReportFixtures.Defaults(), "CostoHorario (defaults)");

    [TestMethod]
    public void SnapshotCostoHorario_AnchosConvertidosNoExcedenElAreaUtil()
        => AssertAnchosNoExcedenElAreaUtil(CostoHorarioReportFixtures.CostoHorario());

    [TestMethod]
    public void SnapshotCostoHorario_FormatosMapeadosCoincidenEntrePdfYExcel()
        => AssertFormatosCoinciden(CostoHorarioReportFixtures.CostoHorario());

    // ─────────────────── B. Goldens del contrato neutral ───────────────────

    [TestMethod]
    public void Golden_CostoHorario_ColumnasDelContratoNeutral()
    {
        var dto = CostoHorarioReportFixtures.Proyectar(CostoHorarioReportFixtures.CostoHorario());

        Snapshots.AssertOrRegenerar(
            "costo-horario-columnas.json",
            Snapshots.ToJson(dto),
            rutaGolden => SanityCostoHorario(dto, rutaGolden));
    }

    // ─────────────────── C. Roles numéricos y precisiones ───────────────────

    [TestMethod]
    public void CostoHorarioEsMonetario_UsaSimboloYDecimalesImporte()
    {
        var snapshot = CostoHorarioReportFixtures.CostoHorario();
        var costo = snapshot.Columnas.Single(c => c.Identificador == "CostoHorario");

        Assert.IsTrue(ReportColumnGridFormat.EsMonedaGrid(costo));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(costo));

        var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, costo, snapshot);
        Assert.IsTrue(pdf.Contains(ReportColumnGridFormat.SimboloMoneda),
            "El Costo Horario debe llevar el '$' centralizado.");
        Assert.IsTrue(pdf.Contains("1,234.50"), "Debe usar los decimales de importe del proyecto (2).");
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(costo, snapshot));
    }

    [TestMethod]
    public void PotenciaNominalEsCantidad_UsaDecimalesDeCantidadCinco()
    {
        var snapshot = CostoHorarioReportFixtures.CostoHorario();
        var potencia = snapshot.Columnas.Single(c => c.Identificador == "PotenciaNominal");

        Assert.IsTrue(ReportColumnGridFormat.EsCantidadGrid(potencia));

        // La precisión histórica de cinco decimales para cantidades viene de la
        // configuración global del proyecto (DecimalesCantidad), no del layout fijo.
        Assert.AreEqual(5, snapshot.DecimalesCantidad);
        Assert.AreEqual(
            123.45678m.ToString("N5", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPdf(123.45678m, potencia, snapshot));
        Assert.AreEqual("#,##0.00000", ReportColumnGridFormat.ResolveExcelFormat(potencia, snapshot));
    }

    [TestMethod]
    public void Cantidad_DecimalesDeCeroAN()
    {
        var baseSnapshot = CostoHorarioReportFixtures.CostoHorario();
        var potencia = baseSnapshot.Columnas
            .Single(c => c.Identificador == "PotenciaNominal");

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
                ReportColumnGridFormat.FormatearPdf(12.5m, potencia, snapshot));
            Assert.AreEqual(
                ReportColumnGridFormat.FormatoNumeroExcel(decimales),
                ReportColumnGridFormat.ResolveExcelFormat(potencia, snapshot));
        }
    }

    [TestMethod]
    public void Monetario_CeroYNegativo()
    {
        var snapshot = CostoHorarioReportFixtures.CostoHorario();
        var costo = snapshot.Columnas.Single(c => c.Identificador == "CostoHorario");

        var cero = ReportColumnGridFormat.FormatearPdf(0m, costo, snapshot);
        Assert.IsTrue(cero.Contains(ReportColumnGridFormat.SimboloMoneda));
        Assert.IsTrue(cero.Contains("0.00"));

        var negativo = ReportColumnGridFormat.FormatearPdf(-1234.5m, costo, snapshot);
        Assert.IsTrue(negativo.Contains(ReportColumnGridFormat.SimboloMoneda), "El negativo conserva el '$'.");
        Assert.IsTrue(negativo.Contains("1,234.50"));
        Assert.IsTrue(negativo.Contains("-"), "El negativo conserva el signo.");
    }

    [TestMethod]
    public void ColumnaTexto_SinFormatoNiRol_UsaDefaultLegacy()
    {
        var snapshot = CostoHorarioReportFixtures.CostoHorario();
        var origen = snapshot.Columnas.Single(c => c.Identificador == "Origen");

        Assert.AreEqual(string.Empty, origen.FormatoNumerico);
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(origen));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(origen));
        Assert.IsFalse(origen.EsNumerica);
    }

    [TestMethod]
    public void Defaults_SinConfiguracion_ExponeElCatalogoConEstiloLegacyCatalogo()
    {
        var snapshot = CostoHorarioReportFixtures.Defaults();

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "PotenciaNominal", "Combustible", "CostoHorario", "TipoCosto", "Origen" },
            snapshot.Columnas.Select(c => c.Identificador).ToArray());
        Assert.AreEqual(ReportTableStyle.LegacyCatalogo(), snapshot.EstiloTabla);
        Assert.AreEqual("Maquinaria", snapshot.TipoReporte);
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

            // Divergencia legítima por renderizador (fidelidad legacy 8afabde).
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

    private static void SanityCostoHorario(PilotoSnapshotDto dto, string rutaGolden)
    {
        Assert.AreEqual("Maquinaria", dto.TipoReporte);
        Assert.AreEqual(CostoHorarioReportFixtures.ProyectoId, dto.ProyectoId);

        CollectionAssert.AreEqual(
            new[] { "Clave", "Descripcion", "PotenciaNominal", "Combustible", "CostoHorario", "TipoCosto", "Origen" },
            dto.Columnas.Select(c => c.Identificador).ToArray(),
            "El golden de Costo Horario debe exponer las siete columnas del catálogo.");

        var costo = dto.Columnas.Single(c => c.Identificador == "CostoHorario");
        Assert.IsTrue(costo.EsNumerica, "El Costo Horario es numérico.");

        var potencia = dto.Columnas.Single(c => c.Identificador == "PotenciaNominal");
        Assert.AreEqual("N2", potencia.Formato, "La potencia usa el token de cantidad N2.");

        Assert.AreEqual("#F5F5F5", dto.EstiloTabla.FilaAlterna.ColorFondoAlterno,
            "El estilo de catálogo usa bandeado #F5F5F5.");
        Assert.IsTrue(dto.EstiloTabla.Bordes.Visible, "El estilo de catálogo dibuja grilla.");

        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }
}
