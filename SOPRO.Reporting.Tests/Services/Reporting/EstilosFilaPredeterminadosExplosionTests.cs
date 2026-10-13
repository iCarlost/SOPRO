using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paso 6.1 — defaults NEUTRALES por rol de la Explosión de Insumos. Fija que la
/// única fuente de verdad de los colores/estilos de las filas de banda de sección,
/// Total, Total General y Referencia reproduce el pintado histórico del grid
/// (banda #4682B4, Total #F0F0F0, Total General #2D2D30, Referencia #F0F0F0 con
/// texto #646464 en cursiva) y que el override PARCIAL del usuario se combina
/// propiedad a propiedad sobre el default.
/// </summary>
[TestClass]
public class EstilosFilaPredeterminadosExplosionTests
{
    [TestMethod]
    public void TipoReporteExplosion_EsExplosion()
        => Assert.AreEqual("Explosion", EstilosFilaPredeterminados.TipoReporteExplosion);

    [TestMethod]
    public void Encabezado_ReproduceLaBandaDeSeccionDelGrid()
    {
        var estilo = EstilosFilaPredeterminados.Explosion(RolFilaReporte.Encabezado);

        Assert.AreEqual("#4682B4", estilo.ColorFondo);
        Assert.AreEqual("#FFFFFF", estilo.ColorFuente);
        Assert.AreEqual((bool?)true, estilo.Negrita);
        Assert.AreEqual((float?)9.5f, estilo.TamanoFuente);
    }

    [TestMethod]
    public void Total_ReproduceElGrisClaroDelGrid()
    {
        var estilo = EstilosFilaPredeterminados.Explosion(RolFilaReporte.Total);

        Assert.AreEqual("#F0F0F0", estilo.ColorFondo);
        Assert.AreEqual((bool?)true, estilo.Negrita);
        Assert.IsNull(estilo.ColorFuente, "El Total hereda el color de texto (negro).");
    }

    [TestMethod]
    public void TotalGeneral_ReproduceElFondoOscuroDelGrid()
    {
        var estilo = EstilosFilaPredeterminados.Explosion(RolFilaReporte.TotalGeneral);

        Assert.AreEqual("#2D2D30", estilo.ColorFondo);
        Assert.AreEqual("#FFFFFF", estilo.ColorFuente);
        Assert.AreEqual((bool?)true, estilo.Negrita);
        Assert.AreEqual((float?)10f, estilo.TamanoFuente);
    }

    [TestMethod]
    public void Referencia_ReproduceElGrisYLaCursivaDelGrid()
    {
        var estilo = EstilosFilaPredeterminados.Explosion(RolFilaReporte.Referencia);

        Assert.AreEqual("#F0F0F0", estilo.ColorFondo);
        Assert.AreEqual("#646464", estilo.ColorFuente);
        Assert.AreEqual((bool?)true, estilo.Cursiva);
        Assert.AreEqual((float?)8.5f, estilo.TamanoFuente);
    }

    [TestMethod]
    public void DetalleYRolesDesconocidos_NoImponenColor()
    {
        var detalle = EstilosFilaPredeterminados.Explosion(RolFilaReporte.Concepto);
        var inexistente = EstilosFilaPredeterminados.Explosion("RolInexistente");

        Assert.IsNull(detalle.ColorFondo);
        Assert.IsNull(detalle.ColorFuente);
        Assert.IsNull(inexistente.ColorFondo);
    }

    [TestMethod]
    public void CombinarExplosion_RolUsuarioParcialHeredaElDefault()
    {
        var usuario = new Dictionary<string, ReportStyleOverlay>(System.StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Encabezado] = new ReportStyleOverlay(ColorFondo: "#112233"),
        };

        var efectivo = EstilosFilaPredeterminados.CombinarExplosion(RolFilaReporte.Encabezado, usuario);

        Assert.AreEqual("#112233", efectivo.ColorFondo, "El color del usuario gana.");
        Assert.AreEqual("#FFFFFF", efectivo.ColorFuente, "Las demás propiedades heredan el default.");
        Assert.AreEqual((bool?)true, efectivo.Negrita);
        Assert.AreEqual((float?)9.5f, efectivo.TamanoFuente);
    }

    [TestMethod]
    public void CombinarExplosion_ListaDeRoles_IncluyeTodosLosDefaults()
    {
        var lista = EstilosFilaPredeterminados.CombinarExplosion((IEnumerable<ReportRowRoleStyle>?)null);

        CollectionAssert.AreEquivalent(
            EstilosFilaPredeterminados.RolesExplosion.ToArray(),
            lista.Select(r => r.RolFila).ToArray());
        Assert.IsTrue(lista.Count >= 4);
    }
}
