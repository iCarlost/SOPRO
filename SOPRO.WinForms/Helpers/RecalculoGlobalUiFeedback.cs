using System;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Compone los mensajes del recálculo global separando el fallo de cálculo del
    /// fallo de refresco posterior. Cuando el refresco falla, los datos ya fueron
    /// guardados por <c>RecalculoGlobalService</c>: el mensaje nunca debe sugerir
    /// una reversión. El fallo de cálculo tampoco afirma nada sobre el estado de
    /// los datos, porque el servicio guarda por fases.
    /// </summary>
    internal static class RecalculoGlobalUiFeedback
    {
        internal const string TituloFalloCalculo = "Error";
        internal const string TituloFalloRefresco = "Recálculo completado con avisos";
        internal const string TituloFalloInesperado = "Error";

        internal static string MensajeFalloCalculo(string? detalle)
        {
            string motivo = string.IsNullOrWhiteSpace(detalle)
                ? "No se pudo completar el recálculo."
                : detalle.Trim();

            return $"Error en el recálculo global:\n{motivo}";
        }

        internal static string MensajeFalloRefresco(int pestanasFallidas, Exception? error)
        {
            string mensaje =
                "El recálculo se completó y los datos se guardaron, pero la vista no pudo actualizarse por completo.";

            if (pestanasFallidas > 0)
                mensaje += $"\nPestañas que no se pudieron refrescar: {pestanasFallidas}.";

            if (!string.IsNullOrWhiteSpace(error?.Message))
                mensaje += $"\nDetalle: {error!.Message.Trim()}";

            return mensaje +
                "\nVuelve a intentar el refresco o reabre el proyecto para ver los datos actualizados.";
        }

        internal static string MensajeFalloInesperado(string? detalle)
        {
            string motivo = string.IsNullOrWhiteSpace(detalle)
                ? "La operación se interrumpió de forma inesperada."
                : $"La operación se interrumpió de forma inesperada: {detalle.Trim()}";

            return motivo +
                "\n\nSi el recálculo ya había terminado, los datos guardados no se revierten.";
        }
    }
}
