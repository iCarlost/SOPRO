using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services.Reporting;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación de <see cref="ClaveFilaReporte"/>: las claves se derivan de los
/// Id/claves de dominio (nunca del índice visual), son deterministas y las
/// sintéticas (subtotal, total, IVA) no dependen del orden de las entidades.
/// </summary>
[TestClass]
public class ClaveFilaReporteTests
{
    // ─────────────────── Tokens por reporte ───────────────────

    [TestMethod]
    public void Concepto_UsaIdDeDominio()
    {
        int id = 7;
        Assert.AreEqual("C:7", ClaveFilaReporte.Concepto(id));
        Assert.AreEqual("C:8", ClaveFilaReporte.Concepto(id + 1));
    }

    [TestMethod]
    public void Subtotal_UsaClaveDeAgrupador()
    {
        Assert.AreEqual("SUB:01.02", ClaveFilaReporte.Subtotal("01.02"));
        Assert.AreEqual("SUB:", ClaveFilaReporte.Subtotal(null));
    }

    [TestMethod]
    public void Agrupador_UsaIdDeDominio_UnicoPorFila()
    {
        // A diferencia de Subtotal (parametrizado por una clave que puede repetirse),
        // la fila de un AGRUPADOR persistido se identifica por su Id: dos agrupadores
        // con la misma Clave (o vacía) producen claves DISTINTAS.
        Assert.AreEqual("SUB:7", ClaveFilaReporte.Agrupador(7));
        Assert.AreEqual("SUB:8", ClaveFilaReporte.Agrupador(8));
        CollectionAssert.AllItemsAreUnique(new[]
        {
            ClaveFilaReporte.Agrupador(7),
            ClaveFilaReporte.Agrupador(8),
            ClaveFilaReporte.Subtotal(string.Empty),
        });
    }

    [TestMethod]
    public void Sinteticas_TotalEIva_SonConstantes()
    {
        string claveTotal = ClaveFilaReporte.Total;
        string claveIva = ClaveFilaReporte.Iva;
        Assert.AreEqual("TOTAL", claveTotal, "La clave de la fila de total debe ser TOTAL.");
        Assert.AreEqual("IVA", claveIva, "La clave de la fila de IVA debe ser IVA.");
    }

    [TestMethod]
    public void Indirectos_UsanIdsDeGrupoYConcepto()
    {
        Assert.AreEqual("G:3", ClaveFilaReporte.GrupoIndirecto(3));
        Assert.AreEqual("IC:9", ClaveFilaReporte.ConceptoIndirecto(9));
    }

    [TestMethod]
    public void Apu_UsaConceptoComponenteYPrecioUnitario()
    {
        Assert.AreEqual("C:2", ClaveFilaReporte.Concepto(2));
        Assert.AreEqual("COMP:4", ClaveFilaReporte.Componente(4));
        Assert.AreEqual("PU:2", ClaveFilaReporte.PrecioUnitario(2));
    }

    // ─────────────────── Determinismo ante reordenamiento ───────────────────

    [TestMethod]
    public void Claves_ReordenarEntidades_MismaClavePorId()
    {
        var ids = new[] { 10, 20, 30 };

        var enOrden = ids.Select(ClaveFilaReporte.Concepto).ToArray();
        var invertido = ids.Reverse().Select(ClaveFilaReporte.Concepto).ToArray();

        CollectionAssert.AreEqual(
            new[] { "C:10", "C:20", "C:30" },
            enOrden);
        CollectionAssert.AreEqual(
            new[] { "C:30", "C:20", "C:10" },
            invertido);

        // La clave de un Id es idéntica sin importar su posición.
        Assert.AreEqual(enOrden[0], invertido[2]);
        Assert.AreEqual(enOrden[1], invertido[1]);
        Assert.AreEqual(enOrden[2], invertido[0]);
    }

    [TestMethod]
    public void ClavesSinteticas_SonDeterministas()
    {
        var agrupadores = new[] { "A", "B", "A" };
        var claves = agrupadores.Select(ClaveFilaReporte.Subtotal).ToArray();

        CollectionAssert.AreEqual(new[] { "SUB:A", "SUB:B", "SUB:A" }, claves);
    }

    // ─────────────────── Roles de fila ───────────────────

    [TestMethod]
    public void RolesFila_ContieneLosRolesPlanificadosYSonDistintos()
    {
        var roles = new[]
        {
            RolFilaReporte.Capitulo,
            RolFilaReporte.Subcapitulo,
            RolFilaReporte.Nivel1,
            RolFilaReporte.Nivel2,
            RolFilaReporte.Nivel3,
            RolFilaReporte.Concepto,
            RolFilaReporte.Subtotal,
            RolFilaReporte.Total,
            RolFilaReporte.Grupo,
            RolFilaReporte.Resumen,
            RolFilaReporte.Encabezado,
            RolFilaReporte.TotalGeneral,
            RolFilaReporte.Referencia,
            RolFilaReporte.Seccion,
        };

        Assert.AreEqual(14, roles.Length);
        Assert.AreEqual(roles.Length, roles.Distinct().Count(), "Los roles deben ser etiquetas distintas.");
        Assert.IsTrue(roles.All(r => !string.IsNullOrWhiteSpace(r)));
        CollectionAssert.Contains(roles, "Subtotal");
        CollectionAssert.Contains(roles, "TotalGeneral");
    }
}
