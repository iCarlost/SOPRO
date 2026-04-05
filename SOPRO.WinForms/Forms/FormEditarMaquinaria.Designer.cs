namespace SOPRO.WinForms.Forms
{
    partial class FormEditarMaquinaria
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
            lblPotencia = new Label();
            nudPotencia = new NumericUpDown();
            lblTipoCombustible = new Label();
            cboTipoCombustible = new ComboBox();
            grpCosto = new GroupBox();
            btnCalcular = new Button();
            lblAdvertencia = new Label();
            lblFechaCalculo = new Label();
            label6 = new Label();
            nudCostoHorario = new NumericUpDown();
            label4 = new Label();
            rbCostoManual = new RadioButton();
            rbCostoCalculado = new RadioButton();
            grpOrigen = new GroupBox();
            rbProyecto = new RadioButton();
            rbMaestro = new RadioButton();
            lblNotas = new Label();
            txtNotas = new TextBox();
            btnGuardar = new Button();
            btnCancelar = new Button();
            ((System.ComponentModel.ISupportInitialize)nudPotencia).BeginInit();
            grpCosto.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudCostoHorario).BeginInit();
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
            txtDescripcion.Size = new Size(500, 25);
            txtDescripcion.TabIndex = 3;
            // 
            // lblPotencia
            // 
            lblPotencia.AutoSize = true;
            lblPotencia.Font = new Font("Segoe UI", 10F);
            lblPotencia.Location = new Point(30, 110);
            lblPotencia.Name = "lblPotencia";
            lblPotencia.Size = new Size(118, 19);
            lblPotencia.TabIndex = 4;
            lblPotencia.Text = "Potencia Nominal:";
            // 
            // nudPotencia
            // 
            nudPotencia.DecimalPlaces = 2;
            nudPotencia.Font = new Font("Segoe UI", 10F);
            nudPotencia.Location = new Point(180, 107);
            nudPotencia.Maximum = new decimal(new int[] { 9999, 0, 0, 0 });
            nudPotencia.Name = "nudPotencia";
            nudPotencia.Size = new Size(120, 25);
            nudPotencia.TabIndex = 5;
            nudPotencia.TextAlign = HorizontalAlignment.Right;
            nudPotencia.ThousandsSeparator = true;
            // 
            // lblTipoCombustible
            // 
            lblTipoCombustible.AutoSize = true;
            lblTipoCombustible.Font = new Font("Segoe UI", 10F);
            lblTipoCombustible.Location = new Point(320, 110);
            lblTipoCombustible.Name = "lblTipoCombustible";
            lblTipoCombustible.Size = new Size(89, 19);
            lblTipoCombustible.TabIndex = 6;
            lblTipoCombustible.Text = "Combustible:";
            // 
            // cboTipoCombustible
            // 
            cboTipoCombustible.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTipoCombustible.Font = new Font("Segoe UI", 10F);
            cboTipoCombustible.FormattingEnabled = true;
            cboTipoCombustible.Location = new Point(420, 107);
            cboTipoCombustible.Name = "cboTipoCombustible";
            cboTipoCombustible.Size = new Size(150, 25);
            cboTipoCombustible.TabIndex = 7;
            // 
            // grpCosto
            // 
            grpCosto.Controls.Add(btnCalcular);
            grpCosto.Controls.Add(lblAdvertencia);
            grpCosto.Controls.Add(lblFechaCalculo);
            grpCosto.Controls.Add(label6);
            grpCosto.Controls.Add(nudCostoHorario);
            grpCosto.Controls.Add(label4);
            grpCosto.Controls.Add(rbCostoManual);
            grpCosto.Controls.Add(rbCostoCalculado);
            grpCosto.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpCosto.Location = new Point(30, 150);
            grpCosto.Name = "grpCosto";
            grpCosto.Size = new Size(700, 150);
            grpCosto.TabIndex = 8;
            grpCosto.TabStop = false;
            grpCosto.Text = "Costo Horario";
            // 
            // btnCalcular
            // 
            btnCalcular.BackColor = Color.FromArgb(255, 152, 0);
            btnCalcular.Enabled = false;
            btnCalcular.FlatStyle = FlatStyle.Flat;
            btnCalcular.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnCalcular.ForeColor = Color.White;
            btnCalcular.Location = new Point(500, 25);
            btnCalcular.Name = "btnCalcular";
            btnCalcular.Size = new Size(180, 40);
            btnCalcular.TabIndex = 7;
            btnCalcular.Text = "\U0001f9ee Calcular Costo";
            btnCalcular.UseVisualStyleBackColor = false;
            btnCalcular.Click += btnCalcular_Click;
            // 
            // lblAdvertencia
            // 
            lblAdvertencia.AutoSize = true;
            lblAdvertencia.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblAdvertencia.ForeColor = Color.FromArgb(255, 87, 34);
            lblAdvertencia.Location = new Point(20, 125);
            lblAdvertencia.Name = "lblAdvertencia";
            lblAdvertencia.Size = new Size(309, 15);
            lblAdvertencia.TabIndex = 6;
            lblAdvertencia.Text = "⚠ Use el botón 'Calcular' para determinar el costo horario";
            lblAdvertencia.Visible = false;
            // 
            // lblFechaCalculo
            // 
            lblFechaCalculo.AutoSize = true;
            lblFechaCalculo.Font = new Font("Segoe UI", 9F);
            lblFechaCalculo.ForeColor = Color.Gray;
            lblFechaCalculo.Location = new Point(470, 105);
            lblFechaCalculo.Name = "lblFechaCalculo";
            lblFechaCalculo.Size = new Size(29, 15);
            lblFechaCalculo.TabIndex = 5;
            lblFechaCalculo.Text = "N/A";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Segoe UI", 9F);
            label6.ForeColor = Color.Gray;
            label6.Location = new Point(360, 105);
            label6.Name = "label6";
            label6.Size = new Size(103, 15);
            label6.TabIndex = 4;
            label6.Text = "Último cálculo en:";
            // 
            // nudCostoHorario
            // 
            nudCostoHorario.DecimalPlaces = 4;
            nudCostoHorario.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            nudCostoHorario.Location = new Point(150, 90);
            nudCostoHorario.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
            nudCostoHorario.Name = "nudCostoHorario";
            nudCostoHorario.Size = new Size(200, 29);
            nudCostoHorario.TabIndex = 3;
            nudCostoHorario.TextAlign = HorizontalAlignment.Right;
            nudCostoHorario.ThousandsSeparator = true;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Segoe UI", 10F);
            label4.Location = new Point(20, 95);
            label4.Name = "label4";
            label4.Size = new Size(101, 19);
            label4.TabIndex = 2;
            label4.Text = "Costo ($/hora):";
            // 
            // rbCostoManual
            // 
            rbCostoManual.AutoSize = true;
            rbCostoManual.Font = new Font("Segoe UI", 10F);
            rbCostoManual.Location = new Point(250, 30);
            rbCostoManual.Name = "rbCostoManual";
            rbCostoManual.Size = new Size(149, 23);
            rbCostoManual.TabIndex = 1;
            rbCostoManual.TabStop = true;
            rbCostoManual.Text = "✏ Captura Manual";
            rbCostoManual.UseVisualStyleBackColor = true;
            rbCostoManual.CheckedChanged += rbCostoManual_CheckedChanged;
            // 
            // rbCostoCalculado
            // 
            rbCostoCalculado.AutoSize = true;
            rbCostoCalculado.Font = new Font("Segoe UI", 10F);
            rbCostoCalculado.Location = new Point(20, 30);
            rbCostoCalculado.Name = "rbCostoCalculado";
            rbCostoCalculado.Size = new Size(157, 23);
            rbCostoCalculado.TabIndex = 0;
            rbCostoCalculado.TabStop = true;
            rbCostoCalculado.Text = "\U0001f9ee Calculado (OPUS)";
            rbCostoCalculado.UseVisualStyleBackColor = true;
            rbCostoCalculado.CheckedChanged += rbCostoCalculado_CheckedChanged;
            // 
            // grpOrigen
            // 
            grpOrigen.Controls.Add(rbProyecto);
            grpOrigen.Controls.Add(rbMaestro);
            grpOrigen.Font = new Font("Segoe UI", 10F);
            grpOrigen.Location = new Point(30, 518);
            grpOrigen.Name = "grpOrigen";
            grpOrigen.Size = new Size(700, 70);
            grpOrigen.TabIndex = 9;
            grpOrigen.TabStop = false;
            grpOrigen.Text = "Origen del Registro";
            grpOrigen.Visible = false;
            // 
            // rbProyecto
            // 
            rbProyecto.AutoSize = true;
            rbProyecto.Location = new Point(350, 30);
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
            lblNotas.Location = new Point(30, 319);
            lblNotas.Name = "lblNotas";
            lblNotas.Size = new Size(48, 19);
            lblNotas.TabIndex = 10;
            lblNotas.Text = "Notas:";
            // 
            // txtNotas
            // 
            txtNotas.Font = new Font("Segoe UI", 9.5F);
            txtNotas.Location = new Point(30, 344);
            txtNotas.Multiline = true;
            txtNotas.Name = "txtNotas";
            txtNotas.ScrollBars = ScrollBars.Vertical;
            txtNotas.Size = new Size(700, 80);
            txtNotas.TabIndex = 11;
            // 
            // btnGuardar
            // 
            btnGuardar.BackColor = Color.FromArgb(76, 175, 80);
            btnGuardar.FlatStyle = FlatStyle.Flat;
            btnGuardar.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGuardar.ForeColor = Color.White;
            btnGuardar.Location = new Point(530, 449);
            btnGuardar.Name = "btnGuardar";
            btnGuardar.Size = new Size(200, 40);
            btnGuardar.TabIndex = 12;
            btnGuardar.Text = "💾 Guardar";
            btnGuardar.UseVisualStyleBackColor = false;
            btnGuardar.Click += btnGuardar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.BackColor = Color.FromArgb(220, 220, 220);
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Font = new Font("Segoe UI", 10F);
            btnCancelar.Location = new Point(410, 449);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(100, 40);
            btnCancelar.TabIndex = 13;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = false;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // FormEditarMaquinaria
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(764, 507);
            Controls.Add(btnCancelar);
            Controls.Add(btnGuardar);
            Controls.Add(txtNotas);
            Controls.Add(lblNotas);
            Controls.Add(grpOrigen);
            Controls.Add(grpCosto);
            Controls.Add(cboTipoCombustible);
            Controls.Add(lblTipoCombustible);
            Controls.Add(nudPotencia);
            Controls.Add(lblPotencia);
            Controls.Add(txtDescripcion);
            Controls.Add(lblDescripcion);
            Controls.Add(txtClave);
            Controls.Add(lblClave);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormEditarMaquinaria";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Editar Maquinaria";
            ((System.ComponentModel.ISupportInitialize)nudPotencia).EndInit();
            grpCosto.ResumeLayout(false);
            grpCosto.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudCostoHorario).EndInit();
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
        private System.Windows.Forms.Label lblPotencia;
        private System.Windows.Forms.NumericUpDown nudPotencia;
        private System.Windows.Forms.Label lblTipoCombustible;
        private System.Windows.Forms.ComboBox cboTipoCombustible;
        private System.Windows.Forms.GroupBox grpCosto;
        private System.Windows.Forms.RadioButton rbCostoCalculado;
        private System.Windows.Forms.RadioButton rbCostoManual;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown nudCostoHorario;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label lblFechaCalculo;
        private System.Windows.Forms.Label lblAdvertencia;
        private System.Windows.Forms.Button btnCalcular;
        private System.Windows.Forms.GroupBox grpOrigen;
        private System.Windows.Forms.RadioButton rbMaestro;
        private System.Windows.Forms.RadioButton rbProyecto;
        private System.Windows.Forms.Label lblNotas;
        private System.Windows.Forms.TextBox txtNotas;
        private System.Windows.Forms.Button btnGuardar;
        private System.Windows.Forms.Button btnCancelar;
    }
}
