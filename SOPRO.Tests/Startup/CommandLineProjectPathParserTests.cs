using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Startup;

/// <summary>
/// Parser de la ruta de proyecto recibida por línea de comandos (Fase 1.1).
/// Toma el primer argumento que sea un archivo admitido, ignora switches y
/// normaliza la ruta sin alterar espacios ni Unicode.
/// </summary>
[TestClass]
public class CommandLineProjectPathParserTests
{
    [TestMethod]
    public void TryParse_SinArgumentos_DevuelveFalso()
    {
        Assert.IsFalse(CommandLineProjectPathParser.TryParse(null, out var nullPath));
        Assert.IsNull(nullPath);

        Assert.IsFalse(CommandLineProjectPathParser.TryParse(Array.Empty<string>(), out var emptyPath));
        Assert.IsNull(emptyPath);
    }

    [TestMethod]
    public void TryParse_SoloFlags_DevuelveFalso()
    {
        Assert.IsFalse(CommandLineProjectPathParser.TryParse(
            new[] { "-x", "--verbose", "--output", "-" }, out var path));
        Assert.IsNull(path);
    }

    [TestMethod]
    public void TryParse_UnArgumentoValido_DevuelveRutaAbsoluta()
    {
        var file = Path.Combine("Proyectos", "Obra.soproproj");

        Assert.IsTrue(CommandLineProjectPathParser.TryParse(new[] { file }, out var path));
        Assert.AreEqual(Path.GetFullPath(file), path);
    }

    [TestMethod]
    public void TryParse_ArchivoConEspaciosYUnicode_PreservaLosCaracteres()
    {
        var file = Path.Combine(Path.GetTempPath(), "Mis Proyectos", "Ñandú 项目.soproproj");

        Assert.IsTrue(CommandLineProjectPathParser.TryParse(new[] { file }, out var path));
        Assert.AreEqual(Path.GetFullPath(file), path);
        StringAssert.Contains(path!, "Mis Proyectos");
        StringAssert.Contains(path!, "Ñandú 项目");
    }

    [TestMethod]
    public void TryParse_RutaRelativa_LaNormalizaAAbsoluta()
    {
        var file = Path.Combine("carpeta", "subcarpeta", "proyecto.db");

        Assert.IsTrue(CommandLineProjectPathParser.TryParse(new[] { file }, out var path));
        Assert.IsTrue(Path.IsPathFullyQualified(path!), "la ruta devuelta debe ser absoluta");
        Assert.AreEqual(Path.GetFullPath(file), path);
    }

    [TestMethod]
    public void TryParse_ExtensionNoAdmitida_DevuelveFalso()
    {
        Assert.IsFalse(CommandLineProjectPathParser.TryParse(new[] { "proyecto.txt" }, out var path));
        Assert.IsNull(path);

        Assert.IsFalse(CommandLineProjectPathParser.TryParse(
            new[] { "documento.docx", "imagen.png" }, out var other));
        Assert.IsNull(other);
    }

    [TestMethod]
    public void TryParse_MayusculasEnLaExtension_EsAceptado()
    {
        var file = Path.Combine("Proyectos", "Obra.SOPROPROJ");

        Assert.IsTrue(CommandLineProjectPathParser.TryParse(new[] { file }, out var path));
        Assert.AreEqual(Path.GetFullPath(file), path);
    }

    [TestMethod]
    public void TryParse_AliasTransicionalSopro_EsAceptado()
    {
        var file = Path.Combine("Proyectos", "Obra.sopro");

        Assert.IsTrue(CommandLineProjectPathParser.TryParse(new[] { file }, out var path));
        Assert.AreEqual(Path.GetFullPath(file), path);
    }

    [TestMethod]
    public void TryParse_MultiplesArgumentos_TomaElPrimerArchivo()
    {
        var primero = Path.Combine("Proyectos", "Primero.db");
        var segundo = Path.Combine("Proyectos", "Segundo.soproproj");

        Assert.IsTrue(CommandLineProjectPathParser.TryParse(
            new[] { "--verbose", "readme.txt", primero, segundo }, out var path));
        Assert.AreEqual(Path.GetFullPath(primero), path);
    }

    [TestMethod]
    public void Parse_VarianteDeConveniencia_DevuelveNullSinArchivo()
    {
        Assert.IsNull(CommandLineProjectPathParser.Parse(new[] { "--help" }));

        var file = Path.Combine("Proyectos", "Obra.soproproj");
        Assert.AreEqual(Path.GetFullPath(file), CommandLineProjectPathParser.Parse(new[] { file }));
    }
}
