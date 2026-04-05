using System.Drawing.Drawing2D;
using System.Drawing.Text;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Models;

namespace SOPRO.WinForms.Controls
{
    internal sealed class GanttTimelineControl : ScrollableControl
    {
        private const int HeaderHeight = 58;
        private const int LeftPadding = 8;
        private const int RightPadding = 8;
        private const int TopPadding = 4;
        private const int BottomPadding = 8;
        private const int FooterHeaderHeight = 18;
        private const int FooterRowHeight = 18;
        private const int FooterSpacing = 6;

        private DataGridView? _sourceGrid;
        private GanttRenderModel? _renderModel;
        private readonly Dictionary<int, GanttRowDto> _filasPorId = new();
        private int? _selectedActivityId;
        private bool _invalidatePending;
        private GanttFooterDisplayMode _footerDisplayMode = GanttFooterDisplayMode.Ninguno;
        private GanttSegmentLabelPosition _segmentLabelPosition = GanttSegmentLabelPosition.Arriba;
        private int _timelineCellWidth = 64;
        private bool _isResizingTimeline;
        private int _resizeStartX;
        private int _resizeStartWidth;
        private const int ResizeHitPadding = 4;

        private readonly ContextMenuStrip _contextMenu;
        private readonly ToolStripMenuItem _mnuConfigurarApariencia;
        private readonly ToolStripMenuItem _mnuRestablecerApariencia;
        private GanttVisualSettings _visualSettings = GanttVisualSettings.CreateDefault();

        public event EventHandler? TimelineCellWidthChanged;
        public event EventHandler? VisualSettingsChanged;

