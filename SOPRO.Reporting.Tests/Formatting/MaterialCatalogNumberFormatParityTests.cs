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
/// Defecto: la ruta PDF de materiales debe traducir el token del contrato con la
/// ruta central <see cref="ReportNumberFormatMap"/> (no aplicar el token crudo),
/// respetando la fidelidad legacy POR RENDERIZADOR.
///
/// La "paridad" PDF/Excel es de cadena sólo para los tokens legacy comunes
/// (N2, "#,##0.0000", C2). Para C4/P2 las cadenas divergen legítimamente
/// (fidelidad 8afabde) y lo que se verifica es la SEMÁNTICA: en particular que
/// "C4" NUNCA produzca el símbolo '$' en ninguna ruta (regresión del usuario).
/// </summary>
[TestClass]
public class MaterialCatalogNumberFormatParityTests
{
    private static ReportColumnDefinition Mapear(string token) =>
        ReportColumnDefinitionMapper.MapearMaterial(new ColumnaMaterial
        {
            ProyectoId = 1,
            Nombre = "Precio Unitario",
            NombreInterno = "PrecioUnitario",
            Orden = 1,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = token,
        });

    private static MaterialListItem Material(decimal precio) =>
        new(1, "MAT-1", "Descripción", "pza", precio, string.Empty, OrigenInsumo.Proyecto);

    // ─────────────── Tokens legacy comunes: misma cadena mapeada ───────────────

    [DataTestMethod]
    [DataRow("N2")]
    [DataRow("#,##0.0000")]
    [DataRow("C2")]
    public void RutaPdfDeMateriales_CoincideConExcelEnTokensLegacyComunes(string token)
    {
        var material = Material(1234.5m);

        var pdf = MaterialCatalogExportResolver.ResolveValue(material, Mapear(token));
        var excelFmt = ReportNumberFormatMapper.ToExcelFormat(token);
        var pdfFmt = ReportNumberFormatMap.ToPdfFormat(token);

        // Mismo string mapeado en ambas rutas para los tokens legacy comunes.
        Assert.AreEqual(excelFmt, pdfFmt, $"'{token}' debe mapear igual en PDF y Excel.");
        Assert.AreEqual(
            material.PrecioUnitario.ToString(pdfFmt, CultureInfo.InvariantCulture),
            pdf,
            "La ruta PDF usa el formato mapeado (no el token crudo).");
    }

    // ──────────── C4: divergencia legítima, pero SIN símbolo de moneda ────────────

    [TestMethod]
    public void TokenC4_NoIntroduceSimboloDeMonedaEnNingunaRuta()
    {
        // PDF legacy: passthrough literal; Excel legacy: default sin moneda.
        Assert.AreEqual("C4", ReportNumberFormatMap.ToPdfFormat("C4"));
        Assert.AreEqual("#,##0.00", ReportNumberFormatMapper.ToExcelFormat("C4"));
        Assert.IsFalse(ReportNumberFormatMap.ToPdfFormat("C4").Contains('$'));
        Assert.IsFalse(ReportNumberFormatMapper.ToExcelFormat("C4").Contains('$'));

        var material = Material(1234.5m);

        // Ruta PDF de materiales (ResolveValue → FormatearNumero con InvariantCulture).
        var pdf = MaterialCatalogExportResolver.ResolveValue(material, Mapear("C4"));
        Assert.IsFalse(pdf.Contains('$'), "Regresión del usuario: 'C4' no debe mostrar '$'.");
        Assert.AreEqual(
            1234.5m.ToString("C4", CultureInfo.InvariantCulture),
            pdf,
            "El passthrough de 'C4' se aplica con InvariantCulture (símbolo ¤ genérico).");

        // Ruta Excel.
        var excel = material.PrecioUnitario.ToString(
            ReportNumberFormatMapper.ToExcelFormat("C4"), CultureInfo.InvariantCulture);
        Assert.IsFalse(excel.Contains('$'), "Excel legacy tampoco introduce '$' para 'C4'.");
    }

    [TestMethod]
    public void TokenC2_MantieneMonedaLegacyEnAmbasRutas()
    {
        Assert.AreEqual("$#,##0.00", ReportNumberFormatMap.ToPdfFormat("C2"));
        Assert.AreEqual("$#,##0.00", ReportNumberFormatMapper.ToExcelFormat("C2"));

        var pdf = MaterialCatalogExportResolver.ResolveValue(Material(10m), Mapear("C2"));
        Assert.AreEqual("$10.00", pdf);
    }

    [TestMethod]
    public void TokenP2_DivergePeroSinMoneda()
    {
        Assert.AreEqual("P2", ReportNumberFormatMap.ToPdfFormat("P2"));
        Assert.AreEqual("0.00%", ReportNumberFormatMapper.ToExcelFormat("P2"));
        Assert.IsFalse(ReportNumberFormatMap.ToPdfFormat("P2").Contains('$'));
        Assert.IsFalse(ReportNumberFormatMapper.ToExcelFormat("P2").Contains('$'));
    }

    [TestMethod]
    public void RutaPdfDeMateriales_FormatoExplicitoLegacySinMoneda()
    {
        // El formato explícito de Materiales (#,##0.0000) se respeta literalmente
        // y no lleva moneda.
        Assert.AreEqual("10.0000", MaterialCatalogExportResolver.ResolveValue(Material(10m), Mapear("#,##0.0000")));
    }
}
