using System;
using System.Globalization;

namespace SOPRO.Application.Services;

/// <summary>
/// Comparación del contrato de versión de esquema <c>AAAA.MM.revision</c>.
///
/// Contrato estricto: año de 4 dígitos, mes de 2 y revisión de 1+ dígitos, sin
/// componentes extra (<c>2026.04.4.0</c> no equivale a <c>2026.04.4</c>),
/// longitudes laxas (<c>2026.4.4</c> / <c>26.04.4</c>), signos ni espacios.
/// Una versión que no cumple el contrato no es comparable.
/// </summary>
public static class SchemaVersion
{
    /// <summary>
    /// Compara dos versiones. Devuelve un entero negativo si <paramref name="left"/>
    /// es anterior, cero si son iguales, positivo si es posterior, o <c>null</c>
    /// cuando alguna de las dos no cumple el contrato.
    /// </summary>
    public static int? Compare(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftParts = Parse(left);
        var rightParts = Parse(right);
        if (leftParts is null || rightParts is null)
            return null;

        for (var i = 0; i < leftParts.Length; i++)
        {
            var comparison = leftParts[i].CompareTo(rightParts[i]);
            if (comparison != 0)
                return comparison;
        }

        return 0;
    }

    private static int[]? Parse(string value)
    {
        var parts = value.Split('.');
        if (parts.Length != 3
            || parts[0].Length != 4
            || parts[1].Length != 2
            || parts[2].Length == 0)
            return null;

        var parsed = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                return null;

            foreach (var caracter in parts[i])
            {
                if (caracter is < '0' or > '9')
                    return null;
            }

            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out parsed[i]))
                return null;
        }

        return parsed;
    }
}
