using System;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Diagnostics
{
    /// <summary>
    /// Emisiones de diagnóstico para los resultados INCIERTOS de las operaciones
    /// atómicas de la UI. Las operaciones están fijadas: nunca se acepta texto libre,
    /// mensajes, rutas ni datos de negocio. La excepción, cuando existe, se reduce a
    /// sus datos estructurales con <see cref="DiagnosticsEvent.FromException"/>.
    /// Los fallos del sink son best-effort y jamás alteran la funcionalidad.
    /// </summary>
    internal static class OutcomeUnknownDiagnostics
    {
        internal const string Source = "SOPRO.WinForms";

        internal const string OperationProgramaObraResultadoIncierto = "ProgramaObraResultadoIncierto";
        internal const string OperationProgramaObraReconciliacionFallida = "ProgramaObraReconciliacionFallida";
        internal const string OperationPegadoResultadoIncierto = "PegadoResultadoIncierto";
        internal const string OperationPegadoReconciliacionFallida = "PegadoReconciliacionFallida";
        internal const string OperationFormatoCeldasResultadoIncierto = "FormatoCeldasResultadoIncierto";

        private const string VersionDesconocida = "0.0.0";

        internal static void ProgramaObraResultadoIncierto(
            IDiagnosticsSink? sink,
            string? productVersion,
            Exception? exception = null)
            => Emitir(sink, productVersion, OperationProgramaObraResultadoIncierto, exception);

        internal static void ProgramaObraReconciliacionFallida(
            IDiagnosticsSink? sink,
            string? productVersion,
            Exception? exception = null)
            => Emitir(sink, productVersion, OperationProgramaObraReconciliacionFallida, exception);

        internal static void PegadoResultadoIncierto(
            IDiagnosticsSink? sink,
            string? productVersion,
            Exception? exception = null)
            => Emitir(sink, productVersion, OperationPegadoResultadoIncierto, exception);

        internal static void PegadoReconciliacionFallida(
            IDiagnosticsSink? sink,
            string? productVersion,
            Exception? exception = null)
            => Emitir(sink, productVersion, OperationPegadoReconciliacionFallida, exception);

        internal static void FormatoCeldasResultadoIncierto(
            IDiagnosticsSink? sink,
            string? productVersion,
            Exception? exception = null)
            => Emitir(sink, productVersion, OperationFormatoCeldasResultadoIncierto, exception);

        private static void Emitir(
            IDiagnosticsSink? sink,
            string? productVersion,
            string operation,
            Exception? exception)
        {
            if (sink is null)
                return;

            var version = string.IsNullOrWhiteSpace(productVersion)
                ? VersionDesconocida
                : productVersion;
            var correlationId = DiagnosticsCorrelationId.Create(null);

            DiagnosticsEvent diagnosticsEvent;
            try
            {
                diagnosticsEvent = exception is null
                    ? new DiagnosticsEvent(
                        DateTimeOffset.UtcNow,
                        DiagnosticsSeverity.Error,
                        Source,
                        operation,
                        version,
                        correlationId)
                    : DiagnosticsEvent.FromException(
                        DateTimeOffset.UtcNow,
                        DiagnosticsSeverity.Error,
                        Source,
                        operation,
                        version,
                        correlationId,
                        exception);
            }
            catch
            {
                return;
            }

            try
            {
                sink.Write(diagnosticsEvent);
            }
            catch
            {
                // El sink es best-effort: nunca altera la operación de la UI.
            }

            try
            {
                sink.Flush();
            }
            catch
            {
                // El vaciado tampoco puede afectar la funcionalidad.
            }
        }
    }
}
