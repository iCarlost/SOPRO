using System.Reflection;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    public static class ControlRenderHelper
    {
        public static void HabilitarDobleBuffer(Control control)
        {
            if (control == null) return;
            var prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(control, true, null);
        }
    }
}
