using System;
using System.Windows.Forms;
using SOPRO.WinForms.Forms;

namespace SOPRO.WinForms.Diagnostics
{
    /// <summary>
    /// Presentador por defecto del diálogo fatal: crea y muestra
    /// <see cref="FormDiagnosticoFatal"/> de forma modal sobre la ventana
    /// activa. No termina el proceso: eso sigue siendo responsabilidad del
    /// coordinador de diagnóstico.
    /// </summary>
    internal sealed class FatalErrorDialogPresenter : IFatalErrorPresenter
    {
        private readonly Action<string> _abrirCarpeta;

        public FatalErrorDialogPresenter()
            : this(null)
        {
        }

        public FatalErrorDialogPresenter(Action<string>? abrirCarpeta)
        {
            _abrirCarpeta = abrirCarpeta ?? FormDiagnosticoFatal.CrearAccionAbrirCarpetaPorDefecto();
        }

        public void Show(string correlationId, string logsDirectory)
        {
            using var dialogo = new FormDiagnosticoFatal(correlationId, logsDirectory, _abrirCarpeta);

            var propietario = ObtenerVentanaPropietaria();
            if (propietario != null)
                dialogo.ShowDialog(propietario);
            else
                dialogo.ShowDialog();
        }

        private static Form? ObtenerVentanaPropietaria()
        {
            Form? activa = Form.ActiveForm;
            if (activa != null && !activa.IsDisposed && activa.Visible)
                return activa;

            return null;
        }
    }
}
