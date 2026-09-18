using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// Normaliza un PDF de reporte legacy para hacerlo determinista y comparable por hash.
///
/// El generador legacy (PdfSharp/MigraDoc 6.2.4) introduce exactamente cuatro fuentes de
/// no-determinismo que sobreviven a la fijación de /CreationDate, /ModDate e /ID:
///   1. el tag de subconjunto tipográfico (p.ej. "ZWKAGD" en "/BaseFont/ZWKAGD+Segoe..." y
///      "/FontName/ZWKAGD+Segoe#20UI,Bold"),
///   2. las fechas del stream XMP embebido por PdfSharp (&lt;xmp:CreateDate&gt;/&lt;xmp:ModifyDate&gt;),
///   3. los UUIDs de ese mismo stream (&lt;xmpMM:DocumentID&gt;/&lt;xmpMM:InstanceID&gt;),
///   4. el offset de zona horaria con que PdfSharp firma /CreationDate y /ModDate del diccionario
///      Info, que depende del huso local de la máquina (p.ej. "-07'00'" en UTC-07:00 frente a
///      "+00'00'" en un runner UTC).
///
/// La normalización fija fecha e IDs de Info (PdfSharp) y luego reemplaza byte a byte con
/// longitudes conservadas SOLO en esos campos concretos: los tags de fuente únicamente en las
/// entradas /BaseFont y /FontName, las fechas/UUIDs únicamente dentro de su elemento, dentro
/// del paquete xpacket, y el offset de zona únicamente en las entradas /CreationDate y /ModDate
/// del objeto Info referenciado por el trailer. Todo patrón idéntico fuera de esos campos permanece intacto (lo
/// verifica el test centinela) y toda forma no reconocida dentro de los campos provoca fallo
/// rápido (fail-fast), de modo que un cambio de formato de PdfSharp rompe el test en lugar de
/// escaparse silenciosamente.
///
/// NO modifica el archivo recibido ni el contenido de página. El manifiesto se captura antes
/// de guardar (PdfSharp no permite leer páginas después de Save) e incluye el tamaño de los
/// bytes normalizados hasheados.
/// </summary>
internal static class PdfNormalizador
{
    public const string FixedDocumentId = "SOPRO-PDF-GOLDEN";

    private static readonly DateTime FechaFija = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private const string FechaXmpFija = "2000-01-01T00:00:00+00:00";
    private const string UuidFijo = "uuid:00000000-0000-0000-0000-000000000000";
    private const string SubsetFijo = "SOPROX+";

    /// <summary>
    /// Offset de zona canónico congelado en los goldens (huso de la máquina que los generó,
    /// UTC-07:00). Es un literal inmutable: cualquier otro valor cambiaría el hash golden.
    /// </summary>
    private const string OffsetInfoFijo = "-07'00'";

    /// <summary>Tag de subconjunto únicamente precedido por /BaseFont o /FontName.</summary>
    private static readonly Regex TagSubsetEnCampos = new(
        "(?<Campo>/BaseFont|/FontName)(?<Ws>\\s*)/(?<Tag>[A-Z]{6})\\+", RegexOptions.Compiled);

    private static readonly Regex XmpFechaElemento = new(
        "<(?<El>xmp:CreateDate|xmp:ModifyDate|xmp:MetadataDate|stEvt:when)>"
            + "(?<Val>[^<]*)</\\k<El>>", RegexOptions.Compiled);

    private static readonly Regex XmpUuidElemento = new(
        "<(?<El>xmpMM:DocumentID|xmpMM:InstanceID|stEvt:instanceID)>"
            + "(?<Val>[^<]*)</\\k<El>>", RegexOptions.Compiled);

    /// <summary>Única forma de fecha que emite PdfSharp (offset, nunca sufijo Z).</summary>
    private static readonly Regex FechaIsoConOffset = new(
        "^20\\d{2}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}[+-]\\d{2}:\\d{2}$", RegexOptions.Compiled);

    private static readonly Regex FechaIsoForma = new(
        "20\\d{2}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(?:Z|[+-]\\d{2}:\\d{2})", RegexOptions.Compiled);

