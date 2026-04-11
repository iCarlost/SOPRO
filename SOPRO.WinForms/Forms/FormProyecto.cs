using System;
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
    public partial class FormProyecto : Form
    {
        private SOPROContext _context;
        private readonly Proyecto _proyecto;

        private bool _cargandoRibbon = false;
        private IGridFormato _formActivo = null;
        private IRecalculable _formActivoRecalculable = null;
        private IBusquedaGrid _formActivoBuscable = null;
        private IConsolidacionInsumos _formActivoConsolidable = null;
        private bool _restaurandoEstadoArbol = false;
        private const int SidebarExpandedWidth = 250;
        private const int SidebarCollapsedWidth = 60;
        private const int SidebarExpandedMaxWidth = 420;
        private readonly ToolTip _treeMenuToolTip = new ToolTip();
        private string _ultimoTooltipNodo = string.Empty;
        private FormBuscarEnGrid _formBuscarEnGrid = null;
        private bool _ajustandoBarraLateral = false;
        private Color _colorMuestraFondoRibbon = Color.White;
        private Color _colorMuestraTextoRibbon = Color.Black;
        private bool _ajusteHostTabsPendiente = false;

        public FormProyecto(SOPROContext context, Proyecto proyecto)
        {
            _context  = context  ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));
            InitializeComponent();
            // KeyPreview permite interceptar F9/F10 antes que los controles hijos
            this.KeyPreview = true;
            this.KeyDown += FormProyecto_KeyDown;
        }

        private void FormProyecto_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F9 && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnRecalcularRibbon_Click(sender, EventArgs.Empty);
                return;
            }
            if (e.KeyCode == Keys.F10 && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnDepurarRibbon_Click(sender, EventArgs.Empty);
            }
        }

        private void FormProyecto_Load(object sender, EventArgs e)
        {
            lblProyecto.Text = $"📁 {_proyecto.Nombre} | {_proyecto.Ubicacion}";
            InicializarRibbon();
            DesactivarRibbon();
            btnDepurarRibbon.Enabled = true;
            btnConsolidarInsumos.Enabled = false;
            HabilitarControlesFormato(false);

            treeMenu.AfterExpand += treeMenu_AfterExpand;
            treeMenu.AfterCollapse += treeMenu_AfterCollapse;
            treeMenu.MouseMove += treeMenu_MouseMove;
            treeMenu.MouseLeave += treeMenu_MouseLeave;

            _treeMenuToolTip.InitialDelay = 150;
            _treeMenuToolTip.ReshowDelay = 100;
            _treeMenuToolTip.AutoPopDelay = 6000;
            _treeMenuToolTip.ShowAlways = true;

            tabControl.Padding = new Point(0, 0);
            tabControl.SizeMode = TabSizeMode.Normal;
            tabControl.Resize += (_, __) =>
            {
                if (_ajustandoBarraLateral)
                    ProgramarAjusteHostTabsDiferido();
                else
                    AjustarHostTabs();
            };
            Resize += (_, __) =>
            {
                if (_ajustandoBarraLateral)
                    ProgramarAjusteHostTabsDiferido();
                else
                    AjustarHostTabs();
            };

            RestaurarEstadoBarraYLateral();
            AjustarHostTabs();
        }


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
        // =====================================================================
        // RIBBON
        // =====================================================================

        private void InicializarRibbon()
        {
            // Cargar fuentes del sistema
            cboFuente.Items.Clear();
            using var familias = new System.Drawing.Text.InstalledFontCollection();
            foreach (var f in familias.Families.OrderBy(x => x.Name))
                cboFuente.Items.Add(f.Name);

            // Eventos de controles del ribbon
            cboFuente.SelectedIndexChanged += Ribbon_Changed;
            nudTamano.ValueChanged          += Ribbon_Changed;
            btnNegrita.Click                += Ribbon_ToggleEstilo;
            btnCursiva.Click                += Ribbon_ToggleEstilo;
            btnColorFondo.Click             += BtnColorFondo_Click;
            btnColorTexto.Click             += BtnColorTexto_Click;

            ConfigurarIconosRibbon();
            AplicarTemaCompletoRibbon();
            panelRibbon.Resize += (_, __) => AjustarLayoutAccionesRibbon();
            btnAlinIzq.Click += (s, ev) => SetAlineacion(AlineacionColumna.Izquierda);
            btnAlinCen.Click += (s, ev) => SetAlineacion(AlineacionColumna.Centro);
            btnAlinDer.Click += (s, ev) => SetAlineacion(AlineacionColumna.Derecha);
            btnAlinJus.Click += (s, ev) => SetAlineacionVertical(DataGridViewContentAlignment.TopLeft);
            btnAlinMed.Click += (s, ev) => SetAlineacionVertical(DataGridViewContentAlignment.MiddleLeft);
            btnAlinAba.Click += (s, ev) => SetAlineacionVertical(DataGridViewContentAlignment.BottomLeft);

            // Detectar cambio de tab
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Desuscribir del form anterior
            if (_formActivo != null)
            {
                _formActivo.ColumnaSeleccionadaCambiada -= FormActivo_ColumnaSeleccionadaCambiada;
                _formActivo = null;
                _formActivoRecalculable = null;
                _formActivoBuscable = null;
                if (_formActivoConsolidable != null) _formActivoConsolidable.EstadoConsolidacionCambiado -= FormActivoConsolidacionCambiada;
                _formActivoConsolidable = null;
            }

            // Buscar si el form activo implementa IGridFormato
            if (tabControl.SelectedTab?.Controls.Count > 0
                && tabControl.SelectedTab.Controls[0] is IGridFormato gf)
            {
                _formActivo = gf;
                _formActivoRecalculable = tabControl.SelectedTab.Controls[0] as IRecalculable;
                _formActivoBuscable = tabControl.SelectedTab.Controls[0] as IBusquedaGrid;
                _formActivoConsolidable = tabControl.SelectedTab.Controls[0] as IConsolidacionInsumos;
                if (_formActivoConsolidable != null) _formActivoConsolidable.EstadoConsolidacionCambiado += FormActivoConsolidacionCambiada;
                _formActivo.ColumnaSeleccionadaCambiada += FormActivo_ColumnaSeleccionadaCambiada;
                btnExcelRibbon.Enabled = true;
                btnPdfRibbon.Enabled = true;
                btnBuscarRibbon.Enabled = _formActivoBuscable?.GridBusqueda != null;
                btnWrapRibbon.Enabled = _formActivo.GridPrincipal != null;
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                AplicarTemaCompletoRibbon();

                // Si es el presupuesto, recalcular P.U. e importes con la configuración actual
                if (_formActivo is FormPresupuesto fp)
                    fp.RefrescarPreciosDesdeDB();

                if (_formActivo.GridPrincipal != null)
                    FormatoHelper.AjustarAutoAlturaFilas(_formActivo.GridPrincipal);

                if (_formActivo.ColumnaSeleccionada != null)
                {
                    HabilitarControlesFormato(true);
                    CargarColumnaEnRibbon(_formActivo.ColumnaSeleccionada);
                }
                else
                {
                    _cargandoRibbon = true;
                    panelRibbon.Enabled = true;
                    HabilitarControlesFormato(false);
                    lblColumna.Text = tabControl.SelectedTab.Controls[0] is FormFSR
                        ? "Factor de Salario Real"
                        : "Clic en encabezado de columna para formatear";
                    lblColumna.ForeColor = System.Drawing.Color.Gray;
                    _cargandoRibbon = false;
                }
            }
            else
            {
                DesactivarRibbon();
            }
        }

        private void FormActivo_ColumnaSeleccionadaCambiada(object sender, EventArgs e)
        {
            if (_formActivo?.ColumnaSeleccionada != null)
            {
                HabilitarControlesFormato(true);
                CargarColumnaEnRibbon(_formActivo.ColumnaSeleccionada);
            }
            else if (_formActivo != null)
            {
                _cargandoRibbon = true;
                panelRibbon.Enabled = true;
                btnExcelRibbon.Enabled = true;
                btnPdfRibbon.Enabled = true;
                btnBuscarRibbon.Enabled = _formActivoBuscable?.GridBusqueda != null;
                btnWrapRibbon.Enabled = _formActivo.GridPrincipal != null;
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                HabilitarControlesFormato(false);
                lblColumna.Text = tabControl.SelectedTab?.Controls.Count > 0 && tabControl.SelectedTab.Controls[0] is FormFSR
                    ? "Factor de Salario Real"
                    : "Clic en encabezado de columna para formatear";
                lblColumna.ForeColor = Color.Gray;
                _cargandoRibbon = false;
            }
            else
            {
                DesactivarRibbon();
            }
        }


        private void FormActivoConsolidacionCambiada(object sender, EventArgs e)
        {
            btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
        }

        private void btnConsolidarInsumos_Click(object sender, EventArgs e)
        {
            if (_formActivoConsolidable == null)
                return;

            _formActivoConsolidable.EjecutarConsolidacion();
            btnConsolidarInsumos.Enabled = _formActivoConsolidable.ConsolidacionDisponible;
        }

        private void ConfigurarIconosRibbon()
        {
            ConfigurarBotonIcono(btnBuscarRibbon, "Buscar", SoproIconType.Buscar, 16, showText: true);
            ConfigurarBotonIcono(btnExcelRibbon, string.Empty, SoproIconType.Excel, 16, showText: false);
            ConfigurarBotonIcono(btnPdfRibbon, string.Empty, SoproIconType.Pdf, 16, showText: false);
            ConfigurarBotonIcono(btnWrapRibbon, "Ajustar", SoproIconType.AjustarTexto, 16, showText: true);
            ConfigurarBotonIcono(btnRecalcularRibbon, "Recalcular", SoproIconType.Recalcular, 16, showText: true);
            ConfigurarBotonIcono(btnDepurarRibbon, "Depurar", SoproIconType.Depurar, 16, showText: true);
            ConfigurarBotonIcono(btnAplicarATodas, "Aplicar", SoproIconType.AplicarATodas, 16, showText: true);
            ConfigurarBotonIcono(btnConsolidarInsumos, "Consolidar", SoproIconType.Consolidar, 16, showText: true);

            ConfigurarBotonIcono(btnAlinIzq, string.Empty, SoproIconType.AlinearIzquierda, 16, showText: false);
            ConfigurarBotonIcono(btnAlinCen, string.Empty, SoproIconType.AlinearCentro, 16, showText: false);
            ConfigurarBotonIcono(btnAlinDer, string.Empty, SoproIconType.AlinearDerecha, 16, showText: false);
            ConfigurarBotonIcono(btnAlinJus, string.Empty, SoproIconType.AlinearArriba, 16, showText: false);
            ConfigurarBotonIcono(btnAlinMed, string.Empty, SoproIconType.AlinearMedio, 16, showText: false);
            ConfigurarBotonIcono(btnAlinAba, string.Empty, SoproIconType.AlinearAbajo, 16, showText: false);

        }

        private void ConfigurarBotonIcono(Button btn, string text, SoproIconType iconType, int iconSize, bool showText)
        {
            if (btn == null) return;

            if (btn is SoproButton soproButton)
            {
                if (string.IsNullOrWhiteSpace(soproButton.Text))
                    soproButton.Text = text;

                if (soproButton.SoproIcon == null)
                    soproButton.SoproIcon = iconType;

                if (soproButton.SoproIconSize <= 0)
                    soproButton.SoproIconSize = iconSize;

                if (!showText && soproButton.SoproShowText)
                    soproButton.SoproShowText = false;

                if (!showText && soproButton.SoproAutoSizeToContent)
                    soproButton.SoproAutoSizeToContent = false;
            }
            else
            {
                btn.Text = text;
                var options = showText
                    ? SoproIconButtonOptions.ForRibbonText(iconType, iconSize: iconSize)
                    : SoproIconButtonOptions.ForRibbonGlyph(iconType, iconSize);
                SoproRibbonButtonStyler.Apply(btn, options, btn.Enabled ? btn.ForeColor : Color.FromArgb(120, 120, 120));
            }

            var stateIconType = btn is SoproButton sb && sb.SoproIcon.HasValue ? sb.SoproIcon.Value : iconType;
            var stateIconSize = btn is SoproButton sb2 && sb2.SoproIconSize > 0 ? sb2.SoproIconSize : iconSize;
            btn.Tag = new RibbonButtonState(stateIconType, false, stateIconSize, null);
        }

        private bool DebeAplicarLayoutAutomaticoRibbonAcciones() => false;

        private void AjustarLayoutAccionesRibbon()
        {
            if (panelRibbon == null || !DebeAplicarLayoutAutomaticoRibbonAcciones())
                return;

            int topRowY = btnBuscarRibbon.Top;
            int bottomRowY = btnDepurarRibbon.Top;
            int x = lblSepGlobal.Right + SoproUiMetrics.RibbonSeparatorGap;

            ReubicarBotonRibbon(btnBuscarRibbon, x, topRowY);
            x = btnBuscarRibbon.Right + SoproUiMetrics.RibbonButtonHorizontalGap;

            ReubicarBotonRibbon(btnWrapRibbon, x, topRowY);
            x = btnWrapRibbon.Right + SoproUiMetrics.RibbonSeparatorGap;

            label1.Location = new Point(x, label1.Top);
            x = label1.Right + SoproUiMetrics.RibbonSeparatorGap;

            int stackWidth = Math.Max(btnRecalcularRibbon.Width, btnDepurarRibbon.Width);
            ReubicarBotonRibbon(btnRecalcularRibbon, x, topRowY, stackWidth);
            ReubicarBotonRibbon(btnDepurarRibbon, x, bottomRowY, stackWidth);
            x += stackWidth + SoproUiMetrics.RibbonButtonHorizontalGap;

            ReubicarBotonRibbon(btnAplicarATodas, x, topRowY);
            x = btnAplicarATodas.Right + SoproUiMetrics.RibbonSeparatorGap;

            lblSepReporte.Location = new Point(x, lblSepReporte.Top);
            x = lblSepReporte.Right + 20;

            label2.Location = new Point(x, label2.Top);
            x = label2.Right + 7;

            btnExcelRibbon.Location = new Point(x, btnExcelRibbon.Top);
        }

        private void ReubicarBotonRibbon(Button button, int x, int y, int? forcedWidth = null)
        {
            if (button == null)
                return;

            if (forcedWidth.HasValue)
                button.Width = forcedWidth.Value;

            button.Location = new Point(x, y);
        }

        private RibbonButtonState GetRibbonButtonState(Button btn)
        {
            if (btn?.Tag is RibbonButtonState state)
                return state;

            if (btn?.Tag is bool active)
                return new RibbonButtonState(null, active, 16, null);

            return new RibbonButtonState(null, false, 16, null);
        }

        private Color RibbonBaseColor => panelTop?.BackColor ?? Color.FromArgb(51, 51, 76);
        private Color RibbonActiveColor => ControlPaint.Light(RibbonBaseColor);

        private bool EstaBotonActivo(Button btn)
        {
            return GetRibbonButtonState(btn).Active;
        }

        private void AplicarTemaBotonRibbon(Button btn, bool activo)
        {
            if (btn == null) return;

            var state = GetRibbonButtonState(btn) with { Active = activo };
            btn.Tag = state;
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.BorderColor = RibbonBaseColor;
            btn.ForeColor = activo ? Color.White : Color.Silver;
            btn.BackColor = activo ? RibbonActiveColor : RibbonBaseColor;
            btn.FlatAppearance.MouseOverBackColor = activo
                ? ControlPaint.LightLight(RibbonActiveColor)
                : ControlPaint.Light(RibbonBaseColor);
            btn.FlatAppearance.MouseDownBackColor = activo
                ? ControlPaint.Dark(RibbonActiveColor)
                : ControlPaint.Dark(RibbonBaseColor);

            AplicarIconoBotonRibbon(btn);
        }

        private void AplicarTemaBotonColor(Button btn, Color sampleColor, string texto)
        {
            if (btn == null) return;

            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.BorderColor = RibbonBaseColor;
            btn.Text = texto;
            btn.BackColor = RibbonBaseColor;
            btn.ForeColor = sampleColor;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(RibbonBaseColor);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(RibbonBaseColor);
            btn.Image = null;
        }

        private void AplicarTemaCompletoRibbon()
        {
            AplicarTemaBotonRibbon(btnNegrita, EstaBotonActivo(btnNegrita));
            AplicarTemaBotonRibbon(btnCursiva, EstaBotonActivo(btnCursiva));
            AplicarTemaBotonRibbon(btnWrapRibbon, EstaBotonActivo(btnWrapRibbon));

            AplicarTemaBotonRibbon(btnAlinIzq, EstaBotonActivo(btnAlinIzq));
            AplicarTemaBotonRibbon(btnAlinCen, EstaBotonActivo(btnAlinCen));
            AplicarTemaBotonRibbon(btnAlinDer, EstaBotonActivo(btnAlinDer));
            AplicarTemaBotonRibbon(btnAlinJus, EstaBotonActivo(btnAlinJus));
            AplicarTemaBotonRibbon(btnAlinMed, EstaBotonActivo(btnAlinMed));
            AplicarTemaBotonRibbon(btnAlinAba, EstaBotonActivo(btnAlinAba));

            AplicarTemaBotonRibbon(btnBuscarRibbon, false);
            AplicarTemaBotonRibbon(btnAplicarATodas, false);
            AplicarTemaBotonRibbon(btnConsolidarInsumos, false);
            AplicarTemaBotonRibbon(btnExcelRibbon, false);
            AplicarTemaBotonRibbon(btnPdfRibbon, false);
            AplicarTemaBotonRibbon(btnRecalcularRibbon, false);
            AplicarTemaBotonRibbon(btnDepurarRibbon, false);


            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, '■'.ToString());
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, 'A'.ToString());
        }

        private void CargarColumnaEnRibbon(ColumnaPersonalizada col)
        {
            _cargandoRibbon = true;

            int idx = cboFuente.FindStringExact(col.NombreFuente ?? "Segoe UI");
            cboFuente.SelectedIndex = idx >= 0 ? idx : 0;
            nudTamano.Value = col.TamanoFuente >= 6 ? col.TamanoFuente : 9;

            AplicarTemaBotonRibbon(btnNegrita, col.Negrita);
            AplicarTemaBotonRibbon(btnCursiva, col.Cursiva);

            ActualizarBotonesAlin(col.Alineacion);

            _colorMuestraFondoRibbon = TryColor(col.ColorFondo, Color.White);
            _colorMuestraTextoRibbon = TryColor(col.ColorFuente, Color.Black);
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
            AplicarTemaBotonRibbon(btnWrapRibbon, col.WrapTexto);
            ActualizarBotonesAlinVertical(FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical));

            lblColumna.Text      = col.Nombre;
            lblColumna.ForeColor = Color.White;
            panelRibbon.Enabled  = true;

            _cargandoRibbon = false;
        }

        private void DesactivarRibbon()
        {
            _cargandoRibbon = true;
            panelRibbon.Enabled = true;
            btnExcelRibbon.Enabled = false;
            btnPdfRibbon.Enabled = false;
            btnBuscarRibbon.Enabled = false;
            btnWrapRibbon.Enabled = false;
            btnRecalcularRibbon.Enabled = false;
            btnDepurarRibbon.Enabled = true;
            btnConsolidarInsumos.Enabled = false;
            HabilitarControlesFormato(false);
            lblColumna.Text      = "-- sin seleccion --";
            lblColumna.ForeColor = Color.Gray;
            _cargandoRibbon = false;
        }

        private void HabilitarControlesFormato(bool enabled)
        {
            cboFuente.Enabled = enabled;
            nudTamano.Enabled = enabled;
            btnNegrita.Enabled = enabled;
            btnCursiva.Enabled = enabled;
            btnAlinIzq.Enabled = enabled;
            btnAlinCen.Enabled = enabled;
            btnAlinDer.Enabled = enabled;
            btnAlinJus.Enabled = enabled;
            btnAlinMed.Enabled = enabled;
            btnAlinAba.Enabled = enabled;
            btnColorFondo.Enabled = enabled;
            btnColorTexto.Enabled = enabled;
            btnAplicarATodas.Enabled = enabled;
            AplicarTemaCompletoRibbon();
        }

        private void AplicarRibbon()
        {
            if (_cargandoRibbon || _formActivo?.ColumnaSeleccionada == null) return;
            _formActivo.AplicarFormato(BuildFmt());
            AplicarAlineacionVerticalSeleccionActual();
        }

        private ColumnaPersonalizada BuildFmt() => new ColumnaPersonalizada
        {
            NombreFuente = cboFuente.SelectedItem?.ToString() ?? "Segoe UI",
            TamanoFuente = (int)nudTamano.Value,
            Negrita      = EstaBotonActivo(btnNegrita),
            Cursiva      = EstaBotonActivo(btnCursiva),
            Alineacion   = ObtenerAlin(),
            ColorFondo   = HexColor(_colorMuestraFondoRibbon),
            ColorFuente  = HexColor(_colorMuestraTextoRibbon),
            WrapTexto    = EstaBotonActivo(btnWrapRibbon),
            AlineacionVertical = EstaBotonActivo(btnAlinAba) ? 2 : EstaBotonActivo(btnAlinJus) ? 0 : 1,
        };

        // Eventos de controles del ribbon
        private void Ribbon_Changed(object sender, EventArgs e) => AplicarRibbon();

        private void Ribbon_ToggleEstilo(object sender, EventArgs e)
        {
            if (_cargandoRibbon) return;
            var btn = (Button)sender;
            var nuevoEstado = !EstaBotonActivo(btn);
            AplicarTemaBotonRibbon(btn, nuevoEstado);
            AplicarRibbon();
        }

        private void SetAlineacion(AlineacionColumna alin)
        {
            if (_cargandoRibbon) return;
            ActualizarBotonesAlin(alin);
            AplicarRibbon();
        }

        private void SetAlineacionVertical(DataGridViewContentAlignment alineacionBase)
        {
            if (_cargandoRibbon || _formActivo?.GridPrincipal == null) return;

            var grid = _formActivo.GridPrincipal;
            var col = ObtenerColumnaSeleccionadaGrid();
            if (col == null)
            {
                MessageBox.Show("Selecciona primero una columna desde su encabezado.",
                    "Sin columna seleccionada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var actual = col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet
                ? DataGridViewContentAlignment.MiddleLeft
                : col.DefaultCellStyle.Alignment;

            var horizontal = ObtenerHorizontal(actual);
            var nueva = CombinarAlineacion(alineacionBase, horizontal);

            col.DefaultCellStyle.Alignment = nueva;
            if (grid.Columns[col.Index].DefaultCellStyle != null)
                grid.Columns[col.Index].DefaultCellStyle.Alignment = nueva;

            ActualizarBotonesAlinVertical(nueva);
            _formActivo.AplicarFormato(BuildFmt());
            if (col.DefaultCellStyle.WrapMode == DataGridViewTriState.True)
                AjustarAutoAlturaFilas(grid);
            grid.Invalidate();
        }

        private void btnBuscarRibbon_Click(object sender, EventArgs e)
        {
            AbrirBuscadorFlotante();
        }

        private void AplicarIconoBotonRibbon(Button btn)
        {
            var state = GetRibbonButtonState(btn);
            if (state.IconType == null)
            {
                btn.Image = null;
                return;
            }

            if (btn is SoproButton soproButton)
            {
                soproButton.SoproIcon = state.IconType.Value;
                soproButton.SoproIconSize = state.IconSize;
                return;
            }

            var colorIcono = btn.Enabled ? btn.ForeColor : Color.FromArgb(120, 120, 120);
            var options = state.Options ?? SoproIconButtonOptions.ForRibbonText(state.IconType.Value, iconSize: state.IconSize);
            SoproRibbonButtonStyler.Apply(btn, options, colorIcono);
        }

        private void BtnColorFondo_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _colorMuestraFondoRibbon };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _colorMuestraFondoRibbon = dlg.Color;
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
            AplicarRibbon();
        }

        private void BtnColorTexto_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _colorMuestraTextoRibbon };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _colorMuestraTextoRibbon = dlg.Color;
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
            AplicarRibbon();
        }

        private void btnAplicarATodas_Click(object sender, EventArgs e)
        {
            if (_formActivo == null) return;
            _formActivo.AplicarFormatoGlobal(BuildFmt());
            FormatoHelper.AjustarAutoAlturaFilas(_formActivo.GridPrincipal);
            MessageBox.Show(
                "Fuente, tamano, estilo, alineacion y color de texto\naplicados a todas las columnas.\n(Color de fondo no modificado)",
                "Aplicado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void btnRecalcularRibbon_Click(object sender, EventArgs e)
        {
            await RecalcularTodoElProyectoAsync();
        }

        private async Task RecalcularTodoElProyectoAsync()
        {
            var svc = new SOPRO.Application.Services.RecalculoGlobalService();
            var proyId = _proyecto.Id;
            var tabActiva = tabControl.SelectedTab;
            var formActivoAntes = tabActiva?.Controls.Count > 0 ? tabActiva.Controls[0] as Form : null;
            var controlConFocoAntes = ObtenerControlConFoco(formActivoAntes) ?? ObtenerControlConFoco(this);

            // Deshabilitar solo el botón para evitar doble ejecución sin perder foco del formulario activo.
            btnRecalcularRibbon.Enabled = false;
            Cursor = Cursors.WaitCursor;

            SOPRO.Application.Services.RecalculoGlobalResultado resultado = null;
            Exception error = null;

            try
            {
                resultado = await Task.Run(() => svc.Ejecutar(_context, proyId));
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                Cursor = Cursors.Default;
                btnRecalcularRibbon.Enabled = true;
            }

            if (error != null)
            {
                RestaurarFocoTrasRecalculo(tabActiva, formActivoAntes, controlConFocoAntes);
                MessageBox.Show($"Error en el recálculo global:{error.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Refrescar todos los tabs abiertos que implementen IRecalculable
            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Controls.Count > 0 && tab.Controls[0] is IRecalculable form)
                {
                    try { form.RecalcularTodo(); }
                    catch { /* no interrumpir por un tab */ }
                }
            }

            // Ajustar altura de filas del form activo
            if (_formActivo?.GridPrincipal != null)
                FormatoHelper.AjustarAutoAlturaFilas(_formActivo.GridPrincipal);

            RestaurarFocoTrasRecalculo(tabActiva, formActivoAntes, controlConFocoAntes);

            // Mostrar resultado discretamente como tooltip debajo del botón recalcular
            var tip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 0, ReshowDelay = 0 };
            // Mostrar debajo del botón (y = altura del botón + margen)
            tip.Show(resultado.ResumenTexto, btnRecalcularRibbon,
                0, btnRecalcularRibbon.Height + 4, 5000);
        }

        private void btnDepurarRibbon_Click(object sender, EventArgs e)
        {
            try
            {
                var preview = DepuracionService.ObtenerPreview(_context, _proyecto.Id);
                if (preview.TotalCandidatas <= 0)
                {
                    MessageBox.Show(
                        "No se encontraron matrices, auxiliares ni insumos individuales candidatos a depuración.",
                        "Depurar",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var msg =
                    $"Se eliminarán:\n\n" +
                    $"- {preview.MatricesApuCandidatas} matriz(es) APU no asignadas a conceptos\n" +
                    $"- {preview.MatricesBasicasCandidatas} básico(s) sin uso\n" +
                    $"- {preview.CuadrillasCandidatas} cuadrilla(s) sin uso\n" +
                    $"- {preview.MaterialesCandidatos} material(es) sin uso\n" +
                    $"- {preview.ManoDeObraCandidata} mano(s) de obra sin uso\n" +
                    $"- {preview.HerramientasCandidatas} herramienta(s) sin uso\n" +
                    $"- {preview.MaquinariaCandidata} maquinaria(s) sin uso\n\n" +
                    "Solo se eliminarán si no tienen referencias vigentes.\n" +
                    "Los componentes que sigan usándose en otras matrices permanecerán vivos.\n\n" +
                    "¿Deseas continuar?";

                if (MessageBox.Show(msg, "Depurar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                Cursor = Cursors.WaitCursor;

                var resultado = DepuracionService.Ejecutar(_context, _proyecto.Id);
                RefrescarFormulariosTrasDepuracion();

                MessageBox.Show(
                    $"Depuración completada:\n\n" +
                    $"- APU eliminadas: {resultado.MatricesApuEliminadas}\n" +
                    $"- Básicos eliminados: {resultado.MatricesBasicasEliminadas}\n" +
                    $"- Cuadrillas eliminadas: {resultado.CuadrillasEliminadas}\n" +
                    $"- Materiales eliminados: {resultado.MaterialesEliminados}\n" +
                    $"- Mano de obra eliminada: {resultado.ManoDeObraEliminada}\n" +
                    $"- Herramientas eliminadas: {resultado.HerramientasEliminadas}\n" +
                    $"- Maquinaria eliminada: {resultado.MaquinariaEliminada}",
                    "Depurar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al depurar:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RefrescarFormulariosTrasDepuracion()
        {
            foreach (Form f in System.Windows.Forms.Application.OpenForms)
            {
                if (f is not FormProyecto proyecto) continue;

                foreach (TabPage tab in proyecto.tabControl.TabPages)
                {
                    if (tab.Controls.Count == 0) continue;

                    switch (tab.Controls[0])
                    {
                        case FormMatrices fm:
                            fm.RecargarMatrices();
                            break;
                        case FormPresupuesto fp:
                            fp.RefrescarPreciosDesdeDB();
                            break;
                        case FormCatalogoMateriales fmat:
                            fmat.RecargarCatalogo();
                            break;
                        case FormCatalogoManoObra fmo:
                            fmo.RecargarCatalogo();
                            break;
                        case FormCatalogoHerramientas fh:
                            fh.RecargarCatalogo();
                            break;
                        case FormCatalogoMaquinaria fmq:
                            fmq.RecargarCatalogo();
                            break;
                    }
                }
            }
        }

        private void btnExcelRibbon_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                if (_formActivo == null)
                {
                    MessageBox.Show("Abre un modulo primero para generar su reporte.",
                        "Sin modulo activo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _formActivo.GenerarReporteExcel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el reporte:{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void btnPdfRibbon_Click(object sender, EventArgs e)
        {
            try
            {
                if (tabControl.SelectedTab == null || tabControl.SelectedTab.Controls.Count == 0)
                    return;

                var formActivo = tabControl.SelectedTab.Controls[0];
                if (formActivo is FormPresupuesto frmPresupuesto)
                {
                    using var opciones = new FormExportarReporte(frmPresupuesto.Contexto, frmPresupuesto.ProyectoActual, exportarPdf: true);
                    opciones.ShowDialog(this);
                    return;
                }

                if (formActivo is FormExplosionInsumos frmExplosion)
                {
                    frmExplosion.GenerarPdfExplosion();
                    return;
                }

                if (formActivo is FormIndirectos frmIndirectos)
                {
                    frmIndirectos.GenerarPdfIndirectos();
                    return;
                }

                if (formActivo is FormFSR frmFsr)
                {
                    frmFsr.GenerarPdfFSR();
                    return;
                }

                if (formActivo is FormFinanciamiento frmFinanciamiento)
                {
                    frmFinanciamiento.GenerarPdfFinanciamiento();
                    return;
                }

                if (formActivo is FormUtilidad frmUtilidad)
                {
                    frmUtilidad.GenerarPdfUtilidad();
                    return;
                }

                if (formActivo is FormProgramaObra frmProgramaObra)
                {
                    frmProgramaObra.GenerarPdfProgramaObra();
                    return;
                }

                if (formActivo is FormProgramaInsumos frmProgramaInsumos)
                {
                    frmProgramaInsumos.GenerarPdfProgramaInsumos();
                    return;
                }

                if (formActivo is FormCatalogoMateriales frmCatalogoMateriales)
                {
                    frmCatalogoMateriales.GenerarPdfCatalogoMateriales();
                    return;
                }

                if (formActivo is FormCatalogoManoObra frmCatalogoManoObra)
                {
                    frmCatalogoManoObra.GenerarPdfCatalogoManoObra();
                    return;
                }

                if (formActivo is FormCatalogoHerramientas frmCatalogoHerramientas)
                {
                    frmCatalogoHerramientas.GenerarPdfCatalogoHerramientas();
                    return;
                }

                if (formActivo is FormCatalogoMaquinaria frmCatalogoMaquinaria)
                {
                    frmCatalogoMaquinaria.GenerarPdfCostoHorario();
                    return;
                }

                if (formActivo is FormMatrices frmMatrices)
                {
                    frmMatrices.GenerarPdfCatalogoMatrices();
                    return;
                }

                MessageBox.Show("La exportación PDF aún no está disponible para este módulo.", "PDF",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No fue posible exportar a PDF:\n{ex.Message}", "PDF",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnWrapRibbon_Click(object sender, EventArgs e)
        {
            if (_formActivo?.GridPrincipal == null)
            {
                MessageBox.Show("Abre un modulo primero.",
                    "Sin modulo activo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var grid = _formActivo.GridPrincipal;
            var nombreColumna = _formActivo.ColumnaSeleccionada?.Nombre;
            if (string.IsNullOrWhiteSpace(nombreColumna))
            {
                MessageBox.Show("Selecciona primero una columna desde su encabezado.",
                    "Sin columna seleccionada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var col = grid.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.HeaderText, nombreColumna, StringComparison.OrdinalIgnoreCase));

            if (col == null)
            {
                MessageBox.Show("No se encontró la columna seleccionada en el grid activo.",
                    "Columna no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;

                var activar = col.DefaultCellStyle.WrapMode != DataGridViewTriState.True;
                col.DefaultCellStyle.WrapMode = activar ? DataGridViewTriState.True : DataGridViewTriState.False;

                if (activar && (col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet ||
                                col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.MiddleCenter))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                }

                AjustarAutoAlturaFilas(grid);
                AplicarTemaBotonRibbon(btnWrapRibbon, activar);
                _formActivo.AplicarFormato(BuildFmt());
                grid.Invalidate();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private DataGridViewColumn ObtenerColumnaSeleccionadaGrid()
        {
            if (_formActivo?.GridPrincipal == null || _formActivo?.ColumnaSeleccionada == null) return null;

            var nombreColumna = _formActivo.ColumnaSeleccionada.Nombre;
            return _formActivo.GridPrincipal.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.HeaderText, nombreColumna, StringComparison.OrdinalIgnoreCase));
        }

        private void AplicarAlineacionVerticalSeleccionActual()
        {
            if (_formActivo?.GridPrincipal == null) return;

            var col = ObtenerColumnaSeleccionadaGrid();
            if (col == null) return;

            var verticalActual = ObtenerAlineacionVerticalBoton();
            var horizontalActual = ObtenerHorizontal(col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet
                ? DataGridViewContentAlignment.MiddleLeft
                : col.DefaultCellStyle.Alignment);

            col.DefaultCellStyle.Alignment = CombinarAlineacion(verticalActual, horizontalActual);

            if (col.DefaultCellStyle.WrapMode == DataGridViewTriState.True)
                AjustarAutoAlturaFilas(_formActivo.GridPrincipal);

            _formActivo.GridPrincipal.Invalidate();
        }

        private DataGridViewContentAlignment ObtenerAlineacionVerticalActual()
        {
            var col = ObtenerColumnaSeleccionadaGrid();
            if (col == null) return DataGridViewContentAlignment.MiddleLeft;

            var actual = col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet
                ? DataGridViewContentAlignment.MiddleLeft
                : col.DefaultCellStyle.Alignment;

            return actual;
        }

        private DataGridViewContentAlignment ObtenerAlineacionVerticalBoton()
        {
            if (EstaBotonActivo(btnAlinAba)) return DataGridViewContentAlignment.BottomLeft;
            if (EstaBotonActivo(btnAlinJus)) return DataGridViewContentAlignment.TopLeft;
            return DataGridViewContentAlignment.MiddleLeft;
        }

        private void ActualizarBotonesAlinVertical(DataGridViewContentAlignment alin)
        {
            var vertical = ObtenerVertical(alin);

            AplicarTemaBotonRibbon(btnAlinJus, vertical == DataGridViewTriState.True);
            AplicarTemaBotonRibbon(btnAlinMed, vertical == DataGridViewTriState.NotSet);
            AplicarTemaBotonRibbon(btnAlinAba, vertical == DataGridViewTriState.False);
        }

        private DataGridViewTriState ObtenerVertical(DataGridViewContentAlignment alin)
        {
            return alin switch
            {
                DataGridViewContentAlignment.TopLeft or
                DataGridViewContentAlignment.TopCenter or
                DataGridViewContentAlignment.TopRight => DataGridViewTriState.True,
                DataGridViewContentAlignment.BottomLeft or
                DataGridViewContentAlignment.BottomCenter or
                DataGridViewContentAlignment.BottomRight => DataGridViewTriState.False,
                _ => DataGridViewTriState.NotSet
            };
        }

        private HorizontalAlignment ObtenerHorizontal(DataGridViewContentAlignment alin)
        {
            return alin switch
            {
                DataGridViewContentAlignment.TopCenter or
                DataGridViewContentAlignment.MiddleCenter or
                DataGridViewContentAlignment.BottomCenter => HorizontalAlignment.Center,
                DataGridViewContentAlignment.TopRight or
                DataGridViewContentAlignment.MiddleRight or
                DataGridViewContentAlignment.BottomRight => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left
            };
        }

        private DataGridViewContentAlignment CombinarAlineacion(DataGridViewContentAlignment verticalBase, HorizontalAlignment horizontal)
        {
            var esTop = verticalBase == DataGridViewContentAlignment.TopLeft ||
                        verticalBase == DataGridViewContentAlignment.TopCenter ||
                        verticalBase == DataGridViewContentAlignment.TopRight;
            var esBottom = verticalBase == DataGridViewContentAlignment.BottomLeft ||
                           verticalBase == DataGridViewContentAlignment.BottomCenter ||
                           verticalBase == DataGridViewContentAlignment.BottomRight;

            if (esTop)
            {
                return horizontal switch
                {
                    HorizontalAlignment.Center => DataGridViewContentAlignment.TopCenter,
                    HorizontalAlignment.Right => DataGridViewContentAlignment.TopRight,
                    _ => DataGridViewContentAlignment.TopLeft
                };
            }

            if (esBottom)
            {
                return horizontal switch
                {
                    HorizontalAlignment.Center => DataGridViewContentAlignment.BottomCenter,
                    HorizontalAlignment.Right => DataGridViewContentAlignment.BottomRight,
                    _ => DataGridViewContentAlignment.BottomLeft
                };
            }

            return horizontal switch
            {
                HorizontalAlignment.Center => DataGridViewContentAlignment.MiddleCenter,
                HorizontalAlignment.Right => DataGridViewContentAlignment.MiddleRight,
                _ => DataGridViewContentAlignment.MiddleLeft
            };
        }

        private void AjustarAutoAlturaFilas(DataGridView grid) => FormatoHelper.AjustarAutoAlturaFilas(grid);

        // Helpers ribbon
        private bool ObtenerWrapActual()
        {
            if (_formActivo?.GridPrincipal == null || _formActivo?.ColumnaSeleccionada == null) return false;

            var nombreColumna = _formActivo.ColumnaSeleccionada.Nombre;
            var col = _formActivo.GridPrincipal.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.HeaderText, nombreColumna, StringComparison.OrdinalIgnoreCase));

            return col?.DefaultCellStyle.WrapMode == DataGridViewTriState.True;
        }

        private void ActualizarBotonesAlin(AlineacionColumna alin)
        {
            AplicarTemaBotonRibbon(btnAlinIzq, alin == AlineacionColumna.Izquierda);
            AplicarTemaBotonRibbon(btnAlinCen, alin == AlineacionColumna.Centro);
            AplicarTemaBotonRibbon(btnAlinDer, alin == AlineacionColumna.Derecha);
        }

        private AlineacionColumna ObtenerAlin()
        {
            if (EstaBotonActivo(btnAlinCen)) return AlineacionColumna.Centro;
            if (EstaBotonActivo(btnAlinDer)) return AlineacionColumna.Derecha;
            return AlineacionColumna.Izquierda;
        }

        private void ActualizarMuestraTexto()
        {
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
        }

        /// <summary>
        /// Notifica a todos los FormProyecto abiertos que un insumo cambió:
        /// recarga el catálogo de matrices y recalcula el presupuesto.
        /// Llamar desde los catálogos tras eliminar o editar un insumo.
        /// </summary>
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
