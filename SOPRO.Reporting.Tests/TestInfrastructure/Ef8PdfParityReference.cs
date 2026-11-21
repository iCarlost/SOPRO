using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharp.Pdf.IO;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Factories;
using SOPRO.Reporting.Pdf;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

// ---------------------------------------------------------------------------
// Paridad PDF EF8 vs net10 (solo test). El MISMO archivo se compila en dos
// checkouts: el baseline EF8 fijo (2acdea1..., proyecto net8.0-windows) y el
// arbol candidato net10. En el baseline exporta los dos PDF golden; en net10
// los compara semanticamente y valida la cadena de integridad de la referencia.
//
// La referencia NO se versiona: son PDFs generados en el runner y viven solo en
// RUNNER_TEMP. El SHA legacy versionado sigue siendo la unica atadura estable;
// el PDF EF8 se ata a ese SHA antes de comparar (ver AssertParidad).
// ---------------------------------------------------------------------------

/// <summary>
/// Manifiesto escrito por el checkout EF8 junto a los PDFs de referencia.
/// </summary>
internal sealed class Ef8ReferenceManifest
{
    public string Schema { get; set; } = PdfParityConstantes.Schema;
    public string BaselineCommit { get; set; } = "";
    public List<Ef8ReferenceFile> Files { get; set; } = new();
}

internal sealed class Ef8ReferenceFile
{
    public string Escenario { get; set; } = "";
    public string Pdf { get; set; } = "";
    public string PdfNormalizado { get; set; } = "";
    public string GoldenSha256 { get; set; } = "";
    public string RawSha256 { get; set; } = "";
    public string NormalizedSha256 { get; set; } = "";
}

internal static class PdfParityConstantes
{
    public const string Schema = "sopro.pdf-parity.ef8-reference.1";

    public const string BaselineCommit = "2acdea1cb3aafbd4e8ed32025996ca40d74af91c";

    public const string ReferenceDirEnvVar = "SOPRO_EF8_REFERENCE_DIR";

    public const string ExportDirEnvVar = "SOPRO_EF8_EXPORT_DIR";

    public const string BaselineCommitEnvVar = "SOPRO_EF8_BASELINE_COMMIT";

    public const string SinteticoGoldenSha = "CatalogoMatricesPdf.Legacy.sha256";

    public const string ProyectoSinteticoGoldenSha =
        "CatalogoMatricesPdf.ProyectoSintetico.Legacy.sha256";
}

internal static class PdfParityHash
{
    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}

/// <summary>
/// Construye los dos escenarios PDF exactamente igual en ambos checkouts. Es la
/// unica fuente de verdad del documento neutral que se renderiza en EF8 y net10.
/// </summary>
internal static class PdfParityEscenarios
{
    public static byte[] RenderizarSintetico()
    {
        using var fixture = new SinteticoCatalogoFixture();
        var documento = ConstruirDocumento(
            fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "");
        return new CatalogoMatricesPdfRenderer().Render(documento, CultureInfo.InvariantCulture);
    }

    public static byte[] RenderizarProyectoSintetico()
    {
        using var fixture = new ProyectoSinteticoCatalogoFixture();
        fixture.AssertPlantillaSinFechaImpresion();
        var documento = ConstruirDocumento(
            fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "Todos");
        return new CatalogoMatricesPdfRenderer().Render(documento, CultureInfo.InvariantCulture);
    }

    private static MatrixCatalogReportDocument ConstruirDocumento(
        SOPRO.Core.Entities.Proyecto proyecto, string dbPath, IEnumerable<int> ids, string filtroTitulo)
    {
        var sesion = ProjectSessionInfo.Create(
            ProjectRef.FromEntity(proyecto), dbPath, null, proyecto.DecimalesImporte);
        var resultado = new BuildMatrixCatalogReport(new ProjectDbContextFactory())
            .Execute(sesion, new BuildMatrixCatalogReportRequest(ids.ToList(), filtroTitulo, null),
                CancellationToken.None)
            .GetAwaiter().GetResult();
        Assert.IsTrue(resultado.IsSuccess, resultado.Error?.Message ?? "sin mensaje");
        return resultado.Value!;
    }
}

#if NET10_0_OR_GREATER

