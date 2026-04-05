using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Controls
{
    internal class BufferedChartPanel : Panel
    {
        public BufferedChartPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            UpdateStyles();
            BackColor = Color.White;
            Margin = Padding.Empty;
        }
    }
}
