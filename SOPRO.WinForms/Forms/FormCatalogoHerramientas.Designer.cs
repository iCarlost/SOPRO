using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    partial class FormCatalogoHerramientas
    {
        private System.ComponentModel.IContainer components = null;
        private Panel panelHeader;
        private Label lblTitulo;
        private Panel panelBusqueda;
        private Label lblBuscar;
        private TextBox txtBuscar;
        private ToolStripButton btnNuevo;
        private DataGridView dgvHerramientas;
        private ToolStrip panelBotones;
        private ToolStripButton btnEditar;
        private ToolStripButton btnEliminar;
        private ToolStripButton btnCerrar;
        private ToolStripButton btnConfigColumnas;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.CheckBox chkSoloProyecto;
        private System.Windows.Forms.CheckBox chkSoloMaestros;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            panelHeader = new Panel();
            lblTitulo = new Label();
            panelBusqueda = new Panel();
            lblBuscar = new Label();
            txtBuscar = new TextBox();
            chkSoloProyecto = new CheckBox();
            chkSoloMaestros = new CheckBox();
            btnNuevo = new ToolStripButton();
            dgvHerramientas = new DataGridView();
            panelBotones = new ToolStrip();
            btnEditar = new ToolStripButton();
            btnEliminar = new ToolStripButton();
            btnConfigColumnas = new ToolStripButton();
            btnCerrar = new ToolStripButton();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            panelHeader.SuspendLayout();
            panelBusqueda.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvHerramientas).BeginInit();
            panelBotones.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // panelHeader
            // 
            panelHeader.BackColor = Color.FromArgb(51, 51, 76);
            panelHeader.Controls.Add(lblTitulo);
            panelHeader.Dock = DockStyle.Top;
            panelHeader.Location = new Point(0, 0);
            panelHeader.Name = "panelHeader";
            panelHeader.Size = new Size(1000, 60);
            panelHeader.TabIndex = 0;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(20, 15);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(240, 32);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "🛠️ HERRAMIENTAS";
            // 
            // panelBusqueda
            // 
            panelBusqueda.BackColor = Color.White;
            panelBusqueda.Controls.Add(lblBuscar);
            panelBusqueda.Controls.Add(txtBuscar);
            panelBusqueda.Controls.Add(chkSoloProyecto);
            panelBusqueda.Controls.Add(chkSoloMaestros);
            panelBusqueda.Dock = DockStyle.Top;
            panelBusqueda.Location = new Point(0, 90);
            panelBusqueda.Name = "panelBusqueda";
            panelBusqueda.Padding = new Padding(20, 15, 20, 15);
            panelBusqueda.Size = new Size(1000, 60);
            panelBusqueda.TabIndex = 2;
            // 
            // lblBuscar
            // 
            lblBuscar.AutoSize = true;
            lblBuscar.Font = new Font("Segoe UI", 10F);
            lblBuscar.Location = new Point(20, 20);
            lblBuscar.Name = "lblBuscar";
            lblBuscar.Size = new Size(75, 19);
            lblBuscar.TabIndex = 0;
            lblBuscar.Text = "🔍 Buscar:";
            // 
            // txtBuscar
            // 
            txtBuscar.Font = new Font("Segoe UI", 10F);
            txtBuscar.Location = new Point(95, 17);
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(400, 25);
            txtBuscar.TabIndex = 1;
            txtBuscar.TextChanged += txtBuscar_TextChanged;
            // 
            // chkSoloProyecto
            // 
            chkSoloProyecto.AutoSize = true;
            chkSoloProyecto.Font = new Font("Segoe UI", 9F);
            chkSoloProyecto.Location = new Point(520, 20);
            chkSoloProyecto.Name = "chkSoloProyecto";
            chkSoloProyecto.Size = new Size(103, 19);
            chkSoloProyecto.TabIndex = 2;
            chkSoloProyecto.Text = "🏠 Solo locales";
            chkSoloProyecto.UseVisualStyleBackColor = true;
            chkSoloProyecto.CheckedChanged += chkSoloProyecto_CheckedChanged;
            // 
            // chkSoloMaestros
            // 
            chkSoloMaestros.AutoSize = true;
            chkSoloMaestros.Font = new Font("Segoe UI", 9F);
            chkSoloMaestros.Location = new Point(650, 20);
            chkSoloMaestros.Name = "chkSoloMaestros";
            chkSoloMaestros.Size = new Size(128, 19);
            chkSoloMaestros.TabIndex = 3;
            chkSoloMaestros.Text = "📥 Solo importados";
            chkSoloMaestros.UseVisualStyleBackColor = true;
            chkSoloMaestros.CheckedChanged += chkSoloMaestros_CheckedChanged;
            // 
            // btnNuevo
            // 
            btnNuevo.BackColor = SystemColors.Control;
            btnNuevo.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnNuevo.ForeColor = Color.Black;
            btnNuevo.Name = "btnNuevo";
            btnNuevo.Size = new Size(71, 19);
            btnNuevo.Text = "➕ Nueva";
            btnNuevo.Click += btnNuevo_Click;
            // 
            // dgvHerramientas
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvHerramientas.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvHerramientas.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dgvHerramientas.DefaultCellStyle = dataGridViewCellStyle2;
            dgvHerramientas.Dock = DockStyle.Fill;
            dgvHerramientas.Location = new Point(0, 150);
            dgvHerramientas.Name = "dgvHerramientas";
            dgvHerramientas.Size = new Size(1000, 460);
            dgvHerramientas.TabIndex = 2;
            dgvHerramientas.CellDoubleClick += dgvHerramientas_CellDoubleClick;
            dgvHerramientas.SelectionChanged += dgvHerramientas_SelectionChanged;
            // 
            // panelBotones
            // 
            panelBotones.BackColor = Color.WhiteSmoke;
            panelBotones.GripStyle = ToolStripGripStyle.Hidden;
            panelBotones.Items.AddRange(new ToolStripItem[] { btnNuevo, btnEditar, btnEliminar, btnConfigColumnas, btnCerrar });
            panelBotones.Location = new Point(0, 60);
            panelBotones.Name = "panelBotones";
            panelBotones.Padding = new Padding(8, 4, 8, 4);
            panelBotones.Size = new Size(1000, 30);
            panelBotones.TabIndex = 1;
            // 
            // btnEditar
            // 
            btnEditar.BackColor = SystemColors.Control;
            btnEditar.Enabled = false;
            btnEditar.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnEditar.ForeColor = Color.Black;
            btnEditar.Name = "btnEditar";
            btnEditar.Size = new Size(69, 19);
            btnEditar.Text = "✏️ Editar";
            btnEditar.Click += btnEditar_Click;
            // 
            // btnEliminar
            // 
            btnEliminar.BackColor = SystemColors.Control;
            btnEliminar.Enabled = false;
            btnEliminar.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnEliminar.ForeColor = Color.Black;
            btnEliminar.Name = "btnEliminar";
            btnEliminar.Size = new Size(85, 19);
            btnEliminar.Text = "🗑️ Eliminar";
            btnEliminar.Click += btnEliminar_Click;
            // 
            // btnConfigColumnas
            // 
            btnConfigColumnas.BackColor = SystemColors.Control;
            btnConfigColumnas.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnConfigColumnas.ForeColor = Color.Black;
            btnConfigColumnas.Name = "btnConfigColumnas";
            btnConfigColumnas.Size = new Size(95, 19);
            btnConfigColumnas.Text = "📐 Columnas";
            btnConfigColumnas.Click += btnConfigColumnas_Click;
            // 
            // btnCerrar
            // 
            btnCerrar.Alignment = ToolStripItemAlignment.Right;
            btnCerrar.BackColor = SystemColors.Control;
            btnCerrar.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnCerrar.ForeColor = Color.Black;
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(58, 19);
            btnCerrar.Text = "✖ Cerrar";
            btnCerrar.Click += btnCerrar_Click;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 610);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1000, 22);
            statusStrip.TabIndex = 4;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(35, 17);
            lblStatus.Text = "Listo.";
            // 
            // FormCatalogoHerramientas
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 632);
            Controls.Add(dgvHerramientas);
            Controls.Add(statusStrip);
            Controls.Add(panelBusqueda);
            Controls.Add(panelBotones);
            Controls.Add(panelHeader);
            Font = new Font("Segoe UI", 9F);
            Name = "FormCatalogoHerramientas";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Catálogo de Herramientas";
            panelHeader.ResumeLayout(false);
            panelHeader.PerformLayout();
            panelBusqueda.ResumeLayout(false);
            panelBusqueda.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvHerramientas).EndInit();
            panelBotones.ResumeLayout(false);
            panelBotones.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
