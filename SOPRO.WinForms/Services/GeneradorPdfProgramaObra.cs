using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Models;
using SOPRO.WinForms.Helpers;
using System.Windows.Forms;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;

namespace SOPRO.WinForms.Services
{
    public sealed class GeneradorPdfProgramaObra
    {
        private readonly ReporteService _svc;

        public GeneradorPdfProgramaObra(ReporteService svc)
        {
            _svc = svc;
        }

        private sealed class ProgramaPdfColumn
        {
            public string Name { get; set; } = string.Empty;
            public string HeaderText { get; set; } = string.Empty;
            public int WidthPx { get; set; }
            public string FontName { get; set; } = "Segoe UI";
            public float FontSize { get; set; } = 9f;
            public bool Bold { get; set; }
            public bool Italic { get; set; }
            public DrawingColor ForeColor { get; set; } = DrawingColor.Black;
            public DrawingColor BackColor { get; set; } = DrawingColor.White;
            public DataGridViewContentAlignment Alignment { get; set; } = DataGridViewContentAlignment.MiddleLeft;
            public bool WrapText { get; set; }
        }

        private sealed class ProgramaPdfRow
        {
            public int? ItemId { get; set; }
            public Dictionary<string, string> Valores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, object?> RawValores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public DateTime? Inicio { get; set; }
            public DateTime? Fin { get; set; }
            public bool EsCritica { get; set; }
            public bool EsResumen { get; set; }
            public List<GanttPeriodSegmentDto> SegmentosFinancieros { get; set; } = new();
            public string GetTexto(string columnName) => Valores.TryGetValue(columnName, out var value) ? value : string.Empty;
        }

        private sealed class PageSpec
        {
            public PdfSharp.PageSize? StandardSize { get; init; }
            public PdfSharp.PageOrientation Orientation { get; init; } = PdfSharp.PageOrientation.Landscape;
            public double WidthPoints { get; init; }
            public double HeightPoints { get; init; }
            public double LeftTableWidth { get; init; }
            public double GanttWidth { get; init; }
        }

