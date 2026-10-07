using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Tokens de tema centralizados para acercar la interfaz WinForms al look
    /// "glass-vibrant" de la web SOPRO. No depende de librerias de terceros ni
    /// de fuentes empaquetadas: solo usa la familia Segoe UI del sistema.
    ///
    /// Este archivo es infraestructura pura: agrupa la paleta y la tipografia
    /// para evitar hexadecimales dispersos por los formularios. Los metodos de
    /// pintura que consumen estos tokens se agregan por formulario.
    /// </summary>
    public static class SoproTheme
    {
        /// <summary>Familia tipografica unica del tema (no se empaquetan fuentes).</summary>
        private const string FontFamily = "Segoe UI";

        /// <summary>Angulo del gradiente del encabezado, en grados (equivalente web: 35deg).</summary>
        public const float HeaderGradientAngle = 35f;

        /// <summary>
        /// Paleta de colores del tema SOPRO. Los valores replican los tokens de
        /// la web (brand, ink, superficies y rejilla).
        /// </summary>
        public static class Colors
        {
            // --- Marca (brand) ---
            public static readonly Color Brand = Color.FromArgb(0x1E, 0x40, 0xAF);       // #1e40af
            public static readonly Color BrandMd = Color.FromArgb(0x1D, 0x4E, 0xD8);     // #1d4ed8
            public static readonly Color BrandLt = Color.FromArgb(0x3B, 0x82, 0xF6);     // #3b82f6

            // --- Acento (accent) ---
            public static readonly Color Accent = Color.FromArgb(0x08, 0x91, 0xB2);      // #0891b2
            public static readonly Color AccentLt = Color.FromArgb(0x22, 0xD3, 0xEE);    // #22d3ee

            // --- Texto (ink) ---
            public static readonly Color Ink = Color.FromArgb(0x0A, 0x0F, 0x1E);         // #0a0f1e
            public static readonly Color Ink2 = Color.FromArgb(0x1E, 0x29, 0x3B);        // #1e293b
            public static readonly Color Muted = Color.FromArgb(0x4B, 0x5A, 0x72);       // #4b5a72

            // --- Fondos (backgrounds) ---
            public static readonly Color BgBase = Color.FromArgb(0xDB, 0xEA, 0xFE);      // #dbeafe
            public static readonly Color BgTint2 = Color.FromArgb(0xE0, 0xE7, 0xFF);     // #e0e7ff

            // --- Superficies (surfaces) ---
            public static readonly Color Surface = Color.FromArgb(0xFF, 0xFF, 0xFF);     // #ffffff
            public static readonly Color SurfaceBorder = Color.FromArgb(0xC7, 0xD7, 0xF0); // #c7d7f0

            // --- Rejilla (grid) ---
            public static readonly Color RowAlt = Color.FromArgb(0xF4, 0xF7, 0xFD);      // #f4f7fd
            public static readonly Color GridLine = Color.FromArgb(0xE3, 0xE9, 0xF4);    // #e3e9f4
            public static readonly Color SelBg = Color.FromArgb(0xDB, 0xEA, 0xFE);       // #dbeafe

            // --- Chrome del host (header, sidebar y ribbon) ---
            public static readonly Color SidebarBg = Color.FromArgb(0xFA, 0xFA, 0xFA);       // 250,250,250
            public static readonly Color SidebarHeaderBg = Color.FromArgb(0xF0, 0xF0, 0xF3); // 240,240,243
            public static readonly Color RibbonSeparator = Color.FromArgb(0xC8, 0xC8, 0xC8); // 200,200,200
            public static readonly Color Disabled = Color.FromArgb(0x78, 0x78, 0x78);        // 120,120,120

            // --- Derivados usados por el encabezado y las tarjetas de FormPrincipal ---
            public static readonly Color SubtitleText = Color.FromArgb(0xBA, 0xE6, 0xFD); // #bae6fd
            public static readonly Color HeaderBg = Color.FromArgb(0xF1, 0xF5, 0xFB);     // #f1f5fb
            public static readonly Color CenterTop = Color.FromArgb(0xEA, 0xF1, 0xFD);    // #eaf1fd
            public static readonly Color CenterBottom = Color.FromArgb(0xED, 0xF0, 0xFB); // #edf0fb
            public static readonly Color SecondaryBg = Color.FromArgb(0xF0, 0xF8, 0xFF);  // #f0f8ff
            public static readonly Color SecondaryHoverBg = Color.FromArgb(0xF0, 0xF6, 0xFF); // #f0f6ff
            public static readonly Color GlyphIdle = Color.FromArgb(0x8A, 0x94, 0xA6);    // gris icono papelera
            public static readonly Color HoverBg = Color.FromArgb(0xEA, 0xF2, 0xFF);      // hover suave en rejilla
            public static readonly Color Glow = Color.FromArgb(0x4D, 0x3B, 0x82, 0xF6);   // glow azul (alpha 30%)
        }

        /// <summary>
        /// Tipografia del tema. Todo Segoe UI: Bold para titulos/botones/encabezados.
        /// </summary>
        public static class Fonts
        {
            public static readonly Font Title = new Font(FontFamily, 26F, FontStyle.Bold, GraphicsUnit.Point);
            public static readonly Font Subtitle = new Font(FontFamily, 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            public static readonly Font GridCell = new Font(FontFamily, 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            public static readonly Font GridHeader = new Font(FontFamily, 9F, FontStyle.Bold, GraphicsUnit.Point);
            public static readonly Font Button = new Font(FontFamily, 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            public static readonly Font GroupTitle = new Font(FontFamily, 10F, FontStyle.Bold, GraphicsUnit.Point);
        }

        // =====================================================================
        // Aplicacion del tema a FormPrincipal
        // =====================================================================

        /// <summary>
        /// Engancha toda la pintura y estilos del tema SOPRO sobre
        /// <see cref="FormPrincipal"/>: encabezado con gradiente, botones de
        /// accion, panel central, tarjeta de recientes y barra de estado.
        /// No modifica la logica ni los manejadores de eventos del formulario.
        /// </summary>
        public static void ApplyFormPrincipal(FormPrincipal form)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            ApplyHeader(form);
            ApplyCenter(form);
            ApplyRecentGrid(form);
            ApplyStatusStrip(form);
        }

        private static void ApplyHeader(FormPrincipal form)
        {
            var panelTop = form.panelTop;
            var lblTitle = form.lblTitle;
            var lblSubtitle = form.lblSubtitle;
            var btnNuevo = form.btnNuevoProyecto;
            var btnAbrir = form.btnAbrirProyecto;

            EnableDoubleBuffering(panelTop);
            panelTop.BackColor = Colors.BrandMd;
            panelTop.Height = SoproUiMetrics.HeaderHeight;

            panelTop.Paint += (_, e) =>
            {
                var rect = panelTop.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using (var brush = new LinearGradientBrush(rect, Colors.BrandMd, Colors.Accent, HeaderGradientAngle))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }

                // Linea inferior de marca (1px).
                using var pen = new Pen(Colors.Brand);
                e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
            };

            lblTitle.AutoSize = true;
            lblTitle.BackColor = Color.Transparent;
            lblTitle.Font = Fonts.Title;
            lblTitle.ForeColor = Colors.Surface;
            lblTitle.Location = new Point(24, 10);

            lblSubtitle.AutoSize = true;
            lblSubtitle.BackColor = Color.Transparent;
            lblSubtitle.Font = Fonts.Subtitle;
            lblSubtitle.ForeColor = Colors.SubtitleText;
            lblSubtitle.Location = new Point(26, 60);

            ConfigureHeaderButton(btnNuevo, "Nuevo Proyecto", SoproIconType.Mas, primary: true);
            ConfigureHeaderButton(btnAbrir, "Abrir Proyecto", SoproIconType.Carpeta, primary: false);

            void LayoutButtons()
            {
                if (panelTop.IsDisposed) return;

                const int gap = 8;
                const int rightPadding = 24;
                int height = Math.Max(btnNuevo.Height, btnAbrir.Height);
                int y = Math.Max(0, (panelTop.ClientSize.Height - height) / 2);
                int abrirX = panelTop.ClientSize.Width - rightPadding - btnAbrir.Width;
                int nuevoX = abrirX - gap - btnNuevo.Width;

                btnAbrir.Location = new Point(abrirX, y);
                btnNuevo.Location = new Point(nuevoX, y);
            }

            panelTop.SizeChanged += (_, __) => LayoutButtons();
            btnNuevo.SizeChanged += (_, __) => LayoutButtons();
            btnAbrir.SizeChanged += (_, __) => LayoutButtons();
            LayoutButtons();
        }

        private static void ConfigureHeaderButton(SoproButton button, string text, SoproIconType icon, bool primary)
        {
            button.Text = text;
            button.AutoSize = false;
            button.Cursor = Cursors.Hand;
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            button.Font = Fonts.Button;

            button.SoproContentPadding = new Padding(16, 0, 16, 0);
            button.SoproIconTextSpacing = 8;
            button.SoproIconSize = 16;
            button.SoproIcon = icon;
            button.SoproFixedHeight = 34;
            button.SoproMinimumAutoWidth = 128;

            if (primary)
            {
                button.ForeColor = Colors.Surface;
                button.BackColor = Colors.BrandMd;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Colors.Brand;
                button.FlatAppearance.MouseDownBackColor = Colors.Brand;
            }
            else
            {
                button.ForeColor = Colors.BrandMd;
                button.BackColor = Colors.SecondaryBg;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Colors.SurfaceBorder;
                button.FlatAppearance.MouseOverBackColor = Colors.SecondaryHoverBg;
                button.FlatAppearance.MouseDownBackColor = Colors.BgBase;

                // El layout interno del Button (FlatStyle.Flat) descuenta 2*BorderSize
                // del area de contenido. Con el borde de 1px este boton quedaba 2px mas
                // angosto que el texto en una sola linea: WinForms lo partia en dos
                // lineas, inflaba la altura medida del bloque texto+imagen y por eso el
                // icono se centraba mas abajo y el texto se dibujaba hacia arriba.
                // Se reserva +2 en el espaciado icono-texto (que absorbe el inset fijo
                // del layout) y se compensa con -2 en el padding horizontal para
                // conservar exactamente el ancho total ya aprobado del boton.
                button.SoproContentPadding = new Padding(15, 0, 15, 0);
                button.SoproIconTextSpacing = 10;
            }

            // Al final: recalcula ancho/alto deseados segun texto + icono + fuente.
            button.SoproAutoSizeToContent = true;

            // Recorte redondeado (Region) recalculado ante cambios de tamano/DPI.
            ApplyRoundedCorners(button, SoproUiMetrics.ThemeRadiusPx);
        }

        private static void ApplyCenter(FormPrincipal form)
        {
            var panelCenter = form.panelCenter;
            EnableDoubleBuffering(panelCenter);
            panelCenter.Padding = new Padding(28, 24, 28, 24);
            panelCenter.BackColor = Colors.CenterTop;

            panelCenter.Paint += (_, e) =>
            {
                var rect = panelCenter.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using var brush = new LinearGradientBrush(rect, Colors.CenterTop, Colors.CenterBottom, LinearGradientMode.Vertical);
                e.Graphics.FillRectangle(brush, rect);
            };

            var grp = form.grpRecientes;
            EnableDoubleBuffering(grp);
            grp.BackColor = Colors.Surface;
            grp.ForeColor = Colors.Ink;
            grp.Font = Fonts.GroupTitle;
            grp.FlatStyle = FlatStyle.Flat;
            // Inserta el contenido para dejar visible el borde/glow de la tarjeta.
            grp.Padding = new Padding(SoproUiMetrics.CardGlowPx, 0, SoproUiMetrics.CardGlowPx, SoproUiMetrics.CardGlowPx);
            grp.Paint += (_, e) => PaintRecentCard(grp, e.Graphics);
        }

        private static void ApplyRecentGrid(FormPrincipal form)
        {
            var dgv = form.dgvRecientes;
            if (dgv == null) return;

            dgv.AplicarEstiloSOPRO();

            // Seleccion + borde activo con los tokens del tema, sin encabezados de fila
            // (variante con sobrecarga: no altera el estilo por defecto de otras vistas).
            DgvCeldaHelper.Aplicar(dgv, conMenuCopia: false, colorSeleccion: Colors.SelBg, colorBorde: Colors.BrandMd, mostrarRowHeaders: false);

            dgv.GridColor = Colors.GridLine;
            dgv.BackgroundColor = Colors.Surface;
            dgv.BorderStyle = BorderStyle.None;
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = SoproUiMetrics.GridHeaderHeight;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Colors.HeaderBg;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Colors.Ink;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Colors.HeaderBg;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Colors.Ink;
            dgv.ColumnHeadersDefaultCellStyle.Font = Fonts.GridHeader;
            // Patrón read-mutate-assign-back: mutar la propiedad style directamente
            // se pierde en este runtime (el getter devuelve una copia desechable),
            // por lo que se reasigna SIEMPRE el objeto style al grid.
            var baseStyle = dgv.DefaultCellStyle;
            baseStyle.Font = Fonts.GridCell;
            baseStyle.ForeColor = Colors.Ink2;
            baseStyle.BackColor = Colors.Surface;
            baseStyle.SelectionBackColor = Colors.SelBg;
            baseStyle.SelectionForeColor = Colors.Ink;
            dgv.DefaultCellStyle = baseStyle;

            var altStyle = dgv.AlternatingRowsDefaultCellStyle;
            altStyle.BackColor = Colors.RowAlt;
            altStyle.SelectionBackColor = Colors.SelBg;
            altStyle.SelectionForeColor = Colors.Ink;
            dgv.AlternatingRowsDefaultCellStyle = altStyle;
            dgv.RowHeadersVisible = false;

            StyleDeleteColumn(dgv);
        }

        private static void StyleDeleteColumn(DataGridView dgv)
        {
            // La columna de eliminar se recrea en cada LoadRecentProjects,
            // por eso la configuracion se reaplica al agregarse la columna.
            dgv.ColumnAdded += (_, e) =>
            {
                if (e.Column == null) return;
                if (!string.Equals(e.Column.Name, "colEliminar", StringComparison.Ordinal)) return;
                ConfigureDeleteColumn(e.Column);
            };

            ConfigureDeleteColumn(dgv.Columns["colEliminar"]);

            int hoverRow = -1;
            int hoverColumn = -1;

            bool IsDeleteColumn(DataGridView grid, int columnIndex)
            {
                var deleteColumn = grid.Columns["colEliminar"];
                return deleteColumn != null && columnIndex == deleteColumn.Index;
            }

            dgv.CellMouseEnter += (_, e) =>
            {
                if (e.RowIndex < 0 || !IsDeleteColumn(dgv, e.ColumnIndex)) return;
                hoverRow = e.RowIndex;
                hoverColumn = e.ColumnIndex;
                dgv.InvalidateCell(e.ColumnIndex, e.RowIndex);
            };

            dgv.CellMouseLeave += (_, e) =>
            {
                hoverRow = -1;
                hoverColumn = -1;
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                    dgv.InvalidateCell(e.ColumnIndex, e.RowIndex);
            };

            dgv.CellPainting += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (!IsDeleteColumn(dgv, e.ColumnIndex)) return;

                var graphics = e.Graphics;
                if (graphics == null) return;

                bool selected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;
                bool hovered = e.RowIndex == hoverRow && e.ColumnIndex == hoverColumn;
                Color background = selected
                    ? Colors.SelBg
                    : hovered
                        ? Colors.HoverBg
                        : (e.RowIndex % 2 == 1 ? Colors.RowAlt : Colors.Surface);

                using (var brush = new SolidBrush(background))
                    graphics.FillRectangle(brush, e.CellBounds);

                using (var line = new Pen(Colors.GridLine))
                    graphics.DrawLine(line, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);

                if (dgv.CurrentCell != null &&
                    dgv.CurrentCell.RowIndex == e.RowIndex &&
                    dgv.CurrentCell.ColumnIndex == e.ColumnIndex)
                {
                    using var border = new Pen(Colors.BrandMd, 2);
                    graphics.DrawRectangle(border, e.CellBounds.Left + 1, e.CellBounds.Top + 1, e.CellBounds.Width - 3, e.CellBounds.Height - 3);
                }

                const int glyphSize = 16;
                Color glyphColor = hovered ? Colors.BrandMd : Colors.GlyphIdle;
                int glyphX = e.CellBounds.Left + ((e.CellBounds.Width - glyphSize) / 2);
                int glyphY = e.CellBounds.Top + ((e.CellBounds.Height - glyphSize) / 2);

                using (var glyph = SoproIconProvider.GetIcon(SoproIconType.Papelera, glyphColor, glyphSize))
                    graphics.DrawImage(glyph, new Rectangle(glyphX, glyphY, glyphSize, glyphSize));

                e.Handled = true;
            };
        }

        private static void ConfigureDeleteColumn(DataGridViewColumn? column)
        {
            if (column is not DataGridViewButtonColumn buttonColumn) return;

            // Se elimina el emoji: el glifo Papelera se pinta a mano para
            // poder colorearlo (gris en reposo, azul en hover).
            buttonColumn.Text = string.Empty;
            buttonColumn.UseColumnTextForButtonValue = true;
            buttonColumn.FlatStyle = FlatStyle.Flat;
        }

        private static void ApplyStatusStrip(FormPrincipal form)
        {
            var status = form.statusStrip;
            if (status != null)
            {
                status.BackColor = Colors.HeaderBg;
                status.ForeColor = Colors.Muted;
                status.Renderer = new SoproToolStripRenderer();
            }

            var lblStatus = form.lblStatus;
            if (lblStatus != null)
            {
                lblStatus.ForeColor = Colors.Muted;
                lblStatus.Font = Fonts.Subtitle;
            }
        }

        // =====================================================================
        // Aplicacion del tema a FormProyecto (fase 1: chrome del host)
        // =====================================================================

        /// <summary>
        /// Aplica los tokens del tema al chrome del host <see cref="FormProyecto"/>:
        /// header con gradiente (titulo/proyecto), separadores y color de icono
        /// deshabilitado del ribbon, y superficies de la barra lateral/arbol.
        /// No modifica logica, eventos ni geometria del formulario.
        /// </summary>
        public static void ApplyFormProyecto(FormProyecto form)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            ApplyProyectoHeader(form);
            ApplyProyectoSidebar(form);
            ApplyProyectoRibbonChrome(form);
        }

        private static void ApplyProyectoHeader(FormProyecto form)
        {
            var panelTop = form.panelTop;
            var lblTitulo = form.lblTitulo;
            var lblProyecto = form.lblProyecto;

            EnableDoubleBuffering(panelTop);
            // RibbonBaseColor deriva de panelTop.BackColor: debe ser BrandMd.
            panelTop.BackColor = Colors.BrandMd;

            panelTop.Paint += (_, e) =>
            {
                var rect = panelTop.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using (var brush = new LinearGradientBrush(rect, Colors.BrandMd, Colors.Accent, HeaderGradientAngle))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }

                // Linea inferior de marca (1px).
                using var pen = new Pen(Colors.Brand);
                e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
            };

            lblTitulo.BackColor = Color.Transparent;
            lblTitulo.ForeColor = Colors.Surface;
            lblProyecto.BackColor = Color.Transparent;
            lblProyecto.ForeColor = Colors.Surface;
        }

        private static void ApplyProyectoSidebar(FormProyecto form)
        {
            form.panelLeft.BackColor = Colors.SidebarBg;
            form.panelSidebarHeader.BackColor = Colors.SidebarHeaderBg;
            form.treeMenu.BackColor = Colors.SidebarBg;
        }

        private static void ApplyProyectoRibbonChrome(FormProyecto form)
        {
            foreach (var separator in new[]
            {
                form.label1, form.label2, form.lblSepReporte, form.lblSepGlobal,
                form.lblSepAlin, form.lblSepEstilo, form.lblSepFuente
            })
            {
                if (separator != null)
                    separator.BackColor = Colors.RibbonSeparator;
            }

            foreach (var button in new[]
            {
                form.btnPdfRibbon, form.btnExcelRibbon, form.btnDepurarRibbon,
                form.btnRecalcularRibbon, form.btnBuscarRibbon, form.btnWrapRibbon,
                form.btnConsolidarInsumos, form.btnAplicarATodas, form.btnAlinAba,
                form.btnAlinMed, form.btnAlinJus, form.btnAlinDer, form.btnAlinCen,
                form.btnAlinIzq
            })
            {
                if (button != null)
                    button.SoproDisabledIconColor = Colors.Disabled;
            }
        }

        private static void EnableDoubleBuffering(Control control)
        {
            if (control == null) return;

            typeof(Control)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }

        /// <summary>
        /// Pinta el borde de 1px y el resplandor azul sutil de la tarjeta de
        /// proyectos recientes (sin sombra real ni desenfoque).
        /// </summary>
        private static void PaintRecentCard(Control card, Graphics graphics)
        {
            var rect = card.ClientRectangle;
            if (rect.Width < 8 || rect.Height < 8) return;

            int glow = Math.Max(1, SoproUiMetrics.CardGlowPx);

            // Halo azul sutil pegado al borde interno.
            using (var glowPen = new Pen(Colors.Glow, glow) { Alignment = PenAlignment.Inset })
                graphics.DrawRectangle(glowPen, 0, 0, rect.Width - 1, rect.Height - 1);

            // Borde de la tarjeta (1px).
            using var borderPen = new Pen(Colors.SurfaceBorder, 1f) { Alignment = PenAlignment.Inset };
            graphics.DrawRectangle(borderPen, 1, 1, rect.Width - 3, rect.Height - 3);
        }

        /// <summary>
        /// Aplica un recorte redondeado al control y lo recalcula ante cambios
        /// de tamano o de DPI (AutoScaleMode.Font en 100/125/150%).
        /// </summary>
        private static void ApplyRoundedCorners(Control control, int radiusPx)
        {
            if (control == null) return;

            void RefreshRegion()
            {
                if (control.IsDisposed || control.Width <= 0 || control.Height <= 0) return;

                int radius = ScaleRadius(control, radiusPx);
                using var path = CreateRoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius);
                var previous = control.Region;
                control.Region = new Region(path);
                previous?.Dispose();
            }

            control.SizeChanged += (_, __) => RefreshRegion();
            control.HandleCreated += (_, __) => RefreshRegion();
            control.DpiChangedAfterParent += (_, __) => RefreshRegion();
            RefreshRegion();
        }

        private static int ScaleRadius(Control control, int radiusPx)
        {
            int dpi = control.DeviceDpi > 0 ? control.DeviceDpi : 96;
            float scale = dpi / 96f;
            return Math.Max(2, (int)Math.Round(radiusPx * scale));
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = Math.Max(1, radius * 2);
            diameter = Math.Min(diameter, Math.Min(bounds.Width, bounds.Height));

            var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Renderer minimo que tiñe la StatusStrip con los tokens del tema y le
        /// dibuja el borde superior de 1px. No afecta a otros ToolStrip.
        /// </summary>
        private sealed class SoproToolStripRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using var brush = new SolidBrush(Colors.HeaderBg);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using var pen = new Pen(Colors.SurfaceBorder);
                e.Graphics.DrawLine(pen, 0, 0, e.ToolStrip.Width, 0);
            }
        }
    }
}
