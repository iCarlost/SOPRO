namespace SOPRO.WinForms.Forms
{
    partial class FormMatrices
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
            btnConfigReporte = new ToolStripButton();
            btnRefrescar = new ToolStripButton();
            btnCerrar = new ToolStripButton();
            btnImportarExcel = new Button();
            btnExportarExcel = new Button();
            btnCopiar = new Button();
            panelFiltros = new Panel();
            rbSoloBasicos = new RadioButton();
            rbSoloAPU = new RadioButton();
            rbTodos = new RadioButton();
            rbSoloCuadrillas = new RadioButton();
            txtBuscar = new TextBox();
            lblBuscar = new Label();
            dgvMatrices = new DataGridView();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            panelTop.SuspendLayout();
            panelToolbar.SuspendLayout();
            panelFiltros.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvMatrices).BeginInit();
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
            lblTitulo.Size = new Size(441, 32);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "Catálogo de Matrices (APU y Básicos)";
            // 
            // panelToolbar
            // 
            panelToolbar.BackColor = Color.FromArgb(240, 240, 240);
            panelToolbar.GripStyle = ToolStripGripStyle.Hidden;
            panelToolbar.Items.AddRange(new ToolStripItem[] { btnNuevo, btnEditar, btnEliminar, btnConfigReporte, btnRefrescar, btnCerrar });
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
            // btnConfigReporte
            // 
            btnConfigReporte.BackColor = SystemColors.Control;
            btnConfigReporte.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnConfigReporte.ForeColor = Color.Black;
            btnConfigReporte.Name = "btnConfigReporte";
            btnConfigReporte.Size = new Size(82, 19);
            btnConfigReporte.Text = "📐 Columnas";
            btnConfigReporte.Click += btnConfigReporte_Click;
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
            btnCerrar.ForeColor = Color.Black;
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(58, 19);
            btnCerrar.Text = "✖ Cerrar";
            btnCerrar.Click += btnCerrar_Click;
            // 
            // btnImportarExcel
            // 
            btnImportarExcel.BackColor = Color.White;
            btnImportarExcel.FlatStyle = FlatStyle.Flat;
            btnImportarExcel.Font = new Font("Segoe UI", 9F);
            btnImportarExcel.Location = new Point(908, 13);
            btnImportarExcel.Name = "btnImportarExcel";
            btnImportarExcel.Size = new Size(120, 35);
            btnImportarExcel.TabIndex = 5;
            btnImportarExcel.Text = "📥 Importar Excel";
            btnImportarExcel.UseVisualStyleBackColor = false;
            btnImportarExcel.Click += btnImportarExcel_Click;
            // 
            // btnExportarExcel
            // 
            btnExportarExcel.BackColor = Color.White;
            btnExportarExcel.FlatStyle = FlatStyle.Flat;
            btnExportarExcel.Font = new Font("Segoe UI", 9F);
            btnExportarExcel.Location = new Point(1038, 13);
            btnExportarExcel.Name = "btnExportarExcel";
            btnExportarExcel.Size = new Size(120, 35);
            btnExportarExcel.TabIndex = 6;
            btnExportarExcel.Text = "📤 Exportar Excel";
            btnExportarExcel.UseVisualStyleBackColor = false;
            btnExportarExcel.Click += btnExportarExcel_Click;
            // 
            // btnCopiar
            // 
            btnCopiar.BackColor = Color.FromArgb(103, 58, 183);
            btnCopiar.FlatStyle = FlatStyle.Flat;
            btnCopiar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCopiar.ForeColor = Color.White;
            btnCopiar.Location = new Point(390, 13);
            btnCopiar.Name = "btnCopiar";
            btnCopiar.Size = new Size(110, 35);
            btnCopiar.TabIndex = 3;
            btnCopiar.Text = "📋 Copiar";
            btnCopiar.UseVisualStyleBackColor = false;
            btnCopiar.Click += btnCopiar_Click;
            // 
            // panelFiltros
            // 
            panelFiltros.BackColor = Color.White;
            panelFiltros.Controls.Add(rbSoloBasicos);
            panelFiltros.Controls.Add(rbSoloAPU);
            panelFiltros.Controls.Add(rbTodos);
            panelFiltros.Controls.Add(rbSoloCuadrillas);
            panelFiltros.Controls.Add(txtBuscar);
            panelFiltros.Controls.Add(lblBuscar);
            panelFiltros.Dock = DockStyle.Top;
            panelFiltros.Location = new Point(0, 90);
            panelFiltros.Name = "panelFiltros";
            panelFiltros.Padding = new Padding(10);
            panelFiltros.Size = new Size(1200, 60);
            panelFiltros.TabIndex = 2;
            // 
            // rbSoloBasicos
            // 
            rbSoloBasicos.AutoSize = true;
            rbSoloBasicos.Font = new Font("Segoe UI", 9F);
            rbSoloBasicos.Location = new Point(780, 22);
            rbSoloBasicos.Name = "rbSoloBasicos";
            rbSoloBasicos.Size = new Size(105, 19);
            rbSoloBasicos.TabIndex = 4;
            rbSoloBasicos.Text = "\U0001f9e9 Solo Básicos";
            rbSoloBasicos.UseVisualStyleBackColor = true;
            rbSoloBasicos.CheckedChanged += rbSoloBasicos_CheckedChanged;
            // 
            // rbSoloAPU
            // 
            rbSoloAPU.AutoSize = true;
            rbSoloAPU.Font = new Font("Segoe UI", 9F);
            rbSoloAPU.Location = new Point(670, 22);
            rbSoloAPU.Name = "rbSoloAPU";
            rbSoloAPU.Size = new Size(89, 19);
            rbSoloAPU.TabIndex = 3;
            rbSoloAPU.Text = "📊 Solo APU";
            rbSoloAPU.UseVisualStyleBackColor = true;
            rbSoloAPU.CheckedChanged += rbSoloAPU_CheckedChanged;
            // 
            // rbTodos
            // 
            rbTodos.AutoSize = true;
            rbTodos.Checked = true;
            rbTodos.Font = new Font("Segoe UI", 9F);
            rbTodos.Location = new Point(580, 22);
            rbTodos.Name = "rbTodos";
            rbTodos.Size = new Size(72, 19);
            rbTodos.TabIndex = 2;
            rbTodos.TabStop = true;
            rbTodos.Text = "📋 Todos";
            rbTodos.UseVisualStyleBackColor = true;
            rbTodos.CheckedChanged += rbTodos_CheckedChanged;
            // 
            // rbSoloCuadrillas
            // 
            rbSoloCuadrillas.AutoSize = true;
            rbSoloCuadrillas.Font = new Font("Segoe UI", 9F);
            rbSoloCuadrillas.Location = new Point(900, 22);
            rbSoloCuadrillas.Name = "rbSoloCuadrillas";
            rbSoloCuadrillas.Size = new Size(118, 19);
            rbSoloCuadrillas.TabIndex = 5;
            rbSoloCuadrillas.Text = "👷 Solo Cuadrillas";
            rbSoloCuadrillas.UseVisualStyleBackColor = true;
            rbSoloCuadrillas.CheckedChanged += rbSoloCuadrillas_CheckedChanged;
            // 
            // txtBuscar
            // 
            txtBuscar.Font = new Font("Segoe UI", 10F);
            txtBuscar.Location = new Point(90, 19);
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(460, 25);
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
            // dgvMatrices
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvMatrices.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvMatrices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dgvMatrices.DefaultCellStyle = dataGridViewCellStyle2;
            dgvMatrices.Dock = DockStyle.Fill;
            dgvMatrices.Location = new Point(0, 150);
            dgvMatrices.Name = "dgvMatrices";
            dgvMatrices.Size = new Size(1200, 500);
            dgvMatrices.TabIndex = 3;
            dgvMatrices.CellDoubleClick += dgvMatrices_CellDoubleClick;
            dgvMatrices.SelectionChanged += dgvMatrices_SelectionChanged;
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
            // FormMatrices
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 672);
            Controls.Add(dgvMatrices);
            Controls.Add(statusStrip);
            Controls.Add(panelFiltros);
            Controls.Add(panelToolbar);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            Name = "FormMatrices";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Catálogo de Matrices (APU y Básicos) - SOPRO";
            WindowState = FormWindowState.Maximized;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelToolbar.ResumeLayout(false);
            panelToolbar.PerformLayout();
            panelFiltros.ResumeLayout(false);
            panelFiltros.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvMatrices).EndInit();
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
        private System.Windows.Forms.Button btnCopiar;
        private System.Windows.Forms.ToolStripButton btnRefrescar;
        private System.Windows.Forms.ToolStripButton btnConfigReporte;
        private System.Windows.Forms.Button btnImportarExcel;
        private System.Windows.Forms.Button btnExportarExcel;
        private System.Windows.Forms.ToolStripButton btnCerrar;
        private System.Windows.Forms.Panel panelFiltros;
        private System.Windows.Forms.TextBox txtBuscar;
        private System.Windows.Forms.Label lblBuscar;
        private System.Windows.Forms.RadioButton rbTodos;
        private System.Windows.Forms.RadioButton rbSoloAPU;
        private System.Windows.Forms.RadioButton rbSoloBasicos;
        private System.Windows.Forms.RadioButton rbSoloCuadrillas;
        private System.Windows.Forms.DataGridView dgvMatrices;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
