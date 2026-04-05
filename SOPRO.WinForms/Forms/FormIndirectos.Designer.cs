using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    partial class FormIndirectos
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.Label lblProyecto;
        private System.Windows.Forms.ToolStrip panelToolbar;
        private System.Windows.Forms.ToolStripButton btnCrearPredeterminados;
        private System.Windows.Forms.ToolStripButton btnCalcular;
        private System.Windows.Forms.ToolStripButton btnTransferir;
        private System.Windows.Forms.ToolStripButton btnCerrar;
        private System.Windows.Forms.ToolStripButton btnAgregarGrupo;
        private System.Windows.Forms.ToolStripButton btnAgregarConcepto;
        private System.Windows.Forms.ToolStripButton btnEliminarFila;
        private System.Windows.Forms.Button btnAutoCD;
        private System.Windows.Forms.ToolStripButton btnPorcentajesDirectos;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabOficinaCentral;
        private System.Windows.Forms.TabPage tabCampo;
        private System.Windows.Forms.TabPage tabConfiguracion;
        private System.Windows.Forms.DataGridView dgvOficinaCentral;
        private System.Windows.Forms.DataGridView dgvCampo;
        private System.Windows.Forms.Panel panelConfiguracion;
        private System.Windows.Forms.Label lblVolumenAnual;
        private System.Windows.Forms.TextBox txtVolumenAnual;
        private System.Windows.Forms.Label lblCostoDirecto;
        private System.Windows.Forms.TextBox txtCostoDirecto;
        private System.Windows.Forms.GroupBox gbResumen;
        private System.Windows.Forms.Label lblTotalOficinaCentral;
        private System.Windows.Forms.Label lblTotalCampo;
        private System.Windows.Forms.Label lblPorcentajeOC;
        private System.Windows.Forms.Label lblPorcentajeCampo;
        private System.Windows.Forms.Label lblPorcentajeTotal;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;

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
            panelTop = new Panel();
            lblProyecto = new Label();
            lblTitulo = new Label();
            panelToolbar = new ToolStrip();
            btnCrearPredeterminados = new ToolStripButton();
            btnCalcular = new ToolStripButton();
            btnTransferir = new ToolStripButton();
            btnEliminarFila = new ToolStripButton();
            btnAgregarGrupo = new ToolStripButton();
            btnAgregarConcepto = new ToolStripButton();
            btnPorcentajesDirectos = new ToolStripButton();
            btnCerrar = new ToolStripButton();
            btnAutoCD = new Button();
            tabControl = new TabControl();
            tabOficinaCentral = new TabPage();
            dgvOficinaCentral = new DataGridView();
            tabCampo = new TabPage();
            dgvCampo = new DataGridView();
            tabConfiguracion = new TabPage();
            gbResumen = new GroupBox();
            lblPorcentajeTotal = new Label();
            lblPorcentajeCampo = new Label();
            lblPorcentajeOC = new Label();
            lblTotalCampo = new Label();
            lblTotalOficinaCentral = new Label();
            panelConfiguracion = new Panel();
            txtCostoDirecto = new TextBox();
            lblCostoDirecto = new Label();
            txtVolumenAnual = new TextBox();
            lblVolumenAnual = new Label();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            panelTop.SuspendLayout();
            panelToolbar.SuspendLayout();
            tabControl.SuspendLayout();
            tabOficinaCentral.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvOficinaCentral).BeginInit();
            tabCampo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvCampo).BeginInit();
            tabConfiguracion.SuspendLayout();
            gbResumen.SuspendLayout();
            panelConfiguracion.SuspendLayout();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(51, 51, 76);
            panelTop.Controls.Add(lblProyecto);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(1400, 68);
            panelTop.TabIndex = 0;
            // 
            // lblProyecto
            // 
            lblProyecto.AutoSize = true;
            lblProyecto.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblProyecto.ForeColor = Color.White;
            lblProyecto.Location = new Point(18, 42);
            lblProyecto.Name = "lblProyecto";
            lblProyecto.Size = new Size(54, 15);
            lblProyecto.TabIndex = 1;
            lblProyecto.Text = "Proyecto";
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(12, 9);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(345, 32);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "📊 CÁLCULO DE INDIRECTOS";
            // 
            // panelToolbar
            // 
            panelToolbar.BackColor = Color.FromArgb(240, 240, 240);
            panelToolbar.GripStyle = ToolStripGripStyle.Hidden;
            panelToolbar.Items.AddRange(new ToolStripItem[] { btnCrearPredeterminados, btnCalcular, btnTransferir, btnEliminarFila, btnAgregarGrupo, btnAgregarConcepto, btnPorcentajesDirectos, btnCerrar });
            panelToolbar.Location = new Point(0, 68);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Padding = new Padding(8, 4, 8, 4);
            panelToolbar.Size = new Size(1400, 30);
            panelToolbar.TabIndex = 1;
            // 
            // btnCrearPredeterminados
            // 
            btnCrearPredeterminados.BackColor = SystemColors.Control;
            btnCrearPredeterminados.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCrearPredeterminados.ForeColor = Color.Black;
            btnCrearPredeterminados.Name = "btnCrearPredeterminados";
            btnCrearPredeterminados.Size = new Size(156, 19);
            btnCrearPredeterminados.Text = "📋 Crear Predeterminados";
            btnCrearPredeterminados.Click += btnCrearPredeterminados_Click;
            // 
            // btnCalcular
            // 
            btnCalcular.BackColor = SystemColors.Control;
            btnCalcular.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCalcular.ForeColor = Color.Black;
            btnCalcular.Name = "btnCalcular";
            btnCalcular.Size = new Size(138, 19);
            btnCalcular.Text = "\U0001f9ee Calcular Porcentajes";
            btnCalcular.Click += btnCalcular_Click;
            // 
            // btnTransferir
            // 
            btnTransferir.BackColor = SystemColors.Control;
            btnTransferir.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnTransferir.ForeColor = Color.Black;
            btnTransferir.Name = "btnTransferir";
            btnTransferir.Size = new Size(165, 19);
            btnTransferir.Text = "📤 Transferir al Presupuesto";
            btnTransferir.Click += btnTransferir_Click;
            // 
            // btnEliminarFila
            // 
            btnEliminarFila.BackColor = SystemColors.Control;
            btnEliminarFila.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnEliminarFila.ForeColor = Color.Black;
            btnEliminarFila.Name = "btnEliminarFila";
            btnEliminarFila.Size = new Size(71, 19);
            btnEliminarFila.Text = "🗑 Eliminar";
            btnEliminarFila.Click += btnEliminarFila_Click;
            // 
            // btnAgregarGrupo
            // 
            btnAgregarGrupo.BackColor = SystemColors.Control;
            btnAgregarGrupo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAgregarGrupo.ForeColor = Color.Black;
            btnAgregarGrupo.Name = "btnAgregarGrupo";
            btnAgregarGrupo.Size = new Size(110, 19);
            btnAgregarGrupo.Text = "➕ Agregar Grupo";
            btnAgregarGrupo.Click += btnAgregarGrupo_Click;
            // 
            // btnAgregarConcepto
            // 
            btnAgregarConcepto.BackColor = SystemColors.Control;
            btnAgregarConcepto.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAgregarConcepto.ForeColor = Color.Black;
            btnAgregarConcepto.Name = "btnAgregarConcepto";
            btnAgregarConcepto.Size = new Size(128, 19);
            btnAgregarConcepto.Text = "➕ Agregar Concepto";
            btnAgregarConcepto.Click += btnAgregarConcepto_Click;
            // 
            // btnPorcentajesDirectos
            // 
            btnPorcentajesDirectos.BackColor = SystemColors.Control;
            btnPorcentajesDirectos.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnPorcentajesDirectos.ForeColor = Color.Black;
            btnPorcentajesDirectos.Name = "btnPorcentajesDirectos";
            btnPorcentajesDirectos.Size = new Size(142, 19);
            btnPorcentajesDirectos.Text = "📝 Porcentajes Directos";
            btnPorcentajesDirectos.Click += btnPorcentajesDirectos_Click;
            // 
            // btnCerrar
            // 
            btnCerrar.Alignment = ToolStripItemAlignment.Right;
            btnCerrar.BackColor = Color.White;
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(58, 19);
            btnCerrar.Text = "✖ Cerrar";
            btnCerrar.Click += btnCerrar_Click;
            // 
            // btnAutoCD
            // 
            btnAutoCD.BackColor = Color.FromArgb(96, 125, 139);
            btnAutoCD.Font = new Font("Segoe UI", 9F);
            btnAutoCD.ForeColor = Color.White;
            btnAutoCD.Location = new Point(0, 0);
            btnAutoCD.Name = "btnAutoCD";
            btnAutoCD.Size = new Size(155, 35);
            btnAutoCD.TabIndex = 0;
            btnAutoCD.Text = "🔄 CD desde Presupuesto";
            btnAutoCD.UseVisualStyleBackColor = false;
            btnAutoCD.Click += btnAutoCD_Click;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabOficinaCentral);
            tabControl.Controls.Add(tabCampo);
            tabControl.Controls.Add(tabConfiguracion);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Font = new Font("Segoe UI", 10F);
            tabControl.Location = new Point(0, 98);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1400, 740);
            tabControl.TabIndex = 2;
            // 
            // tabOficinaCentral
            // 
            tabOficinaCentral.Controls.Add(dgvOficinaCentral);
            tabOficinaCentral.Location = new Point(4, 26);
            tabOficinaCentral.Name = "tabOficinaCentral";
            tabOficinaCentral.Padding = new Padding(3);
            tabOficinaCentral.Size = new Size(1392, 710);
            tabOficinaCentral.TabIndex = 0;
            tabOficinaCentral.Text = "🏢 Oficina Central";
            tabOficinaCentral.UseVisualStyleBackColor = true;
            // 
            // dgvOficinaCentral
            // 
            dgvOficinaCentral.AllowUserToAddRows = false;
            dgvOficinaCentral.AllowUserToDeleteRows = false;
            dgvOficinaCentral.BackgroundColor = Color.White;
            dgvOficinaCentral.BorderStyle = BorderStyle.None;
            dgvOficinaCentral.ColumnHeadersHeight = 40;
            dgvOficinaCentral.Dock = DockStyle.Fill;
            dgvOficinaCentral.Location = new Point(3, 3);
            dgvOficinaCentral.Name = "dgvOficinaCentral";
            dgvOficinaCentral.RowHeadersWidth = 40;
            dgvOficinaCentral.RowTemplate.Height = 28;
            dgvOficinaCentral.Size = new Size(1386, 704);
            dgvOficinaCentral.TabIndex = 0;
            dgvOficinaCentral.CellEndEdit += Dgv_CellEndEdit;
            dgvOficinaCentral.CellFormatting += Dgv_CellFormatting;
            // 
            // tabCampo
            // 
            tabCampo.Controls.Add(dgvCampo);
            tabCampo.Location = new Point(4, 26);
            tabCampo.Name = "tabCampo";
            tabCampo.Padding = new Padding(3);
            tabCampo.Size = new Size(1392, 710);
            tabCampo.TabIndex = 1;
            tabCampo.Text = "🏗 Campo";
            tabCampo.UseVisualStyleBackColor = true;
            // 
            // dgvCampo
            // 
            dgvCampo.AllowUserToAddRows = false;
            dgvCampo.AllowUserToDeleteRows = false;
            dgvCampo.BackgroundColor = Color.White;
            dgvCampo.BorderStyle = BorderStyle.None;
            dgvCampo.ColumnHeadersHeight = 40;
            dgvCampo.Dock = DockStyle.Fill;
            dgvCampo.Location = new Point(3, 3);
            dgvCampo.Name = "dgvCampo";
            dgvCampo.RowHeadersWidth = 40;
            dgvCampo.RowTemplate.Height = 28;
            dgvCampo.Size = new Size(1386, 704);
            dgvCampo.TabIndex = 0;
            dgvCampo.CellEndEdit += Dgv_CellEndEdit;
            dgvCampo.CellFormatting += Dgv_CellFormatting;
            // 
            // tabConfiguracion
            // 
            tabConfiguracion.Controls.Add(gbResumen);
            tabConfiguracion.Controls.Add(panelConfiguracion);
            tabConfiguracion.Location = new Point(4, 26);
            tabConfiguracion.Name = "tabConfiguracion";
            tabConfiguracion.Size = new Size(1392, 710);
            tabConfiguracion.TabIndex = 2;
            tabConfiguracion.Text = "⚙ Configuración y Resumen";
            tabConfiguracion.UseVisualStyleBackColor = true;
            // 
            // gbResumen
            // 
            gbResumen.Controls.Add(lblPorcentajeTotal);
            gbResumen.Controls.Add(lblPorcentajeCampo);
            gbResumen.Controls.Add(lblPorcentajeOC);
            gbResumen.Controls.Add(lblTotalCampo);
            gbResumen.Controls.Add(lblTotalOficinaCentral);
            gbResumen.Dock = DockStyle.Fill;
            gbResumen.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            gbResumen.Location = new Point(0, 150);
            gbResumen.Name = "gbResumen";
            gbResumen.Padding = new Padding(20);
            gbResumen.Size = new Size(1392, 560);
            gbResumen.TabIndex = 1;
            gbResumen.TabStop = false;
            gbResumen.Text = "📊 RESUMEN DE CÁLCULO";
            // 
            // lblPorcentajeTotal
            // 
            lblPorcentajeTotal.AutoSize = true;
            lblPorcentajeTotal.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            lblPorcentajeTotal.ForeColor = Color.FromArgb(76, 175, 80);
            lblPorcentajeTotal.Location = new Point(23, 260);
            lblPorcentajeTotal.Name = "lblPorcentajeTotal";
            lblPorcentajeTotal.Size = new Size(385, 37);
            lblPorcentajeTotal.TabIndex = 4;
            lblPorcentajeTotal.Text = "% TOTAL INDIRECTOS: 0.00%";
            // 
            // lblPorcentajeCampo
            // 
            lblPorcentajeCampo.AutoSize = true;
            lblPorcentajeCampo.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblPorcentajeCampo.ForeColor = Color.FromArgb(255, 152, 0);
            lblPorcentajeCampo.Location = new Point(23, 190);
            lblPorcentajeCampo.Name = "lblPorcentajeCampo";
            lblPorcentajeCampo.Size = new Size(187, 30);
            lblPorcentajeCampo.TabIndex = 3;
            lblPorcentajeCampo.Text = "% Campo: 0.00%";
            // 
            // lblPorcentajeOC
            // 
            lblPorcentajeOC.AutoSize = true;
            lblPorcentajeOC.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblPorcentajeOC.ForeColor = Color.FromArgb(33, 150, 243);
            lblPorcentajeOC.Location = new Point(23, 140);
            lblPorcentajeOC.Name = "lblPorcentajeOC";
            lblPorcentajeOC.Size = new Size(268, 30);
            lblPorcentajeOC.TabIndex = 2;
            lblPorcentajeOC.Text = "% Oficina Central: 0.00%";
            // 
            // lblTotalCampo
            // 
            lblTotalCampo.AutoSize = true;
            lblTotalCampo.Font = new Font("Segoe UI", 14F);
            lblTotalCampo.Location = new Point(23, 80);
            lblTotalCampo.Name = "lblTotalCampo";
            lblTotalCampo.Size = new Size(170, 25);
            lblTotalCampo.TabIndex = 1;
            lblTotalCampo.Text = "Total Campo: $0.00";
            // 
            // lblTotalOficinaCentral
            // 
            lblTotalOficinaCentral.AutoSize = true;
            lblTotalOficinaCentral.Font = new Font("Segoe UI", 14F);
            lblTotalOficinaCentral.Location = new Point(23, 40);
            lblTotalOficinaCentral.Name = "lblTotalOficinaCentral";
            lblTotalOficinaCentral.Size = new Size(290, 25);
            lblTotalOficinaCentral.TabIndex = 0;
            lblTotalOficinaCentral.Text = "Total Oficina Central Anual: $0.00";
            // 
            // panelConfiguracion
            // 
            panelConfiguracion.Controls.Add(txtCostoDirecto);
            panelConfiguracion.Controls.Add(lblCostoDirecto);
            panelConfiguracion.Controls.Add(txtVolumenAnual);
            panelConfiguracion.Controls.Add(lblVolumenAnual);
            panelConfiguracion.Dock = DockStyle.Top;
            panelConfiguracion.Location = new Point(0, 0);
            panelConfiguracion.Name = "panelConfiguracion";
            panelConfiguracion.Padding = new Padding(20);
            panelConfiguracion.Size = new Size(1392, 150);
            panelConfiguracion.TabIndex = 0;
            // 
            // txtCostoDirecto
            // 
            txtCostoDirecto.Font = new Font("Segoe UI", 12F);
            txtCostoDirecto.Location = new Point(27, 109);
            txtCostoDirecto.Name = "txtCostoDirecto";
            txtCostoDirecto.Size = new Size(300, 29);
            txtCostoDirecto.TabIndex = 3;
            txtCostoDirecto.TextAlign = HorizontalAlignment.Right;
            // 
            // lblCostoDirecto
            // 
            lblCostoDirecto.AutoSize = true;
            lblCostoDirecto.Font = new Font("Segoe UI", 10F);
            lblCostoDirecto.Location = new Point(23, 87);
            lblCostoDirecto.Name = "lblCostoDirecto";
            lblCostoDirecto.Size = new Size(191, 19);
            lblCostoDirecto.TabIndex = 2;
            lblCostoDirecto.Text = "Costo Directo de Esta Obra $:";
            // 
            // txtVolumenAnual
            // 
            txtVolumenAnual.Font = new Font("Segoe UI", 12F);
            txtVolumenAnual.Location = new Point(27, 45);
            txtVolumenAnual.Name = "txtVolumenAnual";
            txtVolumenAnual.Size = new Size(300, 29);
            txtVolumenAnual.TabIndex = 1;
            txtVolumenAnual.TextAlign = HorizontalAlignment.Right;
            // 
            // lblVolumenAnual
            // 
            lblVolumenAnual.AutoSize = true;
            lblVolumenAnual.Font = new Font("Segoe UI", 10F);
            lblVolumenAnual.Location = new Point(23, 23);
            lblVolumenAnual.Name = "lblVolumenAnual";
            lblVolumenAnual.Size = new Size(273, 19);
            lblVolumenAnual.TabIndex = 0;
            lblVolumenAnual.Text = "Volumen Anual de Obra (a costo directo) $:";
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 838);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1400, 22);
            statusStrip.TabIndex = 3;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(32, 17);
            lblStatus.Text = "Listo";
            // 
            // FormIndirectos
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1400, 860);
            Controls.Add(tabControl);
            Controls.Add(statusStrip);
            Controls.Add(panelToolbar);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            Name = "FormIndirectos";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Cálculo de Indirectos - SOPRO";
            Load += FormIndirectos_Load;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelToolbar.ResumeLayout(false);
            panelToolbar.PerformLayout();
            tabControl.ResumeLayout(false);
            tabOficinaCentral.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvOficinaCentral).EndInit();
            tabCampo.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvCampo).EndInit();
            tabConfiguracion.ResumeLayout(false);
            gbResumen.ResumeLayout(false);
            gbResumen.PerformLayout();
            panelConfiguracion.ResumeLayout(false);
            panelConfiguracion.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
