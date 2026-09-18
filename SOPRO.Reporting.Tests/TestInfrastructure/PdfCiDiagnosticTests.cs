using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Factories;
using SOPRO.Reporting.Pdf;

namespace SOPRO.Reporting.Tests.TestInfrastructure;

/// <summary>
/// INSTRUMENTACIÓN DIAGNÓSTICA TEMPORAL (plan "Resolver los bloqueos reales de CI", paso 2.1).
///
/// Objetivo: capturar en CI (Windows Server 2025) la huella exacta del PDF bruto/normalizado
/// de los dos escenarios del catálogo de matrices y del entorno/fuentes del SO, para que el
/// paso 2.3 determine la causa raíz del Problema B con evidencia directa.
///
/// Alcance y límites:
///   - NO toca <c>TestData/Goldens</c> ni usa SOPRO_REGENERATE_GOLDENS.
///   - NO debilita ni salta ningún assert de los tests de paridad (esos siguen intactos).
///   - Escribe SOLO bajo un directorio temporal (por defecto <c>%TEMP%/sopro-ci-diag</c>;
///     o <c>SOPRO_CI_DIAG_DIR</c> si está definido, para que CI y el upload coincidan).
///   - Nunca falla el build de tests: cualquier error de captura se registra en el inventario
///     (<c>errors</c>) y se continúa con el siguiente escenario.
///
/// REMOVER cuando 2.3 cierre el diagnóstico: borrar este archivo, el método
/// <c>PdfNormalizador.NormalizarBytes</c> y el step "Upload PDF CI diagnostics" del workflow.
/// </summary>
[TestClass]
public class PdfCiDiagnosticTests
{
    private const string DiagDirEnvVar = "SOPRO_CI_DIAG_DIR";
    private const string Schema = "sopro.pdf-ci-diagnostic.1";

    private static readonly string[] FuentesOs = { "segoeui.ttf", "segoeuib.ttf" };

    private static readonly string[] TablasRequeridas = { "head", "hhea", "hmtx", "cmap", "cvt", "fpgm", "prep" };

