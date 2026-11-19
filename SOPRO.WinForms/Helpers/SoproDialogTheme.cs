using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Cromo compartido para los dialogos de SOPRO (avisos, gestores y
    /// confirmaciones): banda de encabezado oscura con titulo y subtitulo,
    /// banda de pie con botones alineados a la derecha y botones SoproButton
    /// con la paleta del tema. Solo compone tokens de SoproTheme y
    /// SoproUiMetrics; no agrega dependencias ni logica de negocio.
    ///
    /// Modelo de layout: los formularios se componen con contenedores
    /// estandar (TableLayoutPanel, Dock, AutoSize) que miden el texto con la
    /// fuente real en el DPI del monitor. Sin lineas base de escala ni
    /// multiplicadores: lo medido es lo que se renderiza.
    /// </summary>
    internal static class SoproDialogTheme
    {
        // --- Metricas del cromo de dialogos (px, medidos con la fuente real) ---

        internal const int AltoEncabezado = 64;
        internal const int AltoPie = 58;
        internal const int MargenHorizontalContenido = 24;
        internal const int MargenDerechoPie = 16;
        internal const int SeparacionBotones = 8;
        internal const int MargenLateralItemLista = 8;
        internal const int AltoMinimoItemLista = 24;
        internal const int MargenVerticalItemLista = 6;
        internal const int MargenVerticalEncabezado = 5;
        internal const int SeparacionVerticalEncabezado = 2;

        // --- Cromo del formulario ---

        /// <summary>
        /// Aplica el marco de dialogo SOPRO: FixedDialog sin minimizar/maximizar,
        /// fuera de la barra de tareas, escala por DPI del monitor, fondo del
        /// centro del tema e icono SOPRO en la barra de titulo. No altera la
        /// logica ni los eventos del formulario.
        /// </summary>
        internal static void AplicarCromoDialogo(Form form, Size clientSize, bool centroPantalla)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.StartPosition = centroPantalla
                ? FormStartPosition.CenterScreen
                : FormStartPosition.CenterParent;
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.BackColor = SoproTheme.Colors.CenterBg;
            form.ClientSize = clientSize;
            AplicarIconoSOPRO(form);
        }

        // --- Icono de la barra de titulo ---

        private static Icon? _iconoSopro;
        private static bool _falloIconoSopro;

        /// <summary>
        /// Aplica el icono SOPRO ya incrustado en los recursos del ensamblado
        /// (FormPrincipal.resx, "$this.Icon") a la barra de titulo nativa del
        /// dialogo. No lee SOPRO.ico del disco (asset local no versionado): en
        /// CI el recurso sigue existiendo y, si faltara, el dialogo conserva el
        /// icono generico sin fallar. El Icon se carga UNA vez y se comparte
        /// entre formularios: Form no dispone el Icon asignado, por lo que la
        /// instancia vive todo el proceso sin fugas ni handles duplicados.
        /// </summary>
        internal static void AplicarIconoSOPRO(Form form)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));
            if (_falloIconoSopro) return;

            if (_iconoSopro == null)
            {
                try
                {
                    if (new ComponentResourceManager(typeof(FormPrincipal)).GetObject("$this.Icon") is Icon icono)
                        _iconoSopro = icono;
                    else
                        _falloIconoSopro = true;
                }
                catch
                {
                    _falloIconoSopro = true;
                }
            }

            if (_iconoSopro != null && !form.IsDisposed)
                form.Icon = _iconoSopro;
        }

        // --- Encabezado / pie / tarjetas ---

        /// <summary>
        /// Banda de encabezado oscura con titulo y subtitulo opcional, con la
        /// linea inferior de 1px del cromo de la aplicacion. Composicion con
        /// TableLayoutPanel y filas AutoSize: cada rotulo mide su texto con la
        /// fuente real del monitor, en filas propias (sin solapes) y la banda
        /// crece hasta contenerlos (minimo AltoEncabezado). Sin recortes ni
        /// colapsos a un caracter: el ancho de cada fila es el del texto.
        /// </summary>
        internal static TableLayoutPanel CrearEncabezado(string titulo, string? subtitulo)
        {
            var tabla = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = SoproTheme.Colors.HeaderDark,
                ColumnCount = 1,
                RowCount = string.IsNullOrWhiteSpace(subtitulo) ? 1 : 2,
                Padding = new Padding(20, MargenVerticalEncabezado, 20, MargenVerticalEncabezado),
                Margin = new Padding(0)
            };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int fila = 0; fila < tabla.RowCount; fila++)
                tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.Paint += (_, e) =>
            {
                var rect = tabla.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using var pen = new Pen(SoproTheme.Colors.HeaderBottom);
                e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
            };

            var lblTitulo = new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = SoproTheme.Colors.Surface,
                Font = SoproTheme.Fonts.DialogTitle,
                Text = titulo,
                Margin = new Padding(0, 0, 0, SeparacionVerticalEncabezado)
            };
            tabla.Controls.Add(lblTitulo, 0, 0);

            if (!string.IsNullOrWhiteSpace(subtitulo))
            {
                var lblSubtitulo = new Label
                {
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    ForeColor = SoproTheme.Colors.SubtitleText,
                    Font = SoproTheme.Fonts.Subtitle,
                    Text = subtitulo,
                    Margin = new Padding(0)
                };
                tabla.Controls.Add(lblSubtitulo, 0, 1);
            }

            return tabla;
        }

        /// <summary>Banda de pie con la linea superior de 1px del tema.</summary>
        internal static Panel CrearPie()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = AltoPie,
                BackColor = SoproTheme.Colors.HeaderBg
            };
            panel.Paint += (_, e) =>
            {
                var rect = panel.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using (var brush = new SolidBrush(SoproTheme.Colors.HeaderBg))
                    e.Graphics.FillRectangle(brush, rect);

                using var pen = new Pen(SoproTheme.Colors.SurfaceBorder);
                e.Graphics.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Top);
            };
            return panel;
        }

        /// <summary>
        /// Tarjeta de contenido: superficie blanca con borde de 1px del tema.
        /// El padding de 1px deja visible el borde para el contenido acoplado.
        /// </summary>
        internal static Panel CrearTarjeta()
        {
            var tarjeta = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SoproTheme.Colors.Surface,
                Padding = new Padding(1)
            };
            tarjeta.Paint += (_, e) =>
            {
                var rect = tarjeta.ClientRectangle;
                if (rect.Width < 2 || rect.Height < 2) return;

                using var pen = new Pen(SoproTheme.Colors.SurfaceBorder, 1f) { Alignment = PenAlignment.Inset };
                e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
            };
            return tarjeta;
        }

        // --- Botones ---

        internal static SoproButton CrearBotonPrimario(string texto, SoproIconType? icono = null)
            => CrearBoton(texto, icono, primario: true);

        internal static SoproButton CrearBotonSecundario(string texto, SoproIconType? icono = null)
            => CrearBoton(texto, icono, primario: false);

        private static SoproButton CrearBoton(string texto, SoproIconType? icono, bool primario)
        {
            var button = new SoproButton
            {
                Text = texto,
                AutoSize = false,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                // Icono y texto centrados como UN bloque con pintado propio: el
                // ensamblado nativo Inline reparte icono/texto por separado y
                // en textos largos recorta el rotulo y desalinea el icono.
                SoproLayout = SoproButtonLayout.Small,
                Font = SoproTheme.Fonts.Button,
                SoproContentPadding = new Padding(16, 0, 16, 0),
                SoproIconTextSpacing = 8,
                SoproIconSize = 16,
                SoproIcon = icono,
                SoproFixedHeight = 34,
                SoproMinimumAutoWidth = 110
            };

            if (primario)
            {
                button.ForeColor = SoproTheme.Colors.Surface;
                button.BackColor = SoproTheme.Colors.RibbonActive;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = SoproTheme.Colors.Brand;
                button.FlatAppearance.MouseDownBackColor = SoproTheme.Colors.RibbonDown;
            }
            else
            {
                button.ForeColor = SoproTheme.Colors.Ink2;
                button.BackColor = SoproTheme.Colors.Surface;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = SoproTheme.Colors.SurfaceBorder;
                button.FlatAppearance.MouseOverBackColor = SoproTheme.Colors.HoverBg;
                button.FlatAppearance.MouseDownBackColor = SoproTheme.Colors.SelBg;
            }

            AjustarAnchoContenido(button);
            SoproTheme.ApplyRoundedCorners(button, SoproUiMetrics.ThemeRadiusPx);
            return button;
        }

        /// <summary>
        /// Fija el ancho del boton al contenido (icono + espaciado + texto +
        /// padding horizontal), con el mismo calculo que SoproButton aplica en
        /// Inline. Necesario porque el layout Small no recalcula el ancho
        /// automaticamente. El texto de los dialogos no cambia en ejecucion.
        /// </summary>
        internal static void AjustarAnchoContenido(SoproButton button)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));

            string texto = button.Text ?? string.Empty;
            int anchoTexto = string.IsNullOrWhiteSpace(texto)
                ? 0
                : TextRenderer.MeasureText(texto, button.Font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;

            int anchoIcono = button.SoproIcon.HasValue ? Math.Max(12, button.SoproIconSize) : 0;
            int espaciado = anchoIcono > 0 && anchoTexto > 0 ? button.SoproIconTextSpacing : 0;

            int ancho = button.SoproContentPadding.Left + anchoIcono + espaciado + anchoTexto + button.SoproContentPadding.Right;
            button.Width = Math.Max(button.SoproMinimumAutoWidth, ancho);
            button.Height = button.SoproFixedHeight;
        }

        // --- Layout determinista (funciones puras, testeables) ---

        /// <summary>
        /// X del boton en el indice indicado alineando el grupo a la derecha
        /// del pie. El indice 0 es el boton mas a la derecha (accion primaria).
        /// Funcion PURA: no toca la UI.
        /// </summary>
        internal static int CalcularXBotonPie(int anchoPie, int[] anchos, int indice)
        {
            if (anchos == null) throw new ArgumentNullException(nameof(anchos));
            if (indice < 0 || indice >= anchos.Length) throw new ArgumentOutOfRangeException(nameof(indice));

            int x = anchoPie - MargenDerechoPie;
            for (int i = 0; i <= indice; i++)
                x -= anchos[i];
            x -= SeparacionBotones * indice;
            return x;
        }

        /// <summary>Y centrado del boton dentro de la banda de pie. Funcion PURA.</summary>
        internal static int CalcularYBotonPie(int altoPie, int altoBoton)
            => Math.Max(0, (altoPie - altoBoton) / 2);

        /// <summary>
        /// Alinea los botones a la derecha del pie (indice 0 = mas a la
        /// derecha) y los centra verticalmente.
        /// </summary>
        internal static void AlinearBotonesPie(Control pie, params Control[] botones)
        {
            if (pie == null) throw new ArgumentNullException(nameof(pie));
            if (botones == null) throw new ArgumentNullException(nameof(botones));
            if (pie.IsDisposed || botones.Length == 0) return;

            int[] anchos = botones.Select(boton => boton.Width).ToArray();
            for (int i = 0; i < botones.Length; i++)
            {
                botones[i].Location = new Point(
                    CalcularXBotonPie(pie.ClientSize.Width, anchos, i),
                    CalcularYBotonPie(pie.Height, botones[i].Height));
            }
        }

        /// <summary>
        /// Alto que necesita un texto multilínea (WordBreak) envuelto en el
        /// ancho disponible, con la fuente real del monitor. Funcion PURA.
        /// </summary>
        internal static int CalcularAltoEtiqueta(string texto, Font fuente, int anchoDisponible)
        {
            if (string.IsNullOrEmpty(texto) || fuente == null || anchoDisponible <= 0) return 0;

            var limite = new Size(anchoDisponible, int.MaxValue);
            return TextRenderer.MeasureText(texto, fuente, limite,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        }

        /// <summary>Alto seguro del item de una lista a partir del texto medido. Funcion PURA.</summary>
        internal static int CalcularAltoItemLista(int altoTextoMedido)
            => Math.Max(AltoMinimoItemLista, altoTextoMedido + MargenVerticalItemLista);

        /// <summary>
        /// Alto seguro del item de una lista: mide "Mg" (mayuscula +
        /// descendente) con la fuente real del control en el DPI actual, de
        /// modo que el texto de la fila nunca se recorte verticalmente.
        /// </summary>
        internal static int CalcularAltoItemLista(Control control)
        {
            if (control == null || control.Font == null) return AltoMinimoItemLista;

            const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            var limite = new Size(int.MaxValue, int.MaxValue);

            Graphics? dc = null;
            try
            {
                dc = control.CreateGraphics();
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
                    ? TextRenderer.MeasureText(dc, "Mg", control.Font, limite, flags).Height
                    : TextRenderer.MeasureText("Mg", control.Font, limite, flags).Height;

                return CalcularAltoItemLista(alto);
            }
            finally
            {
                dc?.Dispose();
            }
        }
    }
}
