using System;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Sugiere una clave única basada en los primeros 8 caracteres de un GUID.
    /// El usuario puede reemplazarla libremente antes de guardar.
    /// </summary>
    public static class SugerenciaClave
    {
        public static string Generar() =>
            Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
    }
}
