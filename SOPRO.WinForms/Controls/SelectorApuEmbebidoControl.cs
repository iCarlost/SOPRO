using System;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.Core.Entities;
using SOPRO.Application.Services;
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

        /// <summary>
        /// Cantidad visible en el selector en este instante. El host debe leerla ANTES
        /// de mostrar el panel de matrices, porque <c>MostrarPanelMatrizEmbebido</c>
        /// dispone el selector y perdería la edición del usuario.
        /// </summary>
        internal decimal CantidadActualVisible => _innerForm?.CantidadActualVisible ?? 1m;

        /// <summary>
        /// Cantidad positiva para arrastrar a una matriz nueva: la visible si es válida
        /// o el mínimo del control (nunca cero). Sin selector se usa 1.0.
        /// </summary>
        internal decimal CantidadParaNuevaMatriz
        {
            get
            {
                if (_innerForm is null)
                    return 1m;

                decimal cantidad = _innerForm.CantidadActualVisible;
                return cantidad > 0m ? cantidad : _innerForm.CantidadMinimaVisible;
            }
        }

        public SelectorApuEmbebidoControl()
        {
            InitializeComponent();
            ApplyCorporateToolbarStyle();
            // Estado inicial coherente: sin selección la Edición queda bloqueada; solo
            // UpdateActionStates (selección activa) la habilita.
            btnEditar.Enabled = false;
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

            // Jerarquía de acciones: Nueva Matriz es el comando primario (negrita y acento
            // corporativo, siempre sobre la barra); Editar queda secundario y depende de la
            // selección. Nada se colapsa al overflow: ese chevron diminuto era el único
            // acceso a Nueva Matriz/Editar/Volver en hosts angostos.
            btnNuevaMatriz.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnNuevaMatriz.ForeColor = AccentColor;
            btnNuevaMatriz.Padding = new Padding(0, 0, 6, 0);
            btnEditar.Padding = new Padding(2, 0, 6, 0);
            btnVolver.Padding = new Padding(2, 0, 0, 0);
        }

        /// <summary>Accesos de prueba del ensamblado (SOPRO.Tests.WinForms).</summary>
        internal ToolStripItem BtnNuevaMatrizItem => btnNuevaMatriz;
        internal ToolStripItem BtnEditarItem => btnEditar;
        internal ToolStripItem BtnVolverItem => btnVolver;
        internal ToolStrip StripAcciones => tsAcciones;

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
            => InitializeSelector(context, proyectoId, cantidadActual, matrizIdActual, filtroInicial, workspace: null);

        public void InitializeSelector(
            SOPROContext context,
            int proyectoId,
            decimal cantidadActual,
            int? matrizIdActual,
            string? filtroInicial,
            ProjectWorkspaceService? workspace)
        {
            ClearInner();
            _innerForm = new FormSeleccionarAPU(context, proyectoId, cantidadActual, true, matrizIdActual, filtroInicial, workspace);
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
