using SOPRO.Application.Contracts;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.WinForms.Services
{
    public static class ComponentSelectorDialogService
    {
        public static List<ComponenteMatriz> SelectComponents(
            IWin32Window owner,
            SOPROContext context,
            int proyectoId,
            TipoComponenteMatriz tipoComponente,
            IDiagnosticsSink? diagnosticsSink = null,
            string? productVersion = null)
        {
            using var dialog = new FormSeleccionarInsumo(
                context,
                proyectoId,
                tipoComponente,
                diagnosticsSink: diagnosticsSink,
                productVersion: productVersion);
            return dialog.ShowDialog(owner) == DialogResult.OK
                ? dialog.ComponentesSeleccionados
                : new List<ComponenteMatriz>();
        }
    }
}
