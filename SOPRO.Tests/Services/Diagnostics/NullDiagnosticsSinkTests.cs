using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Diagnostics;

[TestClass]
public sealed class NullDiagnosticsSinkTests
{
    [TestMethod]
    public void Write_Y_Flush_DescartanSinLanzarNiAcumular()
    {
        var sink = new NullDiagnosticsSink();

        sink.Write(new DiagnosticsEvent(
            DateTimeOffset.UtcNow,
            DiagnosticsSeverity.Info,
            "SOPRO.Tests",
            "Operacion",
            "1.19.2.0",
            "ABCD2345"));
        sink.Write(null!);
        sink.Flush();
    }
}
