using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 6.2 — DEFAULTS NEUTRALES de estilo por ROL de fila de los PROGRAMAS
/// (Obra e Insumos). Es la ÚNICA fuente de verdad del gris del agrupador/resumen
/// compartido por grid, PDF y Excel.
///
/// Fija:
/// <list type="bullet">
///   <item>el default del rol Resumen (#F0F0F0 + negrita), que centraliza el gris
///   histórico 240,240,240 del grid; PDF y Excel convergen al mismo valor;</item>
///   <item>que la fila de actividad/concepto no impone color;</item>
///   <item>la combinación PARCIAL default + override del usuario;</item>
///   <item>que la lista efectiva incluye TODOS los defaults y conserva los roles
///   persistidos sin default.</item>
/// </list>
/// </summary>
[TestClass]
public class EstilosFilaPredeterminadosProgramasTests
{
    [TestMethod]
    public void Defaults_Resumen_EsGrisYNegrita()
    {
        var estilo = EstilosFilaPredeterminados.Programas(RolFilaReporte.Resumen);

        Assert.AreEqual("#F0F0F0", estilo.ColorFondo, "El gris del agrupador centraliza 240,240,240 = #F0F0F0.");
        Assert.AreEqual((bool?)true, estilo.Negrita);
    }

    [TestMethod]
    public void Defaults_Concepto_NoImponeColor()
    {
        var estilo = EstilosFilaPredeterminados.Programas(RolFilaReporte.Concepto);

        Assert.IsNull(estilo.ColorFondo, "La actividad hereda el color de su columna/tabla.");
        Assert.IsNull(estilo.Negrita);
    }

    [TestMethod]
    public void Defaults_RolDesconocido_EsVacio()
    {
        var estilo = EstilosFilaPredeterminados.Programas("RolInexistente");

        Assert.IsNull(estilo.ColorFondo);
        Assert.IsNull(estilo.ColorFuente);
    }

    [TestMethod]
    public void TiposDeReporteDePrograma_UsanLosTokensEstables()
    {
        Assert.AreEqual("ProgramaObra", EstilosFilaPredeterminados.TipoReporteProgramaObra);
        Assert.AreEqual("ProgramaInsumos", EstilosFilaPredeterminados.TipoReporteProgramaInsumos);
    }

    // ─────────────────────────── Combinación con el usuario ───────────────────────────

    [TestMethod]
    public void Combinar_OverrideParcialDelUsuario_ConservaElRestoDelDefault()
    {
        var usuario = new Dictionary<string, ReportStyleOverlay>
        {
            [RolFilaReporte.Resumen] = new ReportStyleOverlay(ColorFondo: "#010203"),
        };

        var efectivo = EstilosFilaPredeterminados.CombinarProgramas(RolFilaReporte.Resumen, usuario);

        Assert.AreEqual("#010203", efectivo.ColorFondo, "El fondo del usuario gana.");
        Assert.AreEqual((bool?)true, efectivo.Negrita, "La negrita hereda del default.");
    }

    [TestMethod]
    public void Combinar_SinUsuario_EsElDefault()
    {
        var efectivo = EstilosFilaPredeterminados.CombinarProgramas(RolFilaReporte.Resumen, null);

        Assert.AreEqual("#F0F0F0", efectivo.ColorFondo);
    }

    [TestMethod]
    public void ListaEfectiva_IncluyeTodosLosDefaults()
    {
        var lista = EstilosFilaPredeterminados.CombinarProgramas((IEnumerable<ReportRowRoleStyle>?)null);

        CollectionAssert.AreEquivalent(
            EstilosFilaPredeterminados.RolesProgramas.ToArray(),
            lista.Select(r => r.RolFila).ToArray());
    }

    [TestMethod]
    public void ListaEfectiva_OverrideDelUsuarioGanaYConservaRolesSinDefault()
    {
        var usuario = new[]
        {
            new ReportRowRoleStyle(RolFilaReporte.Resumen, new ReportStyleOverlay(ColorFondo: "#010203")),
            new ReportRowRoleStyle("Desconocido", new ReportStyleOverlay(ColorFondo: "#0A0B0C")),
        };

        var lista = EstilosFilaPredeterminados.CombinarProgramas(usuario);

        var resumen = lista.Single(r => r.RolFila == RolFilaReporte.Resumen);
        Assert.AreEqual("#010203", resumen.Estilo.ColorFondo);
        Assert.AreEqual((bool?)true, resumen.Estilo.Negrita, "El resto del default se conserva.");

        var desconocido = lista.Single(r => r.RolFila == "Desconocido");
        Assert.AreEqual("#0A0B0C", desconocido.Estilo.ColorFondo, "Un rol sin default se conserva tal cual.");
    }
}
