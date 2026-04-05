namespace SOPRO.WinForms.Forms
{
    partial class FormPlantillaReporte
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // ── Controles ────────────────────────────────────────────────────
            this.splitMain          = new System.Windows.Forms.SplitContainer();
            this.tabConfig          = new System.Windows.Forms.TabControl();
            this.tabEncPie          = new System.Windows.Forms.TabPage();
            this.tabDatos           = new System.Windows.Forms.TabPage();

            // Panel encabezado/pie
            this.grpEncabezado      = new System.Windows.Forms.GroupBox();
            this.panelPreviewEnc    = new System.Windows.Forms.Panel();
            this.lblZonasEnc        = new System.Windows.Forms.Label();
            this.btnEncIzq          = new System.Windows.Forms.Button();
            this.btnEncCen          = new System.Windows.Forms.Button();
            this.btnEncDer          = new System.Windows.Forms.Button();
            this.lblAltEnc          = new System.Windows.Forms.Label();
            this.nudAltEncabezado   = new System.Windows.Forms.NumericUpDown();

            this.grpPie             = new System.Windows.Forms.GroupBox();
            this.panelPreviewPie    = new System.Windows.Forms.Panel();
            this.lblZonasPie        = new System.Windows.Forms.Label();
            this.btnPieIzq          = new System.Windows.Forms.Button();
            this.btnPieCen          = new System.Windows.Forms.Button();
            this.btnPieDer          = new System.Windows.Forms.Button();
            this.lblAltPie          = new System.Windows.Forms.Label();
            this.nudAltPie          = new System.Windows.Forms.NumericUpDown();

            // Panel editor de zona
            this.grpEditorZona      = new System.Windows.Forms.GroupBox();
            this.lblTipoZona        = new System.Windows.Forms.Label();
            this.cboTipoZona        = new System.Windows.Forms.ComboBox();
            this.panelTexto         = new System.Windows.Forms.Panel();
            this.lblContenido       = new System.Windows.Forms.Label();
            this.txtContenidoZona   = new System.Windows.Forms.TextBox();
            this.lblCamposDisp      = new System.Windows.Forms.Label();
            this.lstCampos          = new System.Windows.Forms.ListBox();
            this.btnInsertarCampo   = new System.Windows.Forms.Button();
            this.panelImagen        = new System.Windows.Forms.Panel();
            this.lblRutaImagen      = new System.Windows.Forms.Label();
            this.btnSeleccionarImagen = new System.Windows.Forms.Button();

            // Formato zona
            this.grpFormato         = new System.Windows.Forms.GroupBox();
            this.lblFuente          = new System.Windows.Forms.Label();
            this.cboFuente          = new System.Windows.Forms.ComboBox();
            this.lblTamaño          = new System.Windows.Forms.Label();
            this.nudTamaño          = new System.Windows.Forms.NumericUpDown();
            this.chkNegrita         = new System.Windows.Forms.CheckBox();
            this.chkCursiva         = new System.Windows.Forms.CheckBox();
            this.lblAlineacion      = new System.Windows.Forms.Label();
            this.cboAlineacion      = new System.Windows.Forms.ComboBox();

            // Tab datos dinámicos
            this.grpDatosProyecto   = new System.Windows.Forms.GroupBox();
            this.lblElabaro         = new System.Windows.Forms.Label();
            this.txtElabaro         = new System.Windows.Forms.TextBox();
            this.lblReviso          = new System.Windows.Forms.Label();
            this.txtReviso          = new System.Windows.Forms.TextBox();
            this.lblAutorizo        = new System.Windows.Forms.Label();
            this.txtAutorizo        = new System.Windows.Forms.TextBox();
            this.lblDependencia     = new System.Windows.Forms.Label();
            this.txtDependencia     = new System.Windows.Forms.TextBox();
            this.lblNumContrato     = new System.Windows.Forms.Label();
            this.txtNumContrato     = new System.Windows.Forms.TextBox();
            this.lblLicitacion      = new System.Windows.Forms.Label();
            this.txtLicitacion      = new System.Windows.Forms.TextBox();
            this.lblTextoLibre1     = new System.Windows.Forms.Label();
            this.txtTextoLibre1     = new System.Windows.Forms.TextBox();
            this.lblTextoLibre2     = new System.Windows.Forms.Label();
            this.txtTextoLibre2     = new System.Windows.Forms.TextBox();
            this.lblCamposInfo      = new System.Windows.Forms.Label();

            // Botones bottom
            this.panelBottom        = new System.Windows.Forms.Panel();
            this.btnGuardar         = new System.Windows.Forms.Button();
            this.btnCerrar          = new System.Windows.Forms.Button();

            this.SuspendLayout();

            // ── splitMain ────────────────────────────────────────────────────
            this.splitMain.Dock            = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location        = new System.Drawing.Point(0, 0);
            this.splitMain.Name            = "splitMain";
            this.splitMain.Orientation     = System.Windows.Forms.Orientation.Horizontal;
            this.splitMain.SplitterDistance = 520;
            this.splitMain.Size            = new System.Drawing.Size(900, 670);

            // Panel1 = columna izquierda (enc+pie) + columna derecha (editor)
            // Usamos otro SplitContainer horizontal
            this.splitMain.Panel1.Controls.Add(this.tabConfig);
            this.splitMain.Panel2.Controls.Add(this.panelBottom);

            // ── tabConfig ────────────────────────────────────────────────────
            this.tabConfig.Dock     = System.Windows.Forms.DockStyle.Fill;
            this.tabConfig.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.tabConfig.Location = new System.Drawing.Point(0, 0);
            this.tabConfig.Name     = "tabConfig";
            this.tabConfig.Size     = new System.Drawing.Size(900, 520);
            this.tabConfig.TabPages.Add(this.tabEncPie);
            this.tabConfig.TabPages.Add(this.tabDatos);

            // ── tabEncPie ────────────────────────────────────────────────────
            this.tabEncPie.Text     = "Encabezado y Pie de Página";
            this.tabEncPie.Padding  = new System.Windows.Forms.Padding(6);
            this.tabEncPie.Controls.Add(this.grpEditorZona);
            this.tabEncPie.Controls.Add(this.grpFormato);
            this.tabEncPie.Controls.Add(this.grpEncabezado);
            this.tabEncPie.Controls.Add(this.grpPie);

            // ── grpEncabezado ────────────────────────────────────────────────
            this.grpEncabezado.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpEncabezado.Location = new System.Drawing.Point(8, 8);
            this.grpEncabezado.Name     = "grpEncabezado";
            this.grpEncabezado.Size     = new System.Drawing.Size(420, 130);
            this.grpEncabezado.Text     = "Encabezado";

            this.panelPreviewEnc.BackColor  = System.Drawing.Color.White;
            this.panelPreviewEnc.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPreviewEnc.Location   = new System.Drawing.Point(10, 22);
            this.panelPreviewEnc.Name       = "panelPreviewEnc";
            this.panelPreviewEnc.Size       = new System.Drawing.Size(400, 60);
            this.panelPreviewEnc.Paint     += new System.Windows.Forms.PaintEventHandler(this.panelPreviewEnc_Paint);

            this.lblZonasEnc.Font     = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblZonasEnc.Location = new System.Drawing.Point(10, 88);
            this.lblZonasEnc.Name     = "lblZonasEnc";
            this.lblZonasEnc.Size     = new System.Drawing.Size(80, 20);
            this.lblZonasEnc.Text     = "Editar zona:";

            this.btnEncIzq.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.btnEncIzq.Location  = new System.Drawing.Point(94, 86);
            this.btnEncIzq.Name      = "btnEncIzq";
            this.btnEncIzq.Size      = new System.Drawing.Size(80, 24);
            this.btnEncIzq.Text      = "Izquierda";
            this.btnEncIzq.Click    += new System.EventHandler(this.btnEncIzq_Click);

            this.btnEncCen.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.btnEncCen.Location  = new System.Drawing.Point(180, 86);
            this.btnEncCen.Name      = "btnEncCen";
            this.btnEncCen.Size      = new System.Drawing.Size(80, 24);
            this.btnEncCen.Text      = "Centro";
            this.btnEncCen.Click    += new System.EventHandler(this.btnEncCen_Click);

            this.btnEncDer.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.btnEncDer.Location  = new System.Drawing.Point(266, 86);
            this.btnEncDer.Name      = "btnEncDer";
            this.btnEncDer.Size      = new System.Drawing.Size(80, 24);
            this.btnEncDer.Text      = "Derecha";
            this.btnEncDer.Click    += new System.EventHandler(this.btnEncDer_Click);

            this.lblAltEnc.Font     = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblAltEnc.Location = new System.Drawing.Point(10, 112);
            this.lblAltEnc.Name     = "lblAltEnc";
            this.lblAltEnc.Size     = new System.Drawing.Size(90, 20);
            this.lblAltEnc.Text     = "Altura (pts):";

            this.nudAltEncabezado.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.nudAltEncabezado.Location  = new System.Drawing.Point(104, 110);
            this.nudAltEncabezado.Maximum   = new decimal(new int[] { 200, 0, 0, 0 });
            this.nudAltEncabezado.Minimum   = new decimal(new int[] { 20, 0, 0, 0 });
            this.nudAltEncabezado.Name      = "nudAltEncabezado";
            this.nudAltEncabezado.Size      = new System.Drawing.Size(70, 23);
            this.nudAltEncabezado.Value     = new decimal(new int[] { 60, 0, 0, 0 });
            this.nudAltEncabezado.ValueChanged += new System.EventHandler(this.nudAltEncabezado_ValueChanged);

            this.grpEncabezado.Controls.Add(this.panelPreviewEnc);
            this.grpEncabezado.Controls.Add(this.lblZonasEnc);
            this.grpEncabezado.Controls.Add(this.btnEncIzq);
            this.grpEncabezado.Controls.Add(this.btnEncCen);
            this.grpEncabezado.Controls.Add(this.btnEncDer);
            this.grpEncabezado.Controls.Add(this.lblAltEnc);
            this.grpEncabezado.Controls.Add(this.nudAltEncabezado);

            // ── grpPie ───────────────────────────────────────────────────────
            this.grpPie.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpPie.Location = new System.Drawing.Point(8, 148);
            this.grpPie.Name     = "grpPie";
            this.grpPie.Size     = new System.Drawing.Size(420, 130);
            this.grpPie.Text     = "Pie de Página";

            this.panelPreviewPie.BackColor   = System.Drawing.Color.White;
            this.panelPreviewPie.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPreviewPie.Location    = new System.Drawing.Point(10, 22);
            this.panelPreviewPie.Name        = "panelPreviewPie";
            this.panelPreviewPie.Size        = new System.Drawing.Size(400, 60);
            this.panelPreviewPie.Paint      += new System.Windows.Forms.PaintEventHandler(this.panelPreviewPie_Paint);

            this.lblZonasPie.Font     = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblZonasPie.Location = new System.Drawing.Point(10, 88);
            this.lblZonasPie.Name     = "lblZonasPie";
            this.lblZonasPie.Size     = new System.Drawing.Size(80, 20);
            this.lblZonasPie.Text     = "Editar zona:";

            this.btnPieIzq.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.btnPieIzq.Location  = new System.Drawing.Point(94, 86);
            this.btnPieIzq.Name      = "btnPieIzq";
            this.btnPieIzq.Size      = new System.Drawing.Size(80, 24);
            this.btnPieIzq.Text      = "Izquierda";
            this.btnPieIzq.Click    += new System.EventHandler(this.btnPieIzq_Click);

            this.btnPieCen.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.btnPieCen.Location  = new System.Drawing.Point(180, 86);
            this.btnPieCen.Name      = "btnPieCen";
            this.btnPieCen.Size      = new System.Drawing.Size(80, 24);
            this.btnPieCen.Text      = "Centro";
            this.btnPieCen.Click    += new System.EventHandler(this.btnPieCen_Click);

            this.btnPieDer.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.btnPieDer.Location  = new System.Drawing.Point(266, 86);
            this.btnPieDer.Name      = "btnPieDer";
            this.btnPieDer.Size      = new System.Drawing.Size(80, 24);
            this.btnPieDer.Text      = "Derecha";
            this.btnPieDer.Click    += new System.EventHandler(this.btnPieDer_Click);

            this.lblAltPie.Font     = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblAltPie.Location = new System.Drawing.Point(10, 112);
            this.lblAltPie.Name     = "lblAltPie";
            this.lblAltPie.Size     = new System.Drawing.Size(90, 20);
            this.lblAltPie.Text     = "Altura (pts):";

            this.nudAltPie.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.nudAltPie.Location  = new System.Drawing.Point(104, 110);
            this.nudAltPie.Maximum   = new decimal(new int[] { 120, 0, 0, 0 });
            this.nudAltPie.Minimum   = new decimal(new int[] { 10, 0, 0, 0 });
            this.nudAltPie.Name      = "nudAltPie";
            this.nudAltPie.Size      = new System.Drawing.Size(70, 23);
            this.nudAltPie.Value     = new decimal(new int[] { 40, 0, 0, 0 });
            this.nudAltPie.ValueChanged += new System.EventHandler(this.nudAltPie_ValueChanged);

            this.grpPie.Controls.Add(this.panelPreviewPie);
            this.grpPie.Controls.Add(this.lblZonasPie);
            this.grpPie.Controls.Add(this.btnPieIzq);
            this.grpPie.Controls.Add(this.btnPieCen);
            this.grpPie.Controls.Add(this.btnPieDer);
            this.grpPie.Controls.Add(this.lblAltPie);
            this.grpPie.Controls.Add(this.nudAltPie);

            // ── grpEditorZona ────────────────────────────────────────────────
            this.grpEditorZona.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpEditorZona.Location = new System.Drawing.Point(438, 8);
            this.grpEditorZona.Name     = "grpEditorZona";
            this.grpEditorZona.Size     = new System.Drawing.Size(440, 270);
            this.grpEditorZona.Text     = "Contenido de la Zona";

            this.lblTipoZona.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.lblTipoZona.Location = new System.Drawing.Point(10, 24);
            this.lblTipoZona.Name     = "lblTipoZona";
            this.lblTipoZona.Size     = new System.Drawing.Size(80, 20);
            this.lblTipoZona.Text     = "Tipo:";

            this.cboTipoZona.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTipoZona.Font          = new System.Drawing.Font("Segoe UI", 9F);
            this.cboTipoZona.Items.AddRange(new object[] { "Texto", "Imagen" });
            this.cboTipoZona.Location      = new System.Drawing.Point(95, 22);
            this.cboTipoZona.Name          = "cboTipoZona";
            this.cboTipoZona.Size          = new System.Drawing.Size(120, 23);
            this.cboTipoZona.SelectedIndex = 0;
            this.cboTipoZona.SelectedIndexChanged += new System.EventHandler(this.cboTipoZona_SelectedIndexChanged);

            // panelTexto
            this.panelTexto.Location = new System.Drawing.Point(8, 52);
            this.panelTexto.Name     = "panelTexto";
            this.panelTexto.Size     = new System.Drawing.Size(424, 210);
            this.panelTexto.Visible  = true;

            this.lblContenido.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.lblContenido.Location = new System.Drawing.Point(0, 0);
            this.lblContenido.Name     = "lblContenido";
            this.lblContenido.Size     = new System.Drawing.Size(200, 20);
            this.lblContenido.Text     = "Texto (use {campo} para datos dinámicos):";

            this.txtContenidoZona.Font          = new System.Drawing.Font("Segoe UI", 9F);
            this.txtContenidoZona.Location      = new System.Drawing.Point(0, 22);
            this.txtContenidoZona.Multiline     = true;
            this.txtContenidoZona.Name          = "txtContenidoZona";
            this.txtContenidoZona.ScrollBars    = System.Windows.Forms.ScrollBars.Vertical;
            this.txtContenidoZona.Size          = new System.Drawing.Size(424, 50);
            this.txtContenidoZona.TextChanged  += new System.EventHandler(this.txtContenidoZona_TextChanged);

            this.lblCamposDisp.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.lblCamposDisp.Location = new System.Drawing.Point(0, 78);
            this.lblCamposDisp.Name     = "lblCamposDisp";
            this.lblCamposDisp.Size     = new System.Drawing.Size(200, 20);
            this.lblCamposDisp.Text     = "Campos disponibles:";

            this.lstCampos.Font            = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lstCampos.Location        = new System.Drawing.Point(0, 98);
            this.lstCampos.Name            = "lstCampos";
            this.lstCampos.Size            = new System.Drawing.Size(424, 80);

            this.btnInsertarCampo.Font      = new System.Drawing.Font("Segoe UI", 9F);
            this.btnInsertarCampo.Location  = new System.Drawing.Point(0, 182);
            this.btnInsertarCampo.Name      = "btnInsertarCampo";
            this.btnInsertarCampo.Size      = new System.Drawing.Size(150, 26);
            this.btnInsertarCampo.Text      = "Insertar campo →";
            this.btnInsertarCampo.Click    += new System.EventHandler(this.btnInsertarCampo_Click);

            this.panelTexto.Controls.Add(this.lblContenido);
            this.panelTexto.Controls.Add(this.txtContenidoZona);
            this.panelTexto.Controls.Add(this.lblCamposDisp);
            this.panelTexto.Controls.Add(this.lstCampos);
            this.panelTexto.Controls.Add(this.btnInsertarCampo);

            // panelImagen
            this.panelImagen.Location = new System.Drawing.Point(8, 52);
            this.panelImagen.Name     = "panelImagen";
            this.panelImagen.Size     = new System.Drawing.Size(424, 80);
            this.panelImagen.Visible  = false;

            this.lblRutaImagen.Font       = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblRutaImagen.ForeColor  = System.Drawing.Color.Gray;
            this.lblRutaImagen.Location   = new System.Drawing.Point(0, 0);
            this.lblRutaImagen.Name       = "lblRutaImagen";
            this.lblRutaImagen.Size       = new System.Drawing.Size(424, 40);
            this.lblRutaImagen.Text       = "(ninguna imagen seleccionada)";

            this.btnSeleccionarImagen.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.btnSeleccionarImagen.Location = new System.Drawing.Point(0, 46);
            this.btnSeleccionarImagen.Name     = "btnSeleccionarImagen";
            this.btnSeleccionarImagen.Size     = new System.Drawing.Size(160, 26);
            this.btnSeleccionarImagen.Text     = "Seleccionar imagen...";
            this.btnSeleccionarImagen.Click   += new System.EventHandler(this.btnSeleccionarImagen_Click);

            this.panelImagen.Controls.Add(this.lblRutaImagen);
            this.panelImagen.Controls.Add(this.btnSeleccionarImagen);

            this.grpEditorZona.Controls.Add(this.lblTipoZona);
            this.grpEditorZona.Controls.Add(this.cboTipoZona);
            this.grpEditorZona.Controls.Add(this.panelTexto);
            this.grpEditorZona.Controls.Add(this.panelImagen);

            // ── grpFormato ───────────────────────────────────────────────────
            this.grpFormato.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpFormato.Location = new System.Drawing.Point(438, 288);
            this.grpFormato.Name     = "grpFormato";
            this.grpFormato.Size     = new System.Drawing.Size(440, 100);
            this.grpFormato.Text     = "Formato de la Zona";

            this.lblFuente.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.lblFuente.Location = new System.Drawing.Point(10, 24);
            this.lblFuente.Name     = "lblFuente";
            this.lblFuente.Size     = new System.Drawing.Size(50, 20);
            this.lblFuente.Text     = "Fuente:";

            this.cboFuente.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.cboFuente.Location = new System.Drawing.Point(64, 22);
            this.cboFuente.Name     = "cboFuente";
            this.cboFuente.Size     = new System.Drawing.Size(160, 23);
            this.cboFuente.Items.AddRange(new object[] {
                "Segoe UI", "Arial", "Calibri", "Times New Roman",
                "Helvetica", "Verdana", "Tahoma", "Courier New" });
            this.cboFuente.SelectedIndex = 0;
            this.cboFuente.SelectedIndexChanged += new System.EventHandler(this.cboFuente_SelectedIndexChanged);

            this.lblTamaño.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.lblTamaño.Location = new System.Drawing.Point(234, 24);
            this.lblTamaño.Name     = "lblTamaño";
            this.lblTamaño.Size     = new System.Drawing.Size(35, 20);
            this.lblTamaño.Text     = "Tam:";

            this.nudTamaño.DecimalPlaces = 1;
            this.nudTamaño.Font          = new System.Drawing.Font("Segoe UI", 9F);
            this.nudTamaño.Increment     = new decimal(new int[] { 5, 0, 0, 65536 });
            this.nudTamaño.Location      = new System.Drawing.Point(272, 22);
            this.nudTamaño.Maximum       = new decimal(new int[] { 36, 0, 0, 0 });
            this.nudTamaño.Minimum       = new decimal(new int[] { 6, 0, 0, 0 });
            this.nudTamaño.Name          = "nudTamaño";
            this.nudTamaño.Size          = new System.Drawing.Size(65, 23);
            this.nudTamaño.Value         = new decimal(new int[] { 9, 0, 0, 0 });
            this.nudTamaño.ValueChanged += new System.EventHandler(this.nudTamaño_ValueChanged);

            this.chkNegrita.AutoSize  = true;
            this.chkNegrita.Font      = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.chkNegrita.Location  = new System.Drawing.Point(10, 56);
            this.chkNegrita.Name      = "chkNegrita";
            this.chkNegrita.Size      = new System.Drawing.Size(70, 19);
            this.chkNegrita.Text      = "Negrita";
            this.chkNegrita.CheckedChanged += new System.EventHandler(this.chkNegrita_CheckedChanged);

            this.chkCursiva.AutoSize  = true;
            this.chkCursiva.Font      = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
            this.chkCursiva.Location  = new System.Drawing.Point(90, 56);
            this.chkCursiva.Name      = "chkCursiva";
            this.chkCursiva.Size      = new System.Drawing.Size(65, 19);
            this.chkCursiva.Text      = "Cursiva";
            this.chkCursiva.CheckedChanged += new System.EventHandler(this.chkCursiva_CheckedChanged);

            this.lblAlineacion.Font     = new System.Drawing.Font("Segoe UI", 9F);
            this.lblAlineacion.Location = new System.Drawing.Point(170, 56);
            this.lblAlineacion.Name     = "lblAlineacion";
            this.lblAlineacion.Size     = new System.Drawing.Size(65, 20);
            this.lblAlineacion.Text     = "Alineación:";

            this.cboAlineacion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboAlineacion.Font          = new System.Drawing.Font("Segoe UI", 9F);
            this.cboAlineacion.Items.AddRange(new object[] { "Izquierda", "Centro", "Derecha" });
            this.cboAlineacion.Location      = new System.Drawing.Point(238, 54);
            this.cboAlineacion.Name          = "cboAlineacion";
            this.cboAlineacion.Size          = new System.Drawing.Size(110, 23);
            this.cboAlineacion.SelectedIndex = 0;
            this.cboAlineacion.SelectedIndexChanged += new System.EventHandler(this.cboAlineacion_SelectedIndexChanged);

            this.grpFormato.Controls.Add(this.lblFuente);
            this.grpFormato.Controls.Add(this.cboFuente);
            this.grpFormato.Controls.Add(this.lblTamaño);
            this.grpFormato.Controls.Add(this.nudTamaño);
            this.grpFormato.Controls.Add(this.chkNegrita);
            this.grpFormato.Controls.Add(this.chkCursiva);
            this.grpFormato.Controls.Add(this.lblAlineacion);
            this.grpFormato.Controls.Add(this.cboAlineacion);

            // ── tabDatos ─────────────────────────────────────────────────────
            this.tabDatos.Text    = "Datos del Proyecto";
            this.tabDatos.Padding = new System.Windows.Forms.Padding(6);
            this.tabDatos.Controls.Add(this.grpDatosProyecto);

            this.grpDatosProyecto.Font     = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpDatosProyecto.Location = new System.Drawing.Point(8, 8);
            this.grpDatosProyecto.Name     = "grpDatosProyecto";
            this.grpDatosProyecto.Size     = new System.Drawing.Size(860, 460);
            this.grpDatosProyecto.Text     = "Valores de los campos dinámicos {campo}";

            this.lblCamposInfo.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
            this.lblCamposInfo.ForeColor = System.Drawing.Color.FromArgb(30, 136, 229);
            this.lblCamposInfo.Location  = new System.Drawing.Point(10, 24);
            this.lblCamposInfo.Name      = "lblCamposInfo";
            this.lblCamposInfo.Size      = new System.Drawing.Size(830, 36);
            this.lblCamposInfo.Text      = "Estos valores se sustituirán automáticamente en el encabezado y pie de página. " +
                                           "Los campos como {nombre_proyecto} y {fecha_inicio} se toman directamente del proyecto.";

            void AddField(System.Windows.Forms.Label lbl, System.Windows.Forms.TextBox txt,
                          string labelText, System.EventHandler handler, int y)
            {
                lbl.Font     = new System.Drawing.Font("Segoe UI", 9F);
                lbl.Location = new System.Drawing.Point(10, y);
                lbl.Size     = new System.Drawing.Size(130, 20);
                lbl.Text     = labelText;
                txt.Font     = new System.Drawing.Font("Segoe UI", 9F);
                txt.Location = new System.Drawing.Point(144, y - 2);
                txt.Size     = new System.Drawing.Size(300, 23);
                txt.TextChanged += handler;
                this.grpDatosProyecto.Controls.Add(lbl);
                this.grpDatosProyecto.Controls.Add(txt);
            }

            AddField(lblElabaro,      txtElabaro,      "Elaboró {elaboro}:",          new System.EventHandler(txtElabaro_TextChanged),      68);
            AddField(lblReviso,       txtReviso,        "Revisó {reviso}:",            new System.EventHandler(txtReviso_TextChanged),        98);
            AddField(lblAutorizo,     txtAutorizo,      "Autorizó {autorizo}:",        new System.EventHandler(txtAutorizo_TextChanged),      128);
            AddField(lblDependencia,  txtDependencia,   "Dependencia {dependencia}:",  new System.EventHandler(txtDependencia_TextChanged),   158);
            AddField(lblNumContrato,  txtNumContrato,   "N° contrato {numero_contrato}:", new System.EventHandler(txtNumContrato_TextChanged),188);
            AddField(lblLicitacion,   txtLicitacion,    "Licitación {licitacion}:",    new System.EventHandler(txtLicitacion_TextChanged),    218);
            AddField(lblTextoLibre1,  txtTextoLibre1,   "Texto libre 1 {texto1}:",     new System.EventHandler(txtTextoLibre1_TextChanged),   248);
            AddField(lblTextoLibre2,  txtTextoLibre2,   "Texto libre 2 {texto2}:",     new System.EventHandler(txtTextoLibre2_TextChanged),   278);

            this.grpDatosProyecto.Controls.Add(this.lblCamposInfo);

            // ── panelBottom ──────────────────────────────────────────────────
            this.panelBottom.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            this.panelBottom.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.panelBottom.Name      = "panelBottom";

            this.btnGuardar.BackColor = System.Drawing.Color.FromArgb(21, 101, 192);
            this.btnGuardar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnGuardar.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnGuardar.ForeColor = System.Drawing.Color.White;
            this.btnGuardar.Location  = new System.Drawing.Point(660, 10);
            this.btnGuardar.Name      = "btnGuardar";
            this.btnGuardar.Size      = new System.Drawing.Size(110, 36);
            this.btnGuardar.Text      = "Guardar";
            this.btnGuardar.Click    += new System.EventHandler(this.btnGuardar_Click);

            this.btnCerrar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCerrar.Font      = new System.Drawing.Font("Segoe UI", 10F);
            this.btnCerrar.Location  = new System.Drawing.Point(780, 10);
            this.btnCerrar.Name      = "btnCerrar";
            this.btnCerrar.Size      = new System.Drawing.Size(100, 36);
            this.btnCerrar.Text      = "Cerrar";
            this.btnCerrar.Click    += new System.EventHandler(this.btnCerrar_Click);

            this.panelBottom.Controls.Add(this.btnGuardar);
            this.panelBottom.Controls.Add(this.btnCerrar);

            // ── Form ─────────────────────────────────────────────────────────
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(900, 670);
            this.Controls.Add(this.splitMain);
            this.Font                = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize         = new System.Drawing.Size(920, 710);
            this.Name                = "FormPlantillaReporte";
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text                = "Configuracion de Plantilla de Reporte";

            this.ResumeLayout(false);
        }
        #endregion

        private System.Windows.Forms.SplitContainer    splitMain;
        private System.Windows.Forms.TabControl        tabConfig;
        private System.Windows.Forms.TabPage           tabEncPie, tabDatos;

        private System.Windows.Forms.GroupBox          grpEncabezado, grpPie, grpEditorZona, grpFormato, grpDatosProyecto;
        private System.Windows.Forms.Panel             panelPreviewEnc, panelPreviewPie, panelTexto, panelImagen, panelBottom;

        private System.Windows.Forms.Label             lblZonasEnc, lblAltEnc, lblZonasPie, lblAltPie;
        private System.Windows.Forms.Label             lblTipoZona, lblContenido, lblCamposDisp, lblRutaImagen;
        private System.Windows.Forms.Label             lblFuente, lblTamaño, lblAlineacion, lblCamposInfo;
        private System.Windows.Forms.Label             lblElabaro, lblReviso, lblAutorizo, lblDependencia;
        private System.Windows.Forms.Label             lblNumContrato, lblLicitacion, lblTextoLibre1, lblTextoLibre2;

        private System.Windows.Forms.Button            btnEncIzq, btnEncCen, btnEncDer;
        private System.Windows.Forms.Button            btnPieIzq, btnPieCen, btnPieDer;
        private System.Windows.Forms.Button            btnInsertarCampo, btnSeleccionarImagen;
        private System.Windows.Forms.Button            btnGuardar, btnCerrar;

        private System.Windows.Forms.NumericUpDown     nudAltEncabezado, nudAltPie, nudTamaño;
        private System.Windows.Forms.ComboBox          cboTipoZona, cboFuente, cboAlineacion;
        private System.Windows.Forms.TextBox           txtContenidoZona;
        private System.Windows.Forms.TextBox           txtElabaro, txtReviso, txtAutorizo, txtDependencia;
        private System.Windows.Forms.TextBox           txtNumContrato, txtLicitacion, txtTextoLibre1, txtTextoLibre2;
        private System.Windows.Forms.CheckBox          chkNegrita, chkCursiva;
        private System.Windows.Forms.ListBox           lstCampos;
    }
}
