using SOPRO.Application.Contracts;

namespace SOPRO.Application.Services;

/// <summary>
/// Sink nulo para defaults y composición sin diagnóstico: descarta los eventos
/// y no acumula estado. Al no tener estado es trivialmente thread-safe.
/// </summary>
public sealed class NullDiagnosticsSink : IDiagnosticsSink
{
    public void Write(DiagnosticsEvent diagnosticsEvent)
    {
    }

    public void Flush()
    {
    }
}
