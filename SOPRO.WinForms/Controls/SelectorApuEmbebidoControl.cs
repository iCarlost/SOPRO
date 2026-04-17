using System;
using System.Windows.Forms;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.WinForms.Controls
{
    public partial class SelectorApuEmbebidoControl : UserControl
    {
        private FormSeleccionarAPU? _innerForm;

        public event EventHandler? Accepted;
        public event EventHandler? Cancelled;
        public event EventHandler? RequestNewMatrix;
        public event EventHandler? RequestEditMatrix;

        public Matriz? MatrizSeleccionada => _innerForm?.MatrizSeleccionada;
        public decimal Cantidad => _innerForm?.Cantidad ?? 1m;
        public TipoMatriz SelectedTipo => _innerForm?.WorkspaceSelectedTipo ?? TipoMatriz.APU;
        public int? SelectedMatrixId => _innerForm?.WorkspaceSelectedMatrixId;

        public SelectorApuEmbebidoControl()
        {
            InitializeComponent();
        }

        public void InitializeSelector(SOPROContext context, int proyectoId, decimal cantidadActual, int? matrizIdActual, string? filtroInicial)
        {
            ClearInner();
            _innerForm = new FormSeleccionarAPU(context, proyectoId, cantidadActual, true, matrizIdActual, filtroInicial);
            _innerForm.ConfigureForWorkspaceContentHost();
            _innerForm.EmbeddedAccepted += (_, __) => Accepted?.Invoke(this, EventArgs.Empty);
            _innerForm.EmbeddedCancelled += (_, __) => Cancelled?.Invoke(this, EventArgs.Empty);
            _innerForm.EmbeddedRequestNewMatrix += (_, __) => RequestNewMatrix?.Invoke(this, EventArgs.Empty);
            _innerForm.EmbeddedRequestEditMatrix += (_, __) => RequestEditMatrix?.Invoke(this, EventArgs.Empty);
            pnlHost.Controls.Add(_innerForm);
            _innerForm.Show();
            _innerForm.BringToFront();
            _innerForm.Focus();
        }

        public void ClearInner()
        {
            if (_innerForm == null) return;
            try
            {
                pnlHost.Controls.Remove(_innerForm);
                _innerForm.Dispose();
            }
            catch { }
            _innerForm = null;
        }

        public void TriggerAccept() => _innerForm?.TriggerAcceptSelection();
        public void TriggerCancel() => _innerForm?.TriggerCancelSelection();
        public void TriggerNuevaMatriz() => _innerForm?.TriggerNuevaMatriz();
        public void TriggerEditarMatriz() => _innerForm?.TriggerEditarMatriz();

        private void btnAsignar_Click(object sender, EventArgs e) => TriggerAccept();
        private void btnVolver_Click(object sender, EventArgs e) => TriggerCancel();
        private void btnNuevaMatriz_Click(object sender, EventArgs e) => RequestNewMatrix?.Invoke(this, EventArgs.Empty);
        private void btnEditar_Click(object sender, EventArgs e) => RequestEditMatrix?.Invoke(this, EventArgs.Empty);
    }
}
