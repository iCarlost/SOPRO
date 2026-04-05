using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.WinForms.Services
{
    public static class ApuSelectorDialogService
    {
        public static (bool Accepted, Matriz? Matriz, decimal Cantidad) SelectApu(
            IWin32Window owner,
            SOPROContext context,
            int proyectoId,
            decimal cantidadInicial,
            string? filtroInicial = null)
        {
            using var dialog = new FormSeleccionarAPU(context, proyectoId, cantidadInicial, false, null, filtroInicial);
            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return (false, null, cantidadInicial);

            return (dialog.MatrizSeleccionada != null, dialog.MatrizSeleccionada, dialog.Cantidad);
        }


        public static (bool Accepted, Matriz? Matriz, decimal Cantidad) SelectMatrix(
            IWin32Window owner,
            SOPROContext context,
            int proyectoId,
            decimal cantidadInicial,
            bool includeAuxiliaries,
            int? matrizIdPreseleccionada = null,
            string? filtroInicial = null)
        {
            using var dialog = new FormSeleccionarAPU(context, proyectoId, cantidadInicial, includeAuxiliaries, matrizIdPreseleccionada, filtroInicial);
            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return (false, null, cantidadInicial);

            return (dialog.MatrizSeleccionada != null, dialog.MatrizSeleccionada, dialog.Cantidad);
        }
    }
}
