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
            this.components = new System.ComponentModel.Container();

            this.lblRendH       = new System.Windows.Forms.Label();
            this.lbl1RH         = new System.Windows.Forms.Label();

            this.rbMinimo       = new System.Windows.Forms.RadioButton();
            this.txtRendMinimo  = new System.Windows.Forms.TextBox();
            this.txt1RMinimo    = new System.Windows.Forms.TextBox();

            this.rbMedio        = new System.Windows.Forms.RadioButton();
            this.txtRendMedio   = new System.Windows.Forms.TextBox();
            this.txt1RMedio     = new System.Windows.Forms.TextBox();

            this.rbOptimo       = new System.Windows.Forms.RadioButton();
            this.txtRendOptimo  = new System.Windows.Forms.TextBox();
            this.txt1ROptimo    = new System.Windows.Forms.TextBox();

            this.btnAceptar     = new System.Windows.Forms.Button();
            this.btnCancelar    = new System.Windows.Forms.Button();

            this.SuspendLayout();

            // ── Headers ───────────────────────────────────────────────────────
            this.lblRendH.AutoSize = true;
            this.lblRendH.Font     = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            this.lblRendH.Location = new System.Drawing.Point(100, 15);
            this.lblRendH.Name     = "lblRendH";
            this.lblRendH.Text     = "Rendimiento:";

            this.lbl1RH.AutoSize = true;
            this.lbl1RH.Font     = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            this.lbl1RH.Location = new System.Drawing.Point(230, 15);
            this.lbl1RH.Name     = "lbl1RH";
            this.lbl1RH.Text     = "1/Rendimiento:";

            // ── Fila Mínimo  (y=42) ───────────────────────────────────────────
            this.rbMinimo.AutoSize = true;
            this.rbMinimo.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.rbMinimo.Location = new System.Drawing.Point(15, 44);
            this.rbMinimo.Name     = "rbMinimo";
            this.rbMinimo.Size     = new System.Drawing.Size(72, 19);
            this.rbMinimo.Text     = "Minimo";

            this.txtRendMinimo.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.txtRendMinimo.Location  = new System.Drawing.Point(100, 42);
            this.txtRendMinimo.Name      = "txtRendMinimo";
            this.txtRendMinimo.Size      = new System.Drawing.Size(110, 23);
            this.txtRendMinimo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtRendMinimo.TextChanged += new System.EventHandler(this.txtRendMinimo_TextChanged);

            this.txt1RMinimo.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.txt1RMinimo.Location  = new System.Drawing.Point(230, 42);
            this.txt1RMinimo.Name      = "txt1RMinimo";
            this.txt1RMinimo.Size      = new System.Drawing.Size(110, 23);
            this.txt1RMinimo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txt1RMinimo.TextChanged += new System.EventHandler(this.txt1RMinimo_TextChanged);

            // ── Fila Medio  (y=80) ────────────────────────────────────────────
            this.rbMedio.AutoSize = true;
            this.rbMedio.Checked  = true;
            this.rbMedio.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.rbMedio.Location = new System.Drawing.Point(15, 82);
            this.rbMedio.Name     = "rbMedio";
            this.rbMedio.Size     = new System.Drawing.Size(65, 19);
            this.rbMedio.TabStop  = true;
            this.rbMedio.Text     = "Medio";

            this.txtRendMedio.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.txtRendMedio.Location  = new System.Drawing.Point(100, 80);
            this.txtRendMedio.Name      = "txtRendMedio";
            this.txtRendMedio.Size      = new System.Drawing.Size(110, 23);
            this.txtRendMedio.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtRendMedio.TextChanged += new System.EventHandler(this.txtRendMedio_TextChanged);

            this.txt1RMedio.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.txt1RMedio.Location  = new System.Drawing.Point(230, 80);
            this.txt1RMedio.Name      = "txt1RMedio";
            this.txt1RMedio.Size      = new System.Drawing.Size(110, 23);
            this.txt1RMedio.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txt1RMedio.TextChanged += new System.EventHandler(this.txt1RMedio_TextChanged);

            // ── Fila Óptimo  (y=118) ─────────────────────────────────────────
            this.rbOptimo.AutoSize = true;
            this.rbOptimo.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.rbOptimo.Location = new System.Drawing.Point(15, 120);
            this.rbOptimo.Name     = "rbOptimo";
            this.rbOptimo.Size     = new System.Drawing.Size(65, 19);
            this.rbOptimo.Text     = "Optimo";

            this.txtRendOptimo.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.txtRendOptimo.Location  = new System.Drawing.Point(100, 118);
            this.txtRendOptimo.Name      = "txtRendOptimo";
            this.txtRendOptimo.Size      = new System.Drawing.Size(110, 23);
            this.txtRendOptimo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtRendOptimo.TextChanged += new System.EventHandler(this.txtRendOptimo_TextChanged);

            this.txt1ROptimo.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.txt1ROptimo.Location  = new System.Drawing.Point(230, 118);
            this.txt1ROptimo.Name      = "txt1ROptimo";
            this.txt1ROptimo.Size      = new System.Drawing.Size(110, 23);
            this.txt1ROptimo.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txt1ROptimo.TextChanged += new System.EventHandler(this.txt1ROptimo_TextChanged);

            // ── Botones  (y=160) ─────────────────────────────────────────────
            this.btnAceptar.BackColor = System.Drawing.Color.FromArgb(33, 150, 243);
            this.btnAceptar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAceptar.Font      = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnAceptar.ForeColor = System.Drawing.Color.White;
            this.btnAceptar.Location  = new System.Drawing.Point(70, 158);
            this.btnAceptar.Name      = "btnAceptar";
            this.btnAceptar.Size      = new System.Drawing.Size(80, 28);
            this.btnAceptar.Text      = "Aceptar";
            this.btnAceptar.Click    += new System.EventHandler(this.btnAceptar_Click);

            this.btnCancelar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancelar.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.btnCancelar.Location  = new System.Drawing.Point(165, 158);
            this.btnCancelar.Name      = "btnCancelar";
            this.btnCancelar.Size      = new System.Drawing.Size(80, 28);
            this.btnCancelar.Text      = "Cancelar";
            this.btnCancelar.Click    += new System.EventHandler(this.btnCancelar_Click);

            // ── Form ──────────────────────────────────────────────────────────
            this.AcceptButton        = this.btnAceptar;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor           = System.Drawing.Color.WhiteSmoke;
            this.CancelButton        = this.btnCancelar;
            this.ClientSize          = new System.Drawing.Size(364, 202);
            this.Font                = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle     = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox         = false;
            this.MinimizeBox         = false;
            this.Name                = "FormRendimiento";
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text                = "Rendimiento";

            this.Controls.Add(this.lblRendH);
            this.Controls.Add(this.lbl1RH);
            this.Controls.Add(this.rbMinimo);
            this.Controls.Add(this.txtRendMinimo);
            this.Controls.Add(this.txt1RMinimo);
            this.Controls.Add(this.rbMedio);
            this.Controls.Add(this.txtRendMedio);
            this.Controls.Add(this.txt1RMedio);
            this.Controls.Add(this.rbOptimo);
            this.Controls.Add(this.txtRendOptimo);
            this.Controls.Add(this.txt1ROptimo);
            this.Controls.Add(this.btnAceptar);
            this.Controls.Add(this.btnCancelar);

            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        private System.Windows.Forms.Label lblRendH, lbl1RH;
        private System.Windows.Forms.RadioButton rbMinimo, rbMedio, rbOptimo;
        private System.Windows.Forms.TextBox txtRendMinimo, txt1RMinimo;
        private System.Windows.Forms.TextBox txtRendMedio,  txt1RMedio;
        private System.Windows.Forms.TextBox txtRendOptimo, txt1ROptimo;
        private System.Windows.Forms.Button btnAceptar, btnCancelar;
    }
}
