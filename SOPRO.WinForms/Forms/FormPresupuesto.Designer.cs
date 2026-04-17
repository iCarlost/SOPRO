using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    partial class FormPresupuesto
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ToolStrip panelToolbar;
        private System.Windows.Forms.ToolStripButton btnColumnas;
        private System.Windows.Forms.ToolStripButton btnExplosion;
        private System.Windows.Forms.ToolStripButton btnImportarExcel;
        private System.Windows.Forms.ToolStripButton btnToggleMatrices;
        private System.Windows.Forms.ToolStripButton btnCerrar;
        private System.Windows.Forms.SplitContainer splitContainer;
        private SOPRO.WinForms.Controls.PresupuestoDataGridView dgvPresupuesto;
        private System.Windows.Forms.Panel panelMatrices;
        private System.Windows.Forms.ToolStrip workspaceToolbar;
        private System.Windows.Forms.ToolStripLabel lblWorkspaceTitulo;
        private System.Windows.Forms.ToolStripButton btnWorkspaceMatriz;
        private System.Windows.Forms.ToolStripButton btnWorkspaceSelector;
        private System.Windows.Forms.ToolStripButton btnWorkspaceCerrar;
        private System.Windows.Forms.Panel panelMatricesHost;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblTotalConceptos;
        private System.Windows.Forms.ToolStripStatusLabel lblCostoDirecto;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Desuscribirse del evento
                SOPRO.WinForms.Helpers.FormatoHelper.ConfiguracionCambiada -= OnConfiguracionCambiada;
                
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            panelToolbar = new ToolStrip();
            btnToggleMatrices = new ToolStripButton();
            btnColumnas = new ToolStripButton();
            btnExplosion = new ToolStripButton();
            btnImportarExcel = new ToolStripButton();
            btnCerrar = new ToolStripButton();
            splitContainer = new SplitContainer();
            dgvPresupuesto = new SOPRO.WinForms.Controls.PresupuestoDataGridView();
            panelMatrices = new Panel();
            workspaceToolbar = new ToolStrip();
            lblWorkspaceTitulo = new ToolStripLabel();
            btnWorkspaceMatriz = new ToolStripButton();
            btnWorkspaceSelector = new ToolStripButton();
            btnWorkspaceCerrar = new ToolStripButton();
            panelMatricesHost = new Panel();
            statusStrip = new StatusStrip();
            lblTotalConceptos = new ToolStripStatusLabel();
            lblCostoDirecto = new ToolStripStatusLabel();
            lblTitulo = new Label();
            lblProyecto = new Label();
            lblUbicacion = new Label();
            panelTop = new Panel();
            lblPorcentajesInfo = new Label();
            panelToolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPresupuesto).BeginInit();
            statusStrip.SuspendLayout();
            panelTop.SuspendLayout();
            SuspendLayout();
            // 
            // panelToolbar
            // 
            panelToolbar.BackColor = Color.FromArgb(240, 240, 240);
            panelToolbar.GripStyle = ToolStripGripStyle.Hidden;
            panelToolbar.Items.AddRange(new ToolStripItem[] { btnToggleMatrices, btnColumnas, btnImportarExcel, btnExplosion, btnCerrar });
            panelToolbar.Location = new Point(0, 68);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Padding = new Padding(8, 4, 8, 4);
            panelToolbar.Size = new Size(1400, 30);
            panelToolbar.TabIndex = 1;
            // 
            // btnToggleMatrices
            // 
            btnToggleMatrices.BackColor = SystemColors.Control;
            btnToggleMatrices.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnToggleMatrices.ForeColor = Color.Black;
            btnToggleMatrices.Name = "btnToggleMatrices";
            btnToggleMatrices.Size = new Size(88, 19);
            btnToggleMatrices.Text = "📐 Matrices ▼";
            btnToggleMatrices.Click += btnToggleMatrices_Click;
            // 
            // btnColumnas
            // 
            btnColumnas.BackColor = SystemColors.Control;
            btnColumnas.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnColumnas.ForeColor = Color.Black;
            btnColumnas.Name = "btnColumnas";
            btnColumnas.Size = new Size(80, 19);
            btnColumnas.Text = "⚙ Columnas";
            btnColumnas.Click += btnColumnas_Click;
            // 
            // 
            // btnImportarExcel
            // 
            btnImportarExcel.BackColor = SystemColors.Control;
            btnImportarExcel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnImportarExcel.ForeColor = Color.Black;
            btnImportarExcel.Name = "btnImportarExcel";
            btnImportarExcel.Size = new Size(103, 19);
            btnImportarExcel.Text = "📥 Importar Excel";
            btnImportarExcel.Click += btnImportarExcel_Click;
            // 
            // btnExplosion
            // 
            btnExplosion.BackColor = SystemColors.Control;
            btnExplosion.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnExplosion.ForeColor = Color.Black;
            btnExplosion.Name = "btnExplosion";
            btnExplosion.Size = new Size(79, 19);
            btnExplosion.Text = "💥 Explosión";
            btnExplosion.Click += btnExplosion_Click;
            // 
            // btnCerrar
            // 
            btnCerrar.Alignment = ToolStripItemAlignment.Right;
            btnCerrar.BackColor = SystemColors.Control;
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(58, 19);
            btnCerrar.Text = "✖ Cerrar";
            btnCerrar.Click += btnCerrar_Click;
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(0, 98);
            splitContainer.Name = "splitContainer";
            splitContainer.Orientation = Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(dgvPresupuesto);
            splitContainer.Panel1MinSize = 300;
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(panelMatrices);
            splitContainer.Panel2Collapsed = true;
            splitContainer.Panel2MinSize = 200;
            splitContainer.Size = new Size(1400, 778);
            splitContainer.SplitterDistance = 300;
            splitContainer.TabIndex = 4;
            // 
            // dgvPresupuesto
            // 
            dgvPresupuesto.AllowUserToAddRows = false;
            dgvPresupuesto.AllowUserToDeleteRows = false;
            dgvPresupuesto.AllowUserToOrderColumns = true;
            dgvPresupuesto.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPresupuesto.BackgroundColor = Color.White;
            dgvPresupuesto.BorderStyle = BorderStyle.None;
            dgvPresupuesto.ColumnHeadersHeight = 40;
            dgvPresupuesto.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvPresupuesto.Dock = DockStyle.Fill;
            dgvPresupuesto.EditMode = DataGridViewEditMode.EditOnF2;
            dgvPresupuesto.Location = new Point(0, 0);
            dgvPresupuesto.Name = "dgvPresupuesto";
            dgvPresupuesto.RowHeadersWidth = 40;
            dgvPresupuesto.RowTemplate.Height = 28;
            dgvPresupuesto.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPresupuesto.Size = new Size(1400, 778);
            dgvPresupuesto.TabIndex = 0;
            // 
            // panelMatrices
            // 
            panelMatrices.BackColor = Color.FromArgb(250, 250, 250);
            panelMatrices.Controls.Add(panelMatricesHost);
            panelMatrices.Controls.Add(workspaceToolbar);
            panelMatrices.Dock = DockStyle.Fill;
            panelMatrices.Location = new Point(0, 0);
            panelMatrices.Name = "panelMatrices";
            panelMatrices.Size = new Size(150, 46);
            panelMatrices.TabIndex = 0;
            // 
            // workspaceToolbar
            // 
            workspaceToolbar.BackColor = Color.FromArgb(240, 244, 248);
            workspaceToolbar.GripStyle = ToolStripGripStyle.Hidden;
            workspaceToolbar.ImageScalingSize = new Size(20, 20);
            workspaceToolbar.Items.AddRange(new ToolStripItem[] { lblWorkspaceTitulo, btnWorkspaceCerrar, btnWorkspaceMatriz, btnWorkspaceSelector });
            workspaceToolbar.Location = new Point(0, 0);
            workspaceToolbar.Name = "workspaceToolbar";
            workspaceToolbar.Padding = new Padding(6, 4, 6, 4);
            workspaceToolbar.Size = new Size(150, 31);
            workspaceToolbar.TabIndex = 0;
            workspaceToolbar.Text = "workspaceToolbar";
            // 
            // lblWorkspaceTitulo
            // 
            lblWorkspaceTitulo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblWorkspaceTitulo.ForeColor = Color.FromArgb(31, 78, 121);
            lblWorkspaceTitulo.Name = "lblWorkspaceTitulo";
            lblWorkspaceTitulo.Size = new Size(137, 20);
            lblWorkspaceTitulo.Text = "Área de trabajo: Matriz";
            // 
            // btnWorkspaceMatriz
            // 
            btnWorkspaceMatriz.Alignment = ToolStripItemAlignment.Right;
            btnWorkspaceMatriz.BackColor = Color.FromArgb(221, 235, 247);
            btnWorkspaceMatriz.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnWorkspaceMatriz.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnWorkspaceMatriz.ForeColor = Color.FromArgb(31, 78, 121);
            btnWorkspaceMatriz.Name = "btnWorkspaceMatriz";
            btnWorkspaceMatriz.Size = new Size(52, 20);
            btnWorkspaceMatriz.Text = "Matriz";
            btnWorkspaceMatriz.Click += btnWorkspaceMatriz_Click;
            // 
            // btnWorkspaceCerrar
            // 
            btnWorkspaceCerrar.Alignment = ToolStripItemAlignment.Right;
            btnWorkspaceCerrar.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnWorkspaceCerrar.Font = new Font("Segoe UI", 9F);
            btnWorkspaceCerrar.ForeColor = Color.FromArgb(150, 0, 0);
            btnWorkspaceCerrar.Name = "btnWorkspaceCerrar";
            btnWorkspaceCerrar.Size = new Size(49, 20);
            btnWorkspaceCerrar.Text = "Cerrar";
            btnWorkspaceCerrar.Click += btnWorkspaceCerrar_Click;
            // 
            // btnWorkspaceSelector
            // 
            btnWorkspaceSelector.Alignment = ToolStripItemAlignment.Right;
            btnWorkspaceSelector.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnWorkspaceSelector.Font = new Font("Segoe UI", 9F);
            btnWorkspaceSelector.Name = "btnWorkspaceSelector";
            btnWorkspaceSelector.Size = new Size(80, 20);
            btnWorkspaceSelector.Text = "Selector APU";
            btnWorkspaceSelector.Click += btnWorkspaceSelector_Click;
            // 
            // panelMatricesHost
            // 
            panelMatricesHost.BackColor = Color.FromArgb(250, 250, 250);
            panelMatricesHost.Dock = DockStyle.Fill;
            panelMatricesHost.Location = new Point(0, 31);
            panelMatricesHost.Name = "panelMatricesHost";
            panelMatricesHost.Size = new Size(150, 15);
            panelMatricesHost.TabIndex = 1;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblTotalConceptos, lblCostoDirecto });
            statusStrip.Location = new Point(0, 876);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1400, 24);
            statusStrip.TabIndex = 3;
            // 
            // lblTotalConceptos
            // 
            lblTotalConceptos.Name = "lblTotalConceptos";
            lblTotalConceptos.Size = new Size(71, 19);
            lblTotalConceptos.Text = "0 conceptos";
            // 
            // lblCostoDirecto
            // 
            lblCostoDirecto.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblCostoDirecto.Name = "lblCostoDirecto";
            lblCostoDirecto.Size = new Size(45, 19);
            lblCostoDirecto.Text = "$0.00";
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(12, 9);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(218, 32);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "💰 PRESUPUESTO";
            // 
            // lblProyecto
            // 
            lblProyecto.AutoSize = true;
            lblProyecto.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblProyecto.ForeColor = Color.White;
            lblProyecto.Location = new Point(18, 42);
            lblProyecto.Name = "lblProyecto";
            lblProyecto.Size = new Size(54, 15);
            lblProyecto.TabIndex = 1;
            lblProyecto.Text = "Proyecto";
            // 
            // lblUbicacion
            // 
            lblUbicacion.AutoSize = true;
            lblUbicacion.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblUbicacion.ForeColor = Color.White;
            lblUbicacion.Location = new Point(300, 42);
            lblUbicacion.Name = "lblUbicacion";
            lblUbicacion.Size = new Size(60, 15);
            lblUbicacion.TabIndex = 2;
            lblUbicacion.Text = "Ubicación";
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(51, 51, 76);
            panelTop.Controls.Add(lblPorcentajesInfo);
            panelTop.Controls.Add(lblUbicacion);
            panelTop.Controls.Add(lblProyecto);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(1400, 68);
            panelTop.TabIndex = 0;
            // 
            // lblPorcentajesInfo
            // 
            lblPorcentajesInfo.AutoSize = true;
            lblPorcentajesInfo.Font = new Font("Segoe UI", 9F);
            lblPorcentajesInfo.ForeColor = Color.FromArgb(180, 230, 180);
            lblPorcentajesInfo.Location = new Point(854, 47);
            lblPorcentajesInfo.Name = "lblPorcentajesInfo";
            lblPorcentajesInfo.Size = new Size(0, 15);
            lblPorcentajesInfo.TabIndex = 10;
            // 
            // FormPresupuesto
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1400, 900);
            Controls.Add(splitContainer);
            Controls.Add(statusStrip);
            Controls.Add(panelToolbar);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;
            Name = "FormPresupuesto";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Presupuesto - SOPRO";
            WindowState = FormWindowState.Maximized;
            KeyDown += DgvPresupuesto_KeyDown;
            panelToolbar.ResumeLayout(false);
            panelToolbar.PerformLayout();
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            workspaceToolbar.ResumeLayout(false);
            workspaceToolbar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPresupuesto).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblTitulo;
        private Label lblProyecto;
        private Label lblUbicacion;
        private Panel panelTop;
        private Label lblPorcentajesInfo;
    }
}
