using System;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Controls
{
    public partial class SelectorApuEmbebidoControl : UserControl
    {
        private static readonly Color AccentColor = Color.FromArgb(31, 78, 121);

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
            ApplyCorporateToolbarStyle();
        }

        /// <summary>
        /// Unifica la toolbar del host con el sistema de diseño SOPRO: métricas de
        /// <see cref="SoproUiMetrics"/>, paleta corporativa e iconos vectoriales. Donde
        /// no existe un <see cref="SoproIconType"/> semántico se conserva solo texto.
        /// </summary>
        private void ApplyCorporateToolbarStyle()
        {
            int toolbarHeight = SoproUiMetrics.ContentButtonHeight + SoproUiMetrics.VerticalGap;
            pnlToolbar.Height = toolbarHeight;
            tsAcciones.Padding = new Padding(
                SoproUiMetrics.CompactButtonPadding.Left,
                SoproUiMetrics.VerticalGap,
                SoproUiMetrics.CompactButtonPadding.Right,
                SoproUiMetrics.VerticalGap);

            pnlToolbar.BackColor = Color.FromArgb(240, 244, 248);
            tsAcciones.BackColor = pnlToolbar.BackColor;
            lblTitulo.ForeColor = AccentColor;

            btnNuevaMatriz.Image = SoproIconProvider.GetIcon(
                SoproIconType.Matrices, AccentColor, SoproUiMetrics.RibbonGlyphButtonIconSize);
            btnNuevaMatriz.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            btnNuevaMatriz.TextImageRelation = TextImageRelation.ImageBeforeText;
            btnNuevaMatriz.ImageAlign = ContentAlignment.MiddleLeft;
            btnNuevaMatriz.TextAlign = ContentAlignment.MiddleCenter;
        }

        /// <summary>
        /// Sincroniza el estado habilitado de los comandos del host con la selección del
        /// grid embebido: Editar y Asignar requieren una matriz seleccionada.
        /// </summary>
        private void UpdateActionStates()
        {
            btnEditar.Enabled = _innerForm?.CanEditSelectedMatrix ?? false;
            btnAsignar.Enabled = _innerForm?.HasMatrixSelection ?? false;
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
            _innerForm.MatrixSelectionChanged += (_, __) => UpdateActionStates();
            pnlHost.Controls.Add(_innerForm);
            _innerForm.Show();
            _innerForm.BringToFront();
            _innerForm.Focus();
            UpdateActionStates();
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
