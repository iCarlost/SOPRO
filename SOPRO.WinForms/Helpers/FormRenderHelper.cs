using System.Reflection;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    internal static class FormRenderHelper
    {
        public static void OptimizeForGridRendering(Form form)
        {
            if (form == null) return;
            SetDoubleBuffered(form);
        }

        public static void OptimizarRender(Form form)
        {
            if (form == null) return;
            SetDoubleBuffered(form);
        }

        private static void SetDoubleBuffered(Control control)
        {
            if (control == null) return;

            var prop = typeof(Control).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop?.SetValue(control, true, null);
        }
    }
}
