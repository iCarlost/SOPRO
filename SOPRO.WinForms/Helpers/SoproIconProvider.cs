using System.Collections.Concurrent;
using System.Drawing.Drawing2D;

namespace SOPRO.WinForms.Helpers
{
    public static class SoproIconProvider
    {
        private static readonly ConcurrentDictionary<string, Bitmap> Cache = new();

        public static Image GetIcon(SoproIconType icon, Color color, int size)
        {
            var normalizedSize = Math.Max(12, size);
            var normalizedColor = Color.FromArgb(color.A, color.R, color.G, color.B);
            var key = $"{icon}|{normalizedColor.ToArgb()}|{normalizedSize}";
            var bmp = Cache.GetOrAdd(key, _ => Render(icon, normalizedColor, normalizedSize));
            return (Image)bmp.Clone();
        }

        private static Bitmap Render(SoproIconType icon, Color color, int size)
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.Clear(Color.Transparent);

            switch (icon)
            {
                case SoproIconType.Buscar:
                    DrawBuscar(g, color, size);
                    break;
                case SoproIconType.Excel:
                    DrawExcel(g, color, size);
                    break;
                case SoproIconType.Pdf:
                    DrawPdf(g, color, size);
                    break;
                case SoproIconType.Recalcular:
                    DrawRecalcular(g, color, size);
                    break;
                case SoproIconType.Depurar:
                    DrawDepurar(g, color, size);
                    break;
                case SoproIconType.AjustarTexto:
                    DrawAjustarTexto(g, color, size);
                    break;
                case SoproIconType.AplicarATodas:
                    DrawAplicarATodas(g, color, size);
                    break;
                case SoproIconType.AlinearIzquierda:
                    DrawAlinearHorizontal(g, color, size, ContentAlignment.MiddleLeft);
                    break;
                case SoproIconType.AlinearCentro:
                    DrawAlinearHorizontal(g, color, size, ContentAlignment.MiddleCenter);
                    break;
                case SoproIconType.AlinearDerecha:
                    DrawAlinearHorizontal(g, color, size, ContentAlignment.MiddleRight);
                    break;
                case SoproIconType.AlinearArriba:
                    DrawAlinearVertical(g, color, size, VerticalPlacement.Top);
                    break;
                case SoproIconType.AlinearMedio:
                    DrawAlinearVertical(g, color, size, VerticalPlacement.Middle);
                    break;
                case SoproIconType.AlinearAbajo:
                    DrawAlinearVertical(g, color, size, VerticalPlacement.Bottom);
                    break;
                case SoproIconType.Columnas:
                    DrawColumnas(g, color, size);
                    break;
                case SoproIconType.Matrices:
                    DrawMatrices(g, color, size);
                    break;
                case SoproIconType.Explosion:
                    DrawExplosion(g, color, size);
                    break;
                case SoproIconType.Consolidar:
                    DrawConsolidar(g, color, size);
                    break;
                case SoproIconType.Mas:
                    DrawMas(g, color, size);
                    break;
                case SoproIconType.Carpeta:
                    DrawCarpeta(g, color, size);
                    break;
                case SoproIconType.Copiar:
                    DrawCopiar(g, color, size);
                    break;
                case SoproIconType.Papelera:
                    DrawPapelera(g, color, size);
                    break;
                case SoproIconType.Corazon:
                    DrawCorazon(g, color, size);
                    break;
            }

            return bmp;
        }

        /// <summary>
        /// Grosor de trazo proporcional al tamano. El diseno de referencia usa
        /// glifos de 30 px en las acciones principales, donde un trazo de 1 px se
        /// ve demasiado fino; a 16 px el resultado es identico al de siempre.
        /// </summary>
        private static float Trazo(int size) => Math.Max(1f, size / 16f);

        private static void DrawBuscar(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, Trazo(size));
            using var solid = new SolidBrush(color);