    [TestMethod]
    public void CapturaHuellaPdfYEntorno_ParaDiagnosticoCi()
    {
        string diagDir = ResolveDiagDir();
        Directory.CreateDirectory(diagDir);

        var scenarios = new List<Dictionary<string, object?>>();
        var errors = new List<string>();

        foreach (var (nombre, tipo) in new[]
                 {
                     ("sintetico", TipoEscenario.Sintetico),
                     ("proyecto-sintetico", TipoEscenario.ProyectoSintetico),
                 })
        {
            try
            {
                scenarios.Add(CapturarEscenario(nombre, tipo, diagDir));
            }
            catch (Exception ex)
            {
                errors.Add($"{nombre}: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex}");
            }
        }

        var environment = CapturarEntorno();
        var osFonts = CapturarFuentesOs();

        // Subárbol determinista: todo lo que debe ser idéntico entre dos ejecuciones en la misma
        // máquina. Excluye lo volátil por diseño (hash bruto, /ID, XMP y rutas/tiempos).
        var stable = new Dictionary<string, object?>
        {
            ["environment"] = environment,
            ["osFonts"] = osFonts,
            ["scenarios"] = scenarios
                .Select(s => new Dictionary<string, object?>
                {
                    ["name"] = s["name"],
                    ["normalized"] = s["normalized"],
                    ["pages"] = s["pages"],
                    ["fonts"] = s["fonts"],
                })
                .ToList(),
        };
        string stableFingerprint = Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stable, Snapshots.Json)));

        var root = new Dictionary<string, object?>
        {
            ["schema"] = Schema,
            ["generatedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["stableFingerprint"] = stableFingerprint,
            ["diagnosticDirectory"] = diagDir,
            ["environment"] = environment,
            ["osFonts"] = osFonts,
            ["scenarios"] = scenarios,
            ["errors"] = errors,
        };

        File.WriteAllText(
            Path.Combine(diagDir, "inventory.json"),
            JsonSerializer.Serialize(root, Snapshots.Json),
            new UTF8Encoding(false));

        Console.WriteLine($"[PdfCiDiagnostic] dir={diagDir}");
        Console.WriteLine($"[PdfCiDiagnostic] scenarios={scenarios.Count} errors={errors.Count}");
        Console.WriteLine($"[PdfCiDiagnostic] stableFingerprint={stableFingerprint}");
    }

    private enum TipoEscenario
    {
        Sintetico,
        ProyectoSintetico,
    }

    private static Dictionary<string, object?> CapturarEscenario(string nombre, TipoEscenario tipo, string diagDir)
    {
        MatrixCatalogReportDocument doc;
        if (tipo == TipoEscenario.Sintetico)
        {
            using var fixture = new SinteticoCatalogoFixture();
            doc = BuildDocument(fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "");
        }
        else
        {
            using var fixture = new ProyectoSinteticoCatalogoFixture();
            fixture.AssertPlantillaSinFechaImpresion();
            doc = BuildDocument(fixture.Proyecto, fixture.DbPath, fixture.Matrices.Select(m => m.Id), filtroTitulo: "Todos");
        }

        string rawPath = Path.Combine(diagDir, $"{nombre}.raw.pdf");
        string normalizedPath = Path.Combine(diagDir, $"{nombre}.normalized.pdf");

        byte[] raw = new CatalogoMatricesPdfRenderer().Render(doc, CultureInfo.InvariantCulture);
        File.WriteAllBytes(rawPath, raw);

        byte[] normalized = PdfNormalizador.NormalizarBytes(rawPath);
        File.WriteAllBytes(normalizedPath, normalized);

        // Reutiliza el pipeline instrumental real para obtener el mismo TamanoBytes que el manifiesto.
        long tamanoManifest = ParsearTamanoManifest(
            PdfNormalizador.Normalizar(rawPath, nombre, "diagnostico-ci").ManifestJson);

        var internos = LeerInternosPdf(normalized);

        return new Dictionary<string, object?>
        {
            ["name"] = nombre,
            ["raw"] = new Dictionary<string, object?>
            {
                ["fileName"] = Path.GetFileName(rawPath),
                ["sizeBytes"] = raw.LongLength,
                ["sha256"] = Sha256Hex(raw),
            },
            ["normalized"] = new Dictionary<string, object?>
            {
                ["fileName"] = Path.GetFileName(normalizedPath),
                ["sizeBytes"] = normalized.LongLength,
                ["sha256"] = Sha256Hex(normalized),
                ["manifestTamanoBytes"] = tamanoManifest,
            },
            ["internalsSource"] = "normalized",
            ["pages"] = internos.Pages,
            ["fonts"] = internos.Fonts,
            ["xmp"] = internos.Xmp,
            ["trailerId"] = internos.TrailerId,
        };
    }

    private static long ParsearTamanoManifest(string manifestJson)
    {
        using var json = JsonDocument.Parse(manifestJson);
        return json.RootElement.GetProperty("TamanoBytes").GetInt64();
    }

    private sealed record PdfInternos(
        List<object?> Pages,
        List<object?> Fonts,
        Dictionary<string, object?> Xmp,
        Dictionary<string, object?> TrailerId);

    private static PdfInternos LeerInternosPdf(byte[] bytes)
    {
        using var source = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(source, PdfDocumentOpenMode.Modify);

        var pages = new List<object?>();
        for (int i = 0; i < pdf.PageCount; i++)
        {
            var page = pdf.Pages[i];
            var rect = page.MediaBoxReadOnly;

            using var contentMs = new MemoryStream();
            if (page.Contents != null)
            {
                foreach (var content in page.Contents)
                {
                    var stream = ((PdfDictionary)content).Stream;
                    if (stream == null) continue;
                    byte[] data = SafeUnfiltered(stream);
                    contentMs.Write(data, 0, data.Length);
                }
            }

            pages.Add(new Dictionary<string, object?>
            {
                ["index"] = i,
                ["mediaBox"] = new[] { rect.X1, rect.Y1, rect.X2, rect.Y2 },
                ["widthPt"] = page.Width.Point,
                ["heightPt"] = page.Height.Point,
                ["rotate"] = page.Rotate,
                ["contentLength"] = contentMs.Length,
                ["contentSha256"] = Sha256Hex(contentMs.ToArray()),
            });
        }

        var fonts = new List<object?>();
        Dictionary<string, object?>? xmp = null;
        foreach (var obj in pdf.Internals.GetAllObjects())
        {
            if (obj is not PdfDictionary dict) continue;

            if (TryGetName(dict, "/Type", out var type))
            {
                if (string.Equals(type!.TrimStart('/'), "Font", StringComparison.Ordinal))
                {
                    fonts.Add(LeerFuente(dict));
                    continue;
                }

                if (xmp == null
                    && string.Equals(type.TrimStart('/'), "Metadata", StringComparison.Ordinal)
                    && dict.Stream != null)
                {
                    byte[] xmpBytes = SafeUnfiltered(dict.Stream);
                    xmp = new Dictionary<string, object?>
                    {
                        ["length"] = xmpBytes.Length,
                        ["sha256"] = Sha256Hex(xmpBytes),
                    };
                }
            }
        }

        return new PdfInternos(pages, fonts, xmp ?? new Dictionary<string, object?>(), LeerTrailerId(bytes));
    }

    /// <summary>El /ID se lee del texto PDF (PdfSharp no expone el trailer con fiabilidad).</summary>
    private static Dictionary<string, object?> LeerTrailerId(byte[] bytes)
    {
        var match = Regex.Match(
            Encoding.Latin1.GetString(bytes),
            @"/ID\s*\[\s*<(?<First>[0-9A-Fa-f]*)>\s*<(?<Second>[0-9A-Fa-f]*)>\s*\]");
        if (!match.Success) return new Dictionary<string, object?>();
        return new Dictionary<string, object?>
        {
            ["first"] = match.Groups["First"].Value,
            ["second"] = match.Groups["Second"].Value,
        };
    }

    private static Dictionary<string, object?> LeerFuente(PdfDictionary dict)
    {
        var font = new Dictionary<string, object?>
        {
            ["objectNumber"] = dict.Internals.ObjectNumber,
            ["subtype"] = TryGetName(dict, "/Subtype", out var subtype) ? subtype?.TrimStart('/') : null,
            ["baseFont"] = GetBaseFont(dict),
        };

        var widths = AsArray(dict.Elements.GetValue("/Widths"));
        if (widths != null)
        {
            var sb = new StringBuilder();
            foreach (var item in widths.Elements.Items)
            {
                sb.Append(item?.ToString());
                sb.Append(',');
            }
            font["widthsCount"] = widths.Elements.Count;
            font["widthsSha256"] = Sha256Hex(Encoding.ASCII.GetBytes(sb.ToString()));
        }

        var descriptor = AsDictionary(dict.Elements.GetValue("/FontDescriptor"));
        if (descriptor != null)
            font["descriptor"] = LeerDescriptor(descriptor);

        var fontFile = AsDictionary(descriptor?.Elements.GetValue("/FontFile2"));
        if (fontFile?.Stream != null)
        {
            byte[] compressed = fontFile.Stream.Value ?? Array.Empty<byte>();
            byte[] decompressed = SafeUnfiltered(fontFile.Stream);
            font["fontFile2"] = new Dictionary<string, object?>
            {
                ["compressedLength"] = compressed.Length,
                ["decompressedLength"] = decompressed.Length,
                ["sha256"] = Sha256Hex(decompressed),
                ["tables"] = LeerTablasTtf(decompressed, TablasRequeridas),
            };
        }

        return font;
    }

    private static Dictionary<string, object?> LeerDescriptor(PdfDictionary descriptor)
    {
        var e = descriptor.Elements;
        return new Dictionary<string, object?>
        {
            ["ascent"] = GetIntOrNull(e, "/Ascent"),
            ["descent"] = GetIntOrNull(e, "/Descent"),
            ["capHeight"] = GetIntOrNull(e, "/CapHeight"),
            ["xHeight"] = GetIntOrNull(e, "/XHeight"),
            ["flags"] = GetIntOrNull(e, "/Flags"),
            ["fontBBox"] = ArrayToString(e.GetArray("/FontBBox")),
        };
    }

    // ── Entorno y fuentes del SO ────────────────────────────────────────────

    private static Dictionary<string, object?> CapturarEntorno()
    {
        var osVersion = Environment.OSVersion;
        return new Dictionary<string, object?>
        {
            ["osDescription"] = RuntimeInformation.OSDescription,
            ["osVersion"] = osVersion.ToString(),
            ["osVersionNumber"] = osVersion.Version.ToString(),
            ["osPlatform"] = osVersion.Platform.ToString(),
            ["frameworkDescription"] = RuntimeInformation.FrameworkDescription,
            ["clrVersion"] = Environment.Version.ToString(),
            ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["runtimeIdentifier"] = RuntimeInformation.RuntimeIdentifier,
            ["is64BitProcess"] = Environment.Is64BitProcess,
            ["is64BitOperatingSystem"] = Environment.Is64BitOperatingSystem,
        };
    }

    private static Dictionary<string, object?> CapturarFuentesOs()
    {
        string directory = FuentesDir();
        var files = new List<object?>();
        foreach (string name in FuentesOs)
        {
            var entry = new Dictionary<string, object?> { ["requested"] = name };
            string path = Path.Combine(directory, name);
            entry["path"] = path;
            if (!File.Exists(path))
            {
                entry["exists"] = false;
                files.Add(entry);
                continue;
            }

            byte[] bytes = File.ReadAllBytes(path);
            entry["exists"] = true;
            entry["sizeBytes"] = bytes.LongLength;
            entry["sha256"] = Sha256Hex(bytes);
            entry["sfnt"] = ResumenTtf(bytes);
            files.Add(entry);
        }

        return new Dictionary<string, object?> { ["directory"] = directory, ["files"] = files };
    }

    private static string FuentesDir()
    {
        string special = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrEmpty(special) && Directory.Exists(special)) return special;

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return Path.Combine(string.IsNullOrEmpty(windows) ? @"C:\Windows" : windows, "Fonts");
    }

    // ── Lectura de tablas TrueType/OpenType ─────────────────────────────────

    private static Dictionary<string, object?> ResumenTtf(byte[] font)
    {
        var summary = new Dictionary<string, object?>();
        var directory = LeerDirectorioTtf(font, out uint sfntVersion, out ushort numTables, out string? error);
        summary["sfntVersion"] = error == null ? $"0x{sfntVersion:X8}" : null;
        summary["numTables"] = error == null ? numTables : null;
        if (error != null)
        {
            summary["error"] = error;
            return summary;
        }

        summary["tables"] = LeerTablasTtf(font, TablasRequeridas);
        summary["name1Family"] = LeerNombreTtf(font, directory, 1);
        summary["name5Version"] = LeerNombreTtf(font, directory, 5);
        return summary;
    }

    private static Dictionary<string, object?> LeerTablasTtf(byte[] font, string[] wanted)
    {
        var result = new Dictionary<string, object?>();
        var directory = LeerDirectorioTtf(font, out _, out _, out string? error);
        if (error != null)
        {
            result["error"] = error;
            return result;
        }

        foreach (string table in wanted)
        {
            result[table] = directory.TryGetValue(table, out var entry)
                ? TablaTtf(font, entry)
                : null;
        }
        return result;
    }

    private static Dictionary<string, (uint Checksum, uint Offset, uint Length)> LeerDirectorioTtf(
        byte[] font, out uint sfntVersion, out ushort numTables, out string? error)
    {
        var directory = new Dictionary<string, (uint, uint, uint)>(StringComparer.OrdinalIgnoreCase);
        sfntVersion = 0;
        numTables = 0;
        error = null;

        if (font.Length < 12)
        {
            error = "sfnt-too-short";
            return directory;
        }

        var reader = new BigEndianReader(font);
        sfntVersion = reader.ReadUInt32();
        numTables = reader.ReadUInt16();
        reader.ReadUInt16();
        reader.ReadUInt16();
        reader.ReadUInt16();

        for (int i = 0; i < numTables; i++)
        {
            if (reader.Position + 16 > font.Length)
            {
                error = "table-directory-truncated";
                break;
            }
            string tag = reader.ReadTag().Trim();
            uint checksum = reader.ReadUInt32();
            uint offset = reader.ReadUInt32();
            uint length = reader.ReadUInt32();
            directory[tag] = (checksum, offset, length);
        }
        return directory;
    }

    private static object TablaTtf(byte[] font, (uint Checksum, uint Offset, uint Length) entry)
    {
        var info = new Dictionary<string, object?>
        {
            ["checksum"] = $"0x{entry.Checksum:X8}",
            ["offset"] = entry.Offset,
            ["length"] = entry.Length,
        };

        if (entry.Offset > font.Length || entry.Offset + entry.Length > font.Length)
        {
            info["inRange"] = false;
            return info;
        }

        var slice = new byte[entry.Length];
        Array.Copy(font, entry.Offset, slice, 0, entry.Length);
        info["sha256"] = Sha256Hex(slice);
        return info;
    }

    private static string? LeerNombreTtf(
        byte[] font, Dictionary<string, (uint Checksum, uint Offset, uint Length)> directory, ushort wantedNameId)
    {
        if (!directory.TryGetValue("name", out var name)) return null;

        uint offset = name.Offset;
        uint length = name.Length;
        if (offset > font.Length || offset + length > font.Length || length < 6) return null;

        var reader = new BigEndianReader(font) { Position = (int)offset };
        reader.ReadUInt16(); // format
        ushort count = reader.ReadUInt16();
        ushort stringOffset = reader.ReadUInt16();

        string? fallback = null;
        for (int i = 0; i < count; i++)
        {
            if (reader.Position + 12 > offset + length) break;
            ushort platformId = reader.ReadUInt16();
            reader.ReadUInt16(); // encodingId
            ushort languageId = reader.ReadUInt16();
            ushort nameId = reader.ReadUInt16();
            ushort strLength = reader.ReadUInt16();
            ushort strOffset = reader.ReadUInt16();

            if (nameId != wantedNameId) continue;

            long start = (long)offset + stringOffset + strOffset;
            if (start < 0 || start + strLength > font.Length) continue;

            string text = platformId is 0 or 3
                ? Encoding.BigEndianUnicode.GetString(font, (int)start, strLength)
                : Encoding.Latin1.GetString(font, (int)start, strLength);

            if (platformId == 3 && languageId == 0x409) return text;
            fallback ??= text;
        }
        return fallback;
    }

    private sealed class BigEndianReader
    {
        private readonly byte[] _buffer;

        public BigEndianReader(byte[] buffer) => _buffer = buffer;

        public int Position { get; set; }

        public ushort ReadUInt16()
        {
            ushort value = (ushort)((_buffer[Position] << 8) | _buffer[Position + 1]);
            Position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            uint value = (uint)((_buffer[Position] << 24) | (_buffer[Position + 1] << 16)
                                 | (_buffer[Position + 2] << 8) | _buffer[Position + 3]);
            Position += 4;
            return value;
        }

        public string ReadTag()
        {
            string tag = Encoding.ASCII.GetString(_buffer, Position, 4);
            Position += 4;
            return tag;
        }
    }

    // ── Helpers de bajo nivel ───────────────────────────────────────────────

    private static string ResolveDiagDir()
    {
        string? custom = Environment.GetEnvironmentVariable(DiagDirEnvVar);
        if (!string.IsNullOrWhiteSpace(custom)) return custom;
        return Path.Combine(Path.GetTempPath(), "sopro-ci-diag");
    }

    private static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    private static byte[] SafeUnfiltered(PdfDictionary.PdfStream stream)
    {
        try
        {
            byte[]? value = stream.UnfilteredValue;
            if (value != null) return value;
        }
        catch
        {
            // Filtro no soportado: cae al valor crudo.
        }
        return stream.Value ?? Array.Empty<byte>();
    }

    private static PdfDictionary? AsDictionary(PdfItem? item)
    {
        if (item is PdfReference reference) item = reference.Value;
        return item as PdfDictionary;
    }

    private static PdfArray? AsArray(PdfItem? item)
    {
        if (item is PdfReference reference) item = reference.Value;
        return item as PdfArray;
    }

    private static bool TryGetName(PdfDictionary dict, string key, out string? value)
    {
        value = null;
        if (!dict.Elements.ContainsKey(key)) return false;
        try
        {
            value = dict.Elements.GetName(key);
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetBaseFont(PdfDictionary dict)
    {
        PdfItem? item = dict.Elements.GetValue("/BaseFont");
        if (item is PdfName name) return name.Value?.TrimStart('/');
        return item?.ToString()?.TrimStart('/');
    }

    private static int? GetIntOrNull(PdfDictionary.DictionaryElements elements, string key)
    {
        if (!elements.ContainsKey(key)) return null;
        try
        {
            return elements.GetInteger(key);
        }
        catch
        {
            return null;
        }
    }

    private static string? ArrayToString(PdfArray? array)
    {
        if (array == null) return null;
        return string.Join(",", array.Elements.Items.Select(item => item?.ToString() ?? ""));
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
}
