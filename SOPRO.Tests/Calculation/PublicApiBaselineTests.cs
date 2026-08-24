using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SOPRO.Tests.Calculation;

/// <summary>
/// Gate N6: inventario de la API pública de Sopro.Calculation contra un baseline.
/// Cualquier adición, remoción o cambio de firma pública hace fallir este test
/// hasta que el baseline se actualice de forma intencional (cambio documentado).
///
/// Para regenerar el baseline tras un cambio aprobado:
///   set SOPRO_UPDATE_API_BASELINE=1  (Windows)
///   export SOPRO_UPDATE_API_BASELINE=1  (Linux/macOS)
///   dotnet test --filter PublicApiBaselineTests
/// </summary>
[TestClass]
public class PublicApiBaselineTests
{
    private static readonly string BaselinePath = Path.Combine(
        FindRepoRoot()?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio."),
        "SOPRO.Tests", "Calculation", "SoproCalculationApiBaseline.txt");

    [TestMethod]
    public void PublicApiMatchesBaseline()
    {
        var actual = InventoryPublicApi();

        if (string.Equals(Environment.GetEnvironmentVariable("SOPRO_UPDATE_API_BASELINE"), "1", StringComparison.Ordinal))
        {
            File.WriteAllLines(BaselinePath, actual);
            return;
        }

        var expected = File.ReadAllLines(BaselinePath);

        var missing = expected.Except(actual).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var added = actual.Except(expected).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.IsTrue(missing.Count == 0 && added.Count == 0,
            "La API pública cambió respecto al baseline (Gate N6).\n" +
            "Removido del baseline:\n  " + string.Join("\n  ", missing) + "\n" +
            "Agregado sin baseline:\n  " + string.Join("\n  ", added) + "\n" +
            "Si el cambio es intencional y documentado, regenera el baseline con SOPRO_UPDATE_API_BASELINE=1.");
    }

    private static List<string> InventoryPublicApi()
    {
        var assembly = typeof(Sopro.Calculation.SoproCalculationEngine).Assembly;
        var lines = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in assembly.GetTypes()
                     .Where(t => t.IsPublic && t.IsVisible)
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add($"type {type.FullName}");

            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                lines.Add($"ctor {type.FullName}({FormatParams(ctor.GetParameters())})");

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                lines.Add($"method {FormatType(method.ReturnType)} {type.FullName}.{method.Name}({FormatParams(method.GetParameters())})");

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                lines.Add($"prop {FormatType(property.PropertyType)} {type.FullName}.{property.Name}");

            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                    lines.Add($"enum-member {type.FullName}.{name}");
            }
        }

        return lines.ToList();
    }

    private static string FormatParams(ParameterInfo[] parameters)
        => parameters.Length == 0
            ? string.Empty
            : string.Join(", ", parameters.Select(p => $"{FormatType(p.ParameterType)} {p.Name}"));

    private static string FormatType(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var args = string.Join(", ", type.GetGenericArguments().Select(FormatType));
        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{args}>";
    }

    private static DirectoryInfo? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SOPRO.sln")))
            dir = dir.Parent;
        return dir;
    }
}
