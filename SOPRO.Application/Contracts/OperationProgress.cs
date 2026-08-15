namespace SOPRO.Application.Contracts;

/// <summary>
/// Progreso neutral de una operación larga. Los casos de uso que puedan bloquear
/// o tardar lo aceptan como <c>IProgress&lt;OperationProgress&gt;</c> (PLAN-01 §12).
/// </summary>
public sealed record OperationProgress(int Current, int Total, string? Message = null)
{
    public double Percentage => Total <= 0 ? 0d : (double)Current / Total;

    public static OperationProgress Start(string? message = null) => new(0, 0, message);

    public OperationProgress Advance(int step = 1, string? message = null)
        => new(Current + step, Total, message ?? Message);
}