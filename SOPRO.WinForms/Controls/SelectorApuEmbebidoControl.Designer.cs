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
            this.tsAcciones = new System.Windows.Forms.ToolStrip();
            this.lblTitulo = new System.Windows.Forms.ToolStripLabel();
            this.tsSepAcciones = new System.Windows.Forms.ToolStripSeparator();
            this.btnNuevaMatriz = new System.Windows.Forms.ToolStripButton();
            this.btnEditar = new System.Windows.Forms.ToolStripButton();
            this.btnVolver = new System.Windows.Forms.ToolStripButton();
            this.btnAsignar = new System.Windows.Forms.ToolStripButton();
            this.pnlHost = new System.Windows.Forms.Panel();
            this.pnlToolbar.SuspendLayout();
            this.tsAcciones.SuspendLayout();
            this.SuspendLayout();
            // pnlToolbar
            this.pnlToolbar.BackColor = System.Drawing.Color.FromArgb(240,244,248);
            this.pnlToolbar.Controls.Add(this.tsAcciones);
            this.pnlToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlToolbar.Location = new System.Drawing.Point(0,0);
            this.pnlToolbar.Name = "pnlToolbar";
            this.pnlToolbar.Size = new System.Drawing.Size(900,42);
            this.pnlToolbar.TabIndex = 0;
            // tsAcciones
            this.tsAcciones.AutoSize = false;
            this.tsAcciones.BackColor = System.Drawing.Color.FromArgb(240,244,248);
            this.tsAcciones.CanOverflow = true;
            this.tsAcciones.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tsAcciones.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.tsAcciones.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.tsAcciones.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.lblTitulo,
                this.tsSepAcciones,
                this.btnNuevaMatriz,
                this.btnEditar,
                this.btnVolver,
                this.btnAsignar});
            this.tsAcciones.Location = new System.Drawing.Point(0,0);
            this.tsAcciones.Name = "tsAcciones";
            this.tsAcciones.Padding = new System.Windows.Forms.Padding(8,4,8,4);
            this.tsAcciones.ShowItemToolTips = true;
            this.tsAcciones.Size = new System.Drawing.Size(900,42);
            this.tsAcciones.TabIndex = 0;
            // lblTitulo
            this.lblTitulo.AccessibleName = "Selector APU";
            this.lblTitulo.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblTitulo.ForeColor = System.Drawing.Color.FromArgb(31,78,121);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.lblTitulo.Text = "Selector APU";
            // tsSepAcciones
            this.tsSepAcciones.Name = "tsSepAcciones";
            // btnNuevaMatriz
            this.btnNuevaMatriz.AccessibleName = "Nueva Matriz";
            this.btnNuevaMatriz.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnNuevaMatriz.Name = "btnNuevaMatriz";
            this.btnNuevaMatriz.Overflow = System.Windows.Forms.ToolStripItemOverflow.Always;
            this.btnNuevaMatriz.Size = new System.Drawing.Size(110,28);
            this.btnNuevaMatriz.Text = "Nueva Matriz";
            this.btnNuevaMatriz.ToolTipText = "Crear una nueva matriz de APU";
            this.btnNuevaMatriz.Click += new System.EventHandler(this.btnNuevaMatriz_Click);
            // btnEditar
            this.btnEditar.AccessibleName = "Editar";
            this.btnEditar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnEditar.Name = "btnEditar";
            this.btnEditar.Overflow = System.Windows.Forms.ToolStripItemOverflow.Always;
            this.btnEditar.Size = new System.Drawing.Size(110,28);
            this.btnEditar.Text = "Editar";
            this.btnEditar.ToolTipText = "Editar la matriz de APU seleccionada";
            this.btnEditar.Click += new System.EventHandler(this.btnEditar_Click);
            // btnVolver
            this.btnVolver.AccessibleName = "Volver";
            this.btnVolver.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnVolver.Name = "btnVolver";
            this.btnVolver.Overflow = System.Windows.Forms.ToolStripItemOverflow.Always;
            this.btnVolver.Size = new System.Drawing.Size(90,28);
            this.btnVolver.Text = "Volver";
            this.btnVolver.ToolTipText = "Volver al presupuesto sin asignar";
            this.btnVolver.Click += new System.EventHandler(this.btnVolver_Click);
            // btnAsignar
            this.btnAsignar.AccessibleName = "Asignar";
            this.btnAsignar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnAsignar.Name = "btnAsignar";
            this.btnAsignar.Overflow = System.Windows.Forms.ToolStripItemOverflow.Never;
            this.btnAsignar.Size = new System.Drawing.Size(105,28);
            this.btnAsignar.Text = "Asignar";
            this.btnAsignar.ToolTipText = "Asignar la matriz seleccionada a la fila del presupuesto";
            this.btnAsignar.Click += new System.EventHandler(this.btnAsignar_Click);
            // pnlHost
            this.pnlHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlHost.Location = new System.Drawing.Point(0,42);
            this.pnlHost.Name = "pnlHost";
            this.pnlHost.Size = new System.Drawing.Size(900,458);
            this.pnlHost.TabIndex = 1;
            // control
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F,15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pnlHost);
            this.Controls.Add(this.pnlToolbar);
            this.Name = "SelectorApuEmbebidoControl";
            this.Size = new System.Drawing.Size(900,500);
            this.pnlToolbar.ResumeLayout(false);
            this.pnlToolbar.PerformLayout();
            this.tsAcciones.ResumeLayout(false);
            this.tsAcciones.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel pnlToolbar;
        private System.Windows.Forms.ToolStrip tsAcciones;
        private System.Windows.Forms.ToolStripLabel lblTitulo;
        private System.Windows.Forms.ToolStripSeparator tsSepAcciones;
        private System.Windows.Forms.ToolStripButton btnNuevaMatriz;
        private System.Windows.Forms.ToolStripButton btnEditar;
        private System.Windows.Forms.ToolStripButton btnVolver;
        private System.Windows.Forms.ToolStripButton btnAsignar;
        private System.Windows.Forms.Panel pnlHost;
    }
}
