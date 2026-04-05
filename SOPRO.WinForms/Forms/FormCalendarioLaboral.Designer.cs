namespace SOPRO.WinForms.Forms
{
    partial class FormCalendarioLaboral
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
            this.lblNombre = new System.Windows.Forms.Label();
            this.txtNombre = new System.Windows.Forms.TextBox();
            this.grpDias = new System.Windows.Forms.GroupBox();
            this.chkDomingo = new System.Windows.Forms.CheckBox();
            this.chkSabado = new System.Windows.Forms.CheckBox();
            this.chkViernes = new System.Windows.Forms.CheckBox();
            this.chkJueves = new System.Windows.Forms.CheckBox();
            this.chkMiercoles = new System.Windows.Forms.CheckBox();
            this.chkMartes = new System.Windows.Forms.CheckBox();
            this.chkLunes = new System.Windows.Forms.CheckBox();
            this.lblHoraInicio = new System.Windows.Forms.Label();
            this.lblHoraFin = new System.Windows.Forms.Label();
            this.dtpHoraInicio = new System.Windows.Forms.DateTimePicker();
            this.dtpHoraFin = new System.Windows.Forms.DateTimePicker();
            this.dgvExcepciones = new System.Windows.Forms.DataGridView();
            this.colFechaExcepcion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTipoExcepcion = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colDescripcionExcepcion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnAgregarExcepcion = new System.Windows.Forms.Button();
            this.btnEliminarExcepcion = new System.Windows.Forms.Button();
            this.btnGuardar = new System.Windows.Forms.Button();
            this.btnCancelar = new System.Windows.Forms.Button();
            this.lblExcepciones = new System.Windows.Forms.Label();
            this.grpDias.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvExcepciones)).BeginInit();
            this.SuspendLayout();
            // 
            // lblNombre
            // 
            this.lblNombre.AutoSize = true;
            this.lblNombre.Location = new System.Drawing.Point(18, 18);
            this.lblNombre.Name = "lblNombre";
            this.lblNombre.Size = new System.Drawing.Size(56, 15);
            this.lblNombre.TabIndex = 0;
            this.lblNombre.Text = "Nombre:";
            // 
            // txtNombre
            // 
            this.txtNombre.Location = new System.Drawing.Point(92, 15);
            this.txtNombre.Name = "txtNombre";
            this.txtNombre.Size = new System.Drawing.Size(290, 23);
            this.txtNombre.TabIndex = 1;
            // 
            // grpDias
            // 
            this.grpDias.Controls.Add(this.chkDomingo);
            this.grpDias.Controls.Add(this.chkSabado);
            this.grpDias.Controls.Add(this.chkViernes);
            this.grpDias.Controls.Add(this.chkJueves);
            this.grpDias.Controls.Add(this.chkMiercoles);
            this.grpDias.Controls.Add(this.chkMartes);
            this.grpDias.Controls.Add(this.chkLunes);
            this.grpDias.Location = new System.Drawing.Point(18, 52);
            this.grpDias.Name = "grpDias";
            this.grpDias.Size = new System.Drawing.Size(364, 92);
            this.grpDias.TabIndex = 2;
            this.grpDias.TabStop = false;
            this.grpDias.Text = "Días laborables";
            // 
            // chkDomingo
            // 
            this.chkDomingo.AutoSize = true;
            this.chkDomingo.Location = new System.Drawing.Point(246, 54);
            this.chkDomingo.Name = "chkDomingo";
            this.chkDomingo.Size = new System.Drawing.Size(77, 19);
            this.chkDomingo.TabIndex = 6;
            this.chkDomingo.Text = "Domingo";
            this.chkDomingo.UseVisualStyleBackColor = true;
            // 
            // chkSabado
            // 
            this.chkSabado.AutoSize = true;
            this.chkSabado.Location = new System.Drawing.Point(132, 54);
            this.chkSabado.Name = "chkSabado";
            this.chkSabado.Size = new System.Drawing.Size(67, 19);
            this.chkSabado.TabIndex = 5;
            this.chkSabado.Text = "Sábado";
            this.chkSabado.UseVisualStyleBackColor = true;
            // 
            // chkViernes
            // 
            this.chkViernes.AutoSize = true;
            this.chkViernes.Location = new System.Drawing.Point(17, 54);
            this.chkViernes.Name = "chkViernes";
            this.chkViernes.Size = new System.Drawing.Size(66, 19);
            this.chkViernes.TabIndex = 4;
            this.chkViernes.Text = "Viernes";
            this.chkViernes.UseVisualStyleBackColor = true;
            // 
            // chkJueves
            // 
            this.chkJueves.AutoSize = true;
            this.chkJueves.Location = new System.Drawing.Point(246, 24);
            this.chkJueves.Name = "chkJueves";
            this.chkJueves.Size = new System.Drawing.Size(61, 19);
            this.chkJueves.TabIndex = 3;
            this.chkJueves.Text = "Jueves";
            this.chkJueves.UseVisualStyleBackColor = true;
            // 
            // chkMiercoles
            // 
            this.chkMiercoles.AutoSize = true;
            this.chkMiercoles.Location = new System.Drawing.Point(132, 24);
            this.chkMiercoles.Name = "chkMiercoles";
            this.chkMiercoles.Size = new System.Drawing.Size(82, 19);
            this.chkMiercoles.TabIndex = 2;
            this.chkMiercoles.Text = "Miércoles";
            this.chkMiercoles.UseVisualStyleBackColor = true;
            // 
            // chkMartes
            // 
            this.chkMartes.AutoSize = true;
            this.chkMartes.Location = new System.Drawing.Point(17, 24);
            this.chkMartes.Name = "chkMartes";
            this.chkMartes.Size = new System.Drawing.Size(63, 19);
            this.chkMartes.TabIndex = 1;
            this.chkMartes.Text = "Martes";
            this.chkMartes.UseVisualStyleBackColor = true;
            // 
            // chkLunes
            // 
            this.chkLunes.AutoSize = true;
            this.chkLunes.Location = new System.Drawing.Point(17, 24);
            this.chkLunes.Name = "chkLunes";
            this.chkLunes.Size = new System.Drawing.Size(58, 19);
            this.chkLunes.TabIndex = 0;
            this.chkLunes.Text = "Lunes";
            this.chkLunes.UseVisualStyleBackColor = true;
            // 
            // lblHoraInicio
            // 
            this.lblHoraInicio.AutoSize = true;
            this.lblHoraInicio.Location = new System.Drawing.Point(408, 18);
            this.lblHoraInicio.Name = "lblHoraInicio";
            this.lblHoraInicio.Size = new System.Drawing.Size(69, 15);
            this.lblHoraInicio.TabIndex = 3;
            this.lblHoraInicio.Text = "Hora inicio:";
            // 
            // lblHoraFin
            // 
            this.lblHoraFin.AutoSize = true;
            this.lblHoraFin.Location = new System.Drawing.Point(408, 52);
            this.lblHoraFin.Name = "lblHoraFin";
            this.lblHoraFin.Size = new System.Drawing.Size(55, 15);
            this.lblHoraFin.TabIndex = 4;
            this.lblHoraFin.Text = "Hora fin:";
            // 
            // dtpHoraInicio
            // 
            this.dtpHoraInicio.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            this.dtpHoraInicio.Location = new System.Drawing.Point(496, 15);
            this.dtpHoraInicio.Name = "dtpHoraInicio";
            this.dtpHoraInicio.ShowUpDown = true;
            this.dtpHoraInicio.Size = new System.Drawing.Size(100, 23);
            this.dtpHoraInicio.TabIndex = 5;
            // 
            // dtpHoraFin
            // 
            this.dtpHoraFin.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            this.dtpHoraFin.Location = new System.Drawing.Point(496, 49);
            this.dtpHoraFin.Name = "dtpHoraFin";
            this.dtpHoraFin.ShowUpDown = true;
            this.dtpHoraFin.Size = new System.Drawing.Size(100, 23);
            this.dtpHoraFin.TabIndex = 6;
            // 
            // dgvExcepciones
            // 
            this.dgvExcepciones.AllowUserToAddRows = false;
            this.dgvExcepciones.AllowUserToDeleteRows = false;
            this.dgvExcepciones.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvExcepciones.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvExcepciones.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colFechaExcepcion,
            this.colTipoExcepcion,
            this.colDescripcionExcepcion});
            this.dgvExcepciones.Location = new System.Drawing.Point(18, 182);
            this.dgvExcepciones.MultiSelect = false;
            this.dgvExcepciones.Name = "dgvExcepciones";
            this.dgvExcepciones.RowHeadersVisible = false;
            this.dgvExcepciones.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvExcepciones.Size = new System.Drawing.Size(716, 216);
            this.dgvExcepciones.TabIndex = 7;
            // 
            // colFechaExcepcion
            // 
            this.colFechaExcepcion.DataPropertyName = "Fecha";
            this.colFechaExcepcion.HeaderText = "Fecha";
            this.colFechaExcepcion.Name = "colFechaExcepcion";
            this.colFechaExcepcion.Width = 110;
            // 
            // colTipoExcepcion
            // 
            this.colTipoExcepcion.DataPropertyName = "Tipo";
            this.colTipoExcepcion.HeaderText = "Tipo";
            this.colTipoExcepcion.Name = "colTipoExcepcion";
            this.colTipoExcepcion.Width = 160;
            // 
            // colDescripcionExcepcion
            // 
            this.colDescripcionExcepcion.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colDescripcionExcepcion.DataPropertyName = "Descripcion";
            this.colDescripcionExcepcion.HeaderText = "Descripción";
            this.colDescripcionExcepcion.Name = "colDescripcionExcepcion";
            // 
            // btnAgregarExcepcion
            // 
            this.btnAgregarExcepcion.Location = new System.Drawing.Point(18, 150);
            this.btnAgregarExcepcion.Name = "btnAgregarExcepcion";
            this.btnAgregarExcepcion.Size = new System.Drawing.Size(116, 26);
            this.btnAgregarExcepcion.TabIndex = 8;
            this.btnAgregarExcepcion.Text = "+ Agregar día";
            this.btnAgregarExcepcion.UseVisualStyleBackColor = true;
            this.btnAgregarExcepcion.Click += new System.EventHandler(this.btnAgregarExcepcion_Click);
            // 
            // btnEliminarExcepcion
            // 
            this.btnEliminarExcepcion.Location = new System.Drawing.Point(140, 150);
            this.btnEliminarExcepcion.Name = "btnEliminarExcepcion";
            this.btnEliminarExcepcion.Size = new System.Drawing.Size(117, 26);
            this.btnEliminarExcepcion.TabIndex = 9;
            this.btnEliminarExcepcion.Text = "- Eliminar día";
            this.btnEliminarExcepcion.UseVisualStyleBackColor = true;
            this.btnEliminarExcepcion.Click += new System.EventHandler(this.btnEliminarExcepcion_Click);
            // 
            // btnGuardar
            // 
            this.btnGuardar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnGuardar.Location = new System.Drawing.Point(540, 414);
            this.btnGuardar.Name = "btnGuardar";
            this.btnGuardar.Size = new System.Drawing.Size(94, 30);
            this.btnGuardar.TabIndex = 10;
            this.btnGuardar.Text = "Guardar";
            this.btnGuardar.UseVisualStyleBackColor = true;
            this.btnGuardar.Click += new System.EventHandler(this.btnGuardar_Click);
            // 
            // btnCancelar
            // 
            this.btnCancelar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelar.Location = new System.Drawing.Point(640, 414);
            this.btnCancelar.Name = "btnCancelar";
            this.btnCancelar.Size = new System.Drawing.Size(94, 30);
            this.btnCancelar.TabIndex = 11;
            this.btnCancelar.Text = "Cancelar";
            this.btnCancelar.UseVisualStyleBackColor = true;
            this.btnCancelar.Click += new System.EventHandler(this.btnCancelar_Click);
            // 
            // lblExcepciones
            // 
            this.lblExcepciones.AutoSize = true;
            this.lblExcepciones.Location = new System.Drawing.Point(18, 164);
            this.lblExcepciones.Name = "lblExcepciones";
            this.lblExcepciones.Size = new System.Drawing.Size(73, 15);
            this.lblExcepciones.TabIndex = 12;
            this.lblExcepciones.Text = "Excepciones";
            this.lblExcepciones.Visible = false;
            // 
            // FormCalendarioLaboral
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(751, 456);
            this.Controls.Add(this.lblExcepciones);
            this.Controls.Add(this.btnCancelar);
            this.Controls.Add(this.btnGuardar);
            this.Controls.Add(this.btnEliminarExcepcion);
            this.Controls.Add(this.btnAgregarExcepcion);
            this.Controls.Add(this.dgvExcepciones);
            this.Controls.Add(this.dtpHoraFin);
            this.Controls.Add(this.dtpHoraInicio);
            this.Controls.Add(this.lblHoraFin);
            this.Controls.Add(this.lblHoraInicio);
            this.Controls.Add(this.grpDias);
            this.Controls.Add(this.txtNombre);
            this.Controls.Add(this.lblNombre);
            this.MinimumSize = new System.Drawing.Size(767, 495);
            this.Name = "FormCalendarioLaboral";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Calendario laboral";
            this.Load += new System.EventHandler(this.FormCalendarioLaboral_Load);
            this.grpDias.ResumeLayout(false);
            this.grpDias.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvExcepciones)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblNombre;
        private System.Windows.Forms.TextBox txtNombre;
        private System.Windows.Forms.GroupBox grpDias;
        private System.Windows.Forms.CheckBox chkDomingo;
        private System.Windows.Forms.CheckBox chkSabado;
        private System.Windows.Forms.CheckBox chkViernes;
        private System.Windows.Forms.CheckBox chkJueves;
        private System.Windows.Forms.CheckBox chkMiercoles;
        private System.Windows.Forms.CheckBox chkMartes;
        private System.Windows.Forms.CheckBox chkLunes;
        private System.Windows.Forms.Label lblHoraInicio;
        private System.Windows.Forms.Label lblHoraFin;
        private System.Windows.Forms.DateTimePicker dtpHoraInicio;
        private System.Windows.Forms.DateTimePicker dtpHoraFin;
        private System.Windows.Forms.DataGridView dgvExcepciones;
        private System.Windows.Forms.Button btnAgregarExcepcion;
        private System.Windows.Forms.Button btnEliminarExcepcion;
        private System.Windows.Forms.Button btnGuardar;
        private System.Windows.Forms.Button btnCancelar;
        private System.Windows.Forms.Label lblExcepciones;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFechaExcepcion;
        private System.Windows.Forms.DataGridViewComboBoxColumn colTipoExcepcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDescripcionExcepcion;
    }
}
