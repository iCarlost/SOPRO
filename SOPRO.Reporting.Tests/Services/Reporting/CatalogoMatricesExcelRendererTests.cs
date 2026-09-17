using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Factories;
using SOPRO.Reporting.Excel;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paridad N7-18c del renderer Excel neutral contra los goldens legacy N7-18a.
///
/// El documento se construye con <see cref="BuildMatrixCatalogReport"/> (mismo
/// pipeline neutral ya testeado en Fase 0.5) y el renderer produce bytes XLSX
/// que se caracterizan con <see cref="XlsxSemanticSnapshot"/> EXACTAMENTE como
/// los golden legacy (mismos nombres de archivo, schema y guardian).
/// </summary>
[TestClass]
public class CatalogoMatricesExcelRendererTests
{
    [TestMethod]
    public void CatalogoMatricesExcel_Sintetico_ParidadConGoldenLegacy()
    {
        using var fixture = new SinteticoCatalogoFixture();
        var tmp = TempXlsx("sintetico");
        try
        {
            var doc = BuildDocument(fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "");
            var bytes = new CatalogoMatricesExcelRenderer().Render(doc);
            File.WriteAllBytes(tmp, bytes);

            var snap = XlsxSemanticSnapshot.Of(
                tmp, "sintetico", "seeds-sintetico",
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
        var t1 = TempXlsx("det1");
        var t2 = TempXlsx("det2");
        try
        {
            var doc = BuildDocument(fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "");
            var renderer = new CatalogoMatricesExcelRenderer();

            var s1 = SnapshotOf(renderer.Render(doc), t1, fixture, "sintetico");
            var s2 = SnapshotOf(renderer.Render(doc), t2, fixture, "sintetico");

            Assert.AreEqual(Snapshots.ToJson(s1), Snapshots.ToJson(s2),
                "Dos renders del mismo documento deben producir el mismo snapshot.");
        }
        finally
        {
            Borrar(t1);
            Borrar(t2);
        }
    }

    [TestMethod]
    public void CatalogoMatricesExcel_ProyectoSintetico_ParidadConGoldenLegacy()
    {
        using var fixture = new ProyectoSinteticoCatalogoFixture();
        fixture.AssertPlantillaSinFechaImpresion();
        var tmp = TempXlsx("proyecto-sintetico");
        try
        {
            var doc = BuildDocument(fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "Todos");
            var bytes = new CatalogoMatricesExcelRenderer().Render(doc);
            File.WriteAllBytes(tmp, bytes);

            var snap = XlsxSemanticSnapshot.Of(
                tmp, "proyecto-sintetico", "proyecto-sintetico-vial-demo",
                fixture.Matrices.OrderBy(m => m.Clave).Select(m => m.Clave),
                "test-directo");

            Snapshots.AssertOrRegenerar("CatalogoMatricesExcel.ProyectoSintetico.Legacy.json",
                Snapshots.ToJson(snap), rutaGolden => SanityProyectoSintetico(snap, rutaGolden));
        }
        finally
        {
            Borrar(tmp);
        }
    }

    private static XlsxSemanticSnapshot SnapshotOf(byte[] bytes, string tmp, object fixture, string escenario)
    {
        File.WriteAllBytes(tmp, bytes);
        return XlsxSemanticSnapshot.Of(
            tmp, escenario,
            escenario == "sintetico" ? "seeds-sintetico" : "proyecto-sintetico-vial-demo",
            IEnumerableClaves(fixture),
            "test-directo");
    }

    private static IEnumerable<string> IEnumerableClaves(object fixture)
    {
        if (fixture is SinteticoCatalogoFixture f1) return f1.Matrices.OrderBy(m => m.Clave).Select(m => m.Clave);
        if (fixture is ProyectoSinteticoCatalogoFixture f2) return f2.Matrices.OrderBy(m => m.Clave).Select(m => m.Clave);
        return Array.Empty<string>();
    }

    private static MatrixCatalogReportDocument BuildDocument(
        SOPRO.Core.Entities.Proyecto proyecto, string dbPath, IEnumerable<int> ids, string filtroTitulo)
    {
        var session = ProjectSessionInfo.Create(
            ProjectRef.FromEntity(proyecto), dbPath, null, proyecto.DecimalesImporte);
        var result = new BuildMatrixCatalogReport(new ProjectDbContextFactory())
            .Execute(session, new BuildMatrixCatalogReportRequest(ids.ToList(), filtroTitulo, null), CancellationToken.None)
            .GetAwaiter().GetResult();
        Assert.IsTrue(result.IsSuccess, result.Error?.Message ?? "sin mensaje");
        return result.Value!;
    }

    private static void SanitySintetico(XlsxSemanticSnapshot snap, string rutaGolden)
    {
        Assert.IsTrue(snap.Celdas.Any(c => c.T == "num" && c.V == "30.015"),
            "El golden sintético debe contener el valor crudo 30.015 (3×10.005).");
        Assert.IsTrue(snap.Celdas.Any(c => c.T == "num" && c.V == "30.01"),
            "El golden sintético debe contener el totalMO crudo 30.01 (10.005+20.005).");
        Assert.IsTrue(snap.Celdas.Any(c => c.T == "num" && c.V == "3.001"),
            "El golden sintético debe contener el importe %MO crudo 3.001 (30.01×0.10).");
        Assert.IsTrue(snap.Celdas.Any(c => c.Fmt == "#,##0.00"),
            "El golden sintético debe registrar el formato #,##0.00.");
        Assert.AreEqual(4, snap.Matrices.Count, "El escenario sintético tiene 4 matrices.");
        Assert.AreEqual("Catálogo", snap.Hojas.Single(), "La hoja del catálogo se llama 'Catálogo'.");
        Assert.IsTrue(snap.Celdas.Any(c => c.V == "30.25"),
            "El BAS-001 debe guardar su total crudo 30.25 (27.5 + 2.75).");
        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }

    private static void SanityProyectoSintetico(XlsxSemanticSnapshot snap, string rutaGolden)
    {
        Assert.IsTrue(snap.Celdas.Any(c => c.T == "num" && c.V == "5504.600535"),
            "La cuadrilla CU001 del proyecto sintético debe guardar el SalarioReal crudo 5504.600535 del MO.");
        Assert.AreEqual(7, snap.Matrices.Count, "El proyecto sintético tiene 7 matrices.");
        Console.WriteLine($"Golden regenerado: {rutaGolden}");
    }

    private static string TempXlsx(string tag) =>
        Path.Combine(Path.GetTempPath(), $"sopro_catmat_excel_{tag}_{Guid.NewGuid():N}.xlsx");

    private static void Borrar(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}