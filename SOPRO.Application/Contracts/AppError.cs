namespace SOPRO.Application.Contracts;

/// <summary>
/// Error tipado del resultado de un caso de uso. El mensaje está listo para
/// presentación al usuario.
/// </summary>
public sealed record AppError(AppErrorCode Code, string Message, string? Detail = null);