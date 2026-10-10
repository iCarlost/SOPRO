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

    // ───────────── Fase 0: centralización del símbolo monetario '$' ─────────────

    [TestMethod]
    public void SimboloMoneda_EsDolarYNoDependeDeLaCultura()
    {
        Assert.AreEqual("$", ReportColumnGridFormat.SimboloMoneda);

        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.FormatoMonedaExcel(2, new CultureInfo("de-DE")),
            "El símbolo debe ser '$' aunque la cultura sea de-DE (€).");
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.FormatoMonedaExcel(2, CultureInfo.InvariantCulture),
            "El símbolo debe ser '$' aunque la cultura sea la invariante (¤).");
    }

    [TestMethod]
    public void FormatearPdf_Monetario_UsaSiempreDolarAunqueCambieLaCultura()
    {
        var pu = Columna("PrecioUnitario", TipoDatoColumna.Moneda, "C4");
        var snapshot = Snapshot(4, 2, 4, pu);
        var previa = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var pdf = ReportColumnGridFormat.FormatearPdf(1234.5m, pu, snapshot);

            Assert.IsTrue(pdf.Contains("$"), "El símbolo debe ser el '$' centralizado.");
            Assert.IsFalse(pdf.Contains("€"), "No debe filtrarse el símbolo de la cultura (€).");
            Assert.IsFalse(pdf.Contains('\u00A4'), "No debe filtrarse el glifo ¤ de la invariante.");
            Assert.IsTrue(pdf.Contains("1.234,50"), "Los separadores sí siguen la cultura actual (de-DE).");
        }
        finally
        {
            CultureInfo.CurrentCulture = previa;
        }
    }

    // ───────────── Fase 0: formatos para 0–N decimales ─────────────

    [TestMethod]
    public void BloquesDecimales_DeCeroAN()
    {
        Assert.AreEqual("#,##0", ReportColumnGridFormat.FormatoNumeroExcel(0));
        Assert.AreEqual("#,##0.0", ReportColumnGridFormat.FormatoNumeroExcel(1));
        Assert.AreEqual("#,##0.00", ReportColumnGridFormat.FormatoNumeroExcel(2));
        Assert.AreEqual("#,##0.0000", ReportColumnGridFormat.FormatoNumeroExcel(4));

        Assert.AreEqual("$#,##0", ReportColumnGridFormat.FormatoMonedaPdf(0));
        Assert.AreEqual("$#,##0.000", ReportColumnGridFormat.FormatoMonedaPdf(3));
        Assert.AreEqual("$#,##0.00", ReportColumnGridFormat.FormatoMonedaExcel(2, CultureInfo.CurrentCulture));

        Assert.AreEqual("0%", ReportColumnGridFormat.FormatoPorcentajeExcel(0));
        Assert.AreEqual("0.00%", ReportColumnGridFormat.FormatoPorcentajeExcel(2));
        Assert.AreEqual("0.0000%", ReportColumnGridFormat.FormatoPorcentajeExcel(4));
    }

    // ───────────── Fase 0: negativos y ceros ─────────────

    [TestMethod]
    public void Monetario_CeroYNegativo()
    {
        var pu = Columna("PrecioUnitario", TipoDatoColumna.Moneda, "C2");
        var snapshot = Snapshot(2, 2, 4, pu);

        var cero = ReportColumnGridFormat.FormatearPdf(0m, pu, snapshot);
        Assert.IsTrue(cero.Contains("$"));
        Assert.IsTrue(cero.Contains("0.00"));

        var negativo = ReportColumnGridFormat.FormatearPdf(-1234.5m, pu, snapshot);
        Assert.IsTrue(negativo.Contains("$"), "El negativo debe conservar el '$' centralizado.");
        Assert.IsTrue(negativo.Contains("1,234.50"));
        Assert.IsTrue(negativo.Contains("-"), "El negativo debe conservar el signo.");
    }

    // ───────────── Fase 0: rol de porcentaje (opt-in) ─────────────

    [TestMethod]
    public void Porcentaje_RolYFormatoOptIn()
    {
        var porcentaje = Columna("PorcentajeIndirectos", TipoDatoColumna.Porcentaje, "P2");
        var explicito = Columna("Rendimiento", TipoDatoColumna.Numerico, "#,##0.0000");

        Assert.IsTrue(ReportColumnGridFormat.EsPorcentajeGrid(porcentaje));
        Assert.IsTrue(ReportColumnGridFormat.EsPorcentajeGrid(Columna("Extra", TipoDatoColumna.Numerico, "P4")));
        Assert.IsFalse(ReportColumnGridFormat.EsPorcentajeGrid(explicito));

        var snapshot = Snapshot(2, 2, 4, porcentaje);
        Assert.AreEqual(
            12.3456m.ToString("N4", CultureInfo.CurrentCulture),
            ReportColumnGridFormat.FormatearPorcentajePdf(12.3456m, snapshot),
            "El porcentaje PDF usa DecimalesPorcentaje del proyecto (sin '%', paridad con FormatPorcentaje).");
        Assert.AreEqual("0.0000%", ReportColumnGridFormat.FormatoPorcentajeExcel(snapshot.DecimalesPorcentaje));
    }

    // ───────────── Fase 0: columna de texto sin formato ─────────────

    [TestMethod]
    public void ColumnaTexto_SinFormatoNiRol_UsaDefaultLegacy()
    {
        var col = Columna("Observaciones", TipoDatoColumna.Texto, null);
        var snapshot = Snapshot(2, 2, 4, col);

        Assert.AreEqual(string.Empty, col.FormatoNumerico);
        Assert.IsFalse(ReportColumnGridFormat.EsMonedaGrid(col));
        Assert.IsFalse(ReportColumnGridFormat.EsCantidadGrid(col));
        Assert.IsFalse(ReportColumnGridFormat.EsPorcentajeGrid(col));

        Assert.AreEqual(
            1234.5m.ToString(ReportNumberFormatMapper.ToPdfFormat(string.Empty), CultureInfo.InvariantCulture),
            ReportColumnGridFormat.FormatearPdf(1234.5m, col, snapshot));
    }
}
