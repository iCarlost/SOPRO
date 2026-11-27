using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Árbol de menú, barra lateral, host de pestañas y persistencia del estado visual.
    /// </summary>
    public partial class FormProyecto
    {

        private void AjustarHostTabs()
        {
            if (tabControl == null)
                return;

            tabControl.SuspendLayout();
            try
            {
                foreach (TabPage tab in tabControl.TabPages)
                {
                    tab.Padding = new Padding(0);
                    tab.Margin = new Padding(0);
                    tab.UseVisualStyleBackColor = true;
                    tab.BackColor = SoproTheme.Colors.Surface;

                    if (tab.Controls.Count == 0)
                        continue;

                    var contenido = tab.Controls[0];
                    contenido.Margin = new Padding(0);
                    contenido.Padding = new Padding(0);

                    var areaUtil = tab.ClientRectangle;
                    if (areaUtil.Width <= 0 || areaUtil.Height <= 0)
                        areaUtil = tab.DisplayRectangle;

                    contenido.Dock = DockStyle.None;
                    contenido.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                    contenido.Location = new Point(0, 0);
                    contenido.Bounds = new Rectangle(Point.Empty, areaUtil.Size);

                    if (contenido is Form formContenido)
                    {
                        formContenido.WindowState = FormWindowState.Normal;
                        formContenido.MinimumSize = Size.Empty;
                        formContenido.MaximumSize = Size.Empty;
                    }
                }
            }
            finally
            {
                tabControl.ResumeLayout(true);
                tabControl.PerformLayout();
                if (tabControl.SelectedTab != null)
                    tabControl.SelectedTab.PerformLayout();
            }
        }


        private void ProgramarAjusteHostTabsDiferido()
        {
            if (_ajusteHostTabsPendiente || IsDisposed || !IsHandleCreated)
                return;

            _ajusteHostTabsPendiente = true;
            BeginInvoke(new Action(() =>
            {
                _ajusteHostTabsPendiente = false;
                if (IsDisposed)
                    return;

                AjustarHostTabs();
                if (tabControl?.SelectedTab != null)
                {
                    tabControl.SelectedTab.PerformLayout();
                    if (tabControl.SelectedTab.Controls.Count > 0)
                        tabControl.SelectedTab.Controls[0].PerformLayout();
                }
            }));
        }

        private void RestaurarEstadoBarraYLateral()
        {
            _restaurandoEstadoArbol = true;
            try
            {
                RestaurarEstadoNodosVisual(_proyecto.NodosMenuExpandidos, expandirTodoSiVacio: true);
                AplicarEstadoBarraLateral(_proyecto.BarraLateralColapsada, false);
            }
            finally
            {
                _restaurandoEstadoArbol = false;
            }
        }

        private void RestaurarNodosExpandidos(string serializado)
        {
            treeMenu.CollapseAll();
            var nombres = serializado
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (TreeNode nodo in treeMenu.Nodes)
                RestaurarNodoExpandidoRecursivo(nodo, nombres);
        }

        private void RestaurarNodoExpandidoRecursivo(TreeNode nodo, System.Collections.Generic.HashSet<string> nombres)
        {
            if (nombres.Contains(nodo.Name))
                nodo.Expand();

            foreach (TreeNode hijo in nodo.Nodes)
                RestaurarNodoExpandidoRecursivo(hijo, nombres);
        }

        private void RestaurarEstadoNodosVisual(string serializado, bool expandirTodoSiVacio)
        {
            treeMenu.CollapseAll();

            if (string.IsNullOrWhiteSpace(serializado))
            {
                if (expandirTodoSiVacio)
                    treeMenu.ExpandAll();
                return;
            }

            RestaurarNodosExpandidos(serializado);
        }

        private string ObtenerNodosExpandidosSerializados()
        {
            var nombres = new System.Collections.Generic.List<string>();
            foreach (TreeNode nodo in treeMenu.Nodes)
                RecopilarNodosExpandidos(nodo, nombres);
            return string.Join("|", nombres.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private void RecopilarNodosExpandidos(TreeNode nodo, System.Collections.Generic.List<string> nombres)
        {
            if (nodo.IsExpanded && !string.IsNullOrWhiteSpace(nodo.Name))
                nombres.Add(nodo.Name);

            foreach (TreeNode hijo in nodo.Nodes)
                RecopilarNodosExpandidos(hijo, nombres);
        }

        private void GuardarEstadoNodosMenu()
        {
            if (_restaurandoEstadoArbol) return;
            try
            {
                _proyecto.NodosMenuExpandidos = ObtenerNodosExpandidosSerializados();
                _context.Proyectos.Update(_proyecto);
                _context.SaveChanges();
            }
            catch
            {
                // No bloquear UX por persistencia visual.
            }
        }

        private int ObtenerAnchoBarraLateralColapsada()
        {
            return EscalarPixelsSegunDpi(SidebarCollapsedWidth);
        }

        /// <summary>
        /// Reaplica TODA la geometría del sidebar dependiente del modo y del
        /// DPI: ancho del panel, header, botón de colapso (tamaño/posición),
        /// indent y alto de fila. La usan el toggle y los cambios de DPI para
        /// no duplicar la receta ni dejar métricas desfasadas.
        /// </summary>
        private void AplicarGeometriaSidebar(bool colapsada)
        {
            var metricas = SidebarMetrics.Crear(DeviceDpi);
            var sidebarWidth = colapsada ? ObtenerAnchoBarraLateralColapsada() : ObtenerAnchoBarraLateralExpandida();

            panelLeft.MinimumSize = new Size(sidebarWidth, 0);
            panelLeft.MaximumSize = new Size(sidebarWidth, 0);
            panelLeft.Width = sidebarWidth;

            panelSidebarHeader.Height = metricas.AltoHeader;
            btnToggleSidebar.Dock = DockStyle.None;
            btnToggleSidebar.Size = new Size(metricas.LadoBotonColapso, metricas.LadoBotonColapso);
            btnToggleSidebar.Location = new Point(
                Math.Max(0, panelSidebarHeader.Width - metricas.LadoBotonColapso - metricas.MargenBotonColapso - 1),
                Math.Max(0, (metricas.AltoHeader - metricas.LadoBotonColapso) / 2));
            btnToggleSidebar.BringToFront();
            btnToggleSidebar.Visible = true;

            // La indentación nativa solo alimenta el hit-testing (GetNodeAt);
            // el dibujo usa las métricas.
            treeMenu.Indent = metricas.SangriaNivel;
            treeMenu.ItemHeight = colapsada ? metricas.AltoFilaColapsada : metricas.AltoFila;

            AlinearBloqueMarcaRibbon();
        }

        private const int MargenLateralMarca = 14;

        /// <summary>
        /// Alinea el arranque del ribbon con el borde derecho real del sidebar
        /// y centra el wordmark dentro del bloque de marca (issue #195). En
        /// expandido el separador cae exactamente en panelLeft.Width (variaba
        /// con el modo y el DPI; antes quedaba fijo en 180). Si el rail
        /// colapsado no alcanza a contener el wordmark con aire propio, el
        /// bloque conserva un ancho mínimo medido desde el texto, nunca
        /// hardcodeado. La invoca AplicarGeometriaSidebar: toggle y cambio de
        /// DPI no dejan la alineación desfasada.
        /// </summary>
        private void AlinearBloqueMarcaRibbon()
        {
            if (panelRibbon == null || panelLeft == null || lblTitulo == null) return;

            int margenMarca = EscalarPixelsSegunDpi(MargenLateralMarca);
            int anchoTexto = lblTitulo.PreferredWidth;
            int anchoMinimoBloque = anchoTexto + 2 * margenMarca;
            int inicioRibbon = Math.Max(anchoMinimoBloque, panelLeft.Width);

            // Se conserva el borde derecho del ribbon: desplazar el inicio sin
            // recortar el ancho empujaría el extremo derecho fuera del margen.
            panelRibbon.Width = Math.Max(1, panelRibbon.Right - inicioRibbon);
            panelRibbon.Left = inicioRibbon;

            // Wordmark centrado en el bloque de marca y en el alto de la banda.
            lblTitulo.Left = Math.Max(margenMarca, (inicioRibbon - anchoTexto) / 2);
            lblTitulo.Top = Math.Max(0, (panelTop.Height - lblTitulo.PreferredHeight) / 2);
        }

        private int ObtenerAnchoBarraLateralExpandida()
        {
            var anchoBase = EscalarPixelsSegunDpi(SidebarExpandedWidth);
            var anchoMaximo = EscalarPixelsSegunDpi(SidebarExpandedMaxWidth);
            var anchoContenido = MedirAnchoContenidoTreeMenuExpandido();

            return Math.Min(anchoMaximo, Math.Max(anchoBase, anchoContenido));
        }

        private int MedirAnchoContenidoTreeMenuExpandido()
        {
            if (treeMenu == null)
                return EscalarPixelsSegunDpi(SidebarExpandedWidth);

            var maxAnchoTexto = 0;
            foreach (TreeNode nodo in treeMenu.Nodes)
                maxAnchoTexto = Math.Max(maxAnchoTexto, MedirNodoExpandido(nodo));

            var bordePanel = EscalarPixelsSegunDpi(18);
            var margenSeguridad = EscalarPixelsSegunDpi(24);
            return maxAnchoTexto + bordePanel + margenSeguridad;
        }

        private int MedirNodoExpandido(TreeNode nodo)
        {
            var metricas = SidebarMetrics.Crear(DeviceDpi);
            var texto = ObtenerTextoNodoExpandido(nodo.Name);
            var anchoTexto = TextRenderer.MeasureText(
                texto ?? string.Empty,
                treeMenu.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;

            // Mismo reparto de espacio que el pintado (issue #193): sangría por
            // nivel, acolchado de la píldora y, en raíces, la zona del chevron.
            bool esRaiz = nodo.Level == 0;
            int accesorios = esRaiz
                ? metricas.AnchoChevron + metricas.MargenChevron + metricas.AcolchadoPildoraHorizontal
                : 2 * metricas.AcolchadoPildoraHorizontal;
            int anchoActual = metricas.SangriaHoja(Math.Max(1, nodo.Level)) + accesorios + anchoTexto + metricas.MargenLateral;

            foreach (TreeNode hijo in nodo.Nodes)
                anchoActual = Math.Max(anchoActual, MedirNodoExpandido(hijo));

            return anchoActual;
        }

        private int EscalarPixelsSegunDpi(int pixelsBase)
        {
            var dpi = DeviceDpi > 0 ? DeviceDpi : 96;
            return (int)Math.Ceiling(pixelsBase * dpi / 96d);
        }

        private void AplicarEstadoBarraLateral(bool colapsada, bool persistir = true)
        {
            // El estado visual manda desde el primer instante: el pintado que
            // ocurra durante este cambio ya ve el modo solicitado, sin esperar
            // a que la persistencia actualice la entidad.
            _sidebarColapsadoVisual = colapsada;

            var estadoNodos = ObtenerNodosExpandidosSerializados();
            var controlActivo = tabControl.SelectedTab?.Controls.Count > 0 ? tabControl.SelectedTab.Controls[0] : null;

            _ajustandoBarraLateral = true;
            _restaurandoEstadoArbol = true;
            SuspendDrawing(this);
            SuspendDrawing(panelLeft);
            SuspendDrawing(panelSidebarHeader);
            SuspendDrawing(treeMenu);
            SuspendDrawing(tabControl);
            if (controlActivo != null)
                SuspendDrawing(controlActivo);

            SuspendLayout();
            panelLeft.SuspendLayout();
            panelSidebarHeader.SuspendLayout();
            tabControl.SuspendLayout();
            treeMenu.BeginUpdate();
            try
            {
                AplicarGeometriaSidebar(colapsada);
                btnToggleSidebar.Text = colapsada ? "»" : "«";

                ActualizarTextosTreeMenu(colapsada);
                RestaurarEstadoNodosVisual(estadoNodos, expandirTodoSiVacio: false);
            }
            finally
            {
                treeMenu.EndUpdate();
                tabControl.ResumeLayout(true);
                panelSidebarHeader.ResumeLayout(true);
                panelLeft.ResumeLayout(true);
                ResumeLayout(true);
                PerformLayout();
                panelLeft.PerformLayout();
                panelSidebarHeader.PerformLayout();
                tabControl.PerformLayout();
                AjustarHostTabs();
                if (tabControl.SelectedTab != null)
                    tabControl.SelectedTab.PerformLayout();
                if (controlActivo != null)
                    controlActivo.PerformLayout();

                if (controlActivo != null)
                    ResumeDrawing(controlActivo);
                ResumeDrawing(tabControl);
                ResumeDrawing(treeMenu);
                ResumeDrawing(panelSidebarHeader);
                ResumeDrawing(panelLeft);
                ResumeDrawing(this);
                ProgramarAjusteHostTabsDiferido();

                _restaurandoEstadoArbol = false;
                _ajustandoBarraLateral = false;
            }

            if (!persistir)
                return;

            try
            {
                _proyecto.BarraLateralColapsada = colapsada;
                _proyecto.NodosMenuExpandidos = estadoNodos;
                _context.Proyectos.Update(_proyecto);
                _context.SaveChanges();
            }
            catch
            {
                // No bloquear UX por persistencia visual.
            }
        }

        private void ActualizarTextosTreeMenu(bool colapsada)
        {
            treeMenu.BeginUpdate();
            try
            {
                foreach (TreeNode nodo in treeMenu.Nodes)
                    ActualizarTextoNodoRecursivo(nodo, colapsada);
            }
            finally
            {
                treeMenu.EndUpdate();
            }
        }

        private void ActualizarTextoNodoRecursivo(TreeNode nodo, bool colapsada)
        {
            nodo.Text = colapsada ? ObtenerTextoNodoColapsado(nodo.Name) : ObtenerTextoNodoExpandido(nodo.Name);
            foreach (TreeNode hijo in nodo.Nodes)
                ActualizarTextoNodoRecursivo(hijo, colapsada);
        }

        private string ObtenerTextoNodoExpandido(string nodeName)
        {
            return nodeName switch
            {
                "nodeDatosProyecto" => "📁 Datos del Proyecto",
                "nodePorcentajes" => "📐 Porcentajes",
                "nodeConfiguracion" => "⚙ Configuración",
                "nodeHojaPresupuesto" => "📄 Hoja de Presupuesto",
                "nodeExplosionInsumos" => "💥 Explosión de Insumos",
                "nodeIndirectos" => "📉 Cálculo de Indirectos",
                "nodeFinanciamiento" => "💳 Cálculo de Financiamiento",
                "nodeUtilidad" => "💰 Cálculo de Utilidad",
                "nodeFSR" => "👷 Factor Salario Real (FSR)",
                "nodePlantillaReporte" => "🧾 Plantilla de Reporte",
                "nodeReportes" => "📑 Reportes",
                "nodePresupuesto" => "📊 Presupuesto",
                "nodeMateriales" => "🧱 Materiales",
                "nodeManoObra" => "👷 Mano de Obra",
                "nodeHerramienta" => "🛠 Herramienta",
                "nodeEquipo" => "🚜 Equipo",
                "nodeMatrices" => "🧩 Matrices (APU/Básicos)",
                "nodeProgramaObra" => "🗓 Programa de Obra",
                "nodeProgramaInsumos" => "📦 Programa de Insumos",
                "nodeProgramacion" => "📅 Programación",
                "nodeCatalogos" => "📚 Catálogos",
                _ => nodeName
            };
        }

        private string ObtenerTextoNodoColapsado(string nodeName)
        {
            return nodeName switch
            {
                "nodeDatosProyecto" => "📁",
                "nodePorcentajes" => "📐",
                "nodeConfiguracion" => "⚙",
                "nodeHojaPresupuesto" => "📄",
                "nodeExplosionInsumos" => "💥",
                "nodeIndirectos" => "📉",
                "nodeFinanciamiento" => "💳",
                "nodeUtilidad" => "💰",
                "nodeFSR" => "👷",
                "nodePlantillaReporte" => "🧾",
                "nodeReportes" => "📑",
                "nodePresupuesto" => "📊",
                "nodeMateriales" => "🧱",
                "nodeManoObra" => "👷",
                "nodeHerramienta" => "🛠",
                "nodeEquipo" => "🚜",
                "nodeMatrices" => "🧩",
                "nodeProgramaObra" => "🗓",
                "nodeProgramaInsumos" => "📦",
                "nodeProgramacion" => "📅",
                "nodeCatalogos" => "📚",
                _ => string.Empty
            };
        }

        private void treeMenu_MouseMove(object sender, MouseEventArgs e)
        {
            if (!SidebarColapsado)
            {
                if (!string.IsNullOrEmpty(_ultimoTooltipNodo))
                {
                    _treeMenuToolTip.SetToolTip(treeMenu, string.Empty);
                    _ultimoTooltipNodo = string.Empty;
                }
                return;
            }

            var nodo = treeMenu.GetNodeAt(e.Location);
            var tooltip = nodo == null ? string.Empty : ObtenerTextoNodoExpandido(nodo.Name);
            if (tooltip == _ultimoTooltipNodo)
                return;

            _treeMenuToolTip.SetToolTip(treeMenu, tooltip);
            _ultimoTooltipNodo = tooltip;
        }

        private void treeMenu_MouseLeave(object sender, EventArgs e)
        {
            _treeMenuToolTip.SetToolTip(treeMenu, string.Empty);
            _ultimoTooltipNodo = string.Empty;
        }

        private void btnToggleSidebar_Click(object sender, EventArgs e)
        {
            AplicarEstadoBarraLateral(!SidebarColapsado);
        }

        private void treeMenu_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (_ajustandoBarraLateral) return;
            GuardarEstadoNodosMenu();
        }

        private void treeMenu_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            if (_ajustandoBarraLateral) return;
            GuardarEstadoNodosMenu();
        }

        // =====================================================================
        // SIDEBAR OWNER-DRAW (issue #193)
        // =====================================================================

        /// <summary>
        /// Pinta cada fila del árbol con el lenguaje del mockup: secciones como
        /// rótulos en mayúsculas con chevron propio y hojas como píldoras
        /// insertadas con estados reposo/hover/activo/foco. Todo el pintado
        /// puro vive en <see cref="SidebarRenderer"/> para poder probarlo sin UI.
        /// </summary>
        private void treeMenu_DrawNode(object? sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || treeMenu == null) return;

            var graphics = e.Graphics;
            if (graphics == null) return;

            bool colapsado = SidebarColapsado;
            var metricas = SidebarMetrics.Crear(DeviceDpi);
            var fila = new Rectangle(
                0,
                e.Bounds.Y,
                Math.Max(1, treeMenu.ClientSize.Width),
                Math.Max(1, treeMenu.ItemHeight));

            bool esRaiz = e.Node.Level == 0;
            bool seleccionada = (e.State & TreeNodeStates.Selected) != 0;
            bool conFoco = (e.State & TreeNodeStates.Focused) != 0;
            bool enHover = ReferenceEquals(e.Node, _nodoSidebarEnHover);

            SidebarRenderer.PintarFondo(graphics, fila);

            if (esRaiz)
            {
                SidebarRenderer.PintarSeccion(
                    graphics, fila, e.Node.Text,
                    e.Node.NodeFont ?? SoproTheme.Fonts.SidebarSeccion,
                    metricas, e.Node.IsExpanded, colapsado);
            }
            else
            {
                SidebarRenderer.PintarHoja(
                    graphics, fila, e.Node.Text,
                    e.Node.NodeFont ?? treeMenu.Font,
                    metricas, e.Node.Level,
                    seleccionada, conFoco, enHover, colapsado);
            }

            e.DrawDefault = false;
        }

        /// <summary>
        /// Un clic sobre una sección (raíz) alterna su expansión: con el glifo
        /// +/- nativo fuera, la fila completa es el objetivo de clic. La
        /// persistencia la siguen dando AfterExpand/AfterCollapse. Como el
        /// clic nativo seleccionaría la raíz (borrando la píldora de la hoja
        /// activa), se restaura la última hoja seleccionada. Las hojas no se
        /// tocan aquí: su clic lo resuelve <see cref="treeMenu_NodeMouseClick"/>.
        /// </summary>
        private void treeMenu_NodeMouseClick_ToggleRaiz(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node == null || e.Node.Level != 0) return;

            if (e.Node.IsExpanded)
                e.Node.Collapse();
            else
                e.Node.Expand();

            if (_ultimaHojaSeleccionadaSidebar?.TreeView != null)
                treeMenu.SelectedNode = _ultimaHojaSeleccionadaSidebar;
        }

        /// <summary>Recuerda la última hoja seleccionada (nunca raíces).</summary>
        private void treeMenu_AfterSelect_RecordarHoja(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Level > 0)
                _ultimaHojaSeleccionadaSidebar = e.Node;
        }

        /// <summary>Hover propio del sidebar: repinta solo las filas afectadas.</summary>
        private void treeMenu_MouseMove_Hover(object? sender, MouseEventArgs e)
        {
            if (treeMenu == null || !treeMenu.IsHandleCreated) return;

            var nodo = treeMenu.GetNodeAt(e.Location);
            if (ReferenceEquals(nodo, _nodoSidebarEnHover)) return;

            var previo = _nodoSidebarEnHover;
            _nodoSidebarEnHover = nodo;
            InvalidarFilaSidebar(previo);
            InvalidarFilaSidebar(nodo);
        }

        private void treeMenu_MouseLeave_Hover(object? sender, EventArgs e)
        {
            if (_nodoSidebarEnHover == null) return;

            var previo = _nodoSidebarEnHover;
            _nodoSidebarEnHover = null;
            InvalidarFilaSidebar(previo);
        }

        /// <summary>
        /// Ante un cambio de DPI se reaplica la receta completa de geometría
        /// (ancho, header, botón, indent, fila) y se repinta, sin persistir.
        /// </summary>
        private void treeMenu_DpiCambio(object? sender, EventArgs e)
        {
            if (treeMenu == null || _ajustandoBarraLateral) return;

            AplicarGeometriaSidebar(SidebarColapsado);
            treeMenu.Invalidate();
        }

        private void InvalidarFilaSidebar(TreeNode? nodo)
        {
            if (nodo == null || treeMenu == null || !treeMenu.IsHandleCreated) return;

            try
            {
                int y = nodo.Bounds.Top;
                treeMenu.Invalidate(new Rectangle(0, y, treeMenu.ClientSize.Width, treeMenu.ItemHeight));
            }
            catch (ArgumentOutOfRangeException)
            {
                // La fila puede haber desaparecido entre el hover y la invalidación.
            }
        }
    }
}
