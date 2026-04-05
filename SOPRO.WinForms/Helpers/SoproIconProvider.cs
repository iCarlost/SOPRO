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
            }

            return bmp;
        }

        private static void DrawBuscar(Graphics g, Color color, int size)
        {
            var previousSmoothing = g.SmoothingMode;
            var previousInterpolation = g.InterpolationMode;
            var previousPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;

            using var pen = new Pen(color, 1f);
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
            var stroke = Math.Max(1.7f, size * 0.10f);
            using var pen = NewPen(color, stroke);
            using var fill = new SolidBrush(Color.FromArgb(38, color));

            var sheet = CreateRoundedRect(size * 0.28f, size * 0.16f, size * 0.48f, size * 0.68f, size * 0.06f);
            g.FillPath(fill, sheet);
            g.DrawPath(pen, sheet);
            g.DrawLine(pen, size * 0.40f, size * 0.16f, size * 0.40f, size * 0.84f);
            g.DrawLine(pen, size * 0.28f, size * 0.38f, size * 0.76f, size * 0.38f);
            g.DrawLine(pen, size * 0.28f, size * 0.60f, size * 0.76f, size * 0.60f);
            g.DrawLine(pen, size * 0.12f, size * 0.28f, size * 0.28f, size * 0.72f);
            g.DrawLine(pen, size * 0.28f, size * 0.28f, size * 0.12f, size * 0.72f);
        }


        private static void DrawPdf(Graphics g, Color color, int size)
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

            using var pen = new Pen(color, 1f);
            using var solid = new SolidBrush(color);

            int left = 3;
            int top = 3;
            int diameter = size - 7;
            g.DrawArc(pen, left, top, diameter, diameter, 35, 250);
            var arrow = new[]
            {
                new Point(size - 4, 5),
                new Point(size - 8, 5),
                new Point(size - 5, 9)
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

            using var pen = new Pen(color, 1f);
            using var solid = new SolidBrush(color);

            g.DrawLine(pen, 4, size - 4, size - 3, 5);
            g.FillRectangle(solid, 2, size - 6, 5, 3);
            g.DrawLine(pen, 3, size - 3, 2, size - 1);
            g.DrawLine(pen, 5, size - 3, 5, size - 1);
            g.DrawLine(pen, 7, size - 4, 7, size - 1);
            g.FillRectangle(solid, size - 5, 2, 2, 2);

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

            int left = 2;
            int right = size - 7;
            g.FillRectangle(solid, left, 3, right - left + 1, 2);
            g.FillRectangle(solid, left, 7, right - left - 1, 2);
            g.FillRectangle(solid, left, 11, right - left - 4, 2);

            int guideX = size - 4;
            g.FillRectangle(solid, guideX, 3, 2, size - 6);
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

            using var pen = new Pen(color, 1f);
            using var solid = new SolidBrush(color);

            g.DrawRectangle(pen, 2, 5, 5, 5);
            g.DrawRectangle(pen, 5, 2, 5, 5);
            g.DrawLine(pen, 9, 9, size - 4, size - 4);
            g.FillPolygon(solid, new[]
            {
                new Point(size - 2, size - 2),
                new Point(size - 6, size - 3),
                new Point(size - 3, size - 6)
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

        private static GraphicsPath CreateRoundedRect(float x, float y, float width, float height, float radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private enum VerticalPlacement
        {
            Top,
            Middle,
            Bottom
        }
    }
}