            int lens = Math.Max(6, size - 8);
            int x = 2;
            int y = 2;
            g.DrawEllipse(pen, x, y, lens, lens);
            g.DrawLine(pen, x + lens - 1, y + lens - 1, size - 3, size - 3);
            g.FillRectangle(solid, size - 5, size - 5, 2, 2);

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawExcel(Graphics g, Color color, int size)
        {
            // Documento con esquina doblada y una "X" (marca de Excel),
            // como en el diseno de referencia del ribbon.
            float stroke = Math.Max(1.6f, size * 0.075f);
            using var pen = NewPen(color, stroke);
            using var fill = new SolidBrush(Color.FromArgb(30, color));

            float left = size * 0.16f;
            float top = size * 0.08f;
            float right = size * 0.84f;
            float bottom = size * 0.92f;
            float fold = size * 0.22f;

            var path = new GraphicsPath();
            path.StartFigure();
            path.AddLine(left, top, right - fold, top);
            path.AddLine(right - fold, top, right, top + fold);
            path.AddLine(right, top + fold, right, bottom);
            path.AddLine(right, bottom, left, bottom);
            path.CloseFigure();

            g.FillPath(fill, path);
            g.DrawPath(pen, path);

            // Pliegue de la esquina.
            g.DrawLine(pen, right - fold, top, right - fold, top + fold);
            g.DrawLine(pen, right - fold, top + fold, right, top + fold);

            // Cruz interior.
            float centroX = (left + right) / 2f;
            float centroY = (top + bottom) / 2f;
            float radio = size * 0.16f;
            g.DrawLine(pen, centroX - radio, centroY - radio, centroX + radio, centroY + radio);
            g.DrawLine(pen, centroX + radio, centroY - radio, centroX - radio, centroY + radio);
        }


        private static void DrawPdf(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, Trazo(size));
            using var solid = new SolidBrush(color);

            int left = 3;
            int top = 2;
            int width = size - 7;
            int height = size - 5;
            g.DrawRectangle(pen, left, top, width, height);
            g.DrawLine(pen, size - 6, 2, size - 3, 5);
            g.DrawLine(pen, size - 6, 2, size - 6, 5);
            g.DrawLine(pen, size - 6, 5, size - 3, 5);

            g.FillRectangle(solid, 5, 7, 2, size - 11);
            g.FillRectangle(solid, 8, 7, 2, size - 11);
            g.FillRectangle(solid, 5, 7, 4, 2);
            g.FillRectangle(solid, 5, size - 6, 4, 2);

            g.FillRectangle(solid, 10, 7, 2, size - 11);
            g.FillRectangle(solid, 13, 7, 2, size - 11);
            g.FillRectangle(solid, 10, 7, 5, 2);
            g.FillRectangle(solid, 10, (size / 2) - 1, 5, 2);
            g.FillRectangle(solid, 10, size - 6, 5, 2);

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawRecalcular(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, Trazo(size));
            using var solid = new SolidBrush(color);

            int left = 3;
            int top = 3;
            int diameter = size - 7;
            g.DrawArc(pen, left, top, diameter, diameter, 35, 250);

            // Punta de flecha proporcional al tamano del glifo.
            int punta = Math.Max(4, (int)Math.Round(size * 0.25f));
            var arrow = new[]
            {
                new Point(size - 4, top + 2),
                new Point(size - 4 - punta, top + 2),
                new Point(size - 4 - Math.Max(1, punta / 4), top + 2 + punta)
            };
            g.FillPolygon(solid, arrow);

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawDepurar(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, Trazo(size));
            using var solid = new SolidBrush(color);

            // Cabeza de la escoba y fibras, proporcionales al glifo.
            int cabeza = Math.Max(5, (int)Math.Round(size * 0.31f));
            int fibras = Math.Max(3, (int)Math.Round(size * 0.19f));
            int polvo = Math.Max(2, (int)Math.Round(size * 0.12f));

