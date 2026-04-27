// ============================================================
// UcDisenador.Designer.cs — NO modificar manualmente.
// ============================================================
namespace SOPRO.WinForms.Forms
{
    partial class UcDisenador
    {
        private System.ComponentModel.IContainer components = null;

        // ── Toolbar ───────────────────────────────────────────
        private System.Windows.Forms.ToolStrip                tsToolbar;
        private System.Windows.Forms.ToolStripDropDownButton  tsDdZona;
        private System.Windows.Forms.ToolStripMenuItem        tsMenuEncabezado;
        private System.Windows.Forms.ToolStripMenuItem        tsMenuPie;
        private System.Windows.Forms.ToolStripSeparator       tsSep1;
        private System.Windows.Forms.ToolStripLabel           tsLblSecInsertar;
        private System.Windows.Forms.ToolStripButton          tsBtnTexto;
        private System.Windows.Forms.ToolStripButton          tsBtnEtiqueta;
        private System.Windows.Forms.ToolStripButton          tsBtnImagen;
        private System.Windows.Forms.ToolStripSeparator       tsSep2;
        private System.Windows.Forms.ToolStripDropDownButton  tsDdEditar;
        private System.Windows.Forms.ToolStripMenuItem        tsMenuDuplicar;
        private System.Windows.Forms.ToolStripMenuItem        tsMenuCopiar;
        private System.Windows.Forms.ToolStripMenuItem        tsMenuPegar;
        private System.Windows.Forms.ToolStripMenuItem        tsMenuEliminar;
        private System.Windows.Forms.ToolStripSeparator       tsSep3;
        private System.Windows.Forms.ToolStripDropDownButton  tsDdOrden;
        private System.Windows.Forms.ToolStripMenuItem        tsMenuAlFrente;
        private System.Windows.Forms.ToolStripMenuItem        tsMenuAtras;
        private System.Windows.Forms.ToolStripSeparator       tsSep4;
        private System.Windows.Forms.ToolStripButton          tsBtnSnap;

        // ── Layout ────────────────────────────────────────────
        private System.Windows.Forms.Panel              pnlMain;
        private System.Windows.Forms.Panel              pnlVista;
        private System.Windows.Forms.Panel              pnlDerecho;
        private System.Windows.Forms.Panel              pnlContenedor;

        // ── Canvas ────────────────────────────────────────────
        private System.Windows.Forms.Label              lblFranjaEnc;
        private System.Windows.Forms.Label              lblIndicadorEnc;
        private SOPRO.WinForms.Forms.Disenador.CanvasPanel cvEnc;
        private SOPRO.WinForms.Forms.Disenador.DivisorFranja divEnc;
        private System.Windows.Forms.Panel              pnlDetalle;
        private System.Windows.Forms.Label              lblFranjaPie;
        private System.Windows.Forms.Label              lblIndicadorPie;
        private SOPRO.WinForms.Forms.Disenador.CanvasPanel cvPie;
        private SOPRO.WinForms.Forms.Disenador.DivisorFranja divPie;

        // ── Propiedades ───────────────────────────────────────
        private System.Windows.Forms.Panel              pnlProps;
        private System.Windows.Forms.Panel              pnlPropsHeader;
        private System.Windows.Forms.Label              lblPropsTitle;
        private System.Windows.Forms.Label              lblSinSeleccion;
        private System.Windows.Forms.Panel              pnlCampos;
        private System.Windows.Forms.Label              lblContenido;
        private System.Windows.Forms.TextBox            txtContenido;
        private System.Windows.Forms.Label              lblFuente;
        private System.Windows.Forms.ComboBox           cboFuente;
        private System.Windows.Forms.Label              lblTamano;
        private System.Windows.Forms.NumericUpDown      numTamano;
        private System.Windows.Forms.CheckBox           chkNegrita;
        private System.Windows.Forms.CheckBox           chkCursiva;
        private System.Windows.Forms.Label              lblColor;
        private System.Windows.Forms.Button             btnColor;
        private System.Windows.Forms.Label              lblAlineacion;
        private System.Windows.Forms.ComboBox           cboAlineacion;
        private System.Windows.Forms.Label              lblSeparador;
        private System.Windows.Forms.Label              lblPosicion;
        private System.Windows.Forms.Label              lblZOrder;

