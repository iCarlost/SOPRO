using System.Security.Cryptography;

namespace SOPRO.Application.Services;

/// <summary>
/// Generación y validación del identificador de correlación de diagnóstico.
/// Usa un alfabeto sin caracteres ambiguos (I, O, 0, 1) para poder dictarlo
/// por teléfono y un código de respaldo legible de la misma longitud.
/// </summary>
public static class DiagnosticsCorrelationId
{
    public const int Length = 8;

    /// <summary>Alfabeto sin caracteres ambiguos (I, O, 0, 1).</summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Código de respaldo cuando el generador no está disponible.</summary>
    public const string Fallback = "SPRA2AAA";

    /// <summary>Genera un identificador nuevo; nunca lanza.</summary>
    public static string Generate()
    {
        try
        {
            return new string(RandomNumberGenerator.GetItems<char>(Alphabet, Length));
        }
        catch
        {
            return Fallback;
        }
    }

    /// <summary>
    /// Usa el identificador de la factoría solo si es válido; en cualquier otro
    /// caso (factoría nula, excepción o valor inválido) genera uno nuevo.
    /// </summary>
    public static string Create(Func<string>? factory)
    {
        if (factory is not null)
        {
            try
            {
                var candidate = factory();
                if (IsValid(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // La factoría inyectada es best-effort: se usa el generador.
            }
        }

        return Generate();
    }

    /// <summary>
    /// Un identificador válido tiene la longitud y el alfabeto aprobados.
    /// </summary>
    public static bool IsValid(string? correlationId)
    {
        if (correlationId is null || correlationId.Length != Length)
        {
            return false;
        }

        foreach (var caracter in correlationId)
        {
            if (!Alphabet.Contains(caracter))
            {
                return false;
            }
        }

        return true;
    }
}
