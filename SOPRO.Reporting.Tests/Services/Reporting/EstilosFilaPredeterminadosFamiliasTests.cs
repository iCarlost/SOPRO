using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 6.4 — DEFAULTS NEUTRALES de estilo por ROL de fila de las familias
/// Financiamiento, Utilidad y FSR (<see cref="EstilosFilaPredeterminados"/>). Es
/// la ÚNICA fuente de verdad de los colores de secciones/subtotales/totales que
/// antes estaban hardcodeados en los generadores PDF/Excel; grid, PDF y Excel
/// convergen a los MISMOS valores.
/// </summary>
[TestClass]
public class EstilosFilaPredeterminadosFamiliasTests
{
    // ─────────────────────────── Financiamiento ───────────────────────────

    [TestMethod]
    public void TipoReporteFinanciamiento_EsElTokenEstable()
        => Assert.AreEqual("Financiamiento", EstilosFilaPredeterminados.TipoReporteFinanciamiento);

    [TestMethod]
    public void Financiamiento_SeccionEncabezadoYResultado_TienenLosColoresHistoricos()
    {
        Assert.AreEqual("#D9E2F3", EstilosFilaPredeterminados.Financiamiento(RolFilaReporte.Seccion).ColorFondo);

        var enc = EstilosFilaPredeterminados.Financiamiento(RolFilaReporte.Encabezado);
        Assert.AreEqual("#4A4A6A", enc.ColorFondo);
        Assert.AreEqual("#FFFFFF", enc.ColorFuente);
        Assert.AreEqual((bool?)true, enc.Negrita);

        Assert.AreEqual("#FFF2CC", EstilosFilaPredeterminados.Financiamiento(RolFilaReporte.TotalGeneral).ColorFondo);
    }

    [TestMethod]
    public void Financiamiento_RolDesconocido_EsVacio()
    {
        var estilo = EstilosFilaPredeterminados.Financiamiento("RolInexistente");
        Assert.IsNull(estilo.ColorFondo);
        Assert.IsNull(estilo.ColorFuente);
    }

    [TestMethod]
    public void Financiamiento_OverrideParcialDelUsuario_ConservaElRestoDelDefault()
    {
        var usuario = new Dictionary<string, ReportStyleOverlay>
        {
            [RolFilaReporte.Encabezado] = new ReportStyleOverlay(ColorFondo: "#010203"),
        };

        var efectivo = EstilosFilaPredeterminados.CombinarFinanciamiento(RolFilaReporte.Encabezado, usuario);

        Assert.AreEqual("#010203", efectivo.ColorFondo, "El fondo del usuario gana.");
        Assert.AreEqual("#FFFFFF", efectivo.ColorFuente, "El color de fuente hereda del default.");
        Assert.AreEqual((bool?)true, efectivo.Negrita);
    }

    [TestMethod]
    public void Financiamiento_ListaEfectiva_IncluyeTodosLosDefaults()
    {
        var lista = EstilosFilaPredeterminados.CombinarFinanciamiento((IEnumerable<ReportRowRoleStyle>?)null);

        CollectionAssert.AreEquivalent(
            EstilosFilaPredeterminados.RolesFinanciamiento.ToArray(),
            lista.Select(r => r.RolFila).ToArray());
    }

    // ─────────────────────────── Utilidad ───────────────────────────

    [TestMethod]
    public void TipoReporteUtilidad_EsElTokenEstable()
        => Assert.AreEqual("Utilidad", EstilosFilaPredeterminados.TipoReporteUtilidad);

    [TestMethod]
    public void Utilidad_SubtotalTotalYResumen_TienenLosColoresHistoricos()
    {
        var subtotal = EstilosFilaPredeterminados.Utilidad(RolFilaReporte.Subtotal);
        Assert.AreEqual("#FFF2CC", subtotal.ColorFondo);
        Assert.AreEqual("#C00000", subtotal.ColorFuente);

        Assert.AreEqual("#92D050", EstilosFilaPredeterminados.Utilidad(RolFilaReporte.Total).ColorFondo);
        Assert.AreEqual("#DDEBF7", EstilosFilaPredeterminados.Utilidad(RolFilaReporte.Resumen).ColorFondo);
    }