        public string Generar(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            DataGridView grid,
            GanttRenderModel ganttModel,
            GanttVisualSettings ganttVisualSettings,
            string tituloReporte,
            string? rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            ArgumentNullException.ThrowIfNull(proyecto);
            ArgumentNullException.ThrowIfNull(plantilla);
            ArgumentNullException.ThrowIfNull(grid);
            ArgumentNullException.ThrowIfNull(ganttModel);
            ArgumentNullException.ThrowIfNull(ganttVisualSettings);

            var columnas = CapturarColumnasVisibles(grid);
            var filas = ObtenerFilas(grid, ganttModel);
            if (columnas.Count == 0)
                throw new InvalidOperationException("No hay columnas visibles para exportar.");
            if (filas.Count == 0)
                throw new InvalidOperationException("No hay filas para exportar.");
            if (ganttModel.Escala.Count == 0)
                throw new InvalidOperationException("El gantt no tiene escala para exportar.");

            if (string.IsNullOrWhiteSpace(rutaDestino))
            {
                var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"ProgramaObra_{SanitizarNombre(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            using var document = new PdfDocument();
            document.Info.Title = tituloReporte;

            var footerData = ConstruirFooterPorEscala(ganttModel, filas);
            var pageSpec = BuildPageSpec(columnas, ganttModel);
            int rowIndex = 0;
            int paginaNumero = 0;

            while (rowIndex < filas.Count)
            {
                paginaNumero++;
                var page = document.AddPage();
                ApplyPageSpec(page, pageSpec);

                using var gfx = XGraphics.FromPdfPage(page);
                var layout = new PageLayout(page, plantilla, pageSpec);
                var resources = new DrawResources(gfx, columnas, ganttVisualSettings);

                DrawTemplateHeader(gfx, layout, proyecto, plantilla);
                DrawTemplateFooter(gfx, layout, proyecto, plantilla, paginaNumero);

                double y = layout.BodyTop;
                y = DrawReportHeading(gfx, layout, y, proyecto, ganttModel, tituloReporte, resources, tituloCfg);
                var bands = DrawTimelineHeaders(gfx, layout, y, ganttModel, columnas, resources);
                y = bands.BodyTop;

                while (rowIndex < filas.Count)
                {
                    var fila = filas[rowIndex];
                    double rowHeight = MeasureRowHeight(gfx, fila, columnas, bands.LeftWidths, resources);
                    if (y + rowHeight > layout.BodyBottom)
                        break;

                    DrawRow(gfx, y, rowHeight, fila, columnas, ganttModel, resources, bands);
                    y += rowHeight;
                    rowIndex++;
                }

                if (rowIndex >= filas.Count && ganttModel.ViewMode == GanttViewMode.Erogaciones && footerData.Count > 0)
                {
                    y += 8;
                    if (y + 80 > layout.BodyBottom)
                    {
                        paginaNumero++;
                        page = document.AddPage();
                        ApplyPageSpec(page, pageSpec);
                        using var gfx2 = XGraphics.FromPdfPage(page);
                        layout = new PageLayout(page, plantilla, pageSpec);
                        DrawTemplateHeader(gfx2, layout, proyecto, plantilla);
                        DrawTemplateFooter(gfx2, layout, proyecto, plantilla, paginaNumero);
                        y = DrawReportHeading(gfx2, layout, layout.BodyTop, proyecto, ganttModel, tituloReporte, resources, tituloCfg);
                        DrawFinancialFooter(gfx2, layout, y + 6, footerData, resources);
                    }
                    else
                    {
                        DrawFinancialFooter(gfx, layout, y, footerData, resources);
                    }
                }
            }

            document.Save(rutaDestino);
            return rutaDestino;
        }

        private static void ApplyPageSpec(PdfPage page, PageSpec spec)
        {
            if (spec.StandardSize.HasValue)
            {
                page.Size = spec.StandardSize.Value;
                page.Orientation = spec.Orientation;
            }
            else
            {
                page.Width = XUnit.FromPoint(spec.WidthPoints);
                page.Height = XUnit.FromPoint(spec.HeightPoints);
            }
        }

        private static PageSpec BuildPageSpec(List<ProgramaPdfColumn> columnas, GanttRenderModel ganttModel)
        {
            var leftWidths = CalculateProtectedLeftWidths(columnas);
            double leftWidth = leftWidths.Sum();
            double periodoWidth = ganttModel.ViewMode switch
            {
                GanttViewMode.Erogaciones => 36,
                GanttViewMode.Mixto => 30,
                _ => 18
            };
            double ganttWidth = Math.Max(260, ganttModel.Escala.Count * periodoWidth);
            double margins = 52; // left+right margin area approximation
            double desiredWidth = margins + leftWidth + ganttWidth;

            const double letterLandscape = 792;  // 11 x 8.5
            const double legalLandscape = 1008;  // 14 x 8.5
            const double ledgerLandscape = 1224; // 17 x 11

            if (desiredWidth <= letterLandscape)
            {
                var usable = letterLandscape - margins;
                var ratio = usable / (leftWidth + ganttWidth);
                return new PageSpec
                {
                    StandardSize = PdfSharp.PageSize.Letter,
                    Orientation = PdfSharp.PageOrientation.Landscape,
                    WidthPoints = letterLandscape,
                    HeightPoints = 612,
                    LeftTableWidth = leftWidth * ratio,
                    GanttWidth = ganttWidth * ratio
                };
            }
            if (desiredWidth <= legalLandscape)
            {
                var usable = legalLandscape - margins;
                var ratio = usable / (leftWidth + ganttWidth);
                return new PageSpec
                {
                    StandardSize = PdfSharp.PageSize.Legal,
                    Orientation = PdfSharp.PageOrientation.Landscape,
                    WidthPoints = legalLandscape,
                    HeightPoints = 612,
                    LeftTableWidth = leftWidth * ratio,
                    GanttWidth = ganttWidth * ratio
                };
            }
            if (desiredWidth <= ledgerLandscape)
            {
                var usable = ledgerLandscape - margins;
                var ratio = usable / (leftWidth + ganttWidth);
                return new PageSpec
                {
                    StandardSize = PdfSharp.PageSize.Ledger,
                    Orientation = PdfSharp.PageOrientation.Landscape,
                    WidthPoints = ledgerLandscape,
                    HeightPoints = 792,
                    LeftTableWidth = leftWidth * ratio,
                    GanttWidth = ganttWidth * ratio
                };
            }

            double width = desiredWidth + 20;
            return new PageSpec
            {
                StandardSize = null,
                Orientation = PdfSharp.PageOrientation.Landscape,
                WidthPoints = width,
                HeightPoints = 792,
                LeftTableWidth = leftWidth,
                GanttWidth = ganttWidth
            };
        }

        private sealed class PageLayout
        {
            public double MarginLeft { get; }
            public double MarginRight { get; }
            public double MarginTop { get; }
            public double MarginBottom { get; }
            public double HeaderHeight { get; }
            public double FooterHeight { get; }
            public double BodyTop => MarginTop + HeaderHeight + 8;
            public double BodyBottom { get; }
            public double BodyLeft => MarginLeft;
            public double BodyRight { get; }
            public double BodyWidth => BodyRight - BodyLeft;
            public double PageWidth { get; }
            public double PageHeight { get; }
            public double LeftTableWidth { get; }
            public double GanttWidth { get; }

            public PageLayout(PdfPage page, PlantillaReporte plantilla, PageSpec spec)
            {
                PageWidth = page.Width.Point;
                PageHeight = page.Height.Point;
                MarginLeft = 26;
                MarginRight = 26;
                MarginTop = 18;
                MarginBottom = 16;
                HeaderHeight = Math.Max(52, plantilla.EncabezadoAltura * 0.80);
                FooterHeight = Math.Max(28, plantilla.PiePaginaAltura * 0.80);
                BodyRight = PageWidth - MarginRight;
                BodyBottom = PageHeight - MarginBottom - FooterHeight - 4;
                LeftTableWidth = spec.LeftTableWidth;
                GanttWidth = spec.GanttWidth;
            }
        }

        private sealed class DrawResources
        {
            public XGraphics Gfx { get; }
            public XFont HeaderTitleFont { get; }
            public XFont HeaderSubFont { get; }
            public XFont HeaderCellFont { get; }
            public XFont HeaderCellBoldFont { get; }
            public XFont FooterFont { get; }
            public XPen GridPen { get; }
            public XPen LightGridPen { get; }
            public XPen SummaryPen { get; }
            public XPen LeftRowPen { get; }
            public XPen TimelineVerticalPen { get; }
            public XSolidBrush HeaderBrush { get; }
            public XSolidBrush TextBrush { get; }
            public XBrush NormalBarBrush { get; }
            public XBrush CriticalBarBrush { get; }
            public XBrush SummaryBarBrush { get; }
            public XFont TimelineFont { get; }
            public XFont TimelineSmallFont { get; }
            public List<XFont> ColumnFonts { get; } = new();
            public List<XFont> ColumnBoldFonts { get; } = new();
            public List<XSolidBrush> ColumnBrushes { get; } = new();

            public DrawResources(XGraphics gfx, List<ProgramaPdfColumn> columnas, GanttVisualSettings ganttVisualSettings)
            {
                Gfx = gfx;
                HeaderTitleFont = CreateFont("Segoe UI", 11, true, false);
                HeaderSubFont = CreateFont("Segoe UI", 8.5, false, false);
                HeaderCellFont = CreateFont("Segoe UI", 7.2, false, false);
                HeaderCellBoldFont = CreateFont("Segoe UI", 7.2, true, false);
                FooterFont = CreateFont("Segoe UI", 7.5, true, false);
                TimelineFont = CreateFont(ganttVisualSettings.FontFamilyName, 7.0, false, ganttVisualSettings.FontStyle.HasFlag(System.Drawing.FontStyle.Italic));
                TimelineSmallFont = CreateFont(ganttVisualSettings.FontFamilyName, 6.1, false, false);
                GridPen = new XPen(XColor.FromArgb(180, 180, 180), 0.35);
                LightGridPen = new XPen(XColor.FromArgb(220, 220, 220), 0.30);
                SummaryPen = new XPen(XColor.FromArgb(135, 135, 135), 0.7);
                LeftRowPen = new XPen(XColor.FromArgb(214, 219, 226), 0.30);
                TimelineVerticalPen = new XPen(XColor.FromArgb(214, 219, 226), 0.28);
                HeaderBrush = new XSolidBrush(XColor.FromArgb(232, 238, 247));
                TextBrush = new XSolidBrush(XColor.FromArgb(ganttVisualSettings.TextColor.R, ganttVisualSettings.TextColor.G, ganttVisualSettings.TextColor.B));
                NormalBarBrush = new XSolidBrush(XColor.FromArgb(ganttVisualSettings.NormalBarColor.R, ganttVisualSettings.NormalBarColor.G, ganttVisualSettings.NormalBarColor.B));
                CriticalBarBrush = new XSolidBrush(XColor.FromArgb(ganttVisualSettings.CriticalBarColor.R, ganttVisualSettings.CriticalBarColor.G, ganttVisualSettings.CriticalBarColor.B));
                SummaryBarBrush = new XSolidBrush(XColor.FromArgb(ganttVisualSettings.SummaryBarColor.R, ganttVisualSettings.SummaryBarColor.G, ganttVisualSettings.SummaryBarColor.B));

                foreach (var col in columnas)
                {
                    ColumnFonts.Add(CreateFont(col.FontName, Math.Max(6.4, col.FontSize - 0.6f), col.Bold, col.Italic));
                    ColumnBoldFonts.Add(CreateFont(col.FontName, Math.Max(6.4, col.FontSize - 0.6f), true, col.Italic));
                    ColumnBrushes.Add(new XSolidBrush(XColor.FromArgb(col.ForeColor.R, col.ForeColor.G, col.ForeColor.B)));
                }
            }

            private static XFont CreateFont(string family, double size, bool bold, bool italic)
            {
                var normalized = PdfFontHelper.NormalizeFontName(family);
                var style = XFontStyleEx.Regular;
                if (bold && italic) style = XFontStyleEx.BoldItalic;
                else if (bold) style = XFontStyleEx.Bold;
                else if (italic) style = XFontStyleEx.Italic;
                return new XFont(normalized, size, style);
            }
        }

        private sealed class TimelineBands
        {
            public double LeftX { get; set; }
            public double LeftWidth { get; set; }
            public double TimelineX { get; set; }
            public double TimelineWidth { get; set; }
            public double HeaderTop { get; set; }
            public double HeaderBottom { get; set; }
            public double BodyTop { get; set; }
            public List<double> LeftWidths { get; set; } = new();
        }

        private static List<ProgramaPdfColumn> CapturarColumnasVisibles(DataGridView grid)
        {
            return grid.Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Visible && c.Name != "colDummy")
                .OrderBy(c => c.DisplayIndex)
                .Select(c => new ProgramaPdfColumn
                {
                    Name = c.Name,
                    HeaderText = c.HeaderText,
                    WidthPx = c.Width,
                    FontName = c.DefaultCellStyle.Font?.FontFamily.Name ?? c.InheritedStyle.Font?.FontFamily.Name ?? "Segoe UI",
                    FontSize = c.DefaultCellStyle.Font?.Size ?? c.InheritedStyle.Font?.Size ?? 9f,
                    Bold = c.DefaultCellStyle.Font?.Bold ?? c.InheritedStyle.Font?.Bold ?? false,
                    Italic = c.DefaultCellStyle.Font?.Italic ?? c.InheritedStyle.Font?.Italic ?? false,
                    ForeColor = ResolveColor(c.DefaultCellStyle.ForeColor, DrawingColor.Black),
                    BackColor = ResolveColor(c.DefaultCellStyle.BackColor, DrawingColor.White),
                    Alignment = c.DefaultCellStyle.Alignment,
                    WrapText = c.DefaultCellStyle.WrapMode == DataGridViewTriState.True
                })
                .ToList();
        }