        private void InitializeComponent()
        {
            this.components       = new System.ComponentModel.Container();

            this.tsToolbar        = new System.Windows.Forms.ToolStrip();
            this.tsDdZona         = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsMenuEncabezado = new System.Windows.Forms.ToolStripMenuItem();
            this.tsMenuPie        = new System.Windows.Forms.ToolStripMenuItem();
            this.tsSep1           = new System.Windows.Forms.ToolStripSeparator();
            this.tsLblSecInsertar = new System.Windows.Forms.ToolStripLabel();
            this.tsBtnTexto       = new System.Windows.Forms.ToolStripButton();
            this.tsBtnEtiqueta    = new System.Windows.Forms.ToolStripButton();
            this.tsBtnImagen      = new System.Windows.Forms.ToolStripButton();
            this.tsSep2           = new System.Windows.Forms.ToolStripSeparator();
            this.tsDdEditar       = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsMenuDuplicar   = new System.Windows.Forms.ToolStripMenuItem();
            this.tsMenuCopiar     = new System.Windows.Forms.ToolStripMenuItem();
            this.tsMenuPegar      = new System.Windows.Forms.ToolStripMenuItem();
            this.tsMenuEliminar   = new System.Windows.Forms.ToolStripMenuItem();
            this.tsSep3           = new System.Windows.Forms.ToolStripSeparator();
            this.tsDdOrden        = new System.Windows.Forms.ToolStripDropDownButton();
            this.tsMenuAlFrente   = new System.Windows.Forms.ToolStripMenuItem();
            this.tsMenuAtras      = new System.Windows.Forms.ToolStripMenuItem();
            this.tsSep4           = new System.Windows.Forms.ToolStripSeparator();
            this.tsBtnSnap        = new System.Windows.Forms.ToolStripButton();

            this.pnlMain          = new System.Windows.Forms.Panel();
            this.pnlVista         = new System.Windows.Forms.Panel();
            this.pnlDerecho       = new System.Windows.Forms.Panel();
            this.pnlContenedor    = new System.Windows.Forms.Panel();
            this.lblFranjaEnc     = new System.Windows.Forms.Label();
            this.lblIndicadorEnc  = new System.Windows.Forms.Label();
            this.cvEnc            = new SOPRO.WinForms.Forms.Disenador.CanvasPanel();
            this.divEnc           = new SOPRO.WinForms.Forms.Disenador.DivisorFranja();
            this.pnlDetalle       = new System.Windows.Forms.Panel();
            this.lblFranjaPie     = new System.Windows.Forms.Label();
            this.lblIndicadorPie  = new System.Windows.Forms.Label();
            this.cvPie            = new SOPRO.WinForms.Forms.Disenador.CanvasPanel();
            this.divPie           = new SOPRO.WinForms.Forms.Disenador.DivisorFranja();

            this.pnlProps         = new System.Windows.Forms.Panel();
            this.pnlPropsHeader   = new System.Windows.Forms.Panel();
            this.lblPropsTitle    = new System.Windows.Forms.Label();
            this.lblSinSeleccion  = new System.Windows.Forms.Label();
            this.pnlCampos        = new System.Windows.Forms.Panel();
            this.lblContenido     = new System.Windows.Forms.Label();
            this.txtContenido     = new System.Windows.Forms.TextBox();
            this.lblFuente        = new System.Windows.Forms.Label();
            this.cboFuente        = new System.Windows.Forms.ComboBox();
            this.lblTamano        = new System.Windows.Forms.Label();
            this.numTamano        = new System.Windows.Forms.NumericUpDown();
            this.chkNegrita       = new System.Windows.Forms.CheckBox();
            this.chkCursiva       = new System.Windows.Forms.CheckBox();
            this.lblColor         = new System.Windows.Forms.Label();
            this.btnColor         = new System.Windows.Forms.Button();
            this.lblAlineacion    = new System.Windows.Forms.Label();
            this.cboAlineacion    = new System.Windows.Forms.ComboBox();
            this.lblSeparador     = new System.Windows.Forms.Label();
            this.lblPosicion      = new System.Windows.Forms.Label();
            this.lblZOrder        = new System.Windows.Forms.Label();

            this.SuspendLayout();
            this.tsToolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numTamano)).BeginInit();

