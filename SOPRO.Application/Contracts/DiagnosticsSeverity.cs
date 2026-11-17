namespace SOPRO.Application.Contracts;

/// <summary>
/// Severidad de un evento de diagnóstico, ordenada de menor a mayor gravedad.
/// </summary>
public enum DiagnosticsSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Fatal = 3
}
