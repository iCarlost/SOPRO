using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Formatting;

/// <summary>
/// Tests de <see cref="ReportNumberFormatMapper"/>: tabla de tokens del contrato
/// ("N0".."N5", "C2", "C4", "P2"), formatos .NET explícitos y valor por defecto.
/// </summary>
[TestClass]
public class ReportNumberFormatMapperTests
{
    [TestMethod]
    [DataRow("N0", "#,##0")]
    [DataRow("N1", "#,##0.0")]
    [DataRow("N2", "#,##0.00")]
    [DataRow("N3", "#,##0.000")]
    [DataRow("N4", "#,##0.0000")]
    [DataRow("N5", "#,##0.00000")]
    [DataRow("C0", "$#,##0")]
    [DataRow("C1", "$#,##0.0")]
    [DataRow("C2", "$#,##0.00")]
    [DataRow("C3", "$#,##0.000")]
    [DataRow("C4", "$#,##0.0000")]
    [DataRow("P0", "0%")]
    [DataRow("P1", "0.0%")]
    [DataRow("P2", "0.00%")]
    [DataRow("P3", "0.000%")]
    public void Map_TokenDelContrato_DevuelveFormatoEsperado(string token, string esperado)
    {
        Assert.AreEqual(esperado, ReportNumberFormatMapper.Map(token));
    }

    [TestMethod]
    public void Map_FormatoNetExplicito_SeRespeta()
    {
        Assert.AreEqual("#,##0.0000", ReportNumberFormatMapper.Map("#,##0.0000"));
        Assert.AreEqual("0.0000", ReportNumberFormatMapper.Map("0.0000"));
    }

    [TestMethod]
    public void Map_NullOBlanco_DevuelveDefaultN2()
    {
        Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.Map(null));
        Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.Map(""));
        Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.Map("   "));
        Assert.AreEqual(ReportNumberFormatMapper.DefaultFormat, ReportNumberFormatMapper.Map("N2"));
    }

    [TestMethod]
    public void Map_TokenEsCaseInsensitive()
    {
        Assert.AreEqual("#,##0.00", ReportNumberFormatMapper.Map("n2"));
    }

    [TestMethod]
    public void ToPdfFormat_Y_ToExcelFormat_CoincidenConMap()
    {
        foreach (var token in new[] { "N0", "N2", "N5", "C2", "C4", "P2", "#,##0.0000" })
        {
            Assert.AreEqual(ReportNumberFormatMapper.Map(token), ReportNumberFormatMapper.ToPdfFormat(token));
            Assert.AreEqual(ReportNumberFormatMapper.Map(token), ReportNumberFormatMapper.ToExcelFormat(token));
        }
    }
}
