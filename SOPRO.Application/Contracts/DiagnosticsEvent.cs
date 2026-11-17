using System.Collections.ObjectModel;

namespace SOPRO.Application.Contracts;

/// <summary>
/// Evento de diagnóstico inmutable con campos fijos. El payload es deliberadamente
/// conservador: solo expone el tipo, el <see cref="Exception.HResult"/> y la cadena
/// de tipos de las excepciones internas. No existe campo de metadatos libres y nunca
/// contiene <see cref="Exception.Message"/>, <see cref="Exception.StackTrace"/> ni
/// <c>Exception.ToString()</c>, porque ese texto puede arrastrar rutas, nombres de
/// proyecto y datos de usuario.
/// </summary>
public sealed class DiagnosticsEvent
{
    private const int MaxInnerExceptionTypes = 8;

    private readonly ReadOnlyCollection<string> _innerExceptionTypes;

    public DiagnosticsEvent(
        DateTimeOffset timestampUtc,
        DiagnosticsSeverity severity,
        string source,
        string operation,
        string productVersion,
        string correlationId,
        string? schemaVersion = null,
        string? exceptionType = null,
        int? hResult = null,
        IEnumerable<string>? innerExceptionTypes = null,
        string? schemaStage = null)
    {
        TimestampUtc = timestampUtc.ToUniversalTime();
        Severity = severity;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        ProductVersion = productVersion ?? throw new ArgumentNullException(nameof(productVersion));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        SchemaVersion = schemaVersion;
        ExceptionType = exceptionType;
        HResult = hResult;
        SchemaStage = schemaStage;
        _innerExceptionTypes = new ReadOnlyCollection<string>(
            innerExceptionTypes?.ToArray() ?? Array.Empty<string>());
    }

    /// <summary>Instante del evento, normalizado a UTC.</summary>
    public DateTimeOffset TimestampUtc { get; }

    public DiagnosticsSeverity Severity { get; }

    /// <summary>Componente u origen lógico, por ejemplo <c>SOPRO.WinForms</c>.</summary>
    public string Source { get; }

    /// <summary>Operación en curso, por ejemplo <c>AbrirProyecto</c>.</summary>
    public string Operation { get; }

    public string ProductVersion { get; }

    public string? SchemaVersion { get; }

    /// <summary>
    /// Etapa fija del esquema en la que falló la operación (<c>M001</c>…<c>M015</c>,
    /// reparación o sello), cuando se conoce. No admite texto libre del usuario.
    /// </summary>
    public string? SchemaStage { get; }

    /// <summary>Identificador de correlación para enlazar eventos de una misma operación.</summary>
    public string CorrelationId { get; }

    /// <summary>Nombre completo del tipo de la excepción, sin mensaje ni traza.</summary>
    public string? ExceptionType { get; }

    public int? HResult { get; }

    /// <summary>Tipos de las excepciones internas, sin mensajes.</summary>
    public IReadOnlyList<string> InnerExceptionTypes => _innerExceptionTypes;

    /// <summary>
    /// Construye un evento a partir de una excepción extrayendo únicamente datos
    /// estructurales (tipo, HResult y cadena de tipos internos).
    /// </summary>
    public static DiagnosticsEvent FromException(
        DateTimeOffset timestampUtc,
        DiagnosticsSeverity severity,
        string source,
        string operation,
        string productVersion,
        string correlationId,
        Exception exception,
        string? schemaVersion = null,
        string? schemaStage = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var innerTypes = new List<string>();
        CollectInnerExceptionTypes(exception, innerTypes);

        return new DiagnosticsEvent(
            timestampUtc,
            severity,
            source,
            operation,
            productVersion,
            correlationId,
            schemaVersion,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.HResult,
            innerTypes,
            schemaStage);
    }

    private static void CollectInnerExceptionTypes(Exception exception, List<string> destination)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                if (destination.Count >= MaxInnerExceptionTypes)
                {
                    return;
                }

                destination.Add(inner.GetType().FullName ?? inner.GetType().Name);
            }

            return;
        }

        var current = exception.InnerException;
        while (current is not null && destination.Count < MaxInnerExceptionTypes)
        {
            destination.Add(current.GetType().FullName ?? current.GetType().Name);
            current = current.InnerException;
        }
    }
}
