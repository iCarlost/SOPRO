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
            TipoComponenteMatriz tipoComponente)
        {
            using var dialog = new FormSeleccionarInsumo(context, proyectoId, tipoComponente);
            return dialog.ShowDialog(owner) == DialogResult.OK
                ? dialog.ComponentesSeleccionados
                : new List<ComponenteMatriz>();
        }
    }
}
