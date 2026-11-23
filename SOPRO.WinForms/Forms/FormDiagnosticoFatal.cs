using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Diálogo fatal de diagnóstico: informa el cierre inesperado, muestra el
    /// código de referencia (copiable) y da acceso a la carpeta de registros
    /// locales. Code-only (sin diseñador). No cierra la aplicación: el
    /// coordinador de diagnóstico decide el final del proceso.
    /// </summary>
    public sealed class FormDiagnosticoFatal : Form
    {
        private readonly Action<string> _abrirCarpeta;

        private readonly Panel panelHeader;
        private readonly Label lblTitulo;
        private readonly Label lblMensaje;
        private readonly Label lblDetalle;
        private readonly Label lblCaptionCodigo;
        private readonly TextBox txtCodigo;
        private readonly Label lblCaptionRegistros;
        private readonly TextBox txtRegistros;
        private readonly Panel panelPie;
        private readonly SoproButton btnAbrirCarpeta;
        private readonly SoproButton btnCerrar;

        private string _textoTitulo = "Error grave";
        private string _mensajePrincipal = "SOPRO encontró un problema grave y debe cerrarse.";
        private string _mensajeAyuda = "Consulta los registros locales si necesitas compartir información con soporte.";
        private string _textoCaptionCodigo = "Código de referencia";
        private string _textoCaptionRegistros = "Registros locales";
        private string _textoBotonAbrirCarpeta = "Abrir carpeta de registros";
        private string _textoBotonCerrar = "Cerrar aplicación";
        private string _codigoReferencia;
        private string _carpetaRegistros;

        public FormDiagnosticoFatal(string? codigoReferencia, string? carpetaRegistros, Action<string>? abrirCarpeta)
        {
            _codigoReferencia = codigoReferencia ?? string.Empty;
            _carpetaRegistros = carpetaRegistros ?? string.Empty;
            _abrirCarpeta = abrirCarpeta ?? CrearAccionAbrirCarpetaPorDefecto();

            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(500, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            SoproDialogTheme.AplicarIconoSOPRO(this);

            SuspendLayout();

            panelHeader = CrearPanelHeader();
            lblTitulo = CrearTituloHeader();
            panelHeader.Controls.Add(lblTitulo);

            lblMensaje = CrearMensajePrincipal();
            lblDetalle = CrearMensajeAyuda();
            lblCaptionCodigo = CrearCaption(_textoCaptionCodigo, new Point(24, 94));
            txtCodigo = CrearCampoCopiable(new Point(24, 112));
            lblCaptionRegistros = CrearCaption(_textoCaptionRegistros, new Point(24, 149));
            txtRegistros = CrearCampoCopiable(new Point(24, 167));

            panelPie = CrearPanelPie();
            btnAbrirCarpeta = new SoproButton();
            ConfigurarBoton(btnAbrirCarpeta, SoproIconType.Carpeta, primario: false);
            btnCerrar = new SoproButton();
            ConfigurarBoton(btnCerrar, null, primario: true);
            btnAbrirCarpeta.Click += AlHacerClicAbrirCarpeta;
            btnCerrar.Click += (_, __) => Close();
            panelPie.Controls.Add(btnAbrirCarpeta);
            panelPie.Controls.Add(btnCerrar);
            panelPie.Resize += (_, __) => AlinearBotonesPie();
            CancelButton = btnCerrar;
            AcceptButton = btnCerrar;

            var panelCuerpo = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SoproTheme.Colors.CenterBg
            };
            panelCuerpo.Controls.AddRange(new Control[]
            {
                lblMensaje, lblDetalle, lblCaptionCodigo, txtCodigo, lblCaptionRegistros, txtRegistros
            });

            Controls.Add(panelCuerpo);
            Controls.Add(panelHeader);
            Controls.Add(panelPie);

            AplicarTextos();
            AlinearBotonesPie();
            ResumeLayout();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string TextoTitulo
        {
            get => _textoTitulo;
            set { _textoTitulo = value ?? string.Empty; AplicarTextos(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string MensajePrincipal
        {
            get => _mensajePrincipal;
            set { _mensajePrincipal = value ?? string.Empty; AplicarTextos(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string MensajeAyuda
        {
            get => _mensajeAyuda;
            set { _mensajeAyuda = value ?? string.Empty; AplicarTextos(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string TextoCaptionCodigo
        {
            get => _textoCaptionCodigo;
            set { _textoCaptionCodigo = value ?? string.Empty; AplicarTextos(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string TextoCaptionRegistros
        {
            get => _textoCaptionRegistros;
            set { _textoCaptionRegistros = value ?? string.Empty; AplicarTextos(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string TextoBotonAbrirCarpeta
        {
            get => _textoBotonAbrirCarpeta;
            set { _textoBotonAbrirCarpeta = value ?? string.Empty; AplicarTextos(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string TextoBotonCerrar
        {
            get => _textoBotonCerrar;
            set { _textoBotonCerrar = value ?? string.Empty; AplicarTextos(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string CodigoReferencia
        {
            get => _codigoReferencia;
            set { _codigoReferencia = value ?? string.Empty; AplicarTextos(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string CarpetaRegistros
        {
            get => _carpetaRegistros;
            set { _carpetaRegistros = value ?? string.Empty; AplicarTextos(); }
        }

        internal static Action<string> CrearAccionAbrirCarpetaPorDefecto() => ruta =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
            }
            catch
            {
                // El fallo al abrir el explorador no debe alterar el cierre.
            }
        };

        private Panel CrearPanelHeader()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = SoproTheme.Colors.HeaderDark
            };
            panel.Paint += PintarHeader;
            return panel;
        }

        private Label CrearTituloHeader()
        {
            return new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = SoproTheme.Colors.Surface,
                Font = SoproTheme.Fonts.DialogTitle,
                Location = new Point(20, 16),
                Text = _textoTitulo
            };
        }

        private Label CrearMensajePrincipal()
        {
            return new Label
            {
                AutoSize = false,
                Location = new Point(24, 22),
                Size = new Size(452, 22),
                ForeColor = SoproTheme.Colors.Ink,
                Font = SoproTheme.Fonts.DialogBody,
                Text = _mensajePrincipal
            };
        }

        private Label CrearMensajeAyuda()
        {
            return new Label
            {
                AutoSize = false,
                Location = new Point(24, 52),
                Size = new Size(452, 34),
                ForeColor = SoproTheme.Colors.Muted,
                Font = SoproTheme.Fonts.Subtitle,
                Text = _mensajeAyuda
            };
        }

        private Label CrearCaption(string texto, Point ubicacion)
        {
            return new Label
            {
                AutoSize = true,
                Location = ubicacion,
                ForeColor = SoproTheme.Colors.Muted,
                Font = SoproTheme.Fonts.RibbonGroupCaption,
                Text = texto
            };
        }

        private TextBox CrearCampoCopiable(Point ubicacion)
        {
            return new TextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SoproTheme.Colors.Surface,
                ForeColor = SoproTheme.Colors.Ink2,
                Font = SoproTheme.Fonts.GridCell,
                Location = ubicacion,
                Size = new Size(452, 23),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
        }

        private Panel CrearPanelPie()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = SoproTheme.Colors.HeaderBg
            };
            panel.Paint += PintarPie;
            return panel;
        }

        private void ConfigurarBoton(SoproButton button, SoproIconType? icono, bool primario)
        {
            button.AutoSize = false;
            button.Cursor = Cursors.Hand;
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            // Icono y texto centrados como UN bloque (mismo ajuste que los
            // demas dialogos SOPRO): el Inline nativo recorta el texto largo.
            button.SoproLayout = SoproButtonLayout.Small;
            button.Font = SoproTheme.Fonts.Button;
            button.SoproContentPadding = new Padding(16, 0, 16, 0);
            button.SoproIconTextSpacing = 8;
            button.SoproIconSize = 16;
            button.SoproIcon = icono;
            button.SoproFixedHeight = 34;
            button.SoproMinimumAutoWidth = 110;

            if (primario)
            {
                button.ForeColor = SoproTheme.Colors.Surface;
                button.BackColor = SoproTheme.Colors.RibbonActive;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = SoproTheme.Colors.Brand;
                button.FlatAppearance.MouseDownBackColor = SoproTheme.Colors.RibbonDown;
            }
            else
            {
                button.ForeColor = SoproTheme.Colors.Ink2;
                button.BackColor = SoproTheme.Colors.Surface;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = SoproTheme.Colors.SurfaceBorder;
                button.FlatAppearance.MouseOverBackColor = SoproTheme.Colors.HoverBg;
                button.FlatAppearance.MouseDownBackColor = SoproTheme.Colors.SelBg;
            }

            button.SoproAutoSizeToContent = true;
            // El layout Small no recalcula el ancho automaticamente: el cromo
            // compartido fija el ancho al contenido con la misma medicion.
            SoproDialogTheme.AjustarAnchoContenido(button);
            SoproTheme.ApplyRoundedCorners(button, SoproUiMetrics.ThemeRadiusPx);
            button.SizeChanged += (_, __) => AlinearBotonesPie();
        }

        private void AplicarTextos()
        {
            Text = _textoTitulo;
            lblTitulo.Text = _textoTitulo;
            lblMensaje.Text = _mensajePrincipal;
            lblDetalle.Text = _mensajeAyuda;
            lblCaptionCodigo.Text = _textoCaptionCodigo;
            txtCodigo.Text = _codigoReferencia;
            lblCaptionRegistros.Text = _textoCaptionRegistros;
            txtRegistros.Text = _carpetaRegistros;
            btnAbrirCarpeta.Text = _textoBotonAbrirCarpeta;
            btnCerrar.Text = _textoBotonCerrar;
        }

        private void AlinearBotonesPie()
        {
            if (panelPie.IsDisposed || btnCerrar == null || btnAbrirCarpeta == null) return;

            const int margenDerecho = 16;
            const int separacion = 8;

            int y = (panelPie.Height - btnCerrar.Height) / 2;
            int xCerrar = panelPie.ClientSize.Width - margenDerecho - btnCerrar.Width;
            int xAbrir = xCerrar - separacion - btnAbrirCarpeta.Width;

            btnCerrar.Location = new Point(xCerrar, y);
            btnAbrirCarpeta.Location = new Point(xAbrir, y);
        }

        private void AlHacerClicAbrirCarpeta(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_carpetaRegistros)) return;

            try
            {
                _abrirCarpeta(_carpetaRegistros);
            }
            catch
            {
                // La acción inyectada ya traga errores; esto evita cualquier fuga.
            }
        }

        private void PintarHeader(object? sender, PaintEventArgs e)
        {
            var rect = panelHeader.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var brush = new SolidBrush(SoproTheme.Colors.HeaderDark))
                e.Graphics.FillRectangle(brush, rect);

            using var pen = new Pen(SoproTheme.Colors.HeaderBottom);
            e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
        }

        private void PintarPie(object? sender, PaintEventArgs e)
        {
            var rect = panelPie.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var brush = new SolidBrush(SoproTheme.Colors.HeaderBg))
                e.Graphics.FillRectangle(brush, rect);

            using var pen = new Pen(SoproTheme.Colors.SurfaceBorder);
            e.Graphics.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Top);
        }
    }
}
