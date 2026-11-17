using SOPRO.Application.Contracts;

namespace SOPRO.Application.Services;

/// <summary>
/// Sink en memoria para pruebas y composición sin E/S. Es thread-safe y no
/// sanitiza: la sanitización pertenece a la frontera de persistencia.
/// </summary>
public sealed class InMemoryDiagnosticsSink : IDiagnosticsSink
{
    private readonly object _gate = new();
    private readonly List<DiagnosticsEvent> _events = new();

    public void Write(DiagnosticsEvent diagnosticsEvent)
    {
        if (diagnosticsEvent is null)
        {
            return;
        }

        lock (_gate)
        {
            _events.Add(diagnosticsEvent);
        }
    }

    public void Flush()
    {
    }

    /// <summary>Copia inmutable de los eventos registrados hasta el momento.</summary>
    public IReadOnlyList<DiagnosticsEvent> Snapshot()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }
}
