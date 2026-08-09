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
    /// Navegación por pestañas, apertura de módulos, gestión de foco y buscador flotante.
    /// </summary>
    public partial class FormProyecto
    {

        public static void NotificarCambioInsumos()
        {
            foreach (Form f in System.Windows.Forms.Application.OpenForms)
            {
                if (f is not FormProyecto fp) continue;
                foreach (TabPage tab in fp.tabControl.TabPages)
                {
                    if (tab.Controls.Count == 0) continue;
                    var ctrl = tab.Controls[0];
                    if (ctrl is FormMatrices fm)
                        fm.RecargarMatrices();
                    else if (ctrl is FormPresupuesto pres)
                        pres.RefrescarPreciosDesdeDB();
                }
            }
        }

        private static Color TryColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }

        private static string HexColor(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        // =====================================================================
        // NAVEGACION
        // =====================================================================

        private void treeMenu_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node == null || e.Node.Parent == null) return;
            string n = e.Node.Name;

            foreach (TabPage tab in tabControl.TabPages)
                if (tab.Name == n)
                {
                    tabControl.SelectedTab = tab;
                    TabControl_SelectedIndexChanged(tabControl, EventArgs.Empty);
                    if (tab.Controls.Count > 0 && tab.Controls[0] is Form existingForm)
                    {
                        BeginInvoke(new Action(() => TransferirFocoAlFormularioAbierto(existingForm)));
                    }
                    return;
                }

            switch (n)
            {
                case "nodeDatosProyecto":
                    using (var f = new FormDatosProyecto(_proyecto))
                        if (f.ShowDialog() == DialogResult.OK)
                        {
                            // Capturar decimales anteriores para detectar cambio
                            int decImpAntes  = _context.Proyectos.AsNoTracking()
                                .Where(p => p.Id == _proyecto.Id)
                                .Select(p => p.DecimalesImporte).FirstOrDefault();
                            int decCantAntes = _context.Proyectos.AsNoTracking()
                                .Where(p => p.Id == _proyecto.Id)
                                .Select(p => p.DecimalesCantidad).FirstOrDefault();

                            _context.Proyectos.Update(_proyecto);
                            _context.SaveChanges();

                            // Si cambiaron los decimales, ejecutar recálculo global
                            bool cambioDecimales = _proyecto.DecimalesImporte  != decImpAntes
                                                || _proyecto.DecimalesCantidad != decCantAntes;
                            if (cambioDecimales)
                            {
                                var cursor = Cursor.Current;
                                Cursor.Current = Cursors.WaitCursor;
                                try
                                {
                                    var svc = new SOPRO.Application.Services.RecalculoGlobalService();
                                    svc.Ejecutar(_context, _proyecto.Id);
                                    MessageBox.Show("Datos actualizados.", "Guardado",
                                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                finally { Cursor.Current = cursor; }
                            }
                            else
                            {
                                MessageBox.Show("Datos actualizados.", "Guardado",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    break;
                case "nodePorcentajes":
                    Abrir("Porcentajes", new FormPorcentajes(_context, _proyecto), n); break;
                case "nodeHojaPresupuesto":
                    Abrir("Presupuesto", new FormPresupuesto(_context, _proyecto), n); break;
                case "nodeExplosionInsumos":
                    Abrir("Explosion de Insumos", new FormExplosionInsumos(_context, _proyecto.Id), n); break;
                case "nodeIndirectos":
                    Abrir("Indirectos", new FormIndirectos(_context, _proyecto), n); break;
                case "nodeFinanciamiento":
                    Abrir("Financiamiento", new FormFinanciamiento(_context, _proyecto), n); break;
                case "nodeUtilidad":
                    Abrir("Utilidad", new FormUtilidad(_context, _proyecto), n); break;
                case "nodeFSR":
                    Abrir("FSR", new FormFSR(_context, _proyecto), n); break;
                case "nodePlantillaReporte":
                    Abrir("Plantilla de Reporte", new FormPlantillaReporte(_context, _proyecto), n); break;
                case "nodeMateriales":
                    Abrir("Materiales", new FormCatalogoMateriales(_context, _proyecto.Id), n); break;
                case "nodeManoObra":
                    Abrir("Mano de Obra", new FormCatalogoManoObra(_context, _proyecto.Id), n); break;
                case "nodeHerramienta":
                    Abrir("Herramienta", new FormCatalogoHerramientas(_context, _proyecto.Id), n); break;
                case "nodeEquipo":
                    Abrir("Equipo", new FormCatalogoMaquinaria(_context, _proyecto.Id), n); break;
                case "nodeMatrices":
                    Abrir("Matrices", new FormMatrices(_context, _proyecto.Id), n); break;
                case "nodeProgramaObra":
                    Abrir("Programa de Obra", new FormProgramaObra(_context, _proyecto), n); break;
                case "nodeProgramaInsumos":
                    Abrir("Programa de Insumos", new FormProgramaInsumos(_context, _proyecto), n); break;
            }
        }

        private void RestaurarFocoTrasRecalculo(TabPage tabActiva, Form formActivoAntes, Control controlConFocoAntes)
        {
            if (IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;

                if (tabActiva != null && tabControl.TabPages.Contains(tabActiva))
                    tabControl.SelectedTab = tabActiva;

                if (controlConFocoAntes != null
                    && !controlConFocoAntes.IsDisposed
                    && controlConFocoAntes.Visible
                    && controlConFocoAntes.CanFocus)
                {
                    try
                    {
                        controlConFocoAntes.Select();
                        controlConFocoAntes.Focus();
                        return;
                    }
                    catch
                    {
                        // Si el control ya no acepta foco, caer al formulario activo.
                    }
                }

                if (formActivoAntes != null && !formActivoAntes.IsDisposed)
                    TransferirFocoAlFormularioAbierto(formActivoAntes);
            }));
        }

        private Control ObtenerControlConFoco(Control contenedor)
        {
            if (contenedor == null || contenedor.IsDisposed)
                return null;

            var actual = contenedor;
            while (actual is ContainerControl container && container.ActiveControl != null)
                actual = container.ActiveControl;

            return actual != null && actual.Focused ? actual : null;
        }

        private void TransferirFocoAlFormularioAbierto(Form form)
        {
            if (form == null || form.IsDisposed) return;

            tabControl.Focus();
            form.Select();
            form.Focus();

            var objetivo = EncontrarControlPreferente(form);
            if (objetivo != null)
            {
                objetivo.Select();
                objetivo.Focus();
            }
        }

        private Control EncontrarControlPreferente(Control contenedor)
        {
            foreach (Control child in contenedor.Controls)
            {
                if (!child.Visible || !child.CanSelect) continue;

                if (child is DataGridView || child is TextBoxBase || child is ComboBox || child is ListBox || child is TreeView)
                    return child;

                var nested = EncontrarControlPreferente(child);
                if (nested != null) return nested;
            }

            return contenedor.CanSelect ? contenedor : null;
        }

        private void Abrir(string titulo, Form form, string nodeName)
        {
            var tab = new TabPage(titulo) { Name = nodeName, BackColor = Color.White };
            form.TopLevel        = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Dock            = DockStyle.Fill;
            form.FormClosed += (s, ev) =>
            {
                for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
                    if (tabControl.TabPages[i].Name == nodeName)
                    { tabControl.TabPages.RemoveAt(i); break; }
            };

            tab.Padding = new Padding(0);
            tab.Margin = new Padding(0);
            tab.UseVisualStyleBackColor = true;
            tab.BackColor = Color.White;
            form.Margin = new Padding(0);
            form.Padding = new Padding(0);

            tab.SuspendLayout();
            form.SuspendLayout();
            try
            {
                tab.Controls.Add(form);
                tabControl.TabPages.Add(tab);
                tabControl.SelectedTab = tab;
                form.Show();
                AjustarHostTabs();
                // Disparar manualmente porque SelectedIndexChanged se dispara antes de Show()
                TabControl_SelectedIndexChanged(tabControl, EventArgs.Empty);
            }
            finally
            {
                form.ResumeLayout(true);
                tab.ResumeLayout(true);
            }

            BeginInvoke(new Action(() =>
            {
                AjustarHostTabs();
                tab.PerformLayout();
                form.PerformLayout();
                TransferirFocoAlFormularioAbierto(form);
            }));
        }

        // Alias para no romper el evento ya suscrito en el Designer
        private void AbrirFormEnPestana(string titulo, Form form, string nodeName)
            => Abrir(titulo, form, nodeName);

        private void tabControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                for (int i = 0; i < tabControl.TabCount; i++)
                    if (tabControl.GetTabRect(i).Contains(e.Location)) { CerrarPestana(i); break; }
            }
            else if (e.Button == MouseButtons.Right)
            {
                for (int i = 0; i < tabControl.TabCount; i++)
                {
                    if (!tabControl.GetTabRect(i).Contains(e.Location)) continue;
                    int idx = i;
                    var menu = new ContextMenuStrip();
                    menu.Items.Add("X Cerrar",       null, (s, a) => CerrarPestana(idx));
                    menu.Items.Add("X Cerrar todas", null, (s, a) => CerrarTodasLasPestanas());
                    menu.Items.Add("X Cerrar otras", null, (s, a) => CerrarOtrasPestanas(idx));
                    menu.Show(tabControl, e.Location);
                    break;
                }
            }
        }

        private void CerrarTodasLasPestanas()
        {
            while (tabControl.TabPages.Count > 0) CerrarPestana(0);
        }

        private sealed record RibbonButtonState(SoproIconType? IconType, bool Active, int IconSize, SoproIconButtonOptions? Options);

        private void CerrarOtrasPestanas(int index)
        {
            for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
                if (i != index) CerrarPestana(i);
        }

        private void CerrarPestana(int index)
        {
            if (index < 0 || index >= tabControl.TabPages.Count) return;
            var tab = tabControl.TabPages[index];
            if (tab.Controls.Count > 0 && tab.Controls[0] is Form f) f.Close();
            tabControl.TabPages.RemoveAt(index);
        }
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static void SuspendDrawing(Control control)
        {
            if (control == null || !control.IsHandleCreated)
                return;

            SendMessage(control.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        private static void ResumeDrawing(Control control)
        {
            if (control == null || !control.IsHandleCreated)
                return;

            SendMessage(control.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            control.Invalidate(true);
            control.Update();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.B))
            {
                AbrirBuscadorFlotante();
                return true;
            }

            if (keyData == Keys.F3 && _formBuscarEnGrid != null && !_formBuscarEnGrid.IsDisposed && _formBuscarEnGrid.Visible)
            {
                _formBuscarEnGrid.BuscarSiguiente();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void AbrirBuscadorFlotante()
        {
            var controlActivo = tabControl.SelectedTab?.Controls.Count > 0 ? tabControl.SelectedTab.Controls[0] : null;
            var buscable = controlActivo as IBusquedaGrid;
            var grid = buscable?.GridBusqueda;
            if (grid == null)
            {
                MessageBox.Show("El módulo activo no tiene un grid para búsqueda.", "Buscar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_formBuscarEnGrid == null || _formBuscarEnGrid.IsDisposed)
            {
                _formBuscarEnGrid = new FormBuscarEnGrid(grid);
            }
            else if (!ReferenceEquals(grid, ObtenerGridDeBusquedaActual()))
            {
                try { _formBuscarEnGrid.Close(); } catch { }
                _formBuscarEnGrid = new FormBuscarEnGrid(grid);
            }

            if (!_formBuscarEnGrid.Visible)
            {
                var p = PointToScreen(new Point(Math.Max(0, Width - 460), 120));
                _formBuscarEnGrid.StartPosition = FormStartPosition.CenterScreen;
                _formBuscarEnGrid.Location = p;
                _formBuscarEnGrid.Show(this);
            }
            else
            {
                _formBuscarEnGrid.BringToFront();
                _formBuscarEnGrid.Focus();
            }
        }

        private DataGridView ObtenerGridDeBusquedaActual()
        {
            var controlActivo = tabControl.SelectedTab?.Controls.Count > 0 ? tabControl.SelectedTab.Controls[0] : null;
            return (controlActivo as IBusquedaGrid)?.GridBusqueda;
        }

    }
}
