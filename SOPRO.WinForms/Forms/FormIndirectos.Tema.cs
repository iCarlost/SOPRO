using System;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Tema del módulo de Indirectos (issue #205): viste el chrome y los
    /// controles propios del formulario con los tokens de la paleta activa
    /// del proceso (<see cref="SoproTheme"/>), coherente con los tres presets
    /// (Azul claro, Grafito, Verde claro).
    ///
    /// NO toca la cascada de formato de datos (colores de celda configurados
    /// por el usuario en BD, estilos de rol y overlays), el cálculo, la
    /// persistencia ni los formatos de reporte/exportación. Tampoco modifica
    /// el helper genérico de rejillas (<see cref="DgvCeldaHelper"/>): sólo
    /// viste los encabezados y fondos de los grids de ESTE formulario.
    ///
    /// Pestañas: el cuerpo de cada TabPage toma la superficie del tema
    /// (<c>UseVisualStyleBackColor = false</c>). La BANDA de pestañas queda
    /// NATIVA (tono del sistema): tematizarla con owner-draw fue descartado
    /// tras revisión (pintado fuera del clip de update, botones de scroll de
    /// desborde, banderas de WM_PRINT, vida del handle, HC/DPI).
    /// </summary>
    public partial class FormIndirectos
    {
        /// <summary>
        /// Aplica el tema. Debe llamarse tras <see cref="InitializeComponent"/>
        /// y ANTES de hospedar el chrome flotante, porque el host toma el
        /// <see cref="Form.BackColor"/> como fondo de la tarjeta.
        ///
        /// El chrome de los grids NO va aquí: el helper genérico
        /// <see cref="DataGridViewStyleHelper.AplicarEstiloSOPRO"/> corre después
        /// en el constructor y pisa BackgroundColor/GridColor/encabezados, así
        /// que ese repasado vive en <see cref="AplicarTemaGrids"/>.
        /// </summary>
        internal void AplicarTema()
        {
            // Cuerpo de la ventana: fondo central del tema (lo usa también el
            // ChromeFlotanteHost como aire alrededor de header y toolbar).
            BackColor = SoproTheme.Colors.CenterBg;

            TemarHeader();
            TemarToolbar();
            TemarTabs();
            TemarConfiguracion();
            TemarStatus();
        }

        /// <summary>
        /// Chrome de rejilla del módulo. Debe llamarse DESPUÉS de
        /// <c>AplicarEstiloSOPRO()</c> (que pinta los grids en blanco y pisa los
        /// encabezados) y puede correr antes de <see cref="ConfigurarGrids"/>:
        /// el helper de celdas no toca fondo, líneas ni encabezados de columna.
        /// </summary>
        internal void AplicarTemaGrids()
        {
            foreach (var dgv in new[] { dgvOficinaCentral, dgvCampo })
            {
                dgv.BackgroundColor = SoproTheme.Colors.Surface;
                dgv.GridColor = SoproTheme.Colors.GridLine;
                dgv.EnableHeadersVisualStyles = false;

                var encabezado = dgv.ColumnHeadersDefaultCellStyle;
                encabezado.BackColor = SoproTheme.Colors.HeaderBg;
                encabezado.ForeColor = SoproTheme.Colors.Ink;
                encabezado.SelectionBackColor = SoproTheme.Colors.HeaderBg;
                encabezado.SelectionForeColor = SoproTheme.Colors.Ink;
                encabezado.Font = SoproTheme.Fonts.GridHeader;
                dgv.ColumnHeadersDefaultCellStyle = encabezado;

                // Los colores de CELDA (fondo/fuente por columna y por celda)
                // vienen de la configuración del usuario en BD y se resuelven
                // en la cascada de formato: aquí no se tocan.
            }
        }

        private void TemarHeader()
        {
            // Banda de encabezado de diálogo: HeaderDark con hairline inferior.
            panelTop.BackColor = SoproTheme.Colors.HeaderDark;
            panelTop.Paint -= PanelTop_TemaPaint;
            panelTop.Paint += PanelTop_TemaPaint;

            lblTitulo.BackColor = Color.Transparent;
            lblTitulo.ForeColor = SoproTheme.Colors.OnHeader;

            lblProyecto.BackColor = Color.Transparent;
            lblProyecto.ForeColor = SoproTheme.Colors.SubtitleText;
        }

        /// <summary>
        /// Handler estable (método con nombre) para poder des-suscribirlo antes
        /// de suscribirlo: no acumula pintados si el tema se aplica de nuevo.
        /// </summary>
        private void PanelTop_TemaPaint(object? sender, PaintEventArgs e)
        {
            var rect = panelTop.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using var pen = new Pen(SoproTheme.Colors.HeaderBottom);
            e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
        }

        private void TemarToolbar()
        {
            panelToolbar.BackColor = SoproTheme.Colors.SecondaryBg;
            panelToolbar.Renderer = new RendererToolbarIndirectos();

            foreach (ToolStripItem item in panelToolbar.Items)
            {
                item.BackColor = SoproTheme.Colors.SecondaryBg;
                item.ForeColor = SoproTheme.Colors.Ink2;
            }

            // Salida: tono apagado, sin competir con los comandos.
            btnCerrar.ForeColor = SoproTheme.Colors.Muted;
        }

        /// <summary>
        /// Sólo el CUERPO de cada página toma la superficie del tema. La banda
        /// de pestañas y su área restante quedan nativas: sin owner-draw el
        /// TabControl no expinta esa zona, y repintarla requiere subclasificar
        /// el handle (descartado por fragilidad en revisión).
        /// </summary>
        private void TemarTabs()
        {
            foreach (var page in new[] { tabOficinaCentral, tabCampo, tabConfiguracion })
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = SoproTheme.Colors.Surface;
            }
        }

        private void TemarConfiguracion()
        {
            lblVolumenAnual.ForeColor = SoproTheme.Colors.Ink2;
            lblCostoDirecto.ForeColor = SoproTheme.Colors.Ink2;

            txtVolumenAnual.BackColor = SoproTheme.Colors.Surface;
            txtVolumenAnual.ForeColor = SoproTheme.Colors.Ink;
            txtCostoDirecto.BackColor = SoproTheme.Colors.Surface;
            txtCostoDirecto.ForeColor = SoproTheme.Colors.Ink;

            gbResumen.BackColor = SoproTheme.Colors.Surface;
            gbResumen.ForeColor = SoproTheme.Colors.Ink2;
            // Los porcentajes (total/campo/oficina central) conservan sus
            // colores semánticos de dato: sólo se les da fondo transparente.
            lblPorcentajeTotal.BackColor = Color.Transparent;
            lblPorcentajeCampo.BackColor = Color.Transparent;
            lblPorcentajeOC.BackColor = Color.Transparent;
        }

        private void TemarStatus()
        {
            statusStrip.BackColor = SoproTheme.Colors.HeaderBg;
            statusStrip.ForeColor = SoproTheme.Colors.Muted;
            lblStatus.ForeColor = SoproTheme.Colors.Muted;
        }

        /// <summary>
        /// Renderer mínimo de la toolbar del módulo: fondo en superficie
        /// secundaria y, en hover/presionado, relleno con los tokens del tema
        /// MÁS contorno de marca (los rellenos solos quedan ~1:1 contra la
        /// banda; el contorno de marca da ≥3:1 de affordance en los tres
        /// presets). Sin borde general (ChromeFlotanteHost dibuja el suyo).
        /// Sólo afecta a esta toolbar.
        /// </summary>
        private sealed class RendererToolbarIndirectos : ToolStripProfessionalRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using var brush = new SolidBrush(SoproTheme.Colors.SecondaryBg);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                // Sin borde: ChromeFlotanteHost dibuja el hairline de la tarjeta.
            }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item is not ToolStripButton button) return;
                if (!button.Selected && !button.Pressed) return;

                var rect = new Rectangle(Point.Empty, button.Size);

                Color fondo = button.Pressed
                    ? SoproTheme.RibbonClaro.Presionado
                    : SoproTheme.Colors.HoverBg;

                using (var brush = new SolidBrush(fondo))
                    e.Graphics.FillRectangle(brush, rect);

                // Affordance: los rellenos claros contrastan ~1:1 con la banda;
                // el contorno de marca aporta el ≥3:1 no textual.
                using var pen = new Pen(SoproTheme.Colors.BrandMd);
                e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
            }
        }
    }
}
