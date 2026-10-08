using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;

namespace SOPRO.Tests.Services.Workspace;

/// <summary>
/// Contrato de la convención de extensiones de proyecto (Fase 1.1):
/// la canónica es <c>.soproproj</c>, con <c>.db</c> y <c>.sopro</c> admitidas
/// al abrir. Todas las comparaciones deben ser case-insensitive.
/// </summary>
[TestClass]
public class ProjectFileExtensionConventionTests
{
    [TestMethod]
    public void Constantes_TienenLosValoresEsperados()
    {
        Assert.AreEqual(".soproproj", ProjectFileExtensions.Canonical);
        Assert.AreEqual(".db", ProjectFileExtensions.LegacyDb);
        Assert.AreEqual(".sopro", ProjectFileExtensions.TransitionalSopro);
    }

    [TestMethod]
    public void SupportedForOpen_ContieneLasTresFormas_ConLaCanonicaPrimero()
    {
        var admitted = ProjectFileExtensions.SupportedForOpen;

        Assert.IsTrue(admitted.Contains(ProjectFileExtensions.Canonical, StringComparer.OrdinalIgnoreCase));
        Assert.IsTrue(admitted.Contains(ProjectFileExtensions.LegacyDb, StringComparer.OrdinalIgnoreCase));
        Assert.IsTrue(admitted.Contains(ProjectFileExtensions.TransitionalSopro, StringComparer.OrdinalIgnoreCase));
        Assert.AreEqual(ProjectFileExtensions.Canonical, admitted[0], "la canónica debe ir primero");
    }

    [TestMethod]
    public void GetSearchPattern_DerivaGlobDeLaExtension()
    {
        Assert.AreEqual("*.soproproj", ProjectFileExtensions.GetSearchPattern(ProjectFileExtensions.Canonical));
        Assert.AreEqual("*.db", ProjectFileExtensions.GetSearchPattern(ProjectFileExtensions.LegacyDb));
        Assert.AreEqual("*.sopro", ProjectFileExtensions.GetSearchPattern(ProjectFileExtensions.TransitionalSopro));
    }

    [TestMethod]
    public void GetSearchPattern_NormalizaMayusculasYFaltaDePunto()
    {
        Assert.AreEqual("*.db", ProjectFileExtensions.GetSearchPattern(".DB"));
        Assert.AreEqual("*.sopro", ProjectFileExtensions.GetSearchPattern("sopro"));
    }

    [TestMethod]
    public void GetSearchPattern_ExtensionNoAdmitida_Lanza()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ProjectFileExtensions.GetSearchPattern(".txt"));
    }

    [TestMethod]
    public void GetOpenSearchPatterns_ListaCanonicaYAnterioresEnOrden()
    {
        var patterns = ProjectFileExtensions.GetOpenSearchPatterns();

        Assert.AreEqual("*.soproproj", patterns[0]);
        Assert.AreEqual("*.db", patterns[1]);
        Assert.AreEqual("*.sopro", patterns[2]);
    }

    [TestMethod]
    public void BuildFileDialogFilter_IncluyeCanonicaAnterioresYTodos()
    {
        var filter = ProjectFileExtensions.BuildFileDialogFilter();

        StringAssert.Contains(filter, "*.soproproj");
        StringAssert.Contains(filter, "*.db;*.sopro");
        StringAssert.Contains(filter, "Todos los archivos (*.*)|*.*");

        var canonicalIndex = filter.IndexOf(".soproproj", StringComparison.Ordinal);
        var legacyIndex = filter.IndexOf(".db", StringComparison.Ordinal);
        Assert.IsTrue(canonicalIndex >= 0, "el filtro debe contener la extensión canónica");
        Assert.IsTrue(canonicalIndex < legacyIndex, "la canónica debe aparecer antes que las anteriores");
    }

    [TestMethod]
    public void IsSupportedForOpen_EsCaseInsensitive_ParaRutaYExtensionSuelta()
    {
        Assert.IsTrue(ProjectFileExtensions.IsSupportedForOpen(".SOPROPROJ"));
        Assert.IsTrue(ProjectFileExtensions.IsSupportedForOpen(".Sopro"));
        Assert.IsTrue(ProjectFileExtensions.IsSupportedForOpen(Path.Combine("Proyectos", "Obra.DB")));
        Assert.IsTrue(ProjectFileExtensions.IsSupportedForOpen("proyecto.soproproj"));
    }

    [TestMethod]
    public void IsSupportedForOpen_RechazaVaciosYExtensionesAjenas()
    {
        Assert.IsFalse(ProjectFileExtensions.IsSupportedForOpen(null));
        Assert.IsFalse(ProjectFileExtensions.IsSupportedForOpen(string.Empty));
        Assert.IsFalse(ProjectFileExtensions.IsSupportedForOpen("   "));
        Assert.IsFalse(ProjectFileExtensions.IsSupportedForOpen("proyecto.txt"));
        Assert.IsFalse(ProjectFileExtensions.IsSupportedForOpen("proyecto.soproproj.bak"));
    }

    [TestMethod]
    public void GetProjectExtension_DevuelveLaAdmitidaNormalizada()
    {
        Assert.AreEqual(".soproproj", ProjectFileExtensions.GetProjectExtension(Path.Combine("Proyectos", "Obra.SOPROPROJ")));
        Assert.AreEqual(".db", ProjectFileExtensions.GetProjectExtension(Path.Combine("Proyectos", "Obra.Db")));
        Assert.IsNull(ProjectFileExtensions.GetProjectExtension("Obra.xlsx"));
    }
}
