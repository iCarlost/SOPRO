namespace SOPRO.WinForms.Forms
{
    partial class FormSeleccionReporteMO
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
            grpOpciones = new GroupBox();
            rbCatalogo = new RadioButton();
            rbTabulador = new RadioButton();
            lblTitulo = new Label();
            panelOpcion1 = new Panel();
            lblDescCatalogo = new Label();
            panelOpcion2 = new Panel();
            lblDescTabulador = new Label();
            btnGenerar = new Button();
            btnCancelar = new Button();
            grpOpciones.SuspendLayout();
            panelOpcion1.SuspendLayout();
            panelOpcion2.SuspendLayout();
            SuspendLayout();
            // 
            // grpOpciones
            // 
            grpOpciones.BackColor = Color.Transparent;
            grpOpciones.Controls.Add(rbCatalogo);
            grpOpciones.Controls.Add(rbTabulador);
            grpOpciones.FlatStyle = FlatStyle.Flat;
            grpOpciones.Location = new Point(16, 48);
            grpOpciones.Name = "grpOpciones";
            grpOpciones.Size = new Size(376, 134);
            grpOpciones.TabIndex = 0;
            grpOpciones.TabStop = false;
            // 
            // rbCatalogo
            // 
            rbCatalogo.AutoSize = true;
            rbCatalogo.Checked = true;
            rbCatalogo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            rbCatalogo.Location = new Point(26, 37);
            rbCatalogo.Name = "rbCatalogo";
            rbCatalogo.Size = new Size(171, 19);
            rbCatalogo.TabIndex = 0;
            rbCatalogo.TabStop = true;
            rbCatalogo.Text = "Catálogo de Mano de Obra";
            rbCatalogo.CheckedChanged += rbCatalogo_CheckedChanged;
            // 
            // rbTabulador
            // 
            rbTabulador.AutoSize = true;
            rbTabulador.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            rbTabulador.Location = new Point(26, 78);
            rbTabulador.Name = "rbTabulador";
            rbTabulador.Size = new Size(167, 19);
            rbTabulador.TabIndex = 0;
            rbTabulador.Text = "Tabulador desglosado FSR";
            rbTabulador.CheckedChanged += rbTabulador_CheckedChanged;
            // 
            // lblTitulo
            // 
            lblTitulo.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.FromArgb(51, 51, 76);
            lblTitulo.Location = new Point(16, 16);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(380, 22);
            lblTitulo.TabIndex = 1;
            lblTitulo.Text = "¿Qué reporte desea generar?";
            // 
            // panelOpcion1
            // 
            panelOpcion1.BackColor = Color.FromArgb(232, 234, 246);
            panelOpcion1.BorderStyle = BorderStyle.FixedSingle;
            panelOpcion1.Controls.Add(lblDescCatalogo);
            panelOpcion1.Cursor = Cursors.Hand;
            panelOpcion1.Location = new Point(16, 48);
            panelOpcion1.Name = "panelOpcion1";
            panelOpcion1.Size = new Size(376, 60);
            panelOpcion1.TabIndex = 0;
            panelOpcion1.Click += panelOpcion1_Click;
            // 
            // lblDescCatalogo
            // 
            lblDescCatalogo.Font = new Font("Segoe UI", 8F);
            lblDescCatalogo.ForeColor = Color.DimGray;
            lblDescCatalogo.Location = new Point(28, 30);
            lblDescCatalogo.Name = "lblDescCatalogo";
            lblDescCatalogo.Size = new Size(336, 22);
            lblDescCatalogo.TabIndex = 0;
            lblDescCatalogo.Text = "Lista de insumos con clave, descripción, salario base, FSR y salario real.";
            lblDescCatalogo.Click += panelOpcion1_Click;
            // 
            // panelOpcion2
            // 
            panelOpcion2.BackColor = Color.FromArgb(245, 245, 255);
            panelOpcion2.BorderStyle = BorderStyle.FixedSingle;
            panelOpcion2.Controls.Add(lblDescTabulador);
            panelOpcion2.Cursor = Cursors.Hand;
            panelOpcion2.Location = new Point(16, 118);
            panelOpcion2.Name = "panelOpcion2";
            panelOpcion2.Size = new Size(376, 60);
            panelOpcion2.TabIndex = 1;
            panelOpcion2.Click += panelOpcion2_Click;
            // 
            // lblDescTabulador
            // 
            lblDescTabulador.Font = new Font("Segoe UI", 8F);
            lblDescTabulador.ForeColor = Color.DimGray;
            lblDescTabulador.Location = new Point(28, 30);
            lblDescTabulador.Name = "lblDescTabulador";
            lblDescTabulador.Size = new Size(336, 22);
            lblDescTabulador.TabIndex = 0;
            lblDescTabulador.Text = "Tabla con el cálculo FSR desglosado por cuotas IMSS/INFONAVIT para cada insumo.";
            lblDescTabulador.Click += panelOpcion2_Click;
            // 
            // btnGenerar
            // 
            btnGenerar.BackColor = Color.FromArgb(51, 51, 76);
            btnGenerar.FlatAppearance.BorderSize = 0;
            btnGenerar.FlatStyle = FlatStyle.Flat;
            btnGenerar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGenerar.ForeColor = Color.White;
            btnGenerar.Location = new Point(208, 196);
            btnGenerar.Name = "btnGenerar";
            btnGenerar.Size = new Size(90, 30);
            btnGenerar.TabIndex = 2;
            btnGenerar.Text = "Generar";
            btnGenerar.UseVisualStyleBackColor = false;
            btnGenerar.Click += btnGenerar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Font = new Font("Segoe UI", 9F);
            btnCancelar.Location = new Point(306, 196);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(80, 30);
            btnCancelar.TabIndex = 3;
            btnCancelar.Text = "Cancelar";
            btnCancelar.Click += btnCancelar_Click;
            // 
            // FormSeleccionReporteMO
            // 
            AcceptButton = btnGenerar;
            BackColor = Color.White;
            CancelButton = btnCancelar;
            ClientSize = new Size(408, 242);
            Controls.Add(grpOpciones);
            Controls.Add(lblTitulo);
            Controls.Add(panelOpcion1);
            Controls.Add(panelOpcion2);
            Controls.Add(btnGenerar);
            Controls.Add(btnCancelar);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormSeleccionReporteMO";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Seleccionar tipo de reporte";
            grpOpciones.ResumeLayout(false);
            grpOpciones.PerformLayout();
            panelOpcion1.ResumeLayout(false);
            panelOpcion2.ResumeLayout(false);
            ResumeLayout(false);
        }
        #endregion

        private System.Windows.Forms.GroupBox grpOpciones;
        private System.Windows.Forms.Label   lblTitulo;
        private System.Windows.Forms.Panel   panelOpcion1;
        private System.Windows.Forms.RadioButton rbCatalogo;
        private System.Windows.Forms.Label   lblDescCatalogo;
        private System.Windows.Forms.Panel   panelOpcion2;
        private System.Windows.Forms.RadioButton rbTabulador;
        private System.Windows.Forms.Label   lblDescTabulador;
        private System.Windows.Forms.Button  btnGenerar;
        private System.Windows.Forms.Button  btnCancelar;
    }
}
