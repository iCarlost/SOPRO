using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Formatting;

/// <summary>
/// Paridad grid↔export del Catálogo de Materiales. El grid es la fuente de verdad
/// visual:
/// <list type="bullet">
///   <item>Precio Unitario (por <c>NombreInterno</c>) es monetario → símbolo de
///   moneda de la cultura actual + <c>DecimalesImporte</c>, SIN depender del token
///   (el default de Materiales es <c>#,##0.0000</c>, pero el grid lo pinta con '$').</item>
///   <item>Cualquier columna con token <c>C*</c> es monetaria → <c>C{DecimalesImporte}</c>.</item>
///   <item>Columna numérica no monetaria (<c>N*</c>) → <c>N{DecimalesCantidad}</c>, sin símbolo.</item>
/// </list>
/// La tabla legacy token→formato (<see cref="ReportNumberFormatMap"/>) se conserva
/// y sigue SIN símbolo para <c>C4</c>; la paridad de P.U. no pasa por esa tabla.
/// </summary>
[TestClass]
public class MaterialCatalogNumberFormatParityTests
{
    private static ReportColumnDefinition Mapear(string nombreInterno, string token) =>
        ReportColumnDefinitionMapper.MapearMaterial(new ColumnaMaterial
        {
            ProyectoId = 1,
            Nombre = nombreInterno,
            NombreInterno = nombreInterno,
            Orden = 1,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = token,
        });

