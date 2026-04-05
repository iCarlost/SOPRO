namespace SOPRO.WinForms.Forms
{
    partial class FormSeleccionarInsumo
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormSeleccionarInsumo));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            panelTop = new Panel();
            lblTitulo = new Label();
            lblBuscar = new Label();
            txtBuscar = new TextBox();
            lblProyecto = new Label();
            cboProyecto = new ComboBox();
            panelFiltroMO = new Panel();
            rbMOTodos = new RadioButton();
            rbMOIndividual = new RadioButton();
            rbMOCuadrillas = new RadioButton();
            btnNuevoInsumo = new Button();
            dgvInsumos = new DataGridView();
            colId = new DataGridViewTextBoxColumn();
            colClave = new DataGridViewTextBoxColumn();
            colDescripcion = new DataGridViewTextBoxColumn();
            colUnidad = new DataGridViewTextBoxColumn();
            colPrecio = new DataGridViewTextBoxColumn();
            colOrigen = new DataGridViewTextBoxColumn();
            colProyecto = new DataGridViewTextBoxColumn();
            colFecha = new DataGridViewTextBoxColumn();
            panelBottom = new Panel();
            btnAceptar = new Button();
            btnCancelar = new Button();
            lblUnidad = new Label();
            nudCantidad = new NumericUpDown();
            lblCantidadLabel = new Label();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            ctxProyectoFavorito = new ContextMenuStrip(components);
            mnuToggleFavorito = new ToolStripMenuItem();
            panelTop.SuspendLayout();
            panelFiltroMO.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvInsumos).BeginInit();
            panelBottom.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudCantidad).BeginInit();
            statusStrip.SuspendLayout();
            ctxProyectoFavorito.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(0, 150, 136);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(980, 60);
            panelTop.TabIndex = 0;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(20, 18);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(253, 30);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "SELECCIONAR INSUMO";
            // 
            // lblBuscar
            // 
            lblBuscar.AutoSize = true;
            lblBuscar.Font = new Font("Segoe UI", 10F);
            lblBuscar.Location = new Point(20, 75);
            lblBuscar.Name = "lblBuscar";
            lblBuscar.Size = new Size(75, 19);
            lblBuscar.TabIndex = 1;
            lblBuscar.Text = "🔍 Buscar:";
            // 
            // txtBuscar
            // 
            txtBuscar.Font = new Font("Segoe UI", 10F);
            txtBuscar.Location = new Point(95, 72);
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(290, 25);
            txtBuscar.TabIndex = 2;
            txtBuscar.TextChanged += txtBuscar_TextChanged;
            // 
            // lblProyecto
            // 
            lblProyecto.AutoSize = true;
            lblProyecto.Font = new Font("Segoe UI", 10F);
            lblProyecto.Location = new Point(403, 75);
            lblProyecto.Name = "lblProyecto";
            lblProyecto.Size = new Size(57, 19);
            lblProyecto.TabIndex = 3;
            lblProyecto.Text = "Origen:";
            // 
            // cboProyecto
            // 
            cboProyecto.DropDownStyle = ComboBoxStyle.DropDownList;
            cboProyecto.Font = new Font("Segoe UI", 9.5F);
            cboProyecto.FormattingEnabled = true;
            cboProyecto.Location = new Point(463, 72);
            cboProyecto.Name = "cboProyecto";
            cboProyecto.Size = new Size(350, 25);
            cboProyecto.TabIndex = 4;
            cboProyecto.SelectedIndexChanged += cboProyecto_SelectedIndexChanged;
            // 
            // panelFiltroMO
            // 
            panelFiltroMO.Controls.Add(rbMOTodos);
            panelFiltroMO.Controls.Add(rbMOIndividual);
            panelFiltroMO.Controls.Add(rbMOCuadrillas);
            panelFiltroMO.Location = new Point(463, 103);
            panelFiltroMO.Name = "panelFiltroMO";
            panelFiltroMO.Size = new Size(350, 32);
            panelFiltroMO.TabIndex = 7;
            panelFiltroMO.Visible = false;
            // 
            // rbMOTodos
            // 
            rbMOTodos.AutoSize = true;
            rbMOTodos.Checked = true;
            rbMOTodos.Font = new Font("Segoe UI", 9F);
            rbMOTodos.Location = new Point(5, 7);
            rbMOTodos.Name = "rbMOTodos";
            rbMOTodos.Size = new Size(72, 19);
            rbMOTodos.TabIndex = 0;
            rbMOTodos.TabStop = true;
            rbMOTodos.Text = "📋 Todos";
            rbMOTodos.UseVisualStyleBackColor = true;
            rbMOTodos.CheckedChanged += rbFiltroMO_CheckedChanged;
            // 
            // rbMOIndividual
            // 
            rbMOIndividual.AutoSize = true;
            rbMOIndividual.Font = new Font("Segoe UI", 9F);
            rbMOIndividual.Location = new Point(100, 7);
            rbMOIndividual.Name = "rbMOIndividual";
            rbMOIndividual.Size = new Size(92, 19);
            rbMOIndividual.TabIndex = 1;
            rbMOIndividual.Text = "👤 Individual";
            rbMOIndividual.UseVisualStyleBackColor = true;
            rbMOIndividual.CheckedChanged += rbFiltroMO_CheckedChanged;
            // 
            // rbMOCuadrillas
            // 
            rbMOCuadrillas.AutoSize = true;
            rbMOCuadrillas.Font = new Font("Segoe UI", 9F);
            rbMOCuadrillas.Location = new Point(230, 7);
            rbMOCuadrillas.Name = "rbMOCuadrillas";
            rbMOCuadrillas.Size = new Size(92, 19);
            rbMOCuadrillas.TabIndex = 2;
            rbMOCuadrillas.Text = "👷 Cuadrillas";
            rbMOCuadrillas.UseVisualStyleBackColor = true;
            rbMOCuadrillas.CheckedChanged += rbFiltroMO_CheckedChanged;
            // 
            // btnNuevoInsumo
            // 
            btnNuevoInsumo.BackColor = Color.FromArgb(76, 175, 80);
            btnNuevoInsumo.FlatStyle = FlatStyle.Flat;
            btnNuevoInsumo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnNuevoInsumo.ForeColor = Color.White;
            btnNuevoInsumo.Location = new Point(829, 71);
            btnNuevoInsumo.Name = "btnNuevoInsumo";
            btnNuevoInsumo.Size = new Size(130, 30);
            btnNuevoInsumo.TabIndex = 8;
            btnNuevoInsumo.Text = "➕ Nuevo";
            btnNuevoInsumo.UseVisualStyleBackColor = false;
            btnNuevoInsumo.Click += btnNuevoInsumo_Click;
            // 
            // dgvInsumos
            // 
            dgvInsumos.AllowUserToAddRows = false;
            dgvInsumos.AllowUserToDeleteRows = false;
            dgvInsumos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvInsumos.BackgroundColor = Color.White;
            dgvInsumos.ColumnHeadersHeight = 35;
            dgvInsumos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvInsumos.Columns.AddRange(new DataGridViewColumn[] { colId, colClave, colDescripcion, colUnidad, colPrecio, colOrigen, colProyecto, colFecha });
            dgvInsumos.Location = new Point(20, 141);
            dgvInsumos.Name = "dgvInsumos";
            dgvInsumos.MultiSelect = true;
            dgvInsumos.RowHeadersVisible = false;
            dgvInsumos.RowTemplate.Height = 30;
            dgvInsumos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvInsumos.Size = new Size(939, 395);
            dgvInsumos.TabIndex = 4;
            // colId
            // 
            colId.HeaderText = "ID";
            colId.Name = "colId";
            colId.ReadOnly = true;
            colId.Visible = false;
            // 
            // colClave
            // 
            colClave.FillWeight = 48F;
            colClave.HeaderText = "CLAVE";
            colClave.Name = "colClave";
            colClave.ReadOnly = true;
            // 
            // colDescripcion
            // 
            colDescripcion.FillWeight = 165F;
            colDescripcion.HeaderText = "DESCRIPCIÓN";
            colDescripcion.Name = "colDescripcion";
            colDescripcion.ReadOnly = true;
            // 
            // colUnidad
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colUnidad.DefaultCellStyle = dataGridViewCellStyle1;
            colUnidad.FillWeight = 40F;
            colUnidad.HeaderText = "UNIDAD";
            colUnidad.Name = "colUnidad";
            colUnidad.ReadOnly = true;
            // 
            // colPrecio
            // 
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleRight;
            colPrecio.DefaultCellStyle = dataGridViewCellStyle2;
            colPrecio.FillWeight = 55F;
            colPrecio.HeaderText = "PRECIO";
            colPrecio.Name = "colPrecio";
            colPrecio.ReadOnly = true;
            // 
            // colOrigen
            // 
            colOrigen.FillWeight = 42F;
            colOrigen.HeaderText = "ORIGEN";
            colOrigen.Name = "colOrigen";
            colOrigen.ReadOnly = true;
            // 
            // colProyecto
            // 
            colProyecto.FillWeight = 95F;
            colProyecto.HeaderText = "PROYECTO";
            colProyecto.Name = "colProyecto";
            colProyecto.ReadOnly = true;
            // 
            // colFecha
            // 
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colFecha.DefaultCellStyle = dataGridViewCellStyle3;
            colFecha.FillWeight = 45F;
            colFecha.HeaderText = "FECHA";
            colFecha.Name = "colFecha";
            colFecha.ReadOnly = true;
            // 
            // panelBottom
            // 
            panelBottom.BackColor = Color.FromArgb(245, 245, 245);
            panelBottom.BorderStyle = BorderStyle.FixedSingle;
            panelBottom.Controls.Add(btnAceptar);
            panelBottom.Controls.Add(btnCancelar);
            panelBottom.Controls.Add(lblUnidad);
            panelBottom.Controls.Add(nudCantidad);
            panelBottom.Controls.Add(lblCantidadLabel);
            panelBottom.Location = new Point(20, 546);
            panelBottom.Name = "panelBottom";
            panelBottom.Size = new Size(939, 80);
            panelBottom.TabIndex = 5;
            // 
            // btnAceptar
            // 
            btnAceptar.BackColor = Color.FromArgb(76, 175, 80);
            btnAceptar.FlatStyle = FlatStyle.Flat;
            btnAceptar.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnAceptar.ForeColor = Color.White;
            btnAceptar.Location = new Point(789, 20);
            btnAceptar.Name = "btnAceptar";
            btnAceptar.Size = new Size(130, 40);
            btnAceptar.TabIndex = 6;
            btnAceptar.Text = "✓ Agregar";
            btnAceptar.UseVisualStyleBackColor = false;
            btnAceptar.Click += btnAceptar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.BackColor = Color.FromArgb(220, 220, 220);
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Location = new Point(679, 20);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(100, 40);
            btnCancelar.TabIndex = 5;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = false;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // lblUnidad
            // 
            lblUnidad.AutoSize = true;
            lblUnidad.Font = new Font("Segoe UI", 10F);
            lblUnidad.Location = new Point(230, 30);
            lblUnidad.Name = "lblUnidad";
            lblUnidad.Size = new Size(27, 19);
            lblUnidad.TabIndex = 2;
            lblUnidad.Text = "---";
            // 
            // nudCantidad
            // 
            nudCantidad.DecimalPlaces = 2;
            nudCantidad.Font = new Font("Segoe UI", 10F);
            nudCantidad.Location = new Point(100, 28);
            nudCantidad.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
            nudCantidad.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
            nudCantidad.Name = "nudCantidad";
            nudCantidad.Size = new Size(120, 25);
            nudCantidad.TabIndex = 1;
            nudCantidad.TextAlign = HorizontalAlignment.Right;
            nudCantidad.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // lblCantidadLabel
            // 
            lblCantidadLabel.AutoSize = true;
            lblCantidadLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblCantidadLabel.Location = new Point(15, 30);
            lblCantidadLabel.Name = "lblCantidadLabel";
            lblCantidadLabel.Size = new Size(73, 19);
            lblCantidadLabel.TabIndex = 0;
            lblCantidadLabel.Text = "Cantidad:";
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 632);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(980, 22);
            statusStrip.TabIndex = 6;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(32, 17);
            lblStatus.Text = "Listo";
            // 
            // ctxProyectoFavorito
            // 
            ctxProyectoFavorito.Items.AddRange(new ToolStripItem[] { mnuToggleFavorito });
            ctxProyectoFavorito.Name = "ctxProyectoFavorito";
            ctxProyectoFavorito.Size = new Size(251, 26);
            // 
            // mnuToggleFavorito
            // 
            mnuToggleFavorito.Name = "mnuToggleFavorito";
            mnuToggleFavorito.Size = new Size(250, 22);
            mnuToggleFavorito.Text = "Marcar proyecto origen como favorito";
            // 
            // FormSeleccionarInsumo
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(980, 654);
            Controls.Add(statusStrip);
            Controls.Add(panelBottom);
            Controls.Add(dgvInsumos);
            Controls.Add(btnNuevoInsumo);
            Controls.Add(panelFiltroMO);
            Controls.Add(cboProyecto);
            Controls.Add(lblProyecto);
            Controls.Add(txtBuscar);
            Controls.Add(lblBuscar);
            Controls.Add(panelTop);
            MinimumSize = new Size(940, 650);
            Name = "FormSeleccionarInsumo";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Seleccionar Insumo";
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelFiltroMO.ResumeLayout(false);
            panelFiltroMO.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvInsumos).EndInit();
            panelBottom.ResumeLayout(false);
            panelBottom.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudCantidad).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ctxProyectoFavorito.ResumeLayout(false);
            AcceptButton = btnAceptar;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Panel panelTop;
        private Label lblTitulo;
        private Label lblBuscar;
        private TextBox txtBuscar;
        private Label lblProyecto;
        private ComboBox cboProyecto;
        private Panel panelFiltroMO;
        private RadioButton rbMOTodos;
        private RadioButton rbMOIndividual;
        private RadioButton rbMOCuadrillas;
        private Button btnNuevoInsumo;
        private DataGridView dgvInsumos;
        private DataGridViewTextBoxColumn colId;
        private DataGridViewTextBoxColumn colClave;
        private DataGridViewTextBoxColumn colDescripcion;
        private DataGridViewTextBoxColumn colUnidad;
        private DataGridViewTextBoxColumn colPrecio;
        private DataGridViewTextBoxColumn colOrigen;
        private DataGridViewTextBoxColumn colProyecto;
        private DataGridViewTextBoxColumn colFecha;
        private Panel panelBottom;
        private Button btnAceptar;
        private Button btnCancelar;
        private Label lblUnidad;
        private NumericUpDown nudCantidad;
        private Label lblCantidadLabel;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ContextMenuStrip ctxProyectoFavorito;
        private ToolStripMenuItem mnuToggleFavorito;
    }
}
