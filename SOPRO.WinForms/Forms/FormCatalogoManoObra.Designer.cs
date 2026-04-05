namespace SOPRO.WinForms.Forms
{
    partial class FormCatalogoManoObra
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            panelTop = new Panel();
            lblTitulo = new Label();
            panelToolbar = new ToolStrip();
            btnNuevo = new ToolStripButton();
            btnEditar = new ToolStripButton();
            btnEliminar = new ToolStripButton();
            btnConfigColumnas = new ToolStripButton();
            btnRefrescar = new ToolStripButton();
            btnCerrar = new ToolStripButton();
            btnImportarExcel = new Button();
            btnExportarExcel = new Button();
            panelFiltros = new Panel();
            chkSoloMaestros = new CheckBox();
            chkSoloProyecto = new CheckBox();
            txtBuscar = new TextBox();
            lblBuscar = new Label();
            dgvManoObra = new DataGridView();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            panelTop.SuspendLayout();
            panelToolbar.SuspendLayout();
            panelFiltros.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvManoObra).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(51, 51, 76);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(1200, 60);
            panelTop.TabIndex = 0;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(20, 15);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(322, 32);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "Catálogo de Mano de Obra";
            // 
            // panelToolbar
            // 
            panelToolbar.BackColor = Color.FromArgb(240, 240, 240);
            panelToolbar.GripStyle = ToolStripGripStyle.Hidden;
            panelToolbar.Items.AddRange(new ToolStripItem[] { btnNuevo, btnEditar, btnEliminar, btnConfigColumnas, btnRefrescar, btnCerrar });
            panelToolbar.Location = new Point(0, 60);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Padding = new Padding(8, 4, 8, 4);
            panelToolbar.Size = new Size(1200, 30);
            panelToolbar.TabIndex = 1;
            // 
            // btnNuevo
            // 
            btnNuevo.BackColor = SystemColors.Control;
            btnNuevo.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnNuevo.ForeColor = Color.Black;
            btnNuevo.Name = "btnNuevo";
            btnNuevo.Size = new Size(65, 19);
            btnNuevo.Text = "➕ Nuevo";
            btnNuevo.Click += btnNuevo_Click;
            // 
            // btnEditar
            // 
            btnEditar.BackColor = SystemColors.Control;
            btnEditar.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnEditar.ForeColor = Color.Black;
            btnEditar.Name = "btnEditar";
            btnEditar.Size = new Size(61, 19);
            btnEditar.Text = "✏ Editar";
            btnEditar.Click += btnEditar_Click;
            // 
            // btnEliminar
            // 
            btnEliminar.BackColor = SystemColors.Control;
            btnEliminar.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnEliminar.ForeColor = Color.Black;
            btnEliminar.Name = "btnEliminar";
            btnEliminar.Size = new Size(72, 19);
            btnEliminar.Text = "🗑 Eliminar";
            btnEliminar.Click += btnEliminar_Click;
            // 
            // btnConfigColumnas
            // 
            btnConfigColumnas.BackColor = SystemColors.Control;
            btnConfigColumnas.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnConfigColumnas.ForeColor = Color.Black;
            btnConfigColumnas.Name = "btnConfigColumnas";
            btnConfigColumnas.Size = new Size(82, 19);
            btnConfigColumnas.Text = "📐 Columnas";
            btnConfigColumnas.Click += btnConfigColumnas_Click;
            // 
            // btnRefrescar
            // 
            btnRefrescar.BackColor = SystemColors.Control;
            btnRefrescar.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnRefrescar.ForeColor = Color.Black;
            btnRefrescar.Name = "btnRefrescar";
            btnRefrescar.Visible = false;
            btnRefrescar.Size = new Size(83, 19);
            btnRefrescar.Text = "🔄 Refrescar";
            btnRefrescar.Click += btnRefrescar_Click;
            // 
            // btnCerrar
            // 
            btnCerrar.Alignment = ToolStripItemAlignment.Right;
            btnCerrar.BackColor = SystemColors.Control;
            btnCerrar.Font = new Font("Segoe UI", 9F);
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(58, 19);
            btnCerrar.Text = "✖ Cerrar";
            btnCerrar.Click += btnCerrar_Click;
            // 
            // btnImportarExcel
            // 
            btnImportarExcel.AutoSize = true;
            btnImportarExcel.BackColor = SystemColors.Control;
            btnImportarExcel.FlatAppearance.BorderSize = 0;
            btnImportarExcel.FlatStyle = FlatStyle.Flat;
            btnImportarExcel.Font = new Font("Segoe UI", 9F);
            btnImportarExcel.Location = new Point(694, 5);
            btnImportarExcel.Name = "btnImportarExcel";
            btnImportarExcel.Size = new Size(120, 25);
            btnImportarExcel.TabIndex = 5;
            btnImportarExcel.Text = "📥 Importar Excel";
            btnImportarExcel.UseVisualStyleBackColor = false;
            btnImportarExcel.Click += btnImportarExcel_Click;
            // 
            // btnExportarExcel
            // 
            btnExportarExcel.AutoSize = true;
            btnExportarExcel.BackColor = SystemColors.Control;
            btnExportarExcel.FlatAppearance.BorderSize = 0;
            btnExportarExcel.FlatStyle = FlatStyle.Flat;
            btnExportarExcel.Font = new Font("Segoe UI", 9F);
            btnExportarExcel.Location = new Point(819, 5);
            btnExportarExcel.Name = "btnExportarExcel";
            btnExportarExcel.Size = new Size(120, 25);
            btnExportarExcel.TabIndex = 6;
            btnExportarExcel.Text = "📤 Exportar Excel";
            btnExportarExcel.UseVisualStyleBackColor = false;
            btnExportarExcel.Click += btnExportarExcel_Click;
            // 
            // panelFiltros
            // 
            panelFiltros.BackColor = Color.White;
            panelFiltros.Controls.Add(chkSoloMaestros);
            panelFiltros.Controls.Add(chkSoloProyecto);
            panelFiltros.Controls.Add(txtBuscar);
            panelFiltros.Controls.Add(lblBuscar);
            panelFiltros.Dock = DockStyle.Top;
            panelFiltros.Location = new Point(0, 90);
            panelFiltros.Name = "panelFiltros";
            panelFiltros.Padding = new Padding(10);
            panelFiltros.Size = new Size(1200, 60);
            panelFiltros.TabIndex = 2;
            // 
            // chkSoloMaestros
            // 
            chkSoloMaestros.AutoSize = true;
            chkSoloMaestros.Font = new Font("Segoe UI", 9F);
            chkSoloMaestros.Location = new Point(710, 22);
            chkSoloMaestros.Name = "chkSoloMaestros";
            chkSoloMaestros.Size = new Size(128, 19);
            chkSoloMaestros.TabIndex = 3;
            chkSoloMaestros.Text = "📥 Solo importados";
            chkSoloMaestros.UseVisualStyleBackColor = true;
            chkSoloMaestros.CheckedChanged += chkSoloMaestros_CheckedChanged;
            // 
            // chkSoloProyecto
            // 
            chkSoloProyecto.AutoSize = true;
            chkSoloProyecto.Font = new Font("Segoe UI", 9F);
            chkSoloProyecto.Location = new Point(550, 22);
            chkSoloProyecto.Name = "chkSoloProyecto";
            chkSoloProyecto.Size = new Size(133, 19);
            chkSoloProyecto.TabIndex = 2;
            chkSoloProyecto.Text = "📁 Solo del Proyecto";
            chkSoloProyecto.UseVisualStyleBackColor = true;
            chkSoloProyecto.CheckedChanged += chkSoloProyecto_CheckedChanged;
            // 
            // txtBuscar
            // 
            txtBuscar.Font = new Font("Segoe UI", 10F);
            txtBuscar.Location = new Point(90, 19);
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(440, 25);
            txtBuscar.TabIndex = 1;
            txtBuscar.TextChanged += txtBuscar_TextChanged;
            // 
            // lblBuscar
            // 
            lblBuscar.AutoSize = true;
            lblBuscar.Font = new Font("Segoe UI", 10F);
            lblBuscar.Location = new Point(20, 22);
            lblBuscar.Name = "lblBuscar";
            lblBuscar.Size = new Size(75, 19);
            lblBuscar.TabIndex = 0;
            lblBuscar.Text = "🔍 Buscar:";
            // 
            // dgvManoObra
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvManoObra.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvManoObra.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dgvManoObra.DefaultCellStyle = dataGridViewCellStyle2;
            dgvManoObra.Dock = DockStyle.Fill;
            dgvManoObra.Location = new Point(0, 150);
            dgvManoObra.Name = "dgvManoObra";
            dgvManoObra.Size = new Size(1200, 500);
            dgvManoObra.TabIndex = 3;
            dgvManoObra.CellDoubleClick += dgvManoObra_CellDoubleClick;
            dgvManoObra.SelectionChanged += dgvManoObra_SelectionChanged;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 650);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1200, 22);
            statusStrip.TabIndex = 4;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(32, 17);
            lblStatus.Text = "Listo";
            // 
            // FormCatalogoManoObra
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 672);
            Controls.Add(dgvManoObra);
            Controls.Add(statusStrip);
            Controls.Add(panelFiltros);
            Controls.Add(panelToolbar);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            Name = "FormCatalogoManoObra";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Catálogo de Mano de Obra - SOPRO";
            WindowState = FormWindowState.Maximized;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelToolbar.ResumeLayout(false);
            panelToolbar.PerformLayout();
            panelFiltros.ResumeLayout(false);
            panelFiltros.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvManoObra).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.ToolStrip panelToolbar;
        private System.Windows.Forms.ToolStripButton btnNuevo;
        private System.Windows.Forms.ToolStripButton btnEditar;
        private System.Windows.Forms.ToolStripButton btnEliminar;
        private System.Windows.Forms.ToolStripButton btnRefrescar;
        private System.Windows.Forms.ToolStripButton btnConfigColumnas;
        private System.Windows.Forms.Button btnImportarExcel;
        private System.Windows.Forms.Button btnExportarExcel;
        private System.Windows.Forms.ToolStripButton btnCerrar;
        private System.Windows.Forms.Panel panelFiltros;
        private System.Windows.Forms.TextBox txtBuscar;
        private System.Windows.Forms.Label lblBuscar;
        private System.Windows.Forms.CheckBox chkSoloProyecto;
        private System.Windows.Forms.CheckBox chkSoloMaestros;
        private System.Windows.Forms.DataGridView dgvManoObra;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
