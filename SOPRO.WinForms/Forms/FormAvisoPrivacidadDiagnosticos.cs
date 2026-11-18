using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    internal sealed class FormAvisoPrivacidadDiagnosticos : Form
    {
        public bool Revisar { get; private set; }
        public bool Reconocido { get; private set; }
        public FormAvisoPrivacidadDiagnosticos()
        {
            Text = "Registros locales"; StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = true; ClientSize = new Size(540, 250);
            var copy = new Label { Dock = DockStyle.Fill, Padding = new Padding(22), Text = "SOPRO guarda registros de diagnóstico locales en %LOCALAPPDATA%\\SOPRO\\logs. Cada archivo tiene un límite aproximado de 1 MiB; se conservan como máximo 10 archivos durante 7 días (retención de mejor esfuerzo). Puedes revisarlos, exportarlos o borrarlos manualmente desde Registros locales. No se suben automáticamente.", AutoSize = false, AccessibleName = "Información sobre registros locales" };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            var understood = new Button { Text = "Entendido", AutoSize = true, DialogResult = DialogResult.OK };
            var review = new Button { Text = "Revisar registros", AutoSize = true };
            understood.Click += (_, _) => Reconocido = true;
            review.Click += (_, _) => { Reconocido = true; Revisar = true; DialogResult = DialogResult.OK; Close(); };
            buttons.Controls.Add(understood); buttons.Controls.Add(review); Controls.Add(copy); Controls.Add(buttons); AcceptButton = understood; CancelButton = null;
        }
    }
}
