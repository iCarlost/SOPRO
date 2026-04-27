namespace SOPRO.WinForms.Forms
{
    partial class FormRendimiento
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
            lblNombre = new Label();
            txtRendMedio = new TextBox();
            txt1RMedio = new TextBox();
            btnAceptar = new Button();
            btnCancelar = new Button();
            lblRendH = new Label();
            lbl1RH = new Label();
            SuspendLayout();
            // 
            // lblNombre
            // 
            lblNombre.AutoEllipsis = true;
            lblNombre.Location = new Point(12, 9);
            lblNombre.Name = "lblNombre";
            lblNombre.Size = new Size(226, 25);
            lblNombre.TabIndex = 0;
            // 
            // txtRendMedio
            // 
            txtRendMedio.Location = new Point(15, 59);
            txtRendMedio.Name = "txtRendMedio";
            txtRendMedio.Size = new Size(100, 23);
            txtRendMedio.TabIndex = 1;
            txtRendMedio.TextAlign = HorizontalAlignment.Right;
            txtRendMedio.TextChanged += txtRendMedio_TextChanged;
            // 
            // txt1RMedio
            // 
            txt1RMedio.Location = new Point(130, 59);
            txt1RMedio.Name = "txt1RMedio";
            txt1RMedio.Size = new Size(100, 23);
            txt1RMedio.TabIndex = 2;
            txt1RMedio.TextAlign = HorizontalAlignment.Right;
            txt1RMedio.TextChanged += txt1RMedio_TextChanged;
            // 
            // btnAceptar
            // 
            btnAceptar.Location = new Point(15, 99);
            btnAceptar.Name = "btnAceptar";
            btnAceptar.Size = new Size(75, 23);
            btnAceptar.TabIndex = 3;
            btnAceptar.Text = "Aceptar";
            btnAceptar.UseVisualStyleBackColor = true;
            btnAceptar.Click += btnAceptar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.Location = new Point(155, 99);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(75, 23);
            btnCancelar.TabIndex = 4;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = true;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // lblRendH
            // 
            lblRendH.AutoSize = true;
            lblRendH.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblRendH.Location = new Point(15, 41);
            lblRendH.Name = "lblRendH";
            lblRendH.Size = new Size(79, 15);
            lblRendH.TabIndex = 5;
            lblRendH.Text = "Rendimiento";
            // 
            // lbl1RH
            // 
            lbl1RH.AutoSize = true;
            lbl1RH.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lbl1RH.Location = new Point(130, 41);
            lbl1RH.Name = "lbl1RH";
            lbl1RH.Size = new Size(97, 15);
            lbl1RH.TabIndex = 6;
            lbl1RH.Text = "1 / Rendimiento";
            // 
            // FormRendimiento
            // 
            AcceptButton = btnAceptar;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancelar;
            ClientSize = new Size(245, 135);
            Controls.Add(lblNombre);
            Controls.Add(lblRendH);
            Controls.Add(lbl1RH);
            Controls.Add(txtRendMedio);
            Controls.Add(txt1RMedio);
            Controls.Add(btnAceptar);
            Controls.Add(btnCancelar);
            Name = "FormRendimiento";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Rendimiento";
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion

        private System.Windows.Forms.Label lblRendH, lbl1RH;
        private System.Windows.Forms.RadioButton rbMinimo, rbMedio, rbOptimo;
        private System.Windows.Forms.TextBox txtRendMinimo, txt1RMinimo;
        private System.Windows.Forms.TextBox txtRendMedio,  txt1RMedio;
        private System.Windows.Forms.TextBox txtRendOptimo, txt1ROptimo;
        private System.Windows.Forms.Button btnAceptar, btnCancelar;
        private System.Windows.Forms.Label lblNombre;
    }
}