    [TestMethod]
    public void Utilidad_ListaEfectiva_IncluyeTodosLosDefaults()
    {
        var lista = EstilosFilaPredeterminados.CombinarUtilidad((IEnumerable<ReportRowRoleStyle>?)null);

        CollectionAssert.AreEquivalent(
            EstilosFilaPredeterminados.RolesUtilidad.ToArray(),
            lista.Select(r => r.RolFila).ToArray());
    }

    // ─────────────────────────── FSR ───────────────────────────

    [TestMethod]
    public void TipoReporteFsr_CoincideConElBuilderNeutral()
    {
        Assert.AreEqual("FactorSalarioReal", EstilosFilaPredeterminados.TipoReporteFsr);
        Assert.AreEqual("TabuladorFsr", EstilosFilaPredeterminados.TipoReporteTabuladorFsr);
    }

    [TestMethod]
    public void Fsr_SeccionReferenciaSubtotalYTotal_TienenLosColoresHistoricos()
    {
        var seccion = EstilosFilaPredeterminados.Fsr(RolFilaReporte.Seccion);
        Assert.AreEqual("#4A4A6A", seccion.ColorFondo);
        Assert.AreEqual("#FFFFFF", seccion.ColorFuente);
        Assert.AreEqual((bool?)true, seccion.Negrita);

        var referencia = EstilosFilaPredeterminados.Fsr(RolFilaReporte.Referencia);
        Assert.AreEqual("#C5CAE9", referencia.ColorFondo);
        Assert.AreEqual("#1A237E", referencia.ColorFuente);

        Assert.AreEqual("#E8EAF6", EstilosFilaPredeterminados.Fsr(RolFilaReporte.Subtotal).ColorFondo);

        var total = EstilosFilaPredeterminados.Fsr(RolFilaReporte.Total);
        Assert.AreEqual("#1A237E", total.ColorFondo);
        Assert.AreEqual("#FFFFFF", total.ColorFuente);
        Assert.AreEqual((bool?)true, total.Negrita);
    }

    [TestMethod]
    public void Fsr_OverrideParcialDelUsuario_ConservaElRestoDelDefault()
    {
        var usuario = new Dictionary<string, ReportStyleOverlay>
        {
            [RolFilaReporte.Seccion] = new ReportStyleOverlay(ColorFondo: "#010203"),
        };

        var efectivo = EstilosFilaPredeterminados.CombinarFsr(RolFilaReporte.Seccion, usuario);

        Assert.AreEqual("#010203", efectivo.ColorFondo, "El fondo del usuario gana.");
        Assert.AreEqual("#FFFFFF", efectivo.ColorFuente, "El color de fuente hereda del default.");
    }

    [TestMethod]
    public void Fsr_ListaEfectiva_IncluyeTodosLosDefaultsYConservaRolesSinDefault()
    {
        var usuario = new[]
        {
            new ReportRowRoleStyle(RolFilaReporte.Seccion, new ReportStyleOverlay(ColorFondo: "#010203")),
            new ReportRowRoleStyle("Desconocido", new ReportStyleOverlay(ColorFondo: "#0A0B0C")),
        };

        var lista = EstilosFilaPredeterminados.CombinarFsr(usuario);

        var seccion = lista.Single(r => r.RolFila == RolFilaReporte.Seccion);
        Assert.AreEqual("#010203", seccion.Estilo.ColorFondo);
        Assert.AreEqual("#FFFFFF", seccion.Estilo.ColorFuente, "El resto del default se conserva.");

        Assert.AreEqual("#0A0B0C", lista.Single(r => r.RolFila == "Desconocido").Estilo.ColorFondo);
    }
}