            g.DrawLine(pen, cabeza - 1, size - 4, size - 3, 5);
            g.FillRectangle(solid, 2, size - 6, cabeza, fibras);
            g.DrawLine(pen, 2 + (cabeza / 3), size - 3, 1, size - 1);
            g.DrawLine(pen, 2 + (cabeza / 3), size - 3, 2 + (cabeza / 3), size - 1);
            g.DrawLine(pen, 2 + ((cabeza * 2) / 3), size - 4, 2 + ((cabeza * 2) / 3), size - 1);
            g.FillRectangle(solid, size - 3 - polvo, 2, polvo, polvo);

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawAjustarTexto(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var solid = new SolidBrush(color);

            int left = Math.Max(2, (int)Math.Round(size * 0.125f));
            int right = size - Math.Max(7, (int)Math.Round(size * 0.44f));
            int top = Math.Max(3, (int)Math.Round(size * 0.19f));
            int paso = Math.Max(4, (int)Math.Round(size * 0.25f));
            int grosor = Math.Max(2, (int)Math.Round(size * 0.125f));

            g.FillRectangle(solid, left, top, right - left + 1, grosor);
            g.FillRectangle(solid, left, top + paso, right - left - 1, grosor);
            g.FillRectangle(solid, left, top + (2 * paso), right - left - 4, grosor);

            int guideX = size - Math.Max(4, (int)Math.Round(size * 0.25f));
            g.FillRectangle(solid, guideX, top, grosor, size - 6);
            g.FillPolygon(solid, new[] { new Point(guideX + 1, 1), new Point(guideX - 2, 4), new Point(guideX + 4, 4) });
            g.FillPolygon(solid, new[] { new Point(guideX + 1, size - 1), new Point(guideX - 2, size - 4), new Point(guideX + 4, size - 4) });

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawAplicarATodas(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, Trazo(size));
            using var solid = new SolidBrush(color);

            int lado = Math.Max(5, (int)Math.Round(size * 0.31f));

            g.DrawRectangle(pen, 2, lado, lado, lado);
            g.DrawRectangle(pen, lado, 2, lado, lado);
            g.DrawLine(pen, 2 + lado, 2 + lado, size - 4, size - 4);
            g.FillPolygon(solid, new[]
            {
                new Point(size - 2, size - 2),
                new Point(size - 2 - lado + 1, size - 3),
                new Point(size - 3, size - 2 - lado + 1)
            });

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawAlinearHorizontal(Graphics g, Color color, int size, ContentAlignment align)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var solid = new SolidBrush(color);

            int padding = Math.Max(2, size / 6);
            int maxWidth = Math.Max(6, size - (padding * 2));
            int barHeight = Math.Max(2, size / 8);
            int gap = Math.Max(2, size / 7);
            int totalHeight = (barHeight * 3) + (gap * 2);
            int firstY = (size - totalHeight) / 2;

            int[] widths =
            {
                maxWidth,
                Math.Max(4, (int)Math.Round(maxWidth * 0.70)),
                Math.Max(5, (int)Math.Round(maxWidth * 0.90))
            };

            for (int i = 0; i < widths.Length; i++)
            {
                int width = widths[i];
                int x = align switch
                {
                    ContentAlignment.MiddleLeft => padding,
                    ContentAlignment.MiddleCenter => (size - width) / 2,
                    _ => size - padding - width
                };

                int y = firstY + (i * (barHeight + gap));
                g.FillRectangle(solid, x, y, width, barHeight);
            }

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawAlinearVertical(Graphics g, Color color, int size, VerticalPlacement placement)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var solid = new SolidBrush(color);

            int padding = Math.Max(2, size / 6);
            int maxWidth = Math.Max(6, size - (padding * 2));
            int barHeight = Math.Max(2, size / 8);
            int gap = Math.Max(2, size / 8);
            int totalHeight = (barHeight * 3) + (gap * 2);

            int full = maxWidth;
            int medium = Math.Max(5, (int)Math.Round(maxWidth * 0.82));
            int small = Math.Max(4, (int)Math.Round(maxWidth * 0.62));

            int y1;
            int y2;
            int y3;
            int[] widths;

            switch (placement)
            {
                case VerticalPlacement.Top:
                    y1 = padding;
                    y2 = y1 + barHeight + gap;
                    y3 = y2 + barHeight + gap;
                    widths = new[] { full, medium, small };
                    break;
                case VerticalPlacement.Middle:
                    y1 = (size - totalHeight) / 2;
                    y2 = y1 + barHeight + gap;
                    y3 = y2 + barHeight + gap;
                    widths = new[] { medium, full, medium };
                    break;
                default:
                    y3 = size - padding - barHeight;
                    y2 = y3 - gap - barHeight;
                    y1 = y2 - gap - barHeight;
                    widths = new[] { small, medium, full };
                    break;
            }

            int centerX = size / 2;
            int[] ys = { y1, y2, y3 };
            for (int i = 0; i < 3; i++)
            {
                int width = widths[i];
                int x = centerX - (width / 2);
                g.FillRectangle(solid, x, ys[i], width, barHeight);
            }

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawColumnas(Graphics g, Color color, int size)
        {
            var stroke = Math.Max(1.7f, size * 0.10f);
            using var pen = NewPen(color, stroke);
            using var fill = new SolidBrush(Color.FromArgb(36, color));
            var columns = new[]
            {
                new RectangleF(size * 0.14f, size * 0.20f, size * 0.14f, size * 0.60f),
                new RectangleF(size * 0.42f, size * 0.20f, size * 0.14f, size * 0.60f),
                new RectangleF(size * 0.70f, size * 0.20f, size * 0.14f, size * 0.60f)
            };
            foreach (var rect in columns)
            {
                g.FillRectangle(fill, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        private static void DrawMatrices(Graphics g, Color color, int size)
        {
            var stroke = Math.Max(1.7f, size * 0.10f);
            using var pen = NewPen(color, stroke);
            var nodes = new[]
            {
                new RectangleF(size * 0.14f, size * 0.18f, size * 0.18f, size * 0.18f),
                new RectangleF(size * 0.68f, size * 0.18f, size * 0.18f, size * 0.18f),
                new RectangleF(size * 0.41f, size * 0.60f, size * 0.18f, size * 0.18f)
            };
            g.DrawLine(pen, size * 0.32f, size * 0.27f, size * 0.68f, size * 0.27f);
            g.DrawLine(pen, size * 0.23f, size * 0.36f, size * 0.50f, size * 0.60f);
            g.DrawLine(pen, size * 0.77f, size * 0.36f, size * 0.50f, size * 0.60f);
            foreach (var node in nodes)
                g.DrawRectangle(pen, node.X, node.Y, node.Width, node.Height);
        }

        private static void DrawExplosion(Graphics g, Color color, int size)
        {
            var stroke = Math.Max(1.9f, size * 0.12f);
            using var pen = NewPen(color, stroke);
            var points = new[]
            {
                new PointF(size * 0.50f, size * 0.10f),
                new PointF(size * 0.60f, size * 0.34f),
                new PointF(size * 0.88f, size * 0.28f),
                new PointF(size * 0.68f, size * 0.48f),
                new PointF(size * 0.90f, size * 0.72f),
                new PointF(size * 0.58f, size * 0.64f),
                new PointF(size * 0.48f, size * 0.92f),
                new PointF(size * 0.38f, size * 0.64f),
                new PointF(size * 0.10f, size * 0.72f),
                new PointF(size * 0.30f, size * 0.48f),
                new PointF(size * 0.12f, size * 0.28f),
                new PointF(size * 0.40f, size * 0.34f)
            };
            g.DrawPolygon(pen, points);
        }

        private static Pen NewPen(Color color, float width)
        {
            return new Pen(color, width)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
                Alignment = PenAlignment.Center
            };
        }

        private static void DrawConsolidar(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, Trazo(size));
            using var solid = new SolidBrush(color);

            // Tres fuentes que convergen en un unico destino.
            int lado = Math.Max(4, (int)Math.Round(size * 0.22f));
            int derecha = size - 3 - lado;
            int medio = size / 2;

            g.DrawRectangle(pen, 2, 3, lado, lado);
            g.DrawRectangle(pen, 2, size - 3 - lado, lado, lado);
            g.DrawRectangle(pen, derecha, medio - (lado / 2), lado, lado);
            g.DrawLine(pen, 2 + lado, 5, derecha - 1, medio);
            g.DrawLine(pen, 2 + lado, size - 5, derecha - 1, medio);
            g.FillPolygon(solid, new[]
            {
                new Point(derecha + lado, medio),
                new Point(derecha - 1, medio - 3),
                new Point(derecha - 1, medio + 3)
            });

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawMas(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var solid = new SolidBrush(color);

            int thickness = Math.Max(2, size / 5);
            int margin = Math.Max(3, size / 5);
            int center = size / 2;

            // Barra horizontal.
            g.FillRectangle(solid, margin, center - (thickness / 2), size - (margin * 2), thickness);
            // Barra vertical.
            g.FillRectangle(solid, center - (thickness / 2), margin, thickness, size - (margin * 2));

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawCarpeta(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, 1f);
            using var solid = new SolidBrush(color);

            int left = 2;
            int top = 3;
            int width = size - 5;
            int height = size - 8;
            int tabWidth = Math.Max(4, width / 3);

            // Pestana superior.
            g.FillRectangle(solid, left, top, tabWidth, 2);
            // Cuerpo de la carpeta.
            g.DrawRectangle(pen, left, top + 2, width, height);
            g.DrawLine(pen, left + 2, top + 5, left + width - 2, top + 5);

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawCopiar(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, 1f);
            using var soft = new SolidBrush(Color.FromArgb(38, color));

            int frontLeft = size / 2;
            int frontTop = size / 2;
            int frontRight = size - 3;
            int frontBottom = size - 3;

            int backLeft = 2;
            int backTop = 2;
            int backRight = Math.Max(frontLeft + 1, size - 6);
            int backBottom = Math.Max(frontTop + 1, size - 6);

            // Hoja trasera: solo el borde en "L" que queda visible detras de la frontal.
            g.DrawLine(pen, backLeft, backTop, backRight, backTop);
            g.DrawLine(pen, backLeft, backTop, backLeft, backBottom);
            g.DrawLine(pen, backLeft, backBottom, frontLeft - 1, backBottom);
            g.DrawLine(pen, backRight, backTop, backRight, frontTop - 1);

            // Hoja frontal: relleno tenue + borde completo.
            g.FillRectangle(soft, frontLeft + 1, frontTop + 1, frontRight - frontLeft - 1, frontBottom - frontTop - 1);
            g.DrawRectangle(pen, frontLeft, frontTop, frontRight - frontLeft, frontBottom - frontTop);

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawPapelera(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, 1f);
            using var solid = new SolidBrush(color);

            int left = 3;
            int right = size - 4;
            int lidY = 5;

            // Tapa.
            g.FillRectangle(solid, left, lidY, right - left + 1, 2);
            // Asa.
            g.DrawRectangle(pen, (size / 2) - 2, lidY - 2, 5, 2);
            // Cuerpo (trapecio).
            g.DrawLine(pen, left + 1, lidY + 3, left + 2, size - 3);
            g.DrawLine(pen, right - 1, lidY + 3, right - 2, size - 3);
            g.DrawLine(pen, left + 2, size - 3, right - 2, size - 3);
            // Nervios internos.
            g.DrawLine(pen, (size / 2) - 2, lidY + 4, (size / 2) - 2, size - 4);
            g.DrawLine(pen, (size / 2) + 1, lidY + 4, (size / 2) + 1, size - 4);

            g.SmoothingMode = previousSmoothing;
            g.InterpolationMode = previousInterpolation;
            g.PixelOffsetMode = previousPixelOffset;
        }

        private static void DrawCorazon(Graphics g, Color color, int size)
        {
            using var brush = new SolidBrush(color);
            using var path = new GraphicsPath();

            float centerX = size * 0.5f;
            float topY = size * 0.26f;
            float lobeY = size * 0.30f;
            float bottomY = size * 0.78f;
            float lobeX = size * 0.22f;
            float controlTopY = size * 0.10f;
            float controlSideY = size * 0.56f;
            float controlBottomY = size * 0.70f;

            path.StartFigure();
            path.AddBezier(centerX, topY, centerX, controlTopY, lobeX, controlTopY, lobeX, lobeY);
            path.AddBezier(lobeX, lobeY, lobeX, controlSideY, centerX - size * 0.05f, controlBottomY, centerX, bottomY);
            path.AddBezier(centerX, bottomY, centerX + size * 0.05f, controlBottomY, size - lobeX, controlSideY, size - lobeX, lobeY);
            path.AddBezier(size - lobeX, lobeY, size - lobeX, controlTopY, centerX, controlTopY, centerX, topY);
            path.CloseFigure();

            g.FillPath(brush, path);
        }

        private enum VerticalPlacement
        {
            Top,
            Middle,
            Bottom
        }
    }
}