            // ── ToolStrip ─────────────────────────────────────
            this.tsToolbar.Dock      = System.Windows.Forms.DockStyle.Top;
            this.tsToolbar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.tsToolbar.Height    = 40;
            this.tsToolbar.Name      = "tsToolbar";
            this.tsToolbar.TabStop   = false;

            this.tsMenuEncabezado.Name = "tsMenuEncabezado"; this.tsMenuEncabezado.Text = "\u25b2 Encabezado"; this.tsMenuEncabezado.Click += new System.EventHandler(this.TsBtnEncabezado_Click);
            this.tsMenuPie.Name        = "tsMenuPie";        this.tsMenuPie.Text        = "\u25bc Pie de p\u00e1gina"; this.tsMenuPie.Click += new System.EventHandler(this.TsBtnPie_Click);
            this.tsDdZona.Name = "tsDdZona"; this.tsDdZona.Text = "\u25b2 Encabezado \u25be"; this.tsDdZona.ToolTipText = "Zona activa de edici\u00f3n";
            this.tsDdZona.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.tsMenuEncabezado, this.tsMenuPie });
            this.tsSep1.Name = "tsSep1";

            this.tsLblSecInsertar.Name = "tsLblSecInsertar"; this.tsLblSecInsertar.Text = "INSERTAR:";
            this.tsBtnTexto.Name = "tsBtnTexto"; this.tsBtnTexto.Text = "T Texto"; this.tsBtnTexto.ToolTipText = "Agregar texto libre"; this.tsBtnTexto.Click += new System.EventHandler(this.TsBtnTexto_Click);
            this.tsBtnEtiqueta.Name = "tsBtnEtiqueta"; this.tsBtnEtiqueta.Text = "{ } Etiqueta"; this.tsBtnEtiqueta.ToolTipText = "Agregar etiqueta din\u00e1mica"; this.tsBtnEtiqueta.Click += new System.EventHandler(this.TsBtnEtiqueta_Click);
            this.tsBtnImagen.Name = "tsBtnImagen"; this.tsBtnImagen.Text = "Imagen"; this.tsBtnImagen.ToolTipText = "Insertar imagen"; this.tsBtnImagen.Click += new System.EventHandler(this.TsBtnImagen_Click);
            this.tsSep2.Name = "tsSep2";

            this.tsMenuDuplicar.Name = "tsMenuDuplicar"; this.tsMenuDuplicar.Text = "Duplicar\tCtrl+D"; this.tsMenuDuplicar.Click += new System.EventHandler(this.TsBtnDuplicar_Click);
            this.tsMenuCopiar.Name   = "tsMenuCopiar";   this.tsMenuCopiar.Text   = "Copiar\tCtrl+C";   this.tsMenuCopiar.Click   += new System.EventHandler(this.TsBtnCopiar_Click);
            this.tsMenuPegar.Name    = "tsMenuPegar";    this.tsMenuPegar.Text    = "Pegar\tCtrl+V";    this.tsMenuPegar.Click    += new System.EventHandler(this.TsBtnPegar_Click);
            this.tsMenuEliminar.Name = "tsMenuEliminar"; this.tsMenuEliminar.Text = "Eliminar\tDel";    this.tsMenuEliminar.Click += new System.EventHandler(this.TsBtnEliminar_Click);
            this.tsDdEditar.Name = "tsDdEditar"; this.tsDdEditar.Text = "Editar \u25be"; this.tsDdEditar.ToolTipText = "Duplicar, Copiar, Pegar, Eliminar";
            this.tsDdEditar.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.tsMenuDuplicar, this.tsMenuCopiar, this.tsMenuPegar, this.tsMenuEliminar });
            this.tsSep3.Name = "tsSep3";

            this.tsMenuAlFrente.Name = "tsMenuAlFrente"; this.tsMenuAlFrente.Text = "\u25b2 Traer al frente"; this.tsMenuAlFrente.Click += new System.EventHandler(this.TsBtnAlFrente_Click);
            this.tsMenuAtras.Name    = "tsMenuAtras";    this.tsMenuAtras.Text    = "\u25bc Enviar atr\u00e1s";  this.tsMenuAtras.Click    += new System.EventHandler(this.TsBtnAtras_Click);
            this.tsDdOrden.Name = "tsDdOrden"; this.tsDdOrden.Text = "Orden \u25be"; this.tsDdOrden.ToolTipText = "Capa del elemento";
            this.tsDdOrden.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.tsMenuAlFrente, this.tsMenuAtras });
            this.tsSep4.Name = "tsSep4";

            this.tsBtnSnap.Name = "tsBtnSnap"; this.tsBtnSnap.Text = "Snap \u25a3"; this.tsBtnSnap.ToolTipText = "Ajuste a cuadr\u00edcula (1mm)"; this.tsBtnSnap.CheckOnClick = true; this.tsBtnSnap.Checked = true; this.tsBtnSnap.Click += new System.EventHandler(this.TsBtnSnap_Click);

            this.tsToolbar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tsDdZona, this.tsSep1,
                this.tsLblSecInsertar, this.tsBtnTexto, this.tsBtnEtiqueta, this.tsBtnImagen, this.tsSep2,
                this.tsDdEditar, this.tsSep3,
                this.tsDdOrden, this.tsSep4,
                this.tsBtnSnap });

            // ── Panel props ───────────────────────────────────
            this.pnlPropsHeader.Dock = System.Windows.Forms.DockStyle.Top; this.pnlPropsHeader.Height = 36; this.pnlPropsHeader.Name = "pnlPropsHeader";
            this.lblPropsTitle.Dock = System.Windows.Forms.DockStyle.Fill; this.lblPropsTitle.Name = "lblPropsTitle"; this.lblPropsTitle.Text = "PROPIEDADES"; this.lblPropsTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.pnlPropsHeader.Controls.Add(this.lblPropsTitle);
            this.lblSinSeleccion.Dock = System.Windows.Forms.DockStyle.Fill; this.lblSinSeleccion.Name = "lblSinSeleccion"; this.lblSinSeleccion.Text = "Selecciona un elemento\npara editar sus propiedades."; this.lblSinSeleccion.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.pnlCampos.Dock = System.Windows.Forms.DockStyle.Fill; this.pnlCampos.Name = "pnlCampos"; this.pnlCampos.Padding = new System.Windows.Forms.Padding(12); this.pnlCampos.Visible = false;

            int y2 = 8;
            this.lblContenido.SetBounds(0,y2,224,18); this.lblContenido.Text="Contenido:"; this.lblContenido.Name="lblContenido"; y2+=20;
            this.txtContenido.SetBounds(0,y2,224,56); this.txtContenido.Multiline=true; this.txtContenido.ScrollBars=System.Windows.Forms.ScrollBars.Vertical; this.txtContenido.Name="txtContenido"; this.txtContenido.TextChanged+=new System.EventHandler(this.TxtContenido_TextChanged); y2+=64;
            this.lblFuente.SetBounds(0,y2,76,18); this.lblFuente.Text="Fuente:"; this.lblFuente.Name="lblFuente";
            this.cboFuente.SetBounds(76,y2,144,22); this.cboFuente.DropDownStyle=System.Windows.Forms.ComboBoxStyle.DropDownList; this.cboFuente.Name="cboFuente"; this.cboFuente.Items.AddRange(new object[]{"Segoe UI","Arial","Times New Roman","Calibri","Tahoma","Verdana","Courier New"}); this.cboFuente.SelectedIndex=0; this.cboFuente.SelectedIndexChanged+=new System.EventHandler(this.PropsCambiadas); y2+=28;
            this.lblTamano.SetBounds(0,y2,76,18); this.lblTamano.Text="Tama\u00f1o:"; this.lblTamano.Name="lblTamano";
            this.numTamano.SetBounds(76,y2,72,22); this.numTamano.DecimalPlaces=1; this.numTamano.Increment=new decimal(new int[]{5,0,0,65536}); this.numTamano.Maximum=new decimal(new int[]{72,0,0,0}); this.numTamano.Minimum=new decimal(new int[]{6,0,0,0}); this.numTamano.Value=new decimal(new int[]{10,0,0,0}); this.numTamano.Name="numTamano"; this.numTamano.ValueChanged+=new System.EventHandler(this.PropsCambiadas); y2+=28;
            this.chkNegrita.SetBounds(0,y2,104,22); this.chkNegrita.Text="Negrita"; this.chkNegrita.Name="chkNegrita"; this.chkNegrita.CheckedChanged+=new System.EventHandler(this.PropsCambiadas);
            this.chkCursiva.SetBounds(110,y2,104,22); this.chkCursiva.Text="Cursiva"; this.chkCursiva.Name="chkCursiva"; this.chkCursiva.CheckedChanged+=new System.EventHandler(this.PropsCambiadas); y2+=28;
            this.lblColor.SetBounds(0,y2,88,18); this.lblColor.Text="Color texto:"; this.lblColor.Name="lblColor";
            this.btnColor.SetBounds(88,y2,34,22); this.btnColor.FlatStyle=System.Windows.Forms.FlatStyle.Flat; this.btnColor.Name="btnColor"; this.btnColor.Click+=new System.EventHandler(this.BtnColor_Click); y2+=28;
            this.lblAlineacion.SetBounds(0,y2,88,18); this.lblAlineacion.Text="Alineaci\u00f3n:"; this.lblAlineacion.Name="lblAlineacion";
            this.cboAlineacion.SetBounds(88,y2,128,22); this.cboAlineacion.DropDownStyle=System.Windows.Forms.ComboBoxStyle.DropDownList; this.cboAlineacion.Name="cboAlineacion"; this.cboAlineacion.Items.AddRange(new object[]{"Izquierda","Centro","Derecha","Arr-Izq","Arr-Centro","Arr-Der","Aba-Izq","Aba-Centro","Aba-Der"}); this.cboAlineacion.SelectedIndex=0; this.cboAlineacion.SelectedIndexChanged+=new System.EventHandler(this.PropsCambiadas); y2+=28;
            this.lblSeparador.SetBounds(0,y2,224,1); this.lblSeparador.Name="lblSeparador"; y2+=8;
            this.lblPosicion.SetBounds(0,y2,224,44); this.lblPosicion.Text="Posici\u00f3n: \u2014"; this.lblPosicion.Name="lblPosicion"; y2+=48;
            this.lblZOrder.SetBounds(0,y2,224,20); this.lblZOrder.Text="Capa: \u2014"; this.lblZOrder.Name="lblZOrder";

            this.pnlCampos.Controls.AddRange(new System.Windows.Forms.Control[]{
                this.lblContenido, this.txtContenido, this.lblFuente, this.cboFuente,
                this.lblTamano, this.numTamano, this.chkNegrita, this.chkCursiva,
                this.lblColor, this.btnColor, this.lblAlineacion, this.cboAlineacion,
                this.lblSeparador, this.lblPosicion, this.lblZOrder });

            this.pnlProps.Dock = System.Windows.Forms.DockStyle.Right; this.pnlProps.Width = 260; this.pnlProps.Name = "pnlProps";
            this.pnlProps.Controls.Add(this.pnlCampos); this.pnlProps.Controls.Add(this.lblSinSeleccion); this.pnlProps.Controls.Add(this.pnlPropsHeader);

            // ── Canvas contenedor ─────────────────────────────
            this.cvEnc.Name = "cvEnc"; this.cvEnc.Zona = SOPRO.WinForms.Forms.Disenador.ZonaCanvas.Encabezado;
            this.cvPie.Name = "cvPie"; this.cvPie.Zona = SOPRO.WinForms.Forms.Disenador.ZonaCanvas.PieDePagina;
            this.divEnc.Name = "divEnc"; this.divEnc.DeltaY += new System.Action<int>(this.DivEnc_DeltaY);
            this.divPie.Name = "divPie"; this.divPie.DeltaY += new System.Action<int>(this.DivPie_DeltaY);

            this.lblFranjaEnc.Name = "lblFranjaEnc"; this.lblFranjaEnc.Text = "\u25b2  ENCABEZADO"; this.lblFranjaEnc.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblIndicadorEnc.Name = "lblIndicadorEnc"; this.lblIndicadorEnc.Text = "\u2014"; this.lblIndicadorEnc.TextAlign = System.Drawing.ContentAlignment.MiddleRight; this.lblIndicadorEnc.Size = new System.Drawing.Size(100, 20);
            this.lblFranjaPie.Name = "lblFranjaPie"; this.lblFranjaPie.Text = "\u25bc  PIE DE P\u00c1GINA"; this.lblFranjaPie.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblIndicadorPie.Name = "lblIndicadorPie"; this.lblIndicadorPie.Text = "\u2014"; this.lblIndicadorPie.TextAlign = System.Drawing.ContentAlignment.MiddleRight; this.lblIndicadorPie.Size = new System.Drawing.Size(100, 20);

            this.pnlDetalle.Name = "pnlDetalle"; this.pnlDetalle.Paint += new System.Windows.Forms.PaintEventHandler(this.PnlDetalle_Paint); this.pnlDetalle.MouseDown += new System.Windows.Forms.MouseEventHandler(this.PnlDetalle_MouseDown);

            this.pnlContenedor.Name = "pnlContenedor"; this.pnlContenedor.Paint += new System.Windows.Forms.PaintEventHandler(this.PnlContenedor_Paint);
            this.pnlContenedor.Controls.AddRange(new System.Windows.Forms.Control[]{
                this.lblFranjaEnc, this.lblIndicadorEnc, this.cvEnc, this.divEnc,
                this.pnlDetalle,
                this.lblFranjaPie, this.lblIndicadorPie, this.cvPie, this.divPie });

            this.pnlVista.Dock = System.Windows.Forms.DockStyle.Fill; this.pnlVista.AutoScroll = true; this.pnlVista.Padding = new System.Windows.Forms.Padding(24); this.pnlVista.Name = "pnlVista";
            this.pnlVista.Controls.Add(this.pnlContenedor);

            this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill; this.pnlMain.Name = "pnlMain";
            this.pnlMain.Controls.Add(this.pnlVista);
            this.pnlMain.Controls.Add(this.pnlProps);

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.Name                = "UcDisenador";
            this.Dock                = System.Windows.Forms.DockStyle.Fill;

            this.Controls.Add(this.pnlMain);
            this.Controls.Add(this.tsToolbar);

            this.tsToolbar.ResumeLayout(false); this.tsToolbar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numTamano)).EndInit();
            this.ResumeLayout(false); this.PerformLayout();
        }
    }
}
