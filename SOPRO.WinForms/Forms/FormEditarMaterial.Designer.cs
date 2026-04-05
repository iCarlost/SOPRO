namespace SOPRO.WinForms.Forms
{
    partial class FormEditarMaterial
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
            txtUnidad = new TextBox();
            lblPrecio = new Label();
            nudPrecio = new NumericUpDown();
            lblNotas = new Label();
            txtNotas = new TextBox();
            btnGuardar = new Button();
            btnCancelar = new Button();
            grpOrigen = new GroupBox();
            rbProyecto = new RadioButton();
            rbMaestro = new RadioButton();
            ((System.ComponentModel.ISupportInitialize)nudPrecio).BeginInit();
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
            txtClave.Location = new Point(150, 27);
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
            txtDescripcion.Location = new Point(150, 67);
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
            // txtUnidad
            // 
            txtUnidad.CharacterCasing = CharacterCasing.Lower;
            txtUnidad.Font = new Font("Segoe UI", 10F);
            txtUnidad.Location = new Point(150, 107);
            txtUnidad.MaxLength = 20;
            txtUnidad.Name = "txtUnidad";
            txtUnidad.Size = new Size(100, 25);
            txtUnidad.TabIndex = 5;
            // 
            // lblPrecio
            // 
            lblPrecio.AutoSize = true;
            lblPrecio.Font = new Font("Segoe UI", 10F);
            lblPrecio.Location = new Point(280, 110);
            lblPrecio.Name = "lblPrecio";
            lblPrecio.Size = new Size(102, 19);
            lblPrecio.TabIndex = 6;
            lblPrecio.Text = "Precio Unitario:";
            // 
            // nudPrecio
            // 
            nudPrecio.DecimalPlaces = 4;
            nudPrecio.Font = new Font("Segoe UI", 10F);
            nudPrecio.Location = new Point(400, 107);
            nudPrecio.Maximum = new decimal(new int[] { 999999999, 0, 0, 0 });
            nudPrecio.Name = "nudPrecio";
            nudPrecio.Size = new Size(200, 25);
            nudPrecio.TabIndex = 7;
            nudPrecio.TextAlign = HorizontalAlignment.Right;
            nudPrecio.ThousandsSeparator = true;
            // 
            // lblNotas
            // 
            lblNotas.AutoSize = true;
            lblNotas.Font = new Font("Segoe UI", 10F);
            lblNotas.Location = new Point(30, 155);
            lblNotas.Name = "lblNotas";
            lblNotas.Size = new Size(48, 19);
            lblNotas.TabIndex = 9;
            lblNotas.Text = "Notas:";
            // 
            // txtNotas
            // 
            txtNotas.Font = new Font("Segoe UI", 9.5F);
            txtNotas.Location = new Point(30, 180);
            txtNotas.Multiline = true;
            txtNotas.Name = "txtNotas";
            txtNotas.ScrollBars = ScrollBars.Vertical;
            txtNotas.Size = new Size(570, 80);
            txtNotas.TabIndex = 10;
            // 
            // btnGuardar
            // 
            btnGuardar.BackColor = Color.FromArgb(76, 175, 80);
            btnGuardar.FlatStyle = FlatStyle.Flat;
            btnGuardar.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGuardar.ForeColor = Color.White;
            btnGuardar.Location = new Point(400, 285);
            btnGuardar.Name = "btnGuardar";
            btnGuardar.Size = new Size(200, 40);
            btnGuardar.TabIndex = 11;
            btnGuardar.Text = "💾 Guardar";
            btnGuardar.UseVisualStyleBackColor = false;
            btnGuardar.Click += btnGuardar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.BackColor = Color.FromArgb(220, 220, 220);
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Font = new Font("Segoe UI", 10F);
            btnCancelar.Location = new Point(280, 285);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(100, 40);
            btnCancelar.TabIndex = 12;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = false;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // grpOrigen
            // 
            grpOrigen.Controls.Add(rbProyecto);
            grpOrigen.Controls.Add(rbMaestro);
            grpOrigen.Font = new Font("Segoe UI", 10F);
            grpOrigen.Location = new Point(30, 366);
            grpOrigen.Name = "grpOrigen";
            grpOrigen.Size = new Size(575, 70);
            grpOrigen.TabIndex = 13;
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
            // FormEditarMaterial
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(634, 346);
            Controls.Add(grpOrigen);
            Controls.Add(btnCancelar);
            Controls.Add(btnGuardar);
            Controls.Add(txtNotas);
            Controls.Add(lblNotas);
            Controls.Add(nudPrecio);
            Controls.Add(lblPrecio);
            Controls.Add(txtUnidad);
            Controls.Add(lblUnidad);
            Controls.Add(txtDescripcion);
            Controls.Add(lblDescripcion);
            Controls.Add(txtClave);
            Controls.Add(lblClave);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormEditarMaterial";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Editar Material";
            ((System.ComponentModel.ISupportInitialize)nudPrecio).EndInit();
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
        private System.Windows.Forms.TextBox txtUnidad;
        private System.Windows.Forms.Label lblPrecio;
        private System.Windows.Forms.NumericUpDown nudPrecio;
        private System.Windows.Forms.Label lblNotas;
        private System.Windows.Forms.TextBox txtNotas;
        private System.Windows.Forms.Button btnGuardar;
        private System.Windows.Forms.Button btnCancelar;
        private GroupBox grpOrigen;
        private RadioButton rbProyecto;
        private RadioButton rbMaestro;
    }
}
