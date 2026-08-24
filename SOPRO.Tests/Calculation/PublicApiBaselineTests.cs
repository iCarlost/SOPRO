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
/// Cubre los siguientes aspectos de superficie (cualquier alteración hace fallir el test
/// hasta regenerar el baseline de forma intencional y documentada):
///  - Tipos públicos, incluidos los anidados, con sus modificadores (sealed/abstract/static) y visibilidad.
///  - Constructores, métodos (incl. operadores op_*) y su visibilidad/static.
///  - Propiedades con getter/setter por separado, su visibilidad, static e init vs set.
///  - Campos (incl. const/static) y eventos con visibilidad/static.
///  - Nulabilidad de referencia (?), incluyendo tipos genéricos (p.ej. IEnumerable&lt;Decimal&gt;?).
///  - Valores por defecto de parámetros (p.ej. = 0m).
///  - RefKind de parámetros: ref / out / in.
///  - Valores numéricos de los miembros de enumeración.
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
            .Where(t => t.IsVisible)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var type in types)
        {
            var nested = type.IsNested ? "nested " : string.Empty;
            lines.Add($"type {TypeModifiers(type)}{nested}{type.FullName}");

            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                         .OrderBy(c => c.Name + "(" + FormatParams(c.GetParameters(), c) + ")", StringComparer.Ordinal))
            {
                var vis = ctor.IsPublic ? string.Empty : "internal ";
                lines.Add($"ctor {vis}{type.FullName}({FormatParams(ctor.GetParameters(), ctor)})");
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(m => !m.IsSpecialName || m.Name.StartsWith("op_", StringComparison.Ordinal))
                         .OrderBy(m => m.Name, StringComparer.Ordinal))
                lines.Add($"method {MemberVisibility(method)}{(method.IsStatic ? "static " : string.Empty)}{FormatType(method.ReturnType, method, method.DeclaringType)} {type.FullName}.{method.Name}({FormatParams(method.GetParameters(), method)})");

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var getter = property.GetMethod;
                var setter = property.SetMethod;
                if (getter != null)
                    lines.Add($"prop-get {MemberVisibility(getter)}{(getter.IsStatic ? "static " : string.Empty)}{FormatType(property.PropertyType, property, property.DeclaringType)} {type.FullName}.{property.Name}");
                if (setter != null)
                {
                    var isInit = setter.ReturnParameter.GetRequiredCustomModifiers()
                        .Any(m => m == typeof(IsExternalInit));
                    lines.Add($"prop-set {MemberVisibility(setter)}{(setter.IsStatic ? "static " : string.Empty)}{(isInit ? "init" : "set")} {FormatType(property.PropertyType, property, property.DeclaringType)} {type.FullName}.{property.Name}");
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(f => !f.IsSpecialName && !type.IsEnum)
                         .OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                var kind = field.IsLiteral ? "const" : (field.IsStatic ? "static-field" : "field");
                var vis = field.IsLiteral ? string.Empty : (field.IsPublic ? string.Empty : "non-public ");
                var suffix = field.IsLiteral ? $" = {FormatDefault(field.GetRawConstantValue())}" : string.Empty;
                lines.Add($"{kind} {vis}{FormatType(field.FieldType, field, type)} {type.FullName}.{field.Name}{suffix}");
            }

            foreach (var evt in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                var add = evt.AddMethod;
                var vis = add != null ? MemberVisibility(add) : string.Empty;
                var stat = add != null && add.IsStatic ? "static " : string.Empty;
                lines.Add($"event {vis}{stat}{FormatType(evt.EventHandlerType!, evt, type)} {type.FullName}.{evt.Name}");
            }

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

    private static string FormatParams(ParameterInfo[] parameters, MethodBase? method)
        => parameters.Length == 0
            ? string.Empty
            : string.Join(", ", parameters.Select(p =>
            {
                var kind = RefKind(p);
                var element = p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType;
                var type = FormatType(element, p, method);
                var def = p.HasDefaultValue ? $" = {FormatDefault(p.DefaultValue)}" : string.Empty;
                return $"{kind}{type} {p.Name}{def}";
            }));

    private static string RefKind(ParameterInfo p)
    {
        if (!p.ParameterType.IsByRef) return string.Empty;
        if (p.IsOut) return "out ";
        if (p.GetRequiredCustomModifiers().Any(m => m.Name == "IsReadOnlyAttribute")) return "in ";
        return "ref ";
    }

    private static string FormatType(Type type, ICustomAttributeProvider? primary, ICustomAttributeProvider? context)
    {
        if (type.IsByRef || type.IsPointer)
            return FormatType(type.GetElementType()!, primary, context) + (type.IsByRef ? " ref" : " ptr");

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return FormatType(underlying, primary, context) + "?";

        if (type.IsGenericType)
        {
            var args = string.Join(", ", type.GetGenericArguments().Select(a => FormatType(a, null, context)));
            var name = type.Name[..type.Name.IndexOf('`')];
            var nullable = (!type.IsValueType && IsNullableReference(primary, context)) ? "?" : string.Empty;
            return $"{name}<{args}>{nullable}";
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

    private static string MemberVisibility(MethodInfo m)
    {
        if (m.IsPublic) return string.Empty;
        if (m.IsFamily) return "protected ";
        if (m.IsAssembly) return "internal ";
        if (m.IsFamilyOrAssembly) return "protected internal ";
        if (m.IsFamilyAndAssembly) return "private protected ";
        return "private ";
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
