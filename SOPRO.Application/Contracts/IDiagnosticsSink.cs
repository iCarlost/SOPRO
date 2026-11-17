namespace SOPRO.Application.Contracts;

/// <summary>
/// Destino de los eventos de diagnóstico. Las implementaciones no deben propagar
/// errores propios ni bloquear la interfaz de usuario.
/// </summary>
public interface IDiagnosticsSink
{
    /// <summary>Registra un evento. Es best-effort: nunca lanza por fallos de E/S.</summary>
    void Write(DiagnosticsEvent diagnosticsEvent);

    /// <summary>Fuerza el vaciado de lo pendiente. Puede ser una operación vacía si el sink ya vacía por escritura.</summary>
    void Flush();
}
