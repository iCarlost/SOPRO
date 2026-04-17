namespace SOPRO.WinForms.Controls
{
    partial class SelectorApuEmbebidoControl
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            try { _innerForm?.Dispose(); } catch { }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlToolbar = new System.Windows.Forms.Panel();
            this.btnVolver = new System.Windows.Forms.Button();
            this.btnAsignar = new System.Windows.Forms.Button();
            this.btnEditar = new System.Windows.Forms.Button();
            this.btnNuevaMatriz = new System.Windows.Forms.Button();
            this.lblTitulo = new System.Windows.Forms.Label();
            this.pnlHost = new System.Windows.Forms.Panel();
            this.pnlToolbar.SuspendLayout();
            this.SuspendLayout();
            // pnlToolbar
            this.pnlToolbar.BackColor = System.Drawing.Color.FromArgb(245,248,252);
            this.pnlToolbar.Controls.Add(this.btnVolver);
            this.pnlToolbar.Controls.Add(this.btnAsignar);
            this.pnlToolbar.Controls.Add(this.btnEditar);
            this.pnlToolbar.Controls.Add(this.btnNuevaMatriz);
            this.pnlToolbar.Controls.Add(this.lblTitulo);
            this.pnlToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlToolbar.Location = new System.Drawing.Point(0,0);
            this.pnlToolbar.Name = "pnlToolbar";
            this.pnlToolbar.Size = new System.Drawing.Size(900,42);
            // lblTitulo
            this.lblTitulo.AutoSize = true;
            this.lblTitulo.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblTitulo.ForeColor = System.Drawing.Color.FromArgb(31,78,121);
            this.lblTitulo.Location = new System.Drawing.Point(10,13);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.Size = new System.Drawing.Size(84,15);
            this.lblTitulo.Text = "Selector APU";
            // buttons
            System.Drawing.Size btnSize = new System.Drawing.Size(110,28);
            this.btnNuevaMatriz.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnNuevaMatriz.Location = new System.Drawing.Point(456,7);
            this.btnNuevaMatriz.Name = "btnNuevaMatriz";
            this.btnNuevaMatriz.Size = btnSize;
            this.btnNuevaMatriz.Text = "Nueva Matriz";
            this.btnNuevaMatriz.UseVisualStyleBackColor = true;
            this.btnNuevaMatriz.Click += new System.EventHandler(this.btnNuevaMatriz_Click);
            this.btnEditar.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnEditar.Location = new System.Drawing.Point(572,7);
            this.btnEditar.Name = "btnEditar";
            this.btnEditar.Size = btnSize;
            this.btnEditar.Text = "Editar";
            this.btnEditar.UseVisualStyleBackColor = true;
            this.btnEditar.Click += new System.EventHandler(this.btnEditar_Click);
            this.btnVolver.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnVolver.Location = new System.Drawing.Point(688,7);
            this.btnVolver.Name = "btnVolver";
            this.btnVolver.Size = new System.Drawing.Size(90,28);
            this.btnVolver.Text = "Volver";
            this.btnVolver.UseVisualStyleBackColor = true;
            this.btnVolver.Click += new System.EventHandler(this.btnVolver_Click);
            this.btnAsignar.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnAsignar.Location = new System.Drawing.Point(784,7);
            this.btnAsignar.Name = "btnAsignar";
            this.btnAsignar.Size = new System.Drawing.Size(105,28);
            this.btnAsignar.Text = "Asignar";
            this.btnAsignar.UseVisualStyleBackColor = true;
            this.btnAsignar.Click += new System.EventHandler(this.btnAsignar_Click);
            // pnlHost
            this.pnlHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlHost.Location = new System.Drawing.Point(0,42);
            this.pnlHost.Name = "pnlHost";
            this.pnlHost.Size = new System.Drawing.Size(900,458);
            // control
            this.Controls.Add(this.pnlHost);
            this.Controls.Add(this.pnlToolbar);
            this.Name = "SelectorApuEmbebidoControl";
            this.Size = new System.Drawing.Size(900,500);
            this.pnlToolbar.ResumeLayout(false);
            this.pnlToolbar.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel pnlToolbar;
        private System.Windows.Forms.Button btnVolver;
        private System.Windows.Forms.Button btnAsignar;
        private System.Windows.Forms.Button btnEditar;
        private System.Windows.Forms.Button btnNuevaMatriz;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.Panel pnlHost;
    }
}
