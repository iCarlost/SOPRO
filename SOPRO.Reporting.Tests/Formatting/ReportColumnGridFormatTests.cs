using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Formatting;

/// <summary>
/// Regresión de PARIDAD con el grid para la exportación de Presupuesto:
/// <list type="bullet">
///   <item>Una columna monetaria (P.U. con token "C4") NO pasa por la traducción
///   token→formato legacy; se formatea con los decimales de importe del proyecto y
///   el símbolo de moneda de la cultura actual (misma semántica que
///   <c>MotorCalculoSopro.FormatImporte</c>).</item>
///   <item>La columna Cantidad usa los decimales de cantidad del proyecto (misma
///   semántica que <c>MotorCalculoSopro.FormatCantidad</c>).</item>
///   <item>El resto de columnas conserva la traducción token→formato legacy.</item>
/// </list>
/// El renderizador PDF/Excel de WinForms no es referenciable desde este proyecto,
/// pero ambos consumen <see cref="ReportColumnGridFormat"/>, que es la regla única
/// y aquí se verifica directamente.
/// </summary>
[TestClass]
public class ReportColumnGridFormatTests
{
    private static ReportColumnDefinition Columna(string nombreInterno, TipoDatoColumna tipo, string? formato)
        => ReportColumnDefinitionMapper.MapearPresupuesto(new ColumnaPersonalizada
        {
            NombreInterno = nombreInterno,
            Nombre = nombreInterno,
            TipoDato = tipo,
            FormatoNumerico = formato ?? string.Empty,
            Alineacion = AlineacionColumna.Derecha
        }, null);

    private static ReportColumnSnapshot Snapshot(
        int decimalesCantidad,
        int decimalesImporte,
        int decimalesPorcentaje,
        params ReportColumnDefinition[] columnas)
        => new(
            TipoReporte: "Presupuesto",
            ProyectoId: 1,
            Titulo: "T",
            Columnas: columnas,
            EstiloTabla: ReportTableStyle.LegacyPresupuesto())
        {
            DecimalesCantidad = decimalesCantidad,
            DecimalesImporte = decimalesImporte,
            DecimalesPorcentaje = decimalesPorcentaje
        };

    // ───────────── P.U. monetario (token C4) → decimales de importe ─────────────

    [TestMethod]
    public void PrecioUnitarioMonetario_ConConfigGrid4y2_EquivaleAC2ConSimbolo()
    {
        var pu = Columna("PrecioUnitario", TipoDatoColumna.Moneda, "C4");
        var snapshot = Snapshot(4, 2, 4, pu);
        const decimal valor = 1234.5m;

        var pdf = ReportColumnGridFormat.FormatearPdf(valor, pu, snapshot);

        Assert.AreEqual(
            valor.ToString("C2", CultureInfo.CurrentCulture),
            pdf,
            "El P.U. debe formatearse como C2 (decimales de importe) con la cultura actual.");
        Assert.IsTrue(
            pdf.Contains(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol),
            "El P.U. debe incluir el símbolo de moneda de la cultura actual.");
        Assert.AreNotEqual(
            valor.ToString("#,##0.0000", CultureInfo.InvariantCulture),
            pdf,
            "El P.U. (EsMoneda) NO debe pasar por la traducción legacy del token C4.");

        Assert.AreEqual(
            ReportColumnGridFormat.FormatoMonedaExcel(2, CultureInfo.CurrentCulture),
            ReportColumnGridFormat.ResolveExcelFormat(pu, snapshot),
            "Excel: el P.U. debe usar símbolo de moneda + 2 decimales.");
    }

    [TestMethod]
    public void Cantidad_ConConfigGrid4_UsaDecimalesCantidad()
    {
        var cantidad = Columna("Cantidad", TipoDatoColumna.Numerico, "N2");
        var snapshot = Snapshot(4, 2, 4, cantidad);
        const decimal valor = 1234.5m;

        var pdf = ReportColumnGridFormat.FormatearPdf(valor, cantidad, snapshot);

        Assert.AreEqual(valor.ToString("N4", CultureInfo.CurrentCulture), pdf);
        Assert.AreEqual("#,##0.0000", ReportColumnGridFormat.ResolveExcelFormat(cantidad, snapshot));
    }

