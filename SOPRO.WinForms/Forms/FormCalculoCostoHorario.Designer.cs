namespace SOPRO.WinForms.Forms
{
    partial class FormCalculoCostoHorario
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
            this.components = new System.ComponentModel.Container();

            this.panelTop            = new System.Windows.Forms.Panel();
            this.lblTitulo           = new System.Windows.Forms.Label();
            this.panelInfo           = new System.Windows.Forms.Panel();
            this.lblClaveVal         = new System.Windows.Forms.Label();
            this.lblDescVal          = new System.Windows.Forms.Label();
            this.lblPotenciaVal      = new System.Windows.Forms.Label();
            this.lblCombVal          = new System.Windows.Forms.Label();
            this.label1              = new System.Windows.Forms.Label();
            this.label2              = new System.Windows.Forms.Label();
            this.label3              = new System.Windows.Forms.Label();
            this.label4              = new System.Windows.Forms.Label();
            this.tabControl          = new System.Windows.Forms.TabControl();
            this.tabResumen          = new System.Windows.Forms.TabPage();
            this.tabCargosFijos      = new System.Windows.Forms.TabPage();
            this.tabConsumos         = new System.Windows.Forms.TabPage();
            this.tabOperacion        = new System.Windows.Forms.TabPage();
            this.panelBottom         = new System.Windows.Forms.Panel();
            this.lblTextoTotal       = new System.Windows.Forms.Label();
            this.lblCostoTotal       = new System.Windows.Forms.Label();
            this.btnRestaurarDefecto = new System.Windows.Forms.Button();
            this.btnCancelar         = new System.Windows.Forms.Button();
            this.btnGuardar          = new System.Windows.Forms.Button();

            // Tab A controls
            this.grpDepreciacion     = new System.Windows.Forms.GroupBox();
            this.label5              = new System.Windows.Forms.Label();
            this.nudValorAdquisicion = new System.Windows.Forms.NumericUpDown();
            this.label6              = new System.Windows.Forms.Label();
            this.nudValorLlantas     = new System.Windows.Forms.NumericUpDown();
            this.label7              = new System.Windows.Forms.Label();
            this.nudValorPiezasEsp   = new System.Windows.Forms.NumericUpDown();
            this.label8              = new System.Windows.Forms.Label();
            this.nudFactorRescate    = new System.Windows.Forms.NumericUpDown();
            this.label9              = new System.Windows.Forms.Label();
            this.nudVidaEconomica    = new System.Windows.Forms.NumericUpDown();
            this.lblVmTxt            = new System.Windows.Forms.Label();
            this.lblVmVal            = new System.Windows.Forms.Label();
            this.lblVrTxt            = new System.Windows.Forms.Label();
            this.lblVrVal            = new System.Windows.Forms.Label();
            this.lblDepreciacionTxt  = new System.Windows.Forms.Label();
            this.lblDepreciacion     = new System.Windows.Forms.Label();
            this.grpInversion        = new System.Windows.Forms.GroupBox();
            this.label10             = new System.Windows.Forms.Label();
            this.nudTasaInteres      = new System.Windows.Forms.NumericUpDown();
            this.label11             = new System.Windows.Forms.Label();
            this.nudHorasAnio        = new System.Windows.Forms.NumericUpDown();
            this.lblVmVrMedioTxt     = new System.Windows.Forms.Label();
            this.lblVmVrMedioVal     = new System.Windows.Forms.Label();
            this.lblInversionTxt     = new System.Windows.Forms.Label();
            this.lblInversion        = new System.Windows.Forms.Label();
            this.grpSeguros          = new System.Windows.Forms.GroupBox();
            this.label12             = new System.Windows.Forms.Label();
            this.nudPrimaSeguro      = new System.Windows.Forms.NumericUpDown();
            this.lblSegurosTxt       = new System.Windows.Forms.Label();
            this.lblSeguros          = new System.Windows.Forms.Label();
            this.grpMantenimiento    = new System.Windows.Forms.GroupBox();
            this.label13             = new System.Windows.Forms.Label();
            this.nudFactorManten     = new System.Windows.Forms.NumericUpDown();
            this.lblMantenimientoTxt = new System.Windows.Forms.Label();
            this.lblMantenimiento    = new System.Windows.Forms.Label();
            this.grpTotalCF          = new System.Windows.Forms.GroupBox();
            this.lblTotalCargosFijos = new System.Windows.Forms.Label();

            // Tab B controls
            this.grpCombustible      = new System.Windows.Forms.GroupBox();
            this.label14             = new System.Windows.Forms.Label();
            this.nudCantCombustible  = new System.Windows.Forms.NumericUpDown();
            this.label15             = new System.Windows.Forms.Label();
            this.nudPrecioCombustible= new System.Windows.Forms.NumericUpDown();
            this.btnEstimarCombustible= new System.Windows.Forms.Button();
            this.lblCombustibleTxt   = new System.Windows.Forms.Label();
            this.lblCombustible      = new System.Windows.Forms.Label();
            this.grpLubricantes      = new System.Windows.Forms.GroupBox();
            this.label16             = new System.Windows.Forms.Label();
            this.nudCantAceite       = new System.Windows.Forms.NumericUpDown();
            this.label17             = new System.Windows.Forms.Label();
            this.nudPrecioAceite     = new System.Windows.Forms.NumericUpDown();
            this.lblLubricantesTxt   = new System.Windows.Forms.Label();
            this.lblLubricantes      = new System.Windows.Forms.Label();
            this.grpLlantas          = new System.Windows.Forms.GroupBox();
            this.label18             = new System.Windows.Forms.Label();
            this.nudNumLlantas       = new System.Windows.Forms.NumericUpDown();
            this.label19             = new System.Windows.Forms.Label();
            this.nudVidaLlantas      = new System.Windows.Forms.NumericUpDown();
            this.lblLlantasTxt       = new System.Windows.Forms.Label();
            this.lblLlantas          = new System.Windows.Forms.Label();
            this.grpPiezasEsp        = new System.Windows.Forms.GroupBox();
            this.label20             = new System.Windows.Forms.Label();
            this.nudVidaPiezasEsp    = new System.Windows.Forms.NumericUpDown();
            this.lblPiezasEspTxt     = new System.Windows.Forms.Label();
            this.lblPiezasEsp        = new System.Windows.Forms.Label();
            this.grpTotalCon         = new System.Windows.Forms.GroupBox();
            this.lblTotalConsumos    = new System.Windows.Forms.Label();

            // Tab C controls
            this.grpOperacion        = new System.Windows.Forms.GroupBox();
            this.label21             = new System.Windows.Forms.Label();
            this.nudSalarioOperador  = new System.Windows.Forms.NumericUpDown();
            this.label22             = new System.Windows.Forms.Label();
            this.nudFSR              = new System.Windows.Forms.NumericUpDown();
            this.label23             = new System.Windows.Forms.Label();
            this.nudHorasTurno       = new System.Windows.Forms.NumericUpDown();
            this.lblSalarioRealTxt   = new System.Windows.Forms.Label();
            this.lblSalarioReal      = new System.Windows.Forms.Label();
            this.lblOperacionTxt     = new System.Windows.Forms.Label();
            this.lblOperacion        = new System.Windows.Forms.Label();
            this.lblNotaOperacion    = new System.Windows.Forms.Label();

            // Tab D controls
            this.panelResumen        = new System.Windows.Forms.Panel();
            this.lblResFormula       = new System.Windows.Forms.Label();
            this.lblResA             = new System.Windows.Forms.Label();
            this.lblResADep          = new System.Windows.Forms.Label();
            this.lblResADepVal       = new System.Windows.Forms.Label();
            this.lblResAInv          = new System.Windows.Forms.Label();
            this.lblResAInvVal       = new System.Windows.Forms.Label();
            this.lblResASeg          = new System.Windows.Forms.Label();
            this.lblResASegVal       = new System.Windows.Forms.Label();
            this.lblResAMnt          = new System.Windows.Forms.Label();
            this.lblResAMntVal       = new System.Windows.Forms.Label();
            this.lblResATot          = new System.Windows.Forms.Label();
            this.lblResATotVal       = new System.Windows.Forms.Label();
            this.lblResB             = new System.Windows.Forms.Label();
            this.lblResBCom          = new System.Windows.Forms.Label();
            this.lblResBComVal       = new System.Windows.Forms.Label();
            this.lblResBLub          = new System.Windows.Forms.Label();
            this.lblResBLubVal       = new System.Windows.Forms.Label();
            this.lblResBLla          = new System.Windows.Forms.Label();
            this.lblResBLlaVal       = new System.Windows.Forms.Label();
            this.lblResBPie          = new System.Windows.Forms.Label();
            this.lblResBPieVal       = new System.Windows.Forms.Label();
            this.lblResBTot          = new System.Windows.Forms.Label();
            this.lblResBTotVal       = new System.Windows.Forms.Label();
            this.lblResC             = new System.Windows.Forms.Label();
            this.lblResCOpe          = new System.Windows.Forms.Label();
            this.lblResCOpeVal       = new System.Windows.Forms.Label();
            this.lblResTotalTxt      = new System.Windows.Forms.Label();
            this.lblResTotalVal      = new System.Windows.Forms.Label();
            this.lblResSep           = new System.Windows.Forms.Label();

            this.SuspendLayout();

            // ──────────────────────────────────────────────────────────────────
            // PANEL TOP
            // ──────────────────────────────────────────────────────────────────
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(230, 81, 0);
            this.panelTop.Dock      = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Height    = 54;
            this.lblTitulo.Text     = "🧮  Cálculo de Costo Horario — Método RLOPSRM";
            this.lblTitulo.Font     = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblTitulo.ForeColor= System.Drawing.Color.White;
            this.lblTitulo.Location = new System.Drawing.Point(16, 13);
            this.lblTitulo.AutoSize = true;
            this.panelTop.Controls.Add(this.lblTitulo);

            // ──────────────────────────────────────────────────────────────────
            // PANEL INFO
            // ──────────────────────────────────────────────────────────────────
            this.panelInfo.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
            this.panelInfo.Dock      = System.Windows.Forms.DockStyle.Top;
            this.panelInfo.Height    = 52;

            this.label1.Text = "Clave:";       this.label1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold); this.label1.Location = new System.Drawing.Point(16, 9);  this.label1.AutoSize = true;
            this.label2.Text = "Equipo:";      this.label2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold); this.label2.Location = new System.Drawing.Point(16, 30); this.label2.AutoSize = true;
            this.label3.Text = "Potencia:";    this.label3.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold); this.label3.Location = new System.Drawing.Point(450, 9);  this.label3.AutoSize = true;
            this.label4.Text = "Combustible:"; this.label4.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold); this.label4.Location = new System.Drawing.Point(450, 30); this.label4.AutoSize = true;

            this.lblClaveVal.Text    = ""; this.lblClaveVal.Location  = new System.Drawing.Point(80,  9);  this.lblClaveVal.AutoSize  = true; this.lblClaveVal.Font  = new System.Drawing.Font("Segoe UI", 9F);
            this.lblDescVal.Text     = ""; this.lblDescVal.Location   = new System.Drawing.Point(80,  30); this.lblDescVal.AutoSize   = true; this.lblDescVal.Font   = new System.Drawing.Font("Segoe UI", 9F);
            this.lblPotenciaVal.Text = ""; this.lblPotenciaVal.Location= new System.Drawing.Point(540, 9);  this.lblPotenciaVal.AutoSize= true; this.lblPotenciaVal.Font= new System.Drawing.Font("Segoe UI", 9F);
            this.lblCombVal.Text     = ""; this.lblCombVal.Location   = new System.Drawing.Point(540, 30); this.lblCombVal.AutoSize   = true; this.lblCombVal.Font   = new System.Drawing.Font("Segoe UI", 9F);

            this.panelInfo.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.label1, this.label2, this.label3, this.label4,
              this.lblClaveVal, this.lblDescVal, this.lblPotenciaVal, this.lblCombVal });

            // ──────────────────────────────────────────────────────────────────
            // PANEL BOTTOM
            // ──────────────────────────────────────────────────────────────────
            this.panelBottom.BackColor = System.Drawing.Color.FromArgb(238, 238, 238);
            this.panelBottom.Dock      = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Height    = 62;

            this.lblTextoTotal.Text     = "COSTO HORARIO TOTAL:";
            this.lblTextoTotal.Font     = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblTextoTotal.Location = new System.Drawing.Point(16, 20);
            this.lblTextoTotal.AutoSize = true;

            this.lblCostoTotal.Text      = "$0.00 / hr";
            this.lblCostoTotal.Font      = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblCostoTotal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);
            this.lblCostoTotal.Location  = new System.Drawing.Point(215, 10);
            this.lblCostoTotal.AutoSize  = true;

            this.btnRestaurarDefecto.Text      = "↺ Restaurar defectos";
            this.btnRestaurarDefecto.Location  = new System.Drawing.Point(595, 13);
            this.btnRestaurarDefecto.Size      = new System.Drawing.Size(160, 36);
            this.btnRestaurarDefecto.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRestaurarDefecto.BackColor = System.Drawing.Color.FromArgb(210, 210, 210);
            this.btnRestaurarDefecto.Click    += new System.EventHandler(this.btnRestaurarDefecto_Click);

            this.btnCancelar.Text      = "Cancelar";
            this.btnCancelar.Location  = new System.Drawing.Point(775, 13);
            this.btnCancelar.Size      = new System.Drawing.Size(100, 36);
            this.btnCancelar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancelar.BackColor = System.Drawing.Color.FromArgb(210, 210, 210);
            this.btnCancelar.Click    += new System.EventHandler(this.btnCancelar_Click);

            this.btnGuardar.Text      = "💾  Guardar";
            this.btnGuardar.Location  = new System.Drawing.Point(890, 13);
            this.btnGuardar.Size      = new System.Drawing.Size(150, 36);
            this.btnGuardar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnGuardar.BackColor = System.Drawing.Color.FromArgb(56, 142, 60);
            this.btnGuardar.ForeColor = System.Drawing.Color.White;
            this.btnGuardar.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnGuardar.Click    += new System.EventHandler(this.btnGuardar_Click);

            this.panelBottom.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.lblTextoTotal, this.lblCostoTotal,
              this.btnRestaurarDefecto, this.btnCancelar, this.btnGuardar });

            // ══════════════════════════════════════════════════════════════════
            // TAB A — CARGOS FIJOS
            // ══════════════════════════════════════════════════════════════════

            // -- grpDepreciacion --
            this.grpDepreciacion.Text     = "A1 — Depreciación";
            this.grpDepreciacion.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpDepreciacion.Location = new System.Drawing.Point(12, 10);
            this.grpDepreciacion.Size     = new System.Drawing.Size(440, 220);

            this.label5.Text = "Valor adquisición Pm ($):"; this.label5.Location = new System.Drawing.Point(10, 28); this.label5.Size = new System.Drawing.Size(210, 20); this.label5.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudValorAdquisicion.Location = new System.Drawing.Point(225, 25); this.nudValorAdquisicion.Size = new System.Drawing.Size(190, 24);
            this.nudValorAdquisicion.Minimum = 0; this.nudValorAdquisicion.Maximum = 99999999; this.nudValorAdquisicion.DecimalPlaces = 2; this.nudValorAdquisicion.ThousandsSeparator = true; this.nudValorAdquisicion.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label6.Text = "Valor de llantas Pn ($):"; this.label6.Location = new System.Drawing.Point(10, 58); this.label6.Size = new System.Drawing.Size(210, 20); this.label6.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudValorLlantas.Location = new System.Drawing.Point(225, 55); this.nudValorLlantas.Size = new System.Drawing.Size(190, 24);
            this.nudValorLlantas.Minimum = 0; this.nudValorLlantas.Maximum = 9999999; this.nudValorLlantas.DecimalPlaces = 2; this.nudValorLlantas.ThousandsSeparator = true; this.nudValorLlantas.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label7.Text = "Valor piezas especiales Pa ($):"; this.label7.Location = new System.Drawing.Point(10, 88); this.label7.Size = new System.Drawing.Size(210, 20); this.label7.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudValorPiezasEsp.Location = new System.Drawing.Point(225, 85); this.nudValorPiezasEsp.Size = new System.Drawing.Size(190, 24);
            this.nudValorPiezasEsp.Minimum = 0; this.nudValorPiezasEsp.Maximum = 9999999; this.nudValorPiezasEsp.DecimalPlaces = 2; this.nudValorPiezasEsp.ThousandsSeparator = true; this.nudValorPiezasEsp.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label8.Text = "Factor de rescate r:"; this.label8.Location = new System.Drawing.Point(10, 118); this.label8.Size = new System.Drawing.Size(210, 20); this.label8.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudFactorRescate.Location = new System.Drawing.Point(225, 115); this.nudFactorRescate.Size = new System.Drawing.Size(190, 24);
            this.nudFactorRescate.Minimum = 0; this.nudFactorRescate.Maximum = 1; this.nudFactorRescate.DecimalPlaces = 4; this.nudFactorRescate.Value = 0.10m; this.nudFactorRescate.Increment = 0.01m; this.nudFactorRescate.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label9.Text = "Vida económica Ve (horas):"; this.label9.Location = new System.Drawing.Point(10, 148); this.label9.Size = new System.Drawing.Size(210, 20); this.label9.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudVidaEconomica.Location = new System.Drawing.Point(225, 145); this.nudVidaEconomica.Size = new System.Drawing.Size(190, 24);
            this.nudVidaEconomica.Minimum = 1; this.nudVidaEconomica.Maximum = 999999; this.nudVidaEconomica.DecimalPlaces = 0; this.nudVidaEconomica.Value = 10000; this.nudVidaEconomica.ThousandsSeparator = true; this.nudVidaEconomica.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.lblVmTxt.Text = "Vm = Pm − Pn − Pa:"; this.lblVmTxt.Location = new System.Drawing.Point(10, 178); this.lblVmTxt.Size = new System.Drawing.Size(210, 20); this.lblVmTxt.Font = new System.Drawing.Font("Segoe UI", 9F); this.lblVmTxt.ForeColor = System.Drawing.Color.Gray;
            this.lblVmVal.Text = "$0.00"; this.lblVmVal.Location = new System.Drawing.Point(225, 178); this.lblVmVal.AutoSize = true; this.lblVmVal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            this.lblVrTxt.Text = "Vr = Vm × r:"; this.lblVrTxt.Location = new System.Drawing.Point(10, 198); this.lblVrTxt.Size = new System.Drawing.Size(210, 20); this.lblVrTxt.Font = new System.Drawing.Font("Segoe UI", 9F); this.lblVrTxt.ForeColor = System.Drawing.Color.Gray;
            this.lblVrVal.Text = "$0.00"; this.lblVrVal.Location = new System.Drawing.Point(225, 198); this.lblVrVal.AutoSize = true; this.lblVrVal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            this.grpDepreciacion.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.label5, this.nudValorAdquisicion, this.label6, this.nudValorLlantas,
              this.label7, this.nudValorPiezasEsp,   this.label8, this.nudFactorRescate,
              this.label9, this.nudVidaEconomica,
              this.lblVmTxt, this.lblVmVal, this.lblVrTxt, this.lblVrVal });

            this.lblDepreciacionTxt.Text = "D — Depreciación ($/hr):"; this.lblDepreciacionTxt.Location = new System.Drawing.Point(470, 54); this.lblDepreciacionTxt.Size = new System.Drawing.Size(220, 20); this.lblDepreciacionTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblDepreciacion.Text    = "$0.00";  this.lblDepreciacion.Location = new System.Drawing.Point(470, 76); this.lblDepreciacion.AutoSize = true; this.lblDepreciacion.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold); this.lblDepreciacion.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            // -- grpInversion --
            this.grpInversion.Text     = "A2 — Inversión";
            this.grpInversion.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpInversion.Location = new System.Drawing.Point(12, 238);
            this.grpInversion.Size     = new System.Drawing.Size(440, 110);

            this.label10.Text = "Tasa de interés i (% anual):"; this.label10.Location = new System.Drawing.Point(10, 28); this.label10.Size = new System.Drawing.Size(210, 20); this.label10.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudTasaInteres.Location = new System.Drawing.Point(225, 25); this.nudTasaInteres.Size = new System.Drawing.Size(190, 24);
            this.nudTasaInteres.Minimum = 0; this.nudTasaInteres.Maximum = 100; this.nudTasaInteres.DecimalPlaces = 4; this.nudTasaInteres.Value = 21.24m; this.nudTasaInteres.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label11.Text = "Horas efectivas/año Hea:"; this.label11.Location = new System.Drawing.Point(10, 58); this.label11.Size = new System.Drawing.Size(210, 20); this.label11.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudHorasAnio.Location = new System.Drawing.Point(225, 55); this.nudHorasAnio.Size = new System.Drawing.Size(190, 24);
            this.nudHorasAnio.Minimum = 1; this.nudHorasAnio.Maximum = 5000; this.nudHorasAnio.DecimalPlaces = 0; this.nudHorasAnio.Value = 1600; this.nudHorasAnio.ThousandsSeparator = true; this.nudHorasAnio.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.lblVmVrMedioTxt.Text = "(Vm+Vr)/2 — base i y s:"; this.lblVmVrMedioTxt.Location = new System.Drawing.Point(10, 83); this.lblVmVrMedioTxt.Size = new System.Drawing.Size(210, 20); this.lblVmVrMedioTxt.Font = new System.Drawing.Font("Segoe UI", 9F); this.lblVmVrMedioTxt.ForeColor = System.Drawing.Color.Gray;
            this.lblVmVrMedioVal.Text = "$0.00"; this.lblVmVrMedioVal.Location = new System.Drawing.Point(225, 83); this.lblVmVrMedioVal.AutoSize = true; this.lblVmVrMedioVal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            this.grpInversion.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.label10, this.nudTasaInteres, this.label11, this.nudHorasAnio,
              this.lblVmVrMedioTxt, this.lblVmVrMedioVal });

            this.lblInversionTxt.Text = "Im — Inversión ($/hr):"; this.lblInversionTxt.Location = new System.Drawing.Point(470, 248); this.lblInversionTxt.Size = new System.Drawing.Size(220, 20); this.lblInversionTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblInversion.Text    = "$0.00"; this.lblInversion.Location = new System.Drawing.Point(470, 270); this.lblInversion.AutoSize = true; this.lblInversion.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold); this.lblInversion.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            // -- grpSeguros --
            this.grpSeguros.Text     = "A3 — Seguros";
            this.grpSeguros.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpSeguros.Location = new System.Drawing.Point(12, 356);
            this.grpSeguros.Size     = new System.Drawing.Size(440, 62);

            this.label12.Text = "Prima anual s (% anual):"; this.label12.Location = new System.Drawing.Point(10, 28); this.label12.Size = new System.Drawing.Size(210, 20); this.label12.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudPrimaSeguro.Location = new System.Drawing.Point(225, 25); this.nudPrimaSeguro.Size = new System.Drawing.Size(190, 24);
            this.nudPrimaSeguro.Minimum = 0; this.nudPrimaSeguro.Maximum = 100; this.nudPrimaSeguro.DecimalPlaces = 4; this.nudPrimaSeguro.Value = 3.00m; this.nudPrimaSeguro.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.grpSeguros.Controls.AddRange(new System.Windows.Forms.Control[] { this.label12, this.nudPrimaSeguro });

            this.lblSegurosTxt.Text = "Sm — Seguros ($/hr):"; this.lblSegurosTxt.Location = new System.Drawing.Point(470, 365); this.lblSegurosTxt.Size = new System.Drawing.Size(220, 20); this.lblSegurosTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblSeguros.Text    = "$0.00"; this.lblSeguros.Location = new System.Drawing.Point(470, 387); this.lblSeguros.AutoSize = true; this.lblSeguros.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold); this.lblSeguros.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            // -- grpMantenimiento --
            this.grpMantenimiento.Text     = "A4 — Mantenimiento";
            this.grpMantenimiento.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpMantenimiento.Location = new System.Drawing.Point(12, 426);
            this.grpMantenimiento.Size     = new System.Drawing.Size(440, 62);

            this.label13.Text = "Coef. mantenimiento Ko:"; this.label13.Location = new System.Drawing.Point(10, 28); this.label13.Size = new System.Drawing.Size(210, 20); this.label13.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudFactorManten.Location = new System.Drawing.Point(225, 25); this.nudFactorManten.Size = new System.Drawing.Size(190, 24);
            this.nudFactorManten.Minimum = 0; this.nudFactorManten.Maximum = 5; this.nudFactorManten.DecimalPlaces = 4; this.nudFactorManten.Value = 0.20m; this.nudFactorManten.Increment = 0.01m; this.nudFactorManten.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.grpMantenimiento.Controls.AddRange(new System.Windows.Forms.Control[] { this.label13, this.nudFactorManten });

            this.lblMantenimientoTxt.Text = "Mn = Ko × D ($/hr):"; this.lblMantenimientoTxt.Location = new System.Drawing.Point(470, 435); this.lblMantenimientoTxt.Size = new System.Drawing.Size(220, 20); this.lblMantenimientoTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblMantenimiento.Text    = "$0.00"; this.lblMantenimiento.Location = new System.Drawing.Point(470, 457); this.lblMantenimiento.AutoSize = true; this.lblMantenimiento.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold); this.lblMantenimiento.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            // -- grpTotalCF --
            this.grpTotalCF.Text     = "TOTAL CARGOS FIJOS  (A = D + Im + Sm + Mn)";
            this.grpTotalCF.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpTotalCF.ForeColor= System.Drawing.Color.FromArgb(191, 54, 12);
            this.grpTotalCF.Location = new System.Drawing.Point(12, 496);
            this.grpTotalCF.Size     = new System.Drawing.Size(1040, 50);
            this.lblTotalCargosFijos.Text      = "$0.00 / hr";
            this.lblTotalCargosFijos.Font      = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblTotalCargosFijos.ForeColor = System.Drawing.Color.FromArgb(191, 54, 12);
            this.lblTotalCargosFijos.Location  = new System.Drawing.Point(760, 14);
            this.lblTotalCargosFijos.AutoSize  = true;
            this.grpTotalCF.Controls.Add(this.lblTotalCargosFijos);

            this.tabCargosFijos.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                this.grpDepreciacion, this.lblDepreciacionTxt, this.lblDepreciacion,
                this.grpInversion,    this.lblInversionTxt,    this.lblInversion,
                this.grpSeguros,      this.lblSegurosTxt,      this.lblSeguros,
                this.grpMantenimiento,this.lblMantenimientoTxt,this.lblMantenimiento,
                this.grpTotalCF
            });
            this.tabCargosFijos.Text      = "A — Cargos Fijos";
            this.tabCargosFijos.Padding   = new System.Windows.Forms.Padding(6);
            this.tabCargosFijos.AutoScroll= true;

            // ══════════════════════════════════════════════════════════════════
            // TAB B — CONSUMOS
            // ══════════════════════════════════════════════════════════════════

            this.grpCombustible.Text     = "B1 — Combustible";
            this.grpCombustible.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpCombustible.Location = new System.Drawing.Point(12, 10);
            this.grpCombustible.Size     = new System.Drawing.Size(440, 105);

            this.label14.Text = "Cantidad Gh (lts/hr):"; this.label14.Location = new System.Drawing.Point(10, 28); this.label14.Size = new System.Drawing.Size(210, 20); this.label14.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudCantCombustible.Location = new System.Drawing.Point(225, 25); this.nudCantCombustible.Size = new System.Drawing.Size(150, 24);
            this.nudCantCombustible.Minimum = 0; this.nudCantCombustible.Maximum = 200; this.nudCantCombustible.DecimalPlaces = 4; this.nudCantCombustible.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.btnEstimarCombustible.Text      = "Estimar↗";
            this.btnEstimarCombustible.Location  = new System.Drawing.Point(381, 24);
            this.btnEstimarCombustible.Size      = new System.Drawing.Size(52, 24);
            this.btnEstimarCombustible.Font      = new System.Drawing.Font("Segoe UI", 7.5F);
            this.btnEstimarCombustible.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEstimarCombustible.BackColor = System.Drawing.Color.FromArgb(33, 150, 243);
            this.btnEstimarCombustible.ForeColor = System.Drawing.Color.White;
            this.btnEstimarCombustible.Click    += new System.EventHandler(this.btnEstimarCombustible_Click);

            this.label15.Text = "Precio Pac ($/lt):"; this.label15.Location = new System.Drawing.Point(10, 62); this.label15.Size = new System.Drawing.Size(210, 20); this.label15.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudPrecioCombustible.Location = new System.Drawing.Point(225, 59); this.nudPrecioCombustible.Size = new System.Drawing.Size(190, 24);
            this.nudPrecioCombustible.Minimum = 0; this.nudPrecioCombustible.Maximum = 9999; this.nudPrecioCombustible.DecimalPlaces = 4; this.nudPrecioCombustible.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.grpCombustible.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.label14, this.nudCantCombustible, this.btnEstimarCombustible, this.label15, this.nudPrecioCombustible });

            this.lblCombustibleTxt.Text = "Co = Gh × Pac ($/hr):"; this.lblCombustibleTxt.Location = new System.Drawing.Point(470, 22); this.lblCombustibleTxt.Size = new System.Drawing.Size(220, 20); this.lblCombustibleTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblCombustible.Text    = "$0.00"; this.lblCombustible.Location = new System.Drawing.Point(470, 44); this.lblCombustible.AutoSize = true; this.lblCombustible.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold); this.lblCombustible.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.grpLubricantes.Text     = "B2 — Lubricantes";
            this.grpLubricantes.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpLubricantes.Location = new System.Drawing.Point(12, 123);
            this.grpLubricantes.Size     = new System.Drawing.Size(440, 95);

            this.label16.Text = "Cantidad Ah (lts/hr):"; this.label16.Location = new System.Drawing.Point(10, 28); this.label16.Size = new System.Drawing.Size(210, 20); this.label16.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudCantAceite.Location = new System.Drawing.Point(225, 25); this.nudCantAceite.Size = new System.Drawing.Size(190, 24);
            this.nudCantAceite.Minimum = 0; this.nudCantAceite.Maximum = 50; this.nudCantAceite.DecimalPlaces = 4; this.nudCantAceite.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label17.Text = "Precio ($/lt):"; this.label17.Location = new System.Drawing.Point(10, 60); this.label17.Size = new System.Drawing.Size(210, 20); this.label17.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudPrecioAceite.Location = new System.Drawing.Point(225, 57); this.nudPrecioAceite.Size = new System.Drawing.Size(190, 24);
            this.nudPrecioAceite.Minimum = 0; this.nudPrecioAceite.Maximum = 9999; this.nudPrecioAceite.DecimalPlaces = 4; this.nudPrecioAceite.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.grpLubricantes.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.label16, this.nudCantAceite, this.label17, this.nudPrecioAceite });

            this.lblLubricantesTxt.Text = "Lb = Ah × Precio ($/hr):"; this.lblLubricantesTxt.Location = new System.Drawing.Point(470, 133); this.lblLubricantesTxt.Size = new System.Drawing.Size(220, 20); this.lblLubricantesTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblLubricantes.Text    = "$0.00"; this.lblLubricantes.Location = new System.Drawing.Point(470, 155); this.lblLubricantes.AutoSize = true; this.lblLubricantes.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold); this.lblLubricantes.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.grpLlantas.Text     = "B3 — Llantas";
            this.grpLlantas.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpLlantas.Location = new System.Drawing.Point(12, 226);
            this.grpLlantas.Size     = new System.Drawing.Size(440, 95);

            this.label18.Text = "Número de llantas:"; this.label18.Location = new System.Drawing.Point(10, 28); this.label18.Size = new System.Drawing.Size(210, 20); this.label18.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudNumLlantas.Location = new System.Drawing.Point(225, 25); this.nudNumLlantas.Size = new System.Drawing.Size(190, 24);
            this.nudNumLlantas.Minimum = 0; this.nudNumLlantas.Maximum = 30; this.nudNumLlantas.DecimalPlaces = 0; this.nudNumLlantas.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label19.Text = "Vida económica llantas (hrs):"; this.label19.Location = new System.Drawing.Point(10, 60); this.label19.Size = new System.Drawing.Size(210, 20); this.label19.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudVidaLlantas.Location = new System.Drawing.Point(225, 57); this.nudVidaLlantas.Size = new System.Drawing.Size(190, 24);
            this.nudVidaLlantas.Minimum = 0; this.nudVidaLlantas.Maximum = 99999; this.nudVidaLlantas.DecimalPlaces = 0; this.nudVidaLlantas.Value = 5000; this.nudVidaLlantas.ThousandsSeparator = true; this.nudVidaLlantas.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.grpLlantas.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.label18, this.nudNumLlantas, this.label19, this.nudVidaLlantas });

            this.lblLlantasTxt.Text = "Nt = ValLlantas / VidaLlantas ($/hr):"; this.lblLlantasTxt.Location = new System.Drawing.Point(470, 238); this.lblLlantasTxt.Size = new System.Drawing.Size(280, 20); this.lblLlantasTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblLlantas.Text    = "$0.00"; this.lblLlantas.Location = new System.Drawing.Point(470, 260); this.lblLlantas.AutoSize = true; this.lblLlantas.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold); this.lblLlantas.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.grpPiezasEsp.Text     = "B4 — Piezas Especiales";
            this.grpPiezasEsp.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpPiezasEsp.Location = new System.Drawing.Point(12, 329);
            this.grpPiezasEsp.Size     = new System.Drawing.Size(440, 62);

            this.label20.Text = "Vida económica piezas (hrs):"; this.label20.Location = new System.Drawing.Point(10, 28); this.label20.Size = new System.Drawing.Size(210, 20); this.label20.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudVidaPiezasEsp.Location = new System.Drawing.Point(225, 25); this.nudVidaPiezasEsp.Size = new System.Drawing.Size(190, 24);
            this.nudVidaPiezasEsp.Minimum = 0; this.nudVidaPiezasEsp.Maximum = 99999; this.nudVidaPiezasEsp.DecimalPlaces = 0; this.nudVidaPiezasEsp.Value = 5000; this.nudVidaPiezasEsp.ThousandsSeparator = true; this.nudVidaPiezasEsp.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.grpPiezasEsp.Controls.AddRange(new System.Windows.Forms.Control[] { this.label20, this.nudVidaPiezasEsp });

            this.lblPiezasEspTxt.Text = "Ae = ValPiezas / VidaPiezas ($/hr):"; this.lblPiezasEspTxt.Location = new System.Drawing.Point(470, 341); this.lblPiezasEspTxt.Size = new System.Drawing.Size(280, 20); this.lblPiezasEspTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblPiezasEsp.Text    = "$0.00"; this.lblPiezasEsp.Location = new System.Drawing.Point(470, 363); this.lblPiezasEsp.AutoSize = true; this.lblPiezasEsp.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold); this.lblPiezasEsp.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.grpTotalCon.Text     = "TOTAL CONSUMOS  (B = Co + Lb + Nt + Ae)";
            this.grpTotalCon.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpTotalCon.ForeColor= System.Drawing.Color.FromArgb(191, 54, 12);
            this.grpTotalCon.Location = new System.Drawing.Point(12, 399);
            this.grpTotalCon.Size     = new System.Drawing.Size(1040, 50);
            this.lblTotalConsumos.Text      = "$0.00 / hr";
            this.lblTotalConsumos.Font      = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblTotalConsumos.ForeColor = System.Drawing.Color.FromArgb(191, 54, 12);
            this.lblTotalConsumos.Location  = new System.Drawing.Point(760, 14);
            this.lblTotalConsumos.AutoSize  = true;
            this.grpTotalCon.Controls.Add(this.lblTotalConsumos);

            this.tabConsumos.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                this.grpCombustible, this.lblCombustibleTxt, this.lblCombustible,
                this.grpLubricantes, this.lblLubricantesTxt, this.lblLubricantes,
                this.grpLlantas,     this.lblLlantasTxt,     this.lblLlantas,
                this.grpPiezasEsp,   this.lblPiezasEspTxt,   this.lblPiezasEsp,
                this.grpTotalCon
            });
            this.tabConsumos.Text      = "B — Consumos";
            this.tabConsumos.Padding   = new System.Windows.Forms.Padding(6);
            this.tabConsumos.AutoScroll= true;

            // ══════════════════════════════════════════════════════════════════
            // TAB C — OPERACIÓN
            // ══════════════════════════════════════════════════════════════════
            this.grpOperacion.Text     = "C — Operación del equipo";
            this.grpOperacion.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpOperacion.Location = new System.Drawing.Point(12, 10);
            this.grpOperacion.Size     = new System.Drawing.Size(440, 152);

            this.label21.Text = "Salario nominal Sn ($/turno):"; this.label21.Location = new System.Drawing.Point(10, 28); this.label21.Size = new System.Drawing.Size(230, 20); this.label21.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudSalarioOperador.Location = new System.Drawing.Point(248, 25); this.nudSalarioOperador.Size = new System.Drawing.Size(167, 24);
            this.nudSalarioOperador.Minimum = 0; this.nudSalarioOperador.Maximum = 99999; this.nudSalarioOperador.DecimalPlaces = 2; this.nudSalarioOperador.ThousandsSeparator = true; this.nudSalarioOperador.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label22.Text = "Factor Salario Real FSR:"; this.label22.Location = new System.Drawing.Point(10, 60); this.label22.Size = new System.Drawing.Size(230, 20); this.label22.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudFSR.Location = new System.Drawing.Point(248, 57); this.nudFSR.Size = new System.Drawing.Size(167, 24);
            this.nudFSR.Minimum = 1; this.nudFSR.Maximum = 5; this.nudFSR.DecimalPlaces = 6; this.nudFSR.Value = 1.6543m; this.nudFSR.Increment = 0.0001m; this.nudFSR.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.label23.Text = "Horas efectivas/turno Ht:"; this.label23.Location = new System.Drawing.Point(10, 92); this.label23.Size = new System.Drawing.Size(230, 20); this.label23.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.nudHorasTurno.Location = new System.Drawing.Point(248, 89); this.nudHorasTurno.Size = new System.Drawing.Size(167, 24);
            this.nudHorasTurno.Minimum = 1; this.nudHorasTurno.Maximum = 24; this.nudHorasTurno.DecimalPlaces = 2; this.nudHorasTurno.Value = 8; this.nudHorasTurno.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

            this.lblSalarioRealTxt.Text = "Sr = Sn × FSR ($/turno):"; this.lblSalarioRealTxt.Location = new System.Drawing.Point(10, 122); this.lblSalarioRealTxt.Size = new System.Drawing.Size(230, 20); this.lblSalarioRealTxt.Font = new System.Drawing.Font("Segoe UI", 9F); this.lblSalarioRealTxt.ForeColor = System.Drawing.Color.Gray;
            this.lblSalarioReal.Text = "$0.00"; this.lblSalarioReal.Location = new System.Drawing.Point(248, 122); this.lblSalarioReal.AutoSize = true; this.lblSalarioReal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            this.grpOperacion.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.label21, this.nudSalarioOperador, this.label22, this.nudFSR,
              this.label23, this.nudHorasTurno, this.lblSalarioRealTxt, this.lblSalarioReal });

            this.lblOperacionTxt.Text = "Po = (Sn × FSR) / Ht ($/hr):"; this.lblOperacionTxt.Location = new System.Drawing.Point(470, 22); this.lblOperacionTxt.Size = new System.Drawing.Size(260, 20); this.lblOperacionTxt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblOperacion.Text    = "$0.00"; this.lblOperacion.Location = new System.Drawing.Point(470, 44); this.lblOperacion.AutoSize = true; this.lblOperacion.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold); this.lblOperacion.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblNotaOperacion.Text      = "Art. 206 RLOPSRM: Po cubre el salario real del operador por hora efectiva de trabajo.\r\n" +
                                              "Si el operador realiza otra actividad en el mismo concepto del APU, puede excluirse de\r\n" +
                                              "este cálculo siempre que el rendimiento del APU no incluya su tiempo de operación del equipo.";
            this.lblNotaOperacion.Location  = new System.Drawing.Point(12, 175);
            this.lblNotaOperacion.Size      = new System.Drawing.Size(1020, 60);
            this.lblNotaOperacion.Font      = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Italic);
            this.lblNotaOperacion.ForeColor = System.Drawing.Color.FromArgb(120, 120, 120);

            this.tabOperacion.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.grpOperacion, this.lblOperacionTxt, this.lblOperacion, this.lblNotaOperacion });
            this.tabOperacion.Text    = "C — Operación";
            this.tabOperacion.Padding = new System.Windows.Forms.Padding(6);

            // ══════════════════════════════════════════════════════════════════
            // TAB D — RESUMEN
            // ══════════════════════════════════════════════════════════════════
            this.panelResumen.Location   = new System.Drawing.Point(4, 4);
            this.panelResumen.Size       = new System.Drawing.Size(1040, 540);
            this.panelResumen.AutoScroll = true;

            // Fórmula cabecera
            this.lblResFormula.Text      = "Phm = A + B + C   (Arts. 194-210 RLOPSRM)";
            this.lblResFormula.Location  = new System.Drawing.Point(50, 14);
            this.lblResFormula.AutoSize  = true;
            this.lblResFormula.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.lblResFormula.ForeColor = System.Drawing.Color.Gray;

            // A — CARGOS FIJOS
            this.lblResA.Text      = "A - CARGOS FIJOS";
            this.lblResA.Location  = new System.Drawing.Point(50, 42);
            this.lblResA.AutoSize  = true;
            this.lblResA.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResA.ForeColor = System.Drawing.Color.FromArgb(51, 51, 76);

            this.lblResADep.Text      = "    D    Depreciacion";
            this.lblResADep.Location  = new System.Drawing.Point(50, 68);
            this.lblResADep.AutoSize  = true;
            this.lblResADep.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResADep.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResADepVal.Text      = "$0.00";
            this.lblResADepVal.Location  = new System.Drawing.Point(800, 68);
            this.lblResADepVal.AutoSize  = true;
            this.lblResADepVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResADepVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResAInv.Text      = "    Im  Inversion";
            this.lblResAInv.Location  = new System.Drawing.Point(50, 90);
            this.lblResAInv.AutoSize  = true;
            this.lblResAInv.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResAInv.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResAInvVal.Text      = "$0.00";
            this.lblResAInvVal.Location  = new System.Drawing.Point(800, 90);
            this.lblResAInvVal.AutoSize  = true;
            this.lblResAInvVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResAInvVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResASeg.Text      = "    Sm  Seguros";
            this.lblResASeg.Location  = new System.Drawing.Point(50, 112);
            this.lblResASeg.AutoSize  = true;
            this.lblResASeg.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResASeg.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResASegVal.Text      = "$0.00";
            this.lblResASegVal.Location  = new System.Drawing.Point(800, 112);
            this.lblResASegVal.AutoSize  = true;
            this.lblResASegVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResASegVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResAMnt.Text      = "    Mn  Mantenimiento";
            this.lblResAMnt.Location  = new System.Drawing.Point(50, 134);
            this.lblResAMnt.AutoSize  = true;
            this.lblResAMnt.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResAMnt.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResAMntVal.Text      = "$0.00";
            this.lblResAMntVal.Location  = new System.Drawing.Point(800, 134);
            this.lblResAMntVal.AutoSize  = true;
            this.lblResAMntVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResAMntVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResATot.Text      = "    Subtotal A";
            this.lblResATot.Location  = new System.Drawing.Point(50, 156);
            this.lblResATot.AutoSize  = true;
            this.lblResATot.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResATot.ForeColor = System.Drawing.Color.FromArgb(191, 54, 12);
            this.lblResATotVal.Text      = "$0.00";
            this.lblResATotVal.Location  = new System.Drawing.Point(800, 156);
            this.lblResATotVal.AutoSize  = true;
            this.lblResATotVal.Font      = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblResATotVal.ForeColor = System.Drawing.Color.FromArgb(191, 54, 12);

            // B — CONSUMOS
            this.lblResB.Text      = "B - CONSUMOS";
            this.lblResB.Location  = new System.Drawing.Point(50, 188);
            this.lblResB.AutoSize  = true;
            this.lblResB.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResB.ForeColor = System.Drawing.Color.FromArgb(51, 51, 76);

            this.lblResBCom.Text      = "    Co  Combustible";
            this.lblResBCom.Location  = new System.Drawing.Point(50, 214);
            this.lblResBCom.AutoSize  = true;
            this.lblResBCom.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResBCom.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResBComVal.Text      = "$0.00";
            this.lblResBComVal.Location  = new System.Drawing.Point(800, 214);
            this.lblResBComVal.AutoSize  = true;
            this.lblResBComVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResBComVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResBLub.Text      = "    Lb  Lubricantes";
            this.lblResBLub.Location  = new System.Drawing.Point(50, 236);
            this.lblResBLub.AutoSize  = true;
            this.lblResBLub.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResBLub.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResBLubVal.Text      = "$0.00";
            this.lblResBLubVal.Location  = new System.Drawing.Point(800, 236);
            this.lblResBLubVal.AutoSize  = true;
            this.lblResBLubVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResBLubVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResBLla.Text      = "    Nt  Llantas";
            this.lblResBLla.Location  = new System.Drawing.Point(50, 258);
            this.lblResBLla.AutoSize  = true;
            this.lblResBLla.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResBLla.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResBLlaVal.Text      = "$0.00";
            this.lblResBLlaVal.Location  = new System.Drawing.Point(800, 258);
            this.lblResBLlaVal.AutoSize  = true;
            this.lblResBLlaVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResBLlaVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResBPie.Text      = "    Ae  Piezas especiales";
            this.lblResBPie.Location  = new System.Drawing.Point(50, 280);
            this.lblResBPie.AutoSize  = true;
            this.lblResBPie.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResBPie.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResBPieVal.Text      = "$0.00";
            this.lblResBPieVal.Location  = new System.Drawing.Point(800, 280);
            this.lblResBPieVal.AutoSize  = true;
            this.lblResBPieVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResBPieVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResBTot.Text      = "    Subtotal B";
            this.lblResBTot.Location  = new System.Drawing.Point(50, 302);
            this.lblResBTot.AutoSize  = true;
            this.lblResBTot.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResBTot.ForeColor = System.Drawing.Color.FromArgb(191, 54, 12);
            this.lblResBTotVal.Text      = "$0.00";
            this.lblResBTotVal.Location  = new System.Drawing.Point(800, 302);
            this.lblResBTotVal.AutoSize  = true;
            this.lblResBTotVal.Font      = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblResBTotVal.ForeColor = System.Drawing.Color.FromArgb(191, 54, 12);

            // C — OPERACION
            this.lblResC.Text      = "C - OPERACION";
            this.lblResC.Location  = new System.Drawing.Point(50, 334);
            this.lblResC.AutoSize  = true;
            this.lblResC.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResC.ForeColor = System.Drawing.Color.FromArgb(51, 51, 76);

            this.lblResCOpe.Text      = "    Po  Operador";
            this.lblResCOpe.Location  = new System.Drawing.Point(50, 360);
            this.lblResCOpe.AutoSize  = true;
            this.lblResCOpe.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.lblResCOpe.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            this.lblResCOpeVal.Text      = "$0.00";
            this.lblResCOpeVal.Location  = new System.Drawing.Point(800, 360);
            this.lblResCOpeVal.AutoSize  = true;
            this.lblResCOpeVal.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblResCOpeVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.lblResSep.Text      = "-------------------------------------------------------------------------------------";
            this.lblResSep.Location  = new System.Drawing.Point(50, 390);
            this.lblResSep.AutoSize  = true;
            this.lblResSep.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.lblResSep.ForeColor = System.Drawing.Color.Silver;

            this.lblResTotalTxt.Text      = "Phm - COSTO HORARIO TOTAL";
            this.lblResTotalTxt.Location  = new System.Drawing.Point(50, 408);
            this.lblResTotalTxt.AutoSize  = true;
            this.lblResTotalTxt.Font      = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblResTotalTxt.ForeColor = System.Drawing.Color.FromArgb(51, 51, 76);
            this.lblResTotalVal.Text      = "$0.00 / hr";
            this.lblResTotalVal.Location  = new System.Drawing.Point(750, 404);
            this.lblResTotalVal.AutoSize  = true;
            this.lblResTotalVal.Font      = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold);
            this.lblResTotalVal.ForeColor = System.Drawing.Color.FromArgb(27, 94, 32);

            this.panelResumen.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                this.lblResFormula,
                this.lblResA,   this.lblResADep, this.lblResADepVal, this.lblResAInv, this.lblResAInvVal,
                                this.lblResASeg, this.lblResASegVal, this.lblResAMnt, this.lblResAMntVal,
                                this.lblResATot, this.lblResATotVal,
                this.lblResB,   this.lblResBCom, this.lblResBComVal, this.lblResBLub, this.lblResBLubVal,
                                this.lblResBLla, this.lblResBLlaVal, this.lblResBPie, this.lblResBPieVal,
                                this.lblResBTot, this.lblResBTotVal,
                this.lblResC,   this.lblResCOpe, this.lblResCOpeVal,
                this.lblResSep, this.lblResTotalTxt, this.lblResTotalVal
            });

            this.tabResumen.Controls.Add(this.panelResumen);
            this.tabResumen.Text    = "📋 Resumen";
            this.tabResumen.Padding = new System.Windows.Forms.Padding(6);

            // ══════════════════════════════════════════════════════════════════
            // TABCONTROL
            // ══════════════════════════════════════════════════════════════════
            this.tabControl.Location     = new System.Drawing.Point(0, 106);
            this.tabControl.Size         = new System.Drawing.Size(1080, 596);
            this.tabControl.Font         = new System.Drawing.Font("Segoe UI", 10F);
            this.tabControl.TabPages.AddRange(new System.Windows.Forms.TabPage[]
            { this.tabResumen, this.tabCargosFijos, this.tabConsumos, this.tabOperacion });
            this.tabControl.SelectedIndex = 0;

            // ══════════════════════════════════════════════════════════════════
            // FORM
            // ══════════════════════════════════════════════════════════════════
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(1080, 764);
            this.Font                = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle     = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox         = false;
            this.MinimizeBox         = false;
            this.Name                = "FormCalculoCostoHorario";
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text                = "Cálculo de Costo Horario — Método RLOPSRM (Arts. 194–210)";
            this.BackColor           = System.Drawing.Color.White;

            this.Controls.AddRange(new System.Windows.Forms.Control[]
            { this.panelTop, this.panelInfo, this.tabControl, this.panelBottom });

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // ── Declaraciones de controles ─────────────────────────────────────────
        private System.Windows.Forms.Panel     panelTop, panelInfo, panelBottom;
        private System.Windows.Forms.Label     lblTitulo;
        private System.Windows.Forms.Label     label1, label2, label3, label4;
        private System.Windows.Forms.Label     lblClaveVal, lblDescVal, lblPotenciaVal, lblCombVal;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage   tabCargosFijos, tabConsumos, tabOperacion, tabResumen;
        private System.Windows.Forms.Label     lblTextoTotal, lblCostoTotal;
        private System.Windows.Forms.Button    btnRestaurarDefecto, btnCancelar, btnGuardar;
        // Tab A
        private System.Windows.Forms.GroupBox  grpDepreciacion, grpInversion, grpSeguros, grpMantenimiento, grpTotalCF;
        private System.Windows.Forms.Label     label5, label6, label7, label8, label9;
        private System.Windows.Forms.NumericUpDown nudValorAdquisicion, nudValorLlantas, nudValorPiezasEsp, nudFactorRescate, nudVidaEconomica;
        private System.Windows.Forms.Label     lblVmTxt, lblVmVal, lblVrTxt, lblVrVal;
        private System.Windows.Forms.Label     label10, label11;
        private System.Windows.Forms.NumericUpDown nudTasaInteres, nudHorasAnio;
        private System.Windows.Forms.Label     lblVmVrMedioTxt, lblVmVrMedioVal;
        private System.Windows.Forms.Label     label12;
        private System.Windows.Forms.NumericUpDown nudPrimaSeguro;
        private System.Windows.Forms.Label     label13;
        private System.Windows.Forms.NumericUpDown nudFactorManten;
        private System.Windows.Forms.Label     lblDepreciacionTxt, lblDepreciacion;
        private System.Windows.Forms.Label     lblInversionTxt,    lblInversion;
        private System.Windows.Forms.Label     lblSegurosTxt,      lblSeguros;
        private System.Windows.Forms.Label     lblMantenimientoTxt,lblMantenimiento;
        private System.Windows.Forms.Label     lblTotalCargosFijos;
        // Tab B
        private System.Windows.Forms.GroupBox  grpCombustible, grpLubricantes, grpLlantas, grpPiezasEsp, grpTotalCon;
        private System.Windows.Forms.Label     label14, label15, label16, label17, label18, label19, label20;
        private System.Windows.Forms.NumericUpDown nudCantCombustible, nudPrecioCombustible;
        private System.Windows.Forms.NumericUpDown nudCantAceite,      nudPrecioAceite;
        private System.Windows.Forms.NumericUpDown nudNumLlantas,      nudVidaLlantas;
        private System.Windows.Forms.NumericUpDown nudVidaPiezasEsp;
        private System.Windows.Forms.Button    btnEstimarCombustible;
        private System.Windows.Forms.Label     lblCombustibleTxt, lblCombustible;
        private System.Windows.Forms.Label     lblLubricantesTxt, lblLubricantes;
        private System.Windows.Forms.Label     lblLlantasTxt,     lblLlantas;
        private System.Windows.Forms.Label     lblPiezasEspTxt,   lblPiezasEsp;
        private System.Windows.Forms.Label     lblTotalConsumos;
        // Tab C
        private System.Windows.Forms.GroupBox  grpOperacion;
        private System.Windows.Forms.Label     label21, label22, label23;
        private System.Windows.Forms.NumericUpDown nudSalarioOperador, nudFSR, nudHorasTurno;
        private System.Windows.Forms.Label     lblSalarioRealTxt, lblSalarioReal;
        private System.Windows.Forms.Label     lblOperacionTxt,   lblOperacion;
        private System.Windows.Forms.Label     lblNotaOperacion;
        // Tab D (Resumen)
        private System.Windows.Forms.Panel     panelResumen;
        private System.Windows.Forms.Label     lblResFormula;
        private System.Windows.Forms.Label     lblResA,   lblResADep, lblResADepVal, lblResAInv, lblResAInvVal;
        private System.Windows.Forms.Label     lblResASeg,lblResASegVal, lblResAMnt, lblResAMntVal;
        private System.Windows.Forms.Label     lblResATot,lblResATotVal;
        private System.Windows.Forms.Label     lblResB,   lblResBCom, lblResBComVal, lblResBLub, lblResBLubVal;
        private System.Windows.Forms.Label     lblResBLla,lblResBLlaVal, lblResBPie, lblResBPieVal;
        private System.Windows.Forms.Label     lblResBTot,lblResBTotVal;
        private System.Windows.Forms.Label     lblResC,   lblResCOpe, lblResCOpeVal;
        private System.Windows.Forms.Label     lblResTotalTxt, lblResTotalVal, lblResSep;
    }
}
