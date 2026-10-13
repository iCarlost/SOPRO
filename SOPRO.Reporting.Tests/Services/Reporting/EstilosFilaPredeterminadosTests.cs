using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 4.3 — DEFAULTS NEUTRALES de estilo por ROL de fila del Presupuesto
/// (<see cref="EstilosFilaPredeterminados"/>). Es la ÚNICA fuente de verdad de los
/// colores de las filas agrupador y de cierre compartidos por grid, PDF y Excel.
///
/// Fija:
/// <list type="bullet">
///   <item>los valores por defecto de cada rol (reproducen el pintado histórico del
///   grid: Capítulo #800080/blanco, Subcapítulo #ADD8E6/negro, Niveles
///   #D3D3D3/negro, Subtotal/Total azulados);</item>
///   <item>la combinación PARCIAL default + override del usuario (un override de
///   sólo el fondo conserva la fuente/negrita del default);</item>
///   <item>que la lista efectiva incluye TODOS los defaults y conserva los roles
///   persistidos sin default.</item>
/// </list>
/// El contrato visual queda versionado en el golden
/// <c>presupuesto-estilos-fila-defaults.json</c>: cualquier cambio del color por
/// defecto rompe el snapshot (cambio visual explícito).
/// </summary>
[TestClass]
public class EstilosFilaPredeterminadosTests
{
    private const string Schema = "sopro.presupuesto.estilos-fila.defaults.1";

    // ─────────────────────────── Defaults por rol ───────────────────────────

    [TestMethod]
    public void Defaults_Capitulo_EsPurpuraBlancoNegrita()
    {
        var estilo = EstilosFilaPredeterminados.Presupuesto(RolFilaReporte.Capitulo);

        Assert.AreEqual("#800080", estilo.ColorFondo);
        Assert.AreEqual("#FFFFFF", estilo.ColorFuente);
        Assert.AreEqual((bool?)true, estilo.Negrita);
    }

    [TestMethod]
    public void Defaults_Subcapitulo_EsAzulClaroNegro()
    {
        var estilo = EstilosFilaPredeterminados.Presupuesto(RolFilaReporte.Subcapitulo);

        Assert.AreEqual("#ADD8E6", estilo.ColorFondo);
        Assert.AreEqual("#000000", estilo.ColorFuente);
        Assert.AreEqual((bool?)true, estilo.Negrita);
    }

    [TestMethod]
    public void Defaults_Niveles_CompartenGrisNegro()
    {
        foreach (var rol in new[] { RolFilaReporte.Nivel1, RolFilaReporte.Nivel2, RolFilaReporte.Nivel3 })
        {
            var estilo = EstilosFilaPredeterminados.Presupuesto(rol);
            Assert.AreEqual("#D3D3D3", estilo.ColorFondo, rol);
            Assert.AreEqual("#000000", estilo.ColorFuente, rol);
            Assert.AreEqual((bool?)true, estilo.Negrita, rol);
        }
    }

    [TestMethod]
    public void Defaults_SubtotalYTotal_TienenFondoAzulado()
    {
        Assert.AreEqual("#E3F2FD", EstilosFilaPredeterminados.Presupuesto(RolFilaReporte.Subtotal).ColorFondo);
        Assert.AreEqual("#BBDEFB", EstilosFilaPredeterminados.Presupuesto(RolFilaReporte.Total).ColorFondo);
    }

    [TestMethod]
    public void Defaults_Concepto_NoImponeColor()
    {
        var estilo = EstilosFilaPredeterminados.Presupuesto(RolFilaReporte.Concepto);

        Assert.IsNull(estilo.ColorFondo, "El concepto terminal hereda el color de su columna/tabla.");
        Assert.IsNull(estilo.Negrita);
    }

    [TestMethod]
    public void Defaults_RolDesconocido_EsVacio()
    {
        var estilo = EstilosFilaPredeterminados.Presupuesto("RolInexistente");

        Assert.IsNull(estilo.ColorFondo);
        Assert.IsNull(estilo.ColorFuente);
        Assert.IsNull(estilo.Negrita);
    }