    // ────────────────────── Conmutación de decimales ──────────────────────

    [TestMethod]
    public void DecimalesImporteCambia_ElPrecioUnitarioSigueLosDecimalesDelProyecto()
    {
        var pu = Columna("PrecioUnitario", TipoDatoColumna.Moneda, "C4");
        const decimal valor = 1234.5m;

        var conDos = Snapshot(4, 2, 4, pu);
        Assert.AreEqual(
            valor.ToString("C2", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPdf(valor, pu, conDos));

        var conTres = Snapshot(4, 3, 4, pu);
        Assert.AreEqual(
            valor.ToString("C3", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPdf(valor, pu, conTres));
        Assert.AreEqual(
            ReportColumnGridFormat.FormatoMonedaExcel(3, CultureInfo.CurrentCulture),
            ReportColumnGridFormat.ResolveExcelFormat(pu, conTres));
    }

    [TestMethod]
    public void ImporteMonedaConTokenNormalizado_IgnoraElTokenYUsaMoneda()
    {
        // "Importe" se normaliza a ImporteTotal y su formato vacío a "N2"; el grid
        // ignora ese token para Moneda y usa C{DecimalesImporte}.
        var importe = Columna("Importe", TipoDatoColumna.Moneda, null);
        var snapshot = Snapshot(4, 2, 4, importe);
        const decimal valor = 1234.5m;

        Assert.AreEqual("ImporteTotal", importe.Identificador);
        Assert.AreEqual("N2", importe.FormatoNumerico);
        Assert.IsTrue(importe.EsMoneda);

        Assert.AreEqual(
            valor.ToString("C2", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPdf(valor, importe, snapshot));
        Assert.AreNotEqual(valor.ToString("N2", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPdf(valor, importe, snapshot));
    }

    // ─────────────────────────── EsMoneda por token ───────────────────────────

    [TestMethod]
    public void EsMoneda_TokensC2C4VsN2YFormatoExplicito()
    {
        Assert.IsTrue(Columna("A", TipoDatoColumna.Moneda, "C2").EsMoneda);
        Assert.IsTrue(Columna("B", TipoDatoColumna.Numerico, "C4").EsMoneda);
        Assert.IsFalse(Columna("Cantidad", TipoDatoColumna.Numerico, "N2").EsMoneda);
        Assert.IsFalse(Columna("D", TipoDatoColumna.Numerico, "#,##0.0000").EsMoneda);
    }

    // ─────────────────── Resto de columnas: sin cambios (token) ───────────────────

    [TestMethod]
    public void ColumnaNoMonetariaNiCantidad_ConservaTraduccionLegacy()
    {
        var porcentaje = Columna("PorcentajeIndirectos", TipoDatoColumna.Porcentaje, "P2");
        var explicito = Columna("Rendimiento", TipoDatoColumna.Numerico, "#,##0.0000");
        var snapshot = Snapshot(4, 2, 4, porcentaje, explicito);
        const decimal valor = 12.3456m;

        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(porcentaje));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(porcentaje));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(explicito),
            "Un formato explícito '#,##0.0000' no es token 'N*'.");

        Assert.AreEqual(
            valor.ToString(ReportNumberFormatMapper.ToPdfFormat("P2"), CultureInfo.InvariantCulture),
            ReportColumnGridFormat.FormatearPdf(valor, porcentaje, snapshot));
        Assert.AreEqual(
            ReportNumberFormatMapper.ToExcelFormat("P2"),
            ReportColumnGridFormat.ResolveExcelFormat(porcentaje, snapshot));
        Assert.AreEqual(
            valor.ToString(ReportNumberFormatMapper.ToPdfFormat("#,##0.0000"), CultureInfo.InvariantCulture),
            ReportColumnGridFormat.FormatearPdf(valor, explicito, snapshot));
    }
}
