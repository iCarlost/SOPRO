using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using System.Reflection;

namespace SOPRO.Tests.Services.ManoObra;

[TestClass]
public class FsrCalculationServiceTests
{
    [TestMethod]
    public void Calcular_SinParametrosODatosInvalidos_DebeRegresarNull()
    {
        Assert.IsNull(FsrCalculationService.Calcular(null, 500m));
        Assert.IsNull(FsrCalculationService.Calcular(string.Empty, 500m));
        Assert.IsNull(FsrCalculationService.Calcular("{ json invalido", 500m));
        Assert.IsNull(FsrCalculationService.Calcular("{}", 0m));
    }

    [TestMethod]
    public void Calcular_ConParametrosBase_DebeRegresarFactorMayorQueUno()
    {
        const string parametros = """
        {
          "SalarioMinimo":"248.93",
          "Jornada":"0",
          "Semestre":"0",
          "Anio":"2026",
          "HorasJornada":"8",
          "DiasCalendario":"365",
          "DiasAguinaldo":"15",
          "DiasVacaciones":"12",
          "PrimaVacacional":"25",
          "DiasDescanso":"52",
          "DiasFestivos":"7",
          "PctGuarderias":"1",
          "PctRetiro":"2",
          "PctRiesgos":"4.58875",
          "PctINFONAVIT":"5",
          "PctNomina":"2.4"
        }
        """;

        var factor = FsrCalculationService.Calcular(parametros, 500m);

        Assert.IsTrue(factor.HasValue);
        Assert.IsTrue(factor.Value > 1m);
        Assert.IsTrue(factor.Value < 3m);
    }

    [TestMethod]
    public void Calcular_ConParametrosBase_DebeRegresarFactorDoradoExacto()
    {
        const string parametros = """
        {
          "SalarioMinimo":"248.93",
          "Jornada":"0",
          "Semestre":"0",
          "Anio":"2026",
          "HorasJornada":"8",
          "DiasCalendario":"365",
          "DiasAguinaldo":"15",
          "DiasVacaciones":"12",
          "PrimaVacacional":"25",
          "DiasDescanso":"52",
          "DiasFestivos":"7",
          "PctGuarderias":"1",
          "PctRetiro":"2",
          "PctRiesgos":"4.58875",
          "PctINFONAVIT":"5",
          "PctNomina":"2.4"
        }
        """;

        var factor = FsrCalculationService.Calcular(parametros, 500m);

        Assert.IsNotNull(factor);
        Assert.AreEqual(1.7308240339418507128878948841m, factor.Value);
    }

    [TestMethod]
    public void TryCalcularFactorSalarioReal_DebeRedondearASeisDecimalesONoAplicar()
    {
        const string parametros = """
        {
          "SalarioMinimo":"248.93",
          "Jornada":"0",
          "Semestre":"0",
          "Anio":"2026",
          "HorasJornada":"8",
          "DiasCalendario":"365",
          "DiasAguinaldo":"15",
          "DiasVacaciones":"12",
          "PrimaVacacional":"25",
          "DiasDescanso":"52",
          "DiasFestivos":"7",
          "PctGuarderias":"1",
          "PctRetiro":"2",
          "PctRiesgos":"4.58875",
          "PctINFONAVIT":"5",
          "PctNomina":"2.4"
        }
        """;

        Assert.IsTrue(FsrCalculationService.TryCalcularFactorSalarioReal(parametros, 500m, out var factor));
        Assert.AreEqual(1.730824m, factor);

        Assert.IsFalse(FsrCalculationService.TryCalcularFactorSalarioReal(null, 500m, out _));
        Assert.IsFalse(FsrCalculationService.TryCalcularFactorSalarioReal("{ json invalido", 500m, out _));
        Assert.IsFalse(FsrCalculationService.TryCalcularFactorSalarioReal(parametros, 0m, out _));
    }

    [TestMethod]
    public void Calcular_ConDiferentesSalarios_DebeRegresarFactoresDorados()
    {
        const string parametros = """
        {
          "SalarioMinimo":"248.93",
          "Jornada":"0",
          "Semestre":"0",
          "Anio":"2026",
          "HorasJornada":"8",
          "DiasCalendario":"365",
          "DiasAguinaldo":"15",
          "DiasVacaciones":"12",
          "PrimaVacacional":"25",
          "DiasDescanso":"52",
          "DiasFestivos":"7",
          "PctGuarderias":"1",
          "PctRetiro":"2",
          "PctRiesgos":"4.58875",
          "PctINFONAVIT":"5",
          "PctNomina":"2.4"
        }
        """;

        var f250 = FsrCalculationService.Calcular(parametros, 250m);
        var f800 = FsrCalculationService.Calcular(parametros, 800m);

        Assert.IsNotNull(f250);
        Assert.IsNotNull(f800);
        Assert.AreEqual(1.8631328690438915292144254962m, f250.Value);
        Assert.AreEqual(1.6828680219556658279750256267m, f800.Value);
    }

    // ═════════════════════ Diagnóstico del catch real (issue #145) ═════════════════════

