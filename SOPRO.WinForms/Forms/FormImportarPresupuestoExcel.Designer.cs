namespace SOPRO.WinForms.Forms
{
    partial class FormImportarPresupuestoExcel
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
            panelTop = new System.Windows.Forms.Panel();
            lblTitulo = new System.Windows.Forms.Label();
            panelArchivo = new System.Windows.Forms.Panel();
            btnRecargar = new System.Windows.Forms.Button();
            chkPrimeraFilaEncabezados = new System.Windows.Forms.CheckBox();
            cmbHojas = new System.Windows.Forms.ComboBox();
            lblHoja = new System.Windows.Forms.Label();
            btnExaminar = new System.Windows.Forms.Button();
            txtRutaArchivo = new System.Windows.Forms.TextBox();
            lblArchivo = new System.Windows.Forms.Label();
            splitContainer = new System.Windows.Forms.SplitContainer();
            dgvPreview = new System.Windows.Forms.DataGridView();
            panelMapeo = new System.Windows.Forms.Panel();
            panelAgrupadores = new System.Windows.Forms.Panel();
            dgvAgrupadores = new System.Windows.Forms.DataGridView();
            lblAgrupadores = new System.Windows.Forms.Label();
            btnCancelar = new System.Windows.Forms.Button();
            btnImportar = new System.Windows.Forms.Button();
            lblAyuda = new System.Windows.Forms.Label();
            cmbTipo = new System.Windows.Forms.ComboBox();
            lblTipo = new System.Windows.Forms.Label();
            cmbCantidad = new System.Windows.Forms.ComboBox();
            lblCantidad = new System.Windows.Forms.Label();
            cmbUnidad = new System.Windows.Forms.ComboBox();
            lblUnidad = new System.Windows.Forms.Label();
            cmbDescripcion = new System.Windows.Forms.ComboBox();
            lblDescripcion = new System.Windows.Forms.Label();
            cmbClave = new System.Windows.Forms.ComboBox();
            lblClave = new System.Windows.Forms.Label();
            statusStrip = new System.Windows.Forms.StatusStrip();
            lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            panelTop.SuspendLayout();
            panelArchivo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPreview).BeginInit();
            panelMapeo.SuspendLayout();
            panelAgrupadores.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvAgrupadores).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = System.Drawing.Color.FromArgb(31, 78, 121);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            panelTop.Location = new System.Drawing.Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new System.Drawing.Size(1180, 58);
            panelTop.TabIndex = 0;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            lblTitulo.ForeColor = System.Drawing.Color.White;
            lblTitulo.Location = new System.Drawing.Point(18, 14);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new System.Drawing.Size(340, 30);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "Importar presupuesto desde Excel";
            // 
            // panelArchivo
            // 
            panelArchivo.Controls.Add(btnRecargar);
            panelArchivo.Controls.Add(chkPrimeraFilaEncabezados);
            panelArchivo.Controls.Add(cmbHojas);
            panelArchivo.Controls.Add(lblHoja);
            panelArchivo.Controls.Add(btnExaminar);
            panelArchivo.Controls.Add(txtRutaArchivo);
            panelArchivo.Controls.Add(lblArchivo);
            panelArchivo.Dock = System.Windows.Forms.DockStyle.Top;
            panelArchivo.Location = new System.Drawing.Point(0, 58);
            panelArchivo.Name = "panelArchivo";
            panelArchivo.Padding = new System.Windows.Forms.Padding(12, 10, 12, 10);
            panelArchivo.Size = new System.Drawing.Size(1180, 74);
            panelArchivo.TabIndex = 1;
            // 
            // btnRecargar
            // 
            btnRecargar.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnRecargar.Location = new System.Drawing.Point(1011, 34);
            btnRecargar.Name = "btnRecargar";
            btnRecargar.Size = new System.Drawing.Size(75, 27);
            btnRecargar.TabIndex = 6;
            btnRecargar.Text = "Recargar";
            btnRecargar.UseVisualStyleBackColor = true;
            btnRecargar.Click += btnRecargar_Click;
            // 
            // chkPrimeraFilaEncabezados
            // 
            chkPrimeraFilaEncabezados.AutoSize = true;
            chkPrimeraFilaEncabezados.Location = new System.Drawing.Point(736, 38);
            chkPrimeraFilaEncabezados.Name = "chkPrimeraFilaEncabezados";
            chkPrimeraFilaEncabezados.Size = new System.Drawing.Size(166, 19);
            chkPrimeraFilaEncabezados.TabIndex = 5;
            chkPrimeraFilaEncabezados.Text = "Primera fila como encabezado";
            chkPrimeraFilaEncabezados.UseVisualStyleBackColor = true;
            chkPrimeraFilaEncabezados.CheckedChanged += chkPrimeraFilaEncabezados_CheckedChanged;
            // 
            // cmbHojas
            // 
            cmbHojas.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbHojas.FormattingEnabled = true;
            cmbHojas.Location = new System.Drawing.Point(470, 35);
            cmbHojas.Name = "cmbHojas";
            cmbHojas.Size = new System.Drawing.Size(248, 23);
            cmbHojas.TabIndex = 4;
            cmbHojas.SelectedIndexChanged += cmbHojas_SelectedIndexChanged;
            // 
            // lblHoja
            // 
            lblHoja.AutoSize = true;
            lblHoja.Location = new System.Drawing.Point(470, 16);
            lblHoja.Name = "lblHoja";
            lblHoja.Size = new System.Drawing.Size(35, 15);
            lblHoja.TabIndex = 3;
            lblHoja.Text = "Hoja";
            // 
            // btnExaminar
            // 
            btnExaminar.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnExaminar.Location = new System.Drawing.Point(373, 34);
            btnExaminar.Name = "btnExaminar";
            btnExaminar.Size = new System.Drawing.Size(84, 27);
            btnExaminar.TabIndex = 2;
            btnExaminar.Text = "Examinar";
            btnExaminar.UseVisualStyleBackColor = true;
            btnExaminar.Click += btnExaminar_Click;
            // 
            // txtRutaArchivo
            // 
            txtRutaArchivo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtRutaArchivo.Location = new System.Drawing.Point(15, 35);
            txtRutaArchivo.Name = "txtRutaArchivo";
            txtRutaArchivo.ReadOnly = true;
            txtRutaArchivo.Size = new System.Drawing.Size(352, 23);
            txtRutaArchivo.TabIndex = 1;
            // 
            // lblArchivo
            // 
            lblArchivo.AutoSize = true;
            lblArchivo.Location = new System.Drawing.Point(15, 16);
            lblArchivo.Name = "lblArchivo";
            lblArchivo.Size = new System.Drawing.Size(43, 15);
            lblArchivo.TabIndex = 0;
            lblArchivo.Text = "Archivo";
            // 
            // splitContainer
            // 
            splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer.Location = new System.Drawing.Point(0, 132);
            splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(dgvPreview);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(panelMapeo);
            splitContainer.Size = new System.Drawing.Size(1180, 620);
            splitContainer.SplitterDistance = 800;
            splitContainer.TabIndex = 2;
            // 
            // dgvPreview
            // 
            dgvPreview.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvPreview.Location = new System.Drawing.Point(0, 0);
            dgvPreview.Name = "dgvPreview";
            dgvPreview.Size = new System.Drawing.Size(800, 620);
            dgvPreview.TabIndex = 0;
            // 
            // panelMapeo
            // 
            panelMapeo.Controls.Add(btnCancelar);
            panelMapeo.Controls.Add(btnImportar);
            panelMapeo.Controls.Add(panelAgrupadores);
            panelMapeo.Controls.Add(lblAyuda);
            panelMapeo.Controls.Add(cmbTipo);
            panelMapeo.Controls.Add(lblTipo);
            panelMapeo.Controls.Add(cmbCantidad);
            panelMapeo.Controls.Add(lblCantidad);
            panelMapeo.Controls.Add(cmbUnidad);
            panelMapeo.Controls.Add(lblUnidad);
            panelMapeo.Controls.Add(cmbDescripcion);
            panelMapeo.Controls.Add(lblDescripcion);
            panelMapeo.Controls.Add(cmbClave);
            panelMapeo.Controls.Add(lblClave);
            panelMapeo.Dock = System.Windows.Forms.DockStyle.Fill;
            panelMapeo.Location = new System.Drawing.Point(0, 0);
            panelMapeo.Name = "panelMapeo";
            panelMapeo.Padding = new System.Windows.Forms.Padding(14);
            panelMapeo.Size = new System.Drawing.Size(376, 620);
            panelMapeo.TabIndex = 0;
            // 
            // 
            // panelAgrupadores
            // 
            panelAgrupadores.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panelAgrupadores.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelAgrupadores.Controls.Add(dgvAgrupadores);
            panelAgrupadores.Controls.Add(lblAgrupadores);
            panelAgrupadores.Location = new System.Drawing.Point(17, 330);
            panelAgrupadores.Name = "panelAgrupadores";
            panelAgrupadores.Size = new System.Drawing.Size(342, 220);
            panelAgrupadores.TabIndex = 13;
            // 
            // dgvAgrupadores
            // 
            dgvAgrupadores.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvAgrupadores.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvAgrupadores.Location = new System.Drawing.Point(0, 32);
            dgvAgrupadores.Name = "dgvAgrupadores";
            dgvAgrupadores.Size = new System.Drawing.Size(340, 186);
            dgvAgrupadores.TabIndex = 1;
            // 
            // lblAgrupadores
            // 
            lblAgrupadores.Dock = System.Windows.Forms.DockStyle.Top;
            lblAgrupadores.ForeColor = System.Drawing.Color.DimGray;
            lblAgrupadores.Location = new System.Drawing.Point(0, 0);
            lblAgrupadores.Name = "lblAgrupadores";
            lblAgrupadores.Size = new System.Drawing.Size(340, 34);
            lblAgrupadores.TabIndex = 0;
            lblAgrupadores.Text = "Agrupadores detectados. Puede ajustar el tipo o marcar No importar antes de importar.";
            // 
            // btnCancelar
            // 
            btnCancelar.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnCancelar.Location = new System.Drawing.Point(17, 570);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new System.Drawing.Size(110, 34);
            btnCancelar.TabIndex = 12;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = true;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // btnImportar
            // 
            btnImportar.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnImportar.BackColor = System.Drawing.Color.FromArgb(76, 175, 80);
            btnImportar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnImportar.ForeColor = System.Drawing.Color.White;
            btnImportar.Location = new System.Drawing.Point(229, 570);
            btnImportar.Name = "btnImportar";
            btnImportar.Size = new System.Drawing.Size(130, 34);
            btnImportar.TabIndex = 11;
            btnImportar.Text = "Importar";
            btnImportar.UseVisualStyleBackColor = false;
            btnImportar.Click += btnImportar_Click;
            // 
            // lblAyuda
            // 
            lblAyuda.ForeColor = System.Drawing.Color.DimGray;
            lblAyuda.Location = new System.Drawing.Point(17, 18);
            lblAyuda.Name = "lblAyuda";
            lblAyuda.Size = new System.Drawing.Size(294, 58);
            lblAyuda.TabIndex = 0;
            lblAyuda.Text = "Mapee las columnas que desea importar. SOPRO solo traerá Clave, Descripción, Unidad, Cantidad y Tipo opcional. No se importan P.U. ni Importe.";
            // 
            // cmbTipo
            // 
            cmbTipo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbTipo.FormattingEnabled = true;
            cmbTipo.Location = new System.Drawing.Point(17, 333);
            cmbTipo.Name = "cmbTipo";
            cmbTipo.Size = new System.Drawing.Size(294, 23);
            cmbTipo.TabIndex = 10;
            // 
            // lblTipo
            // 
            lblTipo.AutoSize = true;
            lblTipo.Location = new System.Drawing.Point(17, 315);
            lblTipo.Name = "lblTipo";
            lblTipo.Size = new System.Drawing.Size(75, 15);
            lblTipo.TabIndex = 9;
            lblTipo.Text = "Tipo (opcional)";
            // 
            // cmbCantidad
            // 
            cmbCantidad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbCantidad.FormattingEnabled = true;
            cmbCantidad.Location = new System.Drawing.Point(17, 273);
            cmbCantidad.Name = "cmbCantidad";
            cmbCantidad.Size = new System.Drawing.Size(294, 23);
            cmbCantidad.TabIndex = 8;
            // 
            // lblCantidad
            // 
            lblCantidad.AutoSize = true;
            lblCantidad.Location = new System.Drawing.Point(17, 255);
            lblCantidad.Name = "lblCantidad";
            lblCantidad.Size = new System.Drawing.Size(55, 15);
            lblCantidad.TabIndex = 7;
            lblCantidad.Text = "Cantidad";
            // 
            // cmbUnidad
            // 
            cmbUnidad.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbUnidad.FormattingEnabled = true;
            cmbUnidad.Location = new System.Drawing.Point(17, 213);
            cmbUnidad.Name = "cmbUnidad";
            cmbUnidad.Size = new System.Drawing.Size(294, 23);
            cmbUnidad.TabIndex = 6;
            // 
            // lblUnidad
            // 
            lblUnidad.AutoSize = true;
            lblUnidad.Location = new System.Drawing.Point(17, 195);
            lblUnidad.Name = "lblUnidad";
            lblUnidad.Size = new System.Drawing.Size(45, 15);
            lblUnidad.TabIndex = 5;
            lblUnidad.Text = "Unidad";
            // 
            // cmbDescripcion
            // 
            cmbDescripcion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbDescripcion.FormattingEnabled = true;
            cmbDescripcion.Location = new System.Drawing.Point(17, 153);
            cmbDescripcion.Name = "cmbDescripcion";
            cmbDescripcion.Size = new System.Drawing.Size(294, 23);
            cmbDescripcion.TabIndex = 4;
            // 
            // lblDescripcion
            // 
            lblDescripcion.AutoSize = true;
            lblDescripcion.Location = new System.Drawing.Point(17, 135);
            lblDescripcion.Name = "lblDescripcion";
            lblDescripcion.Size = new System.Drawing.Size(69, 15);
            lblDescripcion.TabIndex = 3;
            lblDescripcion.Text = "Descripción";
            // 
            // cmbClave
            // 
            cmbClave.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbClave.FormattingEnabled = true;
            cmbClave.Location = new System.Drawing.Point(17, 93);
            cmbClave.Name = "cmbClave";
            cmbClave.Size = new System.Drawing.Size(294, 23);
            cmbClave.TabIndex = 2;
            // 
            // lblClave
            // 
            lblClave.AutoSize = true;
            lblClave.Location = new System.Drawing.Point(17, 75);
            lblClave.Name = "lblClave";
            lblClave.Size = new System.Drawing.Size(37, 15);
            lblClave.TabIndex = 1;
            lblClave.Text = "Clave";
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { lblStatus });
            statusStrip.Location = new System.Drawing.Point(0, 666);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new System.Drawing.Size(1100, 22);
            statusStrip.TabIndex = 3;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(39, 17);
            lblStatus.Text = "Listo";
            // 
            // FormImportarPresupuestoExcel
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1180, 774);
            Controls.Add(splitContainer);
            Controls.Add(panelArchivo);
            Controls.Add(panelTop);
            Controls.Add(statusStrip);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(980, 620);
            Name = "FormImportarPresupuestoExcel";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Importar presupuesto desde Excel";
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelArchivo.ResumeLayout(false);
            panelArchivo.PerformLayout();
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvPreview).EndInit();
            panelAgrupadores.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvAgrupadores).EndInit();
            panelMapeo.ResumeLayout(false);
            panelMapeo.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.Panel panelArchivo;
        private System.Windows.Forms.Button btnExaminar;
        private System.Windows.Forms.TextBox txtRutaArchivo;
        private System.Windows.Forms.Label lblArchivo;
        private System.Windows.Forms.ComboBox cmbHojas;
        private System.Windows.Forms.Label lblHoja;
        private System.Windows.Forms.CheckBox chkPrimeraFilaEncabezados;
        private System.Windows.Forms.Button btnRecargar;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.DataGridView dgvPreview;
        private System.Windows.Forms.Panel panelMapeo;
        private System.Windows.Forms.Label lblAyuda;
        private System.Windows.Forms.ComboBox cmbClave;
        private System.Windows.Forms.Label lblClave;
        private System.Windows.Forms.ComboBox cmbDescripcion;
        private System.Windows.Forms.Label lblDescripcion;
        private System.Windows.Forms.ComboBox cmbUnidad;
        private System.Windows.Forms.Label lblUnidad;
        private System.Windows.Forms.ComboBox cmbCantidad;
        private System.Windows.Forms.Label lblCantidad;
        private System.Windows.Forms.ComboBox cmbTipo;
        private System.Windows.Forms.Label lblTipo;
        private System.Windows.Forms.Button btnCancelar;
        private System.Windows.Forms.Button btnImportar;
        private System.Windows.Forms.Panel panelAgrupadores;
        private System.Windows.Forms.DataGridView dgvAgrupadores;
        private System.Windows.Forms.Label lblAgrupadores;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
