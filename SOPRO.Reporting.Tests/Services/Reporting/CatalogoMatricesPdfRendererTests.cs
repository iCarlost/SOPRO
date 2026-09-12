using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Factories;
using SOPRO.Reporting.Pdf;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.Reporting.Tests.Services.Reporting;

/// <summary>
/// Paridad N7-18c del renderer PDF neutral contra los goldens legacy N7-18a.
///
/// Igual que el generador vintage, el renderer formatea números con
/// ToString("#,##0.00") sensible a CurrentCulture: los tests fijan cultura
/// invariante antes de renderizar (el golden se generó así).
///
/// El contrato de paridad es la comparación de bytes NORMALIZADOS (mismo
/// <see cref="PdfNormalizador"/> que los tests legacy): sha256 del PDF
/// normalizado + manifiesto, contra los goldens versionados.
/// </summary>
[TestClass]
public class CatalogoMatricesPdfRendererTests
{
    [TestMethod]
    public void CatalogoMatricesPdf_Sintetico_NormalizadoEsDeterminista()
    {
        SetInvariantCulture();
        using var fixture = new SinteticoCatalogoFixture();
        var renderer = new CatalogoMatricesPdfRenderer();
        var doc = BuildDocument(fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "");
        var t1 = TempPdf("det1");
        var t2 = TempPdf("det2");
        try
        {
            // La cultura se entrega de forma EXPLÍCITA a la API (dictamen Oracle N7-18c);
            // el resultado es idéntico sin importar la cultura ambiente del hilo.
            byte[] p1 = renderer.Render(doc, CultureInfo.InvariantCulture);
            Thread.Sleep(1500);
            byte[] p2 = renderer.Render(doc, CultureInfo.InvariantCulture);
            File.WriteAllBytes(t1, p1);
            File.WriteAllBytes(t2, p2);

            var n1 = PdfNormalizador.Normalizar(t1, "sintetico", "seeds-sintetico");
            var n2 = PdfNormalizador.Normalizar(t2, "sintetico", "seeds-sintetico");

            Assert.AreEqual(n1.Sha256, n2.Sha256, "PDF sintético normalizado debe ser determinista.");
            Assert.AreEqual(n1.ManifestJson, n2.ManifestJson, "El manifiesto del PDF sintético debe ser idéntico.");
        }
        finally
        {
            Borrar(t1); Borrar(t2);
        }
    }

    [TestMethod]
    public void CatalogoMatricesPdf_Sintetico_HashParidadLegacy()
    {
        SetInvariantCulture();
        using var fixture = new SinteticoCatalogoFixture();
        var tmp = TempPdf("sintetico");
        try
        {
            var doc = BuildDocument(fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "");
            byte[] bytes = new CatalogoMatricesPdfRenderer().Render(doc, CultureInfo.InvariantCulture);
            File.WriteAllBytes(tmp, bytes);

            var normalizado = PdfNormalizador.Normalizar(tmp, "sintetico", "seeds-sintetico");

            Snapshots.AssertSha256("CatalogoMatricesPdf.Legacy.sha256", normalizado.Sha256, "sintético");
            Snapshots.AssertOrRegenerar("CatalogoMatricesPdf.Legacy.manifest.json",
                normalizado.ManifestJson, rutaGolden =>
                {
                    var m = System.Text.Json.JsonSerializer
                        .Deserialize<PdfManifestData>(normalizado.ManifestJson)!;
                    Assert.IsTrue(m.Paginas >= 1, "El libro sintético debe tener al menos una página.");
                    Assert.AreEqual("CATÁLOGO DE MATRICES", m.Titulo);
                    Console.WriteLine($"Golden regenerado: {rutaGolden}");
                });
        }
        finally
        {
            Borrar(tmp);
        }
    }

