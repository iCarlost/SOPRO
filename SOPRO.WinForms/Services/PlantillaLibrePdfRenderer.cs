using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Forms.Disenador;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;
using SysColor = System.Drawing.Color;

namespace SOPRO.WinForms.Services
{
    internal static class PlantillaLibrePdfRenderer
    {
        private const string ZonaEncabezado = "Encabezado";
        private const string ZonaPie = "PieDePagina";

        public static double ObtenerAlturaEncabezadoCm(PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementos)
            => TieneElementos(elementos, ZonaEncabezado)
                ? Math.Max(0.20, plantilla.AlturaEncabezadoDmm / 100.0)
                : Math.Max(1.8, plantilla.EncabezadoAltura / 28.0);

        public static double ObtenerAlturaPieCm(PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementos)
            => TieneElementos(elementos, ZonaPie)
                ? Math.Max(0.15, plantilla.AlturaPieDmm / 100.0)
                : Math.Max(1.2, plantilla.PiePaginaAltura / 28.0);

        public static double ObtenerAlturaEncabezadoPt(PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementos)
            => TieneElementos(elementos, ZonaEncabezado)
                ? Math.Max(6d, DmmAPuntos(plantilla.AlturaEncabezadoDmm))
                : Math.Max(52d, plantilla.EncabezadoAltura * 0.80);

        public static double ObtenerAlturaPiePt(PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementos)
            => TieneElementos(elementos, ZonaPie)
                ? Math.Max(5d, DmmAPuntos(plantilla.AlturaPieDmm))
                : Math.Max(28d, plantilla.PiePaginaAltura * 0.80);

        public static bool TieneEncabezadoLibre(PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementos)
            => TieneElementos(elementos, ZonaEncabezado);

        public static bool TienePieLibre(PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementos)
            => TieneElementos(elementos, ZonaPie);

        public static double ObtenerSeparacionContenidoSuperiorCm(PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementos, double fallbackExtraCm)
            => TieneEncabezadoLibre(plantilla, elementos) ? 0.05 : fallbackExtraCm;

        public static double ObtenerSeparacionContenidoInferiorCm(PlantillaReporte plantilla, IEnumerable<PlantillaReporteElemento> elementos, double fallbackExtraCm)
            => TienePieLibre(plantilla, elementos) ? 0.05 : fallbackExtraCm;

        public static bool TryRenderHeader(HeaderFooter container, Section section, Proyecto proyecto, PlantillaReporte plantilla,
            IEnumerable<PlantillaReporteElemento> elementos, ReporteService svc)
            => TryRenderMigraDoc(container, section, ReportPageLayoutHelper.GetContentWidthCm(section) * 100.0, proyecto, plantilla, elementos, svc, ZonaEncabezado, false);

        public static bool TryRenderFooter(HeaderFooter container, Section section, Proyecto proyecto, PlantillaReporte plantilla,
            IEnumerable<PlantillaReporteElemento> elementos, ReporteService svc)
            => TryRenderMigraDoc(container, section, ReportPageLayoutHelper.GetContentWidthCm(section) * 100.0, proyecto, plantilla, elementos, svc, ZonaPie, true);

        public static bool TryDrawHeader(XGraphics gfx, XRect rect, Proyecto proyecto, PlantillaReporte plantilla,
            IEnumerable<PlantillaReporteElemento> elementos, ReporteService svc, int pagina = 0, int totalPaginas = 0)
            => TryRenderPdfSharp(gfx, rect, proyecto, plantilla, elementos, svc, ZonaEncabezado, pagina, totalPaginas);

        public static bool TryDrawFooter(XGraphics gfx, XRect rect, Proyecto proyecto, PlantillaReporte plantilla,
            IEnumerable<PlantillaReporteElemento> elementos, ReporteService svc, int pagina = 0, int totalPaginas = 0)
            => TryRenderPdfSharp(gfx, rect, proyecto, plantilla, elementos, svc, ZonaPie, pagina, totalPaginas);

