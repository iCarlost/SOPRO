namespace SOPRO.WinForms.Forms
{
    partial class FormDatosProyecto
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            tabControl            = new System.Windows.Forms.TabControl();
            tabObra               = new System.Windows.Forms.TabPage();
            tabCalculo            = new System.Windows.Forms.TabPage();

            // Tab Obra – inputs
            txtNombre             = new System.Windows.Forms.TextBox();
            txtDescripcion        = new System.Windows.Forms.TextBox();
            txtUbicacion          = new System.Windows.Forms.TextBox();
            txtConvocante         = new System.Windows.Forms.TextBox();
            txtContratista        = new System.Windows.Forms.TextBox();
            txtApoderado          = new System.Windows.Forms.TextBox();
            dtpInicio             = new System.Windows.Forms.DateTimePicker();
            dtpTermino            = new System.Windows.Forms.DateTimePicker();
            nudPlazo              = new System.Windows.Forms.NumericUpDown();

            // Tab Obra – labels
            lblNombre             = new System.Windows.Forms.Label();
            lblDescripcion        = new System.Windows.Forms.Label();
            lblUbicacion          = new System.Windows.Forms.Label();
            lblConvocante         = new System.Windows.Forms.Label();
            lblContratista        = new System.Windows.Forms.Label();
            lblApoderado          = new System.Windows.Forms.Label();
            lblInicio             = new System.Windows.Forms.Label();
            lblTermino            = new System.Windows.Forms.Label();
            lblPlazo              = new System.Windows.Forms.Label();

            // Tab Cálculo – inputs
            nudIVA                = new System.Windows.Forms.NumericUpDown();
            nudDecimalesCantidad  = new System.Windows.Forms.NumericUpDown();
            nudDecimalesImporte   = new System.Windows.Forms.NumericUpDown();
            nudDecimalesPorcentaje= new System.Windows.Forms.NumericUpDown();

            // Tab Cálculo – labels
            lblTituloImpuestos    = new System.Windows.Forms.Label();
            lblIVA                = new System.Windows.Forms.Label();
            lblTituloDecimales    = new System.Windows.Forms.Label();
            lblDecCantidad        = new System.Windows.Forms.Label();
            lblDecImporte         = new System.Windows.Forms.Label();
            lblDecPorcentaje      = new System.Windows.Forms.Label();

            // Botones
            btnGuardar            = new System.Windows.Forms.Button();
            btnCancelar           = new System.Windows.Forms.Button();

            tabControl.SuspendLayout();
            tabObra.SuspendLayout();
            tabCalculo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudPlazo).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudIVA).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudDecimalesCantidad).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudDecimalesImporte).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudDecimalesPorcentaje).BeginInit();
            SuspendLayout();

            // ── tabControl ───────────────────────────────────────────────────
            tabControl.Controls.Add(tabObra);
            tabControl.Controls.Add(tabCalculo);
            tabControl.Font      = new System.Drawing.Font("Segoe UI", 10F);
            tabControl.Location  = new System.Drawing.Point(10, 10);
            tabControl.Name      = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size      = new System.Drawing.Size(715, 490);
            tabControl.TabIndex  = 0;

            // ── tabObra ───────────────────────────────────────────────────────
            tabObra.Controls.Add(lblNombre);
            tabObra.Controls.Add(txtNombre);
            tabObra.Controls.Add(lblDescripcion);
            tabObra.Controls.Add(txtDescripcion);
            tabObra.Controls.Add(lblUbicacion);
            tabObra.Controls.Add(txtUbicacion);
            tabObra.Controls.Add(lblConvocante);
            tabObra.Controls.Add(txtConvocante);
            tabObra.Controls.Add(lblContratista);
            tabObra.Controls.Add(txtContratista);
            tabObra.Controls.Add(lblApoderado);
            tabObra.Controls.Add(txtApoderado);
            tabObra.Controls.Add(lblInicio);
            tabObra.Controls.Add(dtpInicio);
            tabObra.Controls.Add(lblTermino);
            tabObra.Controls.Add(dtpTermino);
            tabObra.Controls.Add(lblPlazo);
            tabObra.Controls.Add(nudPlazo);
            tabObra.Location     = new System.Drawing.Point(4, 28);
            tabObra.Name         = "tabObra";
            tabObra.Padding      = new System.Windows.Forms.Padding(3);
            tabObra.Size         = new System.Drawing.Size(707, 458);
            tabObra.TabIndex     = 0;
            tabObra.Text         = "📋 Obra";
            tabObra.UseVisualStyleBackColor = true;

            // ── Tab Obra: labels y controles ──────────────────────────────────
            // Columna X de labels: 20,  columna X de inputs: 190
            // Filas: Nombre=20, Desc=65 (alto=60), Ubic=140, Conv=185, Contr=230, Apod=275, Ini=320, Ter=365, Plazo=410

            lblNombre.AutoSize    = true;
            lblNombre.Location    = new System.Drawing.Point(20, 23);
            lblNombre.Name        = "lblNombre";
            lblNombre.Text        = "Nombre de la obra:";

            txtNombre.Location    = new System.Drawing.Point(190, 20);
            txtNombre.Name        = "txtNombre";
            txtNombre.Size        = new System.Drawing.Size(480, 25);
            txtNombre.TabIndex    = 0;

            lblDescripcion.AutoSize = true;
            lblDescripcion.Location = new System.Drawing.Point(20, 68);
            lblDescripcion.Name     = "lblDescripcion";
            lblDescripcion.Text     = "Descripción:";

            txtDescripcion.Location = new System.Drawing.Point(190, 65);
            txtDescripcion.Multiline= true;
            txtDescripcion.Name     = "txtDescripcion";
            txtDescripcion.Size     = new System.Drawing.Size(480, 60);
            txtDescripcion.TabIndex = 1;

            lblUbicacion.AutoSize   = true;
            lblUbicacion.Location   = new System.Drawing.Point(20, 143);
            lblUbicacion.Name       = "lblUbicacion";
            lblUbicacion.Text       = "Ubicación:";

            txtUbicacion.Location   = new System.Drawing.Point(190, 140);
            txtUbicacion.Name       = "txtUbicacion";
            txtUbicacion.Size       = new System.Drawing.Size(480, 25);
            txtUbicacion.TabIndex   = 2;

            lblConvocante.AutoSize  = true;
            lblConvocante.Location  = new System.Drawing.Point(20, 188);
            lblConvocante.Name      = "lblConvocante";
            lblConvocante.Text      = "Convocante:";

            txtConvocante.Location  = new System.Drawing.Point(190, 185);
            txtConvocante.Name      = "txtConvocante";
            txtConvocante.Size      = new System.Drawing.Size(480, 25);
            txtConvocante.TabIndex  = 3;

            lblContratista.AutoSize = true;
            lblContratista.Location = new System.Drawing.Point(20, 233);
            lblContratista.Name     = "lblContratista";
            lblContratista.Text     = "Contratista:";

            txtContratista.Location = new System.Drawing.Point(190, 230);
            txtContratista.Name     = "txtContratista";
            txtContratista.Size     = new System.Drawing.Size(480, 25);
            txtContratista.TabIndex = 4;

            lblApoderado.AutoSize   = true;
            lblApoderado.Location   = new System.Drawing.Point(20, 278);
            lblApoderado.Name       = "lblApoderado";
            lblApoderado.Text       = "Apoderado legal:";

            txtApoderado.Location   = new System.Drawing.Point(190, 275);
            txtApoderado.Name       = "txtApoderado";
            txtApoderado.Size       = new System.Drawing.Size(480, 25);
            txtApoderado.TabIndex   = 5;

            lblInicio.AutoSize      = true;
            lblInicio.Location      = new System.Drawing.Point(20, 323);
            lblInicio.Name          = "lblInicio";
            lblInicio.Text          = "Fecha de inicio:";

            dtpInicio.Format        = System.Windows.Forms.DateTimePickerFormat.Short;
            dtpInicio.Location      = new System.Drawing.Point(190, 320);
            dtpInicio.Name          = "dtpInicio";
            dtpInicio.Size          = new System.Drawing.Size(200, 25);
            dtpInicio.TabIndex      = 6;

            lblTermino.AutoSize     = true;
            lblTermino.Location     = new System.Drawing.Point(20, 368);
            lblTermino.Name         = "lblTermino";
            lblTermino.Text         = "Fecha de término:";

            dtpTermino.Format       = System.Windows.Forms.DateTimePickerFormat.Short;
            dtpTermino.Location     = new System.Drawing.Point(190, 365);
            dtpTermino.Name         = "dtpTermino";
            dtpTermino.Size         = new System.Drawing.Size(200, 25);
            dtpTermino.TabIndex     = 7;

            lblPlazo.AutoSize       = true;
            lblPlazo.Location       = new System.Drawing.Point(20, 413);
            lblPlazo.Name           = "lblPlazo";
            lblPlazo.Text           = "Plazo (días):";

            nudPlazo.Location       = new System.Drawing.Point(190, 410);
            nudPlazo.Maximum        = new decimal(new int[] { 9999, 0, 0, 0 });
            nudPlazo.Name           = "nudPlazo";
            nudPlazo.Size           = new System.Drawing.Size(120, 25);
            nudPlazo.TabIndex       = 8;

            // ── tabCalculo ────────────────────────────────────────────────────
            tabCalculo.Controls.Add(lblTituloImpuestos);
            tabCalculo.Controls.Add(lblIVA);
            tabCalculo.Controls.Add(nudIVA);
            tabCalculo.Controls.Add(lblTituloDecimales);
            tabCalculo.Controls.Add(lblDecCantidad);
            tabCalculo.Controls.Add(nudDecimalesCantidad);
            tabCalculo.Controls.Add(lblDecImporte);
            tabCalculo.Controls.Add(nudDecimalesImporte);
            tabCalculo.Controls.Add(lblDecPorcentaje);
            tabCalculo.Controls.Add(nudDecimalesPorcentaje);
            tabCalculo.Location     = new System.Drawing.Point(4, 28);
            tabCalculo.Name         = "tabCalculo";
            tabCalculo.Padding      = new System.Windows.Forms.Padding(3);
            tabCalculo.Size         = new System.Drawing.Size(707, 458);
            tabCalculo.TabIndex     = 1;
            tabCalculo.Text         = "⚙ Cálculo";
            tabCalculo.UseVisualStyleBackColor = true;

            // ── Tab Cálculo: labels y controles ───────────────────────────────
            lblTituloImpuestos.AutoSize  = true;
            lblTituloImpuestos.Font      = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            lblTituloImpuestos.Location  = new System.Drawing.Point(20, 30);
            lblTituloImpuestos.Name      = "lblTituloImpuestos";
            lblTituloImpuestos.Text      = "Impuestos";

            lblIVA.AutoSize              = true;
            lblIVA.Location              = new System.Drawing.Point(40, 73);
            lblIVA.Name                  = "lblIVA";
            lblIVA.Text                  = "IVA (%):";

            nudIVA.DecimalPlaces         = 2;
            nudIVA.Location              = new System.Drawing.Point(190, 70);
            nudIVA.Maximum               = new decimal(new int[] { 100, 0, 0, 0 });
            nudIVA.Name                  = "nudIVA";
            nudIVA.Size                  = new System.Drawing.Size(100, 25);
            nudIVA.TabIndex              = 0;
            nudIVA.Value                 = new decimal(new int[] { 16, 0, 0, 0 });

            lblTituloDecimales.AutoSize  = true;
            lblTituloDecimales.Font      = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            lblTituloDecimales.Location  = new System.Drawing.Point(20, 130);
            lblTituloDecimales.Name      = "lblTituloDecimales";
            lblTituloDecimales.Text      = "Número de decimales";

            lblDecCantidad.AutoSize      = true;
            lblDecCantidad.Location      = new System.Drawing.Point(40, 183);
            lblDecCantidad.Name          = "lblDecCantidad";
            lblDecCantidad.Text          = "En cantidades:";

            nudDecimalesCantidad.Location= new System.Drawing.Point(190, 180);
            nudDecimalesCantidad.Maximum = new decimal(new int[] { 6, 0, 0, 0 });
            nudDecimalesCantidad.Name    = "nudDecimalesCantidad";
            nudDecimalesCantidad.Size    = new System.Drawing.Size(80, 25);
            nudDecimalesCantidad.TabIndex= 1;
            nudDecimalesCantidad.Value   = new decimal(new int[] { 2, 0, 0, 0 });

            lblDecImporte.AutoSize       = true;
            lblDecImporte.Location       = new System.Drawing.Point(40, 228);
            lblDecImporte.Name           = "lblDecImporte";
            lblDecImporte.Text           = "En importes:";

            nudDecimalesImporte.Location = new System.Drawing.Point(190, 225);
            nudDecimalesImporte.Maximum  = new decimal(new int[] { 6, 0, 0, 0 });
            nudDecimalesImporte.Name     = "nudDecimalesImporte";
            nudDecimalesImporte.Size     = new System.Drawing.Size(80, 25);
            nudDecimalesImporte.TabIndex = 2;
            nudDecimalesImporte.Value    = new decimal(new int[] { 2, 0, 0, 0 });

            lblDecPorcentaje.AutoSize    = true;
            lblDecPorcentaje.Location    = new System.Drawing.Point(40, 273);
            lblDecPorcentaje.Name        = "lblDecPorcentaje";
            lblDecPorcentaje.Text        = "En porcentajes:";

            nudDecimalesPorcentaje.Location = new System.Drawing.Point(190, 270);
            nudDecimalesPorcentaje.Maximum  = new decimal(new int[] { 6, 0, 0, 0 });
            nudDecimalesPorcentaje.Name     = "nudDecimalesPorcentaje";
            nudDecimalesPorcentaje.Size     = new System.Drawing.Size(80, 25);
            nudDecimalesPorcentaje.TabIndex = 3;
            nudDecimalesPorcentaje.Value    = new decimal(new int[] { 4, 0, 0, 0 });

            // ── Botones ───────────────────────────────────────────────────────
            btnGuardar.BackColor         = System.Drawing.Color.FromArgb(76, 175, 80);
            btnGuardar.FlatAppearance.BorderSize = 0;
            btnGuardar.FlatStyle         = System.Windows.Forms.FlatStyle.Flat;
            btnGuardar.Font              = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            btnGuardar.ForeColor         = System.Drawing.Color.White;
            btnGuardar.Location          = new System.Drawing.Point(520, 515);
            btnGuardar.Name              = "btnGuardar";
            btnGuardar.Size              = new System.Drawing.Size(100, 35);
            btnGuardar.TabIndex          = 1;
            btnGuardar.Text              = "💾 Guardar";
            btnGuardar.UseVisualStyleBackColor = false;
            btnGuardar.Click            += new System.EventHandler(this.BtnGuardar_Click);

            btnCancelar.FlatStyle        = System.Windows.Forms.FlatStyle.Flat;
            btnCancelar.Location         = new System.Drawing.Point(630, 515);
            btnCancelar.Name             = "btnCancelar";
            btnCancelar.Size             = new System.Drawing.Size(90, 35);
            btnCancelar.TabIndex         = 2;
            btnCancelar.Text             = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = true;
            btnCancelar.Click           += new System.EventHandler(this.btnCancelar_Click);

            // ── Form ──────────────────────────────────────────────────────────
            AutoScaleDimensions          = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode                = System.Windows.Forms.AutoScaleMode.Font;
            BackColor                    = System.Drawing.Color.White;
            ClientSize                   = new System.Drawing.Size(734, 561);
            Controls.Add(btnCancelar);
            Controls.Add(btnGuardar);
            Controls.Add(tabControl);
            Font                         = new System.Drawing.Font("Segoe UI", 9F);
            FormBorderStyle              = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox                  = false;
            MinimizeBox                  = false;
            Name                         = "FormDatosProyecto";
            StartPosition                = System.Windows.Forms.FormStartPosition.CenterParent;
            Text                         = "Datos del Proyecto";

            tabControl.ResumeLayout(false);
            tabObra.ResumeLayout(false);
            tabObra.PerformLayout();
            tabCalculo.ResumeLayout(false);
            tabCalculo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudPlazo).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudIVA).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudDecimalesCantidad).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudDecimalesImporte).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudDecimalesPorcentaje).EndInit();
            ResumeLayout(false);
        }

        #endregion

        // ── Controles ─────────────────────────────────────────────────────────
        private System.Windows.Forms.TabControl    tabControl;
        private System.Windows.Forms.TabPage       tabObra;
        private System.Windows.Forms.TabPage       tabCalculo;
        private System.Windows.Forms.Button        btnGuardar;
        private System.Windows.Forms.Button        btnCancelar;

        // Tab Obra – inputs
        private System.Windows.Forms.TextBox       txtNombre;
        private System.Windows.Forms.TextBox       txtDescripcion;
        private System.Windows.Forms.TextBox       txtUbicacion;
        private System.Windows.Forms.TextBox       txtConvocante;
        private System.Windows.Forms.TextBox       txtContratista;
        private System.Windows.Forms.TextBox       txtApoderado;
        private System.Windows.Forms.DateTimePicker dtpInicio;
        private System.Windows.Forms.DateTimePicker dtpTermino;
        private System.Windows.Forms.NumericUpDown nudPlazo;

        // Tab Obra – labels
        private System.Windows.Forms.Label         lblNombre;
        private System.Windows.Forms.Label         lblDescripcion;
        private System.Windows.Forms.Label         lblUbicacion;
        private System.Windows.Forms.Label         lblConvocante;
        private System.Windows.Forms.Label         lblContratista;
        private System.Windows.Forms.Label         lblApoderado;
        private System.Windows.Forms.Label         lblInicio;
        private System.Windows.Forms.Label         lblTermino;
        private System.Windows.Forms.Label         lblPlazo;

        // Tab Cálculo – inputs
        private System.Windows.Forms.NumericUpDown nudIVA;
        private System.Windows.Forms.NumericUpDown nudDecimalesCantidad;
        private System.Windows.Forms.NumericUpDown nudDecimalesImporte;
        private System.Windows.Forms.NumericUpDown nudDecimalesPorcentaje;

        // Tab Cálculo – labels
        private System.Windows.Forms.Label         lblTituloImpuestos;
        private System.Windows.Forms.Label         lblIVA;
        private System.Windows.Forms.Label         lblTituloDecimales;
        private System.Windows.Forms.Label         lblDecCantidad;
        private System.Windows.Forms.Label         lblDecImporte;
        private System.Windows.Forms.Label         lblDecPorcentaje;
    }
}
