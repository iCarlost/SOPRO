using System;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;

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

            // Regiones redondeadas de las superficies (contrato del prototipo
            // anterior): responden por sí solas a tamaño, handle y DPI.
            SoproTheme.ApplyRoundedCorners(panelTop, SoproUiMetrics.SurfaceRadiusPx);
            SoproTheme.ApplyRoundedCorners(panelToolbar, SoproUiMetrics.SurfaceRadiusPx);
            SoproTheme.ApplyRoundedCorners(workspaceToolbar, SoproUiMetrics.SurfaceRadiusPx);

            // Se hospeda primero la toolbar para conservar los índices z del
            // form: al re-agregar cada host queda exactamente donde estaba su
            // superficie, así el orden de acople no se invierte.
            _panelToolbarHost = Hospedar(
                panelToolbar, contenedor: this, nombre: "panelToolbarHost",
                insetSuperior: 0,
                separacionInferior: ChromeFlotanteHost.SeparacionInferiorLogica,
                colorFondo: BackColor);

            _panelTopHost = Hospedar(
                panelTop, contenedor: this, nombre: "panelTopHost",
                insetSuperior: ChromeFlotanteHost.InsetSuperiorLogico,
                separacionInferior: ChromeFlotanteHost.SeparacionInferiorLogica,
                colorFondo: BackColor);

            _workspaceToolbarHost = Hospedar(
                workspaceToolbar, contenedor: panelMatrices, nombre: "workspaceToolbarHost",
                insetSuperior: ChromeFlotanteHost.SeparacionWorkspaceLogica,
                separacionInferior: ChromeFlotanteHost.SeparacionWorkspaceLogica,
                colorFondo: panelMatrices.BackColor);
        }

        /// <summary>
        /// Mueve la superficie a un <see cref="ChromeFlotanteHost"/> nuevo y deja
        /// el host en el mismo índice z que ocupaba la superficie dentro de su
        /// contenedor (reordena explícitamente para que el acople no dependa del
        /// orden de llegada).
        /// </summary>
        private static ChromeFlotanteHost Hospedar(
            Control superficie, Control contenedor, string nombre,
            int insetSuperior, int separacionInferior, Color colorFondo)
        {
            // Índice z ANTES de crear el host: el constructor del host ya
            // reparenta la superficie dentro de sí mismo.
            int indice = contenedor.Controls.GetChildIndex(superficie);

            var host = new ChromeFlotanteHost(
                superficie,
                SoproUiMetrics.SurfaceRadiusPx,
                nombre,
                insetSuperior,
                separacionInferior,
                colorFondo);

            contenedor.Controls.Remove(superficie);
            contenedor.Controls.Add(host);
            contenedor.Controls.SetChildIndex(host, Math.Clamp(indice, 0, contenedor.Controls.Count - 1));

            return host;
        }
    }
}