        private static bool TryRenderMigraDoc(HeaderFooter container, Section section, double anchoDisponibleDmm, Proyecto proyecto, PlantillaReporte plantilla,
            IEnumerable<PlantillaReporteElemento> elementos, ReporteService svc, string zona, bool soportaPagina)
        {
            var lista = FiltrarZona(elementos, zona);
            if (lista.Count == 0) return false;

            int alturaZonaDmm = string.Equals(zona, ZonaPie, StringComparison.OrdinalIgnoreCase)
                ? Math.Max(0, plantilla.AlturaPieDmm)
                : Math.Max(0, plantilla.AlturaEncabezadoDmm);

            foreach (var el in lista)
            {
                if (el.Y >= alturaZonaDmm)
                    continue;

                double xDmm = DistribuirX(el.X, el.Ancho, anchoDisponibleDmm);
                double yDmm = el.Y;
                double wDmm = el.Ancho;
                double hDmm = Math.Min(el.Alto, Math.Max(0, alturaZonaDmm - el.Y));
                if (hDmm <= 0)
                    continue;

                if (string.Equals(el.Tipo, "Imagen", StringComparison.OrdinalIgnoreCase))
                {
                    string? path = MaterializarImagenTemporal(el);
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        continue;

                    var frameImg = container.AddTextFrame();
                    frameImg.WrapFormat.Style = WrapStyle.Through;
                    if (string.Equals(zona, ZonaEncabezado, StringComparison.OrdinalIgnoreCase))
                    {
                        frameImg.RelativeHorizontal = RelativeHorizontal.Margin;
                        frameImg.RelativeVertical = RelativeVertical.Page;
                    }
                    frameImg.Left = Unit.FromMillimeter(xDmm / 10.0);
                    frameImg.Top = Unit.FromMillimeter(yDmm / 10.0);
                    frameImg.Width = Unit.FromMillimeter(wDmm / 10.0);
                    frameImg.Height = Unit.FromMillimeter(hDmm / 10.0);
                    frameImg.MarginLeft = 0;
                    frameImg.MarginRight = 0;
                    frameImg.MarginTop = 0;
                    frameImg.MarginBottom = 0;
                    var img = frameImg.AddImage(path);
                    img.LockAspectRatio = false;
                    img.Width = frameImg.Width;
                    img.Height = frameImg.Height;
                    continue;
                }

                var frame = container.AddTextFrame();
                frame.WrapFormat.Style = WrapStyle.Through;
                if (string.Equals(zona, ZonaEncabezado, StringComparison.OrdinalIgnoreCase))
                {
                    frame.RelativeHorizontal = RelativeHorizontal.Margin;
                    frame.RelativeVertical = RelativeVertical.Page;
                }
                frame.Left = Unit.FromMillimeter(xDmm / 10.0);
                frame.Top = Unit.FromMillimeter(yDmm / 10.0);
                frame.Width = Unit.FromMillimeter(wDmm / 10.0);
                frame.Height = Unit.FromMillimeter(hDmm / 10.0);
                frame.MarginLeft = 0;
                frame.MarginRight = 0;
                frame.MarginTop = 0;
                frame.MarginBottom = 0;

                var p = frame.AddParagraph();
                p.Format.Alignment = ConvertirAlineacion(el.Alineacion);
                p.Format.SpaceAfter = 0;
                p.Format.SpaceBefore = 0;
                p.Format.Font.Name = PdfFontHelper.NormalizeFontName(string.IsNullOrWhiteSpace(el.Fuente) ? "Segoe UI" : el.Fuente);
                p.Format.Font.Size = Math.Max(6, el.TamanoFuente);
                p.Format.Font.Bold = el.Negrita;
                p.Format.Font.Italic = el.Cursiva;
                if (!string.IsNullOrWhiteSpace(el.ColorTextoHex))
                {
                    var c = SysColorTranslator(el.ColorTextoHex);
                    p.Format.Font.Color = new MColor(c.R, c.G, c.B);
                }

                var texto = ResolverTexto(el, proyecto, plantilla, svc, pagina: 0, totalPaginas: 0);

                // Siempre pasamos por este método en MigraDoc para que {pagina} y
                // {total_paginas} funcionen tanto en encabezado como en pie.
                // Los demás tokens ya vienen resueltos desde ResolverCampos(...).
                AgregarTextoConCamposPagina(p, texto);
            }
            return true;
        }

