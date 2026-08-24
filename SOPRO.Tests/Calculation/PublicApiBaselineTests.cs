using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SOPRO.Tests.Calculation;

/// <summary>
/// Gate N6: inventario exhaustivo de la API pública de Sopro.Calculation contra un baseline.
///
/// Captura (y por tanto detecta cambios en) los siguientes aspectos de superficie:
///  - Tipos públicos (incluidos anidados) y sus modificadores (sealed/abstract/static).
///  - Constructores, métodos, propiedades (get/set/init), campos (incl. const/static), eventos.
///  - Nulabilidad de referencia (?).
///  - Valores por defecto de parámetros (p.ej. = 0m).
///  - Diferencia entre setter init y set.
///  - Valores numéricos de los miembros de enumeración.
///  - Tipos de parámetros y retorno (incl. Nullable&lt;T&gt; y genéricos).
///
/// Cualquier adición, remoción o alteración de cualquiera de esos aspectos hace fallir el
/// test hasta que el baseline se regenere de forma intencional (cambio documentado).
///
/// Para regenerar el baseline tras un cambio aprobado:
///   set SOPRO_UPDATE_API_BASELINE=1  (Windows)
///   export SOPRO_UPDATE_API_BASELINE=1  (Linux/macOS)
///   dotnet test --filter PublicApiBaselineTests
/// </summary>
[TestClass]
public class PublicApiBaselineTests
{
    private static readonly Assembly ApiAssembly = typeof(Sopro.Calculation.SoproCalculationEngine).Assembly;

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

        var missing = expected.Except(actual, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var added = actual.Except(expected, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.IsTrue(missing.Count == 0 && added.Count == 0,
            "La API pública cambió respecto al baseline (Gate N6).\n" +
            "Removido del baseline:\n  " + string.Join("\n  ", missing) + "\n" +
            "Agregado sin baseline:\n  " + string.Join("\n  ", added) + "\n" +
            "Si el cambio es intencional y documentado, regenera el baseline con SOPRO_UPDATE_API_BASELINE=1.");
    }

    private static List<string> InventoryPublicApi()
    {
        var lines = new SortedSet<string>(StringComparer.Ordinal);
        var types = ApiAssembly.GetTypes()
            .Where(t => t.IsPublic && t.IsVisible)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var type in types)
        {
            var modifiers = TypeModifiers(type);
            lines.Add($"type {modifiers}{type.FullName}");

            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                         .OrderBy(c => c.Name + "(" + FormatParams(c.GetParameters()) + ")", StringComparer.Ordinal))
                lines.Add($"ctor {type.FullName}({FormatParams(ctor.GetParameters())})");

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(m => !m.IsSpecialName)
                         .OrderBy(m => m.Name, StringComparer.Ordinal))
                lines.Add($"method {FormatType(method.ReturnType, method, method.DeclaringType)} {type.FullName}.{method.Name}({FormatParams(method.GetParameters())})");

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                lines.Add($"prop {FormatType(property.PropertyType, property, property.DeclaringType)} {type.FullName}.{property.Name}");
                var setMethod = property.SetMethod;
                if (setMethod != null)
                {
                    var isInit = setMethod.ReturnParameter.GetRequiredCustomModifiers()
                        .Any(m => m == typeof(System.Runtime.CompilerServices.IsExternalInit));
                    lines.Add($"prop-set {(isInit ? "init" : "set")} {FormatType(property.PropertyType, property, property.DeclaringType)} {type.FullName}.{property.Name}");
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(f => !f.IsSpecialName && !type.IsEnum)
                         .OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                var kind = field.IsLiteral ? "const" : (field.IsStatic ? "static-field" : "field");
                var suffix = field.IsLiteral ? $" = {FormatDefault(field.GetRawConstantValue())}" : string.Empty;
                lines.Add($"{kind} {FormatType(field.FieldType, field, type)} {type.FullName}.{field.Name}{suffix}");
            }

            foreach (var evt in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .OrderBy(e => e.Name, StringComparer.Ordinal))
                lines.Add($"event {FormatType(evt.EventHandlerType!, evt, type)} {type.FullName}.{evt.Name}");

            if (type.IsEnum)
            {
                var underlying = Enum.GetUnderlyingType(type);
                foreach (var name in Enum.GetNames(type).OrderBy(n => n, StringComparer.Ordinal))
                {
                    var value = Convert.ChangeType(Enum.Parse(type, name), underlying);
                    lines.Add($"enum-member {type.FullName}.{name} = {value}");
                }
            }
        }

        return lines.ToList();
    }

    private static string FormatParams(ParameterInfo[] parameters)
        => parameters.Length == 0
            ? string.Empty
            : string.Join(", ", parameters.Select(p =>
            {
                var type = FormatType(p.ParameterType, p, null);
                var def = p.HasDefaultValue ? $" = {FormatDefault(p.DefaultValue)}" : string.Empty;
                return $"{type} {p.Name}{def}";
            }));

    private static string FormatType(Type type, ICustomAttributeProvider? primary, ICustomAttributeProvider? context)
    {
        if (type.IsByRef || type.IsPointer)
            return FormatType(type.GetElementType()!, primary, context) + (type.IsByRef ? " ref" : string.Empty);

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return FormatType(underlying, primary, context) + "?";

        if (type.IsGenericType)
        {
            var args = string.Join(", ", type.GetGenericArguments().Select(a => FormatType(a, null, context)));
            var name = type.Name[..type.Name.IndexOf('`')];
            return $"{name}<{args}>";
        }

        if (!type.IsValueType && IsNullableReference(primary, context))
            return type.Name + "?";

        return type.Name;
    }

    private static bool IsNullableReference(ICustomAttributeProvider? primary, ICustomAttributeProvider? context)
    {
        var flag = GetNullableFlag(primary) ?? GetNullableFlag(context) ?? GetNullableContextFlag(context) ?? GetNullableContextFlag(ApiAssembly);
        return flag == 2;
    }

    private static byte? GetNullableFlag(ICustomAttributeProvider? provider)
    {
        if (provider == null) return null;
        var attr = provider.GetCustomAttributes(typeof(NullableAttribute), false)
            .Cast<NullableAttribute>().FirstOrDefault();
        return attr?.NullableFlags[0];
    }

    private static byte? GetNullableContextFlag(ICustomAttributeProvider? provider)
    {
        if (provider == null) return null;
        var attr = provider.GetCustomAttributes(typeof(NullableContextAttribute), false)
            .Cast<NullableContextAttribute>().FirstOrDefault();
        return attr?.Flag;
    }

    private static string FormatDefault(object? value)
    {
        if (value == null) return "null";
        return value switch
        {
            string s => $"\"{s}\"",
            decimal d => $"{d}m",
            float f => $"{f}f",
            double db => $"{db}d",
            char c => $"'{c}'",
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static string TypeModifiers(Type type)
    {
        if (type.IsValueType || type.IsEnum || type.IsInterface || typeof(Delegate).IsAssignableFrom(type)) return string.Empty;
        if (type.IsSealed && type.IsAbstract) return "static ";
        if (type.IsAbstract) return "abstract ";
        if (type.IsSealed) return "sealed ";
        return string.Empty;
    }

    private static DirectoryInfo? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SOPRO.sln")))
            dir = dir.Parent;
        return dir;
    }
}
