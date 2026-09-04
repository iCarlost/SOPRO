using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using SOPRO.Core.Entities;
using Sopro.Calculation.Equipment;

namespace SOPRO.WinForms.Services
{
    public class GeneradorPdfCostoHorario
    {
        private readonly ReporteService _svc;

        public GeneradorPdfCostoHorario(ReporteService svc) => _svc = svc;

        public string Generar(Proyecto proyecto, IEnumerable<Maquinaria> lista, PlantillaReporte plantilla, string rutaDestino, ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (lista == null) throw new ArgumentNullException(nameof(lista));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (string.IsNullOrWhiteSpace(rutaDestino)) throw new ArgumentNullException(nameof(rutaDestino));

            var maquinas = lista.ToList();
            if (maquinas.Count == 0)
                throw new InvalidOperationException("No hay maquinaria para exportar.");

            using var document = new PdfDocument();
            document.Info.Title = "Análisis de costo horario de maquinaria y equipo";

            for (int i = 0; i < maquinas.Count; i++)
            {
                var maq = maquinas[i];
                var page = document.AddPage();
                page.Size = PdfSharp.PageSize.Letter;
                page.Orientation = PdfSharp.PageOrientation.Portrait;

                using var gfx = XGraphics.FromPdfPage(page);
                var layout = CreateLayout(page, plantilla);

                DrawTemplateHeader(gfx, layout, proyecto, plantilla, i + 1, maquinas.Count);
                DrawTemplateFooter(gfx, layout, proyecto, plantilla, i + 1, maquinas.Count);
                DrawMachineSheet(gfx, layout, proyecto, maq, tituloCfg);
            }

            document.Save(rutaDestino);
            return rutaDestino;
        }

        private sealed class PageLayout
        {
            public double PageWidth { get; init; }
            public double PageHeight { get; init; }
            public double MarginLeft { get; init; }
            public double MarginRight { get; init; }
            public double MarginTop { get; init; }
            public double MarginBottom { get; init; }
            public double HeaderHeight { get; init; }
            public double FooterHeight { get; init; }
            public double BodyLeft => MarginLeft;
            public double BodyTop => MarginTop + HeaderHeight + 8;
            public double BodyWidth => PageWidth - MarginLeft - MarginRight;
            public double BodyHeight => PageHeight - MarginTop - MarginBottom - HeaderHeight - FooterHeight - 16;
            public double BodyBottom => BodyTop + BodyHeight;
        }

        private PageLayout CreateLayout(PdfPage page, PlantillaReporte plantilla)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            double headerHeight = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoPt(plantilla, elementosPdf);
            double footerHeight = PlantillaLibrePdfRenderer.ObtenerAlturaPiePt(plantilla, elementosPdf);
            return new PageLayout
            {
                PageWidth = page.Width.Point,
                PageHeight = page.Height.Point,
                MarginLeft = 28d,
                MarginRight = 28d,
                MarginTop = 18d,
                MarginBottom = 18d,
                HeaderHeight = headerHeight,
                FooterHeight = footerHeight
            };
        }

