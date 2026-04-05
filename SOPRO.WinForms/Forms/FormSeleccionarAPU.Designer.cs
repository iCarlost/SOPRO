namespace SOPRO.WinForms.Forms
{
    partial class FormSeleccionarAPU
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            panelTop = new Panel();
            lblTitulo = new Label();
            lblBuscar = new Label();
            txtBuscar = new TextBox();
            _cboTipo = new ComboBox();
            _cboProyecto = new ComboBox();
            dgvMatrices = new DataGridView();
            colId = new DataGridViewTextBoxColumn();
            colTipo = new DataGridViewTextBoxColumn();
            colClave = new DataGridViewTextBoxColumn();
            colDescripcion = new DataGridViewTextBoxColumn();
            colUnidad = new DataGridViewTextBoxColumn();
            colCosto = new DataGridViewTextBoxColumn();
            colOrigen = new DataGridViewTextBoxColumn();
            colProyecto = new DataGridViewTextBoxColumn();
            colFecha = new DataGridViewTextBoxColumn();
            panelInfo = new Panel();
            lblImporte = new Label();
            lblImporteLabel = new Label();
            lblCostoUnitario = new Label();
            lblCostoLabel = new Label();
            lblUnidad = new Label();
            lblUnidadLabel = new Label();
            nudCantidad = new NumericUpDown();
            lblCantidadLabel = new Label();
            btnAceptar = new Button();
            btnEditarMatriz = new Button();
            btnNuevaMatriz = new Button();
            btnCancelar = new Button();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            ctxProyectoFavorito = new ContextMenuStrip(components);
            mnuToggleFavorito = new ToolStripMenuItem();
            panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvMatrices).BeginInit();
            panelInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudCantidad).BeginInit();
            statusStrip.SuspendLayout();
            ctxProyectoFavorito.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(156, 39, 176);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(884, 60);
            panelTop.TabIndex = 0;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(20, 18);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(348, 30);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "📊 SELECCIONAR MATRIZ (APU)";
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
            txtBuscar.Size = new Size(291, 25);
            txtBuscar.TabIndex = 2;
            txtBuscar.TextChanged += txtBuscar_TextChanged;
            // 
            // _cboTipo
            // 
            _cboTipo.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboTipo.Font = new Font("Segoe UI", 9F);
            _cboTipo.FormattingEnabled = true;
            _cboTipo.Location = new Point(392, 74);
            _cboTipo.Name = "_cboTipo";
            _cboTipo.Size = new Size(84, 23);
            _cboTipo.TabIndex = 3;
            _cboTipo.Visible = false;
            // 
            // _cboProyecto
            // 
            _cboProyecto.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboProyecto.Font = new Font("Segoe UI", 9F);
            _cboProyecto.FormattingEnabled = true;
            _cboProyecto.Location = new Point(482, 74);
            _cboProyecto.Name = "_cboProyecto";
            _cboProyecto.Size = new Size(207, 23);
            _cboProyecto.TabIndex = 4;
            _cboProyecto.SelectedIndexChanged += cboProyecto_SelectedIndexChanged;
            // 
            // dgvMatrices
            // 
            dgvMatrices.AllowUserToAddRows = false;
            dgvMatrices.AllowUserToDeleteRows = false;
            dgvMatrices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvMatrices.BackgroundColor = Color.White;
            dgvMatrices.ColumnHeadersHeight = 35;
            dgvMatrices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvMatrices.Columns.AddRange(new DataGridViewColumn[] { colId, colTipo, colClave, colDescripcion, colUnidad, colCosto, colOrigen, colProyecto, colFecha });
            dgvMatrices.Location = new Point(20, 109);
            dgvMatrices.MultiSelect = false;
            dgvMatrices.Name = "dgvMatrices";
            dgvMatrices.ReadOnly = true;
            dgvMatrices.RowHeadersVisible = false;
            dgvMatrices.RowHeadersWidth = 51;
            dgvMatrices.RowTemplate.Height = 30;
            dgvMatrices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMatrices.Size = new Size(850, 358);
            dgvMatrices.TabIndex = 7;
            dgvMatrices.CellDoubleClick += dgvMatrices_CellDoubleClick;
            dgvMatrices.SelectionChanged += dgvMatrices_SelectionChanged;
            dgvMatrices.KeyDown += dgvMatrices_KeyDown;
            // 
            // colId
            // 
            colId.HeaderText = "ID";
            colId.MinimumWidth = 6;
            colId.Name = "colId";
            colId.ReadOnly = true;
            colId.Visible = false;
            // 
            // colTipo
            // 
            colTipo.FillWeight = 45F;
            colTipo.HeaderText = "TIPO";
            colTipo.MinimumWidth = 6;
            colTipo.Name = "colTipo";
            colTipo.ReadOnly = true;
            // 
            // colClave
            // 
            colClave.FillWeight = 55F;
            colClave.HeaderText = "CLAVE";
            colClave.MinimumWidth = 6;
            colClave.Name = "colClave";
            colClave.ReadOnly = true;
            // 
            // colDescripcion
            // 
            colDescripcion.FillWeight = 180F;
            colDescripcion.HeaderText = "DESCRIPCIÓN";
            colDescripcion.MinimumWidth = 6;
            colDescripcion.Name = "colDescripcion";
            colDescripcion.ReadOnly = true;
            // 
            // colUnidad
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colUnidad.DefaultCellStyle = dataGridViewCellStyle1;
            colUnidad.FillWeight = 40F;
            colUnidad.HeaderText = "UNIDAD";
            colUnidad.MinimumWidth = 6;
            colUnidad.Name = "colUnidad";
            colUnidad.ReadOnly = true;
            // 
            // colCosto
            // 
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleRight;
            colCosto.DefaultCellStyle = dataGridViewCellStyle2;
            colCosto.FillWeight = 65F;
            colCosto.HeaderText = "COSTO DIRECTO";
            colCosto.MinimumWidth = 6;
            colCosto.Name = "colCosto";
            colCosto.ReadOnly = true;
            // 
            // colOrigen
            // 
            colOrigen.FillWeight = 50F;
            colOrigen.HeaderText = "ORIGEN";
            colOrigen.MinimumWidth = 6;
            colOrigen.Name = "colOrigen";
            colOrigen.ReadOnly = true;
            // 
            // colProyecto
            // 
            colProyecto.FillWeight = 110F;
            colProyecto.HeaderText = "PROYECTO";
            colProyecto.MinimumWidth = 6;
            colProyecto.Name = "colProyecto";
            colProyecto.ReadOnly = true;
            // 
            // colFecha
            // 
            colFecha.FillWeight = 55F;
            colFecha.HeaderText = "FECHA";
            colFecha.MinimumWidth = 6;
            colFecha.Name = "colFecha";
            colFecha.ReadOnly = true;
            // 
            // panelInfo
            // 
            panelInfo.BackColor = Color.FromArgb(245, 245, 245);
            panelInfo.BorderStyle = BorderStyle.FixedSingle;
            panelInfo.Controls.Add(lblImporte);
            panelInfo.Controls.Add(lblImporteLabel);
            panelInfo.Controls.Add(lblCostoUnitario);
            panelInfo.Controls.Add(lblCostoLabel);
            panelInfo.Controls.Add(lblUnidad);
            panelInfo.Controls.Add(lblUnidadLabel);
            panelInfo.Controls.Add(nudCantidad);
            panelInfo.Controls.Add(lblCantidadLabel);
            panelInfo.Location = new Point(20, 476);
            panelInfo.Name = "panelInfo";
            panelInfo.Size = new Size(850, 80);
            panelInfo.TabIndex = 8;
            // 
            // lblImporte
            // 
            lblImporte.AutoSize = true;
            lblImporte.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblImporte.ForeColor = Color.FromArgb(46, 125, 50);
            lblImporte.Location = new Point(540, 45);
            lblImporte.Name = "lblImporte";
            lblImporte.Size = new Size(61, 25);
            lblImporte.TabIndex = 7;
            lblImporte.Text = "$0.00";
            // 
            // lblImporteLabel
            // 
            lblImporteLabel.AutoSize = true;
            lblImporteLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblImporteLabel.Location = new Point(400, 45);
            lblImporteLabel.Name = "lblImporteLabel";
            lblImporteLabel.Size = new Size(128, 20);
            lblImporteLabel.TabIndex = 6;
            lblImporteLabel.Text = "IMPORTE TOTAL:";
            // 
            // lblCostoUnitario
            // 
            lblCostoUnitario.AutoSize = true;
            lblCostoUnitario.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblCostoUnitario.ForeColor = Color.FromArgb(33, 150, 243);
            lblCostoUnitario.Location = new Point(130, 45);
            lblCostoUnitario.Name = "lblCostoUnitario";
            lblCostoUnitario.Size = new Size(49, 20);
            lblCostoUnitario.TabIndex = 5;
            lblCostoUnitario.Text = "$0.00";
            // 
            // lblCostoLabel
            // 
            lblCostoLabel.AutoSize = true;
            lblCostoLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblCostoLabel.Location = new Point(15, 45);
            lblCostoLabel.Name = "lblCostoLabel";
            lblCostoLabel.Size = new Size(109, 19);
            lblCostoLabel.TabIndex = 4;
            lblCostoLabel.Text = "Costo Unitario:";
            // 
            // lblUnidad
            // 
            lblUnidad.AutoSize = true;
            lblUnidad.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblUnidad.Location = new Point(300, 15);
            lblUnidad.Name = "lblUnidad";
            lblUnidad.Size = new Size(27, 19);
            lblUnidad.TabIndex = 3;
            lblUnidad.Text = "---";
            // 
            // lblUnidadLabel
            // 
            lblUnidadLabel.AutoSize = true;
            lblUnidadLabel.Font = new Font("Segoe UI", 10F);
            lblUnidadLabel.Location = new Point(240, 15);
            lblUnidadLabel.Name = "lblUnidadLabel";
            lblUnidadLabel.Size = new Size(56, 19);
            lblUnidadLabel.TabIndex = 2;
            lblUnidadLabel.Text = "Unidad:";
            // 
            // nudCantidad
            // 
            nudCantidad.DecimalPlaces = 2;
            nudCantidad.Font = new Font("Segoe UI", 10F);
            nudCantidad.Location = new Point(100, 12);
            nudCantidad.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
            nudCantidad.Minimum = new decimal(new int[] { 1, 0, 0, 131072 });
            nudCantidad.Name = "nudCantidad";
            nudCantidad.Size = new Size(120, 25);
            nudCantidad.TabIndex = 1;
            nudCantidad.TextAlign = HorizontalAlignment.Right;
            nudCantidad.Value = new decimal(new int[] { 1, 0, 0, 0 });
            nudCantidad.ValueChanged += nudCantidad_ValueChanged;
            // 
            // lblCantidadLabel
            // 
            lblCantidadLabel.AutoSize = true;
            lblCantidadLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblCantidadLabel.Location = new Point(15, 15);
            lblCantidadLabel.Name = "lblCantidadLabel";
            lblCantidadLabel.Size = new Size(73, 19);
            lblCantidadLabel.TabIndex = 0;
            lblCantidadLabel.Text = "Cantidad:";
            // 
            // btnAceptar
            // 
            btnAceptar.AutoSize = true;
            btnAceptar.BackColor = Color.FromArgb(76, 175, 80);
            btnAceptar.FlatStyle = FlatStyle.Flat;
            btnAceptar.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnAceptar.ForeColor = Color.White;
            btnAceptar.Location = new Point(746, 568);
            btnAceptar.Name = "btnAceptar";
            btnAceptar.Size = new Size(123, 40);
            btnAceptar.TabIndex = 9;
            btnAceptar.Text = "✓ Asignar APU";
            btnAceptar.UseVisualStyleBackColor = false;
            btnAceptar.Click += btnAceptar_Click;
            // 
            // btnEditarMatriz
            // 
            btnEditarMatriz.BackColor = Color.FromArgb(33, 150, 243);
            btnEditarMatriz.Enabled = false;
            btnEditarMatriz.FlatStyle = FlatStyle.Flat;
            btnEditarMatriz.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnEditarMatriz.ForeColor = Color.White;
            btnEditarMatriz.Location = new Point(180, 568);
            btnEditarMatriz.Name = "btnEditarMatriz";
            btnEditarMatriz.Size = new Size(150, 40);
            btnEditarMatriz.TabIndex = 10;
            btnEditarMatriz.Text = "✏ Editar Matriz";
            btnEditarMatriz.UseVisualStyleBackColor = false;
            btnEditarMatriz.Click += btnEditarMatriz_Click;
            // 
            // btnNuevaMatriz
            // 
            btnNuevaMatriz.BackColor = Color.FromArgb(76, 175, 80);
            btnNuevaMatriz.FlatStyle = FlatStyle.Flat;
            btnNuevaMatriz.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnNuevaMatriz.ForeColor = Color.White;
            btnNuevaMatriz.Location = new Point(20, 568);
            btnNuevaMatriz.Name = "btnNuevaMatriz";
            btnNuevaMatriz.Size = new Size(150, 40);
            btnNuevaMatriz.TabIndex = 11;
            btnNuevaMatriz.Text = "➕ Nueva Matriz";
            btnNuevaMatriz.UseVisualStyleBackColor = false;
            btnNuevaMatriz.Click += btnNuevaMatriz_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.BackColor = Color.FromArgb(220, 220, 220);
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Location = new Point(627, 568);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(100, 40);
            btnCancelar.TabIndex = 12;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = false;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // statusStrip
            // 
            statusStrip.ImageScalingSize = new Size(20, 20);
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 618);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(884, 22);
            statusStrip.TabIndex = 13;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(32, 17);
            lblStatus.Text = "Listo";
            // 
            // ctxProyectoFavorito
            // 
            ctxProyectoFavorito.ImageScalingSize = new Size(20, 20);
            ctxProyectoFavorito.Items.AddRange(new ToolStripItem[] { mnuToggleFavorito });
            ctxProyectoFavorito.Name = "ctxProyectoFavorito";
            ctxProyectoFavorito.Size = new Size(277, 26);
            // 
            // mnuToggleFavorito
            // 
            mnuToggleFavorito.Name = "mnuToggleFavorito";
            mnuToggleFavorito.Size = new Size(276, 22);
            mnuToggleFavorito.Text = "Marcar proyecto origen como favorito";
            // 
            // FormSeleccionarAPU
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(884, 640);
            Controls.Add(statusStrip);
            Controls.Add(btnCancelar);
            Controls.Add(btnNuevaMatriz);
            Controls.Add(btnEditarMatriz);
            Controls.Add(btnAceptar);
            Controls.Add(panelInfo);
            Controls.Add(_cboProyecto);
            Controls.Add(_cboTipo);
            Controls.Add(dgvMatrices);
            Controls.Add(txtBuscar);
            Controls.Add(lblBuscar);
            Controls.Add(panelTop);
            MinimumSize = new Size(800, 598);
            Name = "FormSeleccionarAPU";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Seleccionar Matriz (APU)";
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvMatrices).EndInit();
            panelInfo.ResumeLayout(false);
            panelInfo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudCantidad).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ctxProyectoFavorito.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.Label lblBuscar;
        private System.Windows.Forms.TextBox txtBuscar;
        private System.Windows.Forms.ComboBox _cboTipo;
        private System.Windows.Forms.ComboBox _cboProyecto;
        private System.Windows.Forms.DataGridView dgvMatrices;
        private System.Windows.Forms.DataGridViewTextBoxColumn colId;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTipo;
        private System.Windows.Forms.DataGridViewTextBoxColumn colClave;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDescripcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnidad;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCosto;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOrigen;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProyecto;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFecha;
        private System.Windows.Forms.Panel panelInfo;
        private System.Windows.Forms.Label lblImporte;
        private System.Windows.Forms.Label lblImporteLabel;
        private System.Windows.Forms.Label lblCostoUnitario;
        private System.Windows.Forms.Label lblCostoLabel;
        private System.Windows.Forms.Label lblUnidad;
        private System.Windows.Forms.Label lblUnidadLabel;
        private System.Windows.Forms.NumericUpDown nudCantidad;
        private System.Windows.Forms.Label lblCantidadLabel;
        private System.Windows.Forms.Button btnAceptar;
        private System.Windows.Forms.Button btnEditarMatriz;
        private System.Windows.Forms.Button btnNuevaMatriz;
        private System.Windows.Forms.Button btnCancelar;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ContextMenuStrip ctxProyectoFavorito;
        private System.Windows.Forms.ToolStripMenuItem mnuToggleFavorito;
    }
}
