using System;
using System.Text.Json;
using SOPRO.Application.Contracts;
using Sopro.Calculation.Labor;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Calcula el Factor de Salario Real (FSR) para un salario base dado,
    /// usando los parámetros almacenados en Proyecto.ParametrosFSR.
    /// Delega en <see cref="RealSalaryFactorCalculator"/> (motor puro, N7-13b).
    /// </summary>
    public static class FsrCalculationService
    {
        internal const string DiagnosticsSource = "SOPRO.Application";
        internal const string OperationCalcularFactorSalarioReal = "CalcularFactorSalarioReal";

        private const string DefaultProductVersion = "0.0.0";

        /// <summary>
        /// Calcula el FSR a partir de los parámetros del proyecto. Devuelve
        /// <c>null</c> cuando no hay parámetros válidos, el salario no es positivo
        /// o el cálculo falla. Un fallo real del cálculo se registra como evento
        /// <see cref="DiagnosticsSeverity.Error"/> de operación fija en el sink
        /// opcional; los retornos <c>null</c> previstos no se registran.
        /// </summary>
        public static decimal? Calcular(
            string? parametrosFSRJson,
            decimal salarioNominal,
            IDiagnosticsSink? diagnosticsSink = null,
            string? productVersion = null)
        {
            if (string.IsNullOrEmpty(parametrosFSRJson)) return null;
            if (salarioNominal <= 0) return null;

            try
            {
                var p = JsonSerializer.Deserialize<Dictionary<string, string>>(parametrosFSRJson);
                if (p == null) return null;

                var input = FsrInputAdapter.FromDictionary(p, salarioNominal);
                var result = RealSalaryFactorCalculator.Calculate(input);
                return result.Factor;
            }
            catch (Exception ex)
            {
                // Se conserva el retorno (null) para no romper a los llamadores.
                // El rastro es un evento fijo: nunca el JSON, el salario, el
                // Message, el StackTrace, rutas ni datos del trabajador.
                WriteFailureDiagnostics(diagnosticsSink, productVersion, ex);
                return null;
            }
        }

        /// <summary>
        /// Calcula el FSR del proyecto con la precisión persistida/editor (6
        /// decimales, <c>ManoDeObra.FactorSalarioReal</c>). Devuelve <c>false</c>
        /// cuando no hay parámetros válidos o el salario no es positivo, de modo
        /// que el llamador conserve el FSR de origen.
        /// </summary>
        public static bool TryCalcularFactorSalarioReal(
            string? parametrosFSRJson,
            decimal salarioBase,
            out decimal factorSalarioReal,
            IDiagnosticsSink? diagnosticsSink = null,
            string? productVersion = null)
        {
            factorSalarioReal = 0m;

            var calculado = Calcular(parametrosFSRJson, salarioBase, diagnosticsSink, productVersion);
            if (!calculado.HasValue)
                return false;

            factorSalarioReal = Math.Round(calculado.Value, 6);
            return true;
        }

        private static void WriteFailureDiagnostics(
            IDiagnosticsSink? diagnosticsSink,
            string? productVersion,
            Exception exception)
        {
            if (diagnosticsSink is null)
            {
                return;
            }

            var version = string.IsNullOrWhiteSpace(productVersion)
                ? DefaultProductVersion
                : productVersion;

            try
            {
                diagnosticsSink.Write(DiagnosticsEvent.FromException(
                    DateTimeOffset.UtcNow,
                    DiagnosticsSeverity.Error,
                    DiagnosticsSource,
                    OperationCalcularFactorSalarioReal,
                    version,
                    DiagnosticsCorrelationId.Create(null),
                    exception));
            }
            catch
            {
                // Best-effort: un sink defectuoso no altera el cálculo ni el retorno null.
            }

            try
            {
                diagnosticsSink.Flush();
            }
            catch
            {
                // El vaciado también es best-effort.
            }
        }
    }
}
