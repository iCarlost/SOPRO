using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Controls
{
    public class PresupuestoDataGridView : DataGridView
    {
        public Func<Keys, bool>? InterceptarTeclaEspecial { get; set; }

        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            if (InterceptarTeclaEspecial?.Invoke(e.KeyData) == true)
                return true;

            return base.ProcessDataGridViewKey(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (InterceptarTeclaEspecial?.Invoke(keyData) == true)
                return true;

            return base.ProcessDialogKey(keyData);
        }
    }
}
