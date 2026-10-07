using System;
using System.Windows.Forms;
using SOPRO.WinForms.Forms;

namespace SOPRO.WinForms
{
    /// <summary>
    /// Contexto de aplicación para el arranque directo a un proyecto (shell /
    /// doble clic). Arranca con el formulario del proyecto como ventana
    /// principal, de modo que el selector (<see cref="FormPrincipal"/>) no se
    /// pinte al iniciar. Al cerrar el proyecto se vuelve al selector —
    /// comportamiento actual — y la aplicación termina cuando se cierra el
    /// selector, evitando que el proceso se cierre de forma prematura.
    /// </summary>
    internal sealed class ProjectStartupContext : ApplicationContext
    {
        private readonly FormPrincipal _principal;
        private readonly FormProyecto _projectForm;

        public ProjectStartupContext(FormPrincipal principal, FormProyecto projectForm)
        {
            _principal = principal ?? throw new ArgumentNullException(nameof(principal));
            _projectForm = projectForm ?? throw new ArgumentNullException(nameof(projectForm));

            // MainForm se hace visible al entrar en el bucle de mensajes; debe ser
            // el proyecto (nunca el selector) para que no haya parpadeo.
            MainForm = _projectForm;
        }

        protected override void OnMainFormClosed(object? sender, EventArgs e)
        {
            // Al cerrar el proyecto se vuelve al selector (comportamiento actual);
            // la aplicación solo termina cuando se cierra el selector.
            if (ReferenceEquals(sender, _projectForm))
            {
                _principal.Show();
                MainForm = _principal;
                return;
            }

            base.OnMainFormClosed(sender, e);
        }
    }
}
