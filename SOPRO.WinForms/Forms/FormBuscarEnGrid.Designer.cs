namespace SOPRO.WinForms.Forms
{
    partial class FormBuscarEnGrid
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox txtBuscar;
        private System.Windows.Forms.Button btnSiguiente;
        private System.Windows.Forms.Button btnCerrar;
        private System.Windows.Forms.Label lblEstado;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            txtBuscar = new TextBox();
            btnSiguiente = new Button();
            btnCerrar = new Button();
            lblEstado = new Label();
            SuspendLayout();
            // 
            // txtBuscar
            // 
            txtBuscar.Location = new Point(12, 12);
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(238, 23);
            txtBuscar.TabIndex = 0;
            txtBuscar.KeyDown += txtBuscar_KeyDown;
            // 
            // btnSiguiente
            // 
            btnSiguiente.Location = new Point(256, 11);
            btnSiguiente.Name = "btnSiguiente";
            btnSiguiente.Size = new Size(86, 25);
            btnSiguiente.TabIndex = 1;
            btnSiguiente.Text = "Siguiente";
            btnSiguiente.UseVisualStyleBackColor = true;
            btnSiguiente.Click += btnSiguiente_Click;
            // 
            // btnCerrar
            // 
            btnCerrar.Location = new Point(348, 11);
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(72, 25);
            btnCerrar.TabIndex = 2;
            btnCerrar.Text = "Cerrar";
            btnCerrar.UseVisualStyleBackColor = true;
            btnCerrar.Click += btnCerrar_Click;
            // 
            // lblEstado
            // 
            lblEstado.AutoEllipsis = true;
            lblEstado.Location = new Point(12, 47);
            lblEstado.Name = "lblEstado";
            lblEstado.Size = new Size(408, 32);
            lblEstado.TabIndex = 3;
            lblEstado.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // FormBuscarEnGrid
            // 
            AcceptButton = btnSiguiente;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(432, 88);
            Controls.Add(lblEstado);
            Controls.Add(btnCerrar);
            Controls.Add(btnSiguiente);
            Controls.Add(txtBuscar);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormBuscarEnGrid";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Buscar";
            TopMost = true;
            FormClosed += FormBuscarEnGrid_FormClosed;
            ResumeLayout(false);
            PerformLayout();

        }
    }
}