internal static class Ef8PdfParity
{
    /// <summary>
    /// Valida que el PDF candidato sea semanticamente equivalente al PDF generado
    /// por el baseline EF8 fijo y que ese PDF de referencia este atado al SHA
    /// legacy versionado. Falla (no omite) si falta cualquier pieza.
    /// </summary>
    public static void AssertParidad(string escenario, string goldenShaFile, byte[] pdfCandidato)
    {
        if (GoldenPaths.Regenerating)
            Assert.Fail(
                $"La paridad EF8 no admite {GoldenPaths.RegenerateEnvVar}=1: los goldens legacy son inmutables.");

        string directorio = RequerirVariable(PdfParityConstantes.ReferenceDirEnvVar);
        string commitEsperado = RequerirVariable(PdfParityConstantes.BaselineCommitEnvVar);
        Assert.AreEqual(PdfParityConstantes.BaselineCommit, commitEsperado,
            $"El baseline EF8 debe ser el SHA fijo {PdfParityConstantes.BaselineCommit}; "
            + $"se recibio '{commitEsperado}'.");

        string rutaManifiesto = Path.Combine(directorio, "export.json");
        Assert.IsTrue(File.Exists(rutaManifiesto),
            $"Falta la referencia EF8 '{rutaManifiesto}'. El job de CI debe exportarla desde el "
            + $"checkout fijo {PdfParityConstantes.BaselineCommit} antes de ejecutar la suite.");

        var manifiesto = JsonSerializer.Deserialize<Ef8ReferenceManifest>(
            File.ReadAllText(rutaManifiesto))
            ?? throw new InvalidOperationException("El manifiesto de referencia EF8 no se pudo leer.");
        Assert.AreEqual(PdfParityConstantes.Schema, manifiesto.Schema,
            "El manifiesto de referencia EF8 tiene un schema inesperado.");
        Assert.AreEqual(commitEsperado, manifiesto.BaselineCommit,
            "El manifiesto de referencia no corresponde al commit EF8 esperado.");

        var entrada = manifiesto.Files.SingleOrDefault(f => f.Escenario == escenario);
        Assert.IsNotNull(entrada, $"El manifiesto de referencia no contiene el escenario '{escenario}'.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(entrada!.PdfNormalizado),
            $"El manifiesto de referencia no declara el PDF normalizado del escenario '{escenario}'.");

        string rutaPdf = Path.Combine(directorio, entrada.Pdf);
        string rutaNormalizado = Path.Combine(directorio, entrada.PdfNormalizado);
        Assert.IsTrue(File.Exists(rutaPdf), $"Falta el PDF de referencia EF8 '{rutaPdf}'.");
        Assert.IsTrue(File.Exists(rutaNormalizado),
            $"Falta el PDF normalizado de referencia EF8 '{rutaNormalizado}'.");
        byte[] pdfReferencia = File.ReadAllBytes(rutaPdf);

        // Verificacion INDEPENDIENTE del manifiesto: net10 calcula los hashes directamente
        // de los artefactos reales exportados por el proceso EF8.
        string rawShaReal = PdfParityHash.Sha256Hex(pdfReferencia);
        string normalizadoShaReal = PdfParityHash.Sha256Hex(File.ReadAllBytes(rutaNormalizado));

        string goldenVersionado = File.ReadAllText(
            Path.Combine(GoldenPaths.OutputDir, goldenShaFile)).Trim();
        Assert.AreEqual(entrada.GoldenSha256, goldenShaFile,
            "El manifiesto de referencia no declara el golden esperado.");
        Assert.AreEqual(goldenVersionado, normalizadoShaReal,
            $"El PDF normalizado real de referencia EF8 no coincide con el SHA legacy versionado "
            + $"'{goldenShaFile}' (hash calculado por net10 sobre el artefacto).");

        // El manifiesto solo se usa como registro redundante; si no coincide con los
        // artefactos reales, la referencia esta corrupta y el gate falla.
        Assert.AreEqual(rawShaReal, entrada.RawSha256,
            $"El PDF de referencia EF8 '{entrada.Pdf}' fue alterado: su SHA256 real no coincide con el manifiesto.");
        Assert.AreEqual(normalizadoShaReal, entrada.NormalizedSha256,
            "El PDF normalizado de referencia EF8 no coincide con el hash declarado en el manifiesto.");

        PdfStructuralComparador.AssertEquivalent(pdfReferencia, pdfCandidato, escenario);
    }

    private static string RequerirVariable(string nombre)
    {
        string? valor = Environment.GetEnvironmentVariable(nombre);
        Assert.IsFalse(string.IsNullOrWhiteSpace(valor),
            $"Falta la variable de entorno {nombre}. La paridad EF8 se ejecuta en el job Windows de CI "
            + $"con el baseline {PdfParityConstantes.BaselineCommit}");
        return valor!;
    }
}

[TestClass]
public class Ef8PdfParityGateTests
{
    [TestMethod]
    public void Sintetico_EsSemanticamenteEquivalenteAlPdfDelBaselineEf8()
    {
        byte[] candidato = PdfParityEscenarios.RenderizarSintetico();
        Ef8PdfParity.AssertParidad(
            "sintetico", PdfParityConstantes.SinteticoGoldenSha, candidato);
    }

    [TestMethod]
    public void ProyectoSintetico_EsSemanticamenteEquivalenteAlPdfDelBaselineEf8()
    {
        byte[] candidato = PdfParityEscenarios.RenderizarProyectoSintetico();
        Ef8PdfParity.AssertParidad(
            "proyecto-sintetico", PdfParityConstantes.ProyectoSinteticoGoldenSha, candidato);
    }
}

