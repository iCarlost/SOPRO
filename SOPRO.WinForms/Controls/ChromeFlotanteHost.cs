using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Controls
{
    /// <summary>Borde de la ventana donde se acopla el host flotante.</summary>
    internal enum ChromeFlotantePosicion
    {
        /// <summary>Banda superior (header, toolbar, filtros).</summary>
        Arriba,
        /// <summary>Banda inferior (barras de acciones). La sombra sigue cayendo hacia abajo, dentro del aire exterior.</summary>
        Abajo,
    }

    /// <summary>
    /// Host liviano que hace "flotar" una superficie del chrome (header, toolbar
    /// principal o toolbar del área de trabajo): reserva aire alrededor con su
    /// Padding, pinta una sombra neutra determinista de tres pasadas (sin
    /// desenfoque ni dependencia de blur) y un borde de 1px que sigue el radio
    /// de la superficie.
    ///
    /// La superficie hija conserva su Region redondeada (la recorta
    /// <see cref="SoproTheme.ApplyRoundedCorners"/>, quien ya responde a tamaño y
    /// DPI); este host solo pinta alrededor y escala sus métricas con
    /// <c>DeviceDpi</c>. No aloja grids ni cambia la geometría del split, la
    /// status bar ni el cuerpo del panel de matrices.
    /// </summary>
    internal sealed class ChromeFlotanteHost : Panel
    {
        // --- Métricas lógicas (96 DPI) del aire alrededor de la superficie ---

        /// <summary>Inset horizontal exterior de toda superficie flotante.</summary>
        internal const int InsetHorizontalLogico = 10;

        /// <summary>Inset superior exterior (aire sobre la superficie).</summary>
        internal const int InsetSuperiorLogico = 8;

        /// <summary>Separación vertical que una superficie deja por debajo (6–8px).</summary>
        internal const int SeparacionInferiorLogica = 6;

        /// <summary>Separación vertical bajo la toolbar del área de trabajo.</summary>
        internal const int SeparacionWorkspaceLogica = 6;

        /// <summary>
        /// Sombra determinista: rectángulos redondeados desplazados hacia abajo,
        /// uno por pasada. Sin la pasada i la sombra termina en el offset i+1.
        /// Alfas acumulados sobre fondo claro: ~19% a 1px, ~12% a 2px y ~5% a
        /// 3px del borde (sombra sutil, sin halo ni brillo azul).
        /// </summary>
        internal static readonly int[] AlfasSombra = { 24, 16, 8 };

        /// <summary>Desplazamiento vertical (px) de cada pasada de sombra.</summary>
        internal static readonly int[] DesplazamientosSombra = { 1, 2, 3 };

        /// <summary>Tono neutro de la sombra (slate-900, el mismo del tema).</summary>
        internal static readonly Color ColorSombra = Color.FromArgb(0x0F, 0x17, 0x2A);

        /// <summary>Borde de 1px de las superficies (token del tema).</summary>
        internal static readonly Color ColorBorde = SoproTheme.Colors.SurfaceBorder;

        private readonly Control _superficie;
        private readonly int _radioLogico;
        private readonly int _insetSuperiorLogico;
        private readonly int _separacionInferiorLogica;
        private readonly ChromeFlotantePosicion _posicion;
        private bool _ajustandoAltura;

        public ChromeFlotanteHost(
            Control superficie,
            int radioLogico,
            string nombre,
            int insetSuperiorLogico,
            int separacionInferiorLogica,
            Color colorFondo,
            ChromeFlotantePosicion posicion = ChromeFlotantePosicion.Arriba)
        {
            _superficie = superficie ?? throw new ArgumentNullException(nameof(superficie));
            _radioLogico = radioLogico;
            _insetSuperiorLogico = insetSuperiorLogico;
            _separacionInferiorLogica = separacionInferiorLogica;
            _posicion = posicion;

            Name = nombre;
            BackColor = colorFondo;
            Dock = posicion == ChromeFlotantePosicion.Arriba ? DockStyle.Top : DockStyle.Bottom;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);

            // La superficie conserva su acople Top (el Padding del host la
            // separa del borde); el aire restante muestra el fondo circundante.
            Controls.Add(_superficie);

            _superficie.SizeChanged += (_, __) => AjustarAltura();
            HandleCreated += (_, __) => AplicarEscalaDpi();
            DpiChangedAfterParent += (_, __) => AplicarEscalaDpi();
            AplicarEscalaDpi();
        }

        /// <summary>Superficie flotante alojada (header, toolbar o toolbar del workspace).</summary>
        internal Control Superficie => _superficie;

        /// <summary>
        /// Mueve la superficie a un <see cref="ChromeFlotanteHost"/> nuevo y deja
        /// el host en el mismo índice z que ocupaba la superficie dentro de su
        /// contenedor (reordena explícitamente para que el acople no dependa del
        /// orden de llegada). Aplica el recorte redondeado a la superficie:
        /// el host pinta alrededor, la superficie se recorta.
        /// </summary>
        /// <param name="insetSuperiorLogico">Aire junto al borde exterior de la ventana (8 por omisión; 0 en bandas pegadas al header).</param>
        /// <param name="separacionInferiorLogica">Aire junto al interior de la ventana (6 por omisión).</param>
        internal static ChromeFlotanteHost Hospedar(
            Control superficie,
            Control contenedor,
            string nombre,
            Color colorFondo,
            int insetSuperiorLogico = InsetSuperiorLogico,
            int separacionInferiorLogica = SeparacionInferiorLogica,
            ChromeFlotantePosicion posicion = ChromeFlotantePosicion.Arriba)
        {
            if (superficie.Parent != contenedor)
                throw new InvalidOperationException(
                    $"La superficie '{superficie.Name}' ya no pertenece a '{contenedor.Name}' (¿hospedada dos veces?).");

            // Índice z ANTES de crear el host: el constructor del host ya
            // reparenta la superficie dentro de sí mismo.
            int indice = contenedor.Controls.GetChildIndex(superficie);

            SoproTheme.ApplyRoundedCorners(superficie, SoproUiMetrics.SurfaceRadiusPx);

            var host = new ChromeFlotanteHost(
                superficie,
                SoproUiMetrics.SurfaceRadiusPx,
                nombre,
                insetSuperiorLogico,
                separacionInferiorLogica,
                colorFondo,
                posicion);

            contenedor.Controls.Remove(superficie);
            contenedor.Controls.Add(host);
            contenedor.Controls.SetChildIndex(host, Math.Clamp(indice, 0, contenedor.Controls.Count - 1));

            return host;
        }

        /// <summary>Escala una medida lógica (96 DPI) al DPI indicado.</summary>
        internal static int EscalarPixeles(int pixelesLogicos, int dpi)
        {
            int dpiSeguro = dpi > 0 ? dpi : 96;
            return Math.Max(0, (int)Math.Round(pixelesLogicos * dpiSeguro / 96d));
        }

        /// <summary>Radio de esquina escalado al DPI indicado (misma regla que SoproTheme).</summary>
        internal static float RadioEscalado(int radioLogico, int dpi)
        {
            int dpiSeguro = dpi > 0 ? dpi : 96;
            return Math.Max(2f, (float)Math.Round(radioLogico * dpiSeguro / 96d));
        }

        /// <summary>
        /// Reaplica el Padding desde las métricas lógicas (idempotente: nunca
        /// escala sobre el valor ya escalado) y reajusta el alto del host.
        /// </summary>
        internal void AplicarEscalaDpi()
        {
            if (IsDisposed) return;

            int dpi = DeviceDpi;
            int lados = EscalarPixeles(InsetHorizontalLogico, dpi);
            int aireExterior = EscalarPixeles(_insetSuperiorLogico, dpi);
            int aireInterior = EscalarPixeles(_separacionInferiorLogica, dpi);
            Padding = _posicion == ChromeFlotantePosicion.Arriba
                ? new Padding(lados, aireExterior, lados, aireInterior)
                : new Padding(lados, aireInterior, lados, aireExterior);

            AjustarAltura();
        }

        private void AjustarAltura()
        {
            if (_ajustandoAltura || IsDisposed || _superficie.IsDisposed) return;

            int deseada = _superficie.Height + Padding.Vertical;
            if (Height != deseada)
            {
                _ajustandoAltura = true;
                try
                {
                    Height = deseada;
                }
                finally
                {
                    _ajustandoAltura = false;
                }
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var graphics = e.Graphics;
            Rectangle superficie = _superficie.Bounds;
            if (superficie.Width < 4 || superficie.Height < 4) return;

            float radio = RadioEscalado(_radioLogico, DeviceDpi);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Sombra: pasadas apiladas hacia abajo (lo demás lo tapa la
            // superficie), sin sobresalir de los lados.
            for (int i = 0; i < DesplazamientosSombra.Length; i++)
            {
                using var path = CrearPathRedondeado(
                    new RectangleF(superficie.X, superficie.Y + DesplazamientosSombra[i], superficie.Width, superficie.Height),
                    radio);
                using var brush = new SolidBrush(Color.FromArgb(AlfasSombra[i], ColorSombra));
                graphics.FillPath(brush, path);
            }

            // Borde de 1px pegado al borde de la superficie: inflado 0.5px para
            // que la mitad visible del trazo no la tape la superficie hija.
            using (var path = CrearPathRedondeado(
                new RectangleF(superficie.X - 0.5f, superficie.Y - 0.5f, superficie.Width + 1f, superficie.Height + 1f),
                radio + 0.5f))
            using (var pen = new Pen(ColorBorde, 1f))
            {
                graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CrearPathRedondeado(RectangleF bounds, float radio)
        {
            var path = new GraphicsPath();
            float diametro = Math.Min(radio * 2f, Math.Min(bounds.Width, bounds.Height));
            if (diametro < 2f)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, diametro, diametro, 180, 90);
            path.AddArc(bounds.Right - diametro, bounds.Y, diametro, diametro, 270, 90);
            path.AddArc(bounds.Right - diametro, bounds.Bottom - diametro, diametro, diametro, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diametro, diametro, diametro, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
