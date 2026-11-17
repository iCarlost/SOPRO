using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Diagnostics;

[TestClass]
public sealed class InMemoryDiagnosticsSinkTests
{
    [TestMethod]
    public void Write_AcumulaEventosEnOrden()
    {
        var sink = new InMemoryDiagnosticsSink();

        sink.Write(CreateEvent("uno"));
        sink.Write(CreateEvent("dos"));

        var snapshot = sink.Snapshot();
        Assert.AreEqual(2, sink.Count);
        CollectionAssert.AreEqual(new[] { "uno", "dos" }, snapshot.Select(item => item.CorrelationId).ToList());
    }

    [TestMethod]
    public void Snapshot_EsUnaCopiaIndependiente()
    {
        var sink = new InMemoryDiagnosticsSink();
        sink.Write(CreateEvent("uno"));

        var snapshot = sink.Snapshot();
        sink.Clear();

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual(0, sink.Count);
    }

    [TestMethod]
    public void Write_Concurrente_NoPierdeEventos()
    {
        var sink = new InMemoryDiagnosticsSink();

        var tasks = Enumerable.Range(0, 8)
            .Select(worker => Task.Run(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    sink.Write(CreateEvent($"w{worker}-{i:D3}"));
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        Assert.AreEqual(800, sink.Count);
        Assert.AreEqual(800, sink.Snapshot().Select(item => item.CorrelationId).Distinct().Count());
    }

    [TestMethod]
    public void Flush_Y_EventoNulo_NoLanzan()
    {
        var sink = new InMemoryDiagnosticsSink();

        sink.Write(null!);
        sink.Flush();

        Assert.AreEqual(0, sink.Count);
    }

    private static DiagnosticsEvent CreateEvent(string correlationId) =>
        new(
            DateTimeOffset.UtcNow,
            DiagnosticsSeverity.Info,
            "SOPRO.Tests",
            "Operacion",
            "1.11.0",
            correlationId);
}
