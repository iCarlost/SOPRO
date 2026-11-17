using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Diagnostics;

[TestClass]
public sealed class DiagnosticsSanitizerTests
{
    [TestMethod]
    public void Sanitize_SustituyeElPerfilDeUsuarioPorToken()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.IsFalse(string.IsNullOrWhiteSpace(profile), "El entorno de pruebas debe exponer el perfil del usuario.");

        var raw = System.IO.Path.Combine(profile, "Documentos", "proyecto.soproproj");

        var sanitized = DiagnosticsSanitizer.Sanitize($"No se pudo abrir {raw}");

        StringAssert.Contains(sanitized, "%USERPROFILE%");
        Assert.IsFalse(sanitized.Contains(profile, StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(sanitized, "proyecto.soproproj");
    }

    [TestMethod]
    public void Sanitize_SustituyeElPerfilConSeparadoresAlternativos()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.IsFalse(string.IsNullOrWhiteSpace(profile));

        var raw = profile.Replace('\\', '/') + "/Documentos/proyecto.soproproj";

        var sanitized = DiagnosticsSanitizer.Sanitize(raw);

        StringAssert.Contains(sanitized, "%USERPROFILE%");
        Assert.IsFalse(sanitized.Contains(profile, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Sanitize_RedactaPatronesObviosDeSecretos()
    {
        const string valor =
            "Authorization: Bearer abcdef123456; password=hunter2; token: 9876543210; ghp_abcdefgh12345678; api_key=xyz987";

        var sanitized = DiagnosticsSanitizer.Sanitize(valor);

        StringAssert.Contains(sanitized, "[REDACTED]");
        Assert.IsFalse(sanitized.Contains("hunter2"), sanitized);
        Assert.IsFalse(sanitized.Contains("9876543210"), sanitized);
        Assert.IsFalse(sanitized.Contains("abcdef123456"), sanitized);
        Assert.IsFalse(sanitized.Contains("ghp_abcdefgh12345678"), sanitized);
        Assert.IsFalse(sanitized.Contains("xyz987"), sanitized);
    }

    [TestMethod]
    public void Sanitize_NoSustituyeElPerfilDentroDeOtroNombre()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        Assert.IsFalse(string.IsNullOrWhiteSpace(profile));

        foreach (var sufijo in new[] { "copia", "2", "_respaldo", ".bak" })
        {
            var raw = profile + sufijo;

            var sanitized = DiagnosticsSanitizer.Sanitize(raw);

            Assert.AreEqual(raw, sanitized, $"El sufijo '{sufijo}' continúa el nombre y no debe tratarse como perfil.");
            Assert.IsFalse(sanitized.Contains("%USERPROFILE%", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Sanitize_SustituyeElPerfilCuandoTerminaEnFinOSeparador()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        Assert.IsFalse(string.IsNullOrWhiteSpace(profile));

        Assert.AreEqual("%USERPROFILE%", DiagnosticsSanitizer.Sanitize(profile));

        var conSeparador = profile.Replace('\\', '/') + "/Documentos/proyecto.soproproj";
        Assert.AreEqual(
            "%USERPROFILE%/Documentos/proyecto.soproproj",
            DiagnosticsSanitizer.Sanitize(conSeparador));

        Assert.AreEqual(
            "ruta \"%USERPROFILE%\" fin",
            DiagnosticsSanitizer.Sanitize($"ruta \"{profile}\" fin"));
    }

    [TestMethod]
    public void Sanitize_EliminaCaracteresDeControl()
    {
        var sanitized = DiagnosticsSanitizer.Sanitize("a\u0000b\u0001c\r\nd\t\u001B");

        Assert.IsFalse(sanitized.Any(char.IsControl), sanitized);
        StringAssert.Contains(sanitized, "a b c d");
    }

    [TestMethod]
    public void Sanitize_AcotaLaLongitudDelValor()
    {
        var sanitized = DiagnosticsSanitizer.Sanitize(new string('x', 5000));

        Assert.IsTrue(
            sanitized.Length <= DiagnosticsSanitizer.MaxValueLength + 3,
            $"Longitud inesperada: {sanitized.Length}");
        StringAssert.EndsWith(sanitized, "...");
    }

    [TestMethod]
    public void Sanitize_ValorNuloOVacioDevuelveCadenaVacia()
    {
        Assert.AreEqual(string.Empty, DiagnosticsSanitizer.Sanitize(null));
        Assert.AreEqual(string.Empty, DiagnosticsSanitizer.Sanitize(string.Empty));
    }
}
