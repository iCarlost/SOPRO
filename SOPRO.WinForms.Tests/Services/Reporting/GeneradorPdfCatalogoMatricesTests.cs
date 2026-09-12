using System.Globalization;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Data.Context;
using SOPRO.WinForms.Services;
using SOPRO.Reporting.Tests.TestInfrastructure;

namespace SOPRO.WinForms.Tests.Services.Reporting;

/// <summary>
/// Golden legacy del "Catálogo de matrices" en PDF (N7-18a). El generador vintage formatea
/// números con ToString("#,##0.00"), sensible a CurrentCulture: los tests fijan cultura
/// invariante para que el golden no dependa del idioma/región de la máquina.
///
/// El contrato de determinismo es que dos ejecuciones a segundos de distancia, normalizadas
/// mediante <see cref="PdfNormalizador"/>, son byte a byte idénticas. Que los originales
/// difieran entre sí se registra solo como diagnóstico: si PDFsharp pasara a ser
/// determinista la normalización seguiría siendo válida y el test no debe romperse por eso.
/// </summary>
[TestClass]
public class GeneradorPdfCatalogoMatricesTests
{
    private static GeneradorPdfCatalogoMatrices NuevoGenerador(SOPROContext ctx)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        return new GeneradorPdfCatalogoMatrices(new ReporteService(ctx), ctx);
    }

    [TestMethod]
    public void CatalogoMatricesPdf_Sintetico_NormalizadoEsDeterminista()
    {
        using var fixture = new SinteticoCatalogoFixture();
        var gen = NuevoGenerador(fixture.Context);
        var t1 = TempPdf("det1");
        var t2 = TempPdf("det2");
        var t3 = TempPdf("det3");
        try
        {
            string r1 = gen.Generar(fixture.Proyecto, fixture.Matrices, fixture.Plantilla, "", t1);
            Thread.Sleep(1500);
            string r2 = gen.Generar(fixture.Proyecto, fixture.Matrices, fixture.Plantilla, "", t2);
            string r3 = gen.Generar(fixture.Proyecto, fixture.Matrices, fixture.Plantilla, "", t3);

            // 1) Diagnóstico (no contrato): se espera que los originales difieran por ser
            //    IDs/fechas aleatorios; si PDFsharp fuera determinista, no es una regresión.
            bool originalesDifieren = Sha256(r1) != Sha256(r2);
            Console.WriteLine(
                $"Diagnóstico N7-18a: los PDFs crudos difirieron = {originalesDifieren}.");

            // 2) Normalizados, deben ser byte a byte idénticos.
            var n1 = PdfNormalizador.Normalizar(r1, "sintetico", "seeds-sintetico");
            var n2 = PdfNormalizador.Normalizar(r2, "sintetico", "seeds-sintetico");
            var n3 = PdfNormalizador.Normalizar(r3, "sintetico", "seeds-sintetico");

            Assert.AreEqual(n1.Sha256, n2.Sha256, "PDF sintético normalizado debe ser determinista (ej2).");
            Assert.AreEqual(n1.Sha256, n3.Sha256, "PDF sintético normalizado debe ser determinista (ej3).");
            Assert.AreEqual(n1.ManifestJson, n2.ManifestJson, "El manifiesto del PDF sintético debe ser idéntico.");
            Assert.AreEqual(n1.ManifestJson, n3.ManifestJson, "El manifiesto del PDF sintético debe ser idéntico (ej3).");
        }
        finally
        {
            Borrar(t1); Borrar(t2); Borrar(t3);
        }
    }

    [TestMethod]
    public void CatalogoMatricesPdf_Sintetico_HashLegacy()
    {
        using var fixture = new SinteticoCatalogoFixture();
        var gen = NuevoGenerador(fixture.Context);
        var tmp = TempPdf("sintetico");
        try
        {
            string ruta = gen.Generar(fixture.Proyecto, fixture.Matrices, fixture.Plantilla, "", tmp);
            var normalizado = PdfNormalizador.Normalizar(ruta, "sintetico", "seeds-sintetico");

            Snapshots.AssertSha256("CatalogoMatricesPdf.Legacy.sha256", normalizado.Sha256, "sintético");
            Snapshots.AssertOrRegenerar("CatalogoMatricesPdf.Legacy.manifest.json",
                normalizado.ManifestJson, rutaGolden =>
                {
                    var m = System.Text.Json.JsonSerializer
                        .Deserialize<PdfManifestData>(normalizado.ManifestJson)!;
                    Assert.IsTrue(m.Paginas >= 1, "El libro sintético debe tener al menos una página.");
                    Console.WriteLine($"Golden regenerado: {rutaGolden}");
                });
        }
        finally
        {
            Borrar(tmp);
        }
    }

    [TestMethod]
    public void CatalogoMatricesPdf_***REMOVED***_HashLegacy()
    {
        using var fixture = new RealCatalogoFixture();
        fixture.AssertPlantillaSinFechaImpresion();
        var gen = NuevoGenerador(fixture.Context);
        var tmp = TempPdf("real");
        try
        {
            string ruta = gen.Generar(fixture.Proyecto, fixture.Matrices, fixture.Plantilla, "Todos", tmp);
            var normalizado = PdfNormalizador.Normalizar(
                ruta, "proyecto-real", "***REMOVED***");

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

    private static string TempPdf(string tag) =>
        Path.Combine(Path.GetTempPath(), $"sopro_catmat_pdf_{tag}_{Guid.NewGuid():N}.pdf");

    private static string Sha256(string archivo)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(archivo);
        return Convert.ToHexString(sha.ComputeHash(fs));
    }

    private static void Borrar(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}