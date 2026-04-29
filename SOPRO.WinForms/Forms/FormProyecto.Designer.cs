using System.Drawing;
using System.Windows.Forms;
using SOPRO.WinForms.UI.Controls;

namespace SOPRO.WinForms.Forms
{
    partial class FormProyecto
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.Panel panelSidebarHeader;
        private System.Windows.Forms.Button btnToggleSidebar;
        private System.Windows.Forms.TreeView treeMenu;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Panel panelRibbon;
        private System.Windows.Forms.Label lblColumna;
        private System.Windows.Forms.Label lblSepFuente;
        private System.Windows.Forms.ComboBox cboFuente;
        private System.Windows.Forms.NumericUpDown nudTamano;
        private System.Windows.Forms.Button btnNegrita;
        private System.Windows.Forms.Button btnCursiva;
        private System.Windows.Forms.Label lblSepEstilo;
        private SoproButton btnAlinIzq;
        private SoproButton btnAlinCen;
        private SoproButton btnAlinDer;
        private SoproButton btnAlinJus;
        private SoproButton btnAlinMed;
        private SoproButton btnAlinAba;
        private System.Windows.Forms.Label lblSepAlin;
        private System.Windows.Forms.Button btnColorFondo;
        private System.Windows.Forms.Button btnColorTexto;
        private System.Windows.Forms.Label  lblSepReporte;
        private SoproButton btnExcelRibbon;
        private SoproButton btnPdfRibbon;
        private SoproButton btnBuscarRibbon;
        private SoproButton btnWrapRibbon;
        private SoproButton btnRecalcularRibbon;
        private SoproButton btnDepurarRibbon;
        private System.Windows.Forms.Label lblSepGlobal;
        private SoproButton btnAplicarATodas;
        private SoproButton btnConsolidarInsumos;
        private System.Windows.Forms.ToolTip toolTipRibbon;
        private System.Windows.Forms.ContextMenuStrip cmsRibbonOverflow;
        private System.Windows.Forms.Button btnRibbonMas;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.Label lblProyecto;
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
            btnColorTexto = new Button();
            btnColorFondo = new Button();
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
            panelLeft.BackColor = Color.FromArgb(250, 250, 250);
            panelLeft.BorderStyle = BorderStyle.FixedSingle;
            panelLeft.Controls.Add(treeMenu);
            panelLeft.Controls.Add(panelSidebarHeader);
            panelLeft.Dock = DockStyle.Left;
            panelLeft.Location = new Point(0, 80);
            panelLeft.Name = "panelLeft";
            panelLeft.Size = new Size(250, 716);
            panelLeft.TabIndex = 0;
            // 
            // treeMenu
            // 
            treeMenu.BackColor = Color.White;
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
            panelSidebarHeader.BackColor = Color.FromArgb(240, 240, 243);
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
            panelTop.BackColor = Color.FromArgb(51, 51, 76);
            panelTop.Controls.Add(lblProyecto);
            panelTop.Controls.Add(panelRibbon);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(1600, 80);
            panelTop.TabIndex = 1;
            // 
            // lblProyecto
            // 
            lblProyecto.AutoSize = true;
            lblProyecto.Font = new Font("Segoe UI", 11F);
            lblProyecto.ForeColor = Color.White;
            lblProyecto.Location = new Point(20, 45);
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
            panelRibbon.Location = new Point(281, 3);
            panelRibbon.Name = "panelRibbon";
            panelRibbon.Size = new Size(1316, 75);
            panelRibbon.TabIndex = 3;
            // 
            // btnRibbonMas
            // 
            btnRibbonMas.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRibbonMas.FlatAppearance.BorderSize = 0;
            btnRibbonMas.FlatStyle = FlatStyle.Flat;
            btnRibbonMas.Font = new Font("Segoe UI Semibold", 11F);
            btnRibbonMas.ForeColor = Color.Silver;
            btnRibbonMas.Location = new Point(1280, 24);
            btnRibbonMas.Name = "btnRibbonMas";
            btnRibbonMas.Size = new Size(28, 24);
            btnRibbonMas.TabIndex = 29;
            btnRibbonMas.Text = "⋯";
            toolTipRibbon.SetToolTip(btnRibbonMas, "Más acciones");
            btnRibbonMas.UseVisualStyleBackColor = false;
            btnRibbonMas.Visible = false;
            btnRibbonMas.Click += btnRibbonMas_Click;
            // 
            // label2
            // 
            label2.BackColor = Color.FromArgb(200, 200, 200);
            label2.Location = new Point(858, 8);
            label2.Name = "label2";
            label2.Size = new Size(1, 59);
            label2.TabIndex = 28;
            // 
            // label1
            // 
            label1.BackColor = Color.FromArgb(200, 200, 200);
            label1.Location = new Point(614, 8);
            label1.Name = "label1";
            label1.Size = new Size(1, 59);
            label1.TabIndex = 27;
            // 
            // btnPdfRibbon
            // 
            btnPdfRibbon.FlatAppearance.BorderSize = 0;
            btnPdfRibbon.FlatStyle = FlatStyle.Flat;
            btnPdfRibbon.Font = new Font("Segoe UI", 8.5F);
            btnPdfRibbon.ForeColor = Color.Silver;
            btnPdfRibbon.Image = (Image)resources.GetObject("btnPdfRibbon.Image");
            btnPdfRibbon.Location = new Point(864, 39);
            btnPdfRibbon.Name = "btnPdfRibbon";
            btnPdfRibbon.Padding = new Padding(6, 0, 6, 0);
            btnPdfRibbon.Size = new Size(28, 24);
            btnPdfRibbon.SoproContentPadding = new Padding(6, 0, 6, 0);
            btnPdfRibbon.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnPdfRibbon.SoproIcon = Helpers.SoproIconType.Pdf;
            btnPdfRibbon.SoproShowText = false;
            btnPdfRibbon.TabIndex = 19;
            toolTipRibbon.SetToolTip(btnPdfRibbon, "Exportar reporte a .pdf");
            btnPdfRibbon.UseVisualStyleBackColor = false;
            btnPdfRibbon.Click += btnPdfRibbon_Click;
            // 
            // btnExcelRibbon
            // 
            btnExcelRibbon.FlatAppearance.BorderSize = 0;
            btnExcelRibbon.FlatStyle = FlatStyle.Flat;
            btnExcelRibbon.Font = new Font("Segoe UI", 8.5F);
            btnExcelRibbon.ForeColor = Color.Silver;
            btnExcelRibbon.Image = (Image)resources.GetObject("btnExcelRibbon.Image");
            btnExcelRibbon.Location = new Point(864, 9);
            btnExcelRibbon.Name = "btnExcelRibbon";
            btnExcelRibbon.Padding = new Padding(6, 0, 6, 0);
            btnExcelRibbon.Size = new Size(28, 24);
            btnExcelRibbon.SoproContentPadding = new Padding(6, 0, 6, 0);
            btnExcelRibbon.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnExcelRibbon.SoproIcon = Helpers.SoproIconType.Excel;
            btnExcelRibbon.SoproShowText = false;
            btnExcelRibbon.TabIndex = 18;
            toolTipRibbon.SetToolTip(btnExcelRibbon, "Exportar reporte a .xlsx (Excel)");
            btnExcelRibbon.UseVisualStyleBackColor = false;
            btnExcelRibbon.Click += btnExcelRibbon_Click;
            // 
            // btnDepurarRibbon
            // 
            btnDepurarRibbon.FlatAppearance.BorderSize = 0;
            btnDepurarRibbon.FlatStyle = FlatStyle.Flat;
            btnDepurarRibbon.Font = new Font("Segoe UI", 8.5F);
            btnDepurarRibbon.ForeColor = Color.Silver;
            btnDepurarRibbon.Image = (Image)resources.GetObject("btnDepurarRibbon.Image");
            btnDepurarRibbon.ImageAlign = ContentAlignment.MiddleLeft;
            btnDepurarRibbon.Location = new Point(620, 39);
            btnDepurarRibbon.Name = "btnDepurarRibbon";
            btnDepurarRibbon.Padding = new Padding(8, 0, 10, 0);
            btnDepurarRibbon.Size = new Size(107, 24);
            btnDepurarRibbon.SoproContentPadding = new Padding(8, 0, 10, 0);
            btnDepurarRibbon.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnDepurarRibbon.SoproIcon = Helpers.SoproIconType.Depurar;
            btnDepurarRibbon.SoproMinimumAutoWidth = 82;
            btnDepurarRibbon.TabIndex = 16;
            btnDepurarRibbon.Text = "Depurar";
            btnDepurarRibbon.TextImageRelation = TextImageRelation.ImageBeforeText;
            toolTipRibbon.SetToolTip(btnDepurarRibbon, "Eliminar matrices y auxiliares sin uso real");
            btnDepurarRibbon.UseVisualStyleBackColor = false;
            btnDepurarRibbon.Click += btnDepurarRibbon_Click;
            // 
            // btnRecalcularRibbon
            // 
            btnRecalcularRibbon.FlatAppearance.BorderSize = 0;
            btnRecalcularRibbon.FlatStyle = FlatStyle.Flat;
            btnRecalcularRibbon.Font = new Font("Segoe UI", 8.5F);
            btnRecalcularRibbon.ForeColor = Color.Silver;
            btnRecalcularRibbon.Image = (Image)resources.GetObject("btnRecalcularRibbon.Image");
            btnRecalcularRibbon.ImageAlign = ContentAlignment.MiddleLeft;
            btnRecalcularRibbon.Location = new Point(620, 11);
            btnRecalcularRibbon.Name = "btnRecalcularRibbon";
            btnRecalcularRibbon.Padding = new Padding(8, 0, 10, 0);
            btnRecalcularRibbon.Size = new Size(107, 24);
            btnRecalcularRibbon.SoproContentPadding = new Padding(8, 0, 10, 0);
            btnRecalcularRibbon.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnRecalcularRibbon.SoproIcon = Helpers.SoproIconType.Recalcular;
            btnRecalcularRibbon.SoproMinimumAutoWidth = 92;
            btnRecalcularRibbon.TabIndex = 15;
            btnRecalcularRibbon.Text = "Recalcular";
            btnRecalcularRibbon.TextImageRelation = TextImageRelation.ImageBeforeText;
            toolTipRibbon.SetToolTip(btnRecalcularRibbon, "Recalcular el módulo activo");
            btnRecalcularRibbon.UseVisualStyleBackColor = false;
            btnRecalcularRibbon.Click += btnRecalcularRibbon_Click;
            // 
            // btnBuscarRibbon
            // 
            btnBuscarRibbon.FlatAppearance.BorderSize = 0;
            btnBuscarRibbon.FlatStyle = FlatStyle.Flat;
            btnBuscarRibbon.Font = new Font("Segoe UI", 8.5F);
            btnBuscarRibbon.ForeColor = Color.Silver;
            btnBuscarRibbon.Image = (Image)resources.GetObject("btnBuscarRibbon.Image");
            btnBuscarRibbon.ImageAlign = ContentAlignment.MiddleLeft;
            btnBuscarRibbon.Location = new Point(501, 39);
            btnBuscarRibbon.Name = "btnBuscarRibbon";
            btnBuscarRibbon.Padding = new Padding(8, 0, 10, 0);
            btnBuscarRibbon.Size = new Size(104, 24);
            btnBuscarRibbon.SoproContentPadding = new Padding(8, 0, 10, 0);
            btnBuscarRibbon.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnBuscarRibbon.SoproIcon = Helpers.SoproIconType.Buscar;
            btnBuscarRibbon.SoproMinimumAutoWidth = 74;
            btnBuscarRibbon.TabIndex = 13;
            btnBuscarRibbon.Text = "Buscar";
            btnBuscarRibbon.TextImageRelation = TextImageRelation.ImageBeforeText;
            toolTipRibbon.SetToolTip(btnBuscarRibbon, "Buscar en el grid activo (Ctrl+B, F3 para siguiente)");
            btnBuscarRibbon.UseVisualStyleBackColor = false;
            btnBuscarRibbon.Click += btnBuscarRibbon_Click;
            // 
            // btnWrapRibbon
            // 
            btnWrapRibbon.FlatAppearance.BorderSize = 0;
            btnWrapRibbon.FlatStyle = FlatStyle.Flat;
            btnWrapRibbon.Font = new Font("Segoe UI", 8.5F);
            btnWrapRibbon.ForeColor = Color.Silver;
            btnWrapRibbon.Image = (Image)resources.GetObject("btnWrapRibbon.Image");
            btnWrapRibbon.ImageAlign = ContentAlignment.MiddleLeft;
            btnWrapRibbon.Location = new Point(501, 11);
            btnWrapRibbon.Name = "btnWrapRibbon";
            btnWrapRibbon.Padding = new Padding(8, 0, 10, 0);
            btnWrapRibbon.Size = new Size(104, 24);
            btnWrapRibbon.SoproContentPadding = new Padding(8, 0, 10, 0);
            btnWrapRibbon.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnWrapRibbon.SoproIcon = Helpers.SoproIconType.AjustarTexto;
            btnWrapRibbon.SoproMinimumAutoWidth = 86;
            btnWrapRibbon.TabIndex = 14;
            btnWrapRibbon.Text = "Ajustar";
            btnWrapRibbon.TextImageRelation = TextImageRelation.ImageBeforeText;
            toolTipRibbon.SetToolTip(btnWrapRibbon, "Ajustar texto de la columna seleccionada y recalcular altura de filas");
            btnWrapRibbon.UseVisualStyleBackColor = false;
            btnWrapRibbon.Click += btnWrapRibbon_Click;
            // 
            // lblSepReporte
            // 
            lblSepReporte.BackColor = Color.FromArgb(200, 200, 200);
            lblSepReporte.Location = new Point(738, 8);
            lblSepReporte.Name = "lblSepReporte";
            lblSepReporte.Size = new Size(1, 59);
            lblSepReporte.TabIndex = 20;
            // 
            // btnConsolidarInsumos
            // 
            btnConsolidarInsumos.FlatAppearance.BorderSize = 0;
            btnConsolidarInsumos.FlatStyle = FlatStyle.Flat;
            btnConsolidarInsumos.Font = new Font("Segoe UI", 8.5F);
            btnConsolidarInsumos.ForeColor = Color.Silver;
            btnConsolidarInsumos.Image = (Image)resources.GetObject("btnConsolidarInsumos.Image");
            btnConsolidarInsumos.ImageAlign = ContentAlignment.MiddleLeft;
            btnConsolidarInsumos.Location = new Point(744, 39);
            btnConsolidarInsumos.Name = "btnConsolidarInsumos";
            btnConsolidarInsumos.Padding = new Padding(8, 0, 10, 0);
            btnConsolidarInsumos.Size = new Size(107, 24);
            btnConsolidarInsumos.SoproContentPadding = new Padding(8, 0, 10, 0);
            btnConsolidarInsumos.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnConsolidarInsumos.SoproIcon = Helpers.SoproIconType.Consolidar;
            btnConsolidarInsumos.SoproMinimumAutoWidth = 86;
            btnConsolidarInsumos.TabIndex = 29;
            btnConsolidarInsumos.Text = "Consolidar";
            btnConsolidarInsumos.TextImageRelation = TextImageRelation.ImageBeforeText;
            toolTipRibbon.SetToolTip(btnConsolidarInsumos, "Consolidar insumos seleccionados en el catálogo activo");
            btnConsolidarInsumos.UseVisualStyleBackColor = false;
            btnConsolidarInsumos.Click += btnConsolidarInsumos_Click;
            // 
            // btnAplicarATodas
            // 
            btnAplicarATodas.FlatAppearance.BorderSize = 0;
            btnAplicarATodas.FlatStyle = FlatStyle.Flat;
            btnAplicarATodas.Font = new Font("Segoe UI", 8.5F);
            btnAplicarATodas.ForeColor = Color.Silver;
            btnAplicarATodas.Image = (Image)resources.GetObject("btnAplicarATodas.Image");
            btnAplicarATodas.ImageAlign = ContentAlignment.MiddleLeft;
            btnAplicarATodas.Location = new Point(744, 11);
            btnAplicarATodas.Name = "btnAplicarATodas";
            btnAplicarATodas.Padding = new Padding(8, 0, 10, 0);
            btnAplicarATodas.Size = new Size(107, 24);
            btnAplicarATodas.SoproContentPadding = new Padding(8, 0, 10, 0);
            btnAplicarATodas.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnAplicarATodas.SoproIcon = Helpers.SoproIconType.AplicarATodas;
            btnAplicarATodas.SoproMinimumAutoWidth = 86;
            btnAplicarATodas.TabIndex = 17;
            btnAplicarATodas.Text = "Aplicar";
            btnAplicarATodas.TextImageRelation = TextImageRelation.ImageBeforeText;
            toolTipRibbon.SetToolTip(btnAplicarATodas, "Aplicar fuente, tamano, estilo y color de texto a todas las columnas");
            btnAplicarATodas.UseVisualStyleBackColor = false;
            btnAplicarATodas.Click += btnAplicarATodas_Click;
            // 
            // lblSepGlobal
            // 
            lblSepGlobal.BackColor = Color.FromArgb(200, 200, 200);
            lblSepGlobal.Location = new Point(495, 8);
            lblSepGlobal.Name = "lblSepGlobal";
            lblSepGlobal.Size = new Size(1, 59);
            lblSepGlobal.TabIndex = 22;
            // 
            // btnColorTexto
            // 
            btnColorTexto.BackColor = Color.Black;
            btnColorTexto.FlatAppearance.BorderSize = 0;
            btnColorTexto.FlatStyle = FlatStyle.Flat;
            btnColorTexto.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnColorTexto.ForeColor = Color.White;
            btnColorTexto.Location = new Point(294, 38);
            btnColorTexto.Name = "btnColorTexto";
            btnColorTexto.Size = new Size(28, 28);
            btnColorTexto.TabIndex = 11;
            btnColorTexto.Text = "A";
            toolTipRibbon.SetToolTip(btnColorTexto, "Color de texto");
            btnColorTexto.UseVisualStyleBackColor = false;
            // 
            // btnColorFondo
            // 
            btnColorFondo.BackColor = Color.White;
            btnColorFondo.FlatAppearance.BorderSize = 0;
            btnColorFondo.FlatStyle = FlatStyle.Flat;
            btnColorFondo.ForeColor = Color.Black;
            btnColorFondo.Location = new Point(262, 38);
            btnColorFondo.Name = "btnColorFondo";
            btnColorFondo.Size = new Size(28, 28);
            btnColorFondo.TabIndex = 10;
            btnColorFondo.Text = "⬜";
            toolTipRibbon.SetToolTip(btnColorFondo, "Color de fondo de celda");
            btnColorFondo.UseVisualStyleBackColor = false;
            // 
            // lblSepAlin
            // 
            lblSepAlin.BackColor = Color.FromArgb(200, 200, 200);
            lblSepAlin.Location = new Point(255, 37);
            lblSepAlin.Name = "lblSepAlin";
            lblSepAlin.Size = new Size(1, 30);
            lblSepAlin.TabIndex = 23;
            // 
            // btnAlinAba
            // 
            btnAlinAba.FlatAppearance.BorderSize = 0;
            btnAlinAba.FlatStyle = FlatStyle.Flat;
            btnAlinAba.Font = new Font("Segoe UI", 9F);
            btnAlinAba.ForeColor = Color.Silver;
            btnAlinAba.Image = (Image)resources.GetObject("btnAlinAba.Image");
            btnAlinAba.Location = new Point(457, 9);
            btnAlinAba.Name = "btnAlinAba";
            btnAlinAba.Size = new Size(28, 28);
            btnAlinAba.SoproContentPadding = new Padding(0);
            btnAlinAba.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnAlinAba.SoproIcon = Helpers.SoproIconType.AlinearAbajo;
            btnAlinAba.SoproIconSize = 15;
            btnAlinAba.SoproShowText = false;
            btnAlinAba.TabIndex = 9;
            toolTipRibbon.SetToolTip(btnAlinAba, "Alinear abajo");
            btnAlinAba.UseVisualStyleBackColor = false;
            // 
            // btnAlinMed
            // 
            btnAlinMed.FlatAppearance.BorderSize = 0;
            btnAlinMed.FlatStyle = FlatStyle.Flat;
            btnAlinMed.Font = new Font("Segoe UI", 9F);
            btnAlinMed.ForeColor = Color.Silver;
            btnAlinMed.Image = (Image)resources.GetObject("btnAlinMed.Image");
            btnAlinMed.Location = new Point(425, 9);
            btnAlinMed.Name = "btnAlinMed";
            btnAlinMed.Size = new Size(28, 28);
            btnAlinMed.SoproContentPadding = new Padding(0);
            btnAlinMed.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnAlinMed.SoproIcon = Helpers.SoproIconType.AlinearMedio;
            btnAlinMed.SoproIconSize = 15;
            btnAlinMed.SoproShowText = false;
            btnAlinMed.TabIndex = 8;
            toolTipRibbon.SetToolTip(btnAlinMed, "Alinear al medio");
            btnAlinMed.UseVisualStyleBackColor = false;
            // 
            // btnAlinJus
            // 
            btnAlinJus.FlatAppearance.BorderSize = 0;
            btnAlinJus.FlatStyle = FlatStyle.Flat;
            btnAlinJus.Font = new Font("Segoe UI", 9F);
            btnAlinJus.ForeColor = Color.Silver;
            btnAlinJus.Image = (Image)resources.GetObject("btnAlinJus.Image");
            btnAlinJus.Location = new Point(393, 9);
            btnAlinJus.Name = "btnAlinJus";
            btnAlinJus.Size = new Size(28, 28);
            btnAlinJus.SoproContentPadding = new Padding(0);
            btnAlinJus.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnAlinJus.SoproIcon = Helpers.SoproIconType.AlinearArriba;
            btnAlinJus.SoproIconSize = 15;
            btnAlinJus.SoproShowText = false;
            btnAlinJus.TabIndex = 7;
            toolTipRibbon.SetToolTip(btnAlinJus, "Alinear arriba");
            btnAlinJus.UseVisualStyleBackColor = false;
            // 
            // btnAlinDer
            // 
            btnAlinDer.FlatAppearance.BorderSize = 0;
            btnAlinDer.FlatStyle = FlatStyle.Flat;
            btnAlinDer.Font = new Font("Segoe UI", 9F);
            btnAlinDer.ForeColor = Color.Silver;
            btnAlinDer.Image = (Image)resources.GetObject("btnAlinDer.Image");
            btnAlinDer.Location = new Point(457, 38);
            btnAlinDer.Name = "btnAlinDer";
            btnAlinDer.Size = new Size(28, 28);
            btnAlinDer.SoproContentPadding = new Padding(0);
            btnAlinDer.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnAlinDer.SoproIcon = Helpers.SoproIconType.AlinearDerecha;
            btnAlinDer.SoproIconSize = 15;
            btnAlinDer.SoproShowText = false;
            btnAlinDer.TabIndex = 6;
            toolTipRibbon.SetToolTip(btnAlinDer, "Alinear a la derecha");
            btnAlinDer.UseVisualStyleBackColor = false;
            // 
            // btnAlinCen
            // 
            btnAlinCen.FlatAppearance.BorderSize = 0;
            btnAlinCen.FlatStyle = FlatStyle.Flat;
            btnAlinCen.Font = new Font("Segoe UI", 9F);
            btnAlinCen.ForeColor = Color.Silver;
            btnAlinCen.Image = (Image)resources.GetObject("btnAlinCen.Image");
            btnAlinCen.Location = new Point(425, 38);
            btnAlinCen.Name = "btnAlinCen";
            btnAlinCen.Size = new Size(28, 28);
            btnAlinCen.SoproContentPadding = new Padding(0);
            btnAlinCen.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnAlinCen.SoproIcon = Helpers.SoproIconType.AlinearCentro;
            btnAlinCen.SoproIconSize = 15;
            btnAlinCen.SoproShowText = false;
            btnAlinCen.TabIndex = 5;
            toolTipRibbon.SetToolTip(btnAlinCen, "Centrar");
            btnAlinCen.UseVisualStyleBackColor = false;
            // 
            // btnAlinIzq
            // 
            btnAlinIzq.FlatAppearance.BorderSize = 0;
            btnAlinIzq.FlatStyle = FlatStyle.Flat;
            btnAlinIzq.Font = new Font("Segoe UI", 9F);
            btnAlinIzq.ForeColor = Color.Silver;
            btnAlinIzq.Image = (Image)resources.GetObject("btnAlinIzq.Image");
            btnAlinIzq.Location = new Point(393, 38);
            btnAlinIzq.Name = "btnAlinIzq";
            btnAlinIzq.Size = new Size(28, 28);
            btnAlinIzq.SoproContentPadding = new Padding(0);
            btnAlinIzq.SoproDisabledIconColor = Color.FromArgb(120, 120, 120);
            btnAlinIzq.SoproIcon = Helpers.SoproIconType.AlinearIzquierda;
            btnAlinIzq.SoproIconSize = 15;
            btnAlinIzq.SoproShowText = false;
            btnAlinIzq.TabIndex = 4;
            toolTipRibbon.SetToolTip(btnAlinIzq, "Alinear a la izquierda");
            btnAlinIzq.UseVisualStyleBackColor = false;
            // 
            // lblSepEstilo
            // 
            lblSepEstilo.BackColor = Color.FromArgb(200, 200, 200);
            lblSepEstilo.Location = new Point(386, 8);
            lblSepEstilo.Name = "lblSepEstilo";
            lblSepEstilo.Size = new Size(1, 59);
            lblSepEstilo.TabIndex = 24;
            // 
            // btnCursiva
            // 
            btnCursiva.FlatAppearance.BorderColor = Color.FromArgb(245, 245, 248);
            btnCursiva.FlatAppearance.BorderSize = 0;
            btnCursiva.FlatStyle = FlatStyle.Flat;
            btnCursiva.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            btnCursiva.ForeColor = Color.Silver;
            btnCursiva.Location = new Point(198, 38);
            btnCursiva.Name = "btnCursiva";
            btnCursiva.Size = new Size(28, 28);
            btnCursiva.TabIndex = 3;
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
            btnNegrita.ForeColor = Color.Silver;
            btnNegrita.Location = new Point(166, 38);
            btnNegrita.Name = "btnNegrita";
            btnNegrita.Size = new Size(28, 28);
            btnNegrita.TabIndex = 2;
            btnNegrita.Text = "N";
            toolTipRibbon.SetToolTip(btnNegrita, "Negrita");
            btnNegrita.UseVisualStyleBackColor = true;
            // 
            // nudTamano
            // 
            nudTamano.Font = new Font("Segoe UI", 9F);
            nudTamano.Location = new Point(328, 11);
            nudTamano.Maximum = new decimal(new int[] { 24, 0, 0, 0 });
            nudTamano.Minimum = new decimal(new int[] { 6, 0, 0, 0 });
            nudTamano.Name = "nudTamano";
            nudTamano.Size = new Size(48, 23);
            nudTamano.TabIndex = 1;
            toolTipRibbon.SetToolTip(nudTamano, "Tamano de fuente");
            nudTamano.Value = new decimal(new int[] { 9, 0, 0, 0 });
            // 
            // cboFuente
            // 
            cboFuente.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFuente.Font = new Font("Segoe UI", 9F);
            cboFuente.Location = new Point(162, 11);
            cboFuente.Name = "cboFuente";
            cboFuente.Size = new Size(160, 23);
            cboFuente.TabIndex = 0;
            toolTipRibbon.SetToolTip(cboFuente, "Tipo de fuente");
            // 
            // lblSepFuente
            // 
            lblSepFuente.BackColor = Color.FromArgb(200, 200, 200);
            lblSepFuente.Location = new Point(156, 8);
            lblSepFuente.Name = "lblSepFuente";
            lblSepFuente.Size = new Size(1, 59);
            lblSepFuente.TabIndex = 25;
            // 
            // lblColumna
            // 
            lblColumna.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblColumna.ForeColor = Color.Gray;
            lblColumna.Location = new Point(8, 14);
            lblColumna.Name = "lblColumna";
            lblColumna.Size = new Size(140, 18);
            lblColumna.TabIndex = 26;
            lblColumna.Text = "-- sin seleccion --";
            lblColumna.TextAlign = ContentAlignment.MiddleLeft;
            toolTipRibbon.SetToolTip(lblColumna, "Columna seleccionada actualmente");
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(20, 10);
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
            tabControl.Location = new Point(250, 80);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1350, 716);
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

        private Label label1;
        private Label label2;
    }
}
