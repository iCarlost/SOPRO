using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Guarda de teclado para la eliminación múltiple del catálogo de matrices.
    /// Supr y su alias Ctrl+Supr despachan el mismo comando; <paramref name="control"/>
    /// no cambia la decisión. Se ignoran Shift/Alt y no se despacha mientras la celda
    /// está en edición ni cuando no hay una matriz objetivo (selección real o la
    /// selección vigente del formulario).
    /// </summary>
    internal static class MatricesDeleteKeyHelper
    {
        public static bool DebeDespacharSupr(
            Keys keyCode,
            bool control,
            bool shift,
            bool alt,
            bool enEdicionCelda,
            bool tieneObjetivo)
            => keyCode == Keys.Delete
               && !shift
               && !alt
               && !enEdicionCelda
               && tieneObjetivo;
    }
}
