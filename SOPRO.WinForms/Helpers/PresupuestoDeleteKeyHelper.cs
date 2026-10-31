using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Guarda de teclado para la eliminación de filas del presupuesto. Supr (y su
    /// alias Ctrl+Supr) despacha el borrado solo cuando el grid no está editando
    /// una celda ni el panel embebido tiene el foco en una entrada de texto; en
    /// esos casos la tecla debe llegar al control de edición para no perder datos.
    /// </summary>
    internal static class PresupuestoDeleteKeyHelper
    {
        public static bool DebeDespacharSupr(
            Keys keyCode,
            bool shift,
            bool alt,
            bool enEdicionCelda,
            bool focoEnEntradaTextoPanel)
            => keyCode == Keys.Delete
               && !shift
               && !alt
               && !enEdicionCelda
               && !focoEnEntradaTextoPanel;
    }
}
