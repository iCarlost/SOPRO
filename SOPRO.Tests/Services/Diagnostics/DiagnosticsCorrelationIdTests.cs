using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Diagnostics;

[TestClass]
public sealed class DiagnosticsCorrelationIdTests
{
    [TestMethod]
    public void Generate_UsaLongitudYAlfabetoAprobados()
    {
        for (var i = 0; i < 200; i++)
        {
            var correlationId = DiagnosticsCorrelationId.Generate();

            Assert.AreEqual(DiagnosticsCorrelationId.Length, correlationId.Length);
            Assert.IsTrue(
                correlationId.All(caracter => DiagnosticsCorrelationId.Alphabet.Contains(caracter)),
                $"El código {correlationId} contiene caracteres fuera del alfabeto.");
        }
    }

    [TestMethod]
    public void Fallback_EsLegibleYDeLaMismaLongitud()
    {
        Assert.AreEqual(DiagnosticsCorrelationId.Length, DiagnosticsCorrelationId.Fallback.Length);
        Assert.IsTrue(
            DiagnosticsCorrelationId.Fallback
                .All(caracter => DiagnosticsCorrelationId.Alphabet.Contains(caracter)));
    }

    [TestMethod]
    public void Create_ConFactoryValida_ConservaElValor()
    {
        Assert.AreEqual("ABCD2345", DiagnosticsCorrelationId.Create(() => "ABCD2345"));
    }

    [DataTestMethod]
    [DataRow("ABCD234")]
    [DataRow("ABCD23456")]
    [DataRow("ABCD234O")]
    [DataRow("ABCD2340")]
    [DataRow("abcd2345")]
    public void Create_ConFactoryInvalida_GeneraUnCodigoLegible(string valor)
    {
        var correlationId = DiagnosticsCorrelationId.Create(() => valor);

        Assert.AreNotEqual(valor, correlationId);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(correlationId));
    }

    [TestMethod]
    public void Create_ConFactoryQueLanza_GeneraUnCodigoLegible()
    {
        var correlationId = DiagnosticsCorrelationId.Create(
            () => throw new InvalidOperationException("fábrica rota"));

        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(correlationId));
    }

    [TestMethod]
    public void Create_SinFactory_GeneraUnCodigoLegible()
    {
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(DiagnosticsCorrelationId.Create(null)));
    }
}
