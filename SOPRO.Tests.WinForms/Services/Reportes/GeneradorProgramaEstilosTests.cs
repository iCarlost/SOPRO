using System.Collections.Generic;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.WinForms.Services.Reportes;

/// <summary>
/// Paso 6.2 — los generadores Excel y PDF de los PROGRAMAS aplican, por celda, los
/// estilos resueltos por el contrato neutral (<c>ReportCellStyleResolver</c>):
/// default de rol (agrupador gris #F0F0F0 + negrita) &gt; override de rol del
/// usuario &gt; override de celda. Verifica también que la clave de fila usada por
/// cada formato es la ESTABLE (<c>ACT:</c>/<c>RES:</c>/<c>INS:</c>).
/// </summary>
[TestClass]
public class GeneradorProgramaEstilosTests
{
    private const int Proyecto = 7;

    private static ReportColumnSnapshot SnapshotObra()
        => new ProgramaObraReportSnapshotBuilder().Build(Proyecto, "Obra", columnas: null);

    private static ReportColumnSnapshot SnapshotObraConDefaults()
        => SnapshotObra() with
        {
            EstilosFila = EstilosFilaPredeterminados.CombinarProgramas((IEnumerable<ReportRowRoleStyle>?)null)
        };

    // ─────────────────────────── Excel ───────────────────────────

    [TestMethod]
    public void Excel_AplicarOverlay_VuelcaLasPropiedadesNoNulas()
    {
        using var wb = new XLWorkbook();
        var cell = wb.Worksheets.Add("t").Cell(1, 1);

        GeneradorExcelProgramaBase.AplicarOverlayExcel(cell,
            new ReportStyleOverlay(Fuente: "Arial", TamanoFuente: 12, Negrita: true, Cursiva: true,
                ColorFuente: "#112233", ColorFondo: "#ABCDEF"));

        Assert.AreEqual("Arial", cell.Style.Font.FontName);
        Assert.AreEqual(12, cell.Style.Font.FontSize);
        Assert.IsTrue(cell.Style.Font.Bold);
        Assert.IsTrue(cell.Style.Font.Italic);
        Assert.AreEqual(XLColor.FromHtml("#112233"), cell.Style.Font.FontColor);
        Assert.AreEqual(XLColor.FromHtml("#ABCDEF"), cell.Style.Fill.BackgroundColor);
    }

    [TestMethod]
    public void Excel_Resumen_DefaultAplicaGrisYNegritaPorRol()
    {
        using var wb = new XLWorkbook();
        var cell = wb.Worksheets.Add("t").Cell(1, 1);

        GeneradorExcelProgramaBase.AplicarEstiloCeldaPrograma(
            cell, SnapshotObraConDefaults(), "RES:1", RolFilaReporte.Resumen, "colImporte");

        Assert.AreEqual(XLColor.FromHtml("#F0F0F0"), cell.Style.Fill.BackgroundColor);
        Assert.IsTrue(cell.Style.Font.Bold);
    }