    // ─────────────────────────── Combinación con el usuario ───────────────────────────

    [TestMethod]
    public void Combinar_OverrideParcialDelUsuario_ConservaElRestoDelDefault()
    {
        var usuario = new System.Collections.Generic.Dictionary<string, ReportStyleOverlay>
        {
            [RolFilaReporte.Capitulo] = new ReportStyleOverlay(ColorFondo: "#010203"),
        };

        var efectivo = EstilosFilaPredeterminados.CombinarPresupuesto(RolFilaReporte.Capitulo, usuario);

        Assert.AreEqual("#010203", efectivo.ColorFondo, "El fondo del usuario gana.");
        Assert.AreEqual("#FFFFFF", efectivo.ColorFuente, "La fuente hereda del default.");
        Assert.AreEqual((bool?)true, efectivo.Negrita, "La negrita hereda del default.");
    }

    [TestMethod]
    public void Combinar_SinUsuario_EsElDefault()
    {
        var efectivo = EstilosFilaPredeterminados.CombinarPresupuesto(RolFilaReporte.Capitulo, null);

        Assert.AreEqual("#800080", efectivo.ColorFondo);
    }

    [TestMethod]
    public void ListaEfectiva_IncluyeTodosLosDefaults()
    {
        var lista = EstilosFilaPredeterminados.CombinarPresupuesto((System.Collections.Generic.IEnumerable<ReportRowRoleStyle>?)null);

        CollectionAssert.AreEquivalent(
            EstilosFilaPredeterminados.RolesPresupuesto.ToArray(),
            lista.Select(r => r.RolFila).ToArray());
    }

    [TestMethod]
    public void ListaEfectiva_OverrideDelUsuarioGanaYConservaRolesSinDefault()
    {
        var usuario = new[]
        {
            new ReportRowRoleStyle(RolFilaReporte.Capitulo, new ReportStyleOverlay(ColorFondo: "#010203")),
            new ReportRowRoleStyle(RolFilaReporte.Grupo, new ReportStyleOverlay(ColorFondo: "#0A0B0C")),
        };

        var lista = EstilosFilaPredeterminados.CombinarPresupuesto(usuario);

        var capitulo = lista.Single(r => r.RolFila == RolFilaReporte.Capitulo);
        Assert.AreEqual("#010203", capitulo.Estilo.ColorFondo);
        Assert.AreEqual("#FFFFFF", capitulo.Estilo.ColorFuente, "El resto del default se conserva.");

        var grupo = lista.Single(r => r.RolFila == RolFilaReporte.Grupo);
        Assert.AreEqual("#0A0B0C", grupo.Estilo.ColorFondo, "Un rol sin default se conserva tal cual.");
    }

    // ─────────────────────────── Golden del contrato visual ───────────────────────────

    [TestMethod]
    public void Golden_DefaultsDeRolDelPresupuesto()
    {
        var roles = EstilosFilaPredeterminados.RolesPresupuesto
            .Select(rol =>
            {
                var estilo = EstilosFilaPredeterminados.Presupuesto(rol);
                return new
                {
                    Rol = rol,
                    ColorFondo = estilo.ColorFondo,
                    ColorFuente = estilo.ColorFuente,
                    Negrita = estilo.Negrita,
                    Cursiva = estilo.Cursiva,
                };
            })
            .ToList();

        var dto = new { Schema, Roles = roles };
        var json = Snapshots.ToJson(dto);

        Snapshots.AssertOrRegenerar("presupuesto-estilos-fila-defaults.json", json, _ =>
        {
            Assert.AreEqual(EstilosFilaPredeterminados.RolesPresupuesto.Count, roles.Count);
            var capitulo = roles.Single(r => r.Rol == RolFilaReporte.Capitulo);
            Assert.AreEqual("#800080", capitulo.ColorFondo);
        });
    }
}
