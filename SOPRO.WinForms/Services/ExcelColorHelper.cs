using ClosedXML.Excel;
using System.Drawing;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Helper para crear XLColor de forma segura.
    /// XLColor.FromHtml() solo acepta colores en formato hex (#RRGGBB).
    /// Si se le pasa un nombre en inglés ("White", "Black", etc.) lanza excepción.
    /// SafeFromHtml() normaliza cualquier formato que acepte System.Drawing a hex primero.
    /// </summary>
    internal static class ExcelColorHelper
    {
        /// <summary>
        /// Crea un XLColor de forma segura desde cualquier string de color:
        /// "#FFFFFF", "White", "rgb(255,255,255)", etc.
        /// Si el valor no es parseable, usa el color de fallback.
        /// </summary>
        public static XLColor SafeFromHtml(string? valor, string fallbackHex = "#FFFFFF")
        {
            if (string.IsNullOrWhiteSpace(valor))
                return FromHex(fallbackHex);

            // Intentar normalizar a hex usando ColorTranslator (acepta nombres, hex, etc.)
            try
            {
                var c = ColorTranslator.FromHtml(valor.Trim());
                // Usar FromArgb explícito para evitar que ClosedXML intente parsear el nombre
                return XLColor.FromArgb(c.A, c.R, c.G, c.B);
            }
            catch
            {
                return FromHex(fallbackHex);
            }
        }

        private static XLColor FromHex(string hex)
        {
            try
            {
                var c = ColorTranslator.FromHtml(hex);
                return XLColor.FromArgb(c.A, c.R, c.G, c.B);
            }
            catch
            {
                return XLColor.FromArgb(255, 255, 255, 255); // blanco como último recurso
            }
        }
    }
}
