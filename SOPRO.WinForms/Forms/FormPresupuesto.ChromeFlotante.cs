using SOPRO.WinForms.Controls;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Chrome flotante del presupuesto (issue #171): header, toolbar principal y
    /// toolbar del área de trabajo se hospedan en wrappers ligeros con aire
    /// alrededor, borde de 1px redondeado y sombra neutra sutil — la profundidad
    /// del mockup. Solo decisiones ya aprobadas: <c>Solo chrome</c> y
    /// <c>Borde y sombra suave</c>.
    ///
    /// Límites: no se usa <c>Form.Padding</c> (cambiaría la geometría del
    /// split/grid/status) y no se tocan <c>dgvPresupuesto</c>, el panel de
    /// matrices embebido, <c>splitContainer</c>, el TabControl ni
    /// <c>statusStrip</c>. Las superficies conservan su Region redondeada
    /// (SurfaceRadius) y todos los colores existentes.
    /// </summary>
    public partial class FormPresupuesto
    {
        private ChromeFlotanteHost? _panelTopHost;
        private ChromeFlotanteHost? _panelToolbarHost;
        private ChromeFlotanteHost? _workspaceToolbarHost;
        private bool _chromeFlotanteAplicado;

        /// <summary>Host flotante del header oscuro (panelTop).</summary>
        internal ChromeFlotanteHost? PanelTopHost => _panelTopHost;

        /// <summary>Host flotante de la toolbar principal (panelToolbar).</summary>
        internal ChromeFlotanteHost? PanelToolbarHost => _panelToolbarHost;

        /// <summary>Host flotante de la toolbar del área de trabajo.</summary>
        internal ChromeFlotanteHost? WorkspaceToolbarHost => _workspaceToolbarHost;

        /// <summary>
        /// Hospeda las tres superficies del chrome. Se invoca una sola vez desde
        /// el constructor, después de <see cref="InitializeComponent"/>. El orden
        /// de acople del form queda: split (Fill) - statusStrip (Bottom) -
        /// host de la toolbar (Top) - host del header (Top), idéntico al orden
        /// dockable previo; dentro de <c>panelMatrices</c> el host de la toolbar
        /// del workspace sigue acoplado arriba con el host operativo en Fill.
        /// </summary>
        private void AplicarChromeFlotante()
        {
            if (_chromeFlotanteAplicado) return;
            _chromeFlotanteAplicado = true;

            _panelToolbarHost = ChromeFlotanteHost.Hospedar(
                panelToolbar, contenedor: this, nombre: "panelToolbarHost",
                colorFondo: BackColor,
                insetSuperiorLogico: 0);

            _panelTopHost = ChromeFlotanteHost.Hospedar(
                panelTop, contenedor: this, nombre: "panelTopHost",
                colorFondo: BackColor);

            _workspaceToolbarHost = ChromeFlotanteHost.Hospedar(
                workspaceToolbar, contenedor: panelMatrices, nombre: "workspaceToolbarHost",
                colorFondo: panelMatrices.BackColor,
                insetSuperiorLogico: ChromeFlotanteHost.SeparacionWorkspaceLogica,
                separacionInferiorLogica: ChromeFlotanteHost.SeparacionWorkspaceLogica);
        }
    }
}