#else

/// <summary>
/// Harness de exportacion que solo se compila en el checkout baseline EF8. Se
/// ejecuta con SOPRO_EF8_EXPORT_DIR apuntando a RUNNER_TEMP; escribe los dos PDFs
/// y verifica que cada uno normalice EXACTAMENTE al SHA legacy versionado antes
/// de que el job net10 los use como referencia.
/// </summary>
[TestClass]
public class Ef8PdfParityReference
{
    [TestMethod]
    public void ExportarReferenciaEf8()
    {
        if (GoldenPaths.Regenerating)
            Assert.Fail(
                $"La exportacion de referencia no admite {GoldenPaths.RegenerateEnvVar}=1: "
                + "los goldens legacy son inmutables.");

        string directorio = RequerirVariable(PdfParityConstantes.ExportDirEnvVar);
        string baseline = RequerirVariable(PdfParityConstantes.BaselineCommitEnvVar);
        Assert.AreEqual(PdfParityConstantes.BaselineCommit, baseline,
            $"El baseline EF8 debe ser el SHA fijo {PdfParityConstantes.BaselineCommit}; "
            + $"se recibio '{baseline}'.");

        Directory.CreateDirectory(directorio);
        var manifiesto = new Ef8ReferenceManifest { BaselineCommit = baseline };

        manifiesto.Files.Add(Exportar(
            directorio, "sintetico", PdfParityConstantes.SinteticoGoldenSha,
            PdfParityEscenarios.RenderizarSintetico()));
        manifiesto.Files.Add(Exportar(
            directorio, "proyecto-sintetico", PdfParityConstantes.ProyectoSinteticoGoldenSha,
            PdfParityEscenarios.RenderizarProyectoSintetico()));

        File.WriteAllText(
            Path.Combine(directorio, "export.json"),
            JsonSerializer.Serialize(manifiesto, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Ef8ReferenceFile Exportar(
        string directorio, string escenario, string goldenShaFile, byte[] pdf)
    {
        string nombrePdf = escenario + ".pdf";
        string rutaPdf = Path.Combine(directorio, nombrePdf);
        File.WriteAllBytes(rutaPdf, pdf);

        // El proceso EF8 normaliza el PDF EXACTO exportado y publica el artefacto
        // normalizado para que el gate verifique su SHA por si mismo, sin confiar en
        // los valores del manifiesto.
        byte[] normalizado = NormalizarPdfEf8(File.ReadAllBytes(rutaPdf));
        string nombreNormalizado = escenario + ".normalized.pdf";
        string rutaNormalizado = Path.Combine(directorio, nombreNormalizado);
        File.WriteAllBytes(rutaNormalizado, normalizado);

        // Verificacion independiente, releyendo ambos artefactos desde disco.
        string rawSha = PdfParityHash.Sha256Hex(File.ReadAllBytes(rutaPdf));
        string normalizadoSha = PdfParityHash.Sha256Hex(File.ReadAllBytes(rutaNormalizado));
        Assert.AreEqual(PdfParityHash.Sha256Hex(normalizado), normalizadoSha,
            "El PDF normalizado en disco no coincide con los bytes normalizados en memoria.");
        Snapshots.AssertSha256(goldenShaFile, normalizadoSha, escenario);

        return new Ef8ReferenceFile
        {
            Escenario = escenario,
            Pdf = nombrePdf,
            PdfNormalizado = nombreNormalizado,
            GoldenSha256 = goldenShaFile,
            RawSha256 = rawSha,
            NormalizedSha256 = normalizadoSha,
        };
    }

    private static string RequerirVariable(string nombre)
    {
        string? valor = Environment.GetEnvironmentVariable(nombre);
        Assert.IsFalse(string.IsNullOrWhiteSpace(valor),
            $"Falta la variable de entorno {nombre}; el harness EF8 solo se ejecuta desde CI.");
        return valor!;
    }

    /// <summary>
    /// Normaliza el PDF exacto exportado usando las APIs de PdfSharp disponibles en el
    /// baseline EF8 y las constantes del normalizador legacy. Se mantiene local al harness
    /// para no modificar el codigo del checkout de referencia.
    /// </summary>
    private static byte[] NormalizarPdfEf8(byte[] pdf)
    {
        var fechaFija = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        using var origen = new MemoryStream(pdf, writable: false);
        using var documento = PdfReader.Open(origen, PdfDocumentOpenMode.Modify);
        documento.Info.CreationDate = fechaFija;
        documento.Info.ModificationDate = fechaFija;
        documento.Internals.FirstDocumentID = PdfNormalizador.FixedDocumentId;
        documento.Internals.SecondDocumentID = PdfNormalizador.FixedDocumentId;

        using var destino = new MemoryStream();
        documento.Save(destino, false);
        return PdfNormalizador.NeutralizarNoDeterminismoResidual(destino.ToArray());
    }
}

#endif
