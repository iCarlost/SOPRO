using System;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;


namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Aviso de privacidad de los registros de diagnóstico. Code-only (sin
    /// diseñador), con el cromo compartido de dialogos SOPRO. No cambia la
    /// semántica de reconocimiento ni la navegación a Registros locales.
    /// </summary>
    internal sealed class FormAvisoPrivacidadDiagnosticos : Form
    {
        public bool Revisar { get; private set; }
        public bool Reconocido { get; private set; }

        internal const string TextoBotonEntendido = "Entendido";
        internal const string TextoBotonRevisar = "Revisar registros";
        internal const string TextoReconocimiento = "No se suben automáticamente. Todo permanece en este equipo.";
        internal const string TextoDetalle = "SOPRO guarda registros de diagnóstico locales en %LOCALAPPDATA%\\SOPRO\\logs. Cada archivo tiene un límite aproximado de 1 MiB; se conservan como máximo 10 archivos durante 7 días (retención de mejor esfuerzo). Puedes revisarlos, exportarlos o borrarlos manualmente desde Registros locales.";

        private readonly SoproButton _entendido;
        private readonly SoproButton _revisar;
        private readonly Panel _pie;
        private readonly Panel _cuerpo;
        private readonly TableLayoutPanel _encabezado;
        private readonly Label _lblReconocimiento;
        private readonly Label _lblDetalle;

        public FormAvisoPrivacidadDiagnosticos()
        {
            SuspendLayout();
            Text = "Registros locales";
            SoproDialogTheme.AplicarCromoDialogo(this, new Size(540, 300), centroPantalla: true);

            _encabezado = SoproDialogTheme.CrearEncabezado(
                "Registros locales",
                "Cómo SOPRO guarda los registros de diagnóstico");

            _cuerpo = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SoproTheme.Colors.CenterBg,
                Padding = new Padding(SoproDialogTheme.MargenHorizontalContenido, 20, SoproDialogTheme.MargenHorizontalContenido, 12),
                // Respaldo: si el contenido medido no cabe en el área de
                // trabajo (monitores pequeños a DPI altos), el cuerpo se
                // recorre con desplazamiento en lugar de recortarse.
                AutoScroll = true
            };

            // Sin AutoSize: el Label WinForms con AutoSize no envuelve (crece en
            // una sola linea y desborda). El alto se mide con el ancho real
            // disponible y se recalcula en cada cambio de tamano/DPI.
            _lblReconocimiento = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                ForeColor = SoproTheme.Colors.Ink,
                Font = SoproTheme.Fonts.GroupTitle,
                Text = TextoReconocimiento,
                AccessibleName = "Aviso de privacidad de los registros"
            };
            _lblDetalle = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                ForeColor = SoproTheme.Colors.Muted,
                Font = SoproTheme.Fonts.Subtitle,
                Padding = new Padding(0, 8, 0, 0),
                Text = TextoDetalle,
                AccessibleName = "Información sobre registros locales"
            };
            _cuerpo.Controls.Add(_lblDetalle);
            _cuerpo.Controls.Add(_lblReconocimiento);
            _cuerpo.Resize += (_, _) => AjustarLayoutDiferido();
            _cuerpo.DpiChangedAfterParent += (_, _) => AjustarLayoutDiferido();
            _lblReconocimiento.FontChanged += (_, _) => AjustarLayoutDiferido();
            _lblDetalle.FontChanged += (_, _) => AjustarLayoutDiferido();

            _pie = SoproDialogTheme.CrearPie();
            _revisar = SoproDialogTheme.CrearBotonSecundario(TextoBotonRevisar, SoproIconType.Carpeta);
            _entendido = SoproDialogTheme.CrearBotonPrimario(TextoBotonEntendido);
            _entendido.DialogResult = DialogResult.OK;
            _entendido.Click += (_, _) => { Reconocido = true; DialogResult = DialogResult.OK; };
            _revisar.Click += (_, _) => { Reconocido = true; Revisar = true; DialogResult = DialogResult.OK; Close(); };
            _pie.Controls.Add(_entendido);
            _pie.Controls.Add(_revisar);
            _pie.Resize += (_, _) => AlinearBotones();
            _entendido.SizeChanged += (_, _) => AlinearBotones();
            _revisar.SizeChanged += (_, _) => AlinearBotones();

            Controls.Add(_cuerpo);
            Controls.Add(_pie);
            Controls.Add(_encabezado);

            AcceptButton = _entendido;
            CancelButton = _entendido;

            AlinearBotones();
            ResumeLayout();
            AjustarLayout();
        }

        /// <summary>
        /// Ajuste completo de layout (medir párrafos + reservar altura).
        /// Internal: el constructor lo invoca al final (fuera de toda
        /// transacción de layout) y las pruebas lo usan tras cambios de
        /// tamaño programáticos.
        /// </summary>
        internal void AjustarLayout()
        {
            AjustarAlturas();
            AjustarAlturaVentana();
        }

        private void AjustarLayoutDiferido()
        {
            AjustarAlturas();

            // El crecimiento NO puede aplicarse dentro de la transacción que
            // provocó el evento: el setter exterior sobrescribiría la altura
            // recién reservada. Con handle se difiere un ciclo; sin handle
            // (constructor, pruebas) se aplica directo porque no hay
            // transacción pendiente.
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(AjustarAlturaVentana));
            else
                AjustarAlturaVentana();
        }

        private void AjustarAlturas()
        {
            if (_cuerpo.IsDisposed) return;

            int anchoDisponible = _cuerpo.ClientSize.Width - _cuerpo.Padding.Horizontal;
            if (anchoDisponible <= 0) return;

            _lblReconocimiento.Height = SoproDialogTheme.CalcularAltoEtiqueta(TextoReconocimiento, _lblReconocimiento.Font, anchoDisponible);
            _lblDetalle.Height = SoproDialogTheme.CalcularAltoEtiqueta(TextoDetalle, _lblDetalle.Font, anchoDisponible) + _lblDetalle.Padding.Vertical;
        }

        /// <summary>
        /// Reserva la altura del formulario para el contenido REAL medido
        /// (encabezado + párrafos + paddings + pie): solo crece, nunca se
        /// encoge, y queda acotada al área de trabajo del monitor. Si el
        /// contenido ni siquiera cabe con ese tope, el cuerpo (AutoScroll)
        /// se recorre en lugar de recortar el texto detrás del pie.
        /// </summary>
        private void AjustarAlturaVentana()
        {
            if (IsDisposed || _cuerpo.IsDisposed || _encabezado.IsDisposed || _pie.IsDisposed) return;

            int necesario = _encabezado.Height
                + _cuerpo.Padding.Top
                + _lblReconocimiento.Height
                + _lblDetalle.Height
                + _cuerpo.Padding.Bottom
                + _pie.Height;

            int bordeNoCliente = Height - ClientSize.Height;
            int disponible = Math.Max(240,
                Screen.FromControl(this).WorkingArea.Height - bordeNoCliente - 8);

            int altoObjetivo = Math.Max(ClientSize.Height, Math.Min(necesario, disponible));
            if (ClientSize.Height != altoObjetivo)
                ClientSize = new Size(ClientSize.Width, altoObjetivo);
        }

        private void AlinearBotones()
        {
            SoproDialogTheme.AlinearBotonesPie(_pie, _entendido, _revisar);
        }
    }
}
