using System.Drawing;
using System.Windows.Forms;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.WinForms.Forms
{
    partial class FormProyecto
    {
        private System.ComponentModel.IContainer components = null;
        internal System.Windows.Forms.Panel panelLeft;
        internal System.Windows.Forms.Panel panelSidebarHeader;
        private System.Windows.Forms.Button btnToggleSidebar;
        internal System.Windows.Forms.TreeView treeMenu;
        internal System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Panel panelRibbon;
        internal System.Windows.Forms.Label lblColumna;
        internal System.Windows.Forms.Label lblSepFuente;
        internal System.Windows.Forms.ComboBox cboFuente;
        internal System.Windows.Forms.NumericUpDown nudTamano;
        private System.Windows.Forms.Button btnNegrita;
        private System.Windows.Forms.Button btnCursiva;
        internal System.Windows.Forms.Label lblSepEstilo;
        internal System.Windows.Forms.Label lblGroupDatos;
        internal System.Windows.Forms.Label lblGroupExportar;
        internal System.Windows.Forms.Label lblGroupFuente;
        internal System.Windows.Forms.Label lblGroupAlineacion;
        internal System.Windows.Forms.Label lblGroupColores;
        internal System.Windows.Forms.Label lblGroupCeldas;
        internal SoproButton btnAlinIzq;
        internal SoproButton btnAlinCen;
        internal SoproButton btnAlinDer;
        internal SoproButton btnAlinJus;
        internal SoproButton btnAlinMed;
        internal SoproButton btnAlinAba;
        internal System.Windows.Forms.Label lblSepAlin;
        internal SoproButton btnColorFondo;
        internal SoproButton btnColorTexto;
        internal System.Windows.Forms.Label  lblSepReporte;
        internal SoproButton btnExcelRibbon;
        internal SoproButton btnPdfRibbon;
        internal SoproButton btnBuscarRibbon;
        internal SoproButton btnWrapRibbon;
        internal SoproButton btnRecalcularRibbon;
        internal SoproButton btnDepurarRibbon;
        internal System.Windows.Forms.Label lblSepGlobal;
        internal SoproButton btnAplicarATodas;
        internal SoproButton btnConsolidarInsumos;
        internal System.Windows.Forms.ToolTip toolTipRibbon;
        private System.Windows.Forms.ContextMenuStrip cmsRibbonOverflow;
        private System.Windows.Forms.Button btnRibbonMas;
        internal System.Windows.Forms.Label lblTitulo;
        internal System.Windows.Forms.Label lblProyecto;
        private System.Windows.Forms.TabControl tabControl;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            TreeNode treeNode1 = new TreeNode("Plantilla de Reporte");
            TreeNode treeNode2 = new TreeNode("📄 Reportes", new TreeNode[] { treeNode1 });
            TreeNode treeNode3 = new TreeNode("Datos del Proyecto");
            TreeNode treeNode4 = new TreeNode("Porcentajes");
            TreeNode treeNode5 = new TreeNode("⚙ Configuración", new TreeNode[] { treeNode3, treeNode4 });
            TreeNode treeNode6 = new TreeNode("Hoja de Presupuesto");
            TreeNode treeNode7 = new TreeNode("Explosión de Insumos");
            TreeNode treeNode8 = new TreeNode("Cálculo de Indirectos");
            TreeNode treeNode9 = new TreeNode("Cálculo de Financiamiento");
            TreeNode treeNode10 = new TreeNode("Cálculo de Utilidad");
            TreeNode treeNode11 = new TreeNode("Factor Salario Real (FSR)");
            TreeNode treeNode12 = new TreeNode("📊 Presupuesto", new TreeNode[] { treeNode6, treeNode7, treeNode8, treeNode9, treeNode10, treeNode11 });
            TreeNode treeNode13 = new TreeNode("Programa de Obra");
            TreeNode treeNode14 = new TreeNode("Programa de Insumos");
            TreeNode treeNode15 = new TreeNode("📅 Programación", new TreeNode[] { treeNode13, treeNode14 });
            TreeNode treeNode16 = new TreeNode("Materiales");
            TreeNode treeNode17 = new TreeNode("Mano de Obra");
            TreeNode treeNode18 = new TreeNode("Herramienta");
            TreeNode treeNode19 = new TreeNode("Equipo");
            TreeNode treeNode20 = new TreeNode("Matrices (APU/Básicos)");
            TreeNode treeNode21 = new TreeNode("📚 Catálogos", new TreeNode[] { treeNode16, treeNode17, treeNode18, treeNode19, treeNode20 });
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormProyecto));
            panelLeft = new Panel();
            treeMenu = new TreeView();
            panelSidebarHeader = new Panel();
            btnToggleSidebar = new Button();
            panelTop = new Panel();
            lblProyecto = new Label();
            panelRibbon = new Panel();
            btnRibbonMas = new Button();
            label2 = new Label();
            label1 = new Label();
            btnPdfRibbon = new SoproButton();
            btnExcelRibbon = new SoproButton();
            btnDepurarRibbon = new SoproButton();
            btnRecalcularRibbon = new SoproButton();
            btnBuscarRibbon = new SoproButton();
            btnWrapRibbon = new SoproButton();
            lblSepReporte = new Label();
            btnConsolidarInsumos = new SoproButton();
            btnAplicarATodas = new SoproButton();
            lblSepGlobal = new Label();
            btnColorTexto = new SoproButton();
            btnColorFondo = new SoproButton();
            lblSepAlin = new Label();
            btnAlinAba = new SoproButton();
            btnAlinMed = new SoproButton();
            btnAlinJus = new SoproButton();
            btnAlinDer = new SoproButton();
            btnAlinCen = new SoproButton();
            btnAlinIzq = new SoproButton();
            lblSepEstilo = new Label();
            btnCursiva = new Button();
            btnNegrita = new Button();
            nudTamano = new NumericUpDown();
            cboFuente = new ComboBox();
            lblSepFuente = new Label();
            lblColumna = new Label();
            lblGroupDatos = new Label();
            lblGroupExportar = new Label();
            lblGroupFuente = new Label();
            lblGroupAlineacion = new Label();
            lblGroupColores = new Label();
            lblGroupCeldas = new Label();
            lblTitulo = new Label();
            toolTipRibbon = new ToolTip(components);
            cmsRibbonOverflow = new ContextMenuStrip(components);
            tabControl = new TabControl();
            panelLeft.SuspendLayout();
            panelSidebarHeader.SuspendLayout();
            panelTop.SuspendLayout();
            panelRibbon.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudTamano).BeginInit();
            SuspendLayout();
            // 
            // panelLeft
            // 
            panelLeft.BorderStyle = BorderStyle.FixedSingle;
            panelLeft.Controls.Add(treeMenu);
            panelLeft.Controls.Add(panelSidebarHeader);
            panelLeft.Dock = DockStyle.Left;
            panelLeft.Location = new Point(0, 88);
            panelLeft.Name = "panelLeft";
            panelLeft.Size = new Size(250, 708);
            panelLeft.TabIndex = 0;
            // 
            // treeMenu
            // 
            treeMenu.BorderStyle = BorderStyle.None;
            treeMenu.Dock = DockStyle.Fill;
            treeMenu.Font = new Font("Segoe UI", 10F);
            treeMenu.FullRowSelect = true;
            treeMenu.HideSelection = false;
            treeMenu.Indent = 20;
            treeMenu.ItemHeight = 35;
            treeMenu.Location = new Point(0, 34);
            treeMenu.Name = "treeMenu";
            treeNode1.Name = "nodePlantillaReporte";
            treeNode1.Text = "Plantilla de Reporte";
            treeNode2.Name = "nodeReportes";
            treeNode2.Text = "📄 Reportes";
            treeNode3.Name = "nodeDatosProyecto";
            treeNode3.Text = "Datos del Proyecto";
            treeNode4.Name = "nodePorcentajes";
            treeNode4.Text = "Porcentajes";
            treeNode5.Name = "nodeConfiguracion";
            treeNode5.Text = "⚙ Configuración";
            treeNode6.Name = "nodeHojaPresupuesto";
            treeNode6.Text = "Hoja de Presupuesto";
            treeNode7.Name = "nodeExplosionInsumos";
            treeNode7.Text = "Explosión de Insumos";
            treeNode8.Name = "nodeIndirectos";
            treeNode8.Text = "Cálculo de Indirectos";
            treeNode9.Name = "nodeFinanciamiento";
            treeNode9.Text = "Cálculo de Financiamiento";
            treeNode10.Name = "nodeUtilidad";
            treeNode10.Text = "Cálculo de Utilidad";
            treeNode11.Name = "nodeFSR";
            treeNode11.Text = "Factor Salario Real (FSR)";
            treeNode12.Name = "nodePresupuesto";
            treeNode12.Text = "📊 Presupuesto";
            treeNode13.Name = "nodeProgramaObra";
            treeNode13.Text = "Programa de Obra";
            treeNode14.Name = "nodeProgramaInsumos";
            treeNode14.Text = "Programa de Insumos";
            treeNode15.Name = "nodeProgramacion";
            treeNode15.Text = "📅 Programación";
            treeNode16.Name = "nodeMateriales";
            treeNode16.Text = "Materiales";
            treeNode17.Name = "nodeManoObra";
            treeNode17.Text = "Mano de Obra";
            treeNode18.Name = "nodeHerramienta";
            treeNode18.Text = "Herramienta";
            treeNode19.Name = "nodeEquipo";
            treeNode19.Text = "Equipo";
            treeNode20.Name = "nodeMatrices";
            treeNode20.Text = "Matrices (APU/Básicos)";
            treeNode21.Name = "nodeCatalogos";
            treeNode21.Text = "📚 Catálogos";
            treeMenu.Nodes.AddRange(new TreeNode[] { treeNode2, treeNode5, treeNode12, treeNode15, treeNode21 });
            treeMenu.Size = new Size(248, 680);
            treeMenu.TabIndex = 0;
            treeMenu.NodeMouseClick += treeMenu_NodeMouseClick;
            // 
            // panelSidebarHeader
            // 
            panelSidebarHeader.Controls.Add(btnToggleSidebar);
            panelSidebarHeader.Dock = DockStyle.Top;
            panelSidebarHeader.Location = new Point(0, 0);
            panelSidebarHeader.Name = "panelSidebarHeader";
            panelSidebarHeader.Size = new Size(248, 34);
            panelSidebarHeader.TabIndex = 1;
            // 
            // btnToggleSidebar
            // 
            btnToggleSidebar.Dock = DockStyle.Fill;
            btnToggleSidebar.FlatAppearance.BorderSize = 0;
            btnToggleSidebar.FlatStyle = FlatStyle.Flat;
            btnToggleSidebar.Font = new Font("Segoe UI Symbol", 11F, FontStyle.Bold);
            btnToggleSidebar.Location = new Point(0, 0);
            btnToggleSidebar.Margin = new Padding(0);
            btnToggleSidebar.Name = "btnToggleSidebar";
            btnToggleSidebar.Size = new Size(248, 34);
            btnToggleSidebar.TabIndex = 0;
            btnToggleSidebar.TabStop = false;
            btnToggleSidebar.Text = "☰";
            btnToggleSidebar.UseVisualStyleBackColor = true;
            btnToggleSidebar.Click += btnToggleSidebar_Click;
            // 
            // panelTop
            // 
            panelTop.Controls.Add(lblProyecto);
            panelTop.Controls.Add(panelRibbon);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(1600, 88);
            panelTop.TabIndex = 1;
            // 
            // lblProyecto
            // 
            lblProyecto.AutoSize = true;
            lblProyecto.Font = new Font("Segoe UI", 11F);
            lblProyecto.Location = new Point(20, 50);
            lblProyecto.Name = "lblProyecto";
            lblProyecto.Size = new Size(67, 20);
            lblProyecto.TabIndex = 1;
            lblProyecto.Text = "Proyecto";
            // 
            // panelRibbon
            // 
            panelRibbon.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            panelRibbon.BackColor = Color.Transparent;
            panelRibbon.Controls.Add(btnRibbonMas);
            panelRibbon.Controls.Add(label2);
            panelRibbon.Controls.Add(label1);
            panelRibbon.Controls.Add(btnPdfRibbon);
            panelRibbon.Controls.Add(btnExcelRibbon);
            panelRibbon.Controls.Add(btnDepurarRibbon);
            panelRibbon.Controls.Add(btnRecalcularRibbon);
            panelRibbon.Controls.Add(btnBuscarRibbon);
            panelRibbon.Controls.Add(btnWrapRibbon);
            panelRibbon.Controls.Add(lblSepReporte);
            panelRibbon.Controls.Add(btnConsolidarInsumos);
            panelRibbon.Controls.Add(btnAplicarATodas);
            panelRibbon.Controls.Add(lblSepGlobal);
            panelRibbon.Controls.Add(btnColorTexto);
            panelRibbon.Controls.Add(btnColorFondo);
            panelRibbon.Controls.Add(lblSepAlin);
            panelRibbon.Controls.Add(btnAlinAba);
            panelRibbon.Controls.Add(btnAlinMed);
            panelRibbon.Controls.Add(btnAlinJus);
            panelRibbon.Controls.Add(btnAlinDer);
            panelRibbon.Controls.Add(btnAlinCen);
            panelRibbon.Controls.Add(btnAlinIzq);
            panelRibbon.Controls.Add(lblSepEstilo);
            panelRibbon.Controls.Add(btnCursiva);
            panelRibbon.Controls.Add(btnNegrita);
            panelRibbon.Controls.Add(nudTamano);
            panelRibbon.Controls.Add(cboFuente);
            panelRibbon.Controls.Add(lblSepFuente);
            panelRibbon.Controls.Add(lblColumna);
            panelRibbon.Controls.Add(lblGroupDatos);
            panelRibbon.Controls.Add(lblGroupExportar);
            panelRibbon.Controls.Add(lblGroupFuente);
            panelRibbon.Controls.Add(lblGroupAlineacion);
            panelRibbon.Controls.Add(lblGroupColores);
            panelRibbon.Controls.Add(lblGroupCeldas);
            panelRibbon.Location = new Point(281, 4);
            panelRibbon.Name = "panelRibbon";
            panelRibbon.Size = new Size(1316, 80);
            panelRibbon.TabIndex = 3;
            // 
            // btnRibbonMas
            // 
            btnRibbonMas.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRibbonMas.FlatAppearance.BorderSize = 0;
            btnRibbonMas.FlatStyle = FlatStyle.Flat;
            btnRibbonMas.Font = new Font("Segoe UI Semibold", 11F);
            btnRibbonMas.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnRibbonMas.Location = new Point(1280, 28);
            btnRibbonMas.Name = "btnRibbonMas";
            btnRibbonMas.Size = new Size(28, 24);
            btnRibbonMas.TabIndex = 20;
            btnRibbonMas.Text = "⋯";
            toolTipRibbon.SetToolTip(btnRibbonMas, "Más acciones");
            btnRibbonMas.UseVisualStyleBackColor = false;
            btnRibbonMas.Visible = false;
            btnRibbonMas.Click += btnRibbonMas_Click;
            // 
            // label2
            //
            // Separadores: miden lo mismo que la banda de comandos (y=2..61)
            // para no cruzar la fila de rotulos de grupo (y=63..79).
            label2.Location = new Point(0, 2);
            label2.Name = "label2";
            label2.Size = new Size(1, 60);
            label2.TabIndex = 28;
            // 
            // label1
            // 
            label1.Location = new Point(308, 2);
            label1.Name = "label1";
            label1.Size = new Size(1, 60);
            label1.TabIndex = 27;
            // 
            // btnPdfRibbon
            // 
            btnPdfRibbon.FlatAppearance.BorderSize = 0;
            btnPdfRibbon.FlatStyle = FlatStyle.Flat;
            btnPdfRibbon.Font = new Font("Segoe UI", 9F);
            btnPdfRibbon.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnPdfRibbon.Location = new Point(390, 2);
            btnPdfRibbon.Name = "btnPdfRibbon";
            btnPdfRibbon.Size = new Size(72, 60);
            btnPdfRibbon.SoproContentPadding = new Padding(4, 0, 4, 0);
            btnPdfRibbon.SoproIcon = Helpers.SoproIconType.Pdf;
            btnPdfRibbon.SoproIconSize = 30;
            btnPdfRibbon.SoproLayout = UI.Controls.SoproButtonLayout.Large;
            btnPdfRibbon.SoproShowText = false;
            btnPdfRibbon.TabIndex = 5;
            btnPdfRibbon.Text = "PDF";
            toolTipRibbon.SetToolTip(btnPdfRibbon, "Exportar reporte a .pdf");
            btnPdfRibbon.UseVisualStyleBackColor = false;
            btnPdfRibbon.Click += btnPdfRibbon_Click;
            // 
            // btnExcelRibbon
            // 
            btnExcelRibbon.FlatAppearance.BorderSize = 0;
            btnExcelRibbon.FlatStyle = FlatStyle.Flat;
            btnExcelRibbon.Font = new Font("Segoe UI", 9F);
            btnExcelRibbon.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnExcelRibbon.Location = new Point(316, 2);
            btnExcelRibbon.Name = "btnExcelRibbon";
            btnExcelRibbon.Size = new Size(72, 60);
            btnExcelRibbon.SoproContentPadding = new Padding(4, 0, 4, 0);
            btnExcelRibbon.SoproIcon = Helpers.SoproIconType.Excel;
            btnExcelRibbon.SoproIconSize = 30;
            btnExcelRibbon.SoproLayout = UI.Controls.SoproButtonLayout.Large;
            btnExcelRibbon.SoproShowText = false;
            btnExcelRibbon.TabIndex = 4;
            btnExcelRibbon.Text = "Excel";
            toolTipRibbon.SetToolTip(btnExcelRibbon, "Exportar reporte a .xlsx (Excel)");
            btnExcelRibbon.UseVisualStyleBackColor = false;
            btnExcelRibbon.Click += btnExcelRibbon_Click;
            // 
            // btnDepurarRibbon
            // 
            btnDepurarRibbon.FlatAppearance.BorderSize = 0;
            btnDepurarRibbon.FlatStyle = FlatStyle.Flat;
            btnDepurarRibbon.Font = new Font("Segoe UI", 9F);
            btnDepurarRibbon.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnDepurarRibbon.Location = new Point(82, 2);
            btnDepurarRibbon.Name = "btnDepurarRibbon";
            btnDepurarRibbon.Padding = new Padding(4, 0, 4, 0);
            btnDepurarRibbon.Size = new Size(72, 60);
            btnDepurarRibbon.SoproContentPadding = new Padding(4, 0, 4, 0);
            btnDepurarRibbon.SoproIcon = Helpers.SoproIconType.Depurar;
            btnDepurarRibbon.SoproIconSize = 30;
            btnDepurarRibbon.SoproLayout = UI.Controls.SoproButtonLayout.Large;
            btnDepurarRibbon.SoproMinimumAutoWidth = 82;
            btnDepurarRibbon.TabIndex = 1;
            btnDepurarRibbon.Text = "Depurar";
            toolTipRibbon.SetToolTip(btnDepurarRibbon, "Eliminar matrices y auxiliares sin uso real");
            btnDepurarRibbon.UseVisualStyleBackColor = false;
            btnDepurarRibbon.Click += btnDepurarRibbon_Click;
            // 
            // btnRecalcularRibbon
            // 
            btnRecalcularRibbon.FlatAppearance.BorderSize = 0;
            btnRecalcularRibbon.FlatStyle = FlatStyle.Flat;
            btnRecalcularRibbon.Font = new Font("Segoe UI", 9F);
            btnRecalcularRibbon.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnRecalcularRibbon.Location = new Point(8, 2);
            btnRecalcularRibbon.Name = "btnRecalcularRibbon";
            btnRecalcularRibbon.Padding = new Padding(4, 0, 4, 0);
            btnRecalcularRibbon.Size = new Size(72, 60);
            btnRecalcularRibbon.SoproContentPadding = new Padding(4, 0, 4, 0);
            btnRecalcularRibbon.SoproIcon = Helpers.SoproIconType.Recalcular;
            btnRecalcularRibbon.SoproIconSize = 30;
            btnRecalcularRibbon.SoproLayout = UI.Controls.SoproButtonLayout.Large;
            btnRecalcularRibbon.SoproMinimumAutoWidth = 92;
            btnRecalcularRibbon.TabIndex = 0;
            btnRecalcularRibbon.Text = "Recalcular";
            toolTipRibbon.SetToolTip(btnRecalcularRibbon, "Recalcular el módulo activo (F9)");
            btnRecalcularRibbon.UseVisualStyleBackColor = false;
            btnRecalcularRibbon.Click += btnRecalcularRibbon_Click;
            // 
            // btnBuscarRibbon
            // 
            btnBuscarRibbon.FlatAppearance.BorderSize = 0;
            btnBuscarRibbon.FlatStyle = FlatStyle.Flat;
            btnBuscarRibbon.Font = new Font("Segoe UI", 9F);
            btnBuscarRibbon.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnBuscarRibbon.Location = new Point(230, 2);
            btnBuscarRibbon.Name = "btnBuscarRibbon";
            btnBuscarRibbon.Padding = new Padding(4, 0, 4, 0);
            btnBuscarRibbon.Size = new Size(72, 60);
            btnBuscarRibbon.SoproContentPadding = new Padding(4, 0, 4, 0);
            btnBuscarRibbon.SoproIcon = Helpers.SoproIconType.Buscar;
            btnBuscarRibbon.SoproIconSize = 30;
            btnBuscarRibbon.SoproLayout = UI.Controls.SoproButtonLayout.Large;
            btnBuscarRibbon.SoproMinimumAutoWidth = 74;
            btnBuscarRibbon.TabIndex = 3;
            btnBuscarRibbon.Text = "Buscar";
            toolTipRibbon.SetToolTip(btnBuscarRibbon, "Buscar en el grid activo (Ctrl+B, F3 para siguiente)");
            btnBuscarRibbon.UseVisualStyleBackColor = false;
            btnBuscarRibbon.Click += btnBuscarRibbon_Click;
            // 
            // btnWrapRibbon
            // 
            btnWrapRibbon.FlatAppearance.BorderSize = 0;
            btnWrapRibbon.FlatStyle = FlatStyle.Flat;
            btnWrapRibbon.Font = new Font("Segoe UI", 9F);
            btnWrapRibbon.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnWrapRibbon.Location = new Point(898, 6);
            btnWrapRibbon.Name = "btnWrapRibbon";
            btnWrapRibbon.Padding = new Padding(8, 0, 8, 0);
            btnWrapRibbon.Size = new Size(78, 26);
            btnWrapRibbon.SoproContentPadding = new Padding(8, 0, 8, 0);
            btnWrapRibbon.SoproIcon = Helpers.SoproIconType.AjustarTexto;
            btnWrapRibbon.SoproIconSize = 16;
            btnWrapRibbon.SoproIconTextSpacing = 6;
            btnWrapRibbon.SoproLayout = UI.Controls.SoproButtonLayout.Small;
            btnWrapRibbon.SoproMinimumAutoWidth = 86;
            btnWrapRibbon.TabIndex = 18;
            btnWrapRibbon.Text = "Ajustar";
            toolTipRibbon.SetToolTip(btnWrapRibbon, "Ajustar texto de la columna seleccionada y recalcular altura de filas");
            btnWrapRibbon.UseVisualStyleBackColor = false;
            btnWrapRibbon.Click += btnWrapRibbon_Click;
            // 
            // lblSepReporte
            // 
            lblSepReporte.Location = new Point(468, 2);
            lblSepReporte.Name = "lblSepReporte";
            lblSepReporte.Size = new Size(1, 60);
            lblSepReporte.TabIndex = 29;
            // 
            // btnConsolidarInsumos
            // 
            btnConsolidarInsumos.FlatAppearance.BorderSize = 0;
            btnConsolidarInsumos.FlatStyle = FlatStyle.Flat;
            btnConsolidarInsumos.Font = new Font("Segoe UI", 9F);
            btnConsolidarInsumos.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnConsolidarInsumos.Location = new Point(156, 2);
            btnConsolidarInsumos.Name = "btnConsolidarInsumos";
            btnConsolidarInsumos.Padding = new Padding(4, 0, 4, 0);
            btnConsolidarInsumos.Size = new Size(72, 60);
            btnConsolidarInsumos.SoproContentPadding = new Padding(4, 0, 4, 0);
            btnConsolidarInsumos.SoproIcon = Helpers.SoproIconType.Consolidar;
            btnConsolidarInsumos.SoproIconSize = 30;
            btnConsolidarInsumos.SoproLayout = UI.Controls.SoproButtonLayout.Large;
            btnConsolidarInsumos.SoproMinimumAutoWidth = 86;
            btnConsolidarInsumos.TabIndex = 2;
            btnConsolidarInsumos.Text = "Consolidar";
            toolTipRibbon.SetToolTip(btnConsolidarInsumos, "Consolidar insumos seleccionados en el catálogo activo");
            btnConsolidarInsumos.UseVisualStyleBackColor = false;
            btnConsolidarInsumos.Click += btnConsolidarInsumos_Click;
            // 
            // btnAplicarATodas
            // 
            btnAplicarATodas.FlatAppearance.BorderSize = 0;
            btnAplicarATodas.FlatStyle = FlatStyle.Flat;
            btnAplicarATodas.Font = new Font("Segoe UI", 9F);
            btnAplicarATodas.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnAplicarATodas.Location = new Point(898, 36);
            btnAplicarATodas.Name = "btnAplicarATodas";
            btnAplicarATodas.Padding = new Padding(8, 0, 8, 0);
            btnAplicarATodas.Size = new Size(84, 26);
            btnAplicarATodas.SoproContentPadding = new Padding(8, 0, 8, 0);
            btnAplicarATodas.SoproIcon = Helpers.SoproIconType.AplicarATodas;
            btnAplicarATodas.SoproIconSize = 16;
            btnAplicarATodas.SoproIconTextSpacing = 6;
            btnAplicarATodas.SoproLayout = UI.Controls.SoproButtonLayout.Small;
            btnAplicarATodas.SoproMinimumAutoWidth = 86;
            btnAplicarATodas.TabIndex = 19;
            btnAplicarATodas.Text = "Aplicar";
            toolTipRibbon.SetToolTip(btnAplicarATodas, "Aplicar fuente, tamano, estilo y color de texto a todas las columnas");
            btnAplicarATodas.UseVisualStyleBackColor = false;
            btnAplicarATodas.Click += btnAplicarATodas_Click;
            // 
            // lblSepGlobal
            // 
            lblSepGlobal.Location = new Point(990, 2);
            lblSepGlobal.Name = "lblSepGlobal";
            lblSepGlobal.Size = new Size(1, 60);
            lblSepGlobal.TabIndex = 30;
            // 
            // btnColorTexto
            // 
            btnColorTexto.BackColor = Color.Black;
            btnColorTexto.FlatAppearance.BorderSize = 0;
            btnColorTexto.FlatStyle = FlatStyle.Flat;
            btnColorTexto.Font = new Font("Segoe UI", 9F);
            btnColorTexto.ForeColor = Color.White;
            btnColorTexto.Location = new Point(804, 6);
            btnColorTexto.Name = "btnColorTexto";
            btnColorTexto.Size = new Size(74, 26);
            btnColorTexto.SoproCaption = "Texto";
            btnColorTexto.SoproLayout = UI.Controls.SoproButtonLayout.SampleLetter;
            btnColorTexto.TabIndex = 16;
            btnColorTexto.Text = "A";
            toolTipRibbon.SetToolTip(btnColorTexto, "Color de texto");
            btnColorTexto.UseVisualStyleBackColor = false;
            // 
            // btnColorFondo
            // 
            btnColorFondo.BackColor = Color.White;
            btnColorFondo.FlatAppearance.BorderSize = 0;
            btnColorFondo.FlatStyle = FlatStyle.Flat;
            btnColorFondo.Font = new Font("Segoe UI", 9F);
            btnColorFondo.ForeColor = Color.Black;
            btnColorFondo.Location = new Point(804, 36);
            btnColorFondo.Name = "btnColorFondo";
            btnColorFondo.Size = new Size(78, 26);
            btnColorFondo.SoproCaption = "Fondo";
            btnColorFondo.SoproLayout = UI.Controls.SoproButtonLayout.SampleSwatch;
            btnColorFondo.TabIndex = 17;
            btnColorFondo.Text = "■";
            toolTipRibbon.SetToolTip(btnColorFondo, "Color de fondo de celda");
            btnColorFondo.UseVisualStyleBackColor = false;
            // 
            // lblSepAlin
            // 
            lblSepAlin.Location = new Point(796, 2);
            lblSepAlin.Name = "lblSepAlin";
            lblSepAlin.Size = new Size(1, 60);
            lblSepAlin.TabIndex = 31;
            // 
            // btnAlinAba
            // 
            btnAlinAba.FlatAppearance.BorderSize = 0;
            btnAlinAba.FlatStyle = FlatStyle.Flat;
            btnAlinAba.Font = new Font("Segoe UI", 9F);
            btnAlinAba.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnAlinAba.Location = new Point(758, 36);
            btnAlinAba.Name = "btnAlinAba";
            btnAlinAba.Size = new Size(28, 26);
            btnAlinAba.SoproContentPadding = new Padding(0);
            btnAlinAba.SoproIcon = Helpers.SoproIconType.AlinearAbajo;
            btnAlinAba.SoproIconSize = 16;
            btnAlinAba.SoproShowText = false;
            btnAlinAba.TabIndex = 15;
            toolTipRibbon.SetToolTip(btnAlinAba, "Alinear abajo");
            btnAlinAba.UseVisualStyleBackColor = false;
            // 
            // btnAlinMed
            // 
            btnAlinMed.FlatAppearance.BorderSize = 0;
            btnAlinMed.FlatStyle = FlatStyle.Flat;
            btnAlinMed.Font = new Font("Segoe UI", 9F);
            btnAlinMed.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnAlinMed.Location = new Point(728, 36);
            btnAlinMed.Name = "btnAlinMed";
            btnAlinMed.Size = new Size(28, 26);
            btnAlinMed.SoproContentPadding = new Padding(0);
            btnAlinMed.SoproIcon = Helpers.SoproIconType.AlinearMedio;
            btnAlinMed.SoproIconSize = 16;
            btnAlinMed.SoproShowText = false;
            btnAlinMed.TabIndex = 14;
            toolTipRibbon.SetToolTip(btnAlinMed, "Alinear al medio");
            btnAlinMed.UseVisualStyleBackColor = false;
            // 
            // btnAlinJus
            // 
            btnAlinJus.FlatAppearance.BorderSize = 0;
            btnAlinJus.FlatStyle = FlatStyle.Flat;
            btnAlinJus.Font = new Font("Segoe UI", 9F);
            btnAlinJus.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnAlinJus.Location = new Point(698, 36);
            btnAlinJus.Name = "btnAlinJus";
            btnAlinJus.Size = new Size(28, 26);
            btnAlinJus.SoproContentPadding = new Padding(0);
            btnAlinJus.SoproIcon = Helpers.SoproIconType.AlinearArriba;
            btnAlinJus.SoproIconSize = 16;
            btnAlinJus.SoproShowText = false;
            btnAlinJus.TabIndex = 13;
            toolTipRibbon.SetToolTip(btnAlinJus, "Alinear arriba");
            btnAlinJus.UseVisualStyleBackColor = false;
            // 
            // btnAlinDer
            // 
            btnAlinDer.FlatAppearance.BorderSize = 0;
            btnAlinDer.FlatStyle = FlatStyle.Flat;
            btnAlinDer.Font = new Font("Segoe UI", 9F);
            btnAlinDer.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnAlinDer.Location = new Point(758, 6);
            btnAlinDer.Name = "btnAlinDer";
            btnAlinDer.Size = new Size(28, 26);
            btnAlinDer.SoproContentPadding = new Padding(0);
            btnAlinDer.SoproIcon = Helpers.SoproIconType.AlinearDerecha;
            btnAlinDer.SoproIconSize = 16;
            btnAlinDer.SoproShowText = false;
            btnAlinDer.TabIndex = 12;
            toolTipRibbon.SetToolTip(btnAlinDer, "Alinear a la derecha");
            btnAlinDer.UseVisualStyleBackColor = false;
            // 
            // btnAlinCen
            // 
            btnAlinCen.FlatAppearance.BorderSize = 0;
            btnAlinCen.FlatStyle = FlatStyle.Flat;
            btnAlinCen.Font = new Font("Segoe UI", 9F);
            btnAlinCen.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnAlinCen.Location = new Point(728, 6);
            btnAlinCen.Name = "btnAlinCen";
            btnAlinCen.Size = new Size(28, 26);
            btnAlinCen.SoproContentPadding = new Padding(0);
            btnAlinCen.SoproIcon = Helpers.SoproIconType.AlinearCentro;
            btnAlinCen.SoproIconSize = 16;
            btnAlinCen.SoproShowText = false;
            btnAlinCen.TabIndex = 11;
            toolTipRibbon.SetToolTip(btnAlinCen, "Centrar");
            btnAlinCen.UseVisualStyleBackColor = false;
            // 
            // btnAlinIzq
            // 
            btnAlinIzq.FlatAppearance.BorderSize = 0;
            btnAlinIzq.FlatStyle = FlatStyle.Flat;
            btnAlinIzq.Font = new Font("Segoe UI", 9F);
            btnAlinIzq.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnAlinIzq.Location = new Point(698, 6);
            btnAlinIzq.Name = "btnAlinIzq";
            btnAlinIzq.Size = new Size(28, 26);
            btnAlinIzq.SoproContentPadding = new Padding(0);
            btnAlinIzq.SoproIcon = Helpers.SoproIconType.AlinearIzquierda;
            btnAlinIzq.SoproIconSize = 16;
            btnAlinIzq.SoproShowText = false;
            btnAlinIzq.TabIndex = 10;
            toolTipRibbon.SetToolTip(btnAlinIzq, "Alinear a la izquierda");
            btnAlinIzq.UseVisualStyleBackColor = false;
            // 
            // lblSepEstilo
            // 
            lblSepEstilo.Location = new Point(890, 2);
            lblSepEstilo.Name = "lblSepEstilo";
            lblSepEstilo.Size = new Size(1, 60);
            lblSepEstilo.TabIndex = 32;
            // 
            // btnCursiva
            // 
            btnCursiva.FlatAppearance.BorderColor = Color.FromArgb(245, 245, 248);
            btnCursiva.FlatAppearance.BorderSize = 0;
            btnCursiva.FlatStyle = FlatStyle.Flat;
            btnCursiva.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            btnCursiva.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnCursiva.Location = new Point(506, 36);
            btnCursiva.Name = "btnCursiva";
            btnCursiva.Size = new Size(28, 26);
            btnCursiva.TabIndex = 9;
            btnCursiva.Text = "I";
            toolTipRibbon.SetToolTip(btnCursiva, "Cursiva");
            btnCursiva.UseVisualStyleBackColor = true;
            // 
            // btnNegrita
            // 
            btnNegrita.FlatAppearance.BorderColor = Color.FromArgb(245, 245, 248);
            btnNegrita.FlatAppearance.BorderSize = 0;
            btnNegrita.FlatStyle = FlatStyle.Flat;
            btnNegrita.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnNegrita.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            btnNegrita.Location = new Point(476, 36);
            btnNegrita.Name = "btnNegrita";
            btnNegrita.Size = new Size(28, 26);
            btnNegrita.TabIndex = 8;
            btnNegrita.Text = "N";
            toolTipRibbon.SetToolTip(btnNegrita, "Negrita");
            btnNegrita.UseVisualStyleBackColor = true;
            // 
            // nudTamano
            // 
            nudTamano.BackColor = Helpers.SoproTheme.Colors.RibbonDown;
            nudTamano.Font = new Font("Segoe UI", 9F);
            nudTamano.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            nudTamano.Location = new Point(628, 8);
            nudTamano.Maximum = new decimal(new int[] { 24, 0, 0, 0 });
            nudTamano.Minimum = new decimal(new int[] { 6, 0, 0, 0 });
            nudTamano.Name = "nudTamano";
            nudTamano.Size = new Size(46, 21);
            nudTamano.TabIndex = 7;
            toolTipRibbon.SetToolTip(nudTamano, "Tamano de fuente");
            nudTamano.Value = new decimal(new int[] { 9, 0, 0, 0 });
            // 
            // cboFuente
            // 
            cboFuente.BackColor = Helpers.SoproTheme.Colors.RibbonDown;
            cboFuente.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFuente.FlatStyle = FlatStyle.Flat;
            cboFuente.Font = new Font("Segoe UI", 9F);
            cboFuente.ForeColor = Helpers.SoproTheme.Colors.RibbonText;
            cboFuente.Location = new Point(476, 6);
            cboFuente.Name = "cboFuente";
            cboFuente.Size = new Size(148, 25);
            cboFuente.TabIndex = 6;
            toolTipRibbon.SetToolTip(cboFuente, "Tipo de fuente");
            // 
            // lblSepFuente
            // 
            lblSepFuente.Location = new Point(690, 2);
            lblSepFuente.Name = "lblSepFuente";
            lblSepFuente.Size = new Size(1, 60);
            lblSepFuente.TabIndex = 33;
            // 
            // lblColumna
            // 
            // Indicador de destino del formato. AutoEllipsis para que los
            // textos largos ("Clic en encabezado o selecciona celdas") se
            // recorten con puntos suspensivos en vez de cortarse a media
            // palabra; el color lo fija SoproTheme (token del tema, no gris).
            lblColumna.AutoEllipsis = true;
            lblColumna.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblColumna.ForeColor = Helpers.SoproTheme.Colors.RibbonDisabledIcon;
            lblColumna.Location = new Point(540, 36);
            lblColumna.Name = "lblColumna";
            lblColumna.Size = new Size(134, 26);
            lblColumna.TabIndex = 34;
            lblColumna.Text = "-- sin seleccion --";
            lblColumna.TextAlign = ContentAlignment.MiddleLeft;
            toolTipRibbon.SetToolTip(lblColumna, "Destino del formato: columna o selección de celdas");
            //
            // lblGroupDatos
            //
            lblGroupDatos.AutoSize = false;
            lblGroupDatos.Font = new Font("Segoe UI", 8.5F);
            lblGroupDatos.ForeColor = Helpers.SoproTheme.Colors.RibbonDisabledIcon;
            lblGroupDatos.Location = new Point(8, 63);
            lblGroupDatos.Name = "lblGroupDatos";
            lblGroupDatos.Size = new Size(294, 16);
            lblGroupDatos.TabIndex = 35;
            lblGroupDatos.Text = "Datos";
            lblGroupDatos.TextAlign = ContentAlignment.MiddleCenter;
            //
            // lblGroupExportar
            //
            lblGroupExportar.AutoSize = false;
            lblGroupExportar.Font = new Font("Segoe UI", 8.5F);
            lblGroupExportar.ForeColor = Helpers.SoproTheme.Colors.RibbonDisabledIcon;
            lblGroupExportar.Location = new Point(316, 63);
            lblGroupExportar.Name = "lblGroupExportar";
            lblGroupExportar.Size = new Size(146, 16);
            lblGroupExportar.TabIndex = 36;
            lblGroupExportar.Text = "Exportar";
            lblGroupExportar.TextAlign = ContentAlignment.MiddleCenter;
            //
            // lblGroupFuente
            //
            lblGroupFuente.AutoSize = false;
            lblGroupFuente.Font = new Font("Segoe UI", 8.5F);
            lblGroupFuente.ForeColor = Helpers.SoproTheme.Colors.RibbonDisabledIcon;
            lblGroupFuente.Location = new Point(476, 63);
            lblGroupFuente.Name = "lblGroupFuente";
            lblGroupFuente.Size = new Size(198, 16);
            lblGroupFuente.TabIndex = 37;
            lblGroupFuente.Text = "Fuente";
            lblGroupFuente.TextAlign = ContentAlignment.MiddleCenter;
            //
            // lblGroupAlineacion
            //
            lblGroupAlineacion.AutoSize = false;
            lblGroupAlineacion.Font = new Font("Segoe UI", 8.5F);
            lblGroupAlineacion.ForeColor = Helpers.SoproTheme.Colors.RibbonDisabledIcon;
            lblGroupAlineacion.Location = new Point(698, 63);
            lblGroupAlineacion.Name = "lblGroupAlineacion";
            lblGroupAlineacion.Size = new Size(88, 16);
            lblGroupAlineacion.TabIndex = 38;
            lblGroupAlineacion.Text = "Alineación";
            lblGroupAlineacion.TextAlign = ContentAlignment.MiddleCenter;
            //
            // lblGroupColores
            //
            lblGroupColores.AutoSize = false;
            lblGroupColores.Font = new Font("Segoe UI", 8.5F);
            lblGroupColores.ForeColor = Helpers.SoproTheme.Colors.RibbonDisabledIcon;
            lblGroupColores.Location = new Point(804, 63);
            lblGroupColores.Name = "lblGroupColores";
            lblGroupColores.Size = new Size(78, 16);
            lblGroupColores.TabIndex = 39;
            lblGroupColores.Text = "Colores";
            lblGroupColores.TextAlign = ContentAlignment.MiddleCenter;
            //
            // lblGroupCeldas
            //
            lblGroupCeldas.AutoSize = false;
            lblGroupCeldas.Font = new Font("Segoe UI", 8.5F);
            lblGroupCeldas.ForeColor = Helpers.SoproTheme.Colors.RibbonDisabledIcon;
            lblGroupCeldas.Location = new Point(898, 63);
            lblGroupCeldas.Name = "lblGroupCeldas";
            lblGroupCeldas.Size = new Size(84, 16);
            lblGroupCeldas.TabIndex = 40;
            lblGroupCeldas.Text = "Celdas";
            lblGroupCeldas.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.Location = new Point(20, 14);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(94, 32);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "SOPRO";
            // 
            // toolTipRibbon
            // 
            toolTipRibbon.AutoPopDelay = 5000;
            toolTipRibbon.InitialDelay = 400;
            toolTipRibbon.ReshowDelay = 200;
            // 
            // cmsRibbonOverflow
            // 
            cmsRibbonOverflow.Name = "cmsRibbonOverflow";
            cmsRibbonOverflow.Size = new Size(61, 4);
            // 
            // tabControl
            // 
            tabControl.Dock = DockStyle.Fill;
            tabControl.Font = new Font("Segoe UI", 9F);
            tabControl.Location = new Point(250, 88);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1350, 708);
            tabControl.TabIndex = 2;
            tabControl.MouseClick += tabControl_MouseClick;
            // 
            // FormProyecto
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1600, 796);
            Controls.Add(tabControl);
            Controls.Add(panelLeft);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(720, 535);
            Name = "FormProyecto";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "SOPRO - Sistema de Presupuestos de Obra";
            WindowState = FormWindowState.Maximized;
            Load += FormProyecto_Load;
            panelLeft.ResumeLayout(false);
            panelSidebarHeader.ResumeLayout(false);
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelRibbon.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)nudTamano).EndInit();
            ResumeLayout(false);
        }

        #endregion

        internal Label label1;
        internal Label label2;
    }
}
