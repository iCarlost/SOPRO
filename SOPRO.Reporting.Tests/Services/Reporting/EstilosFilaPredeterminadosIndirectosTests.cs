using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 5.1 — DEFAULTS NEUTRALES de estilo por ROL de fila del Cálculo de
/// Indirectos (<see cref="EstilosFilaPredeterminados"/>). Es la ÚNICA fuente de
/// verdad del gris de la fila de GRUPO y de los tonos de cierre compartidos por
/// grid, PDF y Excel.
///
/// Fija:
/// <list type="bullet">
///   <item>el default del rol Grupo (#E6E6E6 + negrita), que centraliza el gris
///   histórico 230,230,230 del grid; PDF y Excel convergen al mismo valor;</item>
///   <item>los defaults de cierre Subtotal (#E3F2FD) y Resumen (#BBDEFB);</item>
///   <item>la combinación PARCIAL default + override del usuario (un override de
///   sólo el fondo conserva la negrita del default);</item>
///   <item>que la lista efectiva incluye TODOS los defaults y conserva los roles
///   persistidos sin default.</item>
/// </list>
/// </summary>
[TestClass]
public class EstilosFilaPredeterminadosIndirectosTests
{
    [TestMethod]
    public void Defaults_Grupo_EsGrisGrisYNegrita()
    {
        var estilo = EstilosFilaPredeterminados.Indirectos(RolFilaReporte.Grupo);

        Assert.AreEqual("#E6E6E6", estilo.ColorFondo, "El gris de grupo centraliza 230,230,230 = #E6E6E6.");
        Assert.AreEqual((bool?)true, estilo.Negrita);
    }

    [TestMethod]
    public void Defaults_SubtotalYResumen_TienenFondoAzulado()
    {
        Assert.AreEqual("#E3F2FD", EstilosFilaPredeterminados.Indirectos(RolFilaReporte.Subtotal).ColorFondo);
        Assert.AreEqual("#BBDEFB", EstilosFilaPredeterminados.Indirectos(RolFilaReporte.Resumen).ColorFondo);
    }

    [TestMethod]
    public void Defaults_Concepto_NoImponeColor()
    {
        var estilo = EstilosFilaPredeterminados.Indirectos(RolFilaReporte.Concepto);

        Assert.IsNull(estilo.ColorFondo, "El concepto terminal hereda el color de su columna/tabla.");
        Assert.IsNull(estilo.Negrita);
    }

    [TestMethod]
    public void Defaults_RolDesconocido_EsVacio()
    {
        var estilo = EstilosFilaPredeterminados.Indirectos("RolInexistente");

        Assert.IsNull(estilo.ColorFondo);
        Assert.IsNull(estilo.ColorFuente);
    }

    [TestMethod]
    public void TipoReporteIndirectos_EsElTokenEstable()
    {
        Assert.AreEqual("Indirectos", EstilosFilaPredeterminados.TipoReporteIndirectos);
    }

    // ─────────────────────────── Combinación con el usuario ───────────────────────────

    [TestMethod]
    public void Combinar_OverrideParcialDelUsuario_ConservaElRestoDelDefault()
    {
        var usuario = new Dictionary<string, ReportStyleOverlay>
        {
            [RolFilaReporte.Grupo] = new ReportStyleOverlay(ColorFondo: "#010203"),
        };

        var efectivo = EstilosFilaPredeterminados.CombinarIndirectos(RolFilaReporte.Grupo, usuario);

        Assert.AreEqual("#010203", efectivo.ColorFondo, "El fondo del usuario gana.");
        Assert.AreEqual((bool?)true, efectivo.Negrita, "La negrita hereda del default.");
    }

    [TestMethod]
    public void Combinar_SinUsuario_EsElDefault()
    {
        var efectivo = EstilosFilaPredeterminados.CombinarIndirectos(RolFilaReporte.Grupo, null);

        Assert.AreEqual("#E6E6E6", efectivo.ColorFondo);
    }

    [TestMethod]
    public void ListaEfectiva_IncluyeTodosLosDefaults()
    {
        var lista = EstilosFilaPredeterminados.CombinarIndirectos((IEnumerable<ReportRowRoleStyle>?)null);

        CollectionAssert.AreEquivalent(
            EstilosFilaPredeterminados.RolesIndirectos.ToArray(),
            lista.Select(r => r.RolFila).ToArray());
    }

    [TestMethod]
    public void ListaEfectiva_OverrideDelUsuarioGanaYConservaRolesSinDefault()
    {
        var usuario = new[]
        {
            new ReportRowRoleStyle(RolFilaReporte.Grupo, new ReportStyleOverlay(ColorFondo: "#010203")),
            new ReportRowRoleStyle("Desconocido", new ReportStyleOverlay(ColorFondo: "#0A0B0C")),
        };

        var lista = EstilosFilaPredeterminados.CombinarIndirectos(usuario);

        var grupo = lista.Single(r => r.RolFila == RolFilaReporte.Grupo);
        Assert.AreEqual("#010203", grupo.Estilo.ColorFondo);
        Assert.AreEqual((bool?)true, grupo.Estilo.Negrita, "El resto del default se conserva.");

        var desconocido = lista.Single(r => r.RolFila == "Desconocido");
        Assert.AreEqual("#0A0B0C", desconocido.Estilo.ColorFondo, "Un rol sin default se conserva tal cual.");
    }
}