        public GanttTimelineControl()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Opaque, true);
            DoubleBuffered = true;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 8.5f);
            AutoScroll = true;
            AutoScrollMinSize = new Size(0, 0);
            HScroll = true;
            VScroll = false;

            _contextMenu = new ContextMenuStrip();
            _mnuConfigurarApariencia = new ToolStripMenuItem("Configurar apariencia del Gantt...");
            _mnuRestablecerApariencia = new ToolStripMenuItem("Restablecer apariencia predeterminada");
            _mnuConfigurarApariencia.Click += (_, __) => AbrirConfiguracionVisual();
            _mnuRestablecerApariencia.Click += (_, __) => RestablecerAparienciaPredeterminada();
            _contextMenu.Items.AddRange(new ToolStripItem[]
            {
                _mnuConfigurarApariencia,
                new ToolStripSeparator(),
                _mnuRestablecerApariencia
            });
            ContextMenuStrip = _contextMenu;
        }


        public GanttVisualSettings VisualSettings
        {
            get => _visualSettings.Clone();
            set
            {
                _visualSettings = (value ?? GanttVisualSettings.CreateDefault()).Clone();
                RequestRefresh();
            }
        }

        public GanttRenderModel? RenderModel
        {
            get => _renderModel;
            set
            {
                _renderModel = value;
                RebuildRowLookup();
                UpdateScrollMetrics();
                RequestRefresh();
            }
        }

        public int? SelectedActivityId
        {
            get => _selectedActivityId;
            set
            {
                if (_selectedActivityId == value) return;
                _selectedActivityId = value;
                RequestRefresh();
            }
        }

        public GanttFooterDisplayMode FooterDisplayMode
        {
            get => _footerDisplayMode;
            set
            {
                if (_footerDisplayMode == value)
                    return;

                _footerDisplayMode = value;
                UpdateScrollMetrics();
                RequestRefresh();
            }
        }

        public GanttSegmentLabelPosition SegmentLabelPosition
        {
            get => _segmentLabelPosition;
            set
            {
                if (_segmentLabelPosition == value)
                    return;

                _segmentLabelPosition = value;
                RequestRefresh();
            }
        }


        public int TimelineCellWidth
        {
            get => _timelineCellWidth;
            set
            {
                var clamped = Math.Max(40, Math.Min(180, value));
                if (_timelineCellWidth == clamped)
                    return;

                _timelineCellWidth = clamped;
                UpdateScrollMetrics();
                RequestRefresh();
                TimelineCellWidthChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void AbrirConfiguracionVisual()
        {
            if (IsDisposed)
                return;

            using var form = new FormConfiguracionVisualGantt(_visualSettings);
            if (form.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            _visualSettings = form.Settings;
            VisualSettingsChanged?.Invoke(this, EventArgs.Empty);
            RequestRefresh();
        }

        private void RestablecerAparienciaPredeterminada()
        {
            _visualSettings = GanttVisualSettings.CreateDefault();
            VisualSettingsChanged?.Invoke(this, EventArgs.Empty);
            RequestRefresh();
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            AbrirConfiguracionVisual();
        }

        public void BindGrid(DataGridView grid)
        {
            if (_sourceGrid == grid)
                return;

            if (_sourceGrid != null)
            {
                _sourceGrid.Scroll -= SourceGridChanged;
                _sourceGrid.RowHeightChanged -= SourceGridRowHeightChanged;
                _sourceGrid.RowsAdded -= SourceGridChanged;
                _sourceGrid.RowsRemoved -= SourceGridChanged;
                _sourceGrid.Resize -= SourceGridChanged;
            }

            _sourceGrid = grid;

            _sourceGrid.Scroll += SourceGridChanged;
            _sourceGrid.RowHeightChanged += SourceGridRowHeightChanged;
            _sourceGrid.RowsAdded += SourceGridChanged;
            _sourceGrid.RowsRemoved += SourceGridChanged;
            _sourceGrid.Resize += SourceGridChanged;

            RequestRefresh();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left || _renderModel == null || _renderModel.Escala.Count == 0)
                return;

            if (e.Y < 0 || e.Y > HeaderHeight)
                return;

            if (!TryGetResizeBoundaryX(e.Location, out _))
                return;

            _isResizingTimeline = true;
            _resizeStartX = e.X;
            _resizeStartWidth = _timelineCellWidth;
            Capture = true;
            Cursor = Cursors.VSplit;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isResizingTimeline)
            {
                var delta = e.X - _resizeStartX;
                TimelineCellWidth = _resizeStartWidth + delta;
                return;
            }

            Cursor = TryGetResizeBoundaryX(e.Location, out _) ? Cursors.VSplit : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (!_isResizingTimeline)
                return;

            _isResizingTimeline = false;
            Capture = false;
            Cursor = TryGetResizeBoundaryX(e.Location, out _) ? Cursors.VSplit : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_isResizingTimeline)
                Cursor = Cursors.Default;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // El control pinta todo su fondo manualmente para reducir parpadeo.
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollMetrics();
            RequestRefresh();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            if (_renderModel == null || _renderModel.Filas.Count == 0 || _renderModel.Escala.Count == 0)
            {
                g.Clear(Color.White);
                if (_renderModel != null)
                {
                    using var emptyTextBrush = new SolidBrush(Color.FromArgb(64, 64, 64));
                    g.DrawString("Sin datos para diagrama de Gantt.", Font, emptyTextBrush, new PointF(LeftPadding + 8, HeaderHeight + 12));
                }
                return;
            }

            g.SmoothingMode = SmoothingMode.None;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);

            var clientBodyWidth = Math.Max(10, ClientSize.Width - LeftPadding - RightPadding);
            var preferredTimelineWidth = GetPreferredTimelineWidth();
            if (preferredTimelineWidth <= 0)
                return;

            var footerVisible = IsFinancialFooterVisible();
            var footerHeight = footerVisible ? FooterSpacing + FooterHeaderHeight + FooterRowHeight : 0;
            var preferredBodyWidth = Math.Max(10, preferredTimelineWidth - LeftPadding - RightPadding);
            var bodyWidth = Math.Max(clientBodyWidth, preferredBodyWidth);
            var availableBodyHeight = ClientSize.Height - HeaderHeight - TopPadding - BottomPadding - footerHeight;
            var bodyRect = new Rectangle(LeftPadding, HeaderHeight + TopPadding, bodyWidth, Math.Max(10, availableBodyHeight));
            var headerRect = new Rectangle(LeftPadding, 0, bodyWidth, HeaderHeight);
            var timelineRect = new Rectangle(LeftPadding, HeaderHeight + TopPadding, preferredBodyWidth, bodyRect.Height);
            var headerTimelineRect = new Rectangle(LeftPadding, 0, preferredBodyWidth, HeaderHeight);
            var fillerRect = bodyWidth > preferredBodyWidth
                ? new Rectangle(timelineRect.Right, HeaderHeight + TopPadding, bodyWidth - preferredBodyWidth, bodyRect.Height)
                : Rectangle.Empty;
            var fillerHeaderRect = bodyWidth > preferredBodyWidth
                ? new Rectangle(headerTimelineRect.Right, 0, bodyWidth - preferredBodyWidth, HeaderHeight)
                : Rectangle.Empty;
            var footerRect = footerVisible
                ? new Rectangle(LeftPadding, bodyRect.Bottom + FooterSpacing, bodyWidth, FooterHeaderHeight + FooterRowHeight)
                : Rectangle.Empty;
            var footerTimelineRect = footerVisible
                ? new Rectangle(LeftPadding, footerRect.Top, preferredBodyWidth, footerRect.Height)
                : Rectangle.Empty;
            var fillerFooterRect = footerVisible && bodyWidth > preferredBodyWidth
                ? new Rectangle(footerTimelineRect.Right, footerRect.Top, bodyWidth - preferredBodyWidth, footerRect.Height)
                : Rectangle.Empty;

            if (bodyRect.Width <= 0 || bodyRect.Height <= 0 || timelineRect.Width <= 0)
                return;

            using var borderPen = new Pen(Color.Gainsboro);
            using var gridPen = new Pen(Color.FromArgb(230, 230, 230));
            using var textBrush = new SolidBrush(Color.FromArgb(64, 64, 64));
            using var subTextBrush = new SolidBrush(Color.DimGray);
            using var headerBack = new SolidBrush(Color.FromArgb(245, 247, 250));
            using var selectedBrush = new SolidBrush(Color.FromArgb(235, 243, 255));
            using var criticalBrush = new SolidBrush(_visualSettings.CriticalBarColor);
            using var resumenBorder = new Pen(_visualSettings.SummaryBarColor, 1.2f);
            using var normalBrush = new SolidBrush(_visualSettings.NormalBarColor);
            using var normalBorder = new Pen(ControlPaint.Dark(_visualSettings.NormalBarColor), 1.1f);
            using var criticalBorder = new Pen(ControlPaint.Dark(_visualSettings.CriticalBarColor), 1.1f);
            using var financialHeaderBrush = new SolidBrush(Color.FromArgb(249, 250, 252));
            using var financialTextBrush = new SolidBrush(_visualSettings.TextColor);
            using var financialOutlineBrush = new SolidBrush(_visualSettings.OutlineColor);
            using var fontSmall = new Font("Segoe UI", 8f);
            using var fontBold = new Font("Segoe UI", 8f, FontStyle.Bold);
            using var fontTiny = new Font("Segoe UI", 7.5f);
            using var financialLabelFont = _visualSettings.CreateFinancialTextFont(8f);

            var scrollX = AutoScrollPosition.X;
            g.TranslateTransform(scrollX, 0);

            g.FillRectangle(headerBack, headerRect);
            g.DrawLine(borderPen, LeftPadding, HeaderHeight - 1, bodyRect.Right, HeaderHeight - 1);

            var totalDays = Math.Max(1d, (_renderModel.FechaFinRango.Date - _renderModel.FechaInicioRango.Date).TotalDays + 1d);
            float XForDate(DateTime date)
            {
                var offset = (date.Date - _renderModel.FechaInicioRango.Date).TotalDays;
                return timelineRect.Left + (float)(offset / totalDays * timelineRect.Width);
            }

            string? grupoActual = null;
            float grupoInicioX = bodyRect.Left;
            for (int i = 0; i < _renderModel.Escala.Count; i++)
            {
                var celda = _renderModel.Escala[i];
                var x0 = XForDate(celda.FechaInicio);
                var x1 = timelineRect.Left + (float)((celda.FechaFin.Date.AddDays(1) - _renderModel.FechaInicioRango.Date).TotalDays / totalDays * timelineRect.Width);
                var cellRect = RectangleF.FromLTRB(x0, 22, Math.Max(x0 + 1, x1), HeaderHeight - 2);

                using var altBrush = new SolidBrush(i % 2 == 0 ? Color.White : Color.FromArgb(250, 250, 250));
                g.FillRectangle(altBrush, cellRect);
                g.DrawRectangle(borderPen, cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);

                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(celda.Etiqueta, fontSmall, subTextBrush, cellRect, sf);

                if (grupoActual == null)
                {
                    grupoActual = celda.GrupoEtiqueta;
                    grupoInicioX = x0;
                }

                var grupoCambio = grupoActual != celda.GrupoEtiqueta;
                var ultimo = i == _renderModel.Escala.Count - 1;
                if (grupoCambio || ultimo)
                {
                    var endX = grupoCambio ? x0 : x1;
                    var grupoRect = RectangleF.FromLTRB(grupoInicioX, 1, Math.Max(grupoInicioX + 1, endX), 19);
                    g.DrawString(grupoActual, fontBold, textBrush, grupoRect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

                    grupoActual = celda.GrupoEtiqueta;
                    grupoInicioX = x0;
                }

                g.DrawLine(gridPen, x0, HeaderHeight, x0, bodyRect.Bottom);
                if (ultimo)
                    g.DrawLine(gridPen, x1, HeaderHeight, x1, bodyRect.Bottom);
            }

            if (_sourceGrid == null || _sourceGrid.Rows.Count == 0)
            {
                g.DrawRectangle(borderPen, bodyRect);
                g.ResetTransform();
                return;
            }

            var firstRowIndex = 0;
            try
            {
                firstRowIndex = _sourceGrid.FirstDisplayedScrollingRowIndex;
            }
            catch
            {
                firstRowIndex = 0;
            }

            if (firstRowIndex < 0)
                firstRowIndex = 0;

            var y = HeaderHeight;
            for (int i = firstRowIndex; i < _sourceGrid.Rows.Count; i++)
            {
                var row = _sourceGrid.Rows[i];
                if (!row.Visible)
                    continue;

                var rowHeight = row.Height;
                if (y > bodyRect.Bottom)
                    break;

                var rowRect = new Rectangle(bodyRect.Left, y, bodyRect.Width, rowHeight);
                var rowId = ResolveRowId(row);
                if (!rowId.HasValue)
                {
                    y += rowHeight;
                    continue;
                }

                if (_selectedActivityId.HasValue && rowId.Value == _selectedActivityId.Value)
                    g.FillRectangle(selectedBrush, rowRect);

                g.DrawLine(gridPen, bodyRect.Left, rowRect.Bottom - 1, bodyRect.Right, rowRect.Bottom - 1);

                if (!_filasPorId.TryGetValue(rowId.Value, out var modelRow))
                {
                    y += rowHeight;
                    continue;
                }

                if (modelRow.Inicio.HasValue && modelRow.Fin.HasValue)
                {
                    var barX0 = XForDate(modelRow.Inicio.Value);
                    var barX1 = timelineRect.Left + (float)((modelRow.Fin.Value.Date.AddDays(1) - _renderModel.FechaInicioRango.Date).TotalDays / totalDays * timelineRect.Width);
                    barX1 = Math.Max(barX0 + 4, barX1);

                    var barTop = rowRect.Top + Math.Max(4, (rowHeight - 12) / 2);
                    var barHeight = Math.Max(8, Math.Min(14, rowHeight - 8));
                    var barRect = RectangleF.FromLTRB(barX0, barTop, barX1, barTop + barHeight);

                    if (modelRow.EsResumen)
                    {
                        var yMid = barRect.Top + barRect.Height / 2f;
                        g.DrawLine(resumenBorder, barRect.Left, yMid, barRect.Right, yMid);
                        g.DrawLine(resumenBorder, barRect.Left, yMid, barRect.Left + 6, barRect.Top);
                        g.DrawLine(resumenBorder, barRect.Left, yMid, barRect.Left + 6, barRect.Bottom);
                        g.DrawLine(resumenBorder, barRect.Right, yMid, barRect.Right - 6, barRect.Top);
                        g.DrawLine(resumenBorder, barRect.Right, yMid, barRect.Right - 6, barRect.Bottom);
                    }
                    else
                    {
                        var brush = modelRow.EsCritica ? criticalBrush : normalBrush;
                        var pen = modelRow.EsCritica ? criticalBorder : normalBorder;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using var path = RoundedRect(barRect, 3f);
                        g.FillPath(brush, path);
                        g.DrawPath(pen, path);
                        g.SmoothingMode = SmoothingMode.None;
                    }

                    if (_renderModel.ViewMode == GanttViewMode.Erogaciones)
                    {
                        DibujarImportesFinancierosEnFila(g, textBrush, subTextBrush, rowRect, modelRow, XForDate);
                    }
                    else if (_renderModel.ViewMode == GanttViewMode.Mixto)
                    {
                        DibujarEtiquetasMixtasEnFila(g, rowRect, modelRow, XForDate);
                    }
                }

                y += rowHeight;
            }

            if (!fillerRect.IsEmpty)
            {
                using var fillerBrush = new SolidBrush(Color.White);
                g.FillRectangle(fillerBrush, fillerHeaderRect);
                g.FillRectangle(fillerBrush, fillerRect);

                g.DrawLine(gridPen, fillerRect.Left, HeaderHeight, fillerRect.Left, bodyRect.Bottom);
                g.DrawLine(gridPen, fillerRect.Right, HeaderHeight, fillerRect.Right, bodyRect.Bottom);

                var yFill = HeaderHeight;
                for (int i = firstRowIndex; i < _sourceGrid.Rows.Count; i++)
                {
                    var row = _sourceGrid.Rows[i];
                    if (!row.Visible)
                        continue;

                    var rowHeight = row.Height;
                    if (yFill > bodyRect.Bottom)
                        break;

                    g.DrawLine(gridPen, fillerRect.Left, yFill + rowHeight - 1, fillerRect.Right, yFill + rowHeight - 1);
                    yFill += rowHeight;
                }
            }

            g.DrawRectangle(borderPen, bodyRect);

            if (footerVisible && _renderModel.FooterPeriodos.Count > 0)
            {
                DibujarFooterFinanciero(g, borderPen, gridPen, headerBack, financialHeaderBrush, textBrush, financialTextBrush, fontSmall, fontTiny, footerRect, footerTimelineRect, fillerFooterRect, XForDate);
            }

            g.ResetTransform();
        }

        private void DibujarImportesFinancierosEnFila(
            Graphics g,
            Brush textBrush,
            Brush subTextBrush,
            Rectangle rowRect,
            GanttRowDto modelRow,
            Func<DateTime, float> xForDate)
        {
            if (_renderModel == null || modelRow.SegmentosFinancieros == null || modelRow.SegmentosFinancieros.Count == 0)
                return;

            using var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.NoWrap
            };

            foreach (var segmento in modelRow.SegmentosFinancieros)
            {
                if (segmento.ImporteProgramado == 0m)
                    continue;

                var labelRect = ObtenerRectanguloCeldaPeriodo(segmento, rowRect, xForDate);
                if (labelRect.Width < 8f)
                    continue;

                var texto = segmento.ImporteProgramado.ToStringImporte();
                using var baseFont = _visualSettings.CreateFinancialTextFont(8f);
                using var font = CreateBestFitFont(g, texto, baseFont, labelRect.Width - 4f, 6.25f, stringFormat);
                DrawOutlinedText(g, texto, font, _visualSettings.TextColor, _visualSettings.OutlineColor, labelRect, stringFormat, 2f);
            }
        }

        private void DibujarEtiquetasMixtasEnFila(
            Graphics g,
            Rectangle rowRect,
            GanttRowDto modelRow,
            Func<DateTime, float> xForDate)
        {
            if (_renderModel == null || modelRow.SegmentosFinancieros == null || modelRow.SegmentosFinancieros.Count == 0)
                return;

            using var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.LineLimit
            };

            foreach (var segmento in modelRow.SegmentosFinancieros)
            {
                if (segmento.PorcentajeFisicoGlobal == 0m && segmento.PorcentajeFinancieroGlobal == 0m)
                    continue;

                var labelRect = ObtenerRectanguloCeldaPeriodo(segmento, rowRect, xForDate);
                if (labelRect.Width < 18f)
                    continue;

                var texto = $"{segmento.PorcentajeFisicoGlobal.ToStringPorcentaje()}% fis.\n{segmento.PorcentajeFinancieroGlobal.ToStringPorcentaje()}% finan.";
                using var baseFont = _visualSettings.CreateFinancialTextFont(8f);
                using var font = CreateBestFitFont(g, texto, baseFont, labelRect.Width - 4f, 6.0f, stringFormat, true);
                DrawOutlinedText(g, texto, font, _visualSettings.TextColor, _visualSettings.OutlineColor, labelRect, stringFormat, 2f);
            }
        }

        private RectangleF ObtenerRectanguloCeldaPeriodo(
            GanttPeriodSegmentDto segmento,
            Rectangle rowRect,
            Func<DateTime, float> xForDate)
        {
            if (_renderModel != null)
            {
                var celdaPeriodo = _renderModel.Escala.FirstOrDefault(c => c.FechaInicio.Date == segmento.FechaInicio.Date && c.FechaFin.Date == segmento.FechaFin.Date)
                    ?? _renderModel.Escala.FirstOrDefault(c => segmento.FechaInicio.Date >= c.FechaInicio.Date && segmento.FechaInicio.Date <= c.FechaFin.Date)
                    ?? _renderModel.Escala.FirstOrDefault(c => segmento.FechaFin.Date >= c.FechaInicio.Date && segmento.FechaFin.Date <= c.FechaFin.Date);

                if (celdaPeriodo != null)
                {
                    var cellX0 = xForDate(celdaPeriodo.FechaInicio);
                    var cellX1 = xForDate(celdaPeriodo.FechaFin.Date.AddDays(1));
                    if (cellX1 > cellX0)
                    {
                        return RectangleF.FromLTRB(cellX0 + 1f, rowRect.Top + 1f, Math.Max(cellX0 + 2f, cellX1 - 1f), rowRect.Bottom - 1f);
                    }
                }
            }

            var x0 = xForDate(segmento.FechaInicio);
            var x1 = xForDate(segmento.FechaFin.Date.AddDays(1));
            return RectangleF.FromLTRB(x0 + 1f, rowRect.Top + 1f, Math.Max(x0 + 2f, x1 - 1f), rowRect.Bottom - 1f);
        }

        private static Font CreateBestFitFont(Graphics g, string texto, Font baseFont, float maxWidth, float minSize, StringFormat format, bool multiline = false)
        {
            var size = baseFont.Size;
            Font? candidate = null;
            while (size >= minSize)
            {
                candidate?.Dispose();
                candidate = new Font(baseFont.FontFamily, size, baseFont.Style);
                var measured = g.MeasureString(multiline ? texto.Replace("\n", " ") : texto, candidate, SizeF.Empty, format);
                if (measured.Width <= maxWidth)
                    return candidate;

                size -= 0.5f;
            }

            candidate?.Dispose();
            return new Font(baseFont.FontFamily, minSize, baseFont.Style);
        }

        private static void DrawOutlinedText(
            Graphics g,
            string texto,
            Font font,
            Color fillColor,
            Color outlineColor,
            RectangleF layoutRect,
            StringFormat format,
            float outlineWidth)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return;

            using var path = new GraphicsPath();
            var emSize = g.DpiY * font.SizeInPoints / 72f;
            var fontStyle = (int)font.Style;

            var lineHeight = font.GetHeight(g);
            var measured = g.MeasureString(texto, font, SizeF.Empty, format);

            float x = layoutRect.Left;
            if (format.Alignment == StringAlignment.Center)
                x = layoutRect.Left + (layoutRect.Width - measured.Width) / 2f;
            else if (format.Alignment == StringAlignment.Far)
                x = layoutRect.Right - measured.Width;

            float y = layoutRect.Top;
            if (format.LineAlignment == StringAlignment.Center)
                y = layoutRect.Top + (layoutRect.Height - lineHeight) / 2f;
            else if (format.LineAlignment == StringAlignment.Far)
                y = layoutRect.Bottom - lineHeight;

            path.AddString(texto, font.FontFamily, fontStyle, emSize, new PointF(x, y), StringFormat.GenericTypographic);

            var previousSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var outlinePen = new Pen(outlineColor, outlineWidth)
            {
                LineJoin = LineJoin.Round,
                Alignment = PenAlignment.Center
            };
            using var fillBrush = new SolidBrush(fillColor);
            g.DrawPath(outlinePen, path);
            g.FillPath(fillBrush, path);
            g.SmoothingMode = previousSmoothing;
        }

        private bool TryGetResizeBoundaryX(Point clientPoint, out int boundaryX)
        {
            boundaryX = 0;
            if (_renderModel == null || _renderModel.Escala.Count < 2)
                return false;

            if (clientPoint.Y < 0 || clientPoint.Y > HeaderHeight)
                return false;

            var virtualX = clientPoint.X - AutoScrollPosition.X;
            var totalDays = Math.Max(1d, (_renderModel.FechaFinRango.Date - _renderModel.FechaInicioRango.Date).TotalDays + 1d);
            var preferredBodyWidth = Math.Max(10, GetPreferredTimelineWidth() - LeftPadding - RightPadding);
            float XForDate(DateTime date)
            {
                var offset = (date.Date - _renderModel.FechaInicioRango.Date).TotalDays;
                return LeftPadding + (float)(offset / totalDays * preferredBodyWidth);
            }

            for (int i = 0; i < _renderModel.Escala.Count - 1; i++)
            {
                var candidate = (int)Math.Round(XForDate(_renderModel.Escala[i].FechaFin.Date.AddDays(1)));
                if (Math.Abs(virtualX - candidate) <= ResizeHitPadding)
                {
                    boundaryX = candidate;
                    return true;
                }
            }

            return false;
        }

        private void DibujarFooterFinanciero(
            Graphics g,
            Pen borderPen,
            Pen gridPen,
            Brush headerBack,
            Brush footerHeaderBrush,
            Brush textBrush,
            Brush subTextBrush,
            Font fontSmall,
            Font fontTiny,
            Rectangle footerRect,
            Rectangle footerTimelineRect,
            Rectangle fillerFooterRect,
            Func<DateTime, float> xForDate)
        {
            g.FillRectangle(headerBack, footerRect);
            var footerHeaderRect = new Rectangle(footerRect.Left, footerRect.Top, footerRect.Width, FooterHeaderHeight);
            g.FillRectangle(footerHeaderBrush, footerHeaderRect);
            g.DrawRectangle(borderPen, footerRect);
            g.DrawLine(borderPen, footerRect.Left, footerHeaderRect.Bottom, footerRect.Right, footerHeaderRect.Bottom);
            g.DrawString(ObtenerTituloFooter(), fontSmall, textBrush, new RectangleF(footerHeaderRect.Left + 6, footerHeaderRect.Top + 1, footerHeaderRect.Width - 12, footerHeaderRect.Height - 2));

            for (int i = 0; i < _renderModel!.Escala.Count; i++)
            {
                var celda = _renderModel.Escala[i];
                var x0 = xForDate(celda.FechaInicio);
                var x1 = footerTimelineRect.Left + (float)((celda.FechaFin.Date.AddDays(1) - _renderModel.FechaInicioRango.Date).TotalDays /
                    Math.Max(1d, (_renderModel.FechaFinRango.Date - _renderModel.FechaInicioRango.Date).TotalDays + 1d) * footerTimelineRect.Width);
                var cellRect = RectangleF.FromLTRB(x0, footerRect.Top + FooterHeaderHeight, Math.Max(x0 + 1, x1), footerRect.Bottom);
                g.DrawRectangle(borderPen, cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);

                var footer = ObtenerFooterParaCelda(celda);
                if (footer == null)
                    continue;

                var valueRect = new RectangleF(cellRect.Left + 1, cellRect.Top + 1, Math.Max(1, cellRect.Width - 2), FooterRowHeight - 2);
                var valueFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                var texto = ObtenerTextoFooter(footer);
                g.DrawString(texto, fontTiny, subTextBrush, valueRect, valueFormat);
            }

            if (!fillerFooterRect.IsEmpty)
            {
                using var fillerBrush = new SolidBrush(Color.White);
                g.FillRectangle(fillerBrush, fillerFooterRect);
                g.DrawRectangle(borderPen, fillerFooterRect);
            }
        }


        private string ObtenerTextoFooter(GanttFooterPeriodDto footer)
        {
            return _footerDisplayMode switch
            {
                GanttFooterDisplayMode.ImportePeriodo => footer.ImportePeriodo.ToStringImporte(),
                GanttFooterDisplayMode.ImporteAcumulado => footer.ImporteAcumulado.ToStringImporte(),
                GanttFooterDisplayMode.PorcentajePeriodo => $"{footer.PorcentajePeriodo.ToStringPorcentaje()}%",
                GanttFooterDisplayMode.PorcentajeAcumulado => $"{footer.PorcentajeAcumulado.ToStringPorcentaje()}%",
                _ => string.Empty
            };
        }

        private string ObtenerTituloFooter()
        {
            return _footerDisplayMode switch
            {
                GanttFooterDisplayMode.ImportePeriodo => "Importe por período",
                GanttFooterDisplayMode.ImporteAcumulado => "Importe acumulado",
                GanttFooterDisplayMode.PorcentajePeriodo => "% por período",
                GanttFooterDisplayMode.PorcentajeAcumulado => "% acumulado",
                _ => "Erogaciones programadas"
            };
        }

        private GanttFooterPeriodDto? ObtenerFooterParaCelda(GanttScaleCellDto celda)
        {
            if (_renderModel?.FooterPeriodos == null)
                return null;

            foreach (var footer in _renderModel.FooterPeriodos)
            {
                if (footer.FechaInicio.Date <= celda.FechaFin.Date && footer.FechaFin.Date >= celda.FechaInicio.Date)
                    return footer;
            }

            return null;
        }

        private bool IsFinancialFooterVisible()
        {
            return _renderModel != null
                   && _renderModel.ViewMode == GanttViewMode.Erogaciones
                   && _footerDisplayMode != GanttFooterDisplayMode.Ninguno
                   && _renderModel.FooterPeriodos.Count > 0;
        }

        private void RebuildRowLookup()
        {
            _filasPorId.Clear();
            if (_renderModel?.Filas == null)
                return;

            foreach (var fila in _renderModel.Filas)
                _filasPorId[fila.Id] = fila;
        }

        private int GetPreferredTimelineWidth()
        {
            if (_renderModel == null || _renderModel.Escala.Count == 0)
                return 0;

            var basePixelsPerCell = _renderModel.TipoPeriodo switch
            {
                TipoPeriodoPrograma.Dia => 28,
                TipoPeriodoPrograma.Semana => 52,
                TipoPeriodoPrograma.Quincena => 64,
                TipoPeriodoPrograma.Mes => 82,
                _ => 52
            };

            var pixelsPerCell = Math.Max(40, _timelineCellWidth > 0 ? _timelineCellWidth : basePixelsPerCell);
            return (_renderModel.Escala.Count * pixelsPerCell) + LeftPadding + RightPadding;
        }

        private void UpdateScrollMetrics()
        {
            var preferredWidth = Math.Max(0, GetPreferredTimelineWidth());
            AutoScrollMinSize = new Size(preferredWidth, 0);
        }

        private void SourceGridChanged(object? sender, EventArgs e)
        {
            if (!IsHandleCreated || IsDisposed)
                return;

            RequestRefresh();
        }

        private void SourceGridRowHeightChanged(object? sender, DataGridViewRowEventArgs e)
        {
            if (!IsHandleCreated || IsDisposed)
                return;

            RequestRefresh();
        }

        public void RequestRefresh()
        {
            if (!IsHandleCreated || IsDisposed || !Visible)
                return;

            if (_invalidatePending)
                return;

            _invalidatePending = true;
            BeginInvoke(new Action(() =>
            {
                _invalidatePending = false;
                if (IsDisposed || !IsHandleCreated || !Visible)
                    return;

                Invalidate();
            }));
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static int? ResolveRowId(DataGridViewRow row)
        {
            if (row == null)
                return null;

            var dataBoundItem = row.DataBoundItem;
            if (dataBoundItem != null)
            {
                var type = dataBoundItem.GetType();
                foreach (var propertyName in new[] { "Id", "ActividadId", "InsumoId", "ItemId" })
                {
                    var prop = type.GetProperty(propertyName);
                    if (prop?.PropertyType == typeof(int))
                        return (int?)prop.GetValue(dataBoundItem);
                }
            }

            if (row.Tag is int tagId)
                return tagId;

            // Nota: si un int? con valor se almacena en object, se boxea como int; si no tiene valor, queda null.
            // Por eso con revisar el caso int y null es suficiente para row.Tag.

            return null;
        }
    }
}