        private void DrawMachineSheet(XGraphics gfx, PageLayout layout, Proyecto proyecto, Maquinaria maq, ConfiguracionTituloReporte? tituloCfg)
        {
            var result = HourlyCostCalculator.Calculate(new HourlyCostInput
            {
                AcquisitionValue = maq.ValorAdquisicion,
                TireValue = maq.ValorLlantas,
                SpecialPartsValue = maq.ValorPiezasEspeciales,
                SalvageFactor = maq.FactorRescate,
                EconomicLifeHours = maq.VidaEconomica,
                InterestRatePercentage = maq.TasaInteres,
                EffectiveHoursPerYear = maq.HorasEfectivasAnio,
                InsuranceRatePercentage = maq.PrimaSeguro,
                MaintenanceFactor = maq.FactorMantenimiento,
                FuelQuantity = maq.CantidadCombustible,
                FuelPrice = maq.PrecioCombustible,
                OilQuantity = maq.CantidadAceite,
                OilPrice = maq.PrecioAceite,
                TireLifeHours = maq.VidaEconomicaLlantas,
                SpecialPartsLifeHours = maq.VidaPiezasEspeciales,
                OperatorSalary = maq.SalarioOperador,
                RealSalaryFactor = maq.FactorSalarioReal,
                EffectiveHoursPerShift = maq.HorasEfectivasTurno
            });
            decimal valorNeto = result.NetValue;
            decimal valorRescate = result.SalvageValue;
            decimal depreciacion = result.Depreciation;
            decimal inversion = result.Investment;
            decimal seguros = result.Insurance;
            decimal mantenimiento = result.Maintenance;
            decimal totalCargosFijos = result.FixedChargesTotal;
            decimal combustibles = result.Fuel;
            decimal lubricantes = result.Lubricants;
            decimal llantas = result.Tires;
            decimal piezasEsp = result.SpecialParts;
            decimal totalConsumos = result.ConsumptionTotal;
            decimal salarioReal = result.RealSalary;
            decimal operacion = result.Operation;
            decimal costoHorario = result.HourlyCost;

            var fonts = CreateFonts();
            double y = layout.BodyTop;

            // Encabezado del análisis
            var titleRect = new XRect(layout.BodyLeft, y, layout.BodyWidth, 16);
            FillRect(gfx, titleRect, ReportTitleStyleHelper.StandardBackgroundHex);
            var titleFont = ReportTitleStyleHelper.CreatePdfSharpFont(tituloCfg, fonts.Title.Size);
            var titleBrush = ReportTitleStyleHelper.CreatePdfSharpBrush(tituloCfg, "#FFFFFF");
            DrawCentered(gfx, ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "ANÁLISIS DE COSTO HORARIO DE MAQUINARIA Y EQUIPO"), titleFont, titleBrush, titleRect);
            y += 16;
            var subRect = new XRect(layout.BodyLeft, y, layout.BodyWidth, 12);
            FillRect(gfx, subRect, "#E8EAF6");
            DrawCentered(gfx, proyecto?.Nombre ?? string.Empty, fonts.Subtitle, XBrushes.Black, subRect);
            y += 18;

            // Identificación compacta
            var idLeft = new XRect(layout.BodyLeft, y, layout.BodyWidth * 0.64, 52);
            var idRight = new XRect(idLeft.Right + 8, y, layout.BodyWidth - idLeft.Width - 8, 52);
            DrawLabelValueBlock(gfx, idLeft, fonts, new[]
            {
                ("Descripción", maq.Descripcion ?? string.Empty),
                ("Combustible", maq.TipoCombustible.ToString()),
                ("Potencia nominal", $"{maq.PotenciaNominal:N2} hp")
            });
            DrawLabelValueBlock(gfx, idRight, fonts, new[]
            {
                ("Clave", maq.Clave ?? string.Empty),
                ("Unidad", "hora"),
                ("Costo horario", $"${costoHorario:N2}")
            }, emphasizeLastValue: true);
            y += 58;

            // Datos generales
            var secRect = new XRect(layout.BodyLeft, y, layout.BodyWidth, 12);
            FillRect(gfx, secRect, "#E3F2FD");
            DrawLeft(gfx, "DATOS GENERALES", fonts.Section, XBrushes.Black, new XRect(secRect.Left + 4, secRect.Top + 1, secRect.Width - 8, secRect.Height - 2));
            y += 14;

            var generalPairs = new[]
            {
                ($"Vad: {maq.ValorAdquisicion:N2} $", $"Ve: {maq.VidaEconomica:N2} hrs"),
                ($"Pn: {maq.ValorLlantas:N2} $", $"Vn: {maq.VidaEconomicaLlantas:N2} hrs"),
                ($"Pa: {maq.ValorPiezasEspeciales:N2} $", $"Va: {maq.VidaPiezasEspeciales:N2} hrs"),
                ($"Vm=Vad-Pn-Pa: {valorNeto:N2} $", $"Hea: {maq.HorasEfectivasAnio:N2} hrs"),
                ($"r: {maq.FactorRescate:N5}", $"Vr=Vm*r: {valorRescate:N2} $"),
                ($"i: {maq.TasaInteres:N4} %", $"s: {maq.PrimaSeguro:N4} %"),
                ($"Gh: {maq.CantidadCombustible:N5} lts/hr", $"Ah: {maq.CantidadAceite:N5} lts/hr"),
                ($"Pc: {maq.PrecioCombustible:N2} $/lt", $"Pac: {maq.PrecioAceite:N2} $/lt"),
                ($"Ko: {maq.FactorMantenimiento:N5}", string.Empty)
            };
            var generalRect = new XRect(layout.BodyLeft, y, layout.BodyWidth, 92);
            DrawCompactPairsGrid(gfx, generalRect, fonts, generalPairs, 2, 9);
            y += 98;

            // Tabla principal fija
            double tableHeight = Math.Max(220, layout.BodyBottom - y - 10);
            var tableRect = new XRect(layout.BodyLeft, y, layout.BodyWidth, tableHeight);
            DrawCalculationTable(gfx, tableRect, fonts, maq, valorNeto, valorRescate,
                depreciacion, inversion, seguros, mantenimiento, totalCargosFijos,
                combustibles, lubricantes, llantas, piezasEsp, totalConsumos,
                salarioReal, operacion, costoHorario);
        }

        private sealed class FontSet
        {
            public XFont Title { get; init; }
            public XFont Subtitle { get; init; }
            public XFont Section { get; init; }
            public XFont Label { get; init; }
            public XFont Value { get; init; }
            public XFont ValueBold { get; init; }
            public XFont TableHead { get; init; }
            public XFont TableCell { get; init; }
            public XFont TableCellSmall { get; init; }
            public XFont TableBold { get; init; }
        }

        private static FontSet CreateFonts()
        {
            string face = PdfFontHelper.NormalizeFontName("Segoe UI");
            return new FontSet
            {
                Title = new XFont(face, 10, XFontStyleEx.Bold),
                Subtitle = new XFont(face, 7, XFontStyleEx.Regular),
                Section = new XFont(face, 7, XFontStyleEx.Bold),
                Label = new XFont(face, 6.7, XFontStyleEx.Bold),
                Value = new XFont(face, 6.7, XFontStyleEx.Regular),
                ValueBold = new XFont(face, 6.9, XFontStyleEx.Bold),
                TableHead = new XFont(face, 6.5, XFontStyleEx.Bold),
                TableCell = new XFont(face, 6.2, XFontStyleEx.Regular),
                TableCellSmall = new XFont(face, 6.0, XFontStyleEx.Regular),
                TableBold = new XFont(face, 6.4, XFontStyleEx.Bold)
            };
        }

        private void DrawCalculationTable(XGraphics gfx, XRect rect, FontSet fonts, Maquinaria maq,
            decimal valorNeto, decimal valorRescate,
            decimal depreciacion, decimal inversion, decimal seguros, decimal mantenimiento, decimal totalCargosFijos,
            decimal combustibles, decimal lubricantes, decimal llantas, decimal piezasEsp, decimal totalConsumos,
            decimal salarioReal, decimal operacion, decimal costoHorario)
        {
            // proportional widths
            double[] ratios = { 10, 34, 28, 14, 14 };
            double ratioSum = ratios.Sum();
            double[] widths = ratios.Select(r => rect.Width * r / ratioSum).ToArray();
            double[] xs = new double[widths.Length];
            xs[0] = rect.Left;
            for (int i = 1; i < widths.Length; i++) xs[i] = xs[i - 1] + widths[i - 1];

            var rows = new List<RowDef>
            {
                RowDef.Header(),
                RowDef.Section("CARGOS FIJOS"),
                RowDef.Calc("D", "Depreciación  D=(Vm-Vr)/Ve", $"({valorNeto:N2}-{valorRescate:N2})/{maq.VidaEconomica:N2}", depreciacion),
                RowDef.Calc("Im", "Inversión  Im=[(Vm+Vr)/2Hea]*i", $"[({valorNeto:N2}+{valorRescate:N2})/2·{maq.HorasEfectivasAnio:N2}]·{maq.TasaInteres / 100m:N6}", inversion),
                RowDef.Calc("Sm", "Seguros  Sm=[(Vm+Vr)/2Hea]*s", $"[({valorNeto:N2}+{valorRescate:N2})/2·{maq.HorasEfectivasAnio:N2}]·{maq.PrimaSeguro / 100m:N6}", seguros),
                RowDef.Calc("Mn", "Mantenimiento  Mn=Ko*D", $"{maq.FactorMantenimiento:N5}·{depreciacion:N2}", mantenimiento),
                RowDef.Subtotal("Total de Cargos Fijos", totalCargosFijos),
                RowDef.Section("CONSUMOS"),
                RowDef.Calc("Co", "Combustible  Co=Gh*Pc", $"{maq.CantidadCombustible:N5}·{maq.PrecioCombustible:N2}", combustibles),
                RowDef.Calc("Lb", "Lubricante  Lb=Ah*Pac", $"{maq.CantidadAceite:N5}·{maq.PrecioAceite:N2}", lubricantes),
            };
            if (maq.ValorLlantas > 0)
                rows.Add(RowDef.Calc("N", "Llantas  N=Pn/Vn", $"{maq.ValorLlantas:N2}/{maq.VidaEconomicaLlantas:N2}", llantas));
            if (maq.ValorPiezasEspeciales > 0)
                rows.Add(RowDef.Calc("Pe", "Piezas esp.  Pe=Pa/Va", $"{maq.ValorPiezasEspeciales:N2}/{maq.VidaPiezasEspeciales:N2}", piezasEsp));
            rows.Add(RowDef.Subtotal("Total de Consumos", totalConsumos));
            rows.Add(RowDef.Section("OPERACIÓN"));
            rows.Add(RowDef.Value("Sn", "Salario tabulado", maq.SalarioOperador.ToString("#,##0.00")));
            rows.Add(RowDef.Value("Fsr", "Factor salario real", maq.FactorSalarioReal.ToString("#,##0.00000")));
            rows.Add(RowDef.Calc("Sr", "Salario real  Sr=Sn*Fsr", $"{maq.SalarioOperador:N2}·{maq.FactorSalarioReal:N5}", salarioReal));
            rows.Add(RowDef.Value("Ht", "Horas efectivas/turno", maq.HorasEfectivasTurno.ToString("#,##0.00")));
            rows.Add(RowDef.Calc("Po", "Operación  Po=Sr/Ht", $"{salarioReal:N2}/{maq.HorasEfectivasTurno:N2}", operacion));
            rows.Add(RowDef.Subtotal("Total de Operación", operacion));
            rows.Add(RowDef.Total(costoHorario));

            double[] heights = ComputeRowHeights(gfx, rows, widths, fonts, rect.Height);
            double y = rect.Top;
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                double h = heights[r];
                if (row.Kind == RowKind.Header)
                {
                    FillRect(gfx, new XRect(rect.Left, y, rect.Width, h), "#4A4A6A");
                    string[] headers = { "Clave", "Fórmula", "Operaciones", "Resultado", "Total" };
                    for (int c = 0; c < widths.Length; c++)
                        DrawCentered(gfx, headers[c], fonts.TableHead, XBrushes.White, new XRect(xs[c] + 2, y + 1, widths[c] - 4, h - 2));
                    DrawHorizontalRule(gfx, rect.Left, rect.Right, y + h);
                }
                else if (row.Kind == RowKind.Section)
                {
                    FillRect(gfx, new XRect(rect.Left, y, rect.Width, h), "#ECEFF1");
                    DrawLeft(gfx, row.Text1, fonts.TableBold, XBrushes.Black, new XRect(rect.Left + 4, y + 1, rect.Width - 8, h - 2));
                    DrawHorizontalRule(gfx, rect.Left, rect.Right, y + h);
                }
                else if (row.Kind == RowKind.Subtotal)
                {
                    FillRect(gfx, new XRect(rect.Left, y, rect.Width, h), "#E8EAF6");
                    DrawRight(gfx, row.Text1, fonts.TableBold, XBrushes.Black, new XRect(rect.Left + 4, y + 1, rect.Width - widths[4] - 8, h - 2));
                    DrawRight(gfx, row.Text4, fonts.TableBold, XBrushes.Black, new XRect(xs[4] + 2, y + 1, widths[4] - 4, h - 2));
                    DrawHorizontalRule(gfx, rect.Left, rect.Right, y + h);
                }
                else if (row.Kind == RowKind.Total)
                {
                    FillRect(gfx, new XRect(rect.Left, y, rect.Width, h), "#1A237E");
                    DrawRight(gfx, "COSTO HORARIO", fonts.TableBold, XBrushes.White, new XRect(rect.Left + 4, y + 1, rect.Width - widths[4] - 8, h - 2));
                    DrawRight(gfx, row.Text4, fonts.TableBold, XBrushes.White, new XRect(xs[4] + 2, y + 1, widths[4] - 4, h - 2));
                    DrawHorizontalRule(gfx, rect.Left, rect.Right, y + h);
                }
                else
                {
                    DrawCentered(gfx, row.Text0, fonts.TableCell, XBrushes.Black, new XRect(xs[0] + 1, y + 1, widths[0] - 2, h - 2));
                    DrawWrapped(gfx, row.Text1, fonts.TableCell, XBrushes.Black, new XRect(xs[1] + 2, y + 1, widths[1] - 4, h - 2));
                    DrawWrapped(gfx, row.Text2, fonts.TableCellSmall, XBrushes.DarkSlateGray, new XRect(xs[2] + 2, y + 1, widths[2] - 4, h - 2));
                    DrawRight(gfx, row.Text3, fonts.TableCell, XBrushes.Black, new XRect(xs[3] + 2, y + 1, widths[3] - 4, h - 2));
                    DrawRight(gfx, row.Text4, fonts.TableCell, XBrushes.Black, new XRect(xs[4] + 2, y + 1, widths[4] - 4, h - 2));
                    DrawHorizontalRule(gfx, rect.Left, rect.Right, y + h);
                }
                y += h;
            }

            DrawOuterBorder(gfx, rect);
        }

        private static double[] ComputeRowHeights(XGraphics gfx, List<RowDef> rows, double[] widths, FontSet fonts, double availableHeight)
        {
            var result = new double[rows.Count];
            double total = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                double h = row.Kind switch
                {
                    RowKind.Header => 14,
                    RowKind.Section => 12,
                    RowKind.Subtotal => 12,
                    RowKind.Total => 14,
                    RowKind.Value => 12,
                    _ => Math.Max(12, Math.Max(
                        MeasureWrappedTextHeight(gfx, row.Text1, fonts.TableCell, widths[1] - 4),
                        MeasureWrappedTextHeight(gfx, row.Text2, fonts.TableCellSmall, widths[2] - 4)) + 4)
                };
                result[i] = h;
                total += h;
            }

            if (total > availableHeight)
            {
                double overflow = total - availableHeight;
                for (int i = result.Length - 1; i >= 0 && overflow > 0; i--)
                {
                    if (rows[i].Kind is RowKind.Calc or RowKind.Value)
                    {
                        double min = 10;
                        double reducible = Math.Max(0, result[i] - min);
                        double take = Math.Min(reducible, overflow);
                        result[i] -= take;
                        overflow -= take;
                    }
                }
            }
            return result;
        }

        private static double MeasureWrappedTextHeight(XGraphics gfx, string text, XFont font, double width)
        {
            if (string.IsNullOrWhiteSpace(text))
                return gfx.MeasureString("Ag", font).Height;

            var lines = WrapText(gfx, text, font, width);
            var lineH = gfx.MeasureString("Ag", font).Height;
            return Math.Max(lineH, lines.Count * lineH);
        }

        private static List<string> WrapText(XGraphics gfx, string text, XFont font, double width)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Add(string.Empty);
                return result;
            }

            foreach (var rawLine in text.Replace("\r", string.Empty).Split('\n'))
            {
                var words = rawLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    result.Add(string.Empty);
                    continue;
                }

                string current = words[0];
                for (int i = 1; i < words.Length; i++)
                {
                    string candidate = current + " " + words[i];
                    if (gfx.MeasureString(candidate, font).Width <= width)
                    {
                        current = candidate;
                    }
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

        private static void DrawCompactPairsGrid(XGraphics gfx, XRect rect, FontSet fonts, (string Left, string Right)[] pairs, int cols, int rows)
        {
            double rowH = rect.Height / rows;
            double colW = rect.Width / cols;
            DrawOuterBorder(gfx, rect);
            for (int r = 0; r < rows; r++)
            {
                double y = rect.Top + r * rowH;
                if (r > 0)
                    DrawHorizontalRule(gfx, rect.Left, rect.Right, y);

                if (r < pairs.Length)
                {
                    DrawLeft(gfx, pairs[r].Left, fonts.Value, XBrushes.Black, new XRect(rect.Left + 3, y + 1, colW - 8, rowH - 2));
                    DrawLeft(gfx, pairs[r].Right, fonts.Value, XBrushes.Black, new XRect(rect.Left + colW + 5, y + 1, colW - 8, rowH - 2));
                }
            }
        }

        private static void DrawLabelValueBlock(XGraphics gfx, XRect rect, FontSet fonts, (string Label, string Value)[] items, bool emphasizeLastValue = false)
        {
            DrawOuterBorder(gfx, rect);
            double rowH = rect.Height / items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                double y = rect.Top + i * rowH;
                if (i > 0)
                    DrawHorizontalRule(gfx, rect.Left, rect.Right, y);
                var labelRect = new XRect(rect.Left + 4, y + 1, 70, rowH - 2);
                var valueRect = new XRect(rect.Left + 75, y + 1, rect.Width - 79, rowH - 2);
                DrawLeft(gfx, items[i].Label + ":", fonts.Label, XBrushes.Black, labelRect);
                DrawWrapped(gfx, items[i].Value, emphasizeLastValue && i == items.Length - 1 ? fonts.ValueBold : fonts.Value, XBrushes.Black, valueRect);
            }
        }

        private void DrawTemplateHeader(XGraphics gfx, PageLayout layout, Proyecto proyecto, PlantillaReporte plantilla, int pagina, int totalPaginas)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            var rect = new XRect(layout.MarginLeft, layout.MarginTop, layout.PageWidth - layout.MarginLeft - layout.MarginRight, layout.HeaderHeight);
            if (!PlantillaLibrePdfRenderer.TryDrawHeader(gfx, rect, proyecto, plantilla, elementosPdf, _svc, pagina, totalPaginas))
                DrawTemplateBand(gfx, rect, proyecto, plantilla, false, pagina, totalPaginas);
        }

        private void DrawTemplateFooter(XGraphics gfx, PageLayout layout, Proyecto proyecto, PlantillaReporte plantilla, int pagina, int totalPaginas)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            var rect = new XRect(layout.MarginLeft, layout.PageHeight - layout.MarginBottom - layout.FooterHeight, layout.PageWidth - layout.MarginLeft - layout.MarginRight, layout.FooterHeight);
            if (!PlantillaLibrePdfRenderer.TryDrawFooter(gfx, rect, proyecto, plantilla, elementosPdf, _svc, pagina, totalPaginas))
                DrawTemplateBand(gfx, rect, proyecto, plantilla, true, pagina, totalPaginas);
        }

        private void DrawTemplateBand(XGraphics gfx, XRect rect, Proyecto proyecto, PlantillaReporte plantilla, bool esPie, int pagina, int totalPaginas)
        {
            double thirds = rect.Width / 3d;
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
            if (string.Equals(tipo, "Imagen", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(contenido) && File.Exists(contenido))
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
                .Replace("{pagina}", pagina.ToString(CultureInfo.InvariantCulture))
                .Replace("{total_paginas}", totalPaginas.ToString(CultureInfo.InvariantCulture));

            var font = new XFont(PdfFontHelper.NormalizeFontName(fuente), Math.Max(7, tamano),
                negrita && cursiva ? XFontStyleEx.BoldItalic : negrita ? XFontStyleEx.Bold : cursiva ? XFontStyleEx.Italic : XFontStyleEx.Regular);
            var tf = new XTextFormatter(gfx) { Alignment = ConvertAlignment(alineacion) };
            tf.DrawString(texto, font, XBrushes.Black, new XRect(rect.Left + 2, rect.Top + 2, rect.Width - 4, rect.Height - 4), XStringFormats.TopLeft);
        }

        private static bool AlineacionEsDerecha(string alineacion) => string.Equals(alineacion, "Derecha", StringComparison.OrdinalIgnoreCase);
        private static bool AlineacionEsCentro(string alineacion) => string.Equals(alineacion, "Centro", StringComparison.OrdinalIgnoreCase) || string.Equals(alineacion, "Centrado", StringComparison.OrdinalIgnoreCase);
        private static XParagraphAlignment ConvertAlignment(string alineacion) =>
            AlineacionEsDerecha(alineacion) ? XParagraphAlignment.Right : AlineacionEsCentro(alineacion) ? XParagraphAlignment.Center : XParagraphAlignment.Left;

        private static void FillRect(XGraphics gfx, XRect rect, string htmlColor)
        {
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(System.Drawing.ColorTranslator.FromHtml(htmlColor).ToArgb())), rect);
        }

        private static void DrawOuterBorder(XGraphics gfx, XRect rect)
        {
            gfx.DrawRectangle(CreateBorderPen(0.45), rect);
        }

        private static void DrawHorizontalRule(XGraphics gfx, double left, double right, double y)
        {
            gfx.DrawLine(CreateBorderPen(0.22), left, y, right, y);
        }

        private static XPen CreateBorderPen(double width)
        {
            return new XPen(XColor.FromArgb(210, 218, 226), width);
        }

        private static void DrawCentered(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect)
        {
            var tf = new XTextFormatter(gfx) { Alignment = XParagraphAlignment.Center };
            tf.DrawString(text ?? string.Empty, font, brush, rect, XStringFormats.TopLeft);
        }

        private static void DrawLeft(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect)
        {
            var tf = new XTextFormatter(gfx) { Alignment = XParagraphAlignment.Left };
            tf.DrawString(text ?? string.Empty, font, brush, rect, XStringFormats.TopLeft);
        }

        private static void DrawRight(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect)
        {
            var tf = new XTextFormatter(gfx) { Alignment = XParagraphAlignment.Right };
            tf.DrawString(text ?? string.Empty, font, brush, rect, XStringFormats.TopLeft);
        }

        private static void DrawWrapped(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect)
        {
            var tf = new XTextFormatter(gfx) { Alignment = XParagraphAlignment.Left };
            tf.DrawString(text ?? string.Empty, font, brush, rect, XStringFormats.TopLeft);
        }

        private enum RowKind { Header, Section, Calc, Value, Subtotal, Total }

        private sealed class RowDef
        {
            public RowKind Kind { get; private init; }
            public string Text0 { get; private init; } = string.Empty;
            public string Text1 { get; private init; } = string.Empty;
            public string Text2 { get; private init; } = string.Empty;
            public string Text3 { get; private init; } = string.Empty;
            public string Text4 { get; private init; } = string.Empty;
            public static RowDef Header() => new() { Kind = RowKind.Header };
            public static RowDef Section(string text) => new() { Kind = RowKind.Section, Text1 = text };
            public static RowDef Calc(string clave, string formula, string operaciones, decimal resultado) => new() { Kind = RowKind.Calc, Text0 = clave, Text1 = formula, Text2 = operaciones, Text3 = resultado.ToString("#,##0.00") };
            public static RowDef Value(string clave, string formula, string valor) => new() { Kind = RowKind.Value, Text0 = clave, Text1 = formula, Text3 = valor };
            public static RowDef Subtotal(string text, decimal valor) => new() { Kind = RowKind.Subtotal, Text1 = text, Text4 = valor.ToString("#,##0.00") };
            public static RowDef Total(decimal valor) => new() { Kind = RowKind.Total, Text4 = valor.ToString("#,##0.00") };
        }
    }
}
