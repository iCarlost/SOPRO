namespace SOPRO.Application.Contracts;

/// <summary>
/// Resultado inmutable de un caso de uso: valor tipado o error tipado, nunca
/// ambos. Clase sellada sin estado por defecto (no existe una instancia
/// "inválida"): solo se crea con <see cref="Ok"/> o <see cref="Fail"/>.
/// <c>Ok(null)</c> está permitido cuando el tipo lo admite (p. ej.
/// <c>Result&lt;MaterialListItem?&gt;</c> de FindMaterialByKey: "no encontrado"
/// es un éxito con valor nulo, distinto de un error). Los consumidores deben
/// comprobar <see cref="IsSuccess"/> antes de leer <see cref="Value"/> o
/// <see cref="Error"/>.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public AppError? Error { get; }

    private Result(bool isSuccess, T? value, AppError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T? value) => new(true, value, null);

    public static Result<T> Fail(AppError error) => new(false, default, error);

    public static Result<T> Fail(AppErrorCode code, string message, string? detail = null)
        => new(false, default, new AppError(code, message, detail));

    public static implicit operator Result<T>(T value) => new(true, value, null);

    /// <summary>Ejecuta la rama de éxito o la de error según el estado.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<AppError, TResult> onError)
        => IsSuccess ? onSuccess(Value!) : onError(Error!);
}
