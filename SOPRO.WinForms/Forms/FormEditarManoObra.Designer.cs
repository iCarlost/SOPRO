namespace SOPRO.WinForms.Forms
{
    partial class FormEditarManoObra
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
            lblClave = new Label();
            txtClave = new TextBox();
            lblDescripcion = new Label();
            txtDescripcion = new TextBox();
            lblUnidad = new Label();
            cboUnidad = new ComboBox();
            lblSalarioBase = new Label();
            nudSalarioBase = new NumericUpDown();
            lblFSR = new Label();
            nudFSR = new NumericUpDown();
            lblSalarioReal = new Label();
            nudSalarioReal = new NumericUpDown();
            grpOrigen = new GroupBox();
            rbProyecto = new RadioButton();
            rbMaestro = new RadioButton();
            lblNotas = new Label();
            txtNotas = new TextBox();
            btnGuardar = new Button();
            btnCancelar = new Button();
            lblInfo = new Label();
            ((System.ComponentModel.ISupportInitialize)nudSalarioBase).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudFSR).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudSalarioReal).BeginInit();
            grpOrigen.SuspendLayout();
            SuspendLayout();
            // 
            // lblClave
            // 
            lblClave.AutoSize = true;
            lblClave.Font = new Font("Segoe UI", 10F);
            lblClave.Location = new Point(30, 30);
            lblClave.Name = "lblClave";
            lblClave.Size = new Size(45, 19);
            lblClave.TabIndex = 0;
            lblClave.Text = "Clave:";
            // 
            // txtClave
            // 
            txtClave.CharacterCasing = CharacterCasing.Upper;
            txtClave.Font = new Font("Segoe UI", 10F);
            txtClave.Location = new Point(180, 27);
            txtClave.MaxLength = 50;
            txtClave.Name = "txtClave";
            txtClave.Size = new Size(200, 25);
            txtClave.TabIndex = 1;
            txtClave.Leave += txtClave_Leave;
            // 
            // lblDescripcion
            // 
            lblDescripcion.AutoSize = true;
            lblDescripcion.Font = new Font("Segoe UI", 10F);
            lblDescripcion.Location = new Point(30, 70);
            lblDescripcion.Name = "lblDescripcion";
            lblDescripcion.Size = new Size(82, 19);
            lblDescripcion.TabIndex = 2;
            lblDescripcion.Text = "Descripción:";
            // 
            // txtDescripcion
            // 
            txtDescripcion.Font = new Font("Segoe UI", 10F);
            txtDescripcion.Location = new Point(180, 67);
            txtDescripcion.MaxLength = 500;
            txtDescripcion.Name = "txtDescripcion";
            txtDescripcion.Size = new Size(450, 25);
            txtDescripcion.TabIndex = 3;
            // 
            // lblUnidad
            // 
            lblUnidad.AutoSize = true;
            lblUnidad.Font = new Font("Segoe UI", 10F);
            lblUnidad.Location = new Point(30, 110);
            lblUnidad.Name = "lblUnidad";
            lblUnidad.Size = new Size(56, 19);
            lblUnidad.TabIndex = 4;
            lblUnidad.Text = "Unidad:";
            // 
            // cboUnidad
            // 
            cboUnidad.Font = new Font("Segoe UI", 10F);
            cboUnidad.FormattingEnabled = true;
            cboUnidad.Items.AddRange(new object[] { "jor", "hora", "%MO" });
            cboUnidad.Location = new Point(180, 107);
            cboUnidad.Name = "cboUnidad";
            cboUnidad.Size = new Size(100, 25);
            cboUnidad.TabIndex = 5;
            cboUnidad.SelectedIndexChanged += cboUnidad_SelectedIndexChanged;
            // 
            // lblSalarioBase
            // 
            lblSalarioBase.AutoSize = true;
            lblSalarioBase.Font = new Font("Segoe UI", 10F);
            lblSalarioBase.Location = new Point(30, 150);
            lblSalarioBase.Name = "lblSalarioBase";
            lblSalarioBase.Size = new Size(84, 19);
            lblSalarioBase.TabIndex = 6;
            lblSalarioBase.Text = "Salario Base:";
            // 
            // nudSalarioBase
            // 
            nudSalarioBase.DecimalPlaces = 2;
            nudSalarioBase.Font = new Font("Segoe UI", 10F);
            nudSalarioBase.Location = new Point(180, 147);
            nudSalarioBase.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
            nudSalarioBase.Name = "nudSalarioBase";
            nudSalarioBase.Size = new Size(150, 25);
            nudSalarioBase.TabIndex = 7;
            nudSalarioBase.TextAlign = HorizontalAlignment.Right;
            nudSalarioBase.ThousandsSeparator = true;
            nudSalarioBase.ValueChanged += nudSalarioBase_ValueChanged;
            // 
            // lblFSR
            // 
            lblFSR.AutoSize = true;
            lblFSR.Font = new Font("Segoe UI", 10F);
            lblFSR.Location = new Point(360, 150);
            lblFSR.Name = "lblFSR";
            lblFSR.Size = new Size(34, 19);
            lblFSR.TabIndex = 8;
            lblFSR.Text = "FSR:";
            // 
            // nudFSR
            // 
            nudFSR.DecimalPlaces = 6;
            nudFSR.Font = new Font("Segoe UI", 10F);
            nudFSR.Increment = new decimal(new int[] { 1, 0, 0, 262144 });
            nudFSR.Location = new Point(410, 147);
            nudFSR.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            nudFSR.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            nudFSR.Name = "nudFSR";
            nudFSR.Size = new Size(120, 25);
            nudFSR.TabIndex = 9;
            nudFSR.TextAlign = HorizontalAlignment.Right;
            nudFSR.Value = new decimal(new int[] { 16543, 0, 0, 262144 });
            nudFSR.ValueChanged += nudFSR_ValueChanged;
            // 
            // lblSalarioReal
            // 
            lblSalarioReal.AutoSize = true;
            lblSalarioReal.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblSalarioReal.Location = new Point(30, 190);
            lblSalarioReal.Name = "lblSalarioReal";
            lblSalarioReal.Size = new Size(93, 19);
            lblSalarioReal.TabIndex = 10;
            lblSalarioReal.Text = "Salario Real:";
            // 
            // nudSalarioReal
            // 
            nudSalarioReal.DecimalPlaces = 2;
            nudSalarioReal.Enabled = false;
            nudSalarioReal.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            nudSalarioReal.Location = new Point(180, 187);
            nudSalarioReal.Maximum = new decimal(new int[] { 9999999, 0, 0, 0 });
            nudSalarioReal.Name = "nudSalarioReal";
            nudSalarioReal.ReadOnly = true;
            nudSalarioReal.Size = new Size(200, 25);
            nudSalarioReal.TabIndex = 11;
            nudSalarioReal.TextAlign = HorizontalAlignment.Right;
            nudSalarioReal.ThousandsSeparator = true;
            // 
            // grpOrigen
            // 
            grpOrigen.Controls.Add(rbProyecto);
            grpOrigen.Controls.Add(rbMaestro);
            grpOrigen.Font = new Font("Segoe UI", 10F);
            grpOrigen.Location = new Point(30, 456);
            grpOrigen.Name = "grpOrigen";
            grpOrigen.Size = new Size(600, 70);
            grpOrigen.TabIndex = 12;
            grpOrigen.TabStop = false;
            grpOrigen.Text = "Origen del Registro";
            grpOrigen.Visible = false;
            // 
            // rbProyecto
            // 
            rbProyecto.AutoSize = true;
            rbProyecto.Location = new Point(320, 30);
            rbProyecto.Name = "rbProyecto";
            rbProyecto.Size = new Size(188, 23);
            rbProyecto.TabIndex = 1;
            rbProyecto.TabStop = true;
            rbProyecto.Text = "📁 Específico del Proyecto";
            rbProyecto.UseVisualStyleBackColor = true;
            // 
            // rbMaestro
            // 
            rbMaestro.AutoSize = true;
            rbMaestro.Location = new Point(30, 30);
            rbMaestro.Name = "rbMaestro";
            rbMaestro.Size = new Size(160, 23);
            rbMaestro.TabIndex = 0;
            rbMaestro.TabStop = true;
            rbMaestro.Text = "🌐 Catálogo Maestro";
            rbMaestro.UseVisualStyleBackColor = true;
            // 
            // lblNotas
            // 
            lblNotas.AutoSize = true;
            lblNotas.Font = new Font("Segoe UI", 10F);
            lblNotas.Location = new Point(30, 235);
            lblNotas.Name = "lblNotas";
            lblNotas.Size = new Size(48, 19);
            lblNotas.TabIndex = 13;
            lblNotas.Text = "Notas:";
            // 
            // txtNotas
            // 
            txtNotas.Font = new Font("Segoe UI", 9.5F);
            txtNotas.Location = new Point(30, 260);
            txtNotas.Multiline = true;
            txtNotas.Name = "txtNotas";
            txtNotas.ScrollBars = ScrollBars.Vertical;
            txtNotas.Size = new Size(600, 80);
            txtNotas.TabIndex = 14;
            // 
            // btnGuardar
            // 
            btnGuardar.BackColor = Color.FromArgb(76, 175, 80);
            btnGuardar.FlatStyle = FlatStyle.Flat;
            btnGuardar.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGuardar.ForeColor = Color.White;
            btnGuardar.Location = new Point(430, 365);
            btnGuardar.Name = "btnGuardar";
            btnGuardar.Size = new Size(200, 40);
            btnGuardar.TabIndex = 15;
            btnGuardar.Text = "💾 Guardar";
            btnGuardar.UseVisualStyleBackColor = false;
            btnGuardar.Click += btnGuardar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.BackColor = Color.FromArgb(220, 220, 220);
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Font = new Font("Segoe UI", 10F);
            btnCancelar.Location = new Point(310, 365);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(100, 40);
            btnCancelar.TabIndex = 16;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = false;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // lblInfo
            // 
            lblInfo.AutoSize = true;
            lblInfo.Font = new Font("Segoe UI", 8F, FontStyle.Italic);
            lblInfo.ForeColor = Color.Gray;
            lblInfo.Location = new Point(400, 192);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(203, 13);
            lblInfo.TabIndex = 17;
            lblInfo.Text = "(Se calcula automáticamente: Base × FSR)";
            // 
            // FormEditarManoObra
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(664, 428);
            Controls.Add(lblInfo);
            Controls.Add(btnCancelar);
            Controls.Add(btnGuardar);
            Controls.Add(txtNotas);
            Controls.Add(lblNotas);
            Controls.Add(grpOrigen);
            Controls.Add(nudSalarioReal);
            Controls.Add(lblSalarioReal);
            Controls.Add(nudFSR);
            Controls.Add(lblFSR);
            Controls.Add(nudSalarioBase);
            Controls.Add(lblSalarioBase);
            Controls.Add(cboUnidad);
            Controls.Add(lblUnidad);
            Controls.Add(txtDescripcion);
            Controls.Add(lblDescripcion);
            Controls.Add(txtClave);
            Controls.Add(lblClave);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormEditarManoObra";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Editar Mano de Obra";
            ((System.ComponentModel.ISupportInitialize)nudSalarioBase).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudFSR).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudSalarioReal).EndInit();
            grpOrigen.ResumeLayout(false);
            grpOrigen.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblClave;
        private System.Windows.Forms.TextBox txtClave;
        private System.Windows.Forms.Label lblDescripcion;
        private System.Windows.Forms.TextBox txtDescripcion;
        private System.Windows.Forms.Label lblUnidad;
        private System.Windows.Forms.ComboBox cboUnidad;
        private System.Windows.Forms.Label lblSalarioBase;
        private System.Windows.Forms.NumericUpDown nudSalarioBase;
        private System.Windows.Forms.Label lblFSR;
        private System.Windows.Forms.NumericUpDown nudFSR;
        private System.Windows.Forms.Label lblSalarioReal;
        private System.Windows.Forms.NumericUpDown nudSalarioReal;
        private System.Windows.Forms.GroupBox grpOrigen;
        private System.Windows.Forms.RadioButton rbMaestro;
        private System.Windows.Forms.RadioButton rbProyecto;
        private System.Windows.Forms.Label lblNotas;
        private System.Windows.Forms.TextBox txtNotas;
        private System.Windows.Forms.Button btnGuardar;
        private System.Windows.Forms.Button btnCancelar;
        private System.Windows.Forms.Label lblInfo;
    }
}
