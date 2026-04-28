using SOPRO.Application.Services;
using SOPRO.WinForms.Forms;
using System;
using System.Windows.Forms;

namespace SOPRO.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            var workspaceService = new ProjectWorkspaceService();
            workspaceService.EnsureWorkspaceExists();


            System.Windows.Forms.Application.Run(new FormPrincipal(
                workspaceService,
                new ProjectLifecycleService(workspaceService)));
        }
    }
}