        private static bool TryRenderPdfSharp(XGraphics gfx, XRect rect, Proyecto proyecto, PlantillaReporte plantilla,
            IEnumerable<PlantillaReporteElemento> elementos, ReporteService svc, string zona, int pagina, int totalPaginas)
        {
            var lista = FiltrarZona(elementos, zona);
            if (lista.Count == 0) return false;

            double anchoDisponibleDmm = PuntosADmm(rect.Width);

            foreach (var el in lista)
            {
                double xDmm = DistribuirX(el.X, el.Ancho, anchoDisponibleDmm);
                double yDmm = el.Y;
                double wDmm = el.Ancho;
                double hDmm = el.Alto;

                var xr = new XRect(
                    rect.Left + DmmAPuntos(xDmm),
                    rect.Top + DmmAPuntos(yDmm),
                    DmmAPuntos(wDmm),
                    DmmAPuntos(hDmm));

                if (string.Equals(el.Tipo, "Imagen", StringComparison.OrdinalIgnoreCase))
                {
                    string? path = MaterializarImagenTemporal(el);
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        continue;
                    try
                    {
                        using var img = XImage.FromFile(path);
                        gfx.DrawImage(img, xr);
                    }
                    catch { }
                    continue;
                }

                var texto = ResolverTexto(el, proyecto, plantilla, svc, pagina, totalPaginas);
                var font = new XFont(PdfFontHelper.NormalizeFontName(string.IsNullOrWhiteSpace(el.Fuente) ? "Segoe UI" : el.Fuente),
                    Math.Max(6, el.TamanoFuente),
                    el.Negrita && el.Cursiva ? XFontStyleEx.BoldItalic : el.Negrita ? XFontStyleEx.Bold : el.Cursiva ? XFontStyleEx.Italic : XFontStyleEx.Regular);
                var brush = new XSolidBrush(XColor.FromArgb(SysColorTranslator(el.ColorTextoHex).ToArgb()));
                var tf = new XTextFormatter(gfx) { Alignment = ConvertirAlineacionPdfSharp(el.Alineacion) };
                tf.DrawString(texto, font, brush, xr, XStringFormats.TopLeft);
            }

            return true;
        }

        private static double DistribuirX(double xDmm, double anchoElementoDmm, double anchoDisponibleDmm)
        {
            if (anchoDisponibleDmm <= 0)
                return xDmm;

            double anchoBaseDmm = Unidades.ANCHO_BASE_DMM;
            if (anchoBaseDmm <= 0)
                return xDmm;

            // En lugar de aplicar un zoom uniforme, se conserva el tamaño físico del elemento
            // y solo se redistribuye su posición horizontal dentro del ancho útil real del PDF.
            // Se usa el centro relativo del elemento respecto al canvas base para recolocarlo.
            double centroBase = xDmm + (anchoElementoDmm / 2.0);
            double ratioCentro = centroBase / anchoBaseDmm;
            double nuevoCentro = ratioCentro * anchoDisponibleDmm;
            double nuevoX = nuevoCentro - (anchoElementoDmm / 2.0);

            double maxX = Math.Max(0, anchoDisponibleDmm - anchoElementoDmm);
            if (nuevoX < 0) return 0;
            if (nuevoX > maxX) return maxX;
            return nuevoX;
        }

