using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    partial class FormUtilidad
    {
        private System.ComponentModel.IContainer components = null;

        private Panel panelTop;
        private Label lblTitulo;
        private Label lblProyecto;

        private ToolStrip panelToolbar;
        private ToolStripButton btnCalcular;
        private ToolStripButton btnTransferir;
        private ToolStripButton btnCerrar;

        private Panel panelBody;
        private GroupBox gbBase;
        private Label lblCD;
        private Label lblCI;
        private Label lblF;
        private Label lblBase;
        private Label lblCDTitle;
        private Label lblCITitle;
        private Label lblFTitle;
        private Label lblBaseTitle;
        private Label lblModoCalculoTitulo;
        private Label lblModoCalculo;

        private GroupBox gbEntrada;
        private ComboBox cboModo;
        private NumericUpDown nudUtilidadDirecta;
        private NumericUpDown nudUtilidadNeta;
        private NumericUpDown nudISR;
        private NumericUpDown nudPTU;
        private Label lblModo;
        private Label lblUtilDirecta;
        private Label lblUtilNeta;
        private Label lblISRTitle;
        private Label lblPTUTitle;
        private Label lblAyudaISR;
        private Label lblAyudaPTU;

        private GroupBox gbResultado;
        private Label lblPorcentaje;
        private Label lblImporteUtilidad;
        private Label lblImporteISR;
        private Label lblImportePTU;
        private Label lblUtilidadNeta;
        private Label lblPorcentajeTitle;
        private Label lblImporteUtilidadTitle;
        private Label lblImporteISRTitle;
        private Label lblImportePTUTitle;
        private Label lblUtilidadNetaTitle;
        private Label lblNotaTransferencia;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            panelTop = new Panel();
            lblProyecto = new Label();
            lblTitulo = new Label();
            panelToolbar = new ToolStrip();
            btnCalcular = new ToolStripButton();
            btnTransferir = new ToolStripButton();
            btnCerrar = new ToolStripButton();
            panelBody = new Panel();
            gbResultado = new GroupBox();
            lblNotaTransferencia = new Label();
            lblUtilidadNeta = new Label();
            lblUtilidadNetaTitle = new Label();
            lblImportePTU = new Label();
            lblImportePTUTitle = new Label();
            lblImporteISR = new Label();
            lblImporteISRTitle = new Label();
            lblImporteUtilidad = new Label();
            lblImporteUtilidadTitle = new Label();
            lblPorcentaje = new Label();
            lblPorcentajeTitle = new Label();
            gbEntrada = new GroupBox();
            lblAyudaPTU = new Label();
            lblAyudaISR = new Label();
            nudPTU = new NumericUpDown();
            lblPTUTitle = new Label();
            nudISR = new NumericUpDown();
            lblISRTitle = new Label();
            nudUtilidadNeta = new NumericUpDown();
            lblUtilNeta = new Label();
            nudUtilidadDirecta = new NumericUpDown();
            lblUtilDirecta = new Label();
            cboModo = new ComboBox();
            lblModo = new Label();
            gbBase = new GroupBox();
            lblModoCalculo = new Label();
            lblModoCalculoTitulo = new Label();
            lblBase = new Label();
            lblBaseTitle = new Label();
            lblF = new Label();
            lblFTitle = new Label();
            lblCI = new Label();
            lblCITitle = new Label();
            lblCD = new Label();
            lblCDTitle = new Label();
            panelTop.SuspendLayout();
            panelToolbar.SuspendLayout();
            panelBody.SuspendLayout();
            gbResultado.SuspendLayout();
            gbEntrada.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudPTU).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudISR).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudUtilidadNeta).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudUtilidadDirecta).BeginInit();
            gbBase.SuspendLayout();
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
            panelTop.Size = new Size(980, 72);
            panelTop.TabIndex = 0;
            // 
            // lblProyecto
            // 
            lblProyecto.AutoSize = true;
            lblProyecto.Font = new Font("Segoe UI", 9F);
            lblProyecto.ForeColor = Color.WhiteSmoke;
            lblProyecto.Location = new Point(14, 44);
            lblProyecto.Name = "lblProyecto";
            lblProyecto.Size = new Size(54, 15);
            lblProyecto.TabIndex = 1;
            lblProyecto.Text = "Proyecto";
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(12, 8);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(287, 30);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "💼 CÁLCULO DE UTILIDAD";
            // 
            // panelToolbar
            // 
            panelToolbar.BackColor = Color.FromArgb(240, 240, 240);
            panelToolbar.Items.AddRange(new ToolStripItem[] { btnCalcular, btnTransferir, btnCerrar });
            panelToolbar.Location = new Point(0, 72);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Padding = new Padding(8, 4, 8, 4);
            panelToolbar.Size = new Size(980, 30);
            panelToolbar.TabIndex = 1;
            // 
            // btnCalcular
            // 
            btnCalcular.BackColor = SystemColors.Control;
            btnCalcular.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCalcular.ForeColor = Color.Black;
            btnCalcular.Name = "btnCalcular";
            btnCalcular.Size = new Size(70, 19);
            btnCalcular.Text = "\U0001f9ee Calcular";
            btnCalcular.Click += btnCalcular_Click;
            // 
            // btnTransferir
            // 
            btnTransferir.BackColor = SystemColors.Control;
            btnTransferir.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnTransferir.ForeColor = Color.Black;
            btnTransferir.Name = "btnTransferir";
            btnTransferir.Size = new Size(146, 19);
            btnTransferir.Text = "✅ Transferir al Proyecto";
            btnTransferir.Click += btnTransferir_Click;
            // 
            // btnCerrar
            // 
            btnCerrar.Alignment = ToolStripItemAlignment.Right;
            btnCerrar.BackColor = SystemColors.Control;
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(58, 19);
            btnCerrar.Text = "✖ Cerrar";
            btnCerrar.Click += btnCerrar_Click;
            // 
            // panelBody
            // 
            panelBody.BackColor = Color.WhiteSmoke;
            panelBody.Controls.Add(gbResultado);
            panelBody.Controls.Add(gbEntrada);
            panelBody.Controls.Add(gbBase);
            panelBody.Dock = DockStyle.Fill;
            panelBody.Location = new Point(0, 102);
            panelBody.Name = "panelBody";
            panelBody.Padding = new Padding(12);
            panelBody.Size = new Size(980, 446);
            panelBody.TabIndex = 2;
            // 
            // gbResultado
            // 
            gbResultado.Controls.Add(lblNotaTransferencia);
            gbResultado.Controls.Add(lblUtilidadNeta);
            gbResultado.Controls.Add(lblUtilidadNetaTitle);
            gbResultado.Controls.Add(lblImportePTU);
            gbResultado.Controls.Add(lblImportePTUTitle);
            gbResultado.Controls.Add(lblImporteISR);
            gbResultado.Controls.Add(lblImporteISRTitle);
            gbResultado.Controls.Add(lblImporteUtilidad);
            gbResultado.Controls.Add(lblImporteUtilidadTitle);
            gbResultado.Controls.Add(lblPorcentaje);
            gbResultado.Controls.Add(lblPorcentajeTitle);
            gbResultado.Location = new Point(12, 255);
            gbResultado.Name = "gbResultado";
            gbResultado.Size = new Size(956, 145);
            gbResultado.TabIndex = 2;
            gbResultado.TabStop = false;
            gbResultado.Text = "Resultado";
            // 
            // lblNotaTransferencia
            // 
            lblNotaTransferencia.AutoSize = true;
            lblNotaTransferencia.ForeColor = Color.FromArgb(64, 64, 64);
            lblNotaTransferencia.Location = new Point(20, 108);
            lblNotaTransferencia.Name = "lblNotaTransferencia";
            lblNotaTransferencia.Size = new Size(347, 15);
            lblNotaTransferencia.TabIndex = 10;
            lblNotaTransferencia.Text = "El porcentaje que se transfiere al presupuesto es la utilidad bruta.";
            // 
            // lblUtilidadNeta
            // 
            lblUtilidadNeta.AutoSize = true;
            lblUtilidadNeta.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblUtilidadNeta.Location = new Point(731, 35);
            lblUtilidadNeta.Name = "lblUtilidadNeta";
            lblUtilidadNeta.Size = new Size(38, 15);
            lblUtilidadNeta.TabIndex = 9;
            lblUtilidadNeta.Text = "$0.00";
            // 
            // lblUtilidadNetaTitle
            // 
            lblUtilidadNetaTitle.AutoSize = true;
            lblUtilidadNetaTitle.Location = new Point(594, 35);
            lblUtilidadNetaTitle.Name = "lblUtilidadNetaTitle";
            lblUtilidadNetaTitle.Size = new Size(128, 15);
            lblUtilidadNetaTitle.TabIndex = 8;
            lblUtilidadNetaTitle.Text = "Utilidad neta estimada:";
            // 
            // lblImportePTU
            // 
            lblImportePTU.AutoSize = true;
            lblImportePTU.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblImportePTU.Location = new Point(408, 70);
            lblImportePTU.Name = "lblImportePTU";
            lblImportePTU.Size = new Size(38, 15);
            lblImportePTU.TabIndex = 7;
            lblImportePTU.Text = "$0.00";
            // 
            // lblImportePTUTitle
            // 
            lblImportePTUTitle.AutoSize = true;
            lblImportePTUTitle.Location = new Point(285, 70);
            lblImportePTUTitle.Name = "lblImportePTUTitle";
            lblImportePTUTitle.Size = new Size(84, 15);
            lblImportePTUTitle.TabIndex = 6;
            lblImportePTUTitle.Text = "PTU estimado:";
            // 
            // lblImporteISR
            // 
            lblImporteISR.AutoSize = true;
            lblImporteISR.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblImporteISR.Location = new Point(408, 35);
            lblImporteISR.Name = "lblImporteISR";
            lblImporteISR.Size = new Size(38, 15);
            lblImporteISR.TabIndex = 5;
            lblImporteISR.Text = "$0.00";
            // 
            // lblImporteISRTitle
            // 
            lblImporteISRTitle.AutoSize = true;
            lblImporteISRTitle.Location = new Point(285, 35);
            lblImporteISRTitle.Name = "lblImporteISRTitle";
            lblImporteISRTitle.Size = new Size(78, 15);
            lblImporteISRTitle.TabIndex = 4;
            lblImporteISRTitle.Text = "ISR estimado:";
            // 
            // lblImporteUtilidad
            // 
            lblImporteUtilidad.AutoSize = true;
            lblImporteUtilidad.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblImporteUtilidad.Location = new Point(147, 70);
            lblImporteUtilidad.Name = "lblImporteUtilidad";
            lblImporteUtilidad.Size = new Size(38, 15);
            lblImporteUtilidad.TabIndex = 3;
            lblImporteUtilidad.Text = "$0.00";
            // 
            // lblImporteUtilidadTitle
            // 
            lblImporteUtilidadTitle.AutoSize = true;
            lblImporteUtilidadTitle.Location = new Point(20, 70);
            lblImporteUtilidadTitle.Name = "lblImporteUtilidadTitle";
            lblImporteUtilidadTitle.Size = new Size(95, 15);
            lblImporteUtilidadTitle.TabIndex = 2;
            lblImporteUtilidadTitle.Text = "Importe utilidad:";
            // 
            // lblPorcentaje
            // 
            lblPorcentaje.AutoSize = true;
            lblPorcentaje.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblPorcentaje.ForeColor = Color.FromArgb(22, 163, 74);
            lblPorcentaje.Location = new Point(147, 32);
            lblPorcentaje.Name = "lblPorcentaje";
            lblPorcentaje.Size = new Size(80, 20);
            lblPorcentaje.TabIndex = 1;
            lblPorcentaje.Text = "0.00000%";
            // 
            // lblPorcentajeTitle
            // 
            lblPorcentajeTitle.AutoSize = true;
            lblPorcentajeTitle.Location = new Point(20, 35);
            lblPorcentajeTitle.Name = "lblPorcentajeTitle";
            lblPorcentajeTitle.Size = new Size(122, 15);
            lblPorcentajeTitle.TabIndex = 0;
            lblPorcentajeTitle.Text = "% utilidad a transferir:";
            // 
            // gbEntrada
            // 
            gbEntrada.Controls.Add(lblAyudaPTU);
            gbEntrada.Controls.Add(lblAyudaISR);
            gbEntrada.Controls.Add(nudPTU);
            gbEntrada.Controls.Add(lblPTUTitle);
            gbEntrada.Controls.Add(nudISR);
            gbEntrada.Controls.Add(lblISRTitle);
            gbEntrada.Controls.Add(nudUtilidadNeta);
            gbEntrada.Controls.Add(lblUtilNeta);
            gbEntrada.Controls.Add(nudUtilidadDirecta);
            gbEntrada.Controls.Add(lblUtilDirecta);
            gbEntrada.Controls.Add(cboModo);
            gbEntrada.Controls.Add(lblModo);
            gbEntrada.Location = new Point(12, 116);
            gbEntrada.Name = "gbEntrada";
            gbEntrada.Size = new Size(956, 127);
            gbEntrada.TabIndex = 1;
            gbEntrada.TabStop = false;
            gbEntrada.Text = "Parámetros";
            // 
            // lblAyudaPTU
            // 
            lblAyudaPTU.AutoSize = true;
            lblAyudaPTU.ForeColor = Color.DimGray;
            lblAyudaPTU.Location = new Point(658, 71);
            lblAyudaPTU.Name = "lblAyudaPTU";
            lblAyudaPTU.Size = new Size(249, 15);
            lblAyudaPTU.TabIndex = 11;
            lblAyudaPTU.Text = "PTU = reparto de utilidades a los trabajadores.";
            // 
            // lblAyudaISR
            // 
            lblAyudaISR.AutoSize = true;
            lblAyudaISR.ForeColor = Color.DimGray;
            lblAyudaISR.Location = new Point(658, 35);
            lblAyudaISR.Name = "lblAyudaISR";
            lblAyudaISR.Size = new Size(240, 15);
            lblAyudaISR.TabIndex = 10;
            lblAyudaISR.Text = "ISR = impuesto sobre la renta de la empresa.";
            // 
            // nudPTU
            // 
            nudPTU.DecimalPlaces = 2;
            nudPTU.Location = new Point(570, 67);
            nudPTU.Name = "nudPTU";
            nudPTU.Size = new Size(80, 23);
            nudPTU.TabIndex = 9;
            nudPTU.ValueChanged += ValoresChanged;
            // 
            // lblPTUTitle
            // 
            lblPTUTitle.AutoSize = true;
            lblPTUTitle.Location = new Point(500, 70);
            lblPTUTitle.Name = "lblPTUTitle";
            lblPTUTitle.Size = new Size(53, 15);
            lblPTUTitle.TabIndex = 8;
            lblPTUTitle.Text = "PTU (%):";
            // 
            // nudISR
            // 
            nudISR.DecimalPlaces = 2;
            nudISR.Location = new Point(570, 31);
            nudISR.Name = "nudISR";
            nudISR.Size = new Size(80, 23);
            nudISR.TabIndex = 7;
            nudISR.ValueChanged += ValoresChanged;
            // 
            // lblISRTitle
            // 
            lblISRTitle.AutoSize = true;
            lblISRTitle.Location = new Point(500, 35);
            lblISRTitle.Name = "lblISRTitle";
            lblISRTitle.Size = new Size(47, 15);
            lblISRTitle.TabIndex = 6;
            lblISRTitle.Text = "ISR (%):";
            // 
            // nudUtilidadNeta
            // 
            nudUtilidadNeta.DecimalPlaces = 5;
            nudUtilidadNeta.Location = new Point(370, 67);
            nudUtilidadNeta.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            nudUtilidadNeta.Name = "nudUtilidadNeta";
            nudUtilidadNeta.Size = new Size(100, 23);
            nudUtilidadNeta.TabIndex = 5;
            nudUtilidadNeta.ValueChanged += ValoresChanged;
            // 
            // lblUtilNeta
            // 
            lblUtilNeta.AutoSize = true;
            lblUtilNeta.Location = new Point(260, 70);
            lblUtilNeta.Name = "lblUtilNeta";
            lblUtilNeta.Size = new Size(89, 15);
            lblUtilNeta.TabIndex = 4;
            lblUtilNeta.Text = "% utilidad neta:";
            // 
            // nudUtilidadDirecta
            // 
            nudUtilidadDirecta.DecimalPlaces = 5;
            nudUtilidadDirecta.Location = new Point(140, 67);
            nudUtilidadDirecta.Maximum = new decimal(new int[] { 999, 0, 0, 0 });
            nudUtilidadDirecta.Name = "nudUtilidadDirecta";
            nudUtilidadDirecta.Size = new Size(100, 23);
            nudUtilidadDirecta.TabIndex = 3;
            nudUtilidadDirecta.ValueChanged += ValoresChanged;
            // 
            // lblUtilDirecta
            // 
            lblUtilDirecta.AutoSize = true;
            lblUtilDirecta.Location = new Point(20, 70);
            lblUtilDirecta.Name = "lblUtilDirecta";
            lblUtilDirecta.Size = new Size(102, 15);
            lblUtilDirecta.TabIndex = 2;
            lblUtilDirecta.Text = "% utilidad directa:";
            // 
            // cboModo
            // 
            cboModo.DropDownStyle = ComboBoxStyle.DropDownList;
            cboModo.FormattingEnabled = true;
            cboModo.Items.AddRange(new object[] { "Directo", "Asistido (neta + ISR/PTU)" });
            cboModo.Location = new Point(140, 31);
            cboModo.Name = "cboModo";
            cboModo.Size = new Size(330, 23);
            cboModo.TabIndex = 1;
            cboModo.SelectedIndexChanged += ValoresChanged;
            // 
            // lblModo
            // 
            lblModo.AutoSize = true;
            lblModo.Location = new Point(20, 35);
            lblModo.Name = "lblModo";
            lblModo.Size = new Size(42, 15);
            lblModo.TabIndex = 0;
            lblModo.Text = "Modo:";
            // 
            // gbBase
            // 
            gbBase.Controls.Add(lblModoCalculo);
            gbBase.Controls.Add(lblModoCalculoTitulo);
            gbBase.Controls.Add(lblBase);
            gbBase.Controls.Add(lblBaseTitle);
            gbBase.Controls.Add(lblF);
            gbBase.Controls.Add(lblFTitle);
            gbBase.Controls.Add(lblCI);
            gbBase.Controls.Add(lblCITitle);
            gbBase.Controls.Add(lblCD);
            gbBase.Controls.Add(lblCDTitle);
            gbBase.Location = new Point(12, 12);
            gbBase.Name = "gbBase";
            gbBase.Size = new Size(956, 92);
            gbBase.TabIndex = 0;
            gbBase.TabStop = false;
            gbBase.Text = "Base de cálculo";
            // 
            // lblModoCalculo
            // 
            lblModoCalculo.AutoSize = true;
            lblModoCalculo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblModoCalculo.Location = new Point(729, 35);
            lblModoCalculo.Name = "lblModoCalculo";
            lblModoCalculo.Size = new Size(72, 15);
            lblModoCalculo.TabIndex = 9;
            lblModoCalculo.Text = "Acumulable";
            // 
            // lblModoCalculoTitulo
            // 
            lblModoCalculoTitulo.AutoSize = true;
            lblModoCalculoTitulo.Location = new Point(594, 35);
            lblModoCalculoTitulo.Name = "lblModoCalculoTitulo";
            lblModoCalculoTitulo.Size = new Size(106, 15);
            lblModoCalculoTitulo.TabIndex = 8;
            lblModoCalculoTitulo.Text = "Modo porcentajes:";
            // 
            // lblBase
            // 
            lblBase.AutoSize = true;
            lblBase.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblBase.ForeColor = Color.FromArgb(22, 163, 74);
            lblBase.Location = new Point(147, 56);
            lblBase.Name = "lblBase";
            lblBase.Size = new Size(45, 19);
            lblBase.TabIndex = 7;
            lblBase.Text = "$0.00";
            // 
            // lblBaseTitle
            // 
            lblBaseTitle.AutoSize = true;
            lblBaseTitle.Location = new Point(20, 58);
            lblBaseTitle.Name = "lblBaseTitle";
            lblBaseTitle.Size = new Size(98, 15);
            lblBaseTitle.TabIndex = 6;
            lblBaseTitle.Text = "Base CD + CI + F:";
            // 
            // lblF
            // 
            lblF.AutoSize = true;
            lblF.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblF.Location = new Point(482, 58);
            lblF.Name = "lblF";
            lblF.Size = new Size(38, 15);
            lblF.TabIndex = 5;
            lblF.Text = "$0.00";
            // 
            // lblFTitle
            // 
            lblFTitle.AutoSize = true;
            lblFTitle.Location = new Point(351, 58);
            lblFTitle.Name = "lblFTitle";
            lblFTitle.Size = new Size(92, 15);
            lblFTitle.TabIndex = 4;
            lblFTitle.Text = "Financiamiento:";
            // 
            // lblCI
            // 
            lblCI.AutoSize = true;
            lblCI.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCI.Location = new Point(482, 28);
            lblCI.Name = "lblCI";
            lblCI.Size = new Size(38, 15);
            lblCI.TabIndex = 3;
            lblCI.Text = "$0.00";
            // 
            // lblCITitle
            // 
            lblCITitle.AutoSize = true;
            lblCITitle.Location = new Point(351, 28);
            lblCITitle.Name = "lblCITitle";
            lblCITitle.Size = new Size(62, 15);
            lblCITitle.TabIndex = 2;
            lblCITitle.Text = "Indirectos:";
            // 
            // lblCD
            // 
            lblCD.AutoSize = true;
            lblCD.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCD.Location = new Point(147, 28);
            lblCD.Name = "lblCD";
            lblCD.Size = new Size(38, 15);
            lblCD.TabIndex = 1;
            lblCD.Text = "$0.00";
            // 
            // lblCDTitle
            // 
            lblCDTitle.AutoSize = true;
            lblCDTitle.Location = new Point(20, 28);
            lblCDTitle.Name = "lblCDTitle";
            lblCDTitle.Size = new Size(81, 15);
            lblCDTitle.TabIndex = 0;
            lblCDTitle.Text = "Costo directo:";
            // 
            // FormUtilidad
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(980, 548);
            Controls.Add(panelBody);
            Controls.Add(panelToolbar);
            Controls.Add(panelTop);
            MinimumSize = new Size(980, 548);
            Name = "FormUtilidad";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Utilidad — Cálculo RLOPSRM";
            Load += FormUtilidad_Load;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelToolbar.ResumeLayout(false);
            panelToolbar.PerformLayout();
            panelBody.ResumeLayout(false);
            gbResultado.ResumeLayout(false);
            gbResultado.PerformLayout();
            gbEntrada.ResumeLayout(false);
            gbEntrada.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudPTU).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudISR).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudUtilidadNeta).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudUtilidadDirecta).EndInit();
            gbBase.ResumeLayout(false);
            gbBase.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
