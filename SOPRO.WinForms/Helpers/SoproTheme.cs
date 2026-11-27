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
            public static readonly Color RowAlt = Color.FromArgb(0xF8, 0xFA, 0xFC);      // #f8fafc slate-50
            public static readonly Color GridLine = Color.FromArgb(0xE2, 0xE8, 0xF0);    // #e2e8f0 slate-200
            public static readonly Color SelBg = Color.FromArgb(0xDB, 0xEA, 0xFE);       // #dbeafe (unico tinte azul)

            // --- Chrome del host (header, sidebar y ribbon) ---
            public static readonly Color SidebarBg = Color.FromArgb(0xFA, 0xFA, 0xFA);       // 250,250,250
            public static readonly Color SidebarHeaderBg = Color.FromArgb(0xF0, 0xF0, 0xF3); // 240,240,243
            public static readonly Color RibbonSeparator = Color.FromArgb(0xC8, 0xC8, 0xC8); // 200,200,200
            public static readonly Color Disabled = Color.FromArgb(0x78, 0x78, 0x78);        // 120,120,120

            // --- Tema "B - Limpio neutro" del chrome del shell ---
            // Superficies planas neutras para el header, el ribbon y la barra
            // lateral, compartidas por FormPrincipal y FormProyecto.
            public static readonly Color HeaderDark = Color.FromArgb(0x1E, 0x29, 0x3B);        // #1e293b slate-800
            public static readonly Color HeaderBottom = Color.FromArgb(0x0F, 0x17, 0x2A);      // #0f172a slate-900
            public static readonly Color RibbonText = Color.FromArgb(0xE2, 0xE8, 0xF0);        // #e2e8f0 slate-200
            public static readonly Color RibbonHover = Color.FromArgb(0x33, 0x41, 0x55);       // #334155 slate-700
            public static readonly Color RibbonActive = Color.FromArgb(0x1D, 0x4E, 0xD8);      // #1d4ed8 marca
            public static readonly Color RibbonDown = Color.FromArgb(0x0F, 0x17, 0x2A);        // #0f172a slate-900
            public static readonly Color RibbonDisabledIcon = Color.FromArgb(0x94, 0xA3, 0xB8); // #94a3b8 slate-400
            public static readonly Color RibbonSeparatorDark = Color.FromArgb(0x47, 0x55, 0x69); // #475569 slate-600
            public static readonly Color SidebarBg2 = Color.FromArgb(0xF1, 0xF5, 0xF9);        // #f1f5f9 slate-100
            public static readonly Color SidebarHeaderBg2 = Color.FromArgb(0xE2, 0xE8, 0xF0);  // #e2e8f0 slate-200
            public static readonly Color SidebarHairline = Color.FromArgb(0xCB, 0xD5, 0xE1);   // #cbd5e1 slate-300
            public static readonly Color SidebarText = Color.FromArgb(0x33, 0x41, 0x55);        // #334155 slate-700
            public static readonly Color SidebarTextGroup = Color.FromArgb(0x0F, 0x17, 0x2A);   // #0f172a slate-900
            public static readonly Color SidebarActive = Color.FromArgb(0xDB, 0xEA, 0xFE);      // #dbeafe (unico tinte azul)

            // --- Derivados usados por el encabezado y las tarjetas de FormPrincipal ---
            public static readonly Color SubtitleText = Color.FromArgb(0xCB, 0xD5, 0xE1); // #cbd5e1 slate-300
            public static readonly Color HeaderBg = Color.FromArgb(0xF1, 0xF5, 0xF9);     // #f1f5f9 slate-100
            public static readonly Color CenterBg = Color.FromArgb(0xF8, 0xFA, 0xFC);     // #f8fafc slate-50 (fondo del centro)
            public static readonly Color SecondaryBg = Color.FromArgb(0xF1, 0xF5, 0xF9);  // #f1f5f9 slate-100
            public static readonly Color SecondaryHoverBg = Color.FromArgb(0xE2, 0xE8, 0xF0); // #e2e8f0 slate-200
            public static readonly Color GlyphIdle = Color.FromArgb(0x8A, 0x94, 0xA6);    // gris icono papelera
            public static readonly Color HoverBg = Color.FromArgb(0xEA, 0xF2, 0xFF);      // hover suave en rejilla
            public static readonly Color Glow = Color.FromArgb(0x18, 0x0F, 0x17, 0x2A);   // #0f172a @ 9% sombra neutra sutil
        }

        /// <summary>
        /// Tokens del ribbon claro de FormProyecto (issue #180): banda de comandos
        /// sobre el encabezado claro unificado, con iconografía azul, texto en
        /// tinta y estados suaves. Es un CONTEXTO específico del ribbon: no
        /// reutilizar en otras superficies (los tokens Ribbon* históricos siguen
        /// sirviendo al chrome oscuro, FormPrincipal y diálogos). La mayoría de
        /// los valores derivan de la paleta existente; la excepción documentada
        /// es <see cref="ActivoPresionado"/>, el paso azul-900 de la misma
        /// familia de marca para distinguir presionado de hover.
        /// </summary>
        public static class RibbonClaro
        {
            /// <summary>Superficie de la banda de comandos (tarjeta clara).</summary>
            public static readonly Color Superficie = Colors.Surface;

            /// <summary>Texto neutro (toggles tipográficos, indicador con destino activo).</summary>
            public static readonly Color Texto = Colors.Ink2;

            /// <summary>Caption de grupo e indicador sin destino: apagado pero legible.</summary>
            public static readonly Color Caption = Colors.Muted;

            /// <summary>Acento de marca: iconografía y comandos del ribbon.</summary>
            public static readonly Color Acento = Colors.BrandMd;

            /// <summary>Acento profundo: hover del comando activo (un paso bajo el azul de marca).</summary>
            public static readonly Color AcentoProfundo = Colors.Brand;

            /// <summary>Fondo del comando activo (azul de marca, texto claro).</summary>
            public static readonly Color Activo = Colors.RibbonActive;

            /// <summary>
            /// Presionado del comando ACTIVO: el paso azul-900 de la familia de
            /// marca (#1e3a8a). Es el único valor no tomado tal cual de la paleta
            /// global; distingue presionado de hover (que usa
            /// <see cref="AcentoProfundo"/>) y profundiza la jerarquía
            /// reposo→hover→presionado con el texto blanco cada vez más legible.
            /// </summary>
            public static readonly Color ActivoPresionado = Color.FromArgb(0x1E, 0x3A, 0x8A);

            /// <summary>Hover suave sobre la superficie clara.</summary>
            public static readonly Color Hover = Colors.HoverBg;

            /// <summary>Presionado sobre la superficie clara (un paso más profundo que el hover).</summary>
            public static readonly Color Presionado = Colors.SelBg;

            /// <summary>Separador discreto entre grupos y borde del pozo de color.</summary>
            public static readonly Color Separador = Colors.SurfaceBorder;

            /// <summary>Pozo de muestra de color: ficha en reposo sobre la MISMA
            /// superficie clara de la banda, sin marco exterior, con
            /// hover/presionado en tinte azul progresivo y glifo en tinta
            /// (contrato #180: nunca usar aqui el hover historico oscuro).</summary>
            public static readonly Color Chip = Superficie;

            /// <summary>Estado deshabilitado sobre la superficie clara (≥3:1 no textual).</summary>
            public static readonly Color Deshabilitado = Colors.Muted;
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

            /// <summary>
            /// Rotulo de grupo del ribbon (pie de cada bloque). Tono apagado,
            /// al pie de la banda de comandos.
            /// </summary>
            public static readonly Font RibbonGroupCaption = new Font(FontFamily, 8.5F, FontStyle.Regular, GraphicsUnit.Point);

            /// <summary>Titulo de la banda de encabezado de un dialogo SOPRO.</summary>
            public static readonly Font DialogTitle = new Font(FontFamily, 16F, FontStyle.Bold, GraphicsUnit.Point);

            /// <summary>Cuerpo de mensaje de un dialogo SOPRO.</summary>
            public static readonly Font DialogBody = new Font(FontFamily, 10.5F, FontStyle.Regular, GraphicsUnit.Point);

            /// <summary>
            /// Rotulo de sección del sidebar (issue #193): rótulo compacto en
            /// mayúsculas por encima de las hojas de su grupo.
            /// </summary>
            public static readonly Font SidebarSeccion = new Font(FontFamily, 8.5F, FontStyle.Bold, GraphicsUnit.Point);
        }

        // =====================================================================
        // Aplicacion del tema a FormPrincipal
        // =====================================================================

        /// <summary>
        /// Engancha toda la pintura y estilos del tema SOPRO sobre
        /// <see cref="FormPrincipal"/>: encabezado solido neutro, botones de
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
            var btnApoyar = form.btnApoyarKoFi;

            EnableDoubleBuffering(panelTop);
            // Tema "B - Limpio neutro": header solido del mismo tono que el
            // chrome de FormProyecto (un solo look en toda la app).
            panelTop.BackColor = Colors.HeaderDark;
            panelTop.Height = SoproUiMetrics.HeaderHeight;
            // Solo esquinas inferiores redondeadas: arriba queda al ras de la
            // ventana y el separador inferior sigue el recorte de la Región.
            ApplyBottomRoundedCorners(panelTop, SoproUiMetrics.SurfaceRadiusPx);

            panelTop.Paint += (_, e) =>
            {
                var rect = panelTop.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                // Sin gradiente ni angulo: relleno solido del header.
                using (var brush = new SolidBrush(Colors.HeaderDark))
                    e.Graphics.FillRectangle(brush, rect);

                // Linea inferior de 1px bajo el header.
                using var pen = new Pen(Colors.HeaderBottom);
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
            ConfigureHeaderButton(btnApoyar, "Apoyar", SoproIconType.Corazon, primary: false);

            bool layoutInProgress = false;

            void LayoutButtons()
            {
                if (panelTop.IsDisposed) return;
                if (btnNuevo == null || btnAbrir == null || btnApoyar == null) return;
                if (layoutInProgress) return;

                layoutInProgress = true;
                try
                {
                    const int gap = 8;
                    const int rightPadding = 24;
                    const int safeMargin = 20;
                    const int rowGap = 6;

                    int rowHeight = Math.Max(btnNuevo.Height, Math.Max(btnAbrir.Height, btnApoyar.Height));

                    // Orden visual preferido (izquierda -> derecha): Apoyar -> Nuevo -> Abrir.
                    int abrirX = panelTop.ClientSize.Width - rightPadding - btnAbrir.Width;
                    int nuevoX = abrirX - gap - btnNuevo.Width;
                    int apoyarX = nuevoX - gap - btnApoyar.Width;

                    int textRightEdge = Math.Max(lblTitle.Right, lblSubtitle.Right) + safeMargin;

                    bool singleRowFits = apoyarX >= textRightEdge;
                    bool primaryRowFits = nuevoX >= textRightEdge;

                    int desiredHeight;
                    int yTop;
                    int yBottom;

                    if (singleRowFits)
                    {
                        desiredHeight = SoproUiMetrics.HeaderHeight;
                        yTop = Math.Max(0, (desiredHeight - rowHeight) / 2);
                        yBottom = yTop;
                    }
                    else
                    {
                        // Fallback: header crece hacia abajo. Dos filas si caben Nuevo/Abrir;
                        // tres filas verticales alineadas a la derecha si el ancho es muy estrecho.
                        int rows = primaryRowFits ? 2 : 3;
                        int totalContentHeight = rows * rowHeight + (rows - 1) * rowGap;
                        int topMargin = Math.Max(0, (SoproUiMetrics.HeaderHeight - rowHeight) / 2);
                        desiredHeight = totalContentHeight + 2 * topMargin;
                        yTop = topMargin;
                        yBottom = yTop + (rows - 1) * (rowHeight + rowGap);
                    }

                    if (panelTop.Height != desiredHeight)
                        panelTop.Height = desiredHeight;

                    if (singleRowFits)
                    {
                        btnApoyar.Location = new Point(apoyarX, yTop);
                        btnNuevo.Location = new Point(nuevoX, yTop);
                        btnAbrir.Location = new Point(abrirX, yTop);
                    }
                    else if (primaryRowFits)
                    {
                        // Nuevo/Abrir arriba; Apoyar debajo de Abrir.
                        btnNuevo.Location = new Point(nuevoX, yTop);
                        btnAbrir.Location = new Point(abrirX, yTop);
                        btnApoyar.Location = new Point(abrirX, yBottom);
                    }
                    else
                    {
                        // Fallback extremo: columna vertical Apoyar -> Nuevo -> Abrir,
                        // alineados a la derecha para no solapar el título/subtítulo.
                        int yRow2 = yTop + rowHeight + rowGap;
                        btnApoyar.Location = new Point(panelTop.ClientSize.Width - rightPadding - btnApoyar.Width, yTop);
                        btnNuevo.Location = new Point(panelTop.ClientSize.Width - rightPadding - btnNuevo.Width, yRow2);
                        btnAbrir.Location = new Point(abrirX, yBottom);
                    }
                }
                finally
                {
                    layoutInProgress = false;
                }
            }

            panelTop.SizeChanged += (_, __) => LayoutButtons();
            btnNuevo.SizeChanged += (_, __) => LayoutButtons();
            btnAbrir.SizeChanged += (_, __) => LayoutButtons();
            btnApoyar.SizeChanged += (_, __) => LayoutButtons();
            lblTitle.SizeChanged += (_, __) => LayoutButtons();
            lblSubtitle.SizeChanged += (_, __) => LayoutButtons();
            LayoutButtons();
        }

        private static void ConfigureHeaderButton(SoproButton button, string text, SoproIconType? icon, bool primary)
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
                // PRIMARIO: acento de marca (unico azul de accion) sobre el header oscuro.
                button.ForeColor = Colors.Surface;
                button.BackColor = Colors.RibbonActive;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Colors.Brand;
                button.FlatAppearance.MouseDownBackColor = Colors.RibbonDown;
            }
            else
            {
                // SECUNDARIO: superficie neutra oscura con hairline slate.
                button.ForeColor = Colors.RibbonText;
                button.BackColor = Colors.RibbonHover;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Colors.RibbonSeparatorDark;
                button.FlatAppearance.MouseOverBackColor = Colors.RibbonSeparatorDark;
                button.FlatAppearance.MouseDownBackColor = Colors.RibbonDown;

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
            // Tema "B - Limpio neutro": fondo solido slate-50 (sin gradiente azul).
            panelCenter.BackColor = Colors.CenterBg;

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

            StyleActionColumns(dgv);
        }

        private static readonly string[] NombresColumnasAccion = { "colDuplicar", "colEliminar" };

        private static bool EsColumnaAccion(string nombreColumna)
            => Array.IndexOf(NombresColumnasAccion, nombreColumna) >= 0;

        private static bool EsColumnaAccion(DataGridView dgv, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= dgv.Columns.Count) return false;
            return EsColumnaAccion(dgv.Columns[columnIndex].Name);
        }

        private static SoproIconType IconoDeColumnaAccion(string nombreColumna)
            => nombreColumna == "colDuplicar" ? SoproIconType.Copiar : SoproIconType.Papelera;

        private static void StyleActionColumns(DataGridView dgv)
        {
            // Las columnas de accion se recrean en cada LoadRecentProjects,
            // por eso la configuracion se reaplica al agregarse la columna.
            dgv.ColumnAdded += (_, e) =>
            {
                if (e.Column == null) return;
                if (!EsColumnaAccion(e.Column.Name)) return;
                ConfigureActionColumn(e.Column);
            };

            foreach (var nombre in NombresColumnasAccion)
                ConfigureActionColumn(dgv.Columns[nombre]);

            int hoverRow = -1;
            int hoverColumn = -1;

            dgv.CellMouseEnter += (_, e) =>
            {
                if (e.RowIndex < 0 || !EsColumnaAccion(dgv, e.ColumnIndex)) return;
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
                if (!EsColumnaAccion(dgv, e.ColumnIndex)) return;

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

                using (var glyph = SoproIconProvider.GetIcon(IconoDeColumnaAccion(dgv.Columns[e.ColumnIndex].Name), glyphColor, glyphSize))
                    graphics.DrawImage(glyph, new Rectangle(glyphX, glyphY, glyphSize, glyphSize));

                e.Handled = true;
            };
        }

        private static void ConfigureActionColumn(DataGridViewColumn? column)
        {
            if (column is not DataGridViewButtonColumn buttonColumn) return;

            // El glifo (Copiar/Papelera) se pinta a mano para poder colorearlo
            // (gris en reposo, azul en hover). El Text con la etiqueta en
            // espanol permanece invisible y queda disponible para lectores
            // de pantalla si el pintado personalizado no llega a ejecutarse.
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
        /// banda superior clara unificada (bloque de marca + comandos sobre la
        /// misma superficie, con hairline inferior), barra lateral y ribbon.
        /// No modifica logica, eventos ni manejadores del formulario.
        /// </summary>
        public static void ApplyFormProyecto(FormProyecto form)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            ApplyProyectoHeader(form);
            ApplyProyectoSidebar(form);
            ApplyProyectoRibbonChrome(form);
            ApplyProyectoRibbonSelectors(form);
        }

        /// <summary>
        /// Selectores del ribbon (tipo de fuente y tamano). El ComboBox dibuja
        /// el elemento seleccionado con los colores del sistema (ignora su
        /// BackColor), asi que se pinta en modo owner-draw para que respete la
        /// superficie clara del ribbon.
        /// </summary>
        private static void ApplyProyectoRibbonSelectors(FormProyecto form)
        {
            var combo = form.cboFuente;
            if (combo != null)
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = RibbonClaro.Superficie;
                combo.ForeColor = RibbonClaro.Texto;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.ItemHeight = CalcularItemHeightComboRibbon(combo);
                combo.DrawItem -= DrawRibbonComboItem;
                combo.DrawItem += DrawRibbonComboItem;

                // El alto del item depende de la fuente y del DPI: a 200 % un
                // alto fijo (20) recortaba los nombres de fuente. Se recalcula
                // en ambos cambios. El -= previo al += evita suscripciones
                // duplicadas si el tema se aplica mas de una vez al selector.
                combo.FontChanged -= RibbonCombo_AplicarItemHeight;
                combo.FontChanged += RibbonCombo_AplicarItemHeight;
                combo.DpiChangedAfterParent -= RibbonCombo_AplicarItemHeight;
                combo.DpiChangedAfterParent += RibbonCombo_AplicarItemHeight;
            }

            var numerico = form.nudTamano;
            if (numerico != null)
            {
                numerico.BackColor = RibbonClaro.Superficie;
                numerico.ForeColor = RibbonClaro.Texto;

                // Los botones +/- los pinta el sistema: se tiñen con la
                // superficie del selector para que no rompan la banda clara.
                if (numerico.Controls.Count > 0)
                    numerico.Controls[0].BackColor = RibbonClaro.Superficie;
            }
        }

        /// <summary>
        /// Alto minimo del item del selector de fuentes del ribbon (px a 96 DPI).
        /// Es el valor historico: por debajo el texto se rozaba.
        /// </summary>
        internal const int ItemHeightMinimoComboRibbon = 20;

        /// <summary>
        /// Aire vertical del item owner-draw (px): texto medido + este margen.
        /// </summary>
        internal const int MargenVerticalItemComboRibbon = 5;

        /// <summary>
        /// Alto seguro del item owner-draw a partir del alto ya medido del
        /// texto de la fuente. Es una funcion PURA (no toca la UI), pensada para
        /// poder testearla sin levantar el ComboBox: nunca baja del minimo
        /// historico y crece con la fuente.
        /// </summary>
        internal static int CalcularItemHeightComboRibbon(int altoTextoMedido)
            => Math.Max(ItemHeightMinimoComboRibbon, altoTextoMedido + MargenVerticalItemComboRibbon);

        /// <summary>
        /// Alto seguro del item owner-draw del selector de fuentes. Mide "Mg"
        /// (mayuscula + descendente: el caso mas alto) con la fuente real.
        /// Si el control ya tiene handle, la medicion se hace con su DC para
        /// respetar el DPI real del monitor; si no, se usa la medicion global.
        /// </summary>
        internal static int CalcularItemHeightComboRibbon(ComboBox? combo)
        {
            if (combo == null || combo.Font == null) return ItemHeightMinimoComboRibbon;

            const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            var limite = new Size(int.MaxValue, int.MaxValue);

            Graphics? dc = null;
            try
            {
                dc = combo.CreateGraphics();
            }
            catch (ObjectDisposedException)
            {
                dc = null;
            }
            catch (InvalidOperationException)
            {
                dc = null;
            }

            try
            {
                int alto = dc != null
                    ? TextRenderer.MeasureText(dc, "Mg", combo.Font, limite, flags).Height
                    : TextRenderer.MeasureText("Mg", combo.Font, limite, flags).Height;

                return CalcularItemHeightComboRibbon(alto);
            }
            finally
            {
                dc?.Dispose();
            }
        }

        /// <summary>
        /// Recalcula el alto del item owner-draw del selector de fuentes.
        /// Handler ESTATICO a proposito: asi el -= antes del += elimina la
        /// suscripcion anterior en vez de acumular duplicados.
        /// </summary>
        private static void RibbonCombo_AplicarItemHeight(object? sender, EventArgs e)
        {
            if (sender is ComboBox combo)
                combo.ItemHeight = CalcularItemHeightComboRibbon(combo);
        }

        private static void DrawRibbonComboItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox combo || e.Graphics == null) return;

            // La fila resaltada del desplegable usa el tinte azul suave del tema;
            // la cara cerrada (e.Index == -1) se dibuja con el fondo del control
            // para no verse como una selección permanente. Deshabilitado se
            // distingue con el token apagado del ribbon claro.
            bool seleccionado = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                && e.Index >= 0;
            bool deshabilitado = (e.State & DrawItemState.Disabled) != 0;

            Color fondo = seleccionado ? RibbonClaro.Presionado : combo.BackColor;
            Color texto = deshabilitado
                ? RibbonClaro.Deshabilitado
                : seleccionado ? RibbonClaro.Texto : combo.ForeColor;

            using (var brush = new SolidBrush(fondo))
                e.Graphics.FillRectangle(brush, e.Bounds);

            string contenido = string.Empty;
            if (e.Index >= 0 && e.Index < combo.Items.Count)
                contenido = combo.Items[e.Index]?.ToString() ?? string.Empty;
            else if (e.Index == -1 && combo.SelectedItem != null)
                contenido = combo.SelectedItem.ToString() ?? string.Empty;

            if (contenido.Length == 0) return;

            TextRenderer.DrawText(
                e.Graphics,
                contenido,
                combo.Font,
                e.Bounds,
                texto,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        private static void ApplyProyectoHeader(FormProyecto form)
        {
            var panelTop = form.panelTop;
            var lblTitulo = form.lblTitulo;

            EnableDoubleBuffering(panelTop);
            // Banda superior clara unificada (mockup #180): la marca y el ribbon
            // comparten la MISMA superficie blanca, sin tarjeta incrustada ni
            // header oscuro detras. El cierre inferior es un hairline neutro.
            panelTop.BackColor = Colors.Surface;

            panelTop.Paint += (_, e) =>
            {
                var rect = panelTop.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                // Sin gradiente ni angulo: relleno solido del header.
                using (var brush = new SolidBrush(Colors.Surface))
                    e.Graphics.FillRectangle(brush, rect);

                // Hairline de 1px bajo la banda.
                using var pen = new Pen(Colors.SidebarHairline);
                e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
            };

            // Marca en tinta sobre la banda clara (misma jerarquia que el mockup).
            lblTitulo.BackColor = Color.Transparent;
            lblTitulo.ForeColor = Colors.Ink2;
        }

        private static void ApplyProyectoSidebar(FormProyecto form)
        {
            var panelLeft = form.panelLeft;
            var treeMenu = form.treeMenu;

            // Hairline derecha de 1px (padding) en lugar del borde del sistema.
            panelLeft.BorderStyle = BorderStyle.None;
            panelLeft.Padding = new Padding(0, 0, 1, 0);
            panelLeft.BackColor = SidebarPaleta.Fondo;
            panelLeft.Paint -= PanelLeft_Paint;
            panelLeft.Paint += PanelLeft_Paint;
            EnableDoubleBuffering(panelLeft);

            // Header integrado en la MISMA superficie fría del lateral.
            form.panelSidebarHeader.BackColor = SidebarPaleta.Fondo;

            var btnToggle = form.btnToggleSidebar;
            btnToggle.FlatStyle = FlatStyle.Flat;
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.BackColor = SidebarPaleta.Fondo;
            btnToggle.ForeColor = SidebarPaleta.TextoSeccion;
            btnToggle.FlatAppearance.MouseOverBackColor = SidebarPaleta.FondoHover;
            btnToggle.FlatAppearance.MouseDownBackColor = SidebarPaleta.FondoHover;

            // Owner-draw total (#193): sin líneas ni glifos nativos; el pintado
            // (secciones/píldoras/estados) vive en FormProyecto.TreeMenu.
            treeMenu.BackColor = SidebarPaleta.Fondo;
            treeMenu.ForeColor = SidebarPaleta.TextoHoja;
            treeMenu.ShowLines = false;
            treeMenu.ShowRootLines = false;
            treeMenu.ShowPlusMinus = false;
            treeMenu.FullRowSelect = true;
            treeMenu.HideSelection = false;
            treeMenu.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            EnableDoubleBuffering(treeMenu);
        }

        private static void PaintSidebarHairline(Control panel, Graphics graphics)
        {
            var rect = panel.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            int x = rect.Right - 1;
            using var pen = new Pen(Colors.SidebarHairline);
            graphics.DrawLine(pen, x, 0, x, rect.Bottom - 1);
        }

        /// <summary>
        /// Handler estable (método con nombre) para poder des-suscribirlo antes
        /// de suscribirlo: evita acumular pintados duplicados si el tema se
        /// aplica más de una vez.
        /// </summary>
        private static void PanelLeft_Paint(object? sender, PaintEventArgs e)
            => PaintSidebarHairline((Control)sender!, e.Graphics);

        private static void ApplyProyectoRibbonChrome(FormProyecto form)
        {
            // Separadores sutiles: un filete claro que separa los grupos sin
            // competir con los comandos sobre la banda clara.
            foreach (var separator in new[]
            {
                form.label1, form.label2, form.lblSepReporte, form.lblSepGlobal,
                form.lblSepAlin, form.lblSepEstilo, form.lblSepFuente
            })
            {
                if (separator != null)
                    separator.BackColor = RibbonClaro.Separador;
            }

            // Rotulos de grupo: al pie de la banda de comandos, en tono apagado
            // pero legible sobre la superficie clara.
            foreach (var caption in new[]
            {
                form.lblGroupDatos, form.lblGroupExportar, form.lblGroupFuente,
                form.lblGroupAlineacion, form.lblGroupColores, form.lblGroupCeldas
            })
            {
                if (caption == null) continue;
                caption.Font = Fonts.RibbonGroupCaption;
                caption.ForeColor = RibbonClaro.Caption;
            }

            foreach (var button in new[]
            {
                form.btnPdfRibbon, form.btnExcelRibbon, form.btnDepurarRibbon,
                form.btnRecalcularRibbon, form.btnBuscarRibbon, form.btnWrapRibbon,
                form.btnConsolidarInsumos, form.btnAplicarATodas, form.btnAlinAba,
                form.btnAlinMed, form.btnAlinJus, form.btnAlinDer, form.btnAlinCen,
                form.btnAlinIzq, form.btnColorTexto, form.btnColorFondo
            })
            {
                if (button != null)
                    button.SoproDisabledIconColor = RibbonClaro.Deshabilitado;
            }

            ApplyProyectoRibbonIndicadorDestino(form);
        }

        /// <summary>
        /// Indicador de destino del formato (columna o seleccion de celdas):
        /// contraste y tooltip.
        ///
        /// 1. FormProyecto.Ribbon.cs colorea el indicador con <see cref="Color.Gray"/>
        ///    cuando no hay destino activo. Sobre la banda clara ese gris se
        ///    queda en ~3.9:1, por debajo del 4.5:1 de WCAG AA, asi que aqui se
        ///    re-mapea al token de caption del ribbon claro (~7:1) sin tocar el
        ///    ribbon. El tono de destino activo (tinta) lo fija el propio ribbon.
        /// 2. El tooltip se sincroniza con el texto real del indicador: asi
        ///    explica SIEMPRE a que se aplica el formato (columna o celdas) en
        ///    vez de un texto generico que puede quedar desfasado.
        /// </summary>
        private static void ApplyProyectoRibbonIndicadorDestino(FormProyecto form)
        {
            var indicador = form.lblColumna;
            if (indicador == null) return;

            indicador.AutoEllipsis = true;
            indicador.ForeColorChanged += (_, __) => NormalizarColorIndicadorDestino(indicador);
            NormalizarColorIndicadorDestino(indicador);

            var toolTip = form.toolTipRibbon;
            if (toolTip == null) return;

            void SincronizarTooltipIndicador()
                => toolTip.SetToolTip(indicador, $"Destino del formato: {indicador.Text}");

            indicador.TextChanged += (_, __) => SincronizarTooltipIndicador();
            SincronizarTooltipIndicador();
        }

        /// <summary>
        /// Re-mapea el gris heredado del indicador de destino al token de
        /// caption del ribbon claro (apagado pero legible). El tono de tinta
        /// (destino activo) se respeta: ya tiene contraste.
        /// </summary>
        private static void NormalizarColorIndicadorDestino(Label indicador)
        {
            if (indicador == null) return;
            if (indicador.ForeColor == Color.Gray)
                indicador.ForeColor = RibbonClaro.Caption;
        }

        private static void EnableDoubleBuffering(Control control)
        {
            if (control == null) return;

            typeof(Control)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }

        /// <summary>
        /// Pinta el borde de 1px y una sombra neutra sutil de la tarjeta de
        /// proyectos recientes (sin sombra real ni desenfoque).
        /// </summary>
        private static void PaintRecentCard(Control card, Graphics graphics)
        {
            var rect = card.ClientRectangle;
            if (rect.Width < 8 || rect.Height < 8) return;

            int glow = Math.Max(1, SoproUiMetrics.CardGlowPx);

            // Sombra neutra sutil pegada al borde interno (antes halo azul).
            using (var glowPen = new Pen(Colors.Glow, glow) { Alignment = PenAlignment.Inset })
                graphics.DrawRectangle(glowPen, 0, 0, rect.Width - 1, rect.Height - 1);

            // Borde de la tarjeta (1px).
            using var borderPen = new Pen(Colors.SurfaceBorder, 1f) { Alignment = PenAlignment.Inset };
            graphics.DrawRectangle(borderPen, 1, 1, rect.Width - 3, rect.Height - 3);
        }

        /// <summary>
        /// Aplica un recorte redondeado al control y lo recalcula ante cambios
        /// de tamano o de DPI (AutoScaleMode.Font en 100/125/150%).
        /// Internal: los botones de los dialogos SOPRO comparten este recorte
        /// para mantener la misma esquina redondeada que los del encabezado.
        /// </summary>
        internal static void ApplyRoundedCorners(Control control, int radiusPx)
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

        /// <summary>
        /// Variante de <see cref="ApplyRoundedCorners"/> para bandas acopladas
        /// al borde superior de la ventana: redondea SOLO las dos esquinas
        /// inferiores (las superiores quedan cuadradas y al ras) y recalcula
        /// la Región ante cambios de tamaño, creación de handle y DPI,
        /// liberando siempre la Región previa que sustituye.
        /// </summary>
        internal static void ApplyBottomRoundedCorners(Control control, int radiusPx)
        {
            if (control == null) return;

            void RefreshRegion()
            {
                if (control.IsDisposed || control.Width <= 0 || control.Height <= 0) return;

                int radius = ScaleRadius(control, radiusPx);
                using var path = CreateBottomRoundedPath(new Rectangle(0, 0, control.Width, control.Height), radius);
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
        /// Rectángulo con las DOS esquinas inferiores redondeadas y las
        /// superiores cuadradas (arco inferior-izquierda: 180°→90°, arco
        /// inferior-derecha: 90°→0°, recorrido en el sentido del contorno).
        /// </summary>
        private static GraphicsPath CreateBottomRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diametro = Math.Max(1, radius * 2);
            diametro = Math.Min(diametro, Math.Min(bounds.Width, bounds.Height));
            int radio = diametro / 2;
            if (radio < 1)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int w = bounds.Width;
            int h = bounds.Height;
            path.AddLine(bounds.X, bounds.Y, bounds.X, h - radio);
            path.AddArc(new Rectangle(bounds.X, h - radio * 2, radio * 2, radio * 2), 180, -90);
            path.AddLine(bounds.X + radio, h, w - radio, h);
            path.AddArc(new Rectangle(w - radio * 2, h - radio * 2, radio * 2, radio * 2), 90, -90);
            path.AddLine(w, h - radio, bounds.Right, bounds.Y);
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