        private static List<PlantillaReporteElemento> FiltrarZona(IEnumerable<PlantillaReporteElemento> elementos, string zona)
            => (elementos ?? Enumerable.Empty<PlantillaReporteElemento>())
                .Where(e => string.Equals(e.Zona, zona, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.ZOrder)
                .ThenBy(e => e.Id)
                .ToList();

        private static bool TieneElementos(IEnumerable<PlantillaReporteElemento> elementos, string zona)
            => FiltrarZona(elementos, zona).Count > 0;

        private static double ObtenerBottomUsadoDmm(IEnumerable<PlantillaReporteElemento> elementos, string zona)
        {
            var lista = FiltrarZona(elementos, zona);
            if (lista.Count == 0)
                return 0;

            double bottom = 0;
            foreach (var el in lista)
            {
                var candidate = Math.Max(0, el.Y) + Math.Max(0, el.Alto);
                if (candidate > bottom)
                    bottom = candidate;
            }
            return bottom;
        }

        private static string ResolverTexto(PlantillaReporteElemento el, Proyecto proyecto, PlantillaReporte plantilla, ReporteService svc, int pagina, int totalPaginas)
        {
            var texto = svc.ResolverCampos(el.Contenido ?? string.Empty, proyecto, plantilla);

            // IMPORTANTE:
            // En MigraDoc el número de página y el total de páginas deben insertarse como campos
            // dinámicos (AddPageField / AddNumPagesField). Por eso, cuando pagina/totalPaginas
            // no vienen calculados, NO se deben reemplazar por vacío; se conservan los tokens
            // para que AgregarTextoConCamposPagina(...) los convierta en campos reales.
            if (pagina > 0)
                texto = texto.Replace("{pagina}", pagina.ToString(CultureInfo.InvariantCulture));

            if (totalPaginas > 0)
                texto = texto.Replace("{total_paginas}", totalPaginas.ToString(CultureInfo.InvariantCulture));

            return texto;
        }

        private static void AgregarTextoConCamposPagina(Paragraph p, string texto)
        {
            texto ??= string.Empty;
            int index = 0;
            while (index < texto.Length)
            {
                int posPagina = texto.IndexOf("{pagina}", index, StringComparison.OrdinalIgnoreCase);
                int posTotal = texto.IndexOf("{total_paginas}", index, StringComparison.OrdinalIgnoreCase);
                int next = MinPos(posPagina, posTotal);
                if (next < 0)
                {
                    p.AddText(texto.Substring(index));
                    break;
                }
                if (next > index)
                    p.AddText(texto.Substring(index, next - index));
                if (next == posPagina)
                {
                    p.AddPageField();
                    index = next + "{pagina}".Length;
                }
                else
                {
                    p.AddNumPagesField();
                    index = next + "{total_paginas}".Length;
                }
            }
        }

        private static int MinPos(int a, int b)
        {
            if (a < 0) return b;
            if (b < 0) return a;
            return Math.Min(a, b);
        }

        private static string? MaterializarImagenTemporal(PlantillaReporteElemento el)
        {
            if (el.ImagenBytes == null || el.ImagenBytes.Length == 0)
                return null;

            var ext = ObtenerExtension(el);
            var hash = Convert.ToHexString(MD5.HashData(el.ImagenBytes));
            var dir = Path.Combine(Path.GetTempPath(), "SOPRO", "PlantillaPdfImg");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, hash + ext);
            if (!File.Exists(path))
                File.WriteAllBytes(path, el.ImagenBytes);
            return path;
        }

        private static string ObtenerExtension(PlantillaReporteElemento el)
        {
            var ext = Path.GetExtension(el.ImagenNombreOrigen ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(ext)) return ext;
            return (el.ImagenMimeType ?? string.Empty).ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/bmp" => ".bmp",
                _ => ".img"
            };
        }

        private static MParagraphAlignment ConvertirAlineacion(string alineacion)
        {
            var s = (alineacion ?? string.Empty).ToLowerInvariant();
            if (s.Contains("right") || s.Contains("der")) return MParagraphAlignment.Right;
            if (s.Contains("center") || s.Contains("cen")) return MParagraphAlignment.Center;
            return MParagraphAlignment.Left;
        }

        private static XParagraphAlignment ConvertirAlineacionPdfSharp(string alineacion)
        {
            var s = (alineacion ?? string.Empty).ToLowerInvariant();
            if (s.Contains("right") || s.Contains("der")) return XParagraphAlignment.Right;
            if (s.Contains("center") || s.Contains("cen")) return XParagraphAlignment.Center;
            return XParagraphAlignment.Left;
        }

        private static SysColor SysColorTranslator(string? hex)
        {
            try { return SysColorTranslatorImpl.FromHtml(string.IsNullOrWhiteSpace(hex) ? "#000000" : hex); }
            catch { return SysColor.Black; }
        }

        private static double DmmAPuntos(double dmm) => (dmm / 10.0) * 72.0 / 25.4;
        private static double PuntosADmm(double pt) => (pt * 25.4 / 72.0) * 10.0;

        private static class SysColorTranslatorImpl
        {
            public static SysColor FromHtml(string html) => System.Drawing.ColorTranslator.FromHtml(html);
        }
    }
}