    [TestMethod]
    public void Excel_OverrideDeCelda_GanaSobreElRol()
    {
        var snapshot = SnapshotObraConDefaults() with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(ProgramaObraReportSnapshotBuilder.ClaveVista, "RES:1", "colImporte"),
                    new ReportStyleOverlay(ColorFondo: "#FF00AA")),
            }
        };

        using var wb = new XLWorkbook();
        var cell = wb.Worksheets.Add("t").Cell(1, 1);

        GeneradorExcelProgramaBase.AplicarEstiloCeldaPrograma(
            cell, snapshot, "RES:1", RolFilaReporte.Resumen, "colImporte");

        Assert.AreEqual(XLColor.FromHtml("#FF00AA"), cell.Style.Fill.BackgroundColor,
            "El override de celda tiene la máxima precedencia.");
    }

    // ─────────────────────────── PDF ───────────────────────────

    [TestMethod]
    public void Pdf_ResolverOverlay_ResumenAplicaGrisYNegritaPorRol()
    {
        var overlay = GeneradorPdfProgramaObra.ResolverOverlayPdf(
            SnapshotObraConDefaults(), "RES:1", RolFilaReporte.Resumen, "colImporte");

        Assert.AreEqual("#F0F0F0", overlay.ColorFondo);
        Assert.AreEqual((bool?)true, overlay.Negrita);
    }

    [TestMethod]
    public void Pdf_ResolverOverlay_ActividadNoImponeColorDeRol()
    {
        var snapshot = SnapshotObraConDefaults();
        var columna = snapshot.Columnas.Single(c => c.Identificador == "colImporte");

        var overlay = GeneradorPdfProgramaObra.ResolverOverlayPdf(
            snapshot, "ACT:2", RolFilaReporte.Concepto, "colImporte");

        Assert.AreEqual(columna.EstiloContenido.ColorFondo, overlay.ColorFondo,
            "La actividad hereda el color de la columna (no impone el gris del rol Resumen).");
        Assert.AreNotEqual("#F0F0F0", overlay.ColorFondo);
    }

    [TestMethod]
    public void Pdf_ResolverOverlay_OverrideDeCelda_GanaSobreElRol()
    {
        var snapshot = SnapshotObraConDefaults() with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(
                    new ReportCellAddress(ProgramaObraReportSnapshotBuilder.ClaveVista, "ACT:2", "colImporte"),
                    new ReportStyleOverlay(ColorFondo: "#FF00AA")),
            }
        };

        var overlay = GeneradorPdfProgramaObra.ResolverOverlayPdf(
            snapshot, "ACT:2", RolFilaReporte.Concepto, "colImporte");

        Assert.AreEqual("#FF00AA", overlay.ColorFondo);
    }

    // ───────────────── H3: el override de UNA celda no se derrama a la fila ─────────────────

    private static readonly ReportCellAddress CeldaImporteActividad2 =
        new(ProgramaObraReportSnapshotBuilder.ClaveVista, "ACT:2", "colImporte");

    private static ReportColumnSnapshot SnapshotConOverrideEnUnaCelda()
        => SnapshotObraConDefaults() with
        {
            EstilosCelda = new[]
            {
                new ReportCellStyleOverride(CeldaImporteActividad2, new ReportStyleOverlay(ColorFondo: "#FF00AA")),
            }
        };

    [TestMethod]
    public void Pdf_OverrideDeUnaCelda_NoSeDerramaAOtrasColumnasDeLaFila()
    {
        var snapshot = SnapshotConOverrideEnUnaCelda();

        var importe = GeneradorPdfProgramaObra.ResolverOverlayPdf(snapshot, "ACT:2", RolFilaReporte.Concepto, "colImporte");
        var cantidad = GeneradorPdfProgramaObra.ResolverOverlayPdf(snapshot, "ACT:2", RolFilaReporte.Concepto, "colCantidad");

        Assert.AreEqual("#FF00AA", importe.ColorFondo, "El override aparece SOLO en su celda.");
        Assert.AreNotEqual("#FF00AA", cantidad.ColorFondo,
            "Otra columna de la MISMA fila no debe recibir el override (H3).");
    }

    [TestMethod]
    public void Excel_OverrideDeUnaCelda_NoSeDerramaAOtrasColumnasDeLaFila()
    {
        var snapshot = SnapshotConOverrideEnUnaCelda();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("t");
        var celdaImporte = ws.Cell(1, 1);
        var celdaCantidad = ws.Cell(1, 2);

        GeneradorExcelProgramaBase.AplicarEstiloCeldaPrograma(celdaImporte, snapshot, "ACT:2", RolFilaReporte.Concepto, "colImporte");
        GeneradorExcelProgramaBase.AplicarEstiloCeldaPrograma(celdaCantidad, snapshot, "ACT:2", RolFilaReporte.Concepto, "colCantidad");

        Assert.AreEqual(XLColor.FromHtml("#FF00AA"), celdaImporte.Style.Fill.BackgroundColor,
            "El override aparece SOLO en su celda.");
        Assert.AreNotEqual(XLColor.FromHtml("#FF00AA"), celdaCantidad.Style.Fill.BackgroundColor,
            "Otra columna de la MISMA fila no debe recibir el override (H3).");
    }
}
