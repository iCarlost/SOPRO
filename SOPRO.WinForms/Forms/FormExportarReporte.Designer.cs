namespace SOPRO.WinForms.Forms
{
    partial class FormExportarReporte
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            rdoPresupuesto  = new System.Windows.Forms.RadioButton();
            rdoPU           = new System.Windows.Forms.RadioButton();
            btnExportar     = new System.Windows.Forms.Button();
            btnCancelar     = new System.Windows.Forms.Button();
            grpTipo         = new System.Windows.Forms.GroupBox();
            lblTitulo       = new System.Windows.Forms.Label();

            grpTipo.SuspendLayout();
            SuspendLayout();

            // lblTitulo
            lblTitulo.Text      = "Selecciona el tipo de reporte a exportar:";
            lblTitulo.Location  = new System.Drawing.Point(14, 14);
            lblTitulo.Size      = new System.Drawing.Size(320, 20);
            lblTitulo.AutoSize  = false;
            lblTitulo.Font      = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            lblTitulo.Name      = "lblTitulo";

            // grpTipo
            grpTipo.Text        = "";
            grpTipo.Location    = new System.Drawing.Point(14, 38);
            grpTipo.Size        = new System.Drawing.Size(320, 90);
            grpTipo.Name        = "grpTipo";
            grpTipo.Controls.Add(rdoPresupuesto);
            grpTipo.Controls.Add(rdoPU);

            // rdoPresupuesto
            rdoPresupuesto.Text     = "📋  Presupuesto  —  lista de conceptos con cantidades y precios";
            rdoPresupuesto.Location = new System.Drawing.Point(12, 16);
            rdoPresupuesto.Size     = new System.Drawing.Size(296, 30);
            rdoPresupuesto.Checked  = true;
            rdoPresupuesto.Font     = new System.Drawing.Font("Segoe UI", 9F);
            rdoPresupuesto.Name     = "rdoPresupuesto";

            // rdoPU
            rdoPU.Text      = "📐  Precios Unitarios  —  un APU por concepto";
            rdoPU.Location  = new System.Drawing.Point(12, 52);
            rdoPU.Size      = new System.Drawing.Size(296, 30);
            rdoPU.Font      = new System.Drawing.Font("Segoe UI", 9F);
            rdoPU.Name      = "rdoPU";

            // btnExportar
            btnExportar.Text                  = "📊 Exportar";
            btnExportar.Location              = new System.Drawing.Point(158, 142);
            btnExportar.Size                  = new System.Drawing.Size(90, 30);
            btnExportar.BackColor             = System.Drawing.Color.FromArgb(27, 94, 32);
            btnExportar.ForeColor             = System.Drawing.Color.White;
            btnExportar.Font                  = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            btnExportar.FlatStyle             = System.Windows.Forms.FlatStyle.Flat;
            btnExportar.UseVisualStyleBackColor = false;
            btnExportar.Name                  = "btnExportar";
            btnExportar.Click                += btnExportar_Click;

            // btnCancelar
            btnCancelar.Text        = "Cancelar";
            btnCancelar.Location    = new System.Drawing.Point(256, 142);
            btnCancelar.Size        = new System.Drawing.Size(80, 30);
            btnCancelar.FlatStyle   = System.Windows.Forms.FlatStyle.Flat;
            btnCancelar.Name        = "btnCancelar";
            btnCancelar.Click      += btnCancelar_Click;

            // Form
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize          = new System.Drawing.Size(350, 186);
            Controls.Add(lblTitulo);
            Controls.Add(grpTipo);
            Controls.Add(btnExportar);
            Controls.Add(btnCancelar);
            Font                = new System.Drawing.Font("Segoe UI", 9F);
            FormBorderStyle     = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox         = false;
            MinimizeBox         = false;
            StartPosition       = System.Windows.Forms.FormStartPosition.CenterParent;
            Text                = "Exportar a Excel";
            Name                = "FormExportarReporte";
            BackColor           = System.Drawing.Color.White;

            grpTipo.ResumeLayout(false);
            ResumeLayout(false);
        }

        private System.Windows.Forms.RadioButton rdoPresupuesto;
        private System.Windows.Forms.RadioButton rdoPU;
        private System.Windows.Forms.Button      btnExportar;
        private System.Windows.Forms.Button      btnCancelar;
        private System.Windows.Forms.GroupBox    grpTipo;
        private System.Windows.Forms.Label       lblTitulo;
    }
}
