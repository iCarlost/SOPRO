using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;

namespace SOPRO.Tests.Services.Diagnostics;

[TestClass]
public sealed class DiagnosticsEventTests
{
    [TestMethod]
    public void Constructor_NormalizaElTimestampAUtc()
    {
        var original = new DateTimeOffset(2026, 11, 17, 12, 30, 0, TimeSpan.FromHours(5));

        var evento = new DiagnosticsEvent(
            original,
            DiagnosticsSeverity.Info,
            "SOPRO.Tests",
            "Operacion",
            "1.11.0",
            "corr-1");

        Assert.AreEqual(TimeSpan.Zero, evento.TimestampUtc.Offset);
        Assert.AreEqual(original.UtcDateTime, evento.TimestampUtc.UtcDateTime);
    }

    [TestMethod]
    public void Constructor_NoExponePropiedadDeMetadatosLibres()
    {
        Assert.IsNull(
            typeof(DiagnosticsEvent).GetProperty("Metadata"),
            "El contrato conservador no debe admitir metadatos libres.");
    }

    [TestMethod]
    public void Constructor_ExponeLosTiposInternosComoColeccionSoloLectura()
    {
        var types = new List<string> { "System.IO.IOException" };
        var evento = new DiagnosticsEvent(
            DateTimeOffset.UtcNow,
            DiagnosticsSeverity.Warning,
            "SOPRO.Tests",
            "Operacion",
            "1.11.0",
            "corr-1",
            innerExceptionTypes: types);

        types.Add("System.ArgumentException");

        Assert.AreEqual(1, evento.InnerExceptionTypes.Count);
        Assert.IsFalse(evento.InnerExceptionTypes is string[]);
    }

    [TestMethod]
    public void FromException_ExtraeTipoHResultYCadenaDeTiposSinMensajes()
    {
        const string mensajeSecreto = "detalle-sensible-que-no-debe-persistirse";
        var exception = new InvalidOperationException(
            mensajeSecreto,
            new ArgumentException("inner-sensible", new FormatException("nieto-sensible")));

        var evento = DiagnosticsEvent.FromException(
            DateTimeOffset.UtcNow,
            DiagnosticsSeverity.Error,
            "SOPRO.Tests",
            "AbrirProyecto",
            "1.11.0",
            "corr-1",
            exception);

        Assert.AreEqual(typeof(InvalidOperationException).FullName, evento.ExceptionType);
        Assert.AreEqual(exception.HResult, evento.HResult!.Value);
        Assert.AreEqual(2, evento.InnerExceptionTypes.Count);
        Assert.AreEqual(typeof(ArgumentException).FullName, evento.InnerExceptionTypes[0]);
        Assert.AreEqual(typeof(FormatException).FullName, evento.InnerExceptionTypes[1]);
        Assert.IsFalse(evento.InnerExceptionTypes.Any(tipo => tipo.Contains(mensajeSecreto, StringComparison.Ordinal)));
    }

    [TestMethod]
    public void FromException_ConAggregateException_AplanaLosTiposInternos()
    {
        var aggregate = new AggregateException(
            new InvalidOperationException("uno"),
            new IOException("dos"));

        var evento = DiagnosticsEvent.FromException(
            DateTimeOffset.UtcNow,
            DiagnosticsSeverity.Error,
            "SOPRO.Tests",
            "Operacion",
            "1.11.0",
            "corr-1",
            aggregate);

        Assert.IsTrue(evento.InnerExceptionTypes.Contains(typeof(InvalidOperationException).FullName!));
        Assert.IsTrue(evento.InnerExceptionTypes.Contains(typeof(IOException).FullName!));
    }
}
