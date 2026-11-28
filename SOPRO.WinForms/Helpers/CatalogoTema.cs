using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Tema de chrome compartido por los catálogos de insumos (issue #207):
    /// banda de título, barra de botones, banda de búsqueda/filtros, barra de
    /// estado y chrome a nivel de rejilla (fondo, líneas, encabezados).
    ///
    /// Solo viste: no crea controles, no cambia geometría, ítems ni ToolTips, y
    /// nunca toca el formato del usuario por columna (<c>TryColor</c>/
    /// <c>Columna*</c>) ni los estilos compartidos de gran impacto
    /// (<c>DataGridViewStyleHelper</c>, <c>DgvCeldaHelper</c>). La única columna
    /// que estila además de los estilos a nivel de rejilla es la de relleno
    /// vacía (<c>colDummy</c>, sin datos ni encabezado), que es chrome y no
    /// formato. El color del título lo gestiona
    /// <see cref="EditableReportTitleHelper"/> desde la configuración del
    /// usuario, así que aquí no se recolorea.
    ///
    /// Todos los valores se resuelven contra <see cref="SoproTheme"/> en cada
    /// uso: los tres presets quedan cubiertos sin estado propio.
    /// </summary>
    internal static class CatalogoTema
    {
        private static Color FondoBanda => SoproTheme.Colors.HeaderDark;
        private static Color FondoEstado => SoproTheme.Colors.HeaderBg;
        private static Color FondoToolbar => SoproTheme.Colors.SecondaryBg;
        private static Color Superficie => SoproTheme.Colors.Surface;
        private static Color Texto => SoproTheme.Colors.Ink2;
        private static Color TextoApagado => SoproTheme.Colors.Muted;
        private static Color Tinta => SoproTheme.Colors.Ink;
        private static Color Hover => SoproTheme.Colors.HoverBg;
        private static Color Presionado => SoproTheme.RibbonClaro.Presionado;
        private static Color Deshabilitado => SoproTheme.Colors.Disabled;
        private static Color Borde => SoproTheme.Colors.SurfaceBorder;

        /// <summary>
        /// Fondo legacy de la hoja de datos del catálogo: es el MISMO blanco que
        /// <c>AplicarEstiloDesdeCol*</c> usa como fallback al volcar el color
        /// persistido del usuario (<c>TryColor(col.ColorFondo, Color.White)</c>)
        /// y sobre el que alterna el zebra histórico <c>#F5F5F5</c> de
        /// <c>ConfigurarGrid</c>. NO es un token del tema: es la constante
        /// legacy que mantiene la hoja coherente en los tres presets (los
        /// claros ya son blancos; en Grafito la hoja de datos permanece clara
        /// porque el formato persistido se respeta tal cual).
        /// </summary>
        private static readonly Color FondoHojaLegacy = Color.White;

        /// <summary>
        /// Chrome completo del catálogo. Debe llamarse ANTES de
        /// <c>AplicarSuperficiesRedondeadas</c>: <c>ChromeFlotanteHost</c>
        /// captura el <see cref="Form.BackColor"/> al construirse.
        ///
        /// Los controles se pasan explícitamente para cubrir la variante
        /// Herramientas (nombres de bandas distintos) sin reflection.
        /// </summary>
        internal static void AplicarChrome(
            Form form,
            Panel bandaTitulo,
            ToolStrip barraBotones,
            Panel bandaFiltros,
            StatusStrip barraEstado,
            Label etiquetaBuscar,
            TextBox buscar,
            params CheckBox[] casillas)
        {
            bandaTitulo.BackColor = FondoBanda;
            AplicarBarraBotones(barraBotones);
            AplicarBandaFiltros(bandaFiltros, etiquetaBuscar, buscar, casillas);
            AplicarBarraEstado(barraEstado);

            // Al final: el host flotante aún no captura este valor.
            form.BackColor = SoproTheme.Colors.CenterBg;
        }

        /// <summary>
        /// Chrome de rejilla del catálogo. Re-aplicable: se invoca al final de
        /// cada <c>ConfigurarGrid</c> porque las operaciones de CRUD pueden
        /// reconfigurar el grid y devolverle los colores duros.
        ///
        /// Casi todo es estilo a nivel de rejilla. El zebra de filas alternas
        /// NO se toca: conserva el color y la semántica legacy de
        /// <c>ConfigurarGrid</c> (fila impar impone fondo sobre el formato por
        /// columna). El <see cref="DataGridViewCellStyle"/> por columna
        /// (formato del usuario) conserva su precedencia sobre el estilo base.
        ///
        /// Única excepción por columna: la columna de relleno vacía
        /// (<c>colDummy</c>, chrome sin formato de usuario) se viste en
        /// <see cref="AplicarColumnaRelleno"/>. Sus filas impares siguen
        /// heredando el zebra histórico <c>#F5F5F5</c> de
        /// <c>ConfigurarGrid</c> (la precedencia del estilo de filas alternas
        /// gana al estilo por columna); solo se alinea el fondo de las pares,
        /// que sin ello caería al fondo de superficie oscuro.
        /// </summary>
        internal static void AplicarGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = Superficie;
            dgv.GridColor = SoproTheme.Colors.GridLine;

            var encabezados = dgv.ColumnHeadersDefaultCellStyle;
            encabezados.BackColor = FondoEstado;
            encabezados.ForeColor = Tinta;
            encabezados.SelectionBackColor = FondoEstado;
            encabezados.SelectionForeColor = Tinta;
            dgv.ColumnHeadersDefaultCellStyle = encabezados;

            // Encabezados de fila: DgvCeldaHelper fija un gris claro fijo tras
            // su llamada; se re-mapea al preset sin tocar el helper compartido.
            var encabezadosFila = dgv.RowHeadersDefaultCellStyle;
            encabezadosFila.BackColor = FondoEstado;
            encabezadosFila.ForeColor = Texto;
            encabezadosFila.SelectionBackColor = SoproTheme.Colors.SelBg;
            encabezadosFila.SelectionForeColor = SoproTheme.Colors.OnSelection;
            dgv.RowHeadersDefaultCellStyle = encabezadosFila;

            // Patrón read-mutate-assign-back: el getter de los estilos devuelve
            // una copia desechable, hay que reasignar el objeto al grid.
            var celdas = dgv.DefaultCellStyle;
            celdas.BackColor = Superficie;
            celdas.ForeColor = Texto;
            dgv.DefaultCellStyle = celdas;

            AplicarColumnaRelleno(dgv);
        }

        /// <summary>
        /// Viste la columna de relleno (Fill sin datos, <c>colDummy</c>) que
        /// cierra el ancho sobrante de la rejilla.
        ///
        /// Sin estilo propio, sus filas pares heredan el fondo de superficie
        /// (oscuro en Grafito) mientras el zebra claro de <c>ConfigurarGrid</c>
        /// impone el suyo en las impares: tablero de ajedrez en toda la zona
        /// vacía. Se le da el fondo base legacy
        /// (<see cref="FondoHojaLegacy"/>) para que el zebra alterne sobre él y
        /// el relleno continúe la hoja de datos con el mismo ritmo de franjas.
        /// No toca las columnas de datos ni su formato persistido.
        /// </summary>
        private static void AplicarColumnaRelleno(DataGridView dgv)
        {
            foreach (DataGridViewColumn columna in dgv.Columns)
            {
                if (columna.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill) continue;
                if (!string.IsNullOrEmpty(columna.DataPropertyName)) continue;
                if (!string.IsNullOrEmpty(columna.HeaderText)) continue;
                // Las columnas reales del catálogo llevan la Columna* persistida
                // en Tag (fuente del formato del usuario); el relleno no lleva
                // ninguno. Cuarto filtro defensivo contra vestir una columna
                // de datos si algún día cambiara el patrón del relleno.
                if (columna.Tag != null) continue;

                var estiloRelleno = columna.DefaultCellStyle;
                estiloRelleno.BackColor = FondoHojaLegacy;
                columna.DefaultCellStyle = estiloRelleno;
            }
        }

        private static void AplicarBarraBotones(ToolStrip barra)
        {
            // Escalón de fondo propio (SecondaryBg) para que la banda de
            // comandos se distinga de la banda de filtros sobre la misma
            // ventana, con el mismo token en la barra y en sus ítems.
            barra.BackColor = FondoToolbar;
            barra.Renderer = new RendererCatalogo(fondo: FondoToolbar, bordeSuperior: false);

            foreach (ToolStripItem item in barra.Items)
            {
                item.BackColor = FondoToolbar;
                item.ForeColor = Texto;
            }
        }

        private static void AplicarBandaFiltros(
            Panel banda, Label etiqueta, TextBox buscar, CheckBox[] casillas)
        {
            banda.BackColor = Superficie;
            etiqueta.ForeColor = Texto;

            buscar.BackColor = Superficie;
            buscar.ForeColor = Tinta;

            foreach (var casilla in casillas)
            {
                if (casilla == null) continue;

                // Sin UseVisualStyleBackColor el CheckBox ignora el BackColor
                // teñido; con false, glifo y cara usan los colores del tema.
                casilla.UseVisualStyleBackColor = false;
                casilla.BackColor = Superficie;
                casilla.ForeColor = Texto;
            }
        }

        private static void AplicarBarraEstado(StatusStrip barra)
        {
            barra.BackColor = FondoEstado;
            barra.ForeColor = TextoApagado;
            barra.Renderer = new RendererCatalogo(fondo: FondoEstado, bordeSuperior: true);

            foreach (ToolStripItem item in barra.Items)
                item.ForeColor = TextoApagado;
        }

        /// <summary>
        /// Renderer mínimo para la barra de botones y la de estado: relleno
        /// plano del tema (sin gradiente nativo), estados hover/presionado/
        /// deshabilitado con tokens y hairline superior opcional.
        ///
        /// Los comandos de los catálogos son <see cref="ToolStripButton"/>, y
        /// sus estados activos pasan por <see cref="OnRenderButtonBackground"/>
        /// (no por <c>OnRenderMenuItemBackground</c>, que solo atiende menús):
        /// aquí es donde cobra efecto el hover y el presionado. Los demás tipos
        /// de ítem conservan el pintado base. El borde exterior de las
        /// superficies flotantes lo pinta <c>ChromeFlotanteHost</c>, así que la
        /// barra de botones no dibuja borde propio.
        /// </summary>
        private sealed class RendererCatalogo : ToolStripProfessionalRenderer
        {
            private readonly Color _fondo;
            private readonly bool _bordeSuperior;

            public RendererCatalogo(Color fondo, bool bordeSuperior)
            {
                _fondo = fondo;
                _bordeSuperior = bordeSuperior;
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using var brush = new SolidBrush(_fondo);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                if (!_bordeSuperior) return;

                using var pen = new Pen(Borde);
                e.Graphics.DrawLine(pen, 0, 0, e.ToolStrip.Width, 0);
            }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item == null || (!e.Item.Selected && !e.Item.Pressed))
                {
                    base.OnRenderButtonBackground(e);
                    return;
                }

                Color fondo = e.Item.Pressed ? Presionado : Hover;
                using (var brush = new SolidBrush(fondo))
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (e.Item != null && !e.Item.Enabled)
                    e.TextColor = Deshabilitado;

                base.OnRenderItemText(e);
            }
        }
    }
}