    [TestMethod]
    public void Calcular_JsonInvalidoConSink_RegistraErrorSinDatosSensibles()
    {
        const string jsonInvalido = "{ json invalido RUTA:C:\\secreto\\proyecto.soproproj";
        var sink = new InMemoryDiagnosticsSink();

        var factor = FsrCalculationService.Calcular(jsonInvalido, 500m, sink, "1.11.0");

        Assert.IsNull(factor);
        var eventos = sink.Snapshot();
        Assert.AreEqual(1, eventos.Count);

        var evento = eventos[0];
        Assert.AreEqual(DiagnosticsSeverity.Error, evento.Severity);
        Assert.AreEqual(FsrCalculationService.DiagnosticsSource, evento.Source);
        Assert.AreEqual(FsrCalculationService.OperationCalcularFactorSalarioReal, evento.Operation);
        Assert.AreEqual("1.11.0", evento.ProductVersion);
        Assert.AreEqual(typeof(System.Text.Json.JsonException).FullName, evento.ExceptionType);
        Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId));
        Assert.IsTrue(evento.InnerExceptionTypes.All(tipo => tipo.StartsWith("System.", StringComparison.Ordinal)),
            "Los tipos internos solo exponen nombres de tipo.");
        AssertSinDatosSensibles(evento, jsonInvalido, "500", "C:\\secreto", "json invalido");
    }

    [TestMethod]
    public void Calcular_SinParametrosOSalarioNoPositivo_NoRegistraEventos()
    {
        var sink = new InMemoryDiagnosticsSink();

        Assert.IsNull(FsrCalculationService.Calcular(null, 500m, sink, "1.11.0"));
        Assert.IsNull(FsrCalculationService.Calcular(string.Empty, 500m, sink, "1.11.0"));
        Assert.IsNull(FsrCalculationService.Calcular("{}", 0m, sink, "1.11.0"));
        Assert.IsNull(FsrCalculationService.Calcular("{}", -10m, sink, "1.11.0"));

        Assert.AreEqual(0, sink.Count,
            "Los retornos null previstos por parámetros ausentes o salario no positivo no son incidencias.");
    }

    [TestMethod]
    public void Calcular_CatchDeCalculoConSink_RegistraError()
    {
        // Salario mínimo cero: el motor puro divide entre cero al calibrar.
        const string parametros = """{ "SalarioMinimo":"0" }""";
        var sink = new InMemoryDiagnosticsSink();

        var factor = FsrCalculationService.Calcular(parametros, 500m, sink, "1.11.0");

        Assert.IsNull(factor);
        var evento = sink.Snapshot().Single();
        Assert.AreEqual(DiagnosticsSeverity.Error, evento.Severity);
        Assert.AreEqual(FsrCalculationService.OperationCalcularFactorSalarioReal, evento.Operation);
        Assert.AreEqual(typeof(DivideByZeroException).FullName, evento.ExceptionType);
    }

    [TestMethod]
    public void Calcular_SinkQueLanza_NoPropagaYDevuelveNull()
    {
        var factor = FsrCalculationService.Calcular("{ json invalido", 500m, new SinkQueLanza(), "1.11.0");

        Assert.IsNull(factor);
    }

    [TestMethod]
    public void Calcular_SinVersionDeProducto_UsaVersionPorDefecto()
    {
        var sink = new InMemoryDiagnosticsSink();

        FsrCalculationService.Calcular("{ json invalido", 500m, sink);

        Assert.AreEqual("0.0.0", sink.Snapshot().Single().ProductVersion);
    }

    [TestMethod]
    public void TryCalcularFactorSalarioReal_ReenviaSinkYVersionAlCatchReal()
    {
        var sink = new InMemoryDiagnosticsSink();

        var calculado = FsrCalculationService.TryCalcularFactorSalarioReal(
            "{ json invalido", 500m, out var factor, sink, "1.11.0");

        Assert.IsFalse(calculado);
        Assert.AreEqual(0m, factor);

        var evento = sink.Snapshot().Single();
        Assert.AreEqual(DiagnosticsSeverity.Error, evento.Severity);
        Assert.AreEqual(FsrCalculationService.OperationCalcularFactorSalarioReal, evento.Operation);
        Assert.AreEqual("1.11.0", evento.ProductVersion);
    }

    [TestMethod]
    public void TryCalcularFactorSalarioReal_SinParametrosNiSalario_NoRegistraEventos()
    {
        var sink = new InMemoryDiagnosticsSink();

        Assert.IsFalse(FsrCalculationService.TryCalcularFactorSalarioReal(null, 500m, out _, sink, "1.11.0"));
        Assert.IsFalse(FsrCalculationService.TryCalcularFactorSalarioReal("{}", 0m, out _, sink, "1.11.0"));

        Assert.AreEqual(0, sink.Count);
    }

    private static void AssertSinDatosSensibles(DiagnosticsEvent evento, params string[] prohibidos)
    {
        foreach (var propiedad in typeof(DiagnosticsEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (propiedad.PropertyType != typeof(string) || propiedad.GetValue(evento) is not string valor)
            {
                continue;
            }

            foreach (var prohibido in prohibidos)
            {
                Assert.IsFalse(
                    valor.Contains(prohibido, StringComparison.Ordinal),
                    $"La propiedad {propiedad.Name} filtró dato sensible.");
            }

            Assert.IsFalse(valor.Contains("   at ", StringComparison.Ordinal),
                $"La propiedad {propiedad.Name} filtró una traza.");
        }
    }

    private sealed class SinkQueLanza : IDiagnosticsSink
    {
        public void Write(DiagnosticsEvent diagnosticsEvent)
            => throw new InvalidOperationException("fallo de sink inyectado");

        public void Flush()
            => throw new InvalidOperationException("fallo de flush inyectado");
    }
}