    [TestMethod]
    public void CatalogoMatricesPdf_***REMOVED***_HashParidadLegacy_CubrePlantillaLibre()
    {
        SetInvariantCulture();
        using var fixture = new RealCatalogoFixture();
        fixture.AssertPlantillaSinFechaImpresion();

        // Ruta central de 78f801c: el escenario real usa la plantilla libre (elementos),
        // imagen QR, las cuatro alturas de franja y campos de página. Se verifica la
        // PROYECCIÓN al documento neutral ANTES de comparar el hash, para que esta paridad
        // ejerza explícitamente el camino libre (dictamen Oracle N7-18c).
        var plantilla = fixture.Context.PlantillasReporte.AsNoTracking().First();
        var elementosObj = fixture.Context.PlantillasReporteElementos.AsNoTracking()
            .Where(e => e.PlantillaReporteId == plantilla.Id).ToList();
        var elementosHeader = elementosObj.Where(e => e.Zona == "Encabezado")
            .OrderBy(e => e.ZOrder).ThenBy(e => e.Id).ToList();
        var elementosFooter = elementosObj.Where(e => e.Zona == "PieDePagina")
            .OrderBy(e => e.ZOrder).ThenBy(e => e.Id).ToList();

        Assert.IsTrue(elementosHeader.Count > 0,
            "El escenario real debe ejercer el camino de elementos libres del encabezado.");
        Assert.IsTrue(elementosFooter.Count > 0,
            "El escenario real debe ejercer el camino de elementos libres del pie.");
        Assert.IsTrue(elementosObj.Any(e => e.Tipo == "Imagen" && e.ImagenBytes is { Length: > 0 }),
            "El encabezado libre real lleva la imagen QR embebida (bytes).");

        var doc = BuildDocument(fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "Todos");

        Assert.AreEqual(elementosHeader.Count, doc.HeaderElements.Count,
            "Se proyectan todos los elementos libres de encabezado.");
        Assert.AreEqual(elementosFooter.Count, doc.FooterElements.Count,
            "Se proyectan todos los elementos libres de pie.");
        CollectionAssert.AreEqual(
            elementosHeader.Select(e => e.X).ToList(),
            doc.HeaderElements.Select(e => e.X).ToList(),
            "El orden (ZOrder, Id) de los elementos del encabezado es definitivo.");
        CollectionAssert.AreEqual(
            elementosFooter.Select(e => e.X).ToList(),
            doc.FooterElements.Select(e => e.X).ToList(),
            "El orden (ZOrder, Id) de los elementos del pie es definitivo.");
        Assert.IsTrue(doc.HeaderElements.Any(e => e.Kind == MatrixCatalogPageElementKind.Imagen && e.ImageBytes.Count > 0),
            "La imagen QR viaja al documento neutral como bytes inmutables.");
        Assert.IsTrue(doc.FooterElements.Any(e => e.Content.Contains("{pagina}", StringComparison.OrdinalIgnoreCase)),
            "El campo de página del pie libre queda sin resolver para el medio.");

        Assert.AreEqual(plantilla.EncabezadoAltura, doc.PageHeights.HeaderHeight, "EncabezadoAltura se proyecta.");
        Assert.AreEqual(plantilla.AlturaEncabezadoDmm, doc.PageHeights.HeaderHeightDmm, "AlturaEncabezadoDmm se proyecta.");
        Assert.AreEqual(plantilla.PiePaginaAltura, doc.PageHeights.FooterHeight, "PiePaginaAltura se proyecta.");
        Assert.AreEqual(plantilla.AlturaPieDmm, doc.PageHeights.FooterHeightDmm, "AlturaPieDmm se proyecta.");

        var tmp = TempPdf("real");
        try
        {
            byte[] bytes = new CatalogoMatricesPdfRenderer().Render(doc, CultureInfo.InvariantCulture);
            File.WriteAllBytes(tmp, bytes);

            var normalizado = PdfNormalizador.Normalizar(
                tmp, "proyecto-real", "***REMOVED***");

            Snapshots.AssertSha256("CatalogoMatricesPdf.Real.Legacy.sha256", normalizado.Sha256, "proyecto real");
            Snapshots.AssertOrRegenerar("CatalogoMatricesPdf.Real.Legacy.manifest.json",
                normalizado.ManifestJson, rutaGolden =>
                {
                    var m = System.Text.Json.JsonSerializer
                        .Deserialize<PdfManifestData>(normalizado.ManifestJson)!;
                    Assert.IsTrue(m.Paginas >= 1, "El reporte real debe tener al menos una página.");
                    Console.WriteLine($"Golden regenerado: {rutaGolden}");
                });
        }
        finally
        {
            Borrar(tmp);
        }
    }

    private static void SetInvariantCulture()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
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

    private static string TempPdf(string tag) =>
        Path.Combine(Path.GetTempPath(), $"sopro_catmat_pdf_{tag}_{Guid.NewGuid():N}.pdf");

    private static void Borrar(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}