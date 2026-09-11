using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Data.Context;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Tests.TestInfrastructure;

namespace SOPRO.WinForms.Tests.Services.Reporting;

/// <summary>
/// Golden legacy del "Catálogo de matrices" en Excel (N7-18a). Caracteriza la salida REAL
/// del generador vintage sobre escenarios controlados:
///   - escenario sintético discriminante: precios con más de 2 decimales para fijar la
///     aritmética cruda de N0-TABLA filas 22-23 (30.015 / 30.01 / 3.001 almacenados);
///   - proyecto real ***REMOVED*** (solo lectura, copia temp).
/// El snapshot es semántico (valores+estilos+estructura), NO byte a byte del xlsx.
/// </summary>
[TestClass]
public class GeneradorExcelCatalogoMatricesTests
{
    private static GeneradorExcelCatalogoMatrices NuevoGenerador(SOPROContext ctx) =>
        new(new ReporteService(ctx), ctx);

    [TestMethod]
    public void CatalogoMatricesExcel_Sintetico_GeneraLegacySemantico()
    {
        using var fixture = new SinteticoCatalogoFixture();
        var tmp = TempXlsx("sintetico");
        try
        {
            string ruta = NuevoGenerador(fixture.Context).Generar(
                fixture.Proyecto, fixture.Matrices, fixture.Plantilla,
                filtroTitulo: "", rutaDestino: tmp);

            var snap = XlsxSemanticSnapshot.Of(
                ruta, "sintetico", "seeds-sintetico",
                fixture.Matrices.OrderBy(m => m.Clave).Select(m => m.Clave),
                "test-directo");

            Snapshots.AssertOrRegenerar("CatalogoMatricesExcel.Legacy.json",
                Snapshots.ToJson(snap), rutaGolden => SanitySintetico(snap, rutaGolden));
        }
        finally
        {
            Borrar(tmp);
        }
    }

    [TestMethod]
    public void CatalogoMatricesExcel_Sintetico_DeterminismoEntreEjecuciones()
    {
        using var fixture = new SinteticoCatalogoFixture();
        var gen = NuevoGenerador(fixture.Context);
        var t1 = TempXlsx("det1");
        var t2 = TempXlsx("det2");
        try
        {
            string r1 = gen.Generar(fixture.Proyecto, fixture.Matrices, fixture.Plantilla, "", t1);
            string r2 = gen.Generar(fixture.Proyecto, fixture.Matrices, fixture.Plantilla, "", t2);

            var s1 = XlsxSemanticSnapshot.Of(r1, "sintetico", "seeds-sintetico",
                fixture.Matrices.OrderBy(m => m.Clave).Select(m => m.Clave), "test-directo");
            var s2 = XlsxSemanticSnapshot.Of(r2, "sintetico", "seeds-sintetico",
                fixture.Matrices.OrderBy(m => m.Clave).Select(m => m.Clave), "test-directo");

            Assert.AreEqual(Snapshots.ToJson(s1), Snapshots.ToJson(s2),
                "Dos ejecuciones del generador legacy sobre el mismo escenario deben producir el mismo snapshot.");
        }
        finally
        {
            Borrar(t1);
            Borrar(t2);
        }
    }

    [TestMethod]
    public void CatalogoMatricesExcel_***REMOVED***_GeneraLegacySemantico()
    {
        using var fixture = new RealCatalogoFixture();
        fixture.AssertPlantillaSinFechaImpresion();
        var tmp = TempXlsx("real");
        try
        {
            string ruta = NuevoGenerador(fixture.Context).Generar(
                fixture.Proyecto, fixture.Matrices, fixture.Plantilla,
                filtroTitulo: "Todos", rutaDestino: tmp);

            var snap = XlsxSemanticSnapshot.Of(
                ruta, "proyecto-real", "***REMOVED***",
                fixture.Matrices.OrderBy(m => m.Clave).Select(m => m.Clave),
                "test-directo");

            Snapshots.AssertOrRegenerar("CatalogoMatricesExcel.Real.Legacy.json",
                Snapshots.ToJson(snap), rutaGolden => SanityReal(snap, rutaGolden));
        }
        finally
        {
            Borrar(tmp);
        }
    }

    private static void SanitySintetico(XlsxSemanticSnapshot snap, string rutaGolden)
    {
        // Fila 23 N0-TABLA: 3 componentes de 10.005 → importe crudo 30.015 almacenado.
        Assert.IsTrue(snap.Celdas.Any(c => c.T == "num" && c.V == "30.015"),
            "El golden sintético debe contener el valor crudo 30.015 (3×10.005).");
        // Fila 22 N0-TABLA: totalMO crudo 30.01 y su %MO → 3.001.
        Assert.IsTrue(snap.Celdas.Any(c => c.T == "num" && c.V == "30.01"),
            "El golden sintético debe contener el totalMO crudo 30.01 (10.005+20.005).");
        Assert.IsTrue(snap.Celdas.Any(c => c.T == "num" && c.V == "3.001"),
            "El golden sintético debe contener el importe %MO crudo 3.001 (30.01×0.10).");
        Assert.IsTrue(snap.Celdas.Any(c => c.Fmt == "#,##0.00"),
            "El golden sintético debe registrar el formato #,##0.00 (costo unitario/total).");
        Assert.AreEqual(4, snap.Matrices.Count, "El escenario sintético tiene 4 matrices.");
        Assert.AreEqual("Catálogo", snap.Hojas.Single(), "La hoja del catálogo se llama 'Catálogo'.");
        Assert.IsTrue(snap.Celdas.Any(c => c.V == "30.25"),
            "El BAS-001 debe guardar su total crudo 30.25 (27.5 + 2.75).");
        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }

    private static void SanityReal(XlsxSemanticSnapshot snap, string rutaGolden)
    {
        Assert.IsTrue(snap.Celdas.Any(c => c.T == "num" && c.V == "***REMOVED***"),
            "La cuadrilla real CU001 debe guardar el SalarioReal crudo ***REMOVED*** del MO.");
        Assert.AreEqual(7, snap.Matrices.Count, "El proyecto real tiene 7 matrices.");
        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }

    private static string TempXlsx(string tag) =>
        Path.Combine(Path.GetTempPath(), $"sopro_catmat_excel_{tag}_{Guid.NewGuid():N}.xlsx");

    private static void Borrar(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}