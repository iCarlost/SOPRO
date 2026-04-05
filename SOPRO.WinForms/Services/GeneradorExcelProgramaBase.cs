using ClosedXML.Excel;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;

namespace SOPRO.WinForms.Services
{
    public abstract class GeneradorExcelProgramaBase
    {
        private readonly ReporteService _svc;

        protected GeneradorExcelProgramaBase(ReporteService svc)
        {
            _svc = svc;
        }

        protected string GenerarCore(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            List<ProgramaExcelColumnExport> columnas,
            List<ProgramaExcelRowExport> filas,
            GanttRenderModel ganttModel,
            GanttVisualSettings ganttVisualSettings,
            GanttFooterDisplayMode footerMode,
            int timelineCellWidth,
            string tituloReporte,
            string prefijoArchivo,
            string? rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            ArgumentNullException.ThrowIfNull(proyecto);
            ArgumentNullException.ThrowIfNull(plantilla);
            ArgumentNullException.ThrowIfNull(ganttModel);
            ArgumentNullException.ThrowIfNull(ganttVisualSettings);

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
                rutaDestino = Path.Combine(carpeta, $"{prefijoArchivo}_{SanitizarNombre(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
            }

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Programa");

            int timelineColStart = columnas.Count + 1;
            int numCols = columnas.Count + Math.Max(1, ganttModel.Escala.Count);
            int fila = 1;
            fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, _svc);

            var titleRange = ws.Range(fila, 1, fila, numCols);
            ReportTitleStyleHelper.ApplyToClosedXmlTitle(titleRange, tituloCfg, tituloReporte);
            var titleCell = ws.Cell(fila, 1);
            ws.Row(fila).Height = 20;
            fila++;

            ws.Range(fila, 1, fila, numCols).Merge();
            var subtitleCell = ws.Cell(fila, 1);
            subtitleCell.Value = $"Proyecto: {proyecto.Nombre} | Periodicidad: {ganttModel.TipoPeriodo} | Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}";
            subtitleCell.Style.Font.FontSize = 9;
            subtitleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            subtitleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(fila).Height = 18;
            fila++;

            int headerTopRow = fila;
            int headerBottomRow = fila + 1;
            EscribirEncabezadosIzquierda(ws, columnas, headerTopRow, headerBottomRow);
            EscribirEncabezadoTimeline(ws, ganttModel, timelineColStart, headerTopRow, headerBottomRow);
            fila += 2;
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, fila - 1);
            int filaInicioDatos = fila;

            ConfigurarColumnasGrid(ws, columnas);
            ConfigurarColumnasTimeline(ws, ganttModel, timelineColStart, timelineCellWidth);

            int timelineWidthPx = Math.Max(1, ganttModel.Escala.Count) * Math.Max(40, timelineCellWidth);
            var totalDays = Math.Max(1d, (ganttModel.FechaFinRango.Date - ganttModel.FechaInicioRango.Date).TotalDays + 1d);

            for (int i = 0; i < filas.Count; i++)
            {
                var filaExport = filas[i];
                int excelRow = fila + i;
                int rowHeightPx = CalcularAlturaFilaPx(columnas, filaExport, ganttModel.ViewMode == GanttViewMode.Mixto ? 36 : 28);
                ws.Row(excelRow).Height = PixelsToPoints(rowHeightPx);

                for (int colIndex = 0; colIndex < columnas.Count; colIndex++)
                {
                    var col = columnas[colIndex];
                    var cell = ws.Cell(excelRow, colIndex + 1);
                    var texto = EsColumnaImporte(col) && filaExport.EsResumen ? string.Empty : filaExport.GetTexto(col.Name);
                    cell.Value = texto;
                    AplicarEstiloContenido(cell, col);
                }

                EscribirContenidoTimeline(ws, excelRow, timelineColStart, ganttModel, filaExport, ganttModel.ViewMode);

                if (filaExport.Inicio.HasValue && filaExport.Fin.HasValue)
                {
                    var x0 = (float)(((filaExport.Inicio.Value.Date - ganttModel.FechaInicioRango.Date).TotalDays / totalDays) * timelineWidthPx);
                    var x1 = (float)((((filaExport.Fin.Value.Date.AddDays(1) - ganttModel.FechaInicioRango.Date).TotalDays) / totalDays) * timelineWidthPx);
                    if (x1 > x0)
                    {
                        var color = filaExport.EsCritica ? ganttVisualSettings.CriticalBarColor : filaExport.EsResumen ? ganttVisualSettings.SummaryBarColor : ganttVisualSettings.NormalBarColor;
                        var barPath = CrearBarraTemporalPng(timelineWidthPx, rowHeightPx, x0, x1, color, filaExport.EsResumen);
                        try
                        {
                            ws.AddPicture(barPath)
                              .MoveTo(ws.Cell(excelRow, timelineColStart))
                              .WithSize(timelineWidthPx, rowHeightPx);
                        }
                        finally
                        {
                            try { File.Delete(barPath); } catch { }
                        }
                    }
                }
            }

            int filaFinalDatos = fila + filas.Count - 1;
            int? importeColumnIndex = ObtenerIndiceColumnaImporte(columnas);
            int filaFooterInicio = filaFinalDatos + 2;
            int filaFooterFin = filaFinalDatos;

            if (importeColumnIndex.HasValue)
            {
                EscribirTotalColumnaImporte(ws, filas, columnas.Count, importeColumnIndex.Value, filaFooterInicio);
                filaFooterFin = Math.Max(filaFooterFin, filaFooterInicio);
            }

            if (ganttModel.ViewMode == GanttViewMode.Erogaciones)
            {
                int footerBaseRow = Math.Max(filaFooterInicio, filaFooterFin + 2);
                var footerModes = new[]
                {
                    GanttFooterDisplayMode.ImportePeriodo,
                    GanttFooterDisplayMode.ImporteAcumulado,
                    GanttFooterDisplayMode.PorcentajePeriodo,
                    GanttFooterDisplayMode.PorcentajeAcumulado
                };
                var footerValores = ConstruirFooterPorEscala(ganttModel, filas);

                for (int footerIndex = 0; footerIndex < footerModes.Length; footerIndex++)
                {
                    var currentMode = footerModes[footerIndex];
                    int footerRow = footerBaseRow + footerIndex;
                    ws.Cell(footerRow, 1).Value = ObtenerTituloFooter(currentMode);
                    ws.Range(footerRow, 1, footerRow, columnas.Count).Merge();
                    ws.Range(footerRow, 1, footerRow, columnas.Count).Style.Font.Bold = true;
                    ws.Range(footerRow, 1, footerRow, columnas.Count).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Row(footerRow).Height = 18;

                    for (int i = 0; i < footerValores.Count; i++)
                    {
                        var footer = footerValores[i];
                        var cell = ws.Cell(footerRow, timelineColStart + i);
                        cell.Value = ObtenerTextoFooter(footer, currentMode);
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                        cell.Style.Font.FontSize = 8;
                        cell.Style.Font.Bold = true;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    filaFooterFin = Math.Max(filaFooterFin, footerRow);
                }
            }

            ws.SheetView.Freeze(filaInicioDatos, timelineColStart);
            ws.Range(headerTopRow, 1, Math.Max(filaFinalDatos, filaFooterFin), numCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerTopRow, 1, Math.Max(filaFinalDatos, filaFooterFin), numCols).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Left = 0.35;
            ws.PageSetup.Margins.Right = 0.35;
            ws.PageSetup.Margins.Top = 0.55;
            ws.PageSetup.Margins.Bottom = 0.55;

            wb.SaveAs(rutaDestino);
            return rutaDestino;
        }

