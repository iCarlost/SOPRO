using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Paleta del sidebar (#193): solo derivaciones de SoproTheme.Colors, sin
    /// hexadecimales nuevos. Fondo = superficie fría clara (slate-100).
    /// </summary>
    internal static class SidebarPaleta
    {
        public static Color Fondo => SoproTheme.Colors.SidebarBg2;
        public static Color FondoHover => SoproTheme.Colors.Surface;
        public static Color PildoraActiva => SoproTheme.Colors.SidebarActive;

        /// <summary>Borde de la píldora activa cuando el árbol tiene el foco.</summary>
        public static Color BordePildoraActiva => SoproTheme.Colors.BrandLt;

        /// <summary>Anillo de foco de teclado, un paso por debajo del borde.</summary>
        public static Color AnilloFoco => SoproTheme.Colors.BrandMd;

        public static Color TextoHoja => SoproTheme.Colors.SidebarText;
        public static Color TextoHojaActiva => SoproTheme.Colors.SidebarTextGroup;
        public static Color TextoSeccion => SoproTheme.Colors.Muted;
        public static Color Chevron => Color.FromArgb(150, SoproTheme.Colors.Muted);
    }

    /// <summary>
    /// Pintado puro del sidebar (#193) sobre un TreeView en OwnerDrawAll:
    /// secciones como rótulos en mayúsculas con chevron propio y hojas como
    /// píldoras insertadas. Funciones puras (Graphics + métricas) testeables
    /// en bitmap.
    /// </summary>
    internal static class SidebarRenderer
    {
        public static void PintarFondo(Graphics graphics, Rectangle fila)
        {
            using var brocha = new SolidBrush(SidebarPaleta.Fondo);
            graphics.FillRectangle(brocha, fila);
        }

        /// <summary>
        /// Rótulo de sección: texto en mayúsculas y chevron a la derecha. En
        /// modo colapsado queda como emoji centrado, sin chevron.
        /// </summary>
        public static void PintarSeccion(
            Graphics graphics,
            Rectangle fila,
            string texto,
            Font fuente,
            SidebarEscala m,
            bool expandida,
            bool colapsado)
        {
            if (colapsado)
            {
                TextRenderer.DrawText(
                    graphics, texto, fuente, fila, SidebarPaleta.TextoHoja,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }

            var zonaChevron = RectChevron(fila, m);
            var zonaTexto = new Rectangle(
                m.MargenLateral,
                fila.Y,
                Math.Max(1, zonaChevron.Left - m.MargenLateral),
                fila.Height);

            TextRenderer.DrawText(
                graphics,
                TextoDeSeccion(texto),
                fuente,
                zonaTexto,
                SidebarPaleta.TextoSeccion,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            PintarChevron(graphics, zonaChevron, expandida);
        }

        /// <summary>Mayúsculas del rótulo de sección (los emojis no cambian).</summary>
        public static string TextoDeSeccion(string texto)
            => texto?.ToUpperInvariant() ?? string.Empty;

        /// <summary>
        /// Hoja: píldora insertada con estados reposo/hover/activo/activo-sin-
        /// foco/foco de teclado diferenciables.
        /// </summary>
        public static void PintarHoja(
            Graphics graphics,
            Rectangle fila,
            string texto,
            Font fuente,
            SidebarEscala m,
            int nivel,
            bool seleccionada,
            bool conFoco,
            bool enHover,
            bool colapsado)
        {
            var pildora = RectPildora(fila, m, nivel, colapsado);
            if (pildora.Width <= 0 || pildora.Height <= 0) return;

            var suavizado = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                if (seleccionada || enHover)
                {
                    var relleno = seleccionada ? SidebarPaleta.PildoraActiva : SidebarPaleta.FondoHover;
                    using var camino = CrearCaminoRedondeado(pildora, RadioPildoraSeguro(pildora, m));
                    using var brocha = new SolidBrush(relleno);
                    graphics.FillPath(brocha, camino);
                }

                // Activo con foco: borde de acento. Activo sin foco: relleno
                // sin borde (contrato de estados diferenciables).
                if (seleccionada && conFoco)
                {
                    using var camino = CrearCaminoRedondeado(pildora, RadioPildoraSeguro(pildora, m));
                    using var pluma = new Pen(SidebarPaleta.BordePildoraActiva);
                    graphics.DrawPath(pluma, camino);
                }
            }
            finally
            {
                graphics.SmoothingMode = suavizado;
            }

            var colorTexto = seleccionada ? SidebarPaleta.TextoHojaActiva : SidebarPaleta.TextoHoja;
            if (colapsado)
            {
                TextRenderer.DrawText(
                    graphics, texto, fuente, pildora, colorTexto,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            else
            {
                var zonaTexto = new Rectangle(
                    pildora.Left + m.AcolchadoPildoraHorizontal,
                    pildora.Y,
                    Math.Max(1, pildora.Width - 2 * m.AcolchadoPildoraHorizontal),
                    pildora.Height);

                TextRenderer.DrawText(
                    graphics, texto, fuente, zonaTexto, colorTexto,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }

            if (seleccionada && conFoco)
            {
                var anillo = Rectangle.Inflate(pildora, -2, -2);
                if (anillo.Width > 4 && anillo.Height > 4)
                    ControlPaint.DrawFocusRectangle(graphics, anillo, SidebarPaleta.AnilloFoco, SidebarPaleta.PildoraActiva);
            }
        }

        /// <summary>
        /// Rectángulo de la píldora de una hoja. Expandido: sangría por nivel;
        /// colapsado: cuadrado centrado para el emoji.
        /// </summary>
        public static Rectangle RectPildora(Rectangle fila, SidebarEscala m, int nivel, bool colapsado)
        {
            int alto = fila.Height - 2 * m.AcolchadoPildoraVertical;
            if (alto <= 0) return Rectangle.Empty;

            if (colapsado)
            {
                int lado = Math.Min(alto, fila.Width - 2 * m.MargenLateral);
                if (lado <= 0) return Rectangle.Empty;
                return new Rectangle(
                    fila.X + (fila.Width - lado) / 2,
                    fila.Y + (fila.Height - lado) / 2,
                    lado,
                    lado);
            }

            int izquierda = fila.X + m.SangriaHoja(nivel);
            int ancho = fila.Width - izquierda - m.MargenLateral;
            if (ancho <= 0) return Rectangle.Empty;
            return new Rectangle(izquierda, fila.Y + m.AcolchadoPildoraVertical, ancho, alto);
        }

        /// <summary>Zona del chevron de sección (borde derecho de la fila).</summary>
        public static Rectangle RectChevron(Rectangle fila, SidebarEscala m)
        {
            int lado = Math.Max(m.AnchoChevron, m.AltoChevron);
            return new Rectangle(
                fila.Right - m.MargenChevron - m.AnchoChevron,
                fila.Y + (fila.Height - lado) / 2,
                m.AnchoChevron,
                lado);
        }

        /// <summary>Chevron: triángulo abajo (expandida) o derecha (colapsada).</summary>
        public static void PintarChevron(Graphics graphics, Rectangle zona, bool expandida)
        {
            if (zona.Width <= 0 || zona.Height <= 0) return;

            using var brocha = new SolidBrush(SidebarPaleta.Chevron);
            using var camino = new GraphicsPath();

            if (expandida)
            {
                camino.AddPolygon(new[]
                {
                    new PointF(zona.Left, zona.Top),
                    new PointF(zona.Right, zona.Top),
                    new PointF(zona.Left + zona.Width / 2f, zona.Bottom),
                });
            }
            else
            {
                camino.AddPolygon(new[]
                {
                    new PointF(zona.Left, zona.Top),
                    new PointF(zona.Left, zona.Bottom),
                    new PointF(zona.Right, zona.Top + zona.Height / 2f),
                });
            }

            var suavizado = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            try { graphics.FillPath(brocha, camino); }
            finally { graphics.SmoothingMode = suavizado; }
        }

        private static int RadioPildoraSeguro(Rectangle pildora, SidebarEscala m)
            => Math.Min(m.RadioPildora, Math.Min(pildora.Width, pildora.Height) / 2);

        private static GraphicsPath CrearCaminoRedondeado(Rectangle bounds, int radio)
        {
            var camino = new GraphicsPath();
            int diametro = Math.Max(1, radio * 2);
            diametro = Math.Min(diametro, Math.Min(bounds.Width, bounds.Height));

            var arco = new Rectangle(bounds.X, bounds.Y, diametro, diametro);
            camino.AddArc(arco, 180, 90);
            arco.X = bounds.Right - diametro;
            camino.AddArc(arco, 270, 90);
            arco.Y = bounds.Bottom - diametro;
            camino.AddArc(arco, 0, 90);
            arco.X = bounds.X;
            camino.AddArc(arco, 90, 90);
            camino.CloseFigure();
            return camino;
        }
    }
}
