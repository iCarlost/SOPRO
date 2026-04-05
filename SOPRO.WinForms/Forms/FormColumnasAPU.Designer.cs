namespace SOPRO.WinForms.Forms
{
    partial class FormColumnasAPU
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            panelTop = new Panel();
            lblTitulo = new Label();
            splitMain = new SplitContainer();
            dgvColumnas = new DataGridView();
            colId = new DataGridViewTextBoxColumn();
            colNombreInterno = new DataGridViewTextBoxColumn();
            colEncabezado = new DataGridViewTextBoxColumn();
            colOrden = new DataGridViewTextBoxColumn();
            colAncho = new DataGridViewTextBoxColumn();
            colVisible = new DataGridViewCheckBoxColumn();
            panelFormato = new Panel();
            lblColumnaSeleccionada = new Label();
            grpEncabezado = new GroupBox();
            lblFuente = new Label();
            cboFuente = new ComboBox();
            lblTamano = new Label();
            nudTamaño = new NumericUpDown();
            chkNegrita = new CheckBox();
            lblColorFondo = new Label();
            btnColorFondo = new Button();
            lblColorTexto = new Label();
            btnColorTexto = new Button();
            grpContenido = new GroupBox();
            lblFuenteContenido = new Label();
            cboFuenteContenido = new ComboBox();
            lblTamanoContenido = new Label();
            nudTamañoContenido = new NumericUpDown();
            lblAlineacion = new Label();
            cboAlineacion = new ComboBox();
            lblColorFondoContenido = new Label();
            btnColorFondoContenido = new Button();
            lblColorTextoContenido = new Label();
            btnColorTextoContenido = new Button();
            grpPreview = new GroupBox();
            lblPreviewEnc = new Label();
            lblPreviewCon = new Label();
            btnRestaurar = new Button();
            panelButtons = new Panel();
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
            grpEncabezado.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudTamaño).BeginInit();
            grpContenido.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudTamañoContenido).BeginInit();
            grpPreview.SuspendLayout();
            panelButtons.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(13, 71, 161);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(960, 52);
            panelTop.TabIndex = 3;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(16, 14);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(299, 25);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "📐  COLUMNAS — REPORTE APU";
            // 
            // splitMain
            // 
            splitMain.Dock = DockStyle.Fill;
            splitMain.FixedPanel = FixedPanel.Panel2;
            splitMain.Location = new Point(0, 52);
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
            splitMain.Size = new Size(960, 489);
            splitMain.SplitterDistance = 620;
            splitMain.TabIndex = 0;
            // 
            // dgvColumnas
            // 
            dgvColumnas.AllowUserToAddRows = false;
            dgvColumnas.AllowUserToDeleteRows = false;
            dgvColumnas.BackgroundColor = Color.White;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(13, 71, 161);
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = Color.White;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvColumnas.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvColumnas.ColumnHeadersHeight = 32;
            dgvColumnas.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvColumnas.Columns.AddRange(new DataGridViewColumn[] { colId, colNombreInterno, colEncabezado, colOrden, colAncho, colVisible });
            dgvColumnas.Dock = DockStyle.Fill;
            dgvColumnas.EnableHeadersVisualStyles = false;
            dgvColumnas.Location = new Point(10, 8);
            dgvColumnas.MultiSelect = false;
            dgvColumnas.Name = "dgvColumnas";
            dgvColumnas.RowHeadersVisible = false;
            dgvColumnas.RowTemplate.Height = 28;
            dgvColumnas.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvColumnas.Size = new Size(606, 481);
            dgvColumnas.TabIndex = 0;
            dgvColumnas.CellEndEdit += DgvColumnas_CellEndEdit;
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
            // colNombreInterno
            // 
            colNombreInterno.HeaderText = "NOMBRE INTERNO";
            colNombreInterno.Name = "colNombreInterno";
            colNombreInterno.ReadOnly = true;
            colNombreInterno.Width = 140;
            // 
            // colEncabezado
            // 
            colEncabezado.HeaderText = "ENCABEZADO";
            colEncabezado.Name = "colEncabezado";
            colEncabezado.Width = 150;
            // 
            // colOrden
            // 
            colOrden.HeaderText = "ORDEN";
            colOrden.Name = "colOrden";
            colOrden.Width = 58;
            // 
            // colAncho
            // 
            colAncho.HeaderText = "ANCHO";
            colAncho.Name = "colAncho";
            colAncho.Width = 58;
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
            panelFormato.Controls.Add(grpEncabezado);
            panelFormato.Controls.Add(grpContenido);
            panelFormato.Controls.Add(grpPreview);
            panelFormato.Controls.Add(btnRestaurar);
            panelFormato.Dock = DockStyle.Fill;
            panelFormato.Enabled = false;
            panelFormato.Location = new Point(4, 8);
            panelFormato.Name = "panelFormato";
            panelFormato.Size = new Size(322, 481);
            panelFormato.TabIndex = 0;
            // 
            // lblColumnaSeleccionada
            // 
            lblColumnaSeleccionada.Dock = DockStyle.Top;
            lblColumnaSeleccionada.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblColumnaSeleccionada.ForeColor = Color.FromArgb(13, 71, 161);
            lblColumnaSeleccionada.Location = new Point(0, 0);
            lblColumnaSeleccionada.Name = "lblColumnaSeleccionada";
            lblColumnaSeleccionada.Size = new Size(322, 26);
            lblColumnaSeleccionada.TabIndex = 0;
            lblColumnaSeleccionada.Text = "Selecciona una columna";
            lblColumnaSeleccionada.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // grpEncabezado
            // 
            grpEncabezado.Controls.Add(lblFuente);
            grpEncabezado.Controls.Add(cboFuente);
            grpEncabezado.Controls.Add(lblTamano);
            grpEncabezado.Controls.Add(nudTamaño);
            grpEncabezado.Controls.Add(chkNegrita);
            grpEncabezado.Controls.Add(lblColorFondo);
            grpEncabezado.Controls.Add(btnColorFondo);
            grpEncabezado.Controls.Add(lblColorTexto);
            grpEncabezado.Controls.Add(btnColorTexto);
            grpEncabezado.Font = new Font("Segoe UI", 9F);
            grpEncabezado.Location = new Point(0, 30);
            grpEncabezado.Name = "grpEncabezado";
            grpEncabezado.Size = new Size(316, 148);
            grpEncabezado.TabIndex = 1;
            grpEncabezado.TabStop = false;
            grpEncabezado.Text = "Encabezado de columna (Excel)";
            // 
            // lblFuente
            // 
            lblFuente.AutoSize = true;
            lblFuente.Location = new Point(8, 24);
            lblFuente.Name = "lblFuente";
            lblFuente.Size = new Size(34, 15);
            lblFuente.TabIndex = 0;
            lblFuente.Text = "Tipo:";
            // 
            // cboFuente
            // 
            cboFuente.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFuente.Location = new Point(48, 21);
            cboFuente.Name = "cboFuente";
            cboFuente.Size = new Size(256, 23);
            cboFuente.TabIndex = 1;
            cboFuente.SelectedIndexChanged += cboFuente_SelectedIndexChanged;
            // 
            // lblTamano
            // 
            lblTamano.AutoSize = true;
            lblTamano.Location = new Point(8, 52);
            lblTamano.Name = "lblTamano";
            lblTamano.Size = new Size(53, 15);
            lblTamano.TabIndex = 2;
            lblTamano.Text = "Tamaño:";
            // 
            // nudTamaño
            // 
            nudTamaño.Location = new Point(68, 49);
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
            chkNegrita.Location = new Point(8, 80);
            chkNegrita.Name = "chkNegrita";
            chkNegrita.Size = new Size(72, 22);
            chkNegrita.TabIndex = 4;
            chkNegrita.Text = "Negrita";
            chkNegrita.CheckedChanged += chkNegrita_CheckedChanged;
            // 
            // lblColorFondo
            // 
            lblColorFondo.AutoSize = true;
            lblColorFondo.Location = new Point(8, 108);
            lblColorFondo.Name = "lblColorFondo";
            lblColorFondo.Size = new Size(44, 15);
            lblColorFondo.TabIndex = 5;
            lblColorFondo.Text = "Fondo:";
            // 
            // btnColorFondo
            // 
            btnColorFondo.BackColor = Color.FromArgb(21, 101, 192);
            btnColorFondo.FlatStyle = FlatStyle.Flat;
            btnColorFondo.Location = new Point(58, 104);
            btnColorFondo.Name = "btnColorFondo";
            btnColorFondo.Size = new Size(80, 26);
            btnColorFondo.TabIndex = 6;
            btnColorFondo.Text = "Color";
            btnColorFondo.UseVisualStyleBackColor = false;
            btnColorFondo.Click += btnColorFondo_Click;
            // 
            // lblColorTexto
            // 
            lblColorTexto.AutoSize = true;
            lblColorTexto.Location = new Point(150, 108);
            lblColorTexto.Name = "lblColorTexto";
            lblColorTexto.Size = new Size(38, 15);
            lblColorTexto.TabIndex = 7;
            lblColorTexto.Text = "Texto:";
            // 
            // btnColorTexto
            // 
            btnColorTexto.BackColor = Color.White;
            btnColorTexto.FlatStyle = FlatStyle.Flat;
            btnColorTexto.Location = new Point(196, 104);
            btnColorTexto.Name = "btnColorTexto";
            btnColorTexto.Size = new Size(80, 26);
            btnColorTexto.TabIndex = 8;
            btnColorTexto.Text = "Color";
            btnColorTexto.UseVisualStyleBackColor = false;
            btnColorTexto.Click += btnColorTexto_Click;
            // 
            // grpContenido
            // 
            grpContenido.Controls.Add(lblFuenteContenido);
            grpContenido.Controls.Add(cboFuenteContenido);
            grpContenido.Controls.Add(lblTamanoContenido);
            grpContenido.Controls.Add(nudTamañoContenido);
            grpContenido.Controls.Add(lblAlineacion);
            grpContenido.Controls.Add(cboAlineacion);
            grpContenido.Controls.Add(lblColorFondoContenido);
            grpContenido.Controls.Add(btnColorFondoContenido);
            grpContenido.Controls.Add(lblColorTextoContenido);
            grpContenido.Controls.Add(btnColorTextoContenido);
            grpContenido.Font = new Font("Segoe UI", 9F);
            grpContenido.Location = new Point(0, 186);
            grpContenido.Name = "grpContenido";
            grpContenido.Size = new Size(316, 162);
            grpContenido.TabIndex = 2;
            grpContenido.TabStop = false;
            grpContenido.Text = "Contenido (celdas de datos)";
            // 
            // lblFuenteContenido
            // 
            lblFuenteContenido.AutoSize = true;
            lblFuenteContenido.Location = new Point(8, 24);
            lblFuenteContenido.Name = "lblFuenteContenido";
            lblFuenteContenido.Size = new Size(34, 15);
            lblFuenteContenido.TabIndex = 0;
            lblFuenteContenido.Text = "Tipo:";
            // 
            // cboFuenteContenido
            // 
            cboFuenteContenido.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFuenteContenido.Location = new Point(48, 21);
            cboFuenteContenido.Name = "cboFuenteContenido";
            cboFuenteContenido.Size = new Size(256, 23);
            cboFuenteContenido.TabIndex = 1;
            cboFuenteContenido.SelectedIndexChanged += cboFuenteContenido_SelectedIndexChanged;
            // 
            // lblTamanoContenido
            // 
            lblTamanoContenido.AutoSize = true;
            lblTamanoContenido.Location = new Point(8, 52);
            lblTamanoContenido.Name = "lblTamanoContenido";
            lblTamanoContenido.Size = new Size(53, 15);
            lblTamanoContenido.TabIndex = 2;
            lblTamanoContenido.Text = "Tamaño:";
            // 
            // nudTamañoContenido
            // 
            nudTamañoContenido.Location = new Point(68, 49);
            nudTamañoContenido.Maximum = new decimal(new int[] { 24, 0, 0, 0 });
            nudTamañoContenido.Minimum = new decimal(new int[] { 6, 0, 0, 0 });
            nudTamañoContenido.Name = "nudTamañoContenido";
            nudTamañoContenido.Size = new Size(56, 23);
            nudTamañoContenido.TabIndex = 3;
            nudTamañoContenido.Value = new decimal(new int[] { 9, 0, 0, 0 });
            nudTamañoContenido.ValueChanged += nudTamañoContenido_ValueChanged;
            // 
            // lblAlineacion
            // 
            lblAlineacion.AutoSize = true;
            lblAlineacion.Location = new Point(8, 80);
            lblAlineacion.Name = "lblAlineacion";
            lblAlineacion.Size = new Size(66, 15);
            lblAlineacion.TabIndex = 4;
            lblAlineacion.Text = "Alineación:";
            // 
            // cboAlineacion
            // 
            cboAlineacion.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAlineacion.Items.AddRange(new object[] { "Izquierda", "Centro", "Derecha" });
            cboAlineacion.Location = new Point(78, 77);
            cboAlineacion.Name = "cboAlineacion";
            cboAlineacion.Size = new Size(120, 23);
            cboAlineacion.TabIndex = 5;
            cboAlineacion.SelectedIndexChanged += cboAlineacion_SelectedIndexChanged;
            // 
            // lblColorFondoContenido
            // 
            lblColorFondoContenido.AutoSize = true;
            lblColorFondoContenido.Location = new Point(8, 108);
            lblColorFondoContenido.Name = "lblColorFondoContenido";
            lblColorFondoContenido.Size = new Size(44, 15);
            lblColorFondoContenido.TabIndex = 6;
            lblColorFondoContenido.Text = "Fondo:";
            // 
            // btnColorFondoContenido
            // 
            btnColorFondoContenido.BackColor = Color.White;
            btnColorFondoContenido.FlatStyle = FlatStyle.Flat;
            btnColorFondoContenido.Location = new Point(58, 104);
            btnColorFondoContenido.Name = "btnColorFondoContenido";
            btnColorFondoContenido.Size = new Size(80, 26);
            btnColorFondoContenido.TabIndex = 7;
            btnColorFondoContenido.Text = "Color";
            btnColorFondoContenido.UseVisualStyleBackColor = false;
            btnColorFondoContenido.Click += btnColorFondoContenido_Click;
            // 
            // lblColorTextoContenido
            // 
            lblColorTextoContenido.AutoSize = true;
            lblColorTextoContenido.Location = new Point(150, 108);
            lblColorTextoContenido.Name = "lblColorTextoContenido";
            lblColorTextoContenido.Size = new Size(38, 15);
            lblColorTextoContenido.TabIndex = 8;
            lblColorTextoContenido.Text = "Texto:";
            // 
            // btnColorTextoContenido
            // 
            btnColorTextoContenido.BackColor = Color.Black;
            btnColorTextoContenido.FlatStyle = FlatStyle.Flat;
            btnColorTextoContenido.ForeColor = Color.White;
            btnColorTextoContenido.Location = new Point(196, 104);
            btnColorTextoContenido.Name = "btnColorTextoContenido";
            btnColorTextoContenido.Size = new Size(80, 26);
            btnColorTextoContenido.TabIndex = 9;
            btnColorTextoContenido.Text = "Color";
            btnColorTextoContenido.UseVisualStyleBackColor = false;
            btnColorTextoContenido.Click += btnColorTextoContenido_Click;
            // 
            // grpPreview
            // 
            grpPreview.Controls.Add(lblPreviewEnc);
            grpPreview.Controls.Add(lblPreviewCon);
            grpPreview.Font = new Font("Segoe UI", 9F);
            grpPreview.Location = new Point(0, 356);
            grpPreview.Name = "grpPreview";
            grpPreview.Size = new Size(316, 80);
            grpPreview.TabIndex = 3;
            grpPreview.TabStop = false;
            grpPreview.Text = "Vista previa";
            // 
            // lblPreviewEnc
            // 
            lblPreviewEnc.BackColor = Color.FromArgb(21, 101, 192);
            lblPreviewEnc.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblPreviewEnc.ForeColor = Color.White;
            lblPreviewEnc.Location = new Point(3, 18);
            lblPreviewEnc.Name = "lblPreviewEnc";
            lblPreviewEnc.Size = new Size(310, 24);
            lblPreviewEnc.TabIndex = 0;
            lblPreviewEnc.Text = "ENCABEZADO";
            lblPreviewEnc.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblPreviewCon
            // 
            lblPreviewCon.BackColor = Color.White;
            lblPreviewCon.ForeColor = Color.Black;
            lblPreviewCon.Location = new Point(3, 42);
            lblPreviewCon.Name = "lblPreviewCon";
            lblPreviewCon.Size = new Size(310, 24);
            lblPreviewCon.TabIndex = 1;
            lblPreviewCon.Text = "AaBbCc  123.4567";
            lblPreviewCon.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnRestaurar
            // 
            btnRestaurar.FlatStyle = FlatStyle.Flat;
            btnRestaurar.Location = new Point(0, 444);
            btnRestaurar.Name = "btnRestaurar";
            btnRestaurar.Size = new Size(316, 28);
            btnRestaurar.TabIndex = 4;
            btnRestaurar.Text = "Restaurar formato por defecto";
            btnRestaurar.Click += btnRestaurar_Click;
            // 
            // panelButtons
            // 
            panelButtons.BackColor = Color.FromArgb(245, 245, 248);
            panelButtons.Controls.Add(btnCerrar);
            panelButtons.Dock = DockStyle.Bottom;
            panelButtons.Location = new Point(0, 563);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(960, 50);
            panelButtons.TabIndex = 2;
            // 
            // btnCerrar
            // 
            btnCerrar.BackColor = Color.FromArgb(13, 71, 161);
            btnCerrar.FlatStyle = FlatStyle.Flat;
            btnCerrar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCerrar.ForeColor = Color.White;
            btnCerrar.Location = new Point(850, 11);
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(100, 28);
            btnCerrar.TabIndex = 0;
            btnCerrar.Text = "Cerrar";
            btnCerrar.UseVisualStyleBackColor = false;
            btnCerrar.Click += btnCerrar_Click;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 541);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(960, 22);
            statusStrip.TabIndex = 1;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(0, 17);
            // 
            // FormColumnasAPU
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(960, 613);
            Controls.Add(splitMain);
            Controls.Add(statusStrip);
            Controls.Add(panelButtons);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(800, 560);
            Name = "FormColumnasAPU";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Configurar Columnas — Reporte APU";
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvColumnas).EndInit();
            panelFormato.ResumeLayout(false);
            grpEncabezado.ResumeLayout(false);
            grpEncabezado.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudTamaño).EndInit();
            grpContenido.ResumeLayout(false);
            grpContenido.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudTamañoContenido).EndInit();
            grpPreview.ResumeLayout(false);
            panelButtons.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        // Controles
        private System.Windows.Forms.Panel              panelTop, panelButtons, panelFormato;
        private System.Windows.Forms.Label              lblTitulo, lblColumnaSeleccionada;
        private System.Windows.Forms.Label              lblFuente, lblTamano, lblColorFondo, lblColorTexto;
        private System.Windows.Forms.Label              lblFuenteContenido, lblTamanoContenido, lblAlineacion;
        private System.Windows.Forms.Label              lblColorFondoContenido, lblColorTextoContenido;
        private System.Windows.Forms.Label              lblPreviewEnc, lblPreviewCon;
        private System.Windows.Forms.SplitContainer     splitMain;
        private System.Windows.Forms.DataGridView       dgvColumnas;
        private System.Windows.Forms.DataGridViewTextBoxColumn  colId, colNombreInterno, colEncabezado, colOrden, colAncho;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colVisible;
        private System.Windows.Forms.GroupBox           grpEncabezado, grpContenido, grpPreview;
        private System.Windows.Forms.ComboBox           cboFuente, cboFuenteContenido, cboAlineacion;
        private System.Windows.Forms.NumericUpDown      nudTamaño, nudTamañoContenido;
        private System.Windows.Forms.CheckBox           chkNegrita;
        private System.Windows.Forms.Button             btnColorFondo, btnColorTexto;
        private System.Windows.Forms.Button             btnColorFondoContenido, btnColorTextoContenido;
        private System.Windows.Forms.Button             btnRestaurar, btnCerrar;
        private System.Windows.Forms.StatusStrip        statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
