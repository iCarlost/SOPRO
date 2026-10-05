using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;
using System.Reflection;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Host embebido (workspace de presupuesto) y layout del selector.
    ///
    /// El modo independiente (diálogo vía <c>ApuSelectorDialogService</c>) conserva el layout
    /// absoluto declarado en el Designer y su <see cref="Form.MinimumSize"/>.
    ///
    /// El modo embebido usa un contenedor declarativo (<see cref="TableLayoutPanel"/>) con filas
    /// AutoSize para los extremos (búsqueda/filtros y resumen) y una fila porcentual para el grid.
    /// No se usan pisos de altura fijos ni coordenadas calculadas a mano, por lo que el grid nunca
    /// invade el resumen ni sobresale del área cliente aun con el host de ~165-172px de alto.
    /// </summary>
    public partial class FormSeleccionarAPU
    {
        // ── Estado de ventana previo al modo embebido ───────────────────────
        // Se guarda para poder restaurar el comportamiento del diálogo independiente.
        private Size _standaloneMinimumSize;
        private Size _standaloneMaximumSize;
        private FormWindowState _standaloneWindowState;
        private bool _standaloneWindowStateSaved;

        // ── Contenedor declarativo del modo embebido ────────────────────────
        private TableLayoutPanel? _embeddedRoot;
        private TableLayoutPanel? _embeddedFilters;
        private TableLayoutPanel? _embeddedActions;
        private FlowLayoutPanel? _embeddedInfo;
        private bool _embeddedLayoutBuilt;

        // Altura por debajo de la cual el resumen se muestra en variante compacta.
        private const int CompactInfoHeightThreshold = 210;

        // AutoScroll como último recurso para alturas extraordinariamente pequeñas.
        private const int AutoScrollFallbackHeight = 120;
        private const int AutoScrollContentHeight = 150;

        public void ConfigureForEmbeddedHost()
        {
            SaveStandaloneWindowState();

            EmbeddedMode = true;
            TopLevel = false;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Dock = DockStyle.Fill;

            // Sin restricción de tamaño: el host embebido real mide ~165-172px de alto.
            MinimumSize = Size.Empty;

            BuildEmbeddedLayout();

            Resize -= FormSeleccionarAPU_EmbeddedResize;
            Resize += FormSeleccionarAPU_EmbeddedResize;
            ApplyEmbeddedHostLayout();
        }

        public void ConfigureForWorkspaceContentHost()
        {
            ConfigureForEmbeddedHost();
            WorkspaceChromeHidden = true;
            ApplyEmbeddedHostLayout();
        }

        private void FormSeleccionarAPU_EmbeddedResize(object? sender, EventArgs e) => ApplyEmbeddedHostLayout();

        public void TriggerAcceptSelection() => btnAceptar_Click(this, EventArgs.Empty);
        public void TriggerCancelSelection() => btnCancelar_Click(this, EventArgs.Empty);
        public void TriggerNuevaMatriz() => btnNuevaMatriz_Click(this, EventArgs.Empty);
        public void TriggerEditarMatriz() => btnEditarMatriz_Click(this, EventArgs.Empty);
        public TipoMatriz WorkspaceSelectedTipo => GetSelectedTipoFiltro() ?? TipoMatriz.APU;
        public int? WorkspaceSelectedMatrixId => dgvMatrices.SelectedRows.Count == 0 ? null : Convert.ToInt32(dgvMatrices.SelectedRows[0].Cells["colId"].Value);
        public bool HasMatrixSelection => dgvMatrices.SelectedRows.Count > 0;
        public bool CanEditSelectedMatrix =>
            dgvMatrices.SelectedRows.Count > 0
            && dgvMatrices.SelectedRows[0].Tag is MatrixListItemDto;

        // ════════════════════════════════════════════════════════════════════
        // Estado de ventana
        // ════════════════════════════════════════════════════════════════════

        private void SaveStandaloneWindowState()
        {
            if (_standaloneWindowStateSaved) return;

            _standaloneMinimumSize  = MinimumSize;
            _standaloneMaximumSize  = MaximumSize;
            _standaloneWindowState  = WindowState;
            _standaloneWindowStateSaved = true;
        }

        /// <summary>
        /// Restaura el estado de ventana del modo independiente. Queda disponible para una
        /// eventual salida del modo embebido; el ciclo de vida embebido normal no la invoca.
        /// </summary>
        private void RestoreStandaloneWindowState()
        {
            if (!_standaloneWindowStateSaved) return;

            MinimumSize = _standaloneMinimumSize;
            MaximumSize = _standaloneMaximumSize;
            WindowState = _standaloneWindowState;
        }

        // ════════════════════════════════════════════════════════════════════
        // Construcción del contenedor declarativo (una sola vez)
        // ════════════════════════════════════════════════════════════════════

        private void BuildEmbeddedLayout()
        {
            if (_embeddedLayoutBuilt) return;
            _embeddedLayoutBuilt = true;

            SuspendLayout();

            // ── Fila superior: búsqueda + filtros (AutoSize) ────────────────
            _embeddedFilters = new TableLayoutPanel
            {
                Name = "_embeddedFilters",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                RowCount = 1,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, SoproUiMetrics.VerticalGap),
                BackColor = Color.Transparent,
                TabIndex = 0
            };
            _embeddedFilters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // etiqueta "Buscar"
            _embeddedFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // caja de texto (absorbe el ancho)
            _embeddedFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));  // tipo
            _embeddedFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 226F)); // proyecto/origen
            _embeddedFilters.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lblBuscar.AutoSize = true;
            lblBuscar.Dock = DockStyle.None;
            lblBuscar.Anchor = AnchorStyles.Left;
            lblBuscar.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblBuscar.Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0);

            txtBuscar.Dock = DockStyle.None;
            txtBuscar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtBuscar.Margin = new Padding(0, 0, 4, 0);

            _cboTipo.Dock = DockStyle.None;
            _cboTipo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _cboTipo.Margin = new Padding(0, 0, 4, 0);
            _cboTipo.Width = 92;

            _cboProyecto.Dock = DockStyle.None;
            _cboProyecto.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _cboProyecto.Margin = new Padding(0);
            _cboProyecto.Width = 226;

            _embeddedFilters.Controls.Add(lblBuscar, 0, 0);
            _embeddedFilters.Controls.Add(txtBuscar, 1, 0);
            _embeddedFilters.Controls.Add(_cboTipo, 2, 0);
            _embeddedFilters.Controls.Add(_cboProyecto, 3, 0);

            // ── Acciones internas (contenedor legado); invisibles en modo embebido ──
            _embeddedActions = new TableLayoutPanel
            {
                Name = "_embeddedActions",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 5,
                RowCount = 1,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, SoproUiMetrics.VerticalGap, 0, 0),
                BackColor = Color.Transparent,
                TabIndex = 3
            };
            _embeddedActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Nueva Matriz
            _embeddedActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Editar
            _embeddedActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // separador
            _embeddedActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Volver/Cancelar
            _embeddedActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Aceptar
            _embeddedActions.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            foreach (var boton in new[] { btnNuevaMatriz, btnEditarMatriz, btnCancelar, btnAceptar })
            {
                boton.AutoSize = true;
                boton.Dock = DockStyle.None;
                boton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                boton.Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0);
            }

            _embeddedActions.Controls.Add(btnNuevaMatriz, 0, 0);
            _embeddedActions.Controls.Add(btnEditarMatriz, 1, 0);
            _embeddedActions.Controls.Add(btnCancelar, 3, 0);
            _embeddedActions.Controls.Add(btnAceptar, 4, 0);

            // ── panelInfo: resumen en contenedor declarativo de flujo ────────
            // El resumen (Cantidad/Unidad/Costo/Importe) ya no se posiciona con
            // Control.Location ni con un ancho mínimo lógico: un FlowLayoutPanel con
            // Dock/AutoSize y WrapContents lo organiza y lo envuelve según el ancho real,
            // evitando desbordes en hosts estrechos.
            panelInfo.AutoSize = true;
            panelInfo.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelInfo.Dock = DockStyle.None;
            panelInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            panelInfo.Margin = new Padding(0, SoproUiMetrics.VerticalGap, 0, SoproUiMetrics.VerticalGap);
            panelInfo.BorderStyle = BorderStyle.None;
            panelInfo.BackColor = Color.FromArgb(250, 250, 250);

            _embeddedInfo = new FlowLayoutPanel
            {
                Name = "_embeddedInfo",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                TabIndex = 0
            };

            _embeddedInfo.Controls.Add(lblCantidadLabel);
            _embeddedInfo.Controls.Add(nudCantidad);
            _embeddedInfo.Controls.Add(lblUnidadLabel);
            _embeddedInfo.Controls.Add(lblUnidad);
            _embeddedInfo.Controls.Add(lblCostoLabel);
            _embeddedInfo.Controls.Add(lblCostoUnitario);
            _embeddedInfo.Controls.Add(lblImporteLabel);
            _embeddedInfo.Controls.Add(lblImporte);

            panelInfo.Controls.Add(_embeddedInfo);

            // ── Contenedor raíz declarativo ─────────────────────────────────
            _embeddedRoot = new TableLayoutPanel
            {
                Name = "_embeddedRoot",
                ColumnCount = 1,
                RowCount = 4,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap),
                TabIndex = 0
            };
            _embeddedRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _embeddedRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // búsqueda/filtros
            _embeddedRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // grid (Dock=Fill)
            _embeddedRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // resumen (panelInfo)
            _embeddedRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // acciones internas

            dgvMatrices.Dock = DockStyle.Fill;
            dgvMatrices.Margin = new Padding(0, SoproUiMetrics.VerticalGap, 0, SoproUiMetrics.VerticalGap);

            _embeddedRoot.Controls.Add(_embeddedFilters, 0, 0);
            _embeddedRoot.Controls.Add(dgvMatrices, 0, 1);
            _embeddedRoot.Controls.Add(panelInfo, 0, 2);
            _embeddedRoot.Controls.Add(_embeddedActions, 0, 3);

            Controls.Add(_embeddedRoot);
            // El contenedor Fill debe quedar al fondo del z-order para que los docked Top/Bottom
            // (panelTop/statusStrip) se resuelvan primero y él ocupe el resto.
            _embeddedRoot.SendToBack();

            ResumeLayout(true);
        }

        // ════════════════════════════════════════════════════════════════════
        // Aplicación del layout embebido (sin matemática de pisos)
        // ════════════════════════════════════════════════════════════════════

        private void ApplyEmbeddedHostLayout()
        {
            if (!EmbeddedMode || _embeddedRoot is null || _embeddedFilters is null || _embeddedActions is null)
                return;

            BackColor = Color.White;
            panelTop.Visible = false;
            statusStrip.Visible = false;
            lblTitulo.Text = "Seleccionar Matriz";

            // La columna del selector de tipo colapsa cuando no está disponible.
            _embeddedFilters.ColumnStyles[2].Width = _cboTipo.Visible ? 96F : 0F;

            // Acciones internas: en modo embebido la superficie única de acciones es la
            // toolbar del host (SelectorApuEmbebidoControl.tsAcciones). La barra interna
            // queda reservada al diálogo independiente, que no construye este layout.
            _embeddedActions.Visible = false;
            btnNuevaMatriz.Visible = false;
            btnEditarMatriz.Visible = false;
            btnCancelar.Visible = false;
            btnAceptar.Visible = false;

            // Variante compacta del resumen a alturas bajas.
            ApplyInfoSummaryLayout(compact: ClientSize.Height < CompactInfoHeightThreshold);

            // AutoScroll solo como último recurso ante alturas extraordinariamente pequeñas.
            bool tooSmall = ClientSize.Height < AutoScrollFallbackHeight;
            _embeddedRoot.AutoScroll = tooSmall;
            _embeddedRoot.AutoScrollMinSize = tooSmall ? new Size(0, AutoScrollContentHeight) : Size.Empty;
        }

        /// <summary>
        /// Ajusta la densidad del resumen (Cantidad/Unidad/Costo/Importe). La disposición la
        /// resuelve por completo el <see cref="FlowLayoutPanel"/> declarativo <c>_embeddedInfo</c>
        /// (Dock/AutoSize + WrapContents); aquí solo se conmuta el espaciado de la variante
        /// compacta, sin coordenadas ni anchos mínimos lógicos.
        /// </summary>
        private void ApplyInfoSummaryLayout(bool compact)
        {
            if (_embeddedInfo is null) return;

            int groupGap = compact ? SoproUiMetrics.HorizontalGap : SoproUiMetrics.StandardMargin;
            int innerMargin = compact ? SoproUiMetrics.HorizontalGap : SoproUiMetrics.StandardMargin;

            _embeddedInfo.Padding = new Padding(
                innerMargin,
                SoproUiMetrics.VerticalGap,
                innerMargin,
                SoproUiMetrics.VerticalGap);
            _embeddedInfo.WrapContents = true;

            // Separación entre etiqueta y su valor (pequeña y constante).
            lblCantidadLabel.Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0);
            lblUnidadLabel.Margin   = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0);
            lblCostoLabel.Margin    = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0);
            lblImporteLabel.Margin  = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0);

            // Separación tras el valor que cierra cada grupo métrico.
            nudCantidad.Margin      = new Padding(0, 0, groupGap, 0);
            lblUnidad.Margin        = new Padding(0, 0, groupGap, 0);
            lblCostoUnitario.Margin = new Padding(0, 0, groupGap, 0);
            lblImporte.Margin       = new Padding(0);
        }
    }
}
