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
                    tab.BackColor = Color.White;

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
            var texto = ObtenerTextoNodoExpandido(nodo.Name);
            var anchoTexto = TextRenderer.MeasureText(
                texto ?? string.Empty,
                treeMenu.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;

            var sangria = Math.Max(0, nodo.Level) * EscalarPixelsSegunDpi(18);
            var glifos = EscalarPixelsSegunDpi(34);
            var anchoActual = anchoTexto + sangria + glifos;

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
            var estadoNodos = ObtenerNodosExpandidosSerializados();
            var controlActivo = tabControl.SelectedTab?.Controls.Count > 0 ? tabControl.SelectedTab.Controls[0] : null;
            var sidebarWidth = colapsada ? ObtenerAnchoBarraLateralColapsada() : ObtenerAnchoBarraLateralExpandida();

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
                panelLeft.MinimumSize = new Size(sidebarWidth, 0);
                panelLeft.MaximumSize = new Size(sidebarWidth, 0);
                panelLeft.Width = sidebarWidth;

                panelSidebarHeader.Height = 34;
                btnToggleSidebar.Dock = DockStyle.Fill;
                btnToggleSidebar.BringToFront();
                btnToggleSidebar.Visible = true;

                treeMenu.ShowPlusMinus = true;
                treeMenu.ShowLines = !colapsada;
                treeMenu.ShowRootLines = !colapsada;
                treeMenu.FullRowSelect = !colapsada;
                btnToggleSidebar.Text = colapsada ? "☰" : "◀";
                btnToggleSidebar.TextAlign = ContentAlignment.MiddleCenter;
                treeMenu.Indent = colapsada ? 10 : 18;
                treeMenu.ItemHeight = colapsada ? 22 : 26;

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
            if (!_proyecto.BarraLateralColapsada)
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
            AplicarEstadoBarraLateral(!_proyecto.BarraLateralColapsada);
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
    }
}
