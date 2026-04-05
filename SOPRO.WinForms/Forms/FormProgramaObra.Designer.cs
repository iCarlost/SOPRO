namespace SOPRO.WinForms.Forms
{
    partial class FormProgramaObra
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitulo;
        private System.Windows.Forms.Label lblSubtitulo;
        private System.Windows.Forms.ToolStrip toolPrograma;
        private System.Windows.Forms.ToolStripButton btnRecalcular;
        private System.Windows.Forms.ToolStripButton btnCalendario;
        private System.Windows.Forms.ToolStripButton btnSincronizar;
        private System.Windows.Forms.ToolStripButton btnReconstruir;
        private System.Windows.Forms.ToolStripButton btnBorrarPrograma;
        private System.Windows.Forms.ToolStripLabel lblTipoPeriodo;
        private System.Windows.Forms.ToolStripComboBox cmbTipoPeriodo;
        private System.Windows.Forms.ToolStripLabel lblVistaCurva;
        private System.Windows.Forms.ToolStripComboBox cmbVistaCurva;
        private System.Windows.Forms.ToolStripLabel lblPieGantt;
        private System.Windows.Forms.ToolStripComboBox cmbPieGantt;
        private System.Windows.Forms.ToolStripLabel lblEtiquetaSegmentoGantt;
        private System.Windows.Forms.ToolStripComboBox cmbEtiquetaSegmentoGantt;
        private System.Windows.Forms.ToolStripButton btnToggleDetalle;
        private System.Windows.Forms.ToolStripButton btnConfigColumnas;
        private System.Windows.Forms.ToolStripButton btnCerrar;
        private System.Windows.Forms.SplitContainer splitPrincipal;
        private System.Windows.Forms.SplitContainer splitActividadesGantt;
        private System.Windows.Forms.DataGridView dgvActividades;
        private SOPRO.WinForms.Controls.GanttTimelineControl ganttTimeline;
        private System.Windows.Forms.TabControl tabPrograma;
        private System.Windows.Forms.TabPage tabPeriodos;
        private System.Windows.Forms.TabPage tabDistribucion;
        private System.Windows.Forms.TabPage tabDependencias;
        private System.Windows.Forms.DataGridView dgvPeriodos;
        private System.Windows.Forms.DataGridView dgvDistribucion;
        private System.Windows.Forms.DataGridView dgvDependencias;
        private System.Windows.Forms.Label lblEstado;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOrden;
        private System.Windows.Forms.DataGridViewTextBoxColumn colClave;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDescripcion;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPredecesora;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUnidad;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCantidad;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFechaInicio;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFechaFin;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDuracionDias;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRendimientoDiario;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFrentes;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrecioUnitario;
        private System.Windows.Forms.DataGridViewTextBoxColumn colImporte;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colRutaCritica;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeriodoNumero;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeriodoEtiqueta;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeriodoFechaInicio;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeriodoFechaFin;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colPeriodoCerrado;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDistNumero;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDistPeriodo;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDistFechaInicio;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDistFechaFin;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDistCantidad;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDistPorcentaje;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDistImporte;
        private System.Windows.Forms.DataGridViewComboBoxColumn colDepClave;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDepDescripcion;
        private System.Windows.Forms.DataGridViewComboBoxColumn colDepTipo;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDepLag;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            Models.GanttVisualSettings ganttVisualSettings1 = new Models.GanttVisualSettings();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle7 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle8 = new DataGridViewCellStyle();
            panelTop = new Panel();
            lblSubtitulo = new Label();
            lblTitulo = new Label();
            toolPrograma = new ToolStrip();
            btnRecalcular = new ToolStripButton();
            btnCalendario = new ToolStripButton();
            btnSincronizar = new ToolStripButton();
            btnReconstruir = new ToolStripButton();
            btnBorrarPrograma = new ToolStripButton();
            lblTipoPeriodo = new ToolStripLabel();
            cmbTipoPeriodo = new ToolStripComboBox();
            lblVistaCurva = new ToolStripLabel();
            cmbVistaCurva = new ToolStripComboBox();
            lblPieGantt = new ToolStripLabel();
            cmbPieGantt = new ToolStripComboBox();
            lblEtiquetaSegmentoGantt = new ToolStripLabel();
            cmbEtiquetaSegmentoGantt = new ToolStripComboBox();
            btnToggleDetalle = new ToolStripButton();
            btnConfigColumnas = new ToolStripButton();
            btnCerrar = new ToolStripButton();
            splitPrincipal = new SplitContainer();
            splitActividadesGantt = new SplitContainer();
            dgvActividades = new DataGridView();
            colOrden = new DataGridViewTextBoxColumn();
            colClave = new DataGridViewTextBoxColumn();
            colDescripcion = new DataGridViewTextBoxColumn();
            colUnidad = new DataGridViewTextBoxColumn();
            colPredecesora = new DataGridViewTextBoxColumn();
            colCantidad = new DataGridViewTextBoxColumn();
            colFechaInicio = new DataGridViewTextBoxColumn();
            colFechaFin = new DataGridViewTextBoxColumn();
            colDuracionDias = new DataGridViewTextBoxColumn();
            colRendimientoDiario = new DataGridViewTextBoxColumn();
            colFrentes = new DataGridViewTextBoxColumn();
            colPrecioUnitario = new DataGridViewTextBoxColumn();
            colImporte = new DataGridViewTextBoxColumn();
            colRutaCritica = new DataGridViewCheckBoxColumn();
            ganttTimeline = new SOPRO.WinForms.Controls.GanttTimelineControl();
            tabPrograma = new TabControl();
            tabPeriodos = new TabPage();
            dgvPeriodos = new DataGridView();
            colPeriodoNumero = new DataGridViewTextBoxColumn();
            colPeriodoEtiqueta = new DataGridViewTextBoxColumn();
            colPeriodoFechaInicio = new DataGridViewTextBoxColumn();
            colPeriodoFechaFin = new DataGridViewTextBoxColumn();
            colPeriodoCerrado = new DataGridViewCheckBoxColumn();
            tabDistribucion = new TabPage();
            dgvDistribucion = new DataGridView();
            colDistNumero = new DataGridViewTextBoxColumn();
            colDistPeriodo = new DataGridViewTextBoxColumn();
            colDistFechaInicio = new DataGridViewTextBoxColumn();
            colDistFechaFin = new DataGridViewTextBoxColumn();
            colDistCantidad = new DataGridViewTextBoxColumn();
            colDistPorcentaje = new DataGridViewTextBoxColumn();
            colDistImporte = new DataGridViewTextBoxColumn();
            tabDependencias = new TabPage();
            dgvDependencias = new DataGridView();
            colDepClave = new DataGridViewComboBoxColumn();
            colDepDescripcion = new DataGridViewTextBoxColumn();
            colDepTipo = new DataGridViewComboBoxColumn();
            colDepLag = new DataGridViewTextBoxColumn();
            lblEstado = new Label();
            panelTop.SuspendLayout();
            toolPrograma.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitPrincipal).BeginInit();
            splitPrincipal.Panel1.SuspendLayout();
            splitPrincipal.Panel2.SuspendLayout();
            splitPrincipal.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitActividadesGantt).BeginInit();
            splitActividadesGantt.Panel1.SuspendLayout();
            splitActividadesGantt.Panel2.SuspendLayout();
            splitActividadesGantt.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvActividades).BeginInit();
            tabPrograma.SuspendLayout();
            tabPeriodos.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPeriodos).BeginInit();
            tabDistribucion.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvDistribucion).BeginInit();
            tabDependencias.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvDependencias).BeginInit();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(51, 51, 76);
            panelTop.Controls.Add(lblSubtitulo);
            panelTop.Controls.Add(lblTitulo);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(1384, 68);
            panelTop.TabIndex = 0;
            // 
            // lblSubtitulo
            // 
            lblSubtitulo.AutoSize = true;
            lblSubtitulo.Font = new Font("Segoe UI", 9F);
            lblSubtitulo.ForeColor = Color.WhiteSmoke;
            lblSubtitulo.Location = new Point(18, 42);
            lblSubtitulo.Name = "lblSubtitulo";
            lblSubtitulo.Size = new Size(54, 15);
            lblSubtitulo.TabIndex = 1;
            lblSubtitulo.Text = "Proyecto";
            // 
            // lblTitulo
            // 
            lblTitulo.AutoSize = true;
            lblTitulo.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitulo.ForeColor = Color.White;
            lblTitulo.Location = new Point(12, 9);
            lblTitulo.Name = "lblTitulo";
            lblTitulo.Size = new Size(226, 32);
            lblTitulo.TabIndex = 0;
            lblTitulo.Text = "PROGRAMA OBRA";
            // 
            // toolPrograma
            // 
            toolPrograma.GripStyle = ToolStripGripStyle.Hidden;
            toolPrograma.ImageScalingSize = new Size(20, 20);
            toolPrograma.Items.AddRange(new ToolStripItem[] { btnRecalcular, btnCalendario, btnSincronizar, btnReconstruir, btnBorrarPrograma, lblTipoPeriodo, cmbTipoPeriodo, lblVistaCurva, cmbVistaCurva, lblPieGantt, cmbPieGantt, lblEtiquetaSegmentoGantt, cmbEtiquetaSegmentoGantt, btnToggleDetalle, btnConfigColumnas, btnCerrar });
            toolPrograma.Location = new Point(0, 68);
            toolPrograma.Name = "toolPrograma";
            toolPrograma.Padding = new Padding(8, 4, 8, 4);
            toolPrograma.Size = new Size(1384, 31);
            toolPrograma.TabIndex = 1;
            toolPrograma.Text = "toolStrip1";
            // 
            // btnRecalcular
            // 
            btnRecalcular.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnRecalcular.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnRecalcular.Name = "btnRecalcular";
            btnRecalcular.Size = new Size(84, 20);
            btnRecalcular.Text = "\U0001f9ee Recalcular";
            btnRecalcular.Click += btnRecalcular_Click;
            // 
            // btnCalendario
            // 
            btnCalendario.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnCalendario.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCalendario.Name = "btnCalendario";
            btnCalendario.Size = new Size(85, 20);
            btnCalendario.Text = "📅 Calendario";
            btnCalendario.Click += btnCalendario_Click;
            // 
            // btnSincronizar
            // 
            btnSincronizar.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnSincronizar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSincronizar.Name = "btnSincronizar";
            btnSincronizar.Size = new Size(89, 20);
            btnSincronizar.Text = "🔁 Sincronizar";
            btnSincronizar.Click += btnSincronizar_Click;
            // 
            // btnReconstruir
            // 
            btnReconstruir.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnReconstruir.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnReconstruir.Name = "btnReconstruir";
            btnReconstruir.Size = new Size(92, 20);
            btnReconstruir.Text = "♻ Reconstruir";
            btnReconstruir.Click += btnReconstruir_Click;
            // 
            // btnBorrarPrograma
            // 
            btnBorrarPrograma.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnBorrarPrograma.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnBorrarPrograma.Name = "btnBorrarPrograma";
            btnBorrarPrograma.Size = new Size(63, 20);
            btnBorrarPrograma.Text = "🗑 Borrar";
            btnBorrarPrograma.Click += btnBorrarPrograma_Click;
            // 
            // lblTipoPeriodo
            // 
            lblTipoPeriodo.Name = "lblTipoPeriodo";
            lblTipoPeriodo.Size = new Size(51, 20);
            lblTipoPeriodo.Text = "Periodo:";
            // 
            // cmbTipoPeriodo
            // 
            cmbTipoPeriodo.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTipoPeriodo.Name = "cmbTipoPeriodo";
            cmbTipoPeriodo.Size = new Size(121, 23);
            cmbTipoPeriodo.SelectedIndexChanged += cmbTipoPeriodo_SelectedIndexChanged;
            // 
            // lblVistaCurva
            // 
            lblVistaCurva.Name = "lblVistaCurva";
            lblVistaCurva.Size = new Size(35, 20);
            lblVistaCurva.Text = "Vista:";
            // 
            // cmbVistaCurva
            // 
            cmbVistaCurva.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbVistaCurva.Name = "cmbVistaCurva";
            cmbVistaCurva.Size = new Size(110, 23);
            cmbVistaCurva.SelectedIndexChanged += cmbVistaCurva_SelectedIndexChanged;
            // 
            // lblPieGantt
            // 
            lblPieGantt.Name = "lblPieGantt";
            lblPieGantt.Size = new Size(58, 20);
            lblPieGantt.Text = "Pie Gantt:";
            // 
            // cmbPieGantt
            // 
            cmbPieGantt.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPieGantt.Name = "cmbPieGantt";
            cmbPieGantt.Size = new Size(135, 23);
            cmbPieGantt.SelectedIndexChanged += cmbPieGantt_SelectedIndexChanged;
            // 
            // lblEtiquetaSegmentoGantt
            // 
            lblEtiquetaSegmentoGantt.Name = "lblEtiquetaSegmentoGantt";
            lblEtiquetaSegmentoGantt.Size = new Size(51, 20);
            lblEtiquetaSegmentoGantt.Text = "Montos:";
            // 
            // cmbEtiquetaSegmentoGantt
            // 
            cmbEtiquetaSegmentoGantt.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbEtiquetaSegmentoGantt.Name = "cmbEtiquetaSegmentoGantt";
            cmbEtiquetaSegmentoGantt.Size = new Size(90, 23);
            cmbEtiquetaSegmentoGantt.SelectedIndexChanged += cmbEtiquetaSegmentoGantt_SelectedIndexChanged;
            // 
            // btnToggleDetalle
            // 
            btnToggleDetalle.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnToggleDetalle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnToggleDetalle.Name = "btnToggleDetalle";
            btnToggleDetalle.Size = new Size(65, 20);
            btnToggleDetalle.Text = "▾ Detalle";
            btnToggleDetalle.Click += btnToggleDetalle_Click;
            // 
            // btnConfigColumnas
            // 
            btnConfigColumnas.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnConfigColumnas.Name = "btnConfigColumnas";
            btnConfigColumnas.Size = new Size(80, 20);
            btnConfigColumnas.Text = "📐 Columnas";
            btnConfigColumnas.Click += btnConfigColumnas_Click;
            // 
            // btnCerrar
            // 
            btnCerrar.Alignment = ToolStripItemAlignment.Right;
            btnCerrar.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnCerrar.Name = "btnCerrar";
            btnCerrar.Size = new Size(58, 20);
            btnCerrar.Text = "✖ Cerrar";
            btnCerrar.Click += btnCerrar_Click;
            // 
            // splitPrincipal
            // 
            splitPrincipal.Dock = DockStyle.Fill;
            splitPrincipal.Location = new Point(0, 99);
            splitPrincipal.Name = "splitPrincipal";
            splitPrincipal.Orientation = Orientation.Horizontal;
            // 
            // splitPrincipal.Panel1
            // 
            splitPrincipal.Panel1.Controls.Add(splitActividadesGantt);
            // 
            // splitPrincipal.Panel2
            // 
            splitPrincipal.Panel2.Controls.Add(tabPrograma);
            splitPrincipal.Size = new Size(1384, 632);
            splitPrincipal.SplitterDistance = 357;
            splitPrincipal.TabIndex = 2;
            // 
            // splitActividadesGantt
            // 
            splitActividadesGantt.Dock = DockStyle.Fill;
            splitActividadesGantt.FixedPanel = FixedPanel.Panel2;
            splitActividadesGantt.Location = new Point(0, 0);
            splitActividadesGantt.Name = "splitActividadesGantt";
            // 
            // splitActividadesGantt.Panel1
            // 
            splitActividadesGantt.Panel1.Controls.Add(dgvActividades);
            splitActividadesGantt.Panel1MinSize = 320;
            // 
            // splitActividadesGantt.Panel2
            // 
            splitActividadesGantt.Panel2.Controls.Add(ganttTimeline);
            splitActividadesGantt.Panel2MinSize = 260;
            splitActividadesGantt.Size = new Size(1384, 357);
            splitActividadesGantt.SplitterDistance = 885;
            splitActividadesGantt.TabIndex = 0;
            // 
            // dgvActividades
            // 
            dgvActividades.AllowUserToAddRows = false;
            dgvActividades.AllowUserToDeleteRows = false;
            dgvActividades.AllowUserToResizeRows = false;
            dgvActividades.BackgroundColor = Color.White;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvActividades.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvActividades.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvActividades.Columns.AddRange(new DataGridViewColumn[] { colOrden, colClave, colDescripcion, colUnidad, colPredecesora, colCantidad, colFechaInicio, colFechaFin, colDuracionDias, colRendimientoDiario, colFrentes, colPrecioUnitario, colImporte, colRutaCritica });
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dgvActividades.DefaultCellStyle = dataGridViewCellStyle2;
            dgvActividades.Dock = DockStyle.Fill;
            dgvActividades.Location = new Point(0, 0);
            dgvActividades.Name = "dgvActividades";
            dgvActividades.RowHeadersVisible = false;
            dgvActividades.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvActividades.Size = new Size(885, 357);
            dgvActividades.TabIndex = 0;
            dgvActividades.CellEndEdit += dgvActividades_CellEndEdit;
            dgvActividades.SelectionChanged += dgvActividades_SelectionChanged;
            // 
            // colOrden
            // 
            colOrden.DataPropertyName = "Orden";
            colOrden.HeaderText = "Orden";
            colOrden.Name = "colOrden";
            colOrden.ReadOnly = true;
            colOrden.Width = 55;
            // 
            // colClave
            // 
            colClave.DataPropertyName = "Clave";
            colClave.HeaderText = "Clave";
            colClave.Name = "colClave";
            colClave.ReadOnly = true;
            colClave.Width = 90;
            // 
            // colDescripcion
            // 
            colDescripcion.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colDescripcion.DataPropertyName = "Descripcion";
            colDescripcion.HeaderText = "Descripción";
            colDescripcion.Name = "colDescripcion";
            colDescripcion.ReadOnly = true;
            colDescripcion.Width = 360;
            // 
            // colUnidad
            // 
            colUnidad.DataPropertyName = "Unidad";
            colUnidad.HeaderText = "Unidad";
            colUnidad.Name = "colUnidad";
            colUnidad.ReadOnly = true;
            colUnidad.Width = 75;
            // 
            // colPredecesora
            // 
            colPredecesora.DataPropertyName = "PredecesoraResumen";
            colPredecesora.HeaderText = "Predecesora";
            colPredecesora.Name = "colPredecesora";
            colPredecesora.ReadOnly = true;
            colPredecesora.Width = 150;
            // 
            // colCantidad
            // 
            colCantidad.DataPropertyName = "CantidadTotal";
            colCantidad.HeaderText = "Cantidad";
            colCantidad.Name = "colCantidad";
            colCantidad.ReadOnly = true;
            colCantidad.Width = 90;
            // 
            // colFechaInicio
            // 
            colFechaInicio.DataPropertyName = "FechaInicioProgramada";
            colFechaInicio.HeaderText = "Inicio";
            colFechaInicio.Name = "colFechaInicio";
            colFechaInicio.Width = 95;
            // 
            // colFechaFin
            // 
            colFechaFin.DataPropertyName = "FechaFinProgramada";
            colFechaFin.HeaderText = "Fin";
            colFechaFin.Name = "colFechaFin";
            colFechaFin.Width = 95;
            // 
            // colDuracionDias
            // 
            colDuracionDias.DataPropertyName = "DuracionDiasHabiles";
            colDuracionDias.HeaderText = "Días hábiles";
            colDuracionDias.Name = "colDuracionDias";
            colDuracionDias.Width = 95;
            // 
            // colRendimientoDiario
            // 
            colRendimientoDiario.DataPropertyName = "RendimientoDiario";
            colRendimientoDiario.HeaderText = "Rend. diario";
            colRendimientoDiario.Name = "colRendimientoDiario";
            colRendimientoDiario.Width = 95;
            // 
            // colFrentes
            // 
            colFrentes.DataPropertyName = "FrentesTrabajo";
            colFrentes.HeaderText = "Frentes";
            colFrentes.Name = "colFrentes";
            colFrentes.Width = 70;
            // 
            // colPrecioUnitario
            // 
            colPrecioUnitario.DataPropertyName = "PrecioUnitario";
            colPrecioUnitario.HeaderText = "P.U.";
            colPrecioUnitario.Name = "colPrecioUnitario";
            colPrecioUnitario.ReadOnly = true;
            colPrecioUnitario.Width = 90;
            // 
            // colImporte
            // 
            colImporte.DataPropertyName = "ImporteTotal";
            colImporte.HeaderText = "Importe";
            colImporte.Name = "colImporte";
            colImporte.ReadOnly = true;
            colImporte.Width = 110;
            // 
            // colRutaCritica
            // 
            colRutaCritica.DataPropertyName = "RutaCritica";
            colRutaCritica.HeaderText = "Crítica";
            colRutaCritica.Name = "colRutaCritica";
            colRutaCritica.ReadOnly = true;
            colRutaCritica.Width = 60;
            // 
            // ganttTimeline
            // 
            ganttTimeline.AutoScroll = true;
            ganttTimeline.BackColor = Color.White;
            ganttTimeline.Dock = DockStyle.Fill;
            ganttTimeline.Font = new Font("Segoe UI", 8.5F);
            ganttTimeline.FooterDisplayMode = Application.DTOs.Programacion.GanttFooterDisplayMode.Ninguno;
            ganttTimeline.Location = new Point(0, 0);
            ganttTimeline.Name = "ganttTimeline";
            ganttTimeline.RenderModel = null;
            ganttTimeline.SegmentLabelPosition = Application.DTOs.Programacion.GanttSegmentLabelPosition.Arriba;
            ganttTimeline.SelectedActivityId = null;
            ganttTimeline.Size = new Size(495, 357);
            ganttTimeline.TabIndex = 0;
            ganttTimeline.TimelineCellWidth = 64;
            ganttVisualSettings1.CriticalBarColor = Color.FromArgb(231, 76, 60);
            ganttVisualSettings1.FontFamilyName = "Segoe UI";
            ganttVisualSettings1.FontStyle = FontStyle.Regular;
            ganttVisualSettings1.NormalBarColor = Color.FromArgb(52, 152, 219);
            ganttVisualSettings1.OutlineColor = Color.FromArgb(240, 255, 255, 255);
            ganttVisualSettings1.SummaryBarColor = Color.FromArgb(74, 96, 173);
            ganttVisualSettings1.TextColor = Color.FromArgb(64, 64, 64);
            ganttTimeline.VisualSettings = ganttVisualSettings1;
            // 
            // tabPrograma
            // 
            tabPrograma.Controls.Add(tabPeriodos);
            tabPrograma.Controls.Add(tabDistribucion);
            tabPrograma.Controls.Add(tabDependencias);
            tabPrograma.Dock = DockStyle.Fill;
            tabPrograma.Location = new Point(0, 0);
            tabPrograma.Name = "tabPrograma";
            tabPrograma.SelectedIndex = 0;
            tabPrograma.Size = new Size(1384, 271);
            tabPrograma.TabIndex = 0;
            tabPrograma.SelectedIndexChanged += tabPrograma_SelectedIndexChanged;
            // 
            // tabPeriodos
            // 
            tabPeriodos.Controls.Add(dgvPeriodos);
            tabPeriodos.Location = new Point(4, 24);
            tabPeriodos.Name = "tabPeriodos";
            tabPeriodos.Padding = new Padding(3);
            tabPeriodos.Size = new Size(1376, 243);
            tabPeriodos.TabIndex = 0;
            tabPeriodos.Text = "Periodos";
            tabPeriodos.UseVisualStyleBackColor = true;
            // 
            // dgvPeriodos
            // 
            dgvPeriodos.AllowUserToAddRows = false;
            dgvPeriodos.AllowUserToDeleteRows = false;
            dgvPeriodos.AllowUserToResizeRows = false;
            dgvPeriodos.BackgroundColor = Color.White;
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = SystemColors.Control;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle3.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.True;
            dgvPeriodos.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            dgvPeriodos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPeriodos.Columns.AddRange(new DataGridViewColumn[] { colPeriodoNumero, colPeriodoEtiqueta, colPeriodoFechaInicio, colPeriodoFechaFin, colPeriodoCerrado });
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = SystemColors.Window;
            dataGridViewCellStyle4.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle4.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle4.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.False;
            dgvPeriodos.DefaultCellStyle = dataGridViewCellStyle4;
            dgvPeriodos.Dock = DockStyle.Fill;
            dgvPeriodos.Location = new Point(3, 3);
            dgvPeriodos.Name = "dgvPeriodos";
            dgvPeriodos.ReadOnly = true;
            dgvPeriodos.RowHeadersVisible = false;
            dgvPeriodos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPeriodos.Size = new Size(1370, 237);
            dgvPeriodos.TabIndex = 0;
            // 
            // colPeriodoNumero
            // 
            colPeriodoNumero.DataPropertyName = "NumeroPeriodo";
            colPeriodoNumero.HeaderText = "#";
            colPeriodoNumero.Name = "colPeriodoNumero";
            colPeriodoNumero.ReadOnly = true;
            colPeriodoNumero.Width = 50;
            // 
            // colPeriodoEtiqueta
            // 
            colPeriodoEtiqueta.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colPeriodoEtiqueta.DataPropertyName = "Etiqueta";
            colPeriodoEtiqueta.HeaderText = "Periodo";
            colPeriodoEtiqueta.Name = "colPeriodoEtiqueta";
            colPeriodoEtiqueta.ReadOnly = true;
            // 
            // colPeriodoFechaInicio
            // 
            colPeriodoFechaInicio.DataPropertyName = "FechaInicio";
            colPeriodoFechaInicio.HeaderText = "Inicio";
            colPeriodoFechaInicio.Name = "colPeriodoFechaInicio";
            colPeriodoFechaInicio.ReadOnly = true;
            colPeriodoFechaInicio.Width = 95;
            // 
            // colPeriodoFechaFin
            // 
            colPeriodoFechaFin.DataPropertyName = "FechaFin";
            colPeriodoFechaFin.HeaderText = "Fin";
            colPeriodoFechaFin.Name = "colPeriodoFechaFin";
            colPeriodoFechaFin.ReadOnly = true;
            colPeriodoFechaFin.Width = 95;
            // 
            // colPeriodoCerrado
            // 
            colPeriodoCerrado.DataPropertyName = "EsCerrado";
            colPeriodoCerrado.HeaderText = "Cerrado";
            colPeriodoCerrado.Name = "colPeriodoCerrado";
            colPeriodoCerrado.ReadOnly = true;
            colPeriodoCerrado.Width = 70;
            // 
            // tabDistribucion
            // 
            tabDistribucion.Controls.Add(dgvDistribucion);
            tabDistribucion.Location = new Point(4, 24);
            tabDistribucion.Name = "tabDistribucion";
            tabDistribucion.Padding = new Padding(3);
            tabDistribucion.Size = new Size(1376, 243);
            tabDistribucion.TabIndex = 1;
            tabDistribucion.Text = "Distribución";
            tabDistribucion.UseVisualStyleBackColor = true;
            // 
            // dgvDistribucion
            // 
            dgvDistribucion.AllowUserToAddRows = false;
            dgvDistribucion.AllowUserToDeleteRows = false;
            dgvDistribucion.AllowUserToResizeRows = false;
            dgvDistribucion.BackgroundColor = Color.White;
            dataGridViewCellStyle5.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = SystemColors.Control;
            dataGridViewCellStyle5.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle5.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = DataGridViewTriState.True;
            dgvDistribucion.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            dgvDistribucion.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvDistribucion.Columns.AddRange(new DataGridViewColumn[] { colDistNumero, colDistPeriodo, colDistFechaInicio, colDistFechaFin, colDistCantidad, colDistPorcentaje, colDistImporte });
            dataGridViewCellStyle6.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = SystemColors.Window;
            dataGridViewCellStyle6.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle6.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle6.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = DataGridViewTriState.False;
            dgvDistribucion.DefaultCellStyle = dataGridViewCellStyle6;
            dgvDistribucion.Dock = DockStyle.Fill;
            dgvDistribucion.Location = new Point(3, 3);
            dgvDistribucion.Name = "dgvDistribucion";
            dgvDistribucion.ReadOnly = true;
            dgvDistribucion.RowHeadersVisible = false;
            dgvDistribucion.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvDistribucion.Size = new Size(1370, 237);
            dgvDistribucion.TabIndex = 0;
            // 
            // colDistNumero
            // 
            colDistNumero.DataPropertyName = "NumeroPeriodo";
            colDistNumero.HeaderText = "#";
            colDistNumero.Name = "colDistNumero";
            colDistNumero.ReadOnly = true;
            colDistNumero.Width = 45;
            // 
            // colDistPeriodo
            // 
            colDistPeriodo.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colDistPeriodo.DataPropertyName = "Periodo";
            colDistPeriodo.HeaderText = "Periodo";
            colDistPeriodo.Name = "colDistPeriodo";
            colDistPeriodo.ReadOnly = true;
            // 
            // colDistFechaInicio
            // 
            colDistFechaInicio.DataPropertyName = "FechaInicio";
            colDistFechaInicio.HeaderText = "Inicio";
            colDistFechaInicio.Name = "colDistFechaInicio";
            colDistFechaInicio.ReadOnly = true;
            colDistFechaInicio.Width = 95;
            // 
            // colDistFechaFin
            // 
            colDistFechaFin.DataPropertyName = "FechaFin";
            colDistFechaFin.HeaderText = "Fin";
            colDistFechaFin.Name = "colDistFechaFin";
            colDistFechaFin.ReadOnly = true;
            colDistFechaFin.Width = 95;
            // 
            // colDistCantidad
            // 
            colDistCantidad.DataPropertyName = "Cantidad";
            colDistCantidad.HeaderText = "Cantidad";
            colDistCantidad.Name = "colDistCantidad";
            colDistCantidad.ReadOnly = true;
            colDistCantidad.Width = 95;
            // 
            // colDistPorcentaje
            // 
            colDistPorcentaje.DataPropertyName = "Porcentaje";
            colDistPorcentaje.HeaderText = "%";
            colDistPorcentaje.Name = "colDistPorcentaje";
            colDistPorcentaje.ReadOnly = true;
            colDistPorcentaje.Width = 70;
            // 
            // colDistImporte
            // 
            colDistImporte.DataPropertyName = "Importe";
            colDistImporte.HeaderText = "Importe";
            colDistImporte.Name = "colDistImporte";
            colDistImporte.ReadOnly = true;
            colDistImporte.Width = 110;
            // 
            // tabDependencias
            // 
            tabDependencias.Controls.Add(dgvDependencias);
            tabDependencias.Location = new Point(4, 24);
            tabDependencias.Name = "tabDependencias";
            tabDependencias.Padding = new Padding(3);
            tabDependencias.Size = new Size(1376, 243);
            tabDependencias.TabIndex = 2;
            tabDependencias.Text = "Dependencias";
            tabDependencias.UseVisualStyleBackColor = true;
            // 
            // dgvDependencias
            // 
            dgvDependencias.AllowUserToAddRows = false;
            dgvDependencias.AllowUserToDeleteRows = false;
            dgvDependencias.AllowUserToResizeRows = false;
            dgvDependencias.BackgroundColor = Color.White;
            dataGridViewCellStyle7.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = SystemColors.Control;
            dataGridViewCellStyle7.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle7.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle7.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = DataGridViewTriState.True;
            dgvDependencias.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            dgvDependencias.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvDependencias.Columns.AddRange(new DataGridViewColumn[] { colDepClave, colDepDescripcion, colDepTipo, colDepLag });
            dataGridViewCellStyle8.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = SystemColors.Window;
            dataGridViewCellStyle8.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle8.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle8.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle8.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle8.WrapMode = DataGridViewTriState.False;
            dgvDependencias.DefaultCellStyle = dataGridViewCellStyle8;
            dgvDependencias.Dock = DockStyle.Fill;
            dgvDependencias.Location = new Point(3, 3);
            dgvDependencias.Name = "dgvDependencias";
            dgvDependencias.ReadOnly = true;
            dgvDependencias.RowHeadersVisible = false;
            dgvDependencias.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvDependencias.Size = new Size(1370, 237);
            dgvDependencias.TabIndex = 0;
            // 
            // colDepClave
            // 
            colDepClave.DataPropertyName = "ActividadOrigenId";
            colDepClave.HeaderText = "Actividad predecesora";
            colDepClave.Name = "colDepClave";
            colDepClave.ReadOnly = true;
            colDepClave.Width = 280;
            // 
            // colDepDescripcion
            // 
            colDepDescripcion.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colDepDescripcion.DataPropertyName = "Descripcion";
            colDepDescripcion.HeaderText = "Actividad origen";
            colDepDescripcion.Name = "colDepDescripcion";
            colDepDescripcion.ReadOnly = true;
            // 
            // colDepTipo
            // 
            colDepTipo.DataPropertyName = "TipoDependencia";
            colDepTipo.HeaderText = "Tipo";
            colDepTipo.Name = "colDepTipo";
            colDepTipo.ReadOnly = true;
            colDepTipo.Width = 160;
            // 
            // colDepLag
            // 
            colDepLag.DataPropertyName = "DesfaseDias";
            colDepLag.HeaderText = "Lag";
            colDepLag.Name = "colDepLag";
            colDepLag.ReadOnly = true;
            colDepLag.Width = 60;
            // 
            // lblEstado
            // 
            lblEstado.BorderStyle = BorderStyle.FixedSingle;
            lblEstado.Dock = DockStyle.Bottom;
            lblEstado.Location = new Point(0, 731);
            lblEstado.Name = "lblEstado";
            lblEstado.Padding = new Padding(10, 0, 0, 0);
            lblEstado.Size = new Size(1384, 30);
            lblEstado.TabIndex = 3;
            lblEstado.Text = "Estado";
            lblEstado.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // FormProgramaObra
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1384, 761);
            Controls.Add(splitPrincipal);
            Controls.Add(lblEstado);
            Controls.Add(toolPrograma);
            Controls.Add(panelTop);
            Font = new Font("Segoe UI", 9F);
            Name = "FormProgramaObra";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Programa de Obra";
            Load += FormProgramaObra_Load;
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            toolPrograma.ResumeLayout(false);
            toolPrograma.PerformLayout();
            splitPrincipal.Panel1.ResumeLayout(false);
            splitPrincipal.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitPrincipal).EndInit();
            splitPrincipal.ResumeLayout(false);
            splitActividadesGantt.Panel1.ResumeLayout(false);
            splitActividadesGantt.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitActividadesGantt).EndInit();
            splitActividadesGantt.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvActividades).EndInit();
            tabPrograma.ResumeLayout(false);
            tabPeriodos.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvPeriodos).EndInit();
            tabDistribucion.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvDistribucion).EndInit();
            tabDependencias.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvDependencias).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
