namespace SOPRO.WinForms.Forms
{
    partial class FormProgramaInsumos
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.Label lblSubtitulo;
        private System.Windows.Forms.Label lblResumen;
        private System.Windows.Forms.ToolStrip toolStripTop;
        private System.Windows.Forms.ToolStripButton btnRecargar;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton btnConfigColumnas;
        private System.Windows.Forms.ToolStripButton btnCerrar;
        private System.Windows.Forms.ToolStripLabel lblTipoTool;
        private System.Windows.Forms.ToolStripComboBox cboTipo;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripLabel lblVistaTool;
        private System.Windows.Forms.ToolStripComboBox cboVista;
        private System.Windows.Forms.SplitContainer splitPrincipal;
        private System.Windows.Forms.DataGridView dgvProgramaInsumos;
        private System.Windows.Forms.Panel panelGantt;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblEstado;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.panelTop = new System.Windows.Forms.Panel();
            this.lblResumen = new System.Windows.Forms.Label();
            this.lblSubtitulo = new System.Windows.Forms.Label();
            this.lblTitulo = new System.Windows.Forms.Label();
            this.toolStripTop = new System.Windows.Forms.ToolStrip();
            this.btnRecargar = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.lblTipoTool = new System.Windows.Forms.ToolStripLabel();
            this.cboTipo = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.lblVistaTool = new System.Windows.Forms.ToolStripLabel();
            this.cboVista = new System.Windows.Forms.ToolStripComboBox();
            this.btnConfigColumnas = new System.Windows.Forms.ToolStripButton();
            this.btnCerrar = new System.Windows.Forms.ToolStripButton();
            this.splitPrincipal = new System.Windows.Forms.SplitContainer();
            this.dgvProgramaInsumos = new System.Windows.Forms.DataGridView();
            this.panelGantt = new System.Windows.Forms.Panel();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblEstado = new System.Windows.Forms.ToolStripStatusLabel();
            this.panelTop.SuspendLayout();
            this.toolStripTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitPrincipal)).BeginInit();
            this.splitPrincipal.Panel1.SuspendLayout();
            this.splitPrincipal.Panel2.SuspendLayout();
            this.splitPrincipal.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvProgramaInsumos)).BeginInit();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // panelTop
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(51, 51, 76);
            this.panelTop.Controls.Add(this.lblResumen);
            this.panelTop.Controls.Add(this.lblSubtitulo);
            this.panelTop.Controls.Add(this.lblTitulo);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1400, 72);
            // lblResumen
            this.lblResumen.AutoSize = true;
            this.lblResumen.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblResumen.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblResumen.Location = new System.Drawing.Point(15, 49);
            this.lblResumen.Name = "lblResumen";
            this.lblResumen.Size = new System.Drawing.Size(123, 15);
            this.lblResumen.Text = "Sin programa cargado.";
            // lblSubtitulo
            this.lblSubtitulo.AutoSize = true;
            this.lblSubtitulo.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblSubtitulo.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.lblSubtitulo.Location = new System.Drawing.Point(16, 31);
            this.lblSubtitulo.Name = "lblSubtitulo";
            this.lblSubtitulo.Size = new System.Drawing.Size(51, 15);
            this.lblSubtitulo.Text = "Proyecto";
            // lblTitulo
            this.lblTitulo.AutoSize = true;
            this.lblTitulo.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblTitulo.ForeColor = System.Drawing.Color.White;
            this.lblTitulo.Location = new System.Drawing.Point(12, 6);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.Size = new System.Drawing.Size(234, 25);
            this.lblTitulo.Text = "PROGRAMA DE INSUMOS";
            // toolStripTop
            this.toolStripTop.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.toolStripTop.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripTop.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.btnRecargar, this.toolStripSeparator1, this.lblTipoTool, this.cboTipo, this.toolStripSeparator2, this.lblVistaTool, this.cboVista, this.btnConfigColumnas, this.btnCerrar });
            this.toolStripTop.Location = new System.Drawing.Point(0, 72);
            this.toolStripTop.Name = "toolStripTop";
            this.toolStripTop.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.toolStripTop.Size = new System.Drawing.Size(1400, 31);
            // btnRecargar
            this.btnRecargar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRecargar.Name = "btnRecargar";
            this.btnRecargar.Size = new System.Drawing.Size(77, 20);
            this.btnRecargar.Text = "🔄 Recargar";
            this.btnRecargar.Click += new System.EventHandler(this.btnRecargar_Click);
            // toolStripSeparator1
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 23);
            // lblTipoTool
            this.lblTipoTool.Text = "Tipo insumo:";
            // cboTipo
            this.cboTipo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTipo.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.cboTipo.Name = "cboTipo";
            this.cboTipo.Size = new System.Drawing.Size(220, 23);
            this.cboTipo.SelectedIndexChanged += new System.EventHandler(this.cboTipo_SelectedIndexChanged);
            // toolStripSeparator2
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 23);
            // lblVistaTool
            this.lblVistaTool.Text = "Vista:";
            // cboVista
            this.cboVista.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboVista.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.cboVista.Name = "cboVista";
            this.cboVista.Size = new System.Drawing.Size(150, 23);
            this.cboVista.SelectedIndexChanged += new System.EventHandler(this.cboVista_SelectedIndexChanged);
            // btnConfigColumnas
            this.btnConfigColumnas.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnConfigColumnas.Name = "btnConfigColumnas";
            this.btnConfigColumnas.Size = new System.Drawing.Size(76, 20);
            this.btnConfigColumnas.Text = "📐 Columnas";
            this.btnConfigColumnas.Click += new System.EventHandler(this.btnConfigColumnas_Click);
            // btnCerrar
            this.btnCerrar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnCerrar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnCerrar.Name = "btnCerrar";
            this.btnCerrar.Size = new System.Drawing.Size(60, 20);
            this.btnCerrar.Text = "✖ Cerrar";
            this.btnCerrar.Click += new System.EventHandler(this.btnCerrar_Click);
            // splitPrincipal
            this.splitPrincipal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitPrincipal.Location = new System.Drawing.Point(0, 103);
            this.splitPrincipal.Name = "splitPrincipal";
            this.splitPrincipal.SplitterDistance = 930;
            this.splitPrincipal.SplitterWidth = 6;
            this.splitPrincipal.Panel1.Controls.Add(this.dgvProgramaInsumos);
            this.splitPrincipal.Panel2.Controls.Add(this.panelGantt);
            // dgvProgramaInsumos
            this.dgvProgramaInsumos.BackgroundColor = System.Drawing.Color.White;
            this.dgvProgramaInsumos.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvProgramaInsumos.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvProgramaInsumos.Location = new System.Drawing.Point(0, 0);
            this.dgvProgramaInsumos.Name = "dgvProgramaInsumos";
            this.dgvProgramaInsumos.RowTemplate.Height = 25;
            this.dgvProgramaInsumos.Size = new System.Drawing.Size(930, 647);
            // panelGantt
            this.panelGantt.BackColor = System.Drawing.Color.White;
            this.panelGantt.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelGantt.Name = "panelGantt";
            this.panelGantt.Padding = new System.Windows.Forms.Padding(0);
            this.panelGantt.Size = new System.Drawing.Size(464, 647);
            // statusStrip1
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.lblEstado });
            this.statusStrip1.Location = new System.Drawing.Point(0, 750);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1400, 22);
            // lblEstado
            this.lblEstado.Name = "lblEstado";
            this.lblEstado.Size = new System.Drawing.Size(39, 17);
            this.lblEstado.Text = "Listo.";
            // FormProgramaInsumos
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1400, 772);
            this.Controls.Add(this.splitPrincipal);
            this.Controls.Add(this.toolStripTop);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.statusStrip1);
            this.Name = "FormProgramaInsumos";
            this.Text = "Programa de Insumos";
            this.Load += new System.EventHandler(this.FormProgramaInsumos_Load);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.toolStripTop.ResumeLayout(false);
            this.toolStripTop.PerformLayout();
            this.splitPrincipal.Panel1.ResumeLayout(false);
            this.splitPrincipal.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitPrincipal)).EndInit();
            this.splitPrincipal.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvProgramaInsumos)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
