using SOPRO.Application.Services;
using SOPRO.WinForms.Forms;
using System;
using System.Windows.Forms;

namespace SOPRO.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            var workspaceService = new ProjectWorkspaceService();
            workspaceService.EnsureWorkspaceExists();

            // Fase 2.2: apertura por shell / doble clic del archivo de proyecto.
            // Sin argumentos válidos el arranque conserva el comportamiento actual.
            CommandLineProjectPathParser.TryParse(args, out var projectPath);

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                // Arranque directo a un proyecto: se abre ANTES de mostrar
                // cualquier ventana, para que el selector (FormPrincipal) no se
                // pinte. Si la apertura falla, se cae al selector con el error.
                var principal = new FormPrincipal(
                    workspaceService,
                    new ProjectLifecycleService(workspaceService));
                var projectForm = principal.TryOpenProject(projectPath);

                if (projectForm != null)
                {
                    // La comprobación de actualizaciones se lanza ya dentro del
                    // bucle de mensajes (hilo de UI), igual que en el arranque
                    // normal desde el selector.
                    projectForm.BeginInvoke((Action)principal.StartStartupUpdateCheck);
                    System.Windows.Forms.Application.Run(new ProjectStartupContext(principal, projectForm));
                    return;
                }

                System.Windows.Forms.Application.Run(principal);
                return;
            }

            System.Windows.Forms.Application.Run(new FormPrincipal(
                workspaceService,
                new ProjectLifecycleService(workspaceService),
                projectPath));
        }
    }
}
