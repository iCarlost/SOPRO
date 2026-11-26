namespace SOPRO.WinForms.Forms
{
    partial class FormEditarMatriz
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Desuscribirse del evento para evitar errores al cerrar
                SOPRO.WinForms.Helpers.FormatoHelper.ConfiguracionCambiada -= OnConfiguracionCambiada;
                
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            grpDatos = new GroupBox();
            rbBasico = new RadioButton();
            rbAPU = new RadioButton();
            rbCuadrilla = new RadioButton();
            lblTipo = new Label();
            cboUnidad = new ComboBox();
            lblUnidad = new Label();
            txtDescripcion = new TextBox();
            lblDescripcion = new Label();
            txtClave = new TextBox();
            lblClave = new Label();
            grpComponentes = new GroupBox();
            dgvComponentes = new DataGridView();
            btnAgregarBasico = new global::SOPRO.WinForms.UI.Controls.SoproButton();
            btnAgregarHerramienta = new global::SOPRO.WinForms.UI.Controls.SoproButton();
            btnAgregarMaquinaria = new global::SOPRO.WinForms.UI.Controls.SoproButton();
            btnAgregarManoObra = new global::SOPRO.WinForms.UI.Controls.SoproButton();
            btnAgregarMaterial = new global::SOPRO.WinForms.UI.Controls.SoproButton();
            grpResumen = new GroupBox();
            lblCostoDirecto = new Label();
            lblCostoDirectoLabel = new Label();
            label1 = new Label();
            lblTotalBasicos = new Label();
            lblTotalBasicosLabel = new Label();
            lblTotalMaquinaria = new Label();
            lblTotalMaquinariaLabel = new Label();
            lblTotalManoObra = new Label();
            lblTotalManoObraLabel = new Label();
            lblTotalMaterial = new Label();
            lblTotalMaterialLabel = new Label();
            btnGuardar = new global::SOPRO.WinForms.UI.Controls.SoproButton();
            btnCancelar = new global::SOPRO.WinForms.UI.Controls.SoproButton();
            grpDatos.SuspendLayout();
            grpComponentes.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvComponentes).BeginInit();
            grpResumen.SuspendLayout();
            SuspendLayout();
            // 
            // grpDatos
            // 
            grpDatos.Controls.Add(rbBasico);
            grpDatos.Controls.Add(rbAPU);
            grpDatos.Controls.Add(rbCuadrilla);
            grpDatos.Controls.Add(lblTipo);
            grpDatos.Controls.Add(cboUnidad);
            grpDatos.Controls.Add(lblUnidad);
            grpDatos.Controls.Add(txtDescripcion);
            grpDatos.Controls.Add(lblDescripcion);
            grpDatos.Controls.Add(txtClave);
            grpDatos.Controls.Add(lblClave);
            grpDatos.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpDatos.Location = new Point(20, 20);
            grpDatos.Name = "grpDatos";
            grpDatos.Size = new Size(1140, 100);
            grpDatos.TabIndex = 0;
            grpDatos.TabStop = false;
            grpDatos.Text = "DATOS GENERALES";
            // 
            // rbBasico
            // 
            rbBasico.AutoSize = true;
            rbBasico.Location = new Point(500, 63);
            rbBasico.Name = "rbBasico";
            rbBasico.Size = new Size(94, 23);
            rbBasico.TabIndex = 8;
            rbBasico.Text = "Básico";
            rbBasico.UseVisualStyleBackColor = true;
            rbBasico.CheckedChanged += rbTipo_CheckedChanged;
            // 
            // rbAPU
            // 
            rbAPU.AutoSize = true;
            rbAPU.Checked = true;
            rbAPU.Location = new Point(390, 63);
            rbAPU.Name = "rbAPU";
            rbAPU.Size = new Size(80, 23);
            rbAPU.TabIndex = 7;
            rbAPU.TabStop = true;
            rbAPU.Text = "APU";
            rbAPU.UseVisualStyleBackColor = true;
            rbAPU.CheckedChanged += rbTipo_CheckedChanged;
            // 
            // rbCuadrilla
            // 
            rbCuadrilla.AutoSize = true;
            rbCuadrilla.Location = new Point(620, 63);
            rbCuadrilla.Name = "rbCuadrilla";
            rbCuadrilla.Size = new Size(111, 23);
            rbCuadrilla.TabIndex = 9;
            rbCuadrilla.Text = "Cuadrilla";
            rbCuadrilla.UseVisualStyleBackColor = true;
            rbCuadrilla.CheckedChanged += rbTipo_CheckedChanged;
            // 
            // lblTipo
            // 
            lblTipo.AutoSize = true;
            lblTipo.Location = new Point(290, 65);
            lblTipo.Name = "lblTipo";
            lblTipo.Size = new Size(43, 19);
            lblTipo.TabIndex = 6;
            lblTipo.Text = "Tipo:";
            // 
            // cboUnidad
            // 
            cboUnidad.FormattingEnabled = true;
            cboUnidad.Items.AddRange(new object[] { "m", "m2", "m3", "kg", "ton", "pza", "lote", "jor", "ml", "lt" });
            cboUnidad.Location = new Point(120, 62);
            cboUnidad.Name = "cboUnidad";
            cboUnidad.Size = new Size(100, 25);
            cboUnidad.TabIndex = 5;
            // 
            // lblUnidad
            // 
            lblUnidad.AutoSize = true;
            lblUnidad.Location = new Point(20, 65);
            lblUnidad.Name = "lblUnidad";
            lblUnidad.Size = new Size(61, 19);
            lblUnidad.TabIndex = 4;
            lblUnidad.Text = "Unidad:";
            // 
            // txtDescripcion
            // 
            txtDescripcion.Location = new Point(390, 27);
            txtDescripcion.MaxLength = 2000;
            txtDescripcion.Name = "txtDescripcion";
            txtDescripcion.Size = new Size(500, 25);
            txtDescripcion.TabIndex = 3;
            // 
            // lblDescripcion
            // 
            lblDescripcion.AutoSize = true;
            lblDescripcion.Location = new Point(290, 30);
            lblDescripcion.Name = "lblDescripcion";
            lblDescripcion.Size = new Size(91, 19);
            lblDescripcion.TabIndex = 2;
            lblDescripcion.Text = "Descripción:";
            // 
            // txtClave
            // 
            txtClave.CharacterCasing = CharacterCasing.Upper;
            txtClave.Location = new Point(120, 27);
            txtClave.MaxLength = 50;
            txtClave.Name = "txtClave";
            txtClave.Size = new Size(150, 25);
            txtClave.TabIndex = 1;
            // 
            // lblClave
            // 
            lblClave.AutoSize = true;
            lblClave.Location = new Point(20, 30);
            lblClave.Name = "lblClave";
            lblClave.Size = new Size(50, 19);
            lblClave.TabIndex = 0;
            lblClave.Text = "Clave:";
            // 
            // grpComponentes
            // 
            grpComponentes.Controls.Add(dgvComponentes);
            grpComponentes.Controls.Add(btnAgregarBasico);
            grpComponentes.Controls.Add(btnAgregarHerramienta);
            grpComponentes.Controls.Add(btnAgregarMaquinaria);
            grpComponentes.Controls.Add(btnAgregarManoObra);
            grpComponentes.Controls.Add(btnAgregarMaterial);
            grpComponentes.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpComponentes.Location = new Point(20, 130);
            grpComponentes.Name = "grpComponentes";
            grpComponentes.Size = new Size(900, 550);
            grpComponentes.TabIndex = 1;
            grpComponentes.TabStop = false;
            grpComponentes.Text = "COMPONENTES";
            // 
            // dgvComponentes
            // 
            dgvComponentes.BackgroundColor = Color.White;
            dgvComponentes.BorderStyle = BorderStyle.None;
            dgvComponentes.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvComponentes.Location = new Point(20, 76);
            dgvComponentes.Name = "dgvComponentes";
            dgvComponentes.Size = new Size(860, 454);
            dgvComponentes.TabIndex = 5;
            // 
            // btnAgregarBasico
            // 
            btnAgregarBasico.FlatStyle = FlatStyle.Flat;
            btnAgregarBasico.Location = new Point(509, 30);
            btnAgregarBasico.Name = "btnAgregarBasico";
            btnAgregarBasico.Size = new Size(155, 40);
            btnAgregarBasico.TabIndex = 3;
            btnAgregarBasico.Text = "Básico";
            btnAgregarBasico.Click += btnAgregarBasico_Click;
            // 
            // btnAgregarHerramienta
            // 
            btnAgregarHerramienta.FlatStyle = FlatStyle.Flat;
            btnAgregarHerramienta.Location = new Point(672, 30);
            btnAgregarHerramienta.Name = "btnAgregarHerramienta";
            btnAgregarHerramienta.Size = new Size(155, 40);
            btnAgregarHerramienta.TabIndex = 4;
            btnAgregarHerramienta.Text = "Herramienta";
            btnAgregarHerramienta.Click += btnAgregarHerramienta_Click;
            // 
            // btnAgregarMaquinaria
            // 
            btnAgregarMaquinaria.FlatStyle = FlatStyle.Flat;
            btnAgregarMaquinaria.Location = new Point(346, 30);
            btnAgregarMaquinaria.Name = "btnAgregarMaquinaria";
            btnAgregarMaquinaria.Size = new Size(155, 40);
            btnAgregarMaquinaria.TabIndex = 2;
            btnAgregarMaquinaria.Text = "Maquinaria";
            btnAgregarMaquinaria.Click += btnAgregarMaquinaria_Click;
            // 
            // btnAgregarManoObra
            // 
            btnAgregarManoObra.FlatStyle = FlatStyle.Flat;
            btnAgregarManoObra.Location = new Point(183, 30);
            btnAgregarManoObra.Name = "btnAgregarManoObra";
            btnAgregarManoObra.Size = new Size(155, 40);
            btnAgregarManoObra.TabIndex = 1;
            btnAgregarManoObra.Text = "Mano de Obra";
            btnAgregarManoObra.Click += btnAgregarManoObra_Click;
            // 
            // btnAgregarMaterial
            // 
            btnAgregarMaterial.FlatStyle = FlatStyle.Flat;
            btnAgregarMaterial.Location = new Point(20, 30);
            btnAgregarMaterial.Name = "btnAgregarMaterial";
            btnAgregarMaterial.Size = new Size(155, 40);
            btnAgregarMaterial.TabIndex = 0;
            btnAgregarMaterial.Text = "Material";
            btnAgregarMaterial.Click += btnAgregarMaterial_Click;
            // 
            // grpResumen
            // 
            grpResumen.Controls.Add(lblCostoDirecto);
            grpResumen.Controls.Add(lblCostoDirectoLabel);
            grpResumen.Controls.Add(label1);
            grpResumen.Controls.Add(lblTotalBasicos);
            grpResumen.Controls.Add(lblTotalBasicosLabel);
            grpResumen.Controls.Add(lblTotalMaquinaria);
            grpResumen.Controls.Add(lblTotalMaquinariaLabel);
            grpResumen.Controls.Add(lblTotalManoObra);
            grpResumen.Controls.Add(lblTotalManoObraLabel);
            grpResumen.Controls.Add(lblTotalMaterial);
            grpResumen.Controls.Add(lblTotalMaterialLabel);
            grpResumen.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpResumen.Location = new Point(940, 130);
            grpResumen.Name = "grpResumen";
            grpResumen.Size = new Size(240, 550);
            grpResumen.TabIndex = 2;
            grpResumen.TabStop = false;
            grpResumen.Text = "RESUMEN";
            // 
            // lblCostoDirecto
            // 
            lblCostoDirecto.AutoSize = true;
            lblCostoDirecto.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblCostoDirecto.ForeColor = Color.FromArgb(46, 125, 50);
            lblCostoDirecto.Location = new Point(15, 444);
            lblCostoDirecto.Name = "lblCostoDirecto";
            lblCostoDirecto.Size = new Size(71, 30);
            lblCostoDirecto.TabIndex = 10;
            lblCostoDirecto.Text = "$0.00";
            // 
            // lblCostoDirectoLabel
            // 
            lblCostoDirectoLabel.AutoSize = true;
            lblCostoDirectoLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblCostoDirectoLabel.Location = new Point(15, 414);
            lblCostoDirectoLabel.Name = "lblCostoDirectoLabel";
            lblCostoDirectoLabel.Size = new Size(126, 20);
            lblCostoDirectoLabel.TabIndex = 9;
            lblCostoDirectoLabel.Text = "COSTO DIRECTO:";
            // 
            // label1
            // 
            label1.BackColor = Color.Gray;
            label1.Location = new Point(15, 399);
            label1.Name = "label1";
            label1.Size = new Size(210, 2);
            label1.TabIndex = 8;
            // 
            // lblTotalBasicos
            // 
            lblTotalBasicos.AutoSize = true;
            lblTotalBasicos.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblTotalBasicos.ForeColor = Color.FromArgb(156, 39, 176);
            lblTotalBasicos.Location = new Point(15, 324);
            lblTotalBasicos.Name = "lblTotalBasicos";
            lblTotalBasicos.Size = new Size(49, 20);
            lblTotalBasicos.TabIndex = 7;
            lblTotalBasicos.Text = "$0.00";
            // 
            // lblTotalBasicosLabel
            // 
            lblTotalBasicosLabel.AutoSize = true;
            lblTotalBasicosLabel.ForeColor = Color.FromArgb(156, 39, 176);
            lblTotalBasicosLabel.Location = new Point(15, 299);
            lblTotalBasicosLabel.Name = "lblTotalBasicosLabel";
            lblTotalBasicosLabel.Size = new Size(99, 19);
            lblTotalBasicosLabel.TabIndex = 6;
            lblTotalBasicosLabel.Text = "Total Básicos:";
            // 
            // lblTotalMaquinaria
            // 
            lblTotalMaquinaria.AutoSize = true;
            lblTotalMaquinaria.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblTotalMaquinaria.ForeColor = Color.FromArgb(255, 152, 0);
            lblTotalMaquinaria.Location = new Point(15, 249);
            lblTotalMaquinaria.Name = "lblTotalMaquinaria";
            lblTotalMaquinaria.Size = new Size(49, 20);
            lblTotalMaquinaria.TabIndex = 5;
            lblTotalMaquinaria.Text = "$0.00";
            // 
            // lblTotalMaquinariaLabel
            // 
            lblTotalMaquinariaLabel.AutoSize = true;
            lblTotalMaquinariaLabel.ForeColor = Color.FromArgb(255, 152, 0);
            lblTotalMaquinariaLabel.Location = new Point(15, 224);
            lblTotalMaquinariaLabel.Name = "lblTotalMaquinariaLabel";
            lblTotalMaquinariaLabel.Size = new Size(126, 19);
            lblTotalMaquinariaLabel.TabIndex = 4;
            lblTotalMaquinariaLabel.Text = "Total Maquinaria:";
            // 
            // lblTotalManoObra
            // 
            lblTotalManoObra.AutoSize = true;
            lblTotalManoObra.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblTotalManoObra.ForeColor = Color.FromArgb(76, 175, 80);
            lblTotalManoObra.Location = new Point(15, 174);
            lblTotalManoObra.Name = "lblTotalManoObra";
            lblTotalManoObra.Size = new Size(49, 20);
            lblTotalManoObra.TabIndex = 3;
            lblTotalManoObra.Text = "$0.00";
            // 
            // lblTotalManoObraLabel
            // 
            lblTotalManoObraLabel.AutoSize = true;
            lblTotalManoObraLabel.ForeColor = Color.FromArgb(76, 175, 80);
            lblTotalManoObraLabel.Location = new Point(15, 149);
            lblTotalManoObraLabel.Name = "lblTotalManoObraLabel";
            lblTotalManoObraLabel.Size = new Size(147, 19);
            lblTotalManoObraLabel.TabIndex = 2;
            lblTotalManoObraLabel.Text = "Total Mano de Obra:";
            // 
            // lblTotalMaterial
            // 
            lblTotalMaterial.AutoSize = true;
            lblTotalMaterial.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblTotalMaterial.ForeColor = Color.FromArgb(33, 150, 243);
            lblTotalMaterial.Location = new Point(15, 99);
            lblTotalMaterial.Name = "lblTotalMaterial";
            lblTotalMaterial.Size = new Size(49, 20);
            lblTotalMaterial.TabIndex = 1;
            lblTotalMaterial.Text = "$0.00";
            // 
            // lblTotalMaterialLabel
            // 
            lblTotalMaterialLabel.AutoSize = true;
            lblTotalMaterialLabel.ForeColor = Color.FromArgb(33, 150, 243);
            lblTotalMaterialLabel.Location = new Point(15, 74);
            lblTotalMaterialLabel.Name = "lblTotalMaterialLabel";
            lblTotalMaterialLabel.Size = new Size(106, 19);
            lblTotalMaterialLabel.TabIndex = 0;
            lblTotalMaterialLabel.Text = "Total Material:";
            // 
            // btnGuardar
            // 
            btnGuardar.FlatStyle = FlatStyle.Flat;
            btnGuardar.Location = new Point(1070, 700);
            btnGuardar.Name = "btnGuardar";
            btnGuardar.Size = new Size(110, 45);
            btnGuardar.TabIndex = 3;
            btnGuardar.Text = "Guardar";
            btnGuardar.Click += btnGuardar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Location = new Point(950, 700);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(110, 45);
            btnCancelar.TabIndex = 4;
            btnCancelar.Text = "Cancelar";
            btnCancelar.Click += btnCancelar_Click;
            // 
            // FormEditarMatriz
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1200, 800);
            Controls.Add(btnCancelar);
            Controls.Add(btnGuardar);
            Controls.Add(grpResumen);
            Controls.Add(grpComponentes);
            Controls.Add(grpDatos);
            Font = new Font("Segoe UI", 9F);
            Name = "FormEditarMatriz";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Editor de Matriz (APU)";
            grpDatos.ResumeLayout(false);
            grpDatos.PerformLayout();
            grpComponentes.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvComponentes).EndInit();
            grpResumen.ResumeLayout(false);
            grpResumen.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox grpDatos;
        private System.Windows.Forms.RadioButton rbBasico;
        private System.Windows.Forms.RadioButton rbAPU;
        private System.Windows.Forms.RadioButton rbCuadrilla;
        private System.Windows.Forms.Label lblTipo;
        private System.Windows.Forms.ComboBox cboUnidad;
        private System.Windows.Forms.Label lblUnidad;
        private System.Windows.Forms.TextBox txtDescripcion;
        private System.Windows.Forms.Label lblDescripcion;
        private System.Windows.Forms.TextBox txtClave;
        private System.Windows.Forms.Label lblClave;
        private System.Windows.Forms.GroupBox grpComponentes;
        private System.Windows.Forms.DataGridView dgvComponentes;
        private global::SOPRO.WinForms.UI.Controls.SoproButton btnAgregarBasico;
        private global::SOPRO.WinForms.UI.Controls.SoproButton btnAgregarHerramienta;
        private global::SOPRO.WinForms.UI.Controls.SoproButton btnAgregarMaquinaria;
        private global::SOPRO.WinForms.UI.Controls.SoproButton btnAgregarManoObra;
        private global::SOPRO.WinForms.UI.Controls.SoproButton btnAgregarMaterial;
        private System.Windows.Forms.GroupBox grpResumen;
        private System.Windows.Forms.Label lblCostoDirecto;
        private System.Windows.Forms.Label lblCostoDirectoLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblTotalBasicos;
        private System.Windows.Forms.Label lblTotalBasicosLabel;
        private System.Windows.Forms.Label lblTotalMaquinaria;
        private System.Windows.Forms.Label lblTotalMaquinariaLabel;
        private System.Windows.Forms.Label lblTotalManoObra;
        private System.Windows.Forms.Label lblTotalManoObraLabel;
        private System.Windows.Forms.Label lblTotalMaterial;
        private System.Windows.Forms.Label lblTotalMaterialLabel;
        private global::SOPRO.WinForms.UI.Controls.SoproButton btnGuardar;
        private global::SOPRO.WinForms.UI.Controls.SoproButton btnCancelar;
    }
}
