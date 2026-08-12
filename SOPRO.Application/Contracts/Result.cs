namespace SOPRO.Application.Contracts;

/// <summary>
/// Resultado inmutable de un caso de uso: valor tipado o error tipado, nunca ambos.
/// </summary>
public readonly struct Result<T>
{
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }
    public T? Value { get; }
    public AppError? Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(AppError error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(value);

    public static Result<T> Fail(AppError error) => new(error);

    public static Result<T> Fail(AppErrorCode code, string message, string? detail = null)
        => new(new AppError(code, message, detail));

    public static implicit operator Result<T>(T value) => new(value);

    /// <summary>Ejecuta la rama de éxito o la de error según el estado.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<AppError, TResult> onError)
        => IsSuccess ? onSuccess(Value!) : onError(Error!);
}