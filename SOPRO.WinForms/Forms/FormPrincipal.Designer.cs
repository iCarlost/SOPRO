namespace SOPRO.WinForms.Forms
{
    partial class FormPrincipal
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormPrincipal));
            panelTop = new Panel();
            btnAbrirProyecto = new Button();
            btnNuevoProyecto = new Button();
            lblSubtitle = new Label();
            lblTitle = new Label();
            panelCenter = new Panel();
            grpRecientes = new GroupBox();
            dgvRecientes = new DataGridView();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            panelTop.SuspendLayout();
            panelCenter.SuspendLayout();
            grpRecientes.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvRecientes).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(51, 51, 76);
            panelTop.Controls.Add(btnAbrirProyecto);
            panelTop.Controls.Add(btnNuevoProyecto);
            panelTop.Controls.Add(lblSubtitle);
            panelTop.Controls.Add(lblTitle);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(874, 80);
            panelTop.TabIndex = 0;
            // 
            // btnAbrirProyecto
            // 
            btnAbrirProyecto.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAbrirProyecto.BackColor = Color.White;
            btnAbrirProyecto.Cursor = Cursors.Hand;
            btnAbrirProyecto.FlatStyle = FlatStyle.Flat;
            btnAbrirProyecto.Font = new Font("Segoe UI", 9F);
            btnAbrirProyecto.ForeColor = Color.FromArgb(51, 51, 76);
            btnAbrirProyecto.Location = new Point(694, 40);
            btnAbrirProyecto.Name = "btnAbrirProyecto";
            btnAbrirProyecto.Size = new Size(150, 30);
            btnAbrirProyecto.TabIndex = 3;
            btnAbrirProyecto.Text = "📂 Abrir Proyecto";
            btnAbrirProyecto.UseVisualStyleBackColor = false;
            btnAbrirProyecto.Click += BtnAbrirProyecto_Click;
            // 
            // btnNuevoProyecto
            // 
            btnNuevoProyecto.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNuevoProyecto.BackColor = Color.White;
            btnNuevoProyecto.Cursor = Cursors.Hand;
            btnNuevoProyecto.FlatStyle = FlatStyle.Flat;
            btnNuevoProyecto.Font = new Font("Segoe UI", 9F);
            btnNuevoProyecto.ForeColor = Color.FromArgb(51, 51, 76);
            btnNuevoProyecto.Location = new Point(694, 10);
            btnNuevoProyecto.Name = "btnNuevoProyecto";
            btnNuevoProyecto.Size = new Size(150, 30);
            btnNuevoProyecto.TabIndex = 2;
            btnNuevoProyecto.Text = "➕ Nuevo Proyecto";
            btnNuevoProyecto.UseVisualStyleBackColor = false;
            btnNuevoProyecto.Click += BtnNuevoProyecto_Click;
            // 
            // lblSubtitle
            // 
            lblSubtitle.AutoSize = true;
            lblSubtitle.Font = new Font("Segoe UI", 10F);
            lblSubtitle.ForeColor = Color.FromArgb(150, 180, 220);
            lblSubtitle.Location = new Point(20, 52);
            lblSubtitle.Name = "lblSubtitle";
            lblSubtitle.Size = new Size(233, 19);
            lblSubtitle.TabIndex = 1;
            lblSubtitle.Text = "Software para Presupuestos de Obra";
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(20, 14);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(127, 45);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "SOPRO";
            // 
            // panelCenter
            // 
            panelCenter.BackColor = Color.FromArgb(240, 240, 240);
            panelCenter.Controls.Add(grpRecientes);
            panelCenter.Dock = DockStyle.Fill;
            panelCenter.Location = new Point(0, 80);
            panelCenter.Name = "panelCenter";
            panelCenter.Padding = new Padding(40);
            panelCenter.Size = new Size(874, 440);
            panelCenter.TabIndex = 1;
            // 
            // grpRecientes
            // 
            grpRecientes.Controls.Add(dgvRecientes);
            grpRecientes.Dock = DockStyle.Fill;
            grpRecientes.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpRecientes.Location = new Point(40, 40);
            grpRecientes.Name = "grpRecientes";
            grpRecientes.Size = new Size(794, 360);
            grpRecientes.TabIndex = 0;
            grpRecientes.TabStop = false;
            grpRecientes.Text = "PROYECTOS RECIENTES";
            // 
            // dgvRecientes
            // 
            dgvRecientes.AllowUserToAddRows = false;
            dgvRecientes.AllowUserToDeleteRows = false;
            dgvRecientes.AllowUserToResizeRows = false;
            dgvRecientes.BackgroundColor = Color.White;
            dgvRecientes.BorderStyle = BorderStyle.None;
            dgvRecientes.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Window;
            dataGridViewCellStyle1.Font = new Font("Segoe UI Semilight", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            dataGridViewCellStyle1.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.False;
            dgvRecientes.DefaultCellStyle = dataGridViewCellStyle1;
            dgvRecientes.Dock = DockStyle.Fill;
            dgvRecientes.Location = new Point(3, 21);
            dgvRecientes.MultiSelect = false;
            dgvRecientes.Name = "dgvRecientes";
            dgvRecientes.ReadOnly = true;
            dgvRecientes.RowHeadersVisible = false;
            dgvRecientes.RowHeadersWidth = 51;
            dgvRecientes.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvRecientes.Size = new Size(788, 336);
            dgvRecientes.TabIndex = 0;
            dgvRecientes.CellClick += dgvRecientes_CellClick;
            dgvRecientes.CellDoubleClick += dgvRecientes_CellDoubleClick;
            // 
            // statusStrip
            // 
            statusStrip.ImageScalingSize = new Size(20, 20);
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 520);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(874, 22);
            statusStrip.TabIndex = 2;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(32, 17);
            lblStatus.Text = "Listo";
            // 
            // FormPrincipal
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(874, 542);
            Controls.Add(panelCenter);
            Controls.Add(statusStrip);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MaximumSize = new Size(890, 581);
            MinimumSize = new Size(750, 420);
            Name = "FormPrincipal";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "SOPRO - Software para Presupuestos de Obra";
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelCenter.ResumeLayout(false);
            grpRecientes.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvRecientes).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.Button btnNuevoProyecto;
        private System.Windows.Forms.Button btnAbrirProyecto;
        private System.Windows.Forms.Panel panelCenter;
        private System.Windows.Forms.GroupBox grpRecientes;
        private System.Windows.Forms.DataGridView dgvRecientes;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