    private static readonly Regex UuidForma = new(
        "^uuid:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.Compiled);

    private static readonly Regex UuidEnTexto = new(
        "uuid:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.Compiled);

    private static readonly Regex XmpPaquete = new(
        "<\\?xpacket begin=.*?<\\?xpacket end=\"[^\"]*\"\\?>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Referencia /Info del trailer, que identifica el objeto del diccionario Info.</summary>
    private static readonly Regex InfoRefEnTrailer = new(
        "/Info\\s+(?<Objeto>\\d+)\\s+(?<Generacion>\\d+)\\s+R", RegexOptions.Compiled);

    /// <summary>
    /// Entrada de fecha bien formada del diccionario Info: clave (CreationDate|ModDate), string
    /// PDF D:yyyyMMddHHmmss y offset con apóstrofos (±HH'mm'). La forma exacta de PdfSharp.
    /// </summary>
    private static readonly Regex InfoFechaEntrada = new(
        "/(?<Campo>CreationDate|ModDate)\\(D:(?<Fecha>\\d{14})(?<Offset>[+-]\\d{2}'\\d{2}')\\)",
        RegexOptions.Compiled);

    /// <summary>Claves de fecha declaradas en el Info, sea cual sea la forma de su valor.</summary>
    private static readonly Regex InfoClaveFecha = new(
        "/(?<Campo>CreationDate|ModDate)(?=[(<])", RegexOptions.Compiled);

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
    /// Parche concreto y acotado de las cuatro fuentes residuales de no-determinismo (ver resumen
    /// de la clase). Todos los reemplazos conservan la longitud original byte a byte:
    ///   - tags de fuente: solo en entradas /BaseFont y /FontName ("ZWKAGD+" -> "SOPROX+");
    ///   - offset de zona del Info: solo en /CreationDate y /ModDate del objeto Info;
    ///   - fechas XMP: solo dentro de xmp:CreateDate/xmp:ModifyDate (y variantes DateTime);
    ///   - UUIDs XMP: solo dentro de xmpMM:DocumentID/xmpMM:InstanceID.
    /// Opera sobre Latin1 para que los bytes 0-255 hagan round-trip exacto.
    /// </summary>
    internal static byte[] NeutralizarNoDeterminismoResidual(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        text = ParchearTagsFuente(text);
        text = ParchearInfoPdf(text);
        text = ParchearMetadataXmp(text);
        return Encoding.Latin1.GetBytes(text);
    }

    private static string ParchearTagsFuente(string text) =>
        TagSubsetEnCampos.Replace(text, m =>
        {
            VerificarLongitudReemplazo(m.Groups["Tag"].Value.Length + 1, SubsetFijo.Length, "tag de fuente");
            return m.Groups["Campo"].Value + m.Groups["Ws"].Value + "/" + SubsetFijo;
        });

    /// <summary>
    /// Canonicaliza el offset de zona de /CreationDate y /ModDate del objeto Info referenciado
    /// por el trailer. La ausencia del diccionario Info (fragmento sin /Info ni claves de fecha)
    /// no es un error y deja el texto intacto, igual que el parcheo XMP cuando no hay xpacket.
    /// En cambio, si el PDF declara el Info y alguna de sus entradas no tiene la forma exacta de
    /// PdfSharp (o no conserva la longitud), se falla rápido en lugar de silenciar el cambio.
    /// </summary>
    private static string ParchearInfoPdf(string text)
    {
        var refInfo = InfoRefEnTrailer.Match(text);
        bool tieneClaves = InfoClaveFecha.IsMatch(text);
        if (!refInfo.Success && !tieneClaves) return text;

        if (!refInfo.Success)
            throw new InvalidDataException(
                "El PDF declara /CreationDate o /ModDate pero el trailer no referencia /Info.");

        var objeto = Regex.Match(
            text,
            $"(?<!\\d){refInfo.Groups["Objeto"].Value}\\s+{refInfo.Groups["Generacion"].Value}"
                + "\\s+obj\\b(?<Cuerpo>.*?)\\bendobj\\b",
            RegexOptions.Singleline);
        if (!objeto.Success)
            throw new InvalidDataException(
                $"No se localizó el objeto del diccionario Info (/Info "
                + $"{refInfo.Groups["Objeto"].Value} {refInfo.Groups["Generacion"].Value} R) en el PDF.");

        var cuerpo = objeto.Groups["Cuerpo"];
        string cuerpoParcheado = ParchearCuerpoInfo(cuerpo.Value);
        return text.Substring(0, cuerpo.Index) + cuerpoParcheado
            + text.Substring(cuerpo.Index + cuerpo.Length);
    }

    private static string ParchearCuerpoInfo(string cuerpo)
    {
        int clavesCreation = 0;
        int clavesMod = 0;
        foreach (Match clave in InfoClaveFecha.Matches(cuerpo))
        {
            if (clave.Groups["Campo"].Value == "CreationDate") clavesCreation++;
            else clavesMod++;
        }

        if (clavesCreation != 1 || clavesMod != 1)
            throw new InvalidDataException(
                "El diccionario Info debe declarar /CreationDate y /ModDate exactamente una vez "
                + $"(encontrados {clavesCreation} y {clavesMod}).");

        int parcheadas = 0;
        string resultado = InfoFechaEntrada.Replace(cuerpo, m =>
        {
            parcheadas++;
            VerificarLongitudReemplazo(
                m.Groups["Offset"].Value.Length, OffsetInfoFijo.Length, "offset de zona del Info");
            return "/" + m.Groups["Campo"].Value + "(D:" + m.Groups["Fecha"].Value + OffsetInfoFijo + ")";
        });

        if (parcheadas != 2)
            throw new InvalidDataException(
                "Entrada de fecha en forma desconocida dentro del diccionario Info "
                + $"(normalizadas {parcheadas} de 2; la forma esperada es D:yyyyMMddHHmmss[+-]HH'mm').");

        return resultado;
    }

    private static string ParchearMetadataXmp(string text)
    {
        var paquetes = XmpPaquete.Matches(text);
        if (paquetes.Count == 0) return text;

        var sb = new StringBuilder(text.Length);
        int pos = 0;
        foreach (Match p in paquetes)
        {
            sb.Append(text, pos, p.Index - pos);
            sb.Append(ParchearPaqueteXmp(p.Value));
            pos = p.Index + p.Length;
        }
        sb.Append(text, pos, text.Length - pos);
        return sb.ToString();
    }

    private static string ParchearPaqueteXmp(string paquete)
    {
        paquete = XmpFechaElemento.Replace(paquete, m =>
            ParchearValorDeElemento(m, FechaXmpFija, FechaIsoConOffset, "fecha XMP"));
        paquete = XmpUuidElemento.Replace(paquete, m =>
            ParchearValorDeElemento(m, UuidFijo, UuidForma, "UUID XMP"));

        // Verificación final de cantidad y forma: todo patrón fecha/UUID restante en el paquete
        // debe coincidir con el literal fijo. Un valor distinto (fecha fuera de los elementos
        // esperados, sufijo Z, UUID en otro elemento) es una forma desconocida -> fallo rápido.
        foreach (Match fecha in FechaIsoForma.Matches(paquete))
            if (fecha.Value != FechaXmpFija)
                throw new InvalidDataException(
                    $"Fecha ISO no normalizada en el paquete XMP: '{fecha.Value}' " +
                    "(solo se admiten xmp:CreateDate/xmp:ModifyDate).");

        foreach (Match uuid in UuidEnTexto.Matches(paquete))
            if (uuid.Value != UuidFijo)
                throw new InvalidDataException(
                    $"UUID no normalizado en el paquete XMP: '{uuid.Value}' " +
                    "(solo se admiten xmpMM:DocumentID/xmpMM:InstanceID).");
        return paquete;
    }

    private static string ParchearValorDeElemento(Match m, string fijo, Regex forma, string descripcion)
    {
        var valor = m.Groups["Val"].Value;
        if (valor.Length == 0) return m.Value;
        if (!forma.IsMatch(valor))
            throw new InvalidDataException(
                $"Forma desconocida de {descripcion} en <{m.Groups["El"].Value}>: '{valor}'.");
        VerificarLongitudReemplazo(valor.Length, fijo.Length, descripcion);
        return $"<{m.Groups["El"].Value}>{fijo}</{m.Groups["El"].Value}>";
    }

    private static void VerificarLongitudReemplazo(int entrada, int salida, string descripcion)
    {
        if (entrada != salida)
            throw new InvalidDataException(
                $"El reemplazo de {descripcion} no conserva la longitud (bytes {entrada} -> {salida}).");
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