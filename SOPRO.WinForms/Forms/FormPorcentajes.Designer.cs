namespace SOPRO.WinForms.Forms
{
    partial class FormPorcentajes
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
            panelTop = new Panel();
            lblTitulo = new Label();
            grpReferencia = new GroupBox();
            nudCDRef = new NumericUpDown();
            lblCDRef = new Label();
            grpPorcentajes = new GroupBox();
            nudCargos = new NumericUpDown();
            chkCargos = new CheckBox();
            nudUtil = new NumericUpDown();
            chkUtil = new CheckBox();
            nudFin = new NumericUpDown();
            chkFin = new CheckBox();
            nudCampo = new NumericUpDown();
            chkCampo = new CheckBox();
            nudOC = new NumericUpDown();
            chkOC = new CheckBox();
            grpModoCalculo = new GroupBox();
            rbAcumulables = new RadioButton();
            rbSobreCD = new RadioButton();
            grpPreview = new GroupBox();
            lblPUValor = new Label();
            lblPUTotalLabel = new Label();
            lblSeparador = new Label();
            lblCargosMonto = new Label();
            lblCALabel = new Label();
            lblSub3Valor = new Label();
            lblSub3Label = new Label();
            lblUtilMonto = new Label();
            lblUtilLabel = new Label();
            lblSub2Valor = new Label();
            lblSub2Label = new Label();
            lblFinMonto = new Label();
            lblFinLabel = new Label();
            lblSub1Valor = new Label();
            lblSub1Label = new Label();
            lblCampoMonto = new Label();
            lblCampoLabel = new Label();
            lblOCMonto = new Label();
            lblOCLabel = new Label();
            lblCDValor = new Label();
            lblCDLabel = new Label();
            btnAplicar = new Button();
            btnCancelar = new Button();
            btnDesdeIndirectos = new Button();
            panelTop.SuspendLayout();
            grpReferencia.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudCDRef).BeginInit();
            grpPorcentajes.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudCargos).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudUtil).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudFin).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudCampo).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudOC).BeginInit();
            grpModoCalculo.SuspendLayout();
            grpPreview.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(51, 51, 76);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Margin = new Padding(3, 4, 3, 4);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(857, 80);
            panelTop.TabIndex = 0;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(23, 20);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(222, 41);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "PORCENTAJES";
            // 
            // grpReferencia
            // 
            grpReferencia.Controls.Add(nudCDRef);
            grpReferencia.Controls.Add(lblCDRef);
            grpReferencia.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpReferencia.Location = new Point(23, 107);
            grpReferencia.Margin = new Padding(3, 4, 3, 4);
            grpReferencia.Name = "grpReferencia";
            grpReferencia.Padding = new Padding(3, 4, 3, 4);
            grpReferencia.Size = new Size(400, 107);
            grpReferencia.TabIndex = 1;
            grpReferencia.TabStop = false;
            grpReferencia.Text = "COSTO DIRECTO DE REFERENCIA";
            // 
            // nudCDRef
            // 
            nudCDRef.DecimalPlaces = 2;
            nudCDRef.Font = new Font("Segoe UI", 12F);
            nudCDRef.Location = new Point(171, 47);
            nudCDRef.Margin = new Padding(3, 4, 3, 4);
            nudCDRef.Maximum = new decimal(new int[] { -1530494977, 232830, 0, 0 });
            nudCDRef.Name = "nudCDRef";
            nudCDRef.Size = new Size(206, 34);
            nudCDRef.TabIndex = 1;
            nudCDRef.TextAlign = HorizontalAlignment.Right;
            nudCDRef.ThousandsSeparator = true;
            nudCDRef.ValueChanged += nudCDRef_ValueChanged;
            // 
            // lblCDRef
            // 
            lblCDRef.AutoSize = true;
            lblCDRef.Location = new Point(23, 53);
            lblCDRef.Name = "lblCDRef";
            lblCDRef.Size = new Size(124, 23);
            lblCDRef.TabIndex = 0;
            lblCDRef.Text = "Costo Directo:";
            // 
            // grpPorcentajes
            // 
            grpPorcentajes.Controls.Add(nudCargos);
            grpPorcentajes.Controls.Add(chkCargos);
            grpPorcentajes.Controls.Add(nudUtil);
            grpPorcentajes.Controls.Add(chkUtil);
            grpPorcentajes.Controls.Add(nudFin);
            grpPorcentajes.Controls.Add(chkFin);
            grpPorcentajes.Controls.Add(nudCampo);
            grpPorcentajes.Controls.Add(chkCampo);
            grpPorcentajes.Controls.Add(nudOC);
            grpPorcentajes.Controls.Add(chkOC);
            grpPorcentajes.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpPorcentajes.Location = new Point(23, 227);
            grpPorcentajes.Margin = new Padding(3, 4, 3, 4);
            grpPorcentajes.Name = "grpPorcentajes";
            grpPorcentajes.Padding = new Padding(3, 4, 3, 4);
            grpPorcentajes.Size = new Size(400, 373);
            grpPorcentajes.TabIndex = 2;
            grpPorcentajes.TabStop = false;
            grpPorcentajes.Text = "PORCENTAJES";
            // 
            // nudCargos
            // 
            nudCargos.DecimalPlaces = 4;
            nudCargos.Enabled = false;
            nudCargos.Font = new Font("Segoe UI", 11F);
            nudCargos.Location = new Point(263, 307);
            nudCargos.Margin = new Padding(3, 4, 3, 4);
            nudCargos.Name = "nudCargos";
            nudCargos.Size = new Size(114, 32);
            nudCargos.TabIndex = 9;
            nudCargos.TextAlign = HorizontalAlignment.Right;
            nudCargos.ValueChanged += nudCargos_ValueChanged;
            // 
            // chkCargos
            // 
            chkCargos.AutoSize = true;
            chkCargos.Location = new Point(23, 309);
            chkCargos.Margin = new Padding(3, 4, 3, 4);
            chkCargos.Name = "chkCargos";
            chkCargos.Size = new Size(215, 27);
            chkCargos.TabIndex = 8;
            chkCargos.Text = "Cargos Adicionales (%)";
            chkCargos.UseVisualStyleBackColor = true;
            chkCargos.CheckedChanged += chkCargos_CheckedChanged;
            // 
            // nudUtil
            // 
            nudUtil.DecimalPlaces = 4;
            nudUtil.Enabled = false;
            nudUtil.Font = new Font("Segoe UI", 11F);
            nudUtil.Location = new Point(263, 247);
            nudUtil.Margin = new Padding(3, 4, 3, 4);
            nudUtil.Name = "nudUtil";
            nudUtil.Size = new Size(114, 32);
            nudUtil.TabIndex = 7;
            nudUtil.TextAlign = HorizontalAlignment.Right;
            nudUtil.ValueChanged += nudUtil_ValueChanged;
            // 
            // chkUtil
            // 
            chkUtil.AutoSize = true;
            chkUtil.Location = new Point(23, 249);
            chkUtil.Margin = new Padding(3, 4, 3, 4);
            chkUtil.Name = "chkUtil";
            chkUtil.Size = new Size(129, 27);
            chkUtil.TabIndex = 6;
            chkUtil.Text = "Utilidad (%)";
            chkUtil.UseVisualStyleBackColor = true;
            chkUtil.CheckedChanged += chkUtil_CheckedChanged;
            // 
            // nudFin
            // 
            nudFin.DecimalPlaces = 4;
            nudFin.Enabled = false;
            nudFin.Font = new Font("Segoe UI", 11F);
            nudFin.Location = new Point(263, 187);
            nudFin.Margin = new Padding(3, 4, 3, 4);
            nudFin.Name = "nudFin";
            nudFin.Size = new Size(114, 32);
            nudFin.TabIndex = 5;
            nudFin.TextAlign = HorizontalAlignment.Right;
            nudFin.ValueChanged += nudFin_ValueChanged;
            // 
            // chkFin
            // 
            chkFin.AutoSize = true;
            chkFin.Location = new Point(23, 189);
            chkFin.Margin = new Padding(3, 4, 3, 4);
            chkFin.Name = "chkFin";
            chkFin.Size = new Size(186, 27);
            chkFin.TabIndex = 4;
            chkFin.Text = "Financiamiento (%)";
            chkFin.UseVisualStyleBackColor = true;
            chkFin.CheckedChanged += chkFin_CheckedChanged;
            // 
            // nudCampo
            // 
            nudCampo.DecimalPlaces = 4;
            nudCampo.Enabled = false;
            nudCampo.Font = new Font("Segoe UI", 11F);
            nudCampo.Location = new Point(263, 127);
            nudCampo.Margin = new Padding(3, 4, 3, 4);
            nudCampo.Name = "nudCampo";
            nudCampo.Size = new Size(114, 32);
            nudCampo.TabIndex = 3;
            nudCampo.TextAlign = HorizontalAlignment.Right;
            nudCampo.ValueChanged += nudCampo_ValueChanged;
            // 
            // chkCampo
            // 
            chkCampo.AutoSize = true;
            chkCampo.Location = new Point(23, 129);
            chkCampo.Margin = new Padding(3, 4, 3, 4);
            chkCampo.Name = "chkCampo";
            chkCampo.Size = new Size(230, 27);
            chkCampo.TabIndex = 2;
            chkCampo.Text = "Indirectos de Campo (%)";
            chkCampo.UseVisualStyleBackColor = true;
            chkCampo.CheckedChanged += chkCampo_CheckedChanged;
            // 
            // nudOC
            // 
            nudOC.DecimalPlaces = 4;
            nudOC.Enabled = false;
            nudOC.Font = new Font("Segoe UI", 11F);
            nudOC.Location = new Point(263, 67);
            nudOC.Margin = new Padding(3, 4, 3, 4);
            nudOC.Name = "nudOC";
            nudOC.Size = new Size(114, 32);
            nudOC.TabIndex = 1;
            nudOC.TextAlign = HorizontalAlignment.Right;
            nudOC.ValueChanged += nudOC_ValueChanged;
            // 
            // chkOC
            // 
            chkOC.AutoSize = true;
            chkOC.Location = new Point(23, 69);
            chkOC.Margin = new Padding(3, 4, 3, 4);
            chkOC.Name = "chkOC";
            chkOC.Size = new Size(230, 27);
            chkOC.TabIndex = 0;
            chkOC.Text = "Indirectos de Oficina (%)";
            chkOC.UseVisualStyleBackColor = true;
            chkOC.CheckedChanged += chkOC_CheckedChanged;
            // 
            // grpModoCalculo
            // 
            grpModoCalculo.Controls.Add(rbAcumulables);
            grpModoCalculo.Controls.Add(rbSobreCD);
            grpModoCalculo.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpModoCalculo.Location = new Point(23, 613);
            grpModoCalculo.Margin = new Padding(3, 4, 3, 4);
            grpModoCalculo.Name = "grpModoCalculo";
            grpModoCalculo.Padding = new Padding(3, 4, 3, 4);
            grpModoCalculo.Size = new Size(400, 133);
            grpModoCalculo.TabIndex = 3;
            grpModoCalculo.TabStop = false;
            grpModoCalculo.Text = "MODO DE CÁLCULO";
            // 
            // rbAcumulables
            // 
            rbAcumulables.AutoSize = true;
            rbAcumulables.Location = new Point(23, 80);
            rbAcumulables.Margin = new Padding(3, 4, 3, 4);
            rbAcumulables.Name = "rbAcumulables";
            rbAcumulables.Size = new Size(133, 27);
            rbAcumulables.TabIndex = 1;
            rbAcumulables.Text = "Acumulables";
            rbAcumulables.UseVisualStyleBackColor = true;
            rbAcumulables.CheckedChanged += rbAcumulables_CheckedChanged;
            // 
            // rbSobreCD
            // 
            rbSobreCD.AutoSize = true;
            rbSobreCD.Checked = true;
            rbSobreCD.Location = new Point(23, 40);
            rbSobreCD.Margin = new Padding(3, 4, 3, 4);
            rbSobreCD.Name = "rbSobreCD";
            rbSobreCD.Size = new Size(281, 27);
            rbSobreCD.TabIndex = 0;
            rbSobreCD.TabStop = true;
            rbSobreCD.Text = "Todos sobre Costo Directo (CD)";
            rbSobreCD.UseVisualStyleBackColor = true;
            rbSobreCD.CheckedChanged += rbSobreCD_CheckedChanged;
            // 
            // grpPreview
            // 
            grpPreview.Controls.Add(lblPUValor);
            grpPreview.Controls.Add(lblPUTotalLabel);
            grpPreview.Controls.Add(lblSeparador);
            grpPreview.Controls.Add(lblCargosMonto);
            grpPreview.Controls.Add(lblCALabel);
            grpPreview.Controls.Add(lblSub3Valor);
            grpPreview.Controls.Add(lblSub3Label);
            grpPreview.Controls.Add(lblUtilMonto);
            grpPreview.Controls.Add(lblUtilLabel);
            grpPreview.Controls.Add(lblSub2Valor);
            grpPreview.Controls.Add(lblSub2Label);
            grpPreview.Controls.Add(lblFinMonto);
            grpPreview.Controls.Add(lblFinLabel);
            grpPreview.Controls.Add(lblSub1Valor);
            grpPreview.Controls.Add(lblSub1Label);
            grpPreview.Controls.Add(lblCampoMonto);
            grpPreview.Controls.Add(lblCampoLabel);
            grpPreview.Controls.Add(lblOCMonto);
            grpPreview.Controls.Add(lblOCLabel);
            grpPreview.Controls.Add(lblCDValor);
            grpPreview.Controls.Add(lblCDLabel);
            grpPreview.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grpPreview.Location = new Point(446, 107);
            grpPreview.Margin = new Padding(3, 4, 3, 4);
            grpPreview.Name = "grpPreview";
            grpPreview.Padding = new Padding(3, 4, 3, 4);
            grpPreview.Size = new Size(389, 640);
            grpPreview.TabIndex = 4;
            grpPreview.TabStop = false;
            grpPreview.Text = "VISTA PREVIA";
            // 
            // lblPUValor
            // 
            lblPUValor.Location = new Point(0, 0);
            lblPUValor.Name = "lblPUValor";
            lblPUValor.Size = new Size(130, 31);
            lblPUValor.TabIndex = 0;
            // 
            // lblPUTotalLabel
            // 
            lblPUTotalLabel.Location = new Point(0, 0);
            lblPUTotalLabel.Name = "lblPUTotalLabel";
            lblPUTotalLabel.Size = new Size(114, 31);
            lblPUTotalLabel.TabIndex = 1;
            // 
            // lblSeparador
            // 
            lblSeparador.Location = new Point(0, 0);
            lblSeparador.Name = "lblSeparador";
            lblSeparador.Size = new Size(114, 31);
            lblSeparador.TabIndex = 2;
            // 
            // lblCargosMonto
            // 
            lblCargosMonto.Location = new Point(0, 0);
            lblCargosMonto.Name = "lblCargosMonto";
            lblCargosMonto.Size = new Size(114, 31);
            lblCargosMonto.TabIndex = 3;
            // 
            // lblCALabel
            // 
            lblCALabel.Location = new Point(0, 0);
            lblCALabel.Name = "lblCALabel";
            lblCALabel.Size = new Size(114, 31);
            lblCALabel.TabIndex = 4;
            // 
            // lblSub3Valor
            // 
            lblSub3Valor.Location = new Point(0, 0);
            lblSub3Valor.Name = "lblSub3Valor";
            lblSub3Valor.Size = new Size(114, 31);
            lblSub3Valor.TabIndex = 5;
            // 
            // lblSub3Label
            // 
            lblSub3Label.Location = new Point(0, 0);
            lblSub3Label.Name = "lblSub3Label";
            lblSub3Label.Size = new Size(114, 31);
            lblSub3Label.TabIndex = 6;
            // 
            // lblUtilMonto
            // 
            lblUtilMonto.Location = new Point(0, 0);
            lblUtilMonto.Name = "lblUtilMonto";
            lblUtilMonto.Size = new Size(114, 31);
            lblUtilMonto.TabIndex = 7;
            // 
            // lblUtilLabel
            // 
            lblUtilLabel.Location = new Point(0, 0);
            lblUtilLabel.Name = "lblUtilLabel";
            lblUtilLabel.Size = new Size(114, 31);
            lblUtilLabel.TabIndex = 8;
            // 
            // lblSub2Valor
            // 
            lblSub2Valor.Location = new Point(0, 0);
            lblSub2Valor.Name = "lblSub2Valor";
            lblSub2Valor.Size = new Size(114, 31);
            lblSub2Valor.TabIndex = 9;
            // 
            // lblSub2Label
            // 
            lblSub2Label.Location = new Point(0, 0);
            lblSub2Label.Name = "lblSub2Label";
            lblSub2Label.Size = new Size(114, 31);
            lblSub2Label.TabIndex = 10;
            // 
            // lblFinMonto
            // 
            lblFinMonto.Location = new Point(0, 0);
            lblFinMonto.Name = "lblFinMonto";
            lblFinMonto.Size = new Size(114, 31);
            lblFinMonto.TabIndex = 11;
            // 
            // lblFinLabel
            // 
            lblFinLabel.Location = new Point(0, 0);
            lblFinLabel.Name = "lblFinLabel";
            lblFinLabel.Size = new Size(114, 31);
            lblFinLabel.TabIndex = 12;
            // 
            // lblSub1Valor
            // 
            lblSub1Valor.Location = new Point(0, 0);
            lblSub1Valor.Name = "lblSub1Valor";
            lblSub1Valor.Size = new Size(114, 31);
            lblSub1Valor.TabIndex = 13;
            // 
            // lblSub1Label
            // 
            lblSub1Label.Location = new Point(0, 0);
            lblSub1Label.Name = "lblSub1Label";
            lblSub1Label.Size = new Size(114, 31);
            lblSub1Label.TabIndex = 14;
            // 
            // lblCampoMonto
            // 
            lblCampoMonto.Location = new Point(0, 0);
            lblCampoMonto.Name = "lblCampoMonto";
            lblCampoMonto.Size = new Size(114, 31);
            lblCampoMonto.TabIndex = 15;
            // 
            // lblCampoLabel
            // 
            lblCampoLabel.Location = new Point(0, 0);
            lblCampoLabel.Name = "lblCampoLabel";
            lblCampoLabel.Size = new Size(114, 31);
            lblCampoLabel.TabIndex = 16;
            // 
            // lblOCMonto
            // 
            lblOCMonto.Location = new Point(0, 0);
            lblOCMonto.Name = "lblOCMonto";
            lblOCMonto.Size = new Size(114, 31);
            lblOCMonto.TabIndex = 17;
            // 
            // lblOCLabel
            // 
            lblOCLabel.Location = new Point(0, 0);
            lblOCLabel.Name = "lblOCLabel";
            lblOCLabel.Size = new Size(114, 31);
            lblOCLabel.TabIndex = 18;
            // 
            // lblCDValor
            // 
            lblCDValor.Location = new Point(0, 0);
            lblCDValor.Name = "lblCDValor";
            lblCDValor.Size = new Size(114, 31);
            lblCDValor.TabIndex = 19;
            // 
            // lblCDLabel
            // 
            lblCDLabel.Location = new Point(0, 0);
            lblCDLabel.Name = "lblCDLabel";
            lblCDLabel.Size = new Size(114, 31);
            lblCDLabel.TabIndex = 20;
            // 
            // btnAplicar
            // 
            btnAplicar.BackColor = Color.FromArgb(76, 175, 80);
            btnAplicar.FlatStyle = FlatStyle.Flat;
            btnAplicar.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnAplicar.ForeColor = Color.White;
            btnAplicar.Location = new Point(686, 767);
            btnAplicar.Margin = new Padding(3, 4, 3, 4);
            btnAplicar.Name = "btnAplicar";
            btnAplicar.Size = new Size(149, 60);
            btnAplicar.TabIndex = 5;
            btnAplicar.Text = "✓ Aplicar";
            btnAplicar.UseVisualStyleBackColor = false;
            btnAplicar.Click += BtnAplicar_Click;
            // 
            // btnCancelar
            // 
            btnCancelar.BackColor = Color.FromArgb(220, 220, 220);
            btnCancelar.DialogResult = DialogResult.Cancel;
            btnCancelar.FlatStyle = FlatStyle.Flat;
            btnCancelar.Font = new Font("Segoe UI", 10F);
            btnCancelar.Location = new Point(549, 767);
            btnCancelar.Margin = new Padding(3, 4, 3, 4);
            btnCancelar.Name = "btnCancelar";
            btnCancelar.Size = new Size(126, 60);
            btnCancelar.TabIndex = 6;
            btnCancelar.Text = "Cancelar";
            btnCancelar.UseVisualStyleBackColor = false;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // btnDesdeIndirectos
            // 
            btnDesdeIndirectos.BackColor = Color.FromArgb(33, 150, 243);
            btnDesdeIndirectos.FlatStyle = FlatStyle.Flat;
            btnDesdeIndirectos.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnDesdeIndirectos.ForeColor = Color.White;
            btnDesdeIndirectos.Location = new Point(23, 767);
            btnDesdeIndirectos.Margin = new Padding(3, 4, 3, 4);
            btnDesdeIndirectos.Name = "btnDesdeIndirectos";
            btnDesdeIndirectos.Size = new Size(206, 60);
            btnDesdeIndirectos.TabIndex = 7;
            btnDesdeIndirectos.Text = "📊 Desde Indirectos";
            btnDesdeIndirectos.UseVisualStyleBackColor = false;
            btnDesdeIndirectos.Visible = false;
            btnDesdeIndirectos.Click += BtnDesdeIndirectos_Click;
            // 
            // FormPorcentajes
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            CancelButton = btnCancelar;
            ClientSize = new Size(857, 853);
            Controls.Add(btnDesdeIndirectos);
            Controls.Add(btnCancelar);
            Controls.Add(btnAplicar);
            Controls.Add(grpPreview);
            Controls.Add(grpModoCalculo);
            Controls.Add(grpPorcentajes);
            Controls.Add(grpReferencia);
            Controls.Add(panelTop);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(3, 4, 3, 4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FormPorcentajes";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Porcentajes";
            Load += FormPorcentajes_Load;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            grpReferencia.ResumeLayout(false);
            grpReferencia.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudCDRef).EndInit();
            grpPorcentajes.ResumeLayout(false);
            grpPorcentajes.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudCargos).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudUtil).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudFin).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudCampo).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudOC).EndInit();
            grpModoCalculo.ResumeLayout(false);
            grpModoCalculo.PerformLayout();
            grpPreview.ResumeLayout(false);
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.GroupBox grpReferencia;
        private System.Windows.Forms.NumericUpDown nudCDRef;
        private System.Windows.Forms.Label lblCDRef;
        private System.Windows.Forms.GroupBox grpPorcentajes;
        private System.Windows.Forms.NumericUpDown nudCargos;
        private System.Windows.Forms.CheckBox chkCargos;
        private System.Windows.Forms.NumericUpDown nudUtil;
        private System.Windows.Forms.CheckBox chkUtil;
        private System.Windows.Forms.NumericUpDown nudFin;
        private System.Windows.Forms.CheckBox chkFin;
        private System.Windows.Forms.NumericUpDown nudCampo;
        private System.Windows.Forms.CheckBox chkCampo;
        private System.Windows.Forms.NumericUpDown nudOC;
        private System.Windows.Forms.CheckBox chkOC;
        private System.Windows.Forms.GroupBox grpModoCalculo;
        private System.Windows.Forms.RadioButton rbAcumulables;
        private System.Windows.Forms.RadioButton rbSobreCD;
        private System.Windows.Forms.GroupBox grpPreview;
        private System.Windows.Forms.Label lblPUValor;
        private System.Windows.Forms.Label lblPUTotalLabel;
        private System.Windows.Forms.Label lblSeparador;
        private System.Windows.Forms.Label lblCargosMonto;
        private System.Windows.Forms.Label lblCALabel;
        private System.Windows.Forms.Label lblSub3Valor;
        private System.Windows.Forms.Label lblSub3Label;
        private System.Windows.Forms.Label lblUtilMonto;
        private System.Windows.Forms.Label lblUtilLabel;
        private System.Windows.Forms.Label lblSub2Valor;
        private System.Windows.Forms.Label lblSub2Label;
        private System.Windows.Forms.Label lblFinMonto;
        private System.Windows.Forms.Label lblFinLabel;
        private System.Windows.Forms.Label lblSub1Valor;
        private System.Windows.Forms.Label lblSub1Label;
        private System.Windows.Forms.Label lblCampoMonto;
        private System.Windows.Forms.Label lblCampoLabel;
        private System.Windows.Forms.Label lblOCMonto;
        private System.Windows.Forms.Label lblOCLabel;
        private System.Windows.Forms.Label lblCDValor;
        private System.Windows.Forms.Label lblCDLabel;
        private System.Windows.Forms.Button btnAplicar;
        private System.Windows.Forms.Button btnCancelar;
        private System.Windows.Forms.Button btnDesdeIndirectos;
    }
}