        private static List<ProgramaPdfRow> ObtenerFilas(DataGridView grid, GanttRenderModel gantt)
        {
            var filasGantt = gantt.Filas.ToDictionary(x => x.Id);
            var resultado = new List<ProgramaPdfRow>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow || !row.Visible)
                    continue;

                int? id = null;
                if (row.DataBoundItem is ActivityGridRowDto actividad)
                    id = actividad.Id;
                else if (row.Tag is int tagId)
                    id = tagId;

                filasGantt.TryGetValue(id ?? -1, out var filaGantt);
                var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var rawValores = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DataGridViewCell cell in row.Cells)
                {
                    var col = grid.Columns[cell.ColumnIndex];
                    if (!col.Visible || col.Name == "colDummy")
                        continue;
                    valores[col.Name] = Convert.ToString(cell.FormattedValue, CultureInfo.CurrentCulture) ?? string.Empty;
                    rawValores[col.Name] = cell.Value;
                    rawValores[col.HeaderText] = cell.Value;
                }

                resultado.Add(new ProgramaPdfRow
                {
                    ItemId = id,
                    Valores = valores,
                    RawValores = rawValores,
                    Inicio = filaGantt?.Inicio,
                    Fin = filaGantt?.Fin,
                    EsCritica = filaGantt?.EsCritica ?? false,
                    EsResumen = filaGantt?.EsResumen ?? false,
                    SegmentosFinancieros = filaGantt?.SegmentosFinancieros ?? new List<GanttPeriodSegmentDto>()
                });
            }
            return resultado;
        }

        private void DrawTemplateHeader(XGraphics gfx, PageLayout layout, Proyecto proyecto, PlantillaReporte plantilla)
        {
            DrawTemplateBand(gfx, new XRect(layout.MarginLeft, layout.MarginTop, layout.PageWidth - layout.MarginLeft - layout.MarginRight, layout.HeaderHeight), proyecto, plantilla, false, 0, 0);
        }

        private void DrawTemplateFooter(XGraphics gfx, PageLayout layout, Proyecto proyecto, PlantillaReporte plantilla, int pagina)
        {
            DrawTemplateBand(gfx, new XRect(layout.MarginLeft, layout.PageHeight - layout.MarginBottom - layout.FooterHeight, layout.PageWidth - layout.MarginLeft - layout.MarginRight, layout.FooterHeight), proyecto, plantilla, true, pagina, 0);
        }

        private void DrawTemplateBand(XGraphics gfx, XRect rect, Proyecto proyecto, PlantillaReporte plantilla, bool esPie, int pagina, int totalPaginas)
        {
            var thirds = rect.Width / 3d;
            DrawTemplateCell(gfx, new XRect(rect.Left, rect.Top, thirds, rect.Height), proyecto, plantilla,
                esPie ? plantilla.PiePaginaIzqTipo : plantilla.EncabezadoIzqTipo,
                esPie ? plantilla.PiePaginaIzqContenido : plantilla.EncabezadoIzqContenido,
                esPie ? plantilla.PiePaginaIzqFuente : plantilla.EncabezadoIzqFuente,
                esPie ? plantilla.PiePaginaIzqTamaño : plantilla.EncabezadoIzqTamaño,
                esPie ? plantilla.PiePaginaIzqNegrita : plantilla.EncabezadoIzqNegrita,
                esPie ? plantilla.PiePaginaIzqCursiva : plantilla.EncabezadoIzqCursiva,
                esPie ? plantilla.PiePaginaIzqAlineacion : plantilla.EncabezadoIzqAlineacion,
                pagina, totalPaginas);

            DrawTemplateCell(gfx, new XRect(rect.Left + thirds, rect.Top, thirds, rect.Height), proyecto, plantilla,
                esPie ? plantilla.PiePaginaCenTipo : plantilla.EncabezadoCenTipo,
                esPie ? plantilla.PiePaginaCenContenido : plantilla.EncabezadoCenContenido,
                esPie ? plantilla.PiePaginaCenFuente : plantilla.EncabezadoCenFuente,
                esPie ? plantilla.PiePaginaCenTamaño : plantilla.EncabezadoCenTamaño,
                esPie ? plantilla.PiePaginaCenNegrita : plantilla.EncabezadoCenNegrita,
                esPie ? plantilla.PiePaginaCenCursiva : plantilla.EncabezadoCenCursiva,
                esPie ? plantilla.PiePaginaCenAlineacion : plantilla.EncabezadoCenAlineacion,
                pagina, totalPaginas);

            DrawTemplateCell(gfx, new XRect(rect.Left + thirds * 2, rect.Top, thirds, rect.Height), proyecto, plantilla,
                esPie ? plantilla.PiePaginaDerTipo : plantilla.EncabezadoDerTipo,
                esPie ? plantilla.PiePaginaDerContenido : plantilla.EncabezadoDerContenido,
                esPie ? plantilla.PiePaginaDerFuente : plantilla.EncabezadoDerFuente,
                esPie ? plantilla.PiePaginaDerTamaño : plantilla.EncabezadoDerTamaño,
                esPie ? plantilla.PiePaginaDerNegrita : plantilla.EncabezadoDerNegrita,
                esPie ? plantilla.PiePaginaDerCursiva : plantilla.EncabezadoDerCursiva,
                esPie ? plantilla.PiePaginaDerAlineacion : plantilla.EncabezadoDerAlineacion,
                pagina, totalPaginas);
        }

        private void DrawTemplateCell(XGraphics gfx, XRect rect, Proyecto proyecto, PlantillaReporte plantilla,
            string tipo, string contenido, string fuente, float tamano, bool negrita, bool cursiva, string alineacion,
            int pagina, int totalPaginas)
        {
            if (string.Equals(tipo, "Imagen", StringComparison.OrdinalIgnoreCase) && File.Exists(contenido))
            {
                try
                {
                    using var img = XImage.FromFile(contenido);
                    double ratio = Math.Min(rect.Width / img.PixelWidth, rect.Height / img.PixelHeight);
                    var w = img.PixelWidth * ratio;
                    var h = img.PixelHeight * ratio;
                    var x = rect.Left + (AlineacionEsDerecha(alineacion) ? rect.Width - w : AlineacionEsCentro(alineacion) ? (rect.Width - w) / 2 : 0);
                    var y = rect.Top + (rect.Height - h) / 2;
                    gfx.DrawImage(img, x, y, w, h);
                }
                catch
                {
                }
                return;
            }

            var texto = _svc.ResolverCampos(contenido ?? string.Empty, proyecto, plantilla)
                .Replace("{pagina}", pagina <= 0 ? string.Empty : pagina.ToString(CultureInfo.InvariantCulture))
                .Replace("{total_paginas}", totalPaginas <= 0 ? string.Empty : totalPaginas.ToString(CultureInfo.InvariantCulture));

            var font = new XFont(PdfFontHelper.NormalizeFontName(fuente), Math.Max(7, tamano),
                negrita && cursiva ? XFontStyleEx.BoldItalic : negrita ? XFontStyleEx.Bold : cursiva ? XFontStyleEx.Italic : XFontStyleEx.Regular);
            var tf = new XTextFormatter(gfx) { Alignment = ConvertAlignment(alineacion) };
            tf.DrawString(texto, font, XBrushes.Black, new XRect(rect.Left + 2, rect.Top + 2, rect.Width - 4, rect.Height - 4), XStringFormats.TopLeft);
        }

        private static bool AlineacionEsDerecha(string alineacion) => string.Equals(alineacion, "Derecha", StringComparison.OrdinalIgnoreCase);
        private static bool AlineacionEsCentro(string alineacion) => string.Equals(alineacion, "Centro", StringComparison.OrdinalIgnoreCase);
        private static XParagraphAlignment ConvertAlignment(string alineacion) =>
            string.Equals(alineacion, "Derecha", StringComparison.OrdinalIgnoreCase)
                ? XParagraphAlignment.Right
                : string.Equals(alineacion, "Centro", StringComparison.OrdinalIgnoreCase)
                    ? XParagraphAlignment.Center
                    : XParagraphAlignment.Left;

        private double DrawReportHeading(XGraphics gfx, PageLayout layout, double y, Proyecto proyecto, GanttRenderModel ganttModel, string tituloReporte, DrawResources resources, ConfiguracionTituloReporte? tituloCfg)
        {
            var titleRect = new XRect(layout.BodyLeft, y, layout.BodyWidth, 18);
            gfx.DrawRectangle(new XSolidBrush(ReportTitleStyleHelper.ParseDrawingColor(ReportTitleStyleHelper.StandardBackgroundHex)), titleRect);
            gfx.DrawString(ReportTitleStyleHelper.ObtenerTexto(tituloCfg, tituloReporte), ReportTitleStyleHelper.CreatePdfSharpFont(tituloCfg, 14d), ReportTitleStyleHelper.CreatePdfSharpBrush(tituloCfg, ReportTitleStyleHelper.StandardTextHex),
                titleRect, XStringFormats.Center);
            y += 20;
            var subtitulo = $"Proyecto: {proyecto.Nombre} | Periodicidad: {ganttModel.TipoPeriodo} | Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}";
            gfx.DrawString(subtitulo, resources.HeaderSubFont, XBrushes.Black,
                new XRect(layout.BodyLeft, y, layout.BodyWidth, 12), XStringFormats.TopCenter);
            return y + 18;
        }

        private TimelineBands DrawTimelineHeaders(XGraphics gfx, PageLayout layout, double y, GanttRenderModel ganttModel, List<ProgramaPdfColumn> columnas, DrawResources resources)
        {
            var leftWidths = CalculateProtectedLeftWidths(columnas);
            double leftDesired = leftWidths.Sum();
            double ratio = layout.LeftTableWidth / Math.Max(1d, leftDesired);
            leftWidths = leftWidths.Select(w => Math.Round(w * ratio, 2)).ToList();
            leftDesired = leftWidths.Sum();

            var bands = new TimelineBands
            {
                LeftX = layout.BodyLeft,
                LeftWidth = leftDesired,
                TimelineX = layout.BodyLeft + leftDesired,
                TimelineWidth = layout.GanttWidth,
                HeaderTop = y,
                HeaderBottom = y + 28,
                BodyTop = y + 28,
                LeftWidths = leftWidths
            };

            double x = bands.LeftX;
            for (int i = 0; i < columnas.Count; i++)
            {
                var w = leftWidths[i];
                var rect = new XRect(x, y, w, 28);
                gfx.DrawRectangle(resources.HeaderBrush, rect);
                gfx.DrawRectangle(resources.GridPen, rect);
                var tf = new XTextFormatter(gfx) { Alignment = XParagraphAlignment.Center };
                tf.DrawString(columnas[i].HeaderText, resources.HeaderCellBoldFont, XBrushes.Black, new XRect(rect.Left + 2, rect.Top + 4, rect.Width - 4, rect.Height - 8), XStringFormats.TopLeft);
                x += w;
            }

            var escala = ganttModel.Escala;
            double cellW = bands.TimelineWidth / escala.Count;
            int idx = 0;
            while (idx < escala.Count)
            {
                var grupo = escala[idx].GrupoEtiqueta ?? string.Empty;
                int end = idx;
                while (end + 1 < escala.Count && string.Equals(escala[end + 1].GrupoEtiqueta, grupo, StringComparison.OrdinalIgnoreCase))
                    end++;
                var rect = new XRect(bands.TimelineX + idx * cellW, y, (end - idx + 1) * cellW, 14);
                gfx.DrawRectangle(resources.HeaderBrush, rect);
                gfx.DrawRectangle(resources.GridPen, rect);
                gfx.DrawString(grupo, resources.HeaderCellBoldFont, XBrushes.Black, rect, XStringFormats.Center);
                idx = end + 1;
            }
            for (int i = 0; i < escala.Count; i++)
            {
                var rect = new XRect(bands.TimelineX + i * cellW, y + 14, cellW, 14);
                gfx.DrawRectangle(resources.HeaderBrush, rect);
                gfx.DrawRectangle(resources.GridPen, rect);
                gfx.DrawString(escala[i].Etiqueta, resources.HeaderCellFont, XBrushes.Black, rect, XStringFormats.Center);
            }
            return bands;
        }

        private static List<double> CalculateProtectedLeftWidths(List<ProgramaPdfColumn> columnas)
        {
            var result = new List<double>();
            foreach (var col in columnas)
            {
                var header = col.HeaderText.ToLowerInvariant();
                double width;
                if (header.Contains("concepto") || header.Contains("descrip")) width = Math.Max(170, col.WidthPx * 0.62);
                else if (header.Contains("pred")) width = Math.Max(68, col.WidthPx * 0.55);
                else if (header.Contains("importe")) width = Math.Max(62, col.WidthPx * 0.52);
                else if (header.Contains("rend")) width = Math.Max(54, col.WidthPx * 0.5);
                else if (header.Contains("inicio") || header.Contains("fin")) width = Math.Max(52, col.WidthPx * 0.5);
                else if (header.Contains("cantidad")) width = Math.Max(48, col.WidthPx * 0.48);
                else if (header.Contains("plazo")) width = Math.Max(40, col.WidthPx * 0.48);
                else if (header.Contains("crítica") || header.Contains("critica")) width = Math.Max(40, col.WidthPx * 0.42);
                else if (header.Contains("unidad")) width = Math.Max(38, col.WidthPx * 0.45);
                else if (header.Contains("frente")) width = Math.Max(36, col.WidthPx * 0.42);
                else if (header.Contains("clave")) width = Math.Max(40, col.WidthPx * 0.45);
                else width = Math.Max(38, col.WidthPx * 0.45);
                result.Add(width);
            }
            return result;
        }

        private double MeasureRowHeight(XGraphics gfx, ProgramaPdfRow fila, List<ProgramaPdfColumn> columnas, List<double> leftWidths, DrawResources resources)
        {
            double max = 22;
            for (int i = 0; i < columnas.Count; i++)
            {
                var text = fila.GetTexto(columnas[i].Name);
                var width = Math.Max(20, leftWidths[i] - 6);
                var font = fila.EsResumen ? resources.ColumnBoldFonts[i] : resources.ColumnFonts[i];
                var h = MeasureWrappedTextHeight(gfx, text, font, width);
                if (h > max) max = h;
            }
            return Math.Ceiling(max + 16);
        }

        private static double MeasureWrappedTextHeight(XGraphics gfx, string text, XFont font, double width)
        {
            if (string.IsNullOrWhiteSpace(text))
                return gfx.MeasureString("Ag", font).Height;
            var lines = WrapText(gfx, text, font, width);
            var lineH = gfx.MeasureString("Ag", font).Height;
            return Math.Max(lineH, lines.Count * lineH);
        }

        private void DrawRow(XGraphics gfx, double y, double rowHeight, ProgramaPdfRow fila,
            List<ProgramaPdfColumn> columnas, GanttRenderModel ganttModel, DrawResources resources, TimelineBands bands)
        {
            double x = bands.LeftX;
            for (int i = 0; i < columnas.Count; i++)
            {
                var rect = new XRect(x, y, bands.LeftWidths[i], rowHeight);
                if (fila.EsResumen)
                    gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(248, 248, 248)), rect);
                var font = fila.EsResumen ? resources.ColumnBoldFonts[i] : resources.ColumnFonts[i];
                var brush = resources.ColumnBrushes[i];
                var textRect = new XRect(rect.Left + 3, rect.Top + 4, rect.Width - 6, rect.Height - 8);
                DrawCellText(gfx, fila.GetTexto(columnas[i].Name), columnas[i], font, brush, textRect);
                x += bands.LeftWidths[i];
            }

            // Solo línea horizontal sutil en la tabla izquierda.
            gfx.DrawLine(resources.LeftRowPen, bands.LeftX, y + rowHeight, bands.LeftX + bands.LeftWidth, y + rowHeight);

            var timelineRect = new XRect(bands.TimelineX, y, bands.TimelineWidth, rowHeight);
            DrawTimelineGrid(gfx, timelineRect, ganttModel, resources, fila.EsResumen);
            DrawTimelineContent(gfx, timelineRect, fila, ganttModel, resources);
        }

        private void DrawTimelineGrid(XGraphics gfx, XRect rect, GanttRenderModel model, DrawResources resources, bool summary)
        {
            double cellW = rect.Width / model.Escala.Count;
            for (int i = 0; i <= model.Escala.Count; i++)
            {
                var x = rect.Left + (i * cellW);
                gfx.DrawLine(resources.TimelineVerticalPen, x, rect.Top, x, rect.Bottom);
            }
        }

        private void DrawTimelineContent(XGraphics gfx, XRect rect, ProgramaPdfRow fila, GanttRenderModel model, DrawResources resources)
        {
            if (model.ViewMode == GanttViewMode.ProgramaObra)
            {
                DrawProgramBar(gfx, rect, fila, model, resources);
                return;
            }

            if (model.ViewMode == GanttViewMode.Mixto)
            {
                DrawProgramBar(gfx, rect, fila, model, resources);
                DrawMixedLabels(gfx, rect, fila, model, resources);
                return;
            }

            DrawFinancialSegments(gfx, rect, fila, model, resources);
        }

        private void DrawProgramBar(XGraphics gfx, XRect rect, ProgramaPdfRow fila, GanttRenderModel model, DrawResources resources, bool drawAbove = false)
        {
            if (!fila.Inicio.HasValue || !fila.Fin.HasValue)
                return;
            var totalDays = Math.Max(1d, (model.FechaFinRango.Date.AddDays(1) - model.FechaInicioRango.Date).TotalDays);
            var startOffset = Math.Max(0d, (fila.Inicio.Value.Date - model.FechaInicioRango.Date).TotalDays);
            var endOffset = Math.Max(startOffset + 1d, (fila.Fin.Value.Date.AddDays(1) - model.FechaInicioRango.Date).TotalDays);
            var x0 = rect.Left + (startOffset / totalDays) * rect.Width;
            var x1 = rect.Left + (endOffset / totalDays) * rect.Width;
            const double barHeight = 8;
            var y = rect.Top + ((rect.Height - barHeight) / 2);
            if (drawAbove)
                y = Math.Max(rect.Top + 2, y - 5);
            var barRect = new XRect(x0 + 1, y, Math.Max(3, x1 - x0 - 2), barHeight);
            var brush = fila.EsCritica ? resources.CriticalBarBrush : resources.NormalBarBrush;
            gfx.DrawRectangle(brush, barRect);
        }

        private void DrawMixedLabels(XGraphics gfx, XRect rect, ProgramaPdfRow fila, GanttRenderModel model, DrawResources resources)
        {
            if (fila.SegmentosFinancieros.Count == 0 || model.Escala.Count == 0)
                return;

            const double barHeight = 8d;
            double barTop = rect.Top + ((rect.Height - barHeight) / 2d);
            double lineGap = 1d;

            foreach (var seg in fila.SegmentosFinancieros)
            {
                if (seg.PorcentajeFisicoGlobal == 0m && seg.PorcentajeFinancieroGlobal == 0m)
                    continue;

                var segRect = GetPeriodCellRect(rect, model, seg);
                if (segRect.Width < 20)
                    continue;

                var fisicoTxt = $"{seg.PorcentajeFisicoGlobal:0.####}% fis.";
                var financieroTxt = $"{seg.PorcentajeFinancieroGlobal:0.####}% finan.";
                var widest = new[] { fisicoTxt, financieroTxt }
                    .OrderByDescending(x => x.Length)
                    .First();
                var font = FitFontToWidth(gfx, widest, resources.TimelineSmallFont, 3.6, segRect.Width - 4);
                var lineHeight = gfx.MeasureString("Ag", font).Height;

                var topY = Math.Max(rect.Top + 1, barTop - lineHeight - lineGap);
                var topRect = new XRect(segRect.Left + 1, topY, segRect.Width - 2, lineHeight);
                var bottomRect = new XRect(segRect.Left + 1, barTop + barHeight + lineGap, segRect.Width - 2, lineHeight);

                if (topRect.Right > rect.Right) topRect.Width = Math.Max(2, rect.Right - topRect.Left - 1);
                if (bottomRect.Right > rect.Right) bottomRect.Width = Math.Max(2, rect.Right - bottomRect.Left - 1);

                gfx.DrawString(fisicoTxt, font, XBrushes.Black, topRect, XStringFormats.TopCenter);
                gfx.DrawString(financieroTxt, font, XBrushes.Black, bottomRect, XStringFormats.TopCenter);
            }
        }

        private XRect GetPeriodCellRect(XRect rect, GanttRenderModel model, GanttPeriodSegmentDto seg)
        {
            return GetSegmentRect(rect, model, seg, null, null, false);
        }

        private XRect GetSegmentRect(XRect rect, GanttRenderModel model, GanttPeriodSegmentDto seg, DateTime? actividadInicio, DateTime? actividadFin, bool clipToActivityBounds)
        {
            if (model.Escala.Count == 0)
                return new XRect(rect.Left, rect.Top, 0, rect.Height);

            var totalDays = Math.Max(1d, (model.FechaFinRango.Date.AddDays(1) - model.FechaInicioRango.Date).TotalDays);

            double XForDate(DateTime date)
            {
                var offset = (date.Date - model.FechaInicioRango.Date).TotalDays;
                return rect.Left + (offset / totalDays) * rect.Width;
            }

            var celdaPeriodo = model.Escala.FirstOrDefault(c => c.FechaInicio.Date == seg.FechaInicio.Date && c.FechaFin.Date == seg.FechaFin.Date)
                ?? model.Escala.FirstOrDefault(c => seg.FechaInicio.Date >= c.FechaInicio.Date && seg.FechaInicio.Date <= c.FechaFin.Date)
                ?? model.Escala.FirstOrDefault(c => seg.FechaFin.Date >= c.FechaInicio.Date && seg.FechaFin.Date <= c.FechaFin.Date);

            DateTime inicioRect = seg.FechaInicio.Date;
            DateTime finRectExclusivo = seg.FechaFin.Date.AddDays(1);

            if (celdaPeriodo != null)
            {
                inicioRect = celdaPeriodo.FechaInicio.Date;
                finRectExclusivo = celdaPeriodo.FechaFin.Date.AddDays(1);
            }

            if (clipToActivityBounds)
            {
                if (actividadInicio.HasValue && actividadInicio.Value.Date > inicioRect)
                    inicioRect = actividadInicio.Value.Date;
                if (actividadFin.HasValue)
                {
                    var finActividadExclusivo = actividadFin.Value.Date.AddDays(1);
                    if (finActividadExclusivo < finRectExclusivo)
                        finRectExclusivo = finActividadExclusivo;
                }
            }

            var x0 = XForDate(inicioRect);
            var x1 = XForDate(finRectExclusivo);
            return new XRect(x0 + 1, rect.Top + 1, Math.Max(2, x1 - x0 - 2), rect.Height - 2);
        }

        private void DrawFinancialSegments(XGraphics gfx, XRect rect, ProgramaPdfRow fila, GanttRenderModel model, DrawResources resources)
        {
            if (fila.SegmentosFinancieros.Count == 0)
                return;

            const double barHeight = 8d;
            double barTop = rect.Top + ((rect.Height - barHeight) / 2d);
            foreach (var seg in fila.SegmentosFinancieros)
            {
                if (seg.ImporteProgramado == 0 && seg.PorcentajeProgramado == 0 && seg.PorcentajeFisicoGlobal == 0 && seg.PorcentajeFinancieroGlobal == 0)
                    continue;

                var segRect = GetSegmentRect(rect, model, seg, fila.Inicio, fila.Fin, true);
                var y = barTop;
                if (model.ViewMode == GanttViewMode.Mixto)
                    y = Math.Min(rect.Bottom - barHeight - 2, y + 5);

                segRect = new XRect(segRect.Left, y, segRect.Width, barHeight);
                var fill = fila.EsCritica ? resources.CriticalBarBrush : resources.NormalBarBrush;
                gfx.DrawRectangle(fill, segRect);

                // En Mixto los textos se dibujan fuera de la barra desde DrawMixedLabels.
                if (model.ViewMode == GanttViewMode.Mixto)
                    continue;

                var label = seg.ImporteProgramado.ToStringImporte();
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                // La etiqueta financiera debe comportarse como en Mixto: fuera de la barra
                // y centrada respecto al periodo, no dependiendo del ancho recortado de la barra.
                var labelRectBase = GetPeriodCellRect(rect, model, seg);
                var availableWidth = Math.Max(8, labelRectBase.Width - 4);
                var font = FitFontToWidth(gfx, label, resources.TimelineSmallFont, 3.6, availableWidth);
                var lineHeight = gfx.MeasureString("Ag", font).Height;
                var labelY = Math.Max(rect.Top + 1, barTop - lineHeight - 1.5d);
                var topRect = new XRect(labelRectBase.Left + 1, labelY, Math.Max(6, labelRectBase.Width - 2), lineHeight + 1);
                if (topRect.Right > rect.Right)
                    topRect.Width = Math.Max(6, rect.Right - topRect.Left - 1);
                gfx.DrawString(label, font, XBrushes.Black, topRect, XStringFormats.TopCenter);
            }
        }

        private void DrawFinancialFooter(XGraphics gfx, PageLayout layout, double y, List<GanttFooterPeriodDto> footerData, DrawResources resources)
        {
            if (footerData.Count == 0)
                return;

            double labelWidth = 120;
            double timelineWidth = layout.BodyWidth - labelWidth;
            double cellW = timelineWidth / footerData.Count;
            const double rowHeight = 16;
            var modes = new[]
            {
                GanttFooterDisplayMode.ImportePeriodo,
                GanttFooterDisplayMode.ImporteAcumulado,
                GanttFooterDisplayMode.PorcentajePeriodo,
                GanttFooterDisplayMode.PorcentajeAcumulado
            };

            var subtlePen = resources.LeftRowPen;
            gfx.DrawLine(subtlePen, layout.BodyLeft, y, layout.BodyLeft + layout.BodyWidth, y);

            foreach (var mode in modes)
            {
                var labelRect = new XRect(layout.BodyLeft, y, labelWidth, rowHeight);
                gfx.DrawString(ObtenerTituloFooter(mode), resources.FooterFont, XBrushes.Black, labelRect, XStringFormats.CenterRight);
                for (int i = 0; i < footerData.Count; i++)
                {
                    var rect = new XRect(layout.BodyLeft + labelWidth + i * cellW, y, cellW, rowHeight);
                    gfx.DrawString(ObtenerTextoFooter(footerData[i], mode), resources.TimelineSmallFont, XBrushes.Black, rect, XStringFormats.Center);
                }
                y += rowHeight;
                gfx.DrawLine(subtlePen, layout.BodyLeft, y, layout.BodyLeft + layout.BodyWidth, y);
            }
        }

        private static string ObtenerTituloFooter(GanttFooterDisplayMode mode) => mode switch
        {
            GanttFooterDisplayMode.ImportePeriodo => "Importe por período",
            GanttFooterDisplayMode.ImporteAcumulado => "Importe acumulado",
            GanttFooterDisplayMode.PorcentajePeriodo => "% por período",
            GanttFooterDisplayMode.PorcentajeAcumulado => "% acumulado",
            _ => string.Empty
        };

        private static string ObtenerTextoFooter(GanttFooterPeriodDto footer, GanttFooterDisplayMode mode) => mode switch
        {
            GanttFooterDisplayMode.ImportePeriodo => footer.ImportePeriodo.ToStringImporte(),
            GanttFooterDisplayMode.ImporteAcumulado => footer.ImporteAcumulado.ToStringImporte(),
            GanttFooterDisplayMode.PorcentajePeriodo => $"{footer.PorcentajePeriodo.ToStringPorcentaje()}%",
            GanttFooterDisplayMode.PorcentajeAcumulado => $"{footer.PorcentajeAcumulado.ToStringPorcentaje()}%",
            _ => string.Empty
        };

        private static List<GanttFooterPeriodDto> ConstruirFooterPorEscala(GanttRenderModel model, List<ProgramaPdfRow> filas)
        {
            var footer = new List<GanttFooterPeriodDto>();
            decimal acumulado = 0m;
            for (int i = 0; i < model.Escala.Count; i++)
            {
                var escala = model.Escala[i];
                decimal importe = 0m;
                foreach (var fila in filas)
                {
                    foreach (var seg in fila.SegmentosFinancieros)
                    {
                        if (seg.FechaInicio.Date == escala.FechaInicio.Date && seg.FechaFin.Date == escala.FechaFin.Date)
                            importe += seg.ImporteProgramado;
                    }
                }
                acumulado += importe;
                footer.Add(new GanttFooterPeriodDto
                {
                    Etiqueta = escala.Etiqueta,
                    FechaInicio = escala.FechaInicio,
                    FechaFin = escala.FechaFin,
                    ImportePeriodo = importe,
                    ImporteAcumulado = acumulado,
                    PorcentajePeriodo = 0m,
                    PorcentajeAcumulado = 0m
                });
            }
            var total = footer.Sum(x => x.ImportePeriodo);
            if (total != 0)
            {
                decimal accPct = 0m;
                foreach (var item in footer)
                {
                    item.PorcentajePeriodo = Math.Round(item.ImportePeriodo / total * 100m, 4);
                    accPct += item.PorcentajePeriodo;
                    item.PorcentajeAcumulado = Math.Round(accPct, 4);
                }
            }
            return footer;
        }

        private static string AbreviarImporte(decimal valor)
        {
            return valor.ToString("$#,##0.##", CultureInfo.InvariantCulture);
        }

        private static XFont FitFontToWidth(XGraphics gfx, string text, XFont baseFont, double minSize, double maxWidth)
        {
            var size = baseFont.Size;
            while (size > minSize)
            {
                var test = new XFont(baseFont.Name2, size, baseFont.Style);
                if (gfx.MeasureString(text, test).Width <= maxWidth)
                    return test;
                size -= 0.4;
            }
            return new XFont(baseFont.Name2, minSize, baseFont.Style);
        }


        private static bool IsJustifiedDescriptionColumn(ProgramaPdfColumn column)
        {
            var header = (column.HeaderText ?? string.Empty).ToLowerInvariant();
            var name = (column.Name ?? string.Empty).ToLowerInvariant();
            return header.Contains("concepto") || header.Contains("descrip") || name.Contains("concepto") || name.Contains("descrip");
        }

        private void DrawCellText(XGraphics gfx, string text, ProgramaPdfColumn column, XFont font, XBrush brush, XRect rect)
        {
            if (IsJustifiedDescriptionColumn(column))
            {
                DrawJustifiedWrappedText(gfx, text, font, brush, rect);
                return;
            }

            var tf = new XTextFormatter(gfx) { Alignment = ConvertAlignment(column.Alignment) };
            tf.DrawString(text ?? string.Empty, font, brush, rect, XStringFormats.TopLeft);
        }

        private static void DrawJustifiedWrappedText(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect)
        {
            var lines = WrapText(gfx, text ?? string.Empty, font, Math.Max(1, rect.Width));
            var lineHeight = gfx.MeasureString("Ag", font).Height;
            double y = rect.Top;
            for (int i = 0; i < lines.Count; i++)
            {
                if (y + lineHeight > rect.Bottom)
                    break;

                var line = lines[i] ?? string.Empty;
                var isLast = i == lines.Count - 1;
                DrawJustifiedLine(gfx, line, font, brush, rect.Left, y, rect.Width, lineHeight, isLast);
                y += lineHeight;
            }
        }

        private static void DrawJustifiedLine(XGraphics gfx, string line, XFont font, XBrush brush, double x, double y, double width, double lineHeight, bool isLastLine)
        {
            var words = (line ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1 || isLastLine)
            {
                gfx.DrawString(line ?? string.Empty, font, brush, new XRect(x, y, width, lineHeight), XStringFormats.TopLeft);
                return;
            }

            double wordsWidth = words.Sum(w => gfx.MeasureString(w, font).Width);
            double gaps = words.Length - 1;
            double extra = gaps > 0 ? Math.Max(0, (width - wordsWidth) / gaps) : 0;
            double cursor = x;
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                var wordWidth = gfx.MeasureString(word, font).Width;
                gfx.DrawString(word, font, brush, new XRect(cursor, y, wordWidth + 1, lineHeight), XStringFormats.TopLeft);
                cursor += wordWidth;
                if (i < words.Length - 1)
                    cursor += extra;
            }
        }

        private static List<string> WrapText(XGraphics gfx, string text, XFont font, double width)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Add(string.Empty);
                return result;
            }
            var paragraphs = text.Replace("\r", string.Empty).Split('\n');
            foreach (var paragraph in paragraphs)
            {
                var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    result.Add(string.Empty);
                    continue;
                }
                var current = words[0];
                for (int i = 1; i < words.Length; i++)
                {
                    var candidate = current + " " + words[i];
                    if (gfx.MeasureString(candidate, font).Width <= width)
                        current = candidate;
                    else
                    {
                        result.Add(current);
                        current = words[i];
                    }
                }
                result.Add(current);
            }
            return result;
        }

        private static DrawingColor ResolveColor(DrawingColor color, DrawingColor fallback)
        {
            if (color.IsEmpty || color.A == 0) return fallback;
            return color;
        }

        private static XParagraphAlignment ConvertAlignment(DataGridViewContentAlignment alignment)
        {
            return alignment switch
            {
                DataGridViewContentAlignment.BottomCenter or DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.TopCenter => XParagraphAlignment.Center,
                DataGridViewContentAlignment.BottomRight or DataGridViewContentAlignment.MiddleRight or DataGridViewContentAlignment.TopRight => XParagraphAlignment.Right,
                _ => XParagraphAlignment.Left
            };
        }

        private static string SanitizarNombre(string nombre)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre;
        }
    }
}
