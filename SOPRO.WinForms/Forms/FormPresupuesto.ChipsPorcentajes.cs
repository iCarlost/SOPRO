using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    public partial class FormPresupuesto
    {
        // Tokens del header oscuro (51,51,76), alineados con la paleta del panel
        // embebido de matrices (ColorTextoSobreOscuro / EstadoAdvertencia) y con
        // el acento verde histórico de esta misma etiqueta de porcentajes.
        private static readonly Color ColorChipEtiqueta = Color.FromArgb(205, 210, 224);
        private static readonly Color ColorChipValor = Color.FromArgb(180, 230, 180);
        private static readonly Color ColorChipAdvertencia = Color.FromArgb(240, 200, 130);

        /// <summary>
        /// Reconstruye la fila de chips del header a partir de la información
        /// centralizada en <see cref="BudgetPricingService.BuildHeaderInfo"/>:
        /// un chip por porcentaje activo, o un único chip de advertencia con el
        /// texto definido por Application cuando faltan porcentajes. Solo
        /// presentación: no altera valores ni eventos.
        /// </summary>
        private void ReconstruirChipsPorcentajes(BudgetHeaderInfo info)
        {
            if (panelChipsPorcentajes.IsDisposed) return;

            AsegurarAjusteAnchoProyectoPorChips();
            panelChipsPorcentajes.SuspendLayout();
            try
            {
                var previos = new Control[panelChipsPorcentajes.Controls.Count];
                panelChipsPorcentajes.Controls.CopyTo(previos, 0);
                panelChipsPorcentajes.Controls.Clear();
                foreach (var previo in previos)
                    previo.Dispose();

                if (info.TienePorcentajesActivos)
                {
                    foreach (var chip in info.PorcentajesChips)
                        panelChipsPorcentajes.Controls.Add(new ChipPorcentaje(chip.Etiqueta, chip.Valor));
                }
                else
                {
                    panelChipsPorcentajes.Controls.Add(new ChipPorcentaje(info.PorcentajesTexto));
                }
            }
            finally
            {
                panelChipsPorcentajes.ResumeLayout(performLayout: true);
                AjustarAnchoProyectoPorChips();
            }
        }

        // Separación mínima entre el nombre del proyecto y la fila de chips.
        internal const int SeparacionProyectoChipsLogica = 8;

        private bool _anchoProyectoPorChipsSuscrito;

        /// <summary>
        /// El nombre del proyecto y la fila de chips comparten la franja
        /// inferior del header: ante anchos de ventana estrechos o nombres
        /// largos, el nombre cede espacio (con elipsis) y los chips nunca se
        /// ocultan ni se envuelven. La suscripción se instala una sola vez y
        /// se libera sola con el Dispose de los controles implicados.
        /// </summary>
        private void AsegurarAjusteAnchoProyectoPorChips()
        {
            if (_anchoProyectoPorChipsSuscrito) return;
            _anchoProyectoPorChipsSuscrito = true;

            panelTop.Resize += (_, _) => AjustarAnchoProyectoPorChips();
            panelChipsPorcentajes.LocationChanged += (_, _) => AjustarAnchoProyectoPorChips();
            panelChipsPorcentajes.SizeChanged += (_, _) => AjustarAnchoProyectoPorChips();
        }

        /// <summary>
        /// Ancho determinista del nombre del proyecto: el borde derecho del
        /// nombre queda en el borde izquierdo real de la fila de chips menos
        /// un margen, recalculado tras cada layout, cambio de tamaño o DPI.
        /// </summary>
        private void AjustarAnchoProyectoPorChips()
        {
            if (panelTop.IsDisposed || lblProyecto.IsDisposed || panelChipsPorcentajes.IsDisposed) return;

            int separacion = panelTop.LogicalToDeviceUnits(SeparacionProyectoChipsLogica);
            int disponible = panelChipsPorcentajes.Left - separacion - lblProyecto.Margin.Right - lblProyecto.Left;
            if (disponible < 0) disponible = 0;
            if (lblProyecto.Width == disponible) return;

            lblProyecto.Width = disponible;
        }

        /// <summary>
        /// Chip redondeado del header oscuro del presupuesto: control propio
        /// (no Label) que pinta su fondo anti-aliasado (sin Region) con relleno
        /// translúcido y hairline, y el texto en dos tonos (etiqueta apagada +
        /// valor en el acento del módulo).
        /// <para>
        /// Contrato de medidas: medir y pintar usan los mismos tipos de letra y
        /// los mismos <see cref="TextFormatFlags"/> (SingleLine | VerticalCenter |
        /// NoPrefix | NoPadding) y el texto se dibuja SIEMPRE en un rectángulo
        /// acotado, nunca en un punto. El tamaño preferido se calcula con la
        /// misma medida que usa el pintado, de modo que la píldora no puede
        /// quedar más chica que la línea de texto en ningún DPI.
        /// </para>
        /// <para>
        /// El relleno es lógico y se reaplica idempotentemente (siempre se
        /// recalcula desde las constantes lógicas, nunca sobre el valor ya
        /// escalado) al cambiarse el contenedor o el DPI del monitor.
        /// </para>
        /// </summary>
        internal sealed class ChipPorcentaje : Control
        {
            private static readonly Font FuenteEtiqueta = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            private static readonly Font FuenteValor = new("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);

            private static readonly Size MedidaSinLimite = new(int.MaxValue, int.MaxValue);

            // Sin SingleLine GDI ignora VerticalCenter; sin NoPadding el margen
            // oculto de MeasureText desalinearía el valor respecto de la etiqueta.
            internal const TextFormatFlags FormatoLinea =
                TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            internal static readonly Padding RellenoLogico = new(10, 5, 12, 5);

            // El ancho medido por piezas (etiqueta + espacio + valor) puede quedar
            // 1px por debajo del ancho real dibujado por el espaciado entre
            // caracteres en la unión; esta holgura evita recortes a la derecha.
            private const int HolguraHorizontalLogica = 2;

            private readonly string _etiqueta;
            private readonly string _valor;
            private readonly Color _colorEtiqueta;
            private readonly Color _colorValor;

            /// <summary>Chip de porcentaje: etiqueta apagada y valor en acento.</summary>
            public ChipPorcentaje(string etiqueta, string valor)
            {
                _etiqueta = etiqueta;
                _valor = valor;
                _colorEtiqueta = ColorChipEtiqueta;
                _colorValor = ColorChipValor;

                Configurar($"{etiqueta}  {valor}");
            }

            /// <summary>Chip de advertencia de una sola pieza.</summary>
            public ChipPorcentaje(string texto)
            {
                _etiqueta = texto;
                _valor = string.Empty;
                _colorEtiqueta = ColorChipAdvertencia;
                _colorValor = _colorEtiqueta;

                Configurar(texto);
            }

            private void Configurar(string texto)
            {
                SetStyle(ControlStyles.Selectable, false);
                SetStyle(
                    ControlStyles.UserPaint
                    | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.SupportsTransparentBackColor
                    | ControlStyles.ResizeRedraw,
                    true);
                Text = texto;
                AccessibleName = texto;
                AccessibleRole = AccessibleRole.Text;
                Font = FuenteEtiqueta;
                BackColor = Color.Transparent;
                TabStop = false;
                AutoSize = true;
                AplicarEscalaDpi();
            }

            /// <summary>
            /// Tamaño preferido exacto: la misma medición que usa el pintado
            /// (mismas fuentes y flags) más el relleno escalado vigente.
            /// </summary>
            public override Size GetPreferredSize(Size proposedSize)
            {
                Size contenido = MedirContenido(_etiqueta, _valor);
                return new Size(contenido.Width + Padding.Horizontal, contenido.Height + Padding.Vertical);
            }

            /// <summary>
            /// Medición canónica del contenido de ESTE chip (la que usa su
            /// tamaño preferido y su pintado), expuesta para las pruebas.
            /// </summary>
            internal Size MedirContenidoActual() => MedirContenido(_etiqueta, _valor);

            /// <summary>
            /// Medición canónica del contenido del chip, compartida por el
            /// tamaño preferido y por las pruebas de contrato.
            /// </summary>
            internal static Size MedirContenido(string etiqueta, string valor)
            {
                var medidaEtiqueta = TextRenderer.MeasureText(etiqueta, FuenteEtiqueta, MedidaSinLimite, FormatoLinea);
                int alto = Math.Max(
                    medidaEtiqueta.Height,
                    Math.Max(FuenteEtiqueta.Height, FuenteValor.Height));
                if (valor.Length == 0)
                    return new Size(medidaEtiqueta.Width, alto);

                var medidaEspacio = TextRenderer.MeasureText(" ", FuenteEtiqueta, MedidaSinLimite, FormatoLinea);
                var medidaValor = TextRenderer.MeasureText(valor, FuenteValor, MedidaSinLimite, FormatoLinea);
                return new Size(
                    medidaEtiqueta.Width + medidaEspacio.Width + medidaValor.Width + HolguraHorizontalLogica,
                    Math.Max(alto, medidaValor.Height));
            }

            /// <summary>
            /// Escala el relleno y el margen lógicos al DPI vigente. Se llama en
            /// la construcción, al cambiarse el padre y en cada cambio de DPI:
            /// siempre parte de las constantes lógicas, por lo que reaplicarla
            /// es idempotente (no hay doble escalado).
            /// </summary>
            internal void AplicarEscalaDpi()
            {
                Padding = new Padding(
                    LogicalToDeviceUnits(RellenoLogico.Left),
                    LogicalToDeviceUnits(RellenoLogico.Top),
                    LogicalToDeviceUnits(RellenoLogico.Right),
                    LogicalToDeviceUnits(RellenoLogico.Bottom));
                Margin = new Padding(0, 0, LogicalToDeviceUnits(SoproUiMetrics.HorizontalGap), 0);
            }

            // El chip se crea en runtime: en la construcción el DPI puede no
            // reflejar aún el monitor destino; al engancharse al contenedor y
            // en cada cambio de DPI el relleno lógico se vuelve a aplicar.
            protected override void OnParentChanged(EventArgs e)
            {
                base.OnParentChanged(e);
                AplicarEscalaDpi();
            }

            protected override void OnDpiChangedAfterParent(EventArgs e)
            {
                base.OnDpiChangedAfterParent(e);
                AplicarEscalaDpi();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var borde = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var trayecto = CrearTrayectoRedondeado(borde))
                {
                    using (var relleno = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
                        graphics.FillPath(relleno, trayecto);
                    using (var linea = new Pen(Color.FromArgb(70, 255, 255, 255)))
                        graphics.DrawPath(linea, trayecto);
                }

                // Rectángulo acotado a la altura completa del control: GDI
                // centra la línea (SingleLine + VerticalCenter) respetando el
                // alto real de la fuente, sin cálculos manuales de "y".
                var area = new Rectangle(
                    Padding.Left, 0, Math.Max(0, Width - Padding.Horizontal), Height);
                TextRenderer.DrawText(graphics, _etiqueta, FuenteEtiqueta, area, _colorEtiqueta, FormatoLinea);

                if (_valor.Length == 0) return;

                // El avance del valor se mide con el mismo Graphics del pintado
                // y los mismos flags, para que quede pegado tras un espacio.
                int avanceValor = TextRenderer.MeasureText(graphics, _etiqueta, FuenteEtiqueta, MedidaSinLimite, FormatoLinea).Width
                                  + TextRenderer.MeasureText(graphics, " ", FuenteEtiqueta, MedidaSinLimite, FormatoLinea).Width;
                var areaValor = new Rectangle(
                    area.X + avanceValor, 0, Math.Max(0, area.Width - avanceValor), Height);
                TextRenderer.DrawText(graphics, _valor, FuenteValor, areaValor, _colorValor, FormatoLinea);
            }

            private GraphicsPath CrearTrayectoRedondeado(Rectangle limites)
            {
                int radio = Math.Max(2, (int)Math.Round(SoproUiMetrics.SurfaceRadiusPx * (DeviceDpi > 0 ? DeviceDpi : 96) / 96f));
                int diametro = Math.Min(radio * 2, Math.Min(limites.Width, limites.Height));
                diametro = Math.Max(1, diametro);

                var trayecto = new GraphicsPath();
                var arco = new Rectangle(limites.X, limites.Y, diametro, diametro);
                trayecto.AddArc(arco, 180, 90);
                arco.X = limites.Right - diametro;
                trayecto.AddArc(arco, 270, 90);
                arco.Y = limites.Bottom - diametro;
                trayecto.AddArc(arco, 0, 90);
                arco.X = limites.X;
                trayecto.AddArc(arco, 90, 90);
                trayecto.CloseFigure();
                return trayecto;
            }
        }
    }
}
