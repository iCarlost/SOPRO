using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SOPRO.WinForms.Tests.TestInfrastructure;

/// <summary>
/// Constantes, opciones de serialización y helpers para comparar/regenerar goldens.
/// En modo normal se comparan JSON exactos para que cualquier cambio en la salida legacy
/// rompa el golden. Con SOPRO_REGENERATE_GOLDENS=1 se (re)escribe el artefacto en la fuente
/// para dejarlo versionado.
/// </summary>
internal static class Snapshots
{
    public const string SchemaExcel = "sopro.catalogo-matrices.excel.legacy.1";
    public const string SchemaPdfManifest = "sopro.catalogo-matrices.pdf.manifest.1";

    public static JsonSerializerOptions Json { get; } = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson(object value) =>
        JsonSerializer.Serialize(value, value.GetType(), Json);

    public static void AssertOrRegenerar(string goldenFileName, string actualJson, Action<string> sanity)
    {
        string ruta = GoldenPaths.Resolve(goldenFileName);

        if (GoldenPaths.Regenerating)
        {
            sanity(ruta);
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            File.WriteAllText(ruta, actualJson);
            return;
        }

        if (!File.Exists(ruta))
            Assert.Fail($"Falta el golden '{goldenFileName}' (ejecutar una vez con {GoldenPaths.RegenerateEnvVar}=1).");

        var esperado = JsonNode.Parse(File.ReadAllText(ruta))!;
        var actual = JsonNode.Parse(actualJson)!;
        if (!esperado.IsDeepEqual(actual))
        {
            string actualPath = Path.Combine(GoldenPaths.OutputDir, goldenFileName.Replace(".json", ".actual.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
            File.WriteAllText(actualPath, actualJson);
            Assert.Fail($"El snapshot legacy no coincide con el golden '{goldenFileName}'." +
                        $"{Environment.NewLine}Actual regenerado en: {actualPath}{Environment.NewLine}{PrimeraDiferencia(esperado, actual, goldenFileName)}");
        }
    }

    public static void AssertSha256(string goldenFileName, string sha256Hex, string contexto)
    {
        string ruta = GoldenPaths.Resolve(goldenFileName);
        if (GoldenPaths.Regenerating)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            File.WriteAllText(ruta, sha256Hex);
            return;
        }
        if (!File.Exists(ruta))
            Assert.Fail($"Falta el golden '{goldenFileName}' (ejecutar una vez con {GoldenPaths.RegenerateEnvVar}=1).");
        var esperado = File.ReadAllText(ruta).Trim();
        Assert.AreEqual(esperado, sha256Hex,
            $"El hash del PDF ({contexto}) no coincide con el golden legacy '{goldenFileName}'.");
    }

    private static string PrimeraDiferencia(JsonNode esperado, JsonNode actual, string golden)
    {
        var linesExp = esperado.ToJsonString(Json).Split('\n');
        var linesAct = actual.ToJsonString(Json).Split('\n');
        int max = Math.Max(linesExp.Length, linesAct.Length);
        for (int i = 0; i < max; i++)
        {
            string e = i < linesExp.Length ? linesExp[i] : "<fin>";
            string a = i < linesAct.Length ? linesAct[i] : "<fin>";
            if (e != a)
                return $"Primera diferencia en línea {i + 1} de {golden}:{Environment.NewLine}  golden: {e.Trim()}{Environment.NewLine}  actual: {a.Trim()}";
        }
        return string.Empty;
    }
}

internal static class JsonNodeExtensions
{
    public static bool IsDeepEqual(this JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a is JsonValue va && b is JsonValue vb)
        {
            return va.TryGetValue<string>(out var _) || vb.TryGetValue<string>(out var _)
                ? va.ToJsonString() == vb.ToJsonString()
                : va.GetValueKind() == vb.GetValueKind() && va.ToJsonString() == vb.ToJsonString();
        }
        if (a is JsonArray arrA && b is JsonArray arrB)
        {
            if (arrA.Count != arrB.Count) return false;
            for (int i = 0; i < arrA.Count; i++)
                if (!IsDeepEqual(arrA[i], arrB[i])) return false;
            return true;
        }
        if (a is JsonObject objA && b is JsonObject objB)
        {
            if (objA.Count != objB.Count) return false;
            foreach (var kv in objA)
            {
                if (!objB.TryGetPropertyValue(kv.Key, out var other)) return false;
                if (!IsDeepEqual(kv.Value, other)) return false;
            }
            return true;
        }
        return false;
    }
}