namespace SOPRO.WinForms.Forms
{
    partial class FormColumnasPersonalizadas
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            panelTop = new Panel();
            lblTitulo = new Label();
            splitMain = new SplitContainer();
            dgvColumnas = new DataGridView();
            colId = new DataGridViewTextBoxColumn();
            colNombre = new DataGridViewTextBoxColumn();
            colTipo = new DataGridViewTextBoxColumn();
            colAncho = new DataGridViewTextBoxColumn();
            colVisible = new DataGridViewCheckBoxColumn();
            panelFormato = new Panel();
            lblColumnaSeleccionada = new Label();
            grpFuente = new GroupBox();
            lblFuente = new Label();
            cboFuente = new ComboBox();
            lblTamaño = new Label();
            nudTamaño = new NumericUpDown();
            chkNegrita = new CheckBox();
            chkCursiva = new CheckBox();
            grpAlineacion = new GroupBox();
            cboAlineacion = new ComboBox();
            grpColores = new GroupBox();
            lblColorFondo = new Label();
            btnColorFondo = new Button();
            lblColorTexto = new Label();
            btnColorTexto = new Button();
            grpPreview = new GroupBox();
            lblPreview = new Label();
            btnAplicarATodas = new Button();
            btnRestaurarFormato = new Button();
            panelButtons = new Panel();
            btnNueva = new Button();
            btnEliminar = new Button();
            btnPredeterminadas = new Button();
            btnCerrar = new Button();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvColumnas).BeginInit();
            panelFormato.SuspendLayout();
            grpFuente.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudTamaño).BeginInit();
            grpAlineacion.SuspendLayout();
            grpColores.SuspendLayout();
            grpPreview.SuspendLayout();
            panelButtons.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(103, 58, 183);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(820, 56);
            panelTop.TabIndex = 3;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(18, 16);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(313, 25);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "⚙  COLUMNAS PERSONALIZADAS";
            // 
            // splitMain
            // 
            splitMain.Dock = DockStyle.Fill;
            splitMain.FixedPanel = FixedPanel.Panel2;
            splitMain.Location = new Point(0, 56);
            splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            splitMain.Panel1.Controls.Add(dgvColumnas);
            splitMain.Panel1.Padding = new Padding(10, 8, 4, 0);
            // 
            // splitMain.Panel2
            // 
            splitMain.Panel2.Controls.Add(panelFormato);
            splitMain.Panel2.Padding = new Padding(4, 8, 10, 0);
            splitMain.Size = new Size(820, 480);
            splitMain.SplitterDistance = 720;
            splitMain.TabIndex = 0;
            // 
            // dgvColumnas
            // 
            dgvColumnas.AllowUserToAddRows = false;
            dgvColumnas.AllowUserToDeleteRows = false;
            dgvColumnas.BackgroundColor = Color.White;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(103, 58, 183);
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = Color.White;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvColumnas.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvColumnas.ColumnHeadersHeight = 32;
            dgvColumnas.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvColumnas.Columns.AddRange(new DataGridViewColumn[] { colId, colNombre, colTipo, colAncho, colVisible });
            dgvColumnas.Dock = DockStyle.Fill;
            dgvColumnas.EnableHeadersVisualStyles = false;
            dgvColumnas.Location = new Point(10, 8);
            dgvColumnas.MultiSelect = false;
            dgvColumnas.Name = "dgvColumnas";
            dgvColumnas.RowHeadersVisible = false;
            dgvColumnas.RowTemplate.Height = 28;
            dgvColumnas.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvColumnas.Size = new Size(706, 472);
            dgvColumnas.TabIndex = 0;
            dgvColumnas.CellValueChanged += DgvColumnas_CellValueChanged;
            dgvColumnas.CurrentCellDirtyStateChanged += DgvColumnas_CurrentCellDirtyStateChanged;
            dgvColumnas.SelectionChanged += dgvColumnas_SelectionChanged;
            // 
            // colId
            // 
            colId.HeaderText = "ID";
            colId.Name = "colId";
            colId.Visible = false;
            // 
            // colNombre
            // 
            colNombre.HeaderText = "NOMBRE";
            colNombre.Name = "colNombre";
            colNombre.Width = 180;
            // 
            // colTipo
            // 
            colTipo.HeaderText = "TIPO";
            colTipo.Name = "colTipo";
            colTipo.ReadOnly = true;
            colTipo.Width = 90;
            // 
            // colAncho
            // 
            colAncho.HeaderText = "ANCHO";
            colAncho.Name = "colAncho";
            colAncho.ReadOnly = true;
            colAncho.Width = 65;
            // 
            // colVisible
            // 
            colVisible.FalseValue = false;
            colVisible.HeaderText = "VIS.";
            colVisible.Name = "colVisible";
            colVisible.TrueValue = true;
            colVisible.Width = 42;
            // 
            // panelFormato
            // 
            panelFormato.Controls.Add(lblColumnaSeleccionada);
            panelFormato.Controls.Add(grpFuente);
            panelFormato.Controls.Add(grpAlineacion);
            panelFormato.Controls.Add(grpColores);
            panelFormato.Controls.Add(grpPreview);
            panelFormato.Controls.Add(btnAplicarATodas);
            panelFormato.Controls.Add(btnRestaurarFormato);
            panelFormato.Dock = DockStyle.Fill;
            panelFormato.Enabled = false;
            panelFormato.Location = new Point(4, 8);
            panelFormato.Name = "panelFormato";
            panelFormato.Size = new Size(82, 472);
            panelFormato.TabIndex = 0;
            // 
            // lblColumnaSeleccionada
            // 
            lblColumnaSeleccionada.Dock = DockStyle.Top;
            lblColumnaSeleccionada.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblColumnaSeleccionada.ForeColor = Color.FromArgb(103, 58, 183);
            lblColumnaSeleccionada.Location = new Point(0, 0);
            lblColumnaSeleccionada.Name = "lblColumnaSeleccionada";
            lblColumnaSeleccionada.Size = new Size(82, 28);
            lblColumnaSeleccionada.TabIndex = 0;
            lblColumnaSeleccionada.Text = "Selecciona una columna";
            lblColumnaSeleccionada.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // grpFuente
            // 
            grpFuente.Controls.Add(lblFuente);
            grpFuente.Controls.Add(cboFuente);
            grpFuente.Controls.Add(lblTamaño);
            grpFuente.Controls.Add(nudTamaño);
            grpFuente.Controls.Add(chkNegrita);
            grpFuente.Controls.Add(chkCursiva);
            grpFuente.Font = new Font("Segoe UI", 9F);
            grpFuente.Location = new Point(0, 32);
            grpFuente.Name = "grpFuente";
            grpFuente.Size = new Size(280, 130);
            grpFuente.TabIndex = 1;
            grpFuente.TabStop = false;
            grpFuente.Text = "Fuente";
            // 
            // lblFuente
            // 
            lblFuente.AutoSize = true;
            lblFuente.Location = new Point(10, 24);
            lblFuente.Name = "lblFuente";
            lblFuente.Size = new Size(33, 15);
            lblFuente.TabIndex = 0;
            lblFuente.Text = "Tipo:";
            // 
            // cboFuente
            // 
            cboFuente.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFuente.Location = new Point(50, 21);
            cboFuente.Name = "cboFuente";
            cboFuente.Size = new Size(218, 23);
            cboFuente.TabIndex = 1;
            cboFuente.SelectedIndexChanged += cboFuente_SelectedIndexChanged;
            // 
            // lblTamaño
            // 
            lblTamaño.AutoSize = true;
            lblTamaño.Location = new Point(10, 54);
            lblTamaño.Name = "lblTamaño";
            lblTamaño.Size = new Size(52, 15);
            lblTamaño.TabIndex = 2;
            lblTamaño.Text = "Tamaño:";
            // 
            // nudTamaño
            // 
            nudTamaño.Location = new Point(68, 51);
            nudTamaño.Maximum = new decimal(new int[] { 24, 0, 0, 0 });
            nudTamaño.Minimum = new decimal(new int[] { 6, 0, 0, 0 });
            nudTamaño.Name = "nudTamaño";
            nudTamaño.Size = new Size(56, 23);
            nudTamaño.TabIndex = 3;
            nudTamaño.Value = new decimal(new int[] { 9, 0, 0, 0 });
            nudTamaño.ValueChanged += nudTamaño_ValueChanged;
            // 
            // chkNegrita
            // 
            chkNegrita.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            chkNegrita.Location = new Point(10, 86);
            chkNegrita.Name = "chkNegrita";
            chkNegrita.Size = new Size(72, 22);
            chkNegrita.TabIndex = 4;
            chkNegrita.Text = "Negrita";
            chkNegrita.CheckedChanged += chkNegrita_CheckedChanged;
            // 
            // chkCursiva
            // 
            chkCursiva.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            chkCursiva.Location = new Point(90, 86);
            chkCursiva.Name = "chkCursiva";
            chkCursiva.Size = new Size(70, 22);
            chkCursiva.TabIndex = 5;
            chkCursiva.Text = "Cursiva";
            chkCursiva.CheckedChanged += chkCursiva_CheckedChanged;
            // 
            // grpAlineacion
            // 
            grpAlineacion.Controls.Add(cboAlineacion);
            grpAlineacion.Font = new Font("Segoe UI", 9F);
            grpAlineacion.Location = new Point(0, 170);
            grpAlineacion.Name = "grpAlineacion";
            grpAlineacion.Size = new Size(280, 56);
            grpAlineacion.TabIndex = 2;
            grpAlineacion.TabStop = false;
            grpAlineacion.Text = "Alineación";
            // 
            // cboAlineacion
            // 
            cboAlineacion.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAlineacion.Items.AddRange(new object[] { "Izquierda", "Centro", "Derecha", "Justificado" });
            cboAlineacion.Location = new Point(10, 22);
            cboAlineacion.Name = "cboAlineacion";
            cboAlineacion.Size = new Size(258, 23);
            cboAlineacion.TabIndex = 0;
            cboAlineacion.SelectedIndexChanged += cboAlineacion_SelectedIndexChanged;
            // 
            // grpColores
            // 
            grpColores.Controls.Add(lblColorFondo);
            grpColores.Controls.Add(btnColorFondo);
            grpColores.Controls.Add(lblColorTexto);
            grpColores.Controls.Add(btnColorTexto);
            grpColores.Font = new Font("Segoe UI", 9F);
            grpColores.Location = new Point(0, 234);
            grpColores.Name = "grpColores";
            grpColores.Size = new Size(280, 80);
            grpColores.TabIndex = 3;
            grpColores.TabStop = false;
            grpColores.Text = "Colores";
            // 
            // lblColorFondo
            // 
            lblColorFondo.AutoSize = true;
            lblColorFondo.Location = new Point(10, 26);
            lblColorFondo.Name = "lblColorFondo";
            lblColorFondo.Size = new Size(44, 15);
            lblColorFondo.TabIndex = 0;
            lblColorFondo.Text = "Fondo:";
            // 
            // btnColorFondo
            // 
            btnColorFondo.BackColor = Color.White;
            btnColorFondo.FlatStyle = FlatStyle.Flat;
            btnColorFondo.Location = new Point(58, 22);
            btnColorFondo.Name = "btnColorFondo";
            btnColorFondo.Size = new Size(96, 26);
            btnColorFondo.TabIndex = 1;
            btnColorFondo.Text = "Elegir color";
            btnColorFondo.UseVisualStyleBackColor = false;
            btnColorFondo.Click += btnColorFondo_Click;
            // 
            // lblColorTexto
            // 
            lblColorTexto.AutoSize = true;
            lblColorTexto.Location = new Point(10, 54);
            lblColorTexto.Name = "lblColorTexto";
            lblColorTexto.Size = new Size(38, 15);
            lblColorTexto.TabIndex = 2;
            lblColorTexto.Text = "Texto:";
            // 
            // btnColorTexto
            // 
            btnColorTexto.BackColor = Color.Black;
            btnColorTexto.FlatStyle = FlatStyle.Flat;
            btnColorTexto.ForeColor = Color.White;
            btnColorTexto.Location = new Point(58, 50);
            btnColorTexto.Name = "btnColorTexto";
            btnColorTexto.Size = new Size(96, 26);
            btnColorTexto.TabIndex = 3;
            btnColorTexto.Text = "Elegir color";
            btnColorTexto.UseVisualStyleBackColor = false;
            btnColorTexto.Click += btnColorTexto_Click;
            // 
            // grpPreview
            // 
            grpPreview.Controls.Add(lblPreview);
            grpPreview.Font = new Font("Segoe UI", 9F);
            grpPreview.Location = new Point(0, 322);
            grpPreview.Name = "grpPreview";
            grpPreview.Size = new Size(280, 60);
            grpPreview.TabIndex = 4;
            grpPreview.TabStop = false;
            grpPreview.Text = "Vista previa";
            // 
            // lblPreview
            // 
            lblPreview.BackColor = Color.White;
            lblPreview.Dock = DockStyle.Fill;
            lblPreview.Location = new Point(3, 19);
            lblPreview.Name = "lblPreview";
            lblPreview.Size = new Size(274, 38);
            lblPreview.TabIndex = 0;
            lblPreview.Text = "AaBbCc 123.45";
            lblPreview.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnAplicarATodas
            // 
            btnAplicarATodas.BackColor = Color.FromArgb(103, 58, 183);
            btnAplicarATodas.FlatStyle = FlatStyle.Flat;
            btnAplicarATodas.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAplicarATodas.ForeColor = Color.White;
            btnAplicarATodas.Location = new Point(0, 392);
            btnAplicarATodas.Name = "btnAplicarATodas";
            btnAplicarATodas.Size = new Size(280, 28);
            btnAplicarATodas.TabIndex = 5;
            btnAplicarATodas.Text = "Aplicar fuente a todas";
            btnAplicarATodas.UseVisualStyleBackColor = false;
            btnAplicarATodas.Click += btnAplicarATodas_Click;
            // 
            // btnRestaurarFormato
            // 
            btnRestaurarFormato.FlatStyle = FlatStyle.Flat;
            btnRestaurarFormato.Location = new Point(0, 428);
            btnRestaurarFormato.Name = "btnRestaurarFormato";
            btnRestaurarFormato.Size = new Size(280, 28);
            btnRestaurarFormato.TabIndex = 6;
            btnRestaurarFormato.Text = "Restaurar formato por defecto";
            btnRestaurarFormato.Click += btnRestaurarFormato_Click;
            // 
            // panelButtons
            // 
            panelButtons.BackColor = Color.FromArgb(245, 245, 248);
            panelButtons.Controls.Add(btnNueva);
            panelButtons.Controls.Add(btnEliminar);
            panelButtons.Controls.Add(btnPredeterminadas);
            panelButtons.Controls.Add(btnCerrar);
            panelButtons.Dock = DockStyle.Bottom;
            panelButtons.Location = new Point(0, 558);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(820, 52);
            panelButtons.TabIndex = 2;
            // 
            // btnNueva
            // 
            btnNueva.FlatStyle = FlatStyle.Flat;
            btnNueva.Location = new Point(10, 12);
            btnNueva.Name = "btnNueva";
            btnNueva.Size = new Size(90, 28);
            btnNueva.TabIndex = 0;
            btnNueva.Text = "+ Nueva";
            btnNueva.Click += btnNueva_Click;
            // 
            // btnEliminar
            // 
            btnEliminar.FlatStyle = FlatStyle.Flat;
            btnEliminar.Location = new Point(108, 12);
            btnEliminar.Name = "btnEliminar";
            btnEliminar.Size = new Size(90, 28);
            btnEliminar.TabIndex = 1;
            btnEliminar.Text = "Eliminar";
            btnEliminar.Click += btnEliminar_Click;
            // 
            // btnPredeterminadas
            // 
            btnPredeterminadas.FlatStyle = FlatStyle.Flat;
            btnPredeterminadas.Location = new Point(206, 12);
            btnPredeterminadas.Name = "btnPredeterminadas";
            btnPredeterminadas.Size = new Size(130, 28);
            btnPredeterminadas.TabIndex = 2;
            btnPredeterminadas.Text = "Predeterminadas";
            btnPredeterminadas.Click += btnPredeterminadas_Click;
            // 
            // btnCerrar
            // 
            btnCerrar.BackColor = Color.FromArgb(103, 58, 183);
            btnCerrar.FlatStyle = FlatStyle.Flat;
            btnCerrar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCerrar.ForeColor = Color.White;
            btnCerrar.Location = new Point(680, 12);
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(90, 28);
            btnCerrar.TabIndex = 3;
            btnCerrar.Text = "Cerrar";
            btnCerrar.UseVisualStyleBackColor = false;
            btnCerrar.Click += btnCerrar_Click;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 536);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(820, 22);
            statusStrip.TabIndex = 1;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(0, 17);
            // 
            // FormColumnasPersonalizadas
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(820, 610);
            Controls.Add(splitMain);
            Controls.Add(statusStrip);
            Controls.Add(panelButtons);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(820, 580);
            Name = "FormColumnasPersonalizadas";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Columnas Personalizadas";
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvColumnas).EndInit();
            panelFormato.ResumeLayout(false);
            grpFuente.ResumeLayout(false);
            grpFuente.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudTamaño).EndInit();
            grpAlineacion.ResumeLayout(false);
            grpColores.ResumeLayout(false);
            grpColores.PerformLayout();
            grpPreview.ResumeLayout(false);
            panelButtons.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion

        private System.Windows.Forms.Panel         panelTop, panelButtons, panelFormato;
        private System.Windows.Forms.Label         lblTitulo, lblColumnaSeleccionada;
        private System.Windows.Forms.Label         lblFuente, lblTamaño, lblColorFondo, lblColorTexto;
        private System.Windows.Forms.Label         lblPreview;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.DataGridView  dgvColumnas;
        private System.Windows.Forms.DataGridViewTextBoxColumn  colId, colNombre, colTipo, colAncho;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colVisible;
        private System.Windows.Forms.GroupBox      grpFuente, grpAlineacion, grpColores, grpPreview;
        private System.Windows.Forms.ComboBox      cboFuente, cboAlineacion;
        private System.Windows.Forms.NumericUpDown nudTamaño;
        private System.Windows.Forms.CheckBox      chkNegrita, chkCursiva;
        private System.Windows.Forms.Button        btnColorFondo, btnColorTexto;
        private System.Windows.Forms.Button        btnAplicarATodas, btnRestaurarFormato;
        private System.Windows.Forms.Button        btnNueva, btnEliminar, btnPredeterminadas, btnCerrar;
        private System.Windows.Forms.StatusStrip   statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
