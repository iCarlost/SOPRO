using SOPRO.WinForms.UI.Controls;

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
            btnAbrirProyecto = new SoproButton();
            btnNuevoProyecto = new SoproButton();
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
            panelTop.Controls.Add(btnAbrirProyecto);
            panelTop.Controls.Add(btnNuevoProyecto);
            panelTop.Controls.Add(lblSubtitle);
            panelTop.Controls.Add(lblTitle);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(874, 88);
            panelTop.TabIndex = 0;
            // 
            // btnAbrirProyecto
            // 
            btnAbrirProyecto.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAbrirProyecto.Cursor = Cursors.Hand;
            btnAbrirProyecto.FlatStyle = FlatStyle.Flat;
            btnAbrirProyecto.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnAbrirProyecto.Location = new Point(688, 27);
            btnAbrirProyecto.Name = "btnAbrirProyecto";
            btnAbrirProyecto.Size = new Size(160, 34);
            btnAbrirProyecto.TabIndex = 3;
            btnAbrirProyecto.Text = "Abrir Proyecto";
            btnAbrirProyecto.UseVisualStyleBackColor = false;
            btnAbrirProyecto.Click += BtnAbrirProyecto_Click;
            // 
            // btnNuevoProyecto
            // 
            btnNuevoProyecto.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNuevoProyecto.Cursor = Cursors.Hand;
            btnNuevoProyecto.FlatStyle = FlatStyle.Flat;
            btnNuevoProyecto.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnNuevoProyecto.Location = new Point(520, 27);
            btnNuevoProyecto.Name = "btnNuevoProyecto";
            btnNuevoProyecto.Size = new Size(160, 34);
            btnNuevoProyecto.TabIndex = 2;
            btnNuevoProyecto.Text = "Nuevo Proyecto";
            btnNuevoProyecto.UseVisualStyleBackColor = false;
            btnNuevoProyecto.Click += BtnNuevoProyecto_Click;
            // 
            // lblSubtitle
            // 
            lblSubtitle.AutoSize = true;
            lblSubtitle.Font = new Font("Segoe UI", 9.5F);
            lblSubtitle.Location = new Point(26, 60);
            lblSubtitle.Name = "lblSubtitle";
            lblSubtitle.Size = new Size(240, 18);
            lblSubtitle.TabIndex = 1;
            lblSubtitle.Text = "Software para Presupuestos de Obra";
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 26F, FontStyle.Bold);
            lblTitle.Location = new Point(24, 10);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(150, 48);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "SOPRO";
            // 
            // panelCenter
            // 
            panelCenter.Controls.Add(grpRecientes);
            panelCenter.Dock = DockStyle.Fill;
            panelCenter.Location = new Point(0, 88);
            panelCenter.Name = "panelCenter";
            panelCenter.Padding = new Padding(28, 24, 28, 24);
            panelCenter.Size = new Size(874, 432);
            panelCenter.TabIndex = 1;
            // 
            // grpRecientes
            // 
            grpRecientes.Controls.Add(dgvRecientes);
            grpRecientes.Dock = DockStyle.Fill;
            grpRecientes.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpRecientes.Location = new Point(28, 24);
            grpRecientes.Name = "grpRecientes";
            grpRecientes.Size = new Size(818, 384);
            grpRecientes.TabIndex = 0;
            grpRecientes.TabStop = false;
            grpRecientes.Text = "PROYECTOS RECIENTES";
            // 
            // dgvRecientes
            // 
            dgvRecientes.AllowUserToAddRows = false;
            dgvRecientes.AllowUserToDeleteRows = false;
            dgvRecientes.AllowUserToResizeRows = false;
            dgvRecientes.BorderStyle = BorderStyle.None;
            dgvRecientes.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.Font = new Font("Segoe UI Semilight", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
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

        internal System.Windows.Forms.Panel panelTop;
        internal System.Windows.Forms.Label lblTitle;
        internal System.Windows.Forms.Label lblSubtitle;
        internal SoproButton btnNuevoProyecto;
        internal SoproButton btnAbrirProyecto;
        internal System.Windows.Forms.Panel panelCenter;
        internal System.Windows.Forms.GroupBox grpRecientes;
        internal System.Windows.Forms.DataGridView dgvRecientes;
        internal System.Windows.Forms.StatusStrip statusStrip;
        internal System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
