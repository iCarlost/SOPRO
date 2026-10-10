using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Formatting;

/// <summary>
/// Tests de <see cref="ReportNumberFormatMapper"/>: tablas legacy POR RENDERIZADOR
/// (PDF y Excel divergen), formatos .NET explícitos y valor por defecto.
///
/// Regresión del reporte del usuario: el token "C4" NUNCA debe producir símbolo
/// de moneda ('$' ni el glifo ¤). En PDF se mapea a "#,##0.0000" (numérico sin
/// símbolo) y en Excel cae al default legacy "#,##0.00" (también sin símbolo).
/// </summary>
[TestClass]
public class ReportNumberFormatMapperTests
{
    // ─────────────────────────── Ruta PDF (legacy) ───────────────────────────

    [TestMethod]
    [DataRow("N0", "#,##0")]
    [DataRow("N2", "#,##0.00")]
    [DataRow("N3", "#,##0.000")]
    [DataRow("N4", "#,##0.0000")]
    [DataRow("N5", "#,##0.00000")]
    [DataRow("C0", "#,##0")]
    [DataRow("C1", "#,##0.0")]
    [DataRow("C2", "$#,##0.00")]
    [DataRow("C3", "#,##0.000")]
    [DataRow("C4", "#,##0.0000")] // monetario sin símbolo (elimina el glifo ¤)
    [DataRow("N1", "N1")]   // passthrough literal (no existe en la tabla legacy PDF)
    public void ToPdfFormat_TokenDelContrato_DevuelveTablaLegacyPdf(string token, string esperado)
    {
        Assert.AreEqual(esperado, ReportNumberFormatMapper.ToPdfFormat(token));
    }

    // ────────────────────────── Ruta Excel (legacy) ──────────────────────────

    [TestMethod]
    [DataRow("N0", "#,##0")]
    [DataRow("N2", "#,##0.00")]
    [DataRow("N3", "#,##0.000")]
    [DataRow("N4", "#,##0.0000")]
    [DataRow("N5", "#,##0.00000")]
    [DataRow("C2", "$#,##0.00")]
    [DataRow("P2", "0.00%")]
    // Tokens letra no soportados en Excel legacy → default seguro sin '$'.
    [DataRow("C0", "#,##0.00")]
    [DataRow("C1", "#,##0.00")]
    [DataRow("C3", "#,##0.00")]
    [DataRow("C4", "#,##0.00")]
    [DataRow("N1", "#,##0.00")]
    [DataRow("P0", "#,##0.00")]
    [DataRow("P1", "#,##0.00")]
    [DataRow("P3", "#,##0.00")]
    public void ToExcelFormat_TokenDelContrato_DevuelveTablaLegacyExcel(string token, string esperado)
    {
        Assert.AreEqual(esperado, ReportNumberFormatMapper.ToExcelFormat(token));
    }

    // ──────────────────── Formatos .NET explícitos (ambas) ────────────────────

    [TestMethod]
    public void FormatoNetExplicito_SeRespetaEnAmbasRutas()
    {
        foreach (var explicito in new[] { "#,##0.0000", "0.0000", "0.00" })
        {
            Assert.AreEqual(explicito, ReportNumberFormatMapper.ToPdfFormat(explicito));
            Assert.AreEqual(explicito, ReportNumberFormatMapper.ToExcelFormat(explicito));
        }
    }

    [TestMethod]
    public void Map_FormatoNetExplicito_SeRespeta()
    {
        Assert.AreEqual("#,##0.0000", ReportNumberFormatMapper.Map("#,##0.0000"));
        Assert.AreEqual("0.0000", ReportNumberFormatMapper.Map("0.0000"));
    }

    // ────────────────────────────── Default / vacío ──────────────────────────────

    [TestMethod]
    public void NullOBlanco_DevuelveDefaultN2EnAmbasRutas()
    {
        foreach (var vacio in new string?[] { null, "", "   " })
        {
            Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.ToPdfFormat(vacio));
            Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.ToExcelFormat(vacio));
        }

        Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.Map(null));
        Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.Map(""));
        Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.Map("   "));
        Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.Map("N2"));
    }

    [TestMethod]
    public void TokenEsCaseInsensitive()
    {
        Assert.AreEqual("#,##0.00", ReportNumberFormatMapper.Map("n2"));
        Assert.AreEqual("#,##0.00", ReportNumberFormatMapper.ToPdfFormat("n2"));
        Assert.AreEqual("#,##0.00", ReportNumberFormatMapper.ToExcelFormat("n2"));
    }

    [TestMethod]
    public void Map_EquivaleAToPdfFormat()
    {
        foreach (var token in new[] { "N0", "N2", "N5", "C2", "C4", "P2", "#,##0.0000", "0.00", "", null })
        {
            Assert.AreEqual(
                ReportNumberFormatMapper.ToPdfFormat(token),
                ReportNumberFormatMapper.Map(token),
                $"Map('{token}') debe equivaler a ToPdfFormat.");
        }
    }

    // ──────────────────────── Regresión reporte del usuario ────────────────────────

    [TestMethod]
    public void Regresion_TokenC4_NoIntroduceSimboloDeMoneda()
    {
        var excel = ReportNumberFormatMapper.ToExcelFormat("C4");
        var pdf = ReportNumberFormatMapper.ToPdfFormat("C4");

        Assert.AreEqual("#,##0.00", excel, "Excel legacy coerce 'C4' al default sin moneda.");
        Assert.AreEqual("#,##0.0000", pdf, "'C4' se mapea a numérico de 4 decimales sin símbolo.");
        Assert.IsFalse(excel.Contains('$'), "El Excel legacy nunca introduce '$' para 'C4'.");
        Assert.IsFalse(pdf.Contains('$'), "La ruta PDF de 'C4' no debe contener '$'.");
        Assert.IsFalse(pdf.Contains('\u00A4'), "La ruta PDF de 'C4' no debe contener el glifo ¤ (U+00A4).");
        Assert.AreEqual(
            "1,234.5678",
            1234.5678m.ToString(pdf, System.Globalization.CultureInfo.InvariantCulture),
            "La ruta PDF de 'C4' debe renderizar el número sin símbolo de moneda.");
    }

    [TestMethod]
    public void Regresion_TokenC2_MantieneMonedaLegacy()
    {
        Assert.AreEqual("$#,##0.00", ReportNumberFormatMapper.ToPdfFormat("C2"));
        Assert.AreEqual("$#,##0.00", ReportNumberFormatMapper.ToExcelFormat("C2"));
        Assert.IsTrue(ReportNumberFormatMapper.ToPdfFormat("C2").Contains('$'));
        Assert.IsTrue(ReportNumberFormatMapper.ToExcelFormat("C2").Contains('$'));
    }
}
