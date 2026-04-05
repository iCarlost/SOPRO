using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    partial class FormFinanciamiento
    {
        private System.ComponentModel.IContainer components = null;

        // ── Header ────────────────────────────────────────────────────────────
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.Label lblProyecto;

        // ── Toolbar ───────────────────────────────────────────────────────────
        private System.Windows.Forms.ToolStrip panelToolbar;
        private System.Windows.Forms.ToolStripButton btnCalcular;
        private System.Windows.Forms.ToolStripButton btnTransferir;
        private System.Windows.Forms.ToolStripButton btnCerrar;
        private System.Windows.Forms.ToolStripButton btnConfigColumnas;
        private System.Windows.Forms.ToolStripLabel lblModeloFinanciamiento;
        private System.Windows.Forms.ToolStripComboBox cboModeloFinanciamiento;
        private System.Windows.Forms.Label lblSinPrograma;

        // ── Panel izquierdo: parámetros ───────────────────────────────────────
        private System.Windows.Forms.Panel panelParams;
        private System.Windows.Forms.GroupBox gbTasas;
        private System.Windows.Forms.Label lblTIIE;
        private System.Windows.Forms.NumericUpDown nudTIIE;
        private System.Windows.Forms.Label lblPuntos;
        private System.Windows.Forms.NumericUpDown nudPuntos;
        private System.Windows.Forms.Label lblTasaEfectiva;
        private System.Windows.Forms.GroupBox gbContrato;
        private System.Windows.Forms.Label lblAnticipo;
        private System.Windows.Forms.NumericUpDown nudAnticipo;
        private System.Windows.Forms.Label lblPeriodosAmort;
        private System.Windows.Forms.NumericUpDown nudPeriodosAmort;
        private System.Windows.Forms.Label lblDesfase;
        private System.Windows.Forms.NumericUpDown nudDesfase;
        private System.Windows.Forms.GroupBox gbBaseCalculo;
        private System.Windows.Forms.RadioButton rbAcumulable;
        private System.Windows.Forms.RadioButton rbSobreCD;

        // ── Panel resultado ───────────────────────────────────────────────────
        private System.Windows.Forms.Panel panelResultado;
        private System.Windows.Forms.GroupBox gbResultado;
        private System.Windows.Forms.Label lblRIntNeg;
        private System.Windows.Forms.Label lblInteresesNeg;
        private System.Windows.Forms.Label lblRIntPos;
        private System.Windows.Forms.Label lblInteresesPos;
        private System.Windows.Forms.Label lblRFinNeto;
        private System.Windows.Forms.Label lblFinanciamientoNeto;
        private System.Windows.Forms.Label lblRPorcentaje;
        private System.Windows.Forms.Label lblPorcentaje;
        private System.Windows.Forms.Label lblFechaCalculo;

        // ── Tabla flujo ───────────────────────────────────────────────────────
        private System.Windows.Forms.DataGridView dgvFlujo;

        // ── Status ────────────────────────────────────────────────────────────
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle9 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle10 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle12 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle11 = new DataGridViewCellStyle();
            panelTop = new Panel();
            lblProyecto = new Label();
            lblTitulo = new Label();
            lblSinPrograma = new Label();
            panelToolbar = new ToolStrip();
            btnCalcular = new ToolStripButton();
            btnConfigColumnas = new ToolStripButton();
            lblModeloFinanciamiento = new ToolStripLabel();
            cboModeloFinanciamiento = new ToolStripComboBox();
            btnTransferir = new ToolStripButton();
            btnCerrar = new ToolStripButton();
            panelParams = new Panel();
            gbTasas = new GroupBox();
            lblTIIE = new Label();
            nudTIIE = new NumericUpDown();
            lblPuntos = new Label();
            nudPuntos = new NumericUpDown();
            lblTasaEfectiva = new Label();
            gbContrato = new GroupBox();
            lblAnticipo = new Label();
            nudAnticipo = new NumericUpDown();
            lblPeriodosAmort = new Label();
            nudPeriodosAmort = new NumericUpDown();
            lblDesfase = new Label();
            nudDesfase = new NumericUpDown();
            gbBaseCalculo = new GroupBox();
            rbAcumulable = new RadioButton();
            rbSobreCD = new RadioButton();
            panelResultado = new Panel();
            gbResultado = new GroupBox();
            lblRIntNeg = new Label();
            lblInteresesNeg = new Label();
            lblRIntPos = new Label();
            lblInteresesPos = new Label();
            lblRFinNeto = new Label();
            lblFinanciamientoNeto = new Label();
            lblRPorcentaje = new Label();
            lblPorcentaje = new Label();
            lblFechaCalculo = new Label();
            dgvFlujo = new DataGridView();
            colPeriodo = new DataGridViewTextBoxColumn();
            colInicio = new DataGridViewTextBoxColumn();
            colFin = new DataGridViewTextBoxColumn();
            colDias = new DataGridViewTextBoxColumn();
            colCD = new DataGridViewTextBoxColumn();
            colCI = new DataGridViewTextBoxColumn();
            colEgresos = new DataGridViewTextBoxColumn();
            colAnticipo = new DataGridViewTextBoxColumn();
            colEstim = new DataGridViewTextBoxColumn();
            colAmort = new DataGridViewTextBoxColumn();
            colCobroNeto = new DataGridViewTextBoxColumn();
            colFlujoNeto = new DataGridViewTextBoxColumn();
            colSaldo = new DataGridViewTextBoxColumn();
            colTasa = new DataGridViewTextBoxColumn();
            colInteres = new DataGridViewTextBoxColumn();
            colDummy = new DataGridViewTextBoxColumn();
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            panelTop.SuspendLayout();
            panelToolbar.SuspendLayout();
            panelParams.SuspendLayout();
            gbTasas.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudTIIE).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudPuntos).BeginInit();
            gbContrato.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudAnticipo).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudPeriodosAmort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudDesfase).BeginInit();
            gbBaseCalculo.SuspendLayout();
            panelResultado.SuspendLayout();
            gbResultado.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFlujo).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(51, 51, 76);
            panelTop.Controls.Add(lblProyecto);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Controls.Add(lblSinPrograma);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(1200, 68);
            panelTop.TabIndex = 0;
            // 
            // lblProyecto
            // 
            lblProyecto.AutoSize = true;
            lblProyecto.Font = new Font("Segoe UI", 9F);
            lblProyecto.ForeColor = Color.WhiteSmoke;
            lblProyecto.Location = new Point(18, 42);
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
            lblTitulo.Location = new Point(12, 9);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(375, 30);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "💰 CÁLCULO DE FINANCIAMIENTO";
            // 
            // lblSinPrograma
            // 
            lblSinPrograma.AutoSize = true;
            lblSinPrograma.Font = new Font("Segoe UI", 9F);
            lblSinPrograma.ForeColor = Color.DarkOrange;
            lblSinPrograma.Location = new Point(602, 44);
            lblSinPrograma.Name = "lblSinPrograma";
            lblSinPrograma.Size = new Size(438, 15);
            lblSinPrograma.TabIndex = 3;
            lblSinPrograma.Text = "⚠ No hay programa de obra. Genera el programa en el módulo de Programación.";
            lblSinPrograma.Visible = false;
            // 
            // panelToolbar
            // 
            panelToolbar.BackColor = Color.FromArgb(240, 240, 240);
            panelToolbar.GripStyle = ToolStripGripStyle.Hidden;
            panelToolbar.Items.AddRange(new ToolStripItem[] { btnCalcular, btnConfigColumnas, lblModeloFinanciamiento, cboModeloFinanciamiento, btnTransferir, btnCerrar });
            panelToolbar.Location = new Point(0, 68);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Padding = new Padding(8, 4, 8, 4);
            panelToolbar.Size = new Size(1200, 30);
            panelToolbar.TabIndex = 1;
            // 
            // btnCalcular
            // 
            btnCalcular.BackColor = SystemColors.Control;
            btnCalcular.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnCalcular.ForeColor = Color.Black;
            btnCalcular.Name = "btnCalcular";
            btnCalcular.Size = new Size(74, 19);
            btnCalcular.Text = "\U0001f9ee Calcular";
            btnCalcular.Click += btnCalcular_Click;
            // 
            // btnConfigColumnas
            // 
            btnConfigColumnas.BackColor = SystemColors.Control;
            btnConfigColumnas.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnConfigColumnas.ForeColor = Color.Black;
            btnConfigColumnas.Name = "btnConfigColumnas";
            btnConfigColumnas.Size = new Size(82, 19);
            btnConfigColumnas.Text = "📐 Columnas";
            btnConfigColumnas.Click += btnConfigColumnas_Click;
            // 
            // lblModeloFinanciamiento
            // 
            lblModeloFinanciamiento.Name = "lblModeloFinanciamiento";
            lblModeloFinanciamiento.Size = new Size(53, 19);
            lblModeloFinanciamiento.Text = "Modelo:";
            // 
            // cboModeloFinanciamiento
            // 
            cboModeloFinanciamiento.AutoSize = false;
            cboModeloFinanciamiento.DropDownStyle = ComboBoxStyle.DropDownList;
            cboModeloFinanciamiento.Name = "cboModeloFinanciamiento";
            cboModeloFinanciamiento.Size = new Size(110, 23);
            // 
            // btnTransferir
            // 
            btnTransferir.BackColor = SystemColors.Control;
            btnTransferir.Enabled = false;
            btnTransferir.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
            btnTransferir.ForeColor = Color.Black;
            btnTransferir.Name = "btnTransferir";
            btnTransferir.Size = new Size(150, 19);
            btnTransferir.Text = "✅ Transferir al Proyecto";
            btnTransferir.Click += btnTransferir_Click;
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
            // panelParams
            // 
            panelParams.BackColor = Color.FromArgb(248, 249, 250);
            panelParams.BorderStyle = BorderStyle.FixedSingle;
            panelParams.Controls.Add(gbTasas);
            panelParams.Controls.Add(gbContrato);
            panelParams.Controls.Add(gbBaseCalculo);
            panelParams.Dock = DockStyle.Left;
            panelParams.Location = new Point(0, 98);
            panelParams.Name = "panelParams";
            panelParams.Padding = new Padding(8);
            panelParams.Size = new Size(300, 620);
            panelParams.TabIndex = 2;
            // 
            // gbTasas
            // 
            gbTasas.Controls.Add(lblTIIE);
            gbTasas.Controls.Add(nudTIIE);
            gbTasas.Controls.Add(lblPuntos);
            gbTasas.Controls.Add(nudPuntos);
            gbTasas.Controls.Add(lblTasaEfectiva);
            gbTasas.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            gbTasas.Location = new Point(10, 8);
            gbTasas.Name = "gbTasas";
            gbTasas.Size = new Size(276, 160);
            gbTasas.TabIndex = 0;
            gbTasas.TabStop = false;
            gbTasas.Text = "Tasas de interés";
            // 
            // lblTIIE
            // 
            lblTIIE.AutoSize = true;
            lblTIIE.Font = new Font("Segoe UI", 9F);
            lblTIIE.Location = new Point(10, 24);
            lblTIIE.Name = "lblTIIE";
            lblTIIE.Size = new Size(82, 15);
            lblTIIE.TabIndex = 0;
            lblTIIE.Text = "TIIE (% anual):";
            // 
            // nudTIIE
            // 
            nudTIIE.DecimalPlaces = 4;
            nudTIIE.Font = new Font("Segoe UI", 9F);
            nudTIIE.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            nudTIIE.Location = new Point(10, 42);
            nudTIIE.Name = "nudTIIE";
            nudTIIE.Size = new Size(120, 23);
            nudTIIE.TabIndex = 1;
            nudTIIE.Value = new decimal(new int[] { 11, 0, 0, 0 });
            nudTIIE.ValueChanged += nudTIIE_ValueChanged;
            // 
            // lblPuntos
            // 
            lblPuntos.AutoSize = true;
            lblPuntos.Font = new Font("Segoe UI", 9F);
            lblPuntos.Location = new Point(10, 76);
            lblPuntos.Name = "lblPuntos";
            lblPuntos.Size = new Size(198, 15);
            lblPuntos.TabIndex = 2;
            lblPuntos.Text = "Puntos adicionales banco (% anual):";
            // 
            // nudPuntos
            // 
            nudPuntos.DecimalPlaces = 4;
            nudPuntos.Font = new Font("Segoe UI", 9F);
            nudPuntos.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
            nudPuntos.Location = new Point(10, 94);
            nudPuntos.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
            nudPuntos.Name = "nudPuntos";
            nudPuntos.Size = new Size(120, 23);
            nudPuntos.TabIndex = 3;
            nudPuntos.Value = new decimal(new int[] { 3, 0, 0, 0 });
            nudPuntos.ValueChanged += nudPuntos_ValueChanged;
            // 
            // lblTasaEfectiva
            // 
            lblTasaEfectiva.AutoSize = true;
            lblTasaEfectiva.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblTasaEfectiva.ForeColor = Color.FromArgb(51, 51, 76);
            lblTasaEfectiva.Location = new Point(10, 130);
            lblTasaEfectiva.Name = "lblTasaEfectiva";
            lblTasaEfectiva.Size = new Size(172, 15);
            lblTasaEfectiva.TabIndex = 4;
            lblTasaEfectiva.Text = "Tasa efectiva: 14.0000% anual";
            // 
            // gbContrato
            // 
            gbContrato.Controls.Add(lblAnticipo);
            gbContrato.Controls.Add(nudAnticipo);
            gbContrato.Controls.Add(lblPeriodosAmort);
            gbContrato.Controls.Add(nudPeriodosAmort);
            gbContrato.Controls.Add(lblDesfase);
            gbContrato.Controls.Add(nudDesfase);
            gbContrato.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            gbContrato.Location = new Point(10, 175);
            gbContrato.Name = "gbContrato";
            gbContrato.Size = new Size(276, 195);
            gbContrato.TabIndex = 1;
            gbContrato.TabStop = false;
            gbContrato.Text = "Condiciones del contrato";
            // 
            // lblAnticipo
            // 
            lblAnticipo.AutoSize = true;
            lblAnticipo.Font = new Font("Segoe UI", 9F);
            lblAnticipo.Location = new Point(10, 24);
            lblAnticipo.Name = "lblAnticipo";
            lblAnticipo.Size = new Size(161, 15);
            lblAnticipo.TabIndex = 0;
            lblAnticipo.Text = "Anticipo (% del monto total):";
            // 
            // nudAnticipo
            // 
            nudAnticipo.DecimalPlaces = 2;
            nudAnticipo.Font = new Font("Segoe UI", 9F);
            nudAnticipo.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            nudAnticipo.Location = new Point(10, 42);
            nudAnticipo.Name = "nudAnticipo";
            nudAnticipo.Size = new Size(120, 23);
            nudAnticipo.TabIndex = 1;
            nudAnticipo.Value = new decimal(new int[] { 30, 0, 0, 0 });
            // 
            // lblPeriodosAmort
            // 
            lblPeriodosAmort.AutoSize = true;
            lblPeriodosAmort.Font = new Font("Segoe UI", 9F);
            lblPeriodosAmort.Location = new Point(10, 78);
            lblPeriodosAmort.Name = "lblPeriodosAmort";
            lblPeriodosAmort.Size = new Size(262, 15);
            lblPeriodosAmort.TabIndex = 2;
            lblPeriodosAmort.Text = "Amortización = mismo % del anticipo otorgado:";
            // 
            // nudPeriodosAmort
            // 
            nudPeriodosAmort.Enabled = false;
            nudPeriodosAmort.Font = new Font("Segoe UI", 9F);
            nudPeriodosAmort.Location = new Point(10, 96);
            nudPeriodosAmort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            nudPeriodosAmort.Name = "nudPeriodosAmort";
            nudPeriodosAmort.Size = new Size(80, 23);
            nudPeriodosAmort.TabIndex = 3;
            nudPeriodosAmort.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // lblDesfase
            // 
            lblDesfase.AutoSize = true;
            lblDesfase.Font = new Font("Segoe UI", 9F);
            lblDesfase.Location = new Point(10, 132);
            lblDesfase.Name = "lblDesfase";
            lblDesfase.Size = new Size(157, 15);
            lblDesfase.TabIndex = 4;
            lblDesfase.Text = "Desfase de cobro (períodos):";
            // 
            // nudDesfase
            // 
            nudDesfase.Font = new Font("Segoe UI", 9F);
            nudDesfase.Location = new Point(10, 150);
            nudDesfase.Maximum = new decimal(new int[] { 6, 0, 0, 0 });
            nudDesfase.Name = "nudDesfase";
            nudDesfase.Size = new Size(80, 23);
            nudDesfase.TabIndex = 5;
            nudDesfase.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // gbBaseCalculo
            // 
            gbBaseCalculo.Controls.Add(rbAcumulable);
            gbBaseCalculo.Controls.Add(rbSobreCD);
            gbBaseCalculo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            gbBaseCalculo.Location = new Point(10, 378);
            gbBaseCalculo.Name = "gbBaseCalculo";
            gbBaseCalculo.Size = new Size(276, 80);
            gbBaseCalculo.TabIndex = 2;
            gbBaseCalculo.TabStop = false;
            gbBaseCalculo.Text = "Base del % financiamiento";
            // 
            // rbAcumulable
            // 
            rbAcumulable.AutoSize = true;
            rbAcumulable.Checked = true;
            rbAcumulable.Font = new Font("Segoe UI", 9F);
            rbAcumulable.Location = new Point(10, 24);
            rbAcumulable.Name = "rbAcumulable";
            rbAcumulable.Size = new Size(140, 19);
            rbAcumulable.TabIndex = 0;
            rbAcumulable.TabStop = true;
            rbAcumulable.Text = "Sobre CD + Indirectos";
            rbAcumulable.UseVisualStyleBackColor = true;
            // 
            // rbSobreCD
            // 
            rbSobreCD.AutoSize = true;
            rbSobreCD.Font = new Font("Segoe UI", 9F);
            rbSobreCD.Location = new Point(10, 49);
            rbSobreCD.Name = "rbSobreCD";
            rbSobreCD.Size = new Size(130, 19);
            rbSobreCD.TabIndex = 1;
            rbSobreCD.Text = "Sobre Costo Directo";
            rbSobreCD.UseVisualStyleBackColor = true;
            // 
            // panelResultado
            // 
            panelResultado.BackColor = Color.FromArgb(240, 248, 255);
            panelResultado.BorderStyle = BorderStyle.FixedSingle;
            panelResultado.Controls.Add(gbResultado);
            panelResultado.Dock = DockStyle.Bottom;
            panelResultado.Location = new Point(300, 598);
            panelResultado.Name = "panelResultado";
            panelResultado.Size = new Size(900, 120);
            panelResultado.TabIndex = 4;
            // 
            // gbResultado
            // 
            gbResultado.Controls.Add(lblRIntNeg);
            gbResultado.Controls.Add(lblInteresesNeg);
            gbResultado.Controls.Add(lblRIntPos);
            gbResultado.Controls.Add(lblInteresesPos);
            gbResultado.Controls.Add(lblRFinNeto);
            gbResultado.Controls.Add(lblFinanciamientoNeto);
            gbResultado.Controls.Add(lblRPorcentaje);
            gbResultado.Controls.Add(lblPorcentaje);
            gbResultado.Controls.Add(lblFechaCalculo);
            gbResultado.Dock = DockStyle.Fill;
            gbResultado.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            gbResultado.Location = new Point(0, 0);
            gbResultado.Name = "gbResultado";
            gbResultado.Size = new Size(898, 118);
            gbResultado.TabIndex = 0;
            gbResultado.TabStop = false;
            gbResultado.Text = "Resultado del cálculo";
            // 
            // lblRIntNeg
            // 
            lblRIntNeg.AutoSize = true;
            lblRIntNeg.Font = new Font("Segoe UI", 9F);
            lblRIntNeg.Location = new Point(10, 24);
            lblRIntNeg.Name = "lblRIntNeg";
            lblRIntNeg.Size = new Size(150, 15);
            lblRIntNeg.TabIndex = 0;
            lblRIntNeg.Text = "Intereses negativos (costo):";
            // 
            // lblInteresesNeg
            // 
            lblInteresesNeg.AutoSize = true;
            lblInteresesNeg.Font = new Font("Segoe UI", 9F);
            lblInteresesNeg.Location = new Point(200, 24);
            lblInteresesNeg.Name = "lblInteresesNeg";
            lblInteresesNeg.Size = new Size(34, 15);
            lblInteresesNeg.TabIndex = 1;
            lblInteresesNeg.Text = "$0.00";
            // 
            // lblRIntPos
            // 
            lblRIntPos.AutoSize = true;
            lblRIntPos.Font = new Font("Segoe UI", 9F);
            lblRIntPos.Location = new Point(10, 48);
            lblRIntPos.Name = "lblRIntPos";
            lblRIntPos.Size = new Size(153, 15);
            lblRIntPos.TabIndex = 2;
            lblRIntPos.Text = "Intereses positivos (a favor):";
            // 
            // lblInteresesPos
            // 
            lblInteresesPos.AutoSize = true;
            lblInteresesPos.Font = new Font("Segoe UI", 9F);
            lblInteresesPos.Location = new Point(200, 48);
            lblInteresesPos.Name = "lblInteresesPos";
            lblInteresesPos.Size = new Size(34, 15);
            lblInteresesPos.TabIndex = 3;
            lblInteresesPos.Text = "$0.00";
            // 
            // lblRFinNeto
            // 
            lblRFinNeto.AutoSize = true;
            lblRFinNeto.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblRFinNeto.Location = new Point(10, 72);
            lblRFinNeto.Name = "lblRFinNeto";
            lblRFinNeto.Size = new Size(123, 15);
            lblRFinNeto.TabIndex = 4;
            lblRFinNeto.Text = "Financiamiento neto:";
            // 
            // lblFinanciamientoNeto
            // 
            lblFinanciamientoNeto.AutoSize = true;
            lblFinanciamientoNeto.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblFinanciamientoNeto.ForeColor = Color.FromArgb(51, 51, 76);
            lblFinanciamientoNeto.Location = new Point(200, 72);
            lblFinanciamientoNeto.Name = "lblFinanciamientoNeto";
            lblFinanciamientoNeto.Size = new Size(38, 15);
            lblFinanciamientoNeto.TabIndex = 5;
            lblFinanciamientoNeto.Text = "$0.00";
            // 
            // lblRPorcentaje
            // 
            lblRPorcentaje.AutoSize = true;
            lblRPorcentaje.Font = new Font("Segoe UI", 9F);
            lblRPorcentaje.Location = new Point(420, 24);
            lblRPorcentaje.Name = "lblRPorcentaje";
            lblRPorcentaje.Size = new Size(105, 15);
            lblRPorcentaje.TabIndex = 6;
            lblRPorcentaje.Text = "% Financiamiento:";
            // 
            // lblPorcentaje
            // 
            lblPorcentaje.AutoSize = true;
            lblPorcentaje.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            lblPorcentaje.ForeColor = Color.FromArgb(51, 51, 76);
            lblPorcentaje.Location = new Point(420, 42);
            lblPorcentaje.Name = "lblPorcentaje";
            lblPorcentaje.Size = new Size(143, 37);
            lblPorcentaje.TabIndex = 7;
            lblPorcentaje.Text = "0.00000%";
            // 
            // lblFechaCalculo
            // 
            lblFechaCalculo.AutoSize = true;
            lblFechaCalculo.Font = new Font("Segoe UI", 8F);
            lblFechaCalculo.ForeColor = Color.Gray;
            lblFechaCalculo.Location = new Point(420, 88);
            lblFechaCalculo.Name = "lblFechaCalculo";
            lblFechaCalculo.Size = new Size(65, 13);
            lblFechaCalculo.TabIndex = 8;
            lblFechaCalculo.Text = "Sin calcular";
            // 
            // dgvFlujo
            // 
            dgvFlujo.AllowUserToAddRows = false;
            dgvFlujo.AllowUserToDeleteRows = false;
            dataGridViewCellStyle9.BackColor = Color.FromArgb(248, 249, 250);
            dgvFlujo.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle9;
            dgvFlujo.BackgroundColor = Color.White;
            dgvFlujo.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle10.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle10.BackColor = Color.FromArgb(51, 51, 76);
            dataGridViewCellStyle10.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle10.ForeColor = Color.White;
            dataGridViewCellStyle10.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle10.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle10.WrapMode = DataGridViewTriState.True;
            dgvFlujo.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle10;
            dgvFlujo.ColumnHeadersHeight = 34;
            dgvFlujo.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvFlujo.Columns.AddRange(new DataGridViewColumn[] { colPeriodo, colInicio, colFin, colDias, colCD, colCI, colEgresos, colAnticipo, colEstim, colAmort, colCobroNeto, colFlujoNeto, colSaldo, colTasa, colInteres, colDummy });
            dataGridViewCellStyle12.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle12.BackColor = SystemColors.Window;
            dataGridViewCellStyle12.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle12.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle12.SelectionBackColor = Color.FromArgb(221, 235, 247);
            dataGridViewCellStyle12.SelectionForeColor = Color.Black;
            dataGridViewCellStyle12.WrapMode = DataGridViewTriState.False;
            dgvFlujo.DefaultCellStyle = dataGridViewCellStyle12;
            dgvFlujo.Dock = DockStyle.Fill;
            dgvFlujo.EnableHeadersVisualStyles = false;
            dgvFlujo.Font = new Font("Segoe UI", 9F);
            dgvFlujo.Location = new Point(300, 98);
            dgvFlujo.Name = "dgvFlujo";
            dgvFlujo.ReadOnly = true;
            dgvFlujo.RowHeadersVisible = false;
            dgvFlujo.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFlujo.Size = new Size(900, 500);
            dgvFlujo.TabIndex = 3;
            // 
            // colPeriodo
            // 
            colPeriodo.HeaderText = "colPeriodo";
            colPeriodo.Name = "colPeriodo";
            colPeriodo.ReadOnly = true;
            // 
            // colInicio
            // 
            colInicio.HeaderText = "colInicio";
            colInicio.Name = "colInicio";
            colInicio.ReadOnly = true;
            // 
            // colFin
            // 
            colFin.HeaderText = "colFin";
            colFin.Name = "colFin";
            colFin.ReadOnly = true;
            // 
            // colDias
            // 
            colDias.HeaderText = "colDias";
            colDias.Name = "colDias";
            colDias.ReadOnly = true;
            // 
            // colCD
            // 
            colCD.HeaderText = "colCD";
            colCD.Name = "colCD";
            colCD.ReadOnly = true;
            // 
            // colCI
            // 
            colCI.HeaderText = "colCI";
            colCI.Name = "colCI";
            colCI.ReadOnly = true;
            // 
            // colEgresos
            // 
            colEgresos.HeaderText = "colEgresos";
            colEgresos.Name = "colEgresos";
            colEgresos.ReadOnly = true;
            // 
            // colAnticipo
            // 
            colAnticipo.HeaderText = "colAnticipo";
            colAnticipo.Name = "colAnticipo";
            colAnticipo.ReadOnly = true;
            // 
            // colEstim
            // 
            colEstim.HeaderText = "colEstim";
            colEstim.Name = "colEstim";
            colEstim.ReadOnly = true;
            // 
            // colAmort
            // 
            colAmort.HeaderText = "";
            colAmort.Name = "colAmort";
            colAmort.ReadOnly = true;
            // 
            // colCobroNeto
            // 
            colCobroNeto.HeaderText = "";
            colCobroNeto.Name = "colCobroNeto";
            colCobroNeto.ReadOnly = true;
            // 
            // colFlujoNeto
            // 
            colFlujoNeto.HeaderText = "";
            colFlujoNeto.Name = "colFlujoNeto";
            colFlujoNeto.ReadOnly = true;
            // 
            // colSaldo
            // 
            colSaldo.HeaderText = "";
            colSaldo.Name = "colSaldo";
            colSaldo.ReadOnly = true;
            // 
            // colTasa
            // 
            colTasa.HeaderText = "";
            colTasa.Name = "colTasa";
            colTasa.ReadOnly = true;
            // 
            // colInteres
            // 
            colInteres.HeaderText = "";
            colInteres.Name = "colInteres";
            colInteres.ReadOnly = true;
            // 
            // colDummy
            // 
            colDummy.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dataGridViewCellStyle11.BackColor = Color.White;
            colDummy.DefaultCellStyle = dataGridViewCellStyle11;
            colDummy.HeaderText = "";
            colDummy.Name = "colDummy";
            colDummy.ReadOnly = true;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip.Location = new Point(0, 718);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1200, 22);
            statusStrip.TabIndex = 5;
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(35, 17);
            lblStatus.Text = "Listo.";
            // 
            // FormFinanciamiento
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 740);
            Controls.Add(dgvFlujo);
            Controls.Add(panelResultado);
            Controls.Add(panelParams);
            Controls.Add(panelToolbar);
            Controls.Add(panelTop);
            Controls.Add(statusStrip);
            MinimumSize = new Size(1000, 650);
            Name = "FormFinanciamiento";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Financiamiento — Cálculo RLOPSRM";
            Load += FormFinanciamiento_Load;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelToolbar.ResumeLayout(false);
            panelToolbar.PerformLayout();
            panelParams.ResumeLayout(false);
            gbTasas.ResumeLayout(false);
            gbTasas.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudTIIE).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudPuntos).EndInit();
            gbContrato.ResumeLayout(false);
            gbContrato.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudAnticipo).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudPeriodosAmort).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudDesfase).EndInit();
            gbBaseCalculo.ResumeLayout(false);
            gbBaseCalculo.PerformLayout();
            panelResultado.ResumeLayout(false);
            gbResultado.ResumeLayout(false);
            gbResultado.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFlujo).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridViewTextBoxColumn colPeriodo;
        private DataGridViewTextBoxColumn colInicio;
        private DataGridViewTextBoxColumn colFin;
        private DataGridViewTextBoxColumn colDias;
        private DataGridViewTextBoxColumn colCD;
        private DataGridViewTextBoxColumn colCI;
        private DataGridViewTextBoxColumn colEgresos;
        private DataGridViewTextBoxColumn colAnticipo;
        private DataGridViewTextBoxColumn colEstim;
        private DataGridViewTextBoxColumn colAmort;
        private DataGridViewTextBoxColumn colCobroNeto;
        private DataGridViewTextBoxColumn colFlujoNeto;
        private DataGridViewTextBoxColumn colSaldo;
        private DataGridViewTextBoxColumn colTasa;
        private DataGridViewTextBoxColumn colInteres;
        private DataGridViewTextBoxColumn colDummy;
    }
}