        protected static List<ProgramaExcelColumnExport> CapturarColumnasVisibles(DataGridView grid)
        {
            return grid.Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Visible && c.Name != "colDummy")
                .OrderBy(c => c.DisplayIndex)
                .Select(c => new ProgramaExcelColumnExport
                {
                    Name = c.Name,
                    HeaderText = c.HeaderText,
                    Width = c.Width,
                    FontName = c.DefaultCellStyle.Font?.FontFamily.Name ?? c.InheritedStyle.Font?.FontFamily.Name ?? "Segoe UI",
                    FontSize = c.DefaultCellStyle.Font?.Size ?? c.InheritedStyle.Font?.Size ?? 9f,
                    Bold = c.DefaultCellStyle.Font?.Bold ?? c.InheritedStyle.Font?.Bold ?? false,
                    Italic = c.DefaultCellStyle.Font?.Italic ?? c.InheritedStyle.Font?.Italic ?? false,
                    ForeColor = TryGetColor(c.DefaultCellStyle.ForeColor, XLColor.Black),
                    BackColor = TryGetColor(c.DefaultCellStyle.BackColor, XLColor.White),
                    Alignment = ConvertAlignment(c.DefaultCellStyle.Alignment),
                    WrapText = c.DefaultCellStyle.WrapMode == DataGridViewTriState.True
                })
                .ToList();
        }

        protected static void EscribirEncabezadosIzquierda(IXLWorksheet ws, List<ProgramaExcelColumnExport> columnas, int rowTop, int rowBottom)
        {
            for (int i = 0; i < columnas.Count; i++)
            {
                var col = columnas[i];
                var range = ws.Range(rowTop, i + 1, rowBottom, i + 1);
                range.Merge();
                range.Value = col.HeaderText;
                AplicarEstiloEncabezado(range);
            }
        }

        protected static void EscribirEncabezadoTimeline(IXLWorksheet ws, GanttRenderModel model, int timelineColStart, int rowTop, int rowBottom)
        {
            var index = 0;
            while (index < model.Escala.Count)
            {
                var current = model.Escala[index];
                var group = current.GrupoEtiqueta ?? string.Empty;
                int start = timelineColStart + index;
                int end = start;
                while (index + 1 < model.Escala.Count && string.Equals(model.Escala[index + 1].GrupoEtiqueta, group, StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    end = timelineColStart + index;
                }
                var groupRange = ws.Range(rowTop, start, rowTop, end);
                groupRange.Merge();
                groupRange.Value = group;
                AplicarEstiloEncabezado(groupRange);
                index++;
            }

            for (int i = 0; i < model.Escala.Count; i++)
            {
                var cell = ws.Cell(rowBottom, timelineColStart + i);
                cell.Value = model.Escala[i].Etiqueta;
                var range = ws.Range(rowBottom, timelineColStart + i, rowBottom, timelineColStart + i);
                AplicarEstiloEncabezado(range);
            }
            ws.Row(rowTop).Height = 20;
            ws.Row(rowBottom).Height = 22;
        }

        protected static void EscribirContenidoTimeline(IXLWorksheet ws, int excelRow, int timelineColStart, GanttRenderModel ganttModel, ProgramaExcelRowExport fila, GanttViewMode modo)
        {
            for (int i = 0; i < ganttModel.Escala.Count; i++)
            {
                var cell = ws.Cell(excelRow, timelineColStart + i);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Font.FontSize = 8;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D7DDE5");
                cell.Style.Fill.BackgroundColor = XLColor.White;
            }

            if (modo == GanttViewMode.ProgramaObra)
                return;

            foreach (var seg in fila.SegmentosFinancieros)
            {
                int idx = FindScaleIndex(ganttModel, seg);
                if (idx < 0)
                    continue;

                var cell = ws.Cell(excelRow, timelineColStart + idx);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Font.FontSize = 8;
                cell.Value = modo == GanttViewMode.Erogaciones
                    ? seg.ImporteProgramado.ToStringImporte()
                    : $"{seg.PorcentajeFisicoGlobal.ToStringPorcentaje()}% fis.\n{seg.PorcentajeFinancieroGlobal.ToStringPorcentaje()}% finan.";
            }
        }

        protected static int FindScaleIndex(GanttRenderModel model, GanttPeriodSegmentDto seg)
        {
            for (int i = 0; i < model.Escala.Count; i++)
            {
                var cell = model.Escala[i];
                if (cell.FechaInicio.Date == seg.FechaInicio.Date && cell.FechaFin.Date == seg.FechaFin.Date)
                    return i;
                if (seg.FechaInicio.Date >= cell.FechaInicio.Date && seg.FechaInicio.Date <= cell.FechaFin.Date)
                    return i;
                if (seg.FechaFin.Date >= cell.FechaInicio.Date && seg.FechaFin.Date <= cell.FechaFin.Date)
                    return i;
            }
            return -1;
        }

        protected static void AplicarEstiloEncabezado(IXLRange range)
        {
            range.Style.Font.FontName = "Segoe UI";
            range.Style.Font.FontSize = 9;
            range.Style.Font.Bold = true;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = XLColor.White;
            range.Style.Alignment.WrapText = true;
        }

        protected static void AplicarEstiloContenido(IXLCell cell, ProgramaExcelColumnExport col)
        {
            cell.Style.Font.FontName = string.IsNullOrWhiteSpace(col.FontName) ? "Segoe UI" : col.FontName;
            cell.Style.Font.FontSize = Math.Max(8, col.FontSize <= 0 ? 9 : col.FontSize);
            cell.Style.Font.Bold = col.Bold;
            cell.Style.Font.Italic = col.Italic;
            cell.Style.Font.FontColor = col.ForeColor;
            cell.Style.Fill.BackgroundColor = col.BackColor;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.Horizontal = col.Alignment;
            cell.Style.Alignment.WrapText = col.WrapText;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D7DDE5");
        }

        protected static void ConfigurarColumnasGrid(IXLWorksheet ws, List<ProgramaExcelColumnExport> columnas)
        {
            for (int i = 0; i < columnas.Count; i++)
                ws.Column(i + 1).Width = PixelsToExcelWidth(columnas[i].Width);
        }

        protected static void ConfigurarColumnasTimeline(IXLWorksheet ws, GanttRenderModel ganttModel, int timelineColStart, int timelineCellWidth)
        {
            var width = PixelsToExcelWidth(Math.Max(40, timelineCellWidth));
            for (int i = 0; i < Math.Max(1, ganttModel.Escala.Count); i++)
                ws.Column(timelineColStart + i).Width = width;
        }

        protected static int CalcularAlturaFilaPx(List<ProgramaExcelColumnExport> columnas, ProgramaExcelRowExport fila, int alturaBasePx)
        {
            int altura = alturaBasePx;
            foreach (var col in columnas)
            {
                if (!col.WrapText)
                    continue;

                var texto = fila.GetTexto(col.Name);
                if (string.IsNullOrWhiteSpace(texto))
                    continue;

                using var font = new Font(
                    string.IsNullOrWhiteSpace(col.FontName) ? "Segoe UI" : col.FontName,
                    Math.Max(8f, col.FontSize),
                    (col.Bold ? FontStyle.Bold : FontStyle.Regular) | (col.Italic ? FontStyle.Italic : FontStyle.Regular));

                int anchoPx = Math.Max(24, col.Width - 8);
                var proposed = new Size(anchoPx, int.MaxValue);
                var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
                var measured = TextRenderer.MeasureText(texto, font, proposed, flags);
                int requerida = Math.Max(alturaBasePx, measured.Height + 6);
                if (requerida > altura)
                    altura = requerida;
            }

            return altura;
        }

        protected static string CrearBarraTemporalPng(int width, int height, float x0, float x1, Color color, bool summary)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            var path = Path.Combine(Path.GetTempPath(), $"sopro_gantt_{Guid.NewGuid():N}.png");
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var barHeight = summary ? 12 : 9;
            var y = Math.Max(2, height - barHeight - 3);
            var rect = RectangleF.FromLTRB(Math.Max(0, x0), y, Math.Max(x0 + 2, x1), y + barHeight);
            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.FromArgb(180, color), 1f);
            g.FillRectangle(brush, rect);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            bmp.Save(path, ImageFormat.Png);
            return path;
        }

        protected static string ObtenerTituloFooter(GanttFooterDisplayMode mode) => mode switch
        {
            GanttFooterDisplayMode.ImportePeriodo => "Importe por período",
            GanttFooterDisplayMode.ImporteAcumulado => "Importe acumulado",
            GanttFooterDisplayMode.PorcentajePeriodo => "% por período",
            GanttFooterDisplayMode.PorcentajeAcumulado => "% acumulado",
            _ => string.Empty
        };

        protected static string ObtenerTextoFooter(GanttFooterPeriodDto footer, GanttFooterDisplayMode mode) => mode switch
        {
            GanttFooterDisplayMode.ImportePeriodo => footer.ImportePeriodo.ToStringImporte(),
            GanttFooterDisplayMode.ImporteAcumulado => footer.ImporteAcumulado.ToStringImporte(),
            GanttFooterDisplayMode.PorcentajePeriodo => $"{footer.PorcentajePeriodo.ToStringPorcentaje()}%",
            GanttFooterDisplayMode.PorcentajeAcumulado => $"{footer.PorcentajeAcumulado.ToStringPorcentaje()}%",
            _ => string.Empty
        };

        protected static bool EsColumnaImporte(ProgramaExcelColumnExport columna)
        {
            var header = (columna.HeaderText ?? string.Empty).Trim();
            var name = (columna.Name ?? string.Empty).Trim();
            return string.Equals(header, "Importe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "colImporte", StringComparison.OrdinalIgnoreCase);
        }

        protected static List<GanttFooterPeriodDto> ConstruirFooterPorEscala(GanttRenderModel model, List<ProgramaExcelRowExport> filas)
        {
            var resultado = new List<GanttFooterPeriodDto>();
            var filasBase = filas.Where(x => !x.EsResumen).ToList();
            var totalImporte = filasBase.SelectMany(x => x.SegmentosFinancieros).Sum(x => x.ImporteProgramado);
            decimal acumuladoImporte = 0m;
            decimal acumuladoPorcentaje = 0m;

            for (int i = 0; i < model.Escala.Count; i++)
            {
                var scale = model.Escala[i];
                decimal importePeriodo = filasBase
                    .SelectMany(x => x.SegmentosFinancieros)
                    .Where(seg => FindScaleIndex(model, seg) == i)
                    .Sum(seg => seg.ImporteProgramado);

                var porcentajePeriodo = totalImporte == 0m
                    ? 0m
                    : decimal.Round((importePeriodo / totalImporte) * 100m, 4, MidpointRounding.AwayFromZero);

                acumuladoImporte += importePeriodo;
                acumuladoPorcentaje += porcentajePeriodo;

                resultado.Add(new GanttFooterPeriodDto
                {
                    Etiqueta = scale.Etiqueta,
                    FechaInicio = scale.FechaInicio,
                    FechaFin = scale.FechaFin,
                    ImportePeriodo = importePeriodo,
                    ImporteAcumulado = acumuladoImporte,
                    PorcentajePeriodo = porcentajePeriodo,
                    PorcentajeAcumulado = acumuladoPorcentaje
                });
            }

            return resultado;
        }

        protected static int? ObtenerIndiceColumnaImporte(List<ProgramaExcelColumnExport> columnas)
        {
            for (int i = 0; i < columnas.Count; i++)
            {
                var header = (columnas[i].HeaderText ?? string.Empty).Trim();
                if (string.Equals(header, "Importe", StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }
            return null;
        }

        protected static void EscribirTotalColumnaImporte(IXLWorksheet ws, List<ProgramaExcelRowExport> filas, int totalColumnasIzquierda, int importeColumnIndex, int row)
        {
            decimal total = filas.Where(x => !x.EsResumen).Sum(x => x.GetDecimal("colImporte") ?? x.GetDecimal("Importe") ?? 0m);

            if (totalColumnasIzquierda > 1)
            {
                ws.Cell(row, 1).Value = "TOTAL IMPORTE";
                ws.Range(row, 1, row, Math.Max(1, importeColumnIndex - 1)).Merge();
                ws.Range(row, 1, row, Math.Max(1, importeColumnIndex - 1)).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Range(row, 1, row, Math.Max(1, importeColumnIndex - 1)).Style.Font.Bold = true;
            }

            var totalCell = ws.Cell(row, importeColumnIndex);
            totalCell.Value = total;
            totalCell.Style.NumberFormat.Format = "$#,##0.00####";
            totalCell.Style.Font.Bold = true;
            totalCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            totalCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF8E1");
            totalCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        protected static double PixelsToExcelWidth(int pixels)
        {
            if (pixels <= 12)
                return pixels / 12d;
            return Math.Round((pixels - 5d) / 7d, 2);
        }

        protected static double PixelsToPoints(int pixels) => pixels * 0.75d;

        protected static string SanitizarNombre(string valor)
        {
            var invalids = Path.GetInvalidFileNameChars();
            return string.Concat(valor.Select(c => invalids.Contains(c) ? '_' : c));
        }

        private static XLColor TryGetColor(Color color, XLColor fallback)
        {
            if (color.IsEmpty) return fallback;
            // XLColor.FromColor puede fallar con colores nombrados ("White", "Black", etc.)
            // en algunas versiones de ClosedXML. Convertimos siempre a hex ARGB explícito.
            try
            {
                return XLColor.FromArgb(color.A, color.R, color.G, color.B);
            }
            catch
            {
                return fallback;
            }
        }

        private static XLAlignmentHorizontalValues ConvertAlignment(DataGridViewContentAlignment alignment)
        {
            return alignment switch
            {
                DataGridViewContentAlignment.BottomCenter or DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.TopCenter => XLAlignmentHorizontalValues.Center,
                DataGridViewContentAlignment.BottomRight or DataGridViewContentAlignment.MiddleRight or DataGridViewContentAlignment.TopRight => XLAlignmentHorizontalValues.Right,
                _ => XLAlignmentHorizontalValues.Left
            };
        }

        protected sealed class ProgramaExcelColumnExport
        {
            public string Name { get; set; } = string.Empty;
            public string HeaderText { get; set; } = string.Empty;
            public int Width { get; set; }
            public string FontName { get; set; } = "Segoe UI";
            public float FontSize { get; set; } = 9f;
            public bool Bold { get; set; }
            public bool Italic { get; set; }
            public XLColor ForeColor { get; set; } = XLColor.Black;
            public XLColor BackColor { get; set; } = XLColor.White;
            public XLAlignmentHorizontalValues Alignment { get; set; } = XLAlignmentHorizontalValues.Left;
            public bool WrapText { get; set; }
        }

        protected sealed class ProgramaExcelRowExport
        {
            public int? ItemId { get; set; }
            public Dictionary<string, string> Valores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, object?> RawValores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public DateTime? Inicio { get; set; }
            public DateTime? Fin { get; set; }
            public bool EsResumen { get; set; }
            public bool EsCritica { get; set; }
            public List<GanttPeriodSegmentDto> SegmentosFinancieros { get; set; } = new();
            public string GetTexto(string columnName) => Valores.TryGetValue(columnName, out var value) ? value : string.Empty;
            public decimal? GetDecimal(string columnName)
            {
                if (!RawValores.TryGetValue(columnName, out var value) || value == null)
                    return null;
                if (value is decimal dec)
                    return dec;
                if (value is int i)
                    return i;
                if (value is long l)
                    return l;
                if (value is double d)
                    return (decimal)d;
                if (value is float f)
                    return (decimal)f;
                if (decimal.TryParse(Convert.ToString(value, CultureInfo.CurrentCulture), NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed))
                    return parsed;
                return null;
            }
        }
    }
}