    private static ReportColumnSnapshot Snapshot(
        int decimalesCantidad,
        int decimalesImporte,
        int decimalesPorcentaje,
        params ReportColumnDefinition[] columnas)
        => new(
            TipoReporte: "Materiales",
            ProyectoId: 1,
            Titulo: "T",
            Columnas: columnas,
            EstiloTabla: ReportTableStyle.LegacyMateriales())
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };

    private static MaterialListItem Material(decimal precio) =>
        new(1, "MAT-1", "Descripción", "pza", precio, string.Empty, OrigenInsumo.Proyecto);

    // ───────── Precio Unitario: monetario por NombreInterno (paridad grid) ─────────

    [TestMethod]
    public void PrecioUnitario_UsaDecimalesImporteConSimbolo_IgualQueElGrid()
    {
        var pu = Mapear("PrecioUnitario", "#,##0.0000");
        var snapshot = Snapshot(4, 2, 4, pu);
        const decimal valor = 1234.5m;

        var pdf = ReportColumnGridFormat.FormatearPdf(valor, pu, snapshot);

        Assert.IsTrue(pu.EsMoneda,
            "El Precio Unitario debe ser monetario aunque su token sea '#,##0.0000'.");
        Assert.AreEqual(valor.ToString("C2", CultureInfo.CurrentCulture), pdf,
            "El P.U. debe usar decimales de importe + símbolo de moneda de la cultura actual.");
        Assert.IsTrue(pdf.Contains(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol),
            "El P.U. debe mostrar el símbolo de moneda, igual que el grid.");
    }

    [TestMethod]
    public void PrecioUnitario_ConConfigGrid4y2_EquivaleAC2ConSimbolo()
    {
        var pu = Mapear("PrecioUnitario", "C4");
        var snapshot = Snapshot(4, 2, 4, pu);
        const decimal valor = 1234.5m;

        Assert.AreEqual(
            valor.ToString("C2", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPdf(valor, pu, snapshot),
            "Con DecimalesImporte=2 el P.U. debe renderizarse como C2.");
        Assert.AreEqual(
            ReportColumnGridFormat.FormatoMonedaExcel(2, CultureInfo.CurrentCulture),
            ReportColumnGridFormat.ResolveExcelFormat(pu, snapshot),
            "Excel: P.U. con símbolo de moneda + 2 decimales.");
    }

    [TestMethod]
    public void ResolveValue_PrecioUnitario_UsaParidadConElSnapshot()
    {
        var pu = Mapear("PrecioUnitario", "#,##0.0000");
        var snapshot = Snapshot(4, 2, 4, pu);

        Assert.AreEqual(
            60m.ToString("C2", CultureInfo.CurrentCulture),
            MaterialCatalogExportResolver.ResolveValue(Material(60m), pu, snapshot),
            "La ruta real de export (ResolveValue con snapshot) debe usar paridad grid.");
    }

    // ───────────── Regresión (cultura fija): Importe=2 en en-US → '$' ─────────────

    [TestMethod]
    public void Regresion_PrecioUnitarioImporte2_EnUS_MuestraDolarConDosDecimales()
    {
        var previa = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            var pu = Mapear("PrecioUnitario", "C4");
            var snapshot = Snapshot(4, 2, 4, pu);

            Assert.AreEqual("$1,234.56", ReportColumnGridFormat.FormatearPdf(1234.56m, pu, snapshot),
                "PDF: P.U. con DecimalesImporte=2 y cultura en-US → '$1,234.56'.");
            Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.ResolveExcelFormat(pu, snapshot),
                "Excel: P.U. con DecimalesImporte=2 → '$#,##0.00'.");
        }
        finally
        {
            CultureInfo.CurrentCulture = previa;
        }
    }

    // ───────────── Token C* en columna no P.U.: también es monetaria ─────────────

    [TestMethod]
    public void TokenC4_EnColumnaNoPU_EsMonetaria()
    {
        var col = Mapear("Extra", "C4");

        Assert.IsTrue(col.EsMoneda, "Un token 'C4' debe marcar la columna como monetaria.");

        var snapshot = Snapshot(4, 2, 4, col);
        Assert.AreEqual(
            1234.5m.ToString("C2", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPdf(1234.5m, col, snapshot));
    }

    // ───────────── Columna numérica no monetaria (N*): sin símbolo ─────────────

    [TestMethod]
    public void NumeroN2_NoLlevaSimboloDeMoneda()
    {
        var cantidad = Mapear("Cantidad", "N2");
        var snapshot = Snapshot(4, 2, 4, cantidad);

        Assert.IsFalse(cantidad.EsMoneda, "Una columna 'Cantidad' con token 'N2' no es moneda.");

        var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, cantidad, snapshot);
        Assert.IsFalse(pdf.Contains('$'), "N2 no debe introducir '$'.");
        Assert.IsFalse(pdf.Contains('\u00A4'), "N2 no debe introducir el glifo ¤.");
        Assert.AreEqual(1234.5m.ToString("N4", CultureInfo.CurrentCulture), pdf,
            "N2 en el grid usa los decimales de cantidad del proyecto.");
    }

    // ───────── Tabla legacy token→formato: sigue sin símbolo para C4 ─────────

    [TestMethod]
    public void TablaLegacy_TokensMonetariosSinSimbolo()
    {
        Assert.AreEqual("#,##0.0000", ReportNumberFormatMap.ToPdfFormat("C4"));
        Assert.AreEqual("#,##0.00", ReportNumberFormatMapper.ToExcelFormat("C4"));
        Assert.IsFalse(ReportNumberFormatMap.ToPdfFormat("C4").Contains('$'));
        Assert.IsFalse(ReportNumberFormatMapper.ToExcelFormat("C4").Contains('$'));
        Assert.IsFalse(ReportNumberFormatMap.ToPdfFormat("C4").Contains('\u00A4'));
        Assert.IsFalse(ReportNumberFormatMapper.ToExcelFormat("C4").Contains('\u00A4'));
    }

    // ───────────── Excel: P.U. sigue los decimales de importe del proyecto ─────────────

    [TestMethod]
    public void Excel_PrecioUnitario_UsaSimboloYDecimalesDeImporte()
    {
        var pu = Mapear("PrecioUnitario", "#,##0.0000");
        var snapshot = Snapshot(4, 3, 4, pu);

        Assert.AreEqual(
            ReportColumnGridFormat.FormatoMonedaExcel(3, CultureInfo.CurrentCulture),
            ReportColumnGridFormat.ResolveExcelFormat(pu, snapshot),
            "Excel: el P.U. debe seguir DecimalesImporte (3) con símbolo de moneda.");
    }
}
