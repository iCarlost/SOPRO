using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace SOPRO.WinForms.Tests.TestInfrastructure;

/// <summary>
/// Normaliza un PDF de reporte legacy para hacerlo determinista y comparable por hash.
///
/// El generador legacy (PdfSharp/MigraDoc 6.2.4) introduce exactamente tres fuentes de
/// no-determinismo que sobreviven a la fijación de /CreationDate, /ModDate e /ID:
///   1. el tag de subconjunto tipográfico (p.ej. "LWKAJW" en "/FontName LWKAJW+Arial"),
///   2. el stream XMP embebido por PdfSharp, con fechas (<xmp:CreateDate>/<xmp:ModifyDate>),
///   3. los UUIDs del stream XMP (<xmpMM:DocumentID>/<xmpMM:InstanceID>).
/// La normalización fija fecha e IDs (PdfSharp) y luego reemplaza byte a byte (mismas
/// longitudes) los tres elementos citados. NO modifica el archivo recibido ni el contenido
/// de página: solo se neutraliza lo que la fecha/IDs/tags aleatorios harían variar.
/// El manifiesto se captura antes de guardar (PdfSharp no permite leer páginas después de
/// Save) e incluye el tamaño de los bytes normalizados hasheados.
/// </summary>
internal static class PdfNormalizador
{
    public const string FixedDocumentId = "SOPRO-PDF-GOLDEN";

    private static readonly DateTime FechaFija = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private const string FechaXmpFija = "2000-01-01T00:00:00+00:00";
    private const string UuidFijo = "uuid:00000000-0000-0000-0000-000000000000";
    private const string SubsetFijo = "SOPROX+";

    private static readonly Regex TagSubset = new("[A-Z]{6}\\+", RegexOptions.Compiled);
    private static readonly Regex FechaXmp = new(
        "20\\d{2}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:[+-]\\d{2}:\\d{2}|Z)", RegexOptions.Compiled);
    private static readonly Regex UuidsXmp = new(
        "uuid:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.Compiled);

    public static PdfGoldenResult Normalizar(string pdfPath, string escenario, string fuenteDatos)
    {
        using var pdf = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);

        var manifest = new PdfManifestData
        {
            Escenario = escenario,
            FuenteDatos = fuenteDatos,
            Titulo = pdf.Info.Title ?? "",
            Paginas = pdf.PageCount,
            AnchoPt = pdf.Pages[0].Width.Point,
            AltoPt = pdf.Pages[0].Height.Point,
        };

        pdf.Info.CreationDate = FechaFija;
        pdf.Info.ModificationDate = FechaFija;
        pdf.Internals.FirstDocumentID = FixedDocumentId;
        pdf.Internals.SecondDocumentID = FixedDocumentId;

        using var ms = new MemoryStream();
        pdf.Save(ms, false);
        var bytes = NeutralizarNoDeterminismoResidual(ms.ToArray());
        manifest.TamanoBytes = bytes.Length;

        using var sha = SHA256.Create();
        return new PdfGoldenResult(
            Convert.ToHexString(sha.ComputeHash(bytes)),
            Snapshots.ToJson(manifest));
    }

    /// <summary>
    /// Parche concreto de las tres fuentes residuales de no-determinismo (ver resumen de la
    /// clase). Todos los reemplazos conservan la longitud del byte para no romper offsets:
    /// tags de fuente (6 letras + '+'), fechas XMP (25 chars) y UUIDs XMP ("uuid:" + 36).
    /// Opera sobre Latin1 para que los bytes 0-255 hagan round-trip exacto.
    /// </summary>
    internal static byte[] NeutralizarNoDeterminismoResidual(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        text = TagSubset.Replace(text, SubsetFijo);
        text = FechaXmp.Replace(text, FechaXmpFija);
        text = UuidsXmp.Replace(text, UuidFijo);
        return Encoding.Latin1.GetBytes(text);
    }
}

internal sealed class PdfGoldenResult
{
    public PdfGoldenResult(string sha256, string manifestJson)
    {
        Sha256 = sha256;
        ManifestJson = manifestJson;
    }

    public string Sha256 { get; }
    public string ManifestJson { get; }
}

internal sealed class PdfManifestData
{
    public string Schema { get; set; } = Snapshots.SchemaPdfManifest;
    public string Escenario { get; set; } = "";
    public string FuenteDatos { get; set; } = "";
    public string Generador { get; set; } = "GeneradorPdfCatalogoMatrices";
    public string Titulo { get; set; } = "";
    public int Paginas { get; set; }
    public double AnchoPt { get; set; }
    public double AltoPt { get; set; }
    public long TamanoBytes { get; set; }
}