using System.Drawing;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Controls
{
    public partial class PanelMatricesEmbebido
    {
        // Altura (px) por debajo de la cual se activa el modo compacto (oculta el
        // desglose de totales; el estado/contexto sobrevive).
        private const int AlturaModoCompacto = 190;

        // Ancho por debajo del cual los campos se re-apilan a dos líneas para que
        // la Descripción conserve ancho útil (nunca colapsa a 1 px).
        private const int AnchoApiladoCampos = 540;

        // Alto lógico de los chips de tipo (segmento con radio semántico).
        private const int ChipAltura = 24;

        // Paleta corporativa SOPRO (alineada con FormPresupuesto).
        private static readonly Color ColorSuperficie = Color.FromArgb(250, 250, 250);
        private static readonly Color ColorSuperficieAlterna = Color.FromArgb(240, 244, 248);
        private static readonly Color ColorTextoPrimario = Color.FromArgb(60, 60, 60);
        private static readonly Color ColorTextoSecundario = Color.FromArgb(100, 100, 100);
        private static readonly Color ColorAcento = Color.FromArgb(31, 78, 121);
        private static readonly Color ColorAcentoSuave = Color.FromArgb(221, 235, 247);
        private static readonly Color ColorHeaderOscuro = Color.FromArgb(51, 51, 76);
        private static readonly Color ColorHeaderSubedicion = Color.FromArgb(31, 78, 121);
        private static readonly Color ColorHeaderBoton = Color.FromArgb(64, 64, 92);
        private static readonly Color ColorHeaderBotonBorde = Color.FromArgb(96, 96, 132);
        private static readonly Color ColorTextoSobreOscuro = Color.FromArgb(205, 210, 224);
        private static readonly Color ColorNeutro = Color.FromArgb(120, 120, 120);
        private static readonly Color ColorCostoDirecto = Color.FromArgb(46, 125, 50);
        private static readonly Color ColorEliminar = Color.FromArgb(150, 0, 0);
        // Estado no modal sobre el header oscuro (contraste AA sobre #33334C).
        private static readonly Color EstadoExito = Color.FromArgb(150, 214, 160);
        private static readonly Color EstadoAdvertencia = Color.FromArgb(240, 200, 130);
        private static readonly Color EstadoError = Color.FromArgb(240, 160, 150);

        private Panel _scrollHost;
        private TableLayoutPanel _rootLayout;
        private bool _modoCompacto;
        private bool _camposApilados;
        // Transición visual atómica del cambio de concepto: WM_SETREDRAW OFF sobre el
        // subárbol durante la ráfaga de estado, liberado hijos→raíz cuando se agotan
        // los pases diferidos de ReprogramarLayoutVertical (nunca queda activo).
        // _profundidadSupresion: bursts activos (reentrancia/bombeo modal dentro de
        // la ráfaga); sólo se libera al salir del burst externo y agotar los pases.
        // _generacionLayout: token de ciclo de handle; los callbacks en cola de una
        // generación vieja no tocan el estado nuevo tras un recreate.
        private bool _redibujoSuprimido;
        private int _pasesReprogramacionPendientes;
        private bool _reprogramacionPendiente;
        private int _profundidadSupresion;
        private int _generacionLayout;
        private Label _lblAgregar;
        private TableLayoutPanel _tlpCampos;
        private FlowLayoutPanel _panelChipsTipo;
        private ToolTip _toolTip;
        private System.ComponentModel.IContainer _contenedorComponentes;

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = new Font("Segoe UI", 9F);

            // Envoltorio con desplazamiento. El TLP raíz está siempre acoplado Top con
            // altura EXPLÍCITA: filas de chrome con alturas deterministas (calculadas
            // en runtime a partir de las fuentes; respetan DPI) y el grid en la fila
            // porcentual (se lleva el resto). Cuando el panel no puede mostrar siquiera
            // UNA fila del grid, la altura del TLP se fija a (chrome + encabezado de
            // grid + una fila) y el envoltorio ofrece desplazamiento vertical como
            // fallback explícito: nunca hay solapes ni un grid fantasma.
            _scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            // Estructura vertical declarativa: header / datos / agregar / grid / totales.
            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 5,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));      // header
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));      // datos (solo edición)
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));      // agregar
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // grid: única fila flexible
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));      // totales

            ConstruirHeader();
            ConstruirDatos();
            ConstruirAgregar();
            ConstruirGrid();
            ConstruirTotales();

            // Se fija cada control a su celda explícita (columna, fila) para que el
            // layout no dependa del z-order de inserción.
            _rootLayout.Controls.Add(_panelHeader, 0, 0);
            _rootLayout.Controls.Add(_panelDatos, 0, 1);
            _rootLayout.Controls.Add(_panelBotonesAgregar, 0, 2);
            _rootLayout.Controls.Add(_dgvComponentes, 0, 3);
            _rootLayout.Controls.Add(_panelTotales, 0, 4);

            _scrollHost.Controls.Add(_rootLayout);
            this.Controls.Add(_scrollHost);

            this.Resize += (s, e) =>
            {
                ApilarCamposSegunAncho();
                AjustarNavegacionSegunAncho();
                AplicarModoCompacto();
                RecalibrarAlturasCromo();
                AplicarModoScroll();
                this.PerformLayout();
            };
            // Re-medir tras cargar para afinar la alineación si el escalado por DPI cambió la fuente.
            this.Load += (s, e) =>
            {
                IgualarAlturaAgregar();
                AplicarEstiloChipsTipo();
                ApilarCamposSegunAncho();
                AjustarNavegacionSegunAncho();
                AplicarModoCompacto();
                RecalibrarAlturasCromo();
                AplicarModoScroll();
                this.PerformLayout();
            };
            RecalibrarAlturasCromo();
            AplicarModoCompacto();
        }

        private void ConstruirHeader()
        {
            // Header compacto de una línea de identidad: [título][chips de tipo][nav],
            // más una segunda fila condicionada para estado/contexto (solo hay mensaje).
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = ColorHeaderOscuro,
                Margin = new Padding(0),
                Padding = new Padding(SoproUiMetrics.StandardMargin, 2, SoproUiMetrics.StandardMargin, 2)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // identidad
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // chips de tipo
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // navegación
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblTituloMatriz = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, SoproUiMetrics.HorizontalGap, 0),
                Text = string.Empty,
                AccessibleName = "Matriz mostrada"
            };
            _lblInfoMatriz = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTextoSobreOscuro,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0),
                Text = string.Empty,
                Visible = false
            };

            // ToolTip ligado a un contenedor de componentes propio: la desechación la
            // hace el Dispose del control a través del contenedor (sin fugas); el
            // Dispose propio conserva además la llamada directa idempotente.
            if (_contenedorComponentes == null)
                _contenedorComponentes = new System.ComponentModel.Container();
            _toolTip = new ToolTip(_contenedorComponentes)
            {
                InitialDelay = 400,
                AutoPopDelay = 8000,
                ReshowDelay = 100
            }; _toolTip.SetToolTip(_lblTituloMatriz, "Matriz cargada en el panel.");

            _panelChipsTipo = ConstruirChipsTipo();
            var botonesNav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };

            _btnAnterior = new Button
            {
                Text = "▲",
                AutoSize = false,
                Size = new Size(28, ChipAltura),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorHeaderBoton,
                ForeColor = Color.White,
                Enabled = false,
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0),
                AccessibleName = "Concepto anterior",
                TabIndex = 1
            };
            _btnSiguiente = new Button
            {
                Text = "▼",
                AutoSize = false,
                Size = new Size(28, ChipAltura),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorHeaderBoton,
                ForeColor = Color.White,
                Enabled = false,
                Margin = new Padding(0),
                AccessibleName = "Concepto siguiente",
                TabIndex = 2
            };
            _btnAnterior.FlatAppearance.BorderColor = ColorHeaderBotonBorde;
            _btnSiguiente.FlatAppearance.BorderColor = ColorHeaderBotonBorde;
            _btnAnterior.Click += BtnAnterior_Click;
            _btnSiguiente.Click += BtnSiguiente_Click;
            _toolTip.SetToolTip(_btnAnterior, "Ir al concepto anterior del presupuesto.");
            _toolTip.SetToolTip(_btnSiguiente, "Ir al concepto siguiente del presupuesto.");
            botonesNav.Controls.Add(_btnAnterior);
            botonesNav.Controls.Add(_btnSiguiente);

            // Acción primaria de la vista de matrices: crear una matriz cuando el
            // contexto no la tiene. Visible solo para conceptos sin APU y ocultada en
            // cualquier estado de edición; queda a la izquierda de los controles de
            // navegación (que son la acción terciaria) con separación propia.
            _btnNuevaMatriz = new Button
            {
                Name = "BtnNuevaMatrizAcceso",
                Text = "＋ Nueva matriz",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(SoproUiMetrics.ContentButtonMinWidth, ChipAltura),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorAcentoSuave,
                ForeColor = ColorAcento,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                UseVisualStyleBackColor = false,
                Visible = false,
                Anchor = AnchorStyles.Top,
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0),
                AccessibleName = "Nueva matriz",
                TabIndex = 0
            };
            _btnNuevaMatriz.FlatAppearance.BorderSize = 0;
            _btnNuevaMatriz.Click += BtnNuevaMatrizAcceso_Click;
            _toolTip.SetToolTip(_btnNuevaMatriz, "Crear una matriz nueva para este concepto.");
            botonesNav.Controls.Add(_btnNuevaMatriz);
            botonesNav.Controls.SetChildIndex(_btnNuevaMatriz, 0);
            botonesNav.Controls.SetChildIndex(_btnAnterior, 1);
            botonesNav.Controls.SetChildIndex(_btnSiguiente, 2);

            header.Controls.Add(_lblTituloMatriz, 0, 0);
            header.Controls.Add(_panelChipsTipo, 1, 0);
            header.Controls.Add(botonesNav, 2, 0);
            header.SetColumnSpan(_lblInfoMatriz, 3);
            header.Controls.Add(_lblInfoMatriz, 0, 1);
            _panelHeader = header;
        }

        /// <summary>
        /// Control segmentado de tipo: tres botones de aspecto chip respaldados por
        /// RadioButtons (semántica de grupo: ArrowLeft/ArrowRight cicla los tipos con
        /// el teclado, exactamente como un grupo de radios; cada AccessKey —
        /// Alt+A/B/C — salta directo a su tipo). Aparece integrado en el header; no
        /// ocupa línea aparte.
        /// </summary>
        private FlowLayoutPanel ConstruirChipsTipo()
        {
            var chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                AccessibleName = "Tipo de matriz"
            };

            _rbTipoApu = CrearChipTipo("&APU", activo: true);
            _rbTipoBasico = CrearChipTipo("&Básico", activo: false);
            _rbTipoCuadrilla = CrearChipTipo("&Cuadrilla", activo: false);

            chips.Controls.Add(_rbTipoApu);
            chips.Controls.Add(_rbTipoBasico);
            chips.Controls.Add(_rbTipoCuadrilla);
            return chips;
        }

        private static RadioButton CrearChipTipo(string textoConMnemonic, bool activo)
        {
            var chip = new RadioButton
            {
                Text = textoConMnemonic,
                AutoSize = true,
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                Checked = activo,
                MinimumSize = new Size(0, ChipAltura),
                Anchor = AnchorStyles.Top,
                Margin = new Padding(0, 0, 4, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                UseCompatibleTextRendering = false,
                TabIndex = 10,
                UseVisualStyleBackColor = false
            };
            chip.FlatAppearance.BorderSize = 1;
            return chip;
        }

        private void ConstruirDatos()
        {
            var datos = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = ColorSuperficie,
                Visible = false,
                Margin = new Padding(0),
                Padding = new Padding(SoproUiMetrics.StandardMargin, 3, SoproUiMetrics.StandardMargin, 3)
            };
            datos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            datos.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Banda única de campos. La Descripción usa columna Percent flexible
            // (sin piso duro); cuando el ancho aprieta, ApilarCamposSegunAncho la
            // re-apila a una segunda línea en lugar de dejarla colapsar.
            var campos = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 6,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));        // Clave (etiqueta)
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));  // Clave (texto)
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));        // Unidad (etiqueta)
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));   // Unidad (texto)
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));        // Descripción (etiqueta)
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));   // Descripción (texto)
            campos.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblClave = CrearEtiquetaCampo("Clave");
            _txtClaveMatriz = new TextBox
            {
                Dock = DockStyle.Fill,
                CharacterCasing = CharacterCasing.Upper,
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0),
                AccessibleName = "Clave de la matriz"
            };
            var lblUnidad = CrearEtiquetaCampo("Unidad");
            _txtUnidadMatriz = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0),
                AccessibleName = "Unidad de la matriz"
            };
            var lblDesc = CrearEtiquetaCampo("Descripción");
            _txtDescripcionMatriz = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                MaxLength = 2000,
                AccessibleName = "Descripción de la matriz"
            };

            campos.Controls.Add(lblClave, 0, 0);
            campos.Controls.Add(_txtClaveMatriz, 1, 0);
            campos.Controls.Add(lblUnidad, 2, 0);
            campos.Controls.Add(_txtUnidadMatriz, 3, 0);
            campos.Controls.Add(lblDesc, 4, 0);
            campos.Controls.Add(_txtDescripcionMatriz, 5, 0);

            _toolTip.SetToolTip(_txtClaveMatriz, "Clave de la matriz. Al salir del campo se guarda automáticamente.");
            _toolTip.SetToolTip(_txtUnidadMatriz, "Unidad de la matriz. Al salir del campo se guarda automáticamente.");
            _toolTip.SetToolTip(_txtDescripcionMatriz, "Descripción de la matriz. Al salir del campo se guarda automáticamente.");

            datos.Controls.Add(campos, 0, 0);
            _panelDatos = datos;
            _tlpCampos = campos;
        }

        private void ConstruirAgregar()
        {
            var agregar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = ColorSuperficie,
                Enabled = false,
                Margin = new Padding(0),
                Padding = new Padding(SoproUiMetrics.StandardMargin, 3, SoproUiMetrics.StandardMargin, 3)
            };

            _lblAgregar = new Label
            {
                Text = "Agregar:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTextoSecundario,
                // Margen superior 0: igual que los botones "Agregar" (que al ser AutoSize
                // sin relleno vertical arrancan en la parte alta de la fila). La altura de
                // la etiqueta se iguala a la de los botones en IgualarAlturaAgregar para
                // que sus textos queden en la misma línea.
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0)
            };

            _btnAgregarMaterial = CrearBotonAgregar("Material");
            _btnAgregarMO = CrearBotonAgregar("M.O.");
            _btnAgregarMaquinaria = CrearBotonAgregar("Maquinaria");
            _btnAgregarHerramienta = CrearBotonAgregar("Herramienta");
            _btnAgregarBasico = CrearBotonAgregar("Básico");
            _btnAgregarMaterial.Click += BtnAgregarMaterial_Click;
            _btnAgregarMO.Click += BtnAgregarMO_Click;
            _btnAgregarMaquinaria.Click += BtnAgregarMaquinaria_Click;
            _btnAgregarHerramienta.Click += BtnAgregarHerramienta_Click;
            _btnAgregarBasico.Click += BtnAgregarBasico_Click;

            agregar.Controls.Add(_lblAgregar);
            agregar.Controls.Add(_btnAgregarMaterial);
            agregar.Controls.Add(_btnAgregarMO);
            agregar.Controls.Add(_btnAgregarMaquinaria);
            agregar.Controls.Add(_btnAgregarHerramienta);
            agregar.Controls.Add(_btnAgregarBasico);
            _panelBotonesAgregar = agregar;

            // Alinea la etiqueta "Agregar:" con la altura natural de los botones.
            IgualarAlturaAgregar();
        }

        private void ConstruirGrid()
        {
            _dgvComponentes = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = SoproUiMetrics.GridHeaderHeight,
                RowTemplate = { Height = 28 }, Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0),
                AccessibleName = "Componentes de la matriz"
            };
            // Estilo SOPRO: header claro 245,245,248 con texto 60,60,60 y filas alternas sutiles.
            _dgvComponentes.AplicarEstiloSOPRO();
            // Estilo de selección del presupuesto: fila completa azul claro + contorno azul de 2px
            // en la celda activa, sin tocar el comportamiento de portapapeles del grid.
            // Patrón read-mutate-assign-back: el getter devuelve una copia
            // desechable, así que hay que reasignar el style al grid.
            var baseStyle = _dgvComponentes.DefaultCellStyle;
            baseStyle.SelectionBackColor = SoproTheme.Colors.SelBg;
            baseStyle.SelectionForeColor = Color.Black;
            _dgvComponentes.DefaultCellStyle = baseStyle;
            // Reafirmar la selección en filas alternas para que el zebra no se
            // imponga sobre el fondo de la fila seleccionada (selección uniforme).
            var altStyle = _dgvComponentes.AlternatingRowsDefaultCellStyle;
            altStyle.SelectionBackColor = SoproTheme.Colors.SelBg;
            altStyle.SelectionForeColor = Color.Black;
            _dgvComponentes.AlternatingRowsDefaultCellStyle = altStyle;
            _dgvComponentes.CellPainting += DgvComponentes_CellPainting;
            _dgvComponentes.CurrentCellChanged += (s, e) => _dgvComponentes.Invalidate();
            // Se conserva la rejilla horizontal mínima del panel (sin líneas verticales).
            _dgvComponentes.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

            var right = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
            var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
            // Anchos responsivos: las columnas se dimensionan al contenido con un MinimumWidth
            // como piso, y la Descripción es la única columna Fill (mayor peso) para absorber
            // el espacio sobrante. Así, al estrechar, el Fill se encoge hasta su mínimo y solo
            // entonces aparece scroll horizontal; al ensanchar, la Descripción gana el espacio.
            _dgvComponentes.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "ColTipo",     HeaderText = "Tipo",        AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 90,  ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColClave",    HeaderText = "C",           AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 80,  ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColDesc",     HeaderText = "Descripción", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 100F, MinimumWidth = 180, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColUnidad",   HeaderText = "Unidad",      AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 55,  DefaultCellStyle = center, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColCantidad", HeaderText = "Cantidad",    AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 80,  DefaultCellStyle = right, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColPU",       HeaderText = "P.U.",        AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 95,  DefaultCellStyle = right, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColImporte",  HeaderText = "Importe",     AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 95,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColId",       HeaderText = "ID",          Visible = false },
                new DataGridViewButtonColumn
                {
                    Name = "ColEliminar", HeaderText = "", AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, MinimumWidth = 84, ReadOnly = true,
                    Text = "Eliminar", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat,
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = ColorEliminar }
                }
            });
            _dgvComponentes.CellClick += DgvComponentes_CellClick;
            _dgvComponentes.CellDoubleClick += DgvComponentes_PanelCellDoubleClick;
            _dgvComponentes.CellBeginEdit += DgvComponentes_PanelCellBeginEdit;
            _dgvComponentes.CellEndEdit += DgvComponentes_PanelCellEndEdit;
            _dgvComponentes.CellMouseDown += DgvComponentes_CellMouseDown;
            _dgvComponentes.MouseDown += DgvComponentes_MouseDown;
            _dgvComponentes.CellMouseUp += DgvComponentes_MouseUp;
            _dgvComponentes.MouseUp += DgvComponentes_MouseUp;
        }

        private void ConstruirTotales()
        {
            var totales = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = ColorSuperficieAlterna,
                Margin = new Padding(0),
                Padding = new Padding(SoproUiMetrics.StandardMargin, 2, SoproUiMetrics.StandardMargin, 2)
            };

            var fuenteNormal = new Font("Segoe UI", 9F);
            var fuenteTotal = new Font("Segoe UI", 10F, FontStyle.Bold);

            _lblMat = CrearEtiquetaTotal(fuenteNormal, ColorTextoPrimario, new Padding(0, 0, SoproUiMetrics.StandardMargin, 0));
            _lblMO = CrearEtiquetaTotal(fuenteNormal, ColorTextoPrimario, new Padding(0, 0, SoproUiMetrics.StandardMargin, 0));
            _lblMaq = CrearEtiquetaTotal(fuenteNormal, ColorTextoPrimario, new Padding(0, 0, SoproUiMetrics.StandardMargin, 0));
            _lblBas = CrearEtiquetaTotal(fuenteNormal, ColorTextoPrimario, new Padding(0, 0, SoproUiMetrics.StandardMargin, 0));
            _lblDir = CrearEtiquetaTotal(fuenteTotal, ColorCostoDirecto, new Padding(0, 0, 0, 0));

            totales.Controls.Add(_lblMat);
            totales.Controls.Add(_lblMO);
            totales.Controls.Add(_lblMaq);
            totales.Controls.Add(_lblBas);
            totales.Controls.Add(_lblDir);
            _panelTotales = totales;
        }

        private static Label CrearEtiquetaCampo(string texto)
        {
            return new Label
            {
                Text = texto,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTextoPrimario,
                Margin = new Padding(0, SoproUiMetrics.VerticalGap, SoproUiMetrics.HorizontalGap, 0)
            };
        }

        private static Label CrearEtiquetaTotal(Font fuente, Color color, Padding margen)
        {
            return new Label
            {
                AutoSize = true,
                Font = fuente,
                ForeColor = color,
                Margin = margen,
                Text = string.Empty
            };
        }

        private static Button CrearBotonAgregar(string texto)
        {
            var btn = new Button
            {
                Text = texto,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                // Sin piso de altura (0): su altura natural (la que cabe el texto) la mide
                // IgualarAlturaAgregar, igual que el resto de contenidos.
                MinimumSize = new Size(SoproUiMetrics.ContentButtonMinWidth, 0),
                // Solo relleno horizontal: sin relleno vertical, la altura la gobierna el texto.
                Padding = new Padding(SoproUiMetrics.ContentButtonPadding.Left, 0, SoproUiMetrics.ContentButtonPadding.Right, 0),
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorAcentoSuave,
                ForeColor = ColorAcento,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        /// <summary>
        /// Pinta los chips según estado: activo (acento suave sobre el header oscuro),
        /// inactivo (chip oscuro) o deshabilitado; y marca el caso "tipo fijo APU"
        /// de la creación por asignación con su tooltip/descripción de accesibilidad.
        /// Se llama en cada CheckedChanged y cada cambio de Enabled.
        /// </summary>
        private void AplicarEstiloChipsTipo()
        {
            if (_panelChipsTipo == null) return;
            foreach (var chip in new[] { _rbTipoApu, _rbTipoBasico, _rbTipoCuadrilla })
            {
                if (chip == null) continue;
                string texto = QuitarMnemonico(chip.Text);
                bool fijoPorAsignacion = _tipoFijoApu;
                chip.FlatAppearance.BorderSize = 1;
                if (!chip.Enabled && !chip.Checked)
                {
                    // Deshabilitado: gris atenuado sobre el header oscuro.
                    chip.BackColor = ColorHeaderBoton;
                    chip.ForeColor = Color.FromArgb(140, 142, 160);
                    chip.FlatAppearance.BorderColor = ColorHeaderBotonBorde;
                }
                else if (chip.Checked)
                {
                    // Chip activo: acento suave, el único punto de color del header.
                    chip.BackColor = ColorAcentoSuave;
                    chip.ForeColor = ColorAcento;
                    chip.FlatAppearance.BorderColor = ColorAcentoSuave;
                }
                else
                {
                    chip.BackColor = ColorHeaderBoton;
                    chip.ForeColor = ColorTextoSobreOscuro;
                    chip.FlatAppearance.BorderColor = ColorHeaderBotonBorde;
                }

                if (fijoPorAsignacion)
                {
                    _toolTip.SetToolTip(chip, "Fijo en APU: la creación por asignación del presupuesto sólo admite este tipo.");
                    chip.AccessibleDescription = "Tipo fijo en APU por la creación por asignación; no es seleccionable.";
                }
                else if (chip.Checked)
                {
                    _toolTip.SetToolTip(chip, $"Tipo actual: {texto}. Cambiarlo se guarda automáticamente.");
                    chip.AccessibleDescription = "Tipo actual de la matriz.";
                }
                else
                {
                    _toolTip.SetToolTip(chip, $"Cambiar el tipo de matriz a {texto}. El cambio se guarda automáticamente.");
                    chip.AccessibleDescription = "Cambiar el tipo de matriz.";
                }
                chip.AccessibleName = $"Tipo de matriz: {texto}";
            }
        }

        private static string QuitarMnemonico(string texto) => texto.Replace("&", string.Empty);

        // Banda de estado/contexto del header (línea 2 del header, solo hay contenido
        // cuando es útil): instrucciones de creación, avisos de conflicto o resultado
        // del autoguardado ("Guardado" / "Sin guardar — …"). Nunca es un botón.
        private void MostrarEstadoHeader(string texto, Color color)
        {
            if (_lblInfoMatriz == null) return;
            _lblInfoMatriz.Text = texto;
            _lblInfoMatriz.ForeColor = color;
            _lblInfoMatriz.Height = _lblInfoMatriz.Font.Height + 3;
            _lblInfoMatriz.Visible = !string.IsNullOrWhiteSpace(texto) && !_modoCompacto;
            _toolTip.SetToolTip(_lblInfoMatriz, texto);
            _lblInfoMatriz.AccessibleName = "Estado de la matriz";
            _lblInfoMatriz.AccessibleDescription = texto;
            RecalibrarAlturasCromo();
            ReprogramarLayoutVertical();
        }

        private void OcultarEstadoHeader()
        {
            if (_lblInfoMatriz == null) return;
            // Navegar entre matrices vuelve a pedir el estado normal en cada carga; si
            // ya está oculto no hay cambio de chrome que medir ni pase que agendar.
            if (!_lblInfoMatriz.Visible
                && string.IsNullOrEmpty(_lblInfoMatriz.Text)
                && _lblInfoMatriz.Height == 0)
            {
                return;
            }
            _lblInfoMatriz.Text = string.Empty;
            _lblInfoMatriz.Visible = false;
            _lblInfoMatriz.Height = 0;
            _toolTip.SetToolTip(_lblInfoMatriz, string.Empty);
            RecalibrarAlturasCromo();
            ReprogramarLayoutVertical();
        }

        // Modo compacto: en alturas bajas preserva grid, comando principal y totales
        // esenciales, y oculta lo secundario (desglose de totales). La línea de
        // estado/contexto sobrevive porque comunica borradores y fallos. El desglose
        // además colapsa por ancho estrecho y SE REEVALÚA en cada redibujado (el early
        // return solo cubre el trabajo de métricas, no el desglose).
        private void AplicarModoCompacto()
        {
            bool compacto = this.Height > 0 && this.Height < AlturaModoCompacto;
            bool cambioCompacto = compacto != _modoCompacto;
            if (cambioCompacto)
            {
                _modoCompacto = compacto;
                AplicarMetricasContenido(compacto);
            }

            bool desglose = DesgloseTotalesVisibleSegunAncho(compacto, this.Width);
            if (_lblInfoMatriz != null && !string.IsNullOrWhiteSpace(_lblInfoMatriz.Text)
                && _lblInfoMatriz.Visible != !compacto)
                _lblInfoMatriz.Visible = !compacto;

            if (_lblMat != null && _lblMat.Visible != desglose) _lblMat.Visible = desglose;
            if (_lblMO != null && _lblMO.Visible != desglose) _lblMO.Visible = desglose;
            if (_lblMaq != null && _lblMaq.Visible != desglose) _lblMaq.Visible = desglose;
            if (_lblBas != null && _lblBas.Visible != desglose) _lblBas.Visible = desglose;
            if (_lblDir != null) _lblDir.Visible = true;
        }

        /// <summary>
        /// Re-apila los campos cuando el ancho aprieta: fila 1 = Clave + Unidad,
        /// fila 2 = Descripción a todo el ancho. Se reconfiguran también los estilos
        /// de columna (la columna de la etiqueta Descripción pasa a 0 y la caja se
        /// abarca sobre las columnas restantes) porque TLP no mezcla anchos por fila
        /// dentro de una misma tabla. Determinístico (umbral único) y evita que la
        /// Descripción llegue a colapsar a unos cuantos píxeles.
        /// </summary>
        private void ApilarCamposSegunAncho()
        {
            if (_tlpCampos == null) return;
            bool apilar = this.Width > 0 && this.Width < AnchoApiladoCampos;
            if (apilar == _camposApilados) return;
            _camposApilados = apilar;

            SuspendLayout();
            _tlpCampos.SuspendLayout();
            try
            {
                if (apilar)
                {
                    _tlpCampos.RowCount = 2;
                    _tlpCampos.RowStyles.Clear();
                    _tlpCampos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    _tlpCampos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    // Columna de la etiqueta Descripción (índice 4) sin ancho en este modo
                    // (su etiqueta vive en la fila 2, columna 0).
                    _tlpCampos.ColumnStyles[4].SizeType = SizeType.Absolute;
                    _tlpCampos.ColumnStyles[4].Width = 0;
                    _tlpCampos.SetCellPosition(TLBLbl(), new TableLayoutPanelCellPosition(0, 1));
                    _tlpCampos.SetCellPosition(_txtDescripcionMatriz, new TableLayoutPanelCellPosition(1, 1));
                    _tlpCampos.SetColumnSpan(_txtDescripcionMatriz, 5);
                }
                else
                {
                    _tlpCampos.RowCount = 1;
                    _tlpCampos.RowStyles.Clear();
                    _tlpCampos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    _tlpCampos.ColumnStyles[4].SizeType = SizeType.AutoSize;
                    _tlpCampos.SetColumnSpan(_txtDescripcionMatriz, 1);
                    _tlpCampos.SetCellPosition(_txtDescripcionMatriz, new TableLayoutPanelCellPosition(5, 0));
                    _tlpCampos.SetCellPosition(TLBLbl(), new TableLayoutPanelCellPosition(4, 0));
                }
            }
            finally
            {
                _tlpCampos.ResumeLayout(true);
                ResumeLayout(true);
                PerformLayout();
                RecalibrarAlturasCromo();
                AplicarModoScroll();
            }
        }

        private Label TLBLbl()
        {
            foreach (Control c in _tlpCampos.Controls)
            {
                if (c is Label l && l.Text == "Descripción")
                    return l;
            }
            throw new InvalidOperationException("Falta la etiqueta Descripción de la banda de campos.");
        }

        /// <summary>
        /// En anchos muy estrechos (o con fuente grande, p. ej. 125 %) los chips +
        /// navegación no caben junto al título con un mínimo legible: se oculta ▲▼
        /// (acción terciaria; su fallback es navegar las filas del propio grid del
        /// presupuesto, que gobierna el panel). Umbral DINÁMICO: ancho real de chips
        /// (PreferredSize, respeta la fuente activa) + ancho de ＋Nueva matriz si está
        /// visible + ancho de nav + un mínimo de título (texto real, no constante fija
        /// de píxeles), contra el ancho disponible. A 100 % nunca se oculta a 478+
        /// (cabe holgado); a 125 % en 478 sí se oculta para que título y chips no
        /// queden recortados ni los glifos de navegación queden fuera de su fila.
        /// </summary>
        private void AjustarNavegacionSegunAncho()
        {
            if (_btnAnterior == null || _btnSiguiente == null) return;
            int anchoChips = 0;
            if (_panelChipsTipo != null && _panelChipsTipo.Visible)
                anchoChips = _panelChipsTipo.PreferredSize.Width + _panelChipsTipo.Margin.Horizontal;
            int anchoNueva = _btnNuevaMatriz.Visible
                ? _btnNuevaMatriz.Width + _btnNuevaMatriz.Margin.Horizontal
                : 0;
            int anchoNav = _btnAnterior.Width + _btnSiguiente.Width + _btnSiguiente.Margin.Horizontal;
            string textoIdentidad = _lblTituloMatriz != null ? _lblTituloMatriz.Text : string.Empty;
            if (string.IsNullOrWhiteSpace(textoIdentidad)) textoIdentidad = "Matriz: X";
            var fuenteTitulo = _lblTituloMatriz != null ? _lblTituloMatriz.Font : this.Font;
            int minimoTitulo = TextRenderer.MeasureText(
                textoIdentidad.Length > 10 ? textoIdentidad.Substring(0, 10) : textoIdentidad,
                fuenteTitulo).Width;
            int anchoDisponible = this.Width - SoproUiMetrics.StandardMargin * 2;
            bool mostrarNav = anchoDisponible >= anchoChips + anchoNueva + anchoNav + minimoTitulo;
            if (_btnAnterior.Visible != mostrarNav)
            {
                _btnAnterior.Visible = mostrarNav;
                _btnSiguiente.Visible = mostrarNav;
            }
        }

        /// <summary>
        /// Alturas deterministas del chrome, calculadas en runtime a partir de las
        /// fuentes y del estado de visibilidad (respetan el escalado por DPI sin
        /// depender del AutoSize/Dock de los paneles internos ni de pasadas previas).
        /// Fija las filas del TLP raíz como Absolute y delega TODO el espacio restante
        /// al grid (fila porcentual).
        /// </summary>
        private void RecalibrarAlturasCromo()
        {
            if (_rootLayout == null) return;

            bool estadoVisible = _lblInfoMatriz != null && _lblInfoMatriz.Visible && !string.IsNullOrWhiteSpace(_lblInfoMatriz.Text) && !_modoCompacto;
            int altoTitulo = _lblTituloMatriz != null
                ? TextRenderer.MeasureText("Ag", _lblTituloMatriz.Font).Height + 4
                : 20;
            // Alto de línea del header: el mayor entre título, chips de tipo y botones de
            // navegación/acceso (respeta el escalado por DPI; los controles fijos crecen
            // y la fila nunca se queda corta ni aplasta el glifo ▲ ni el texto del chip).
            int altoFilaHeader = Math.Max(altoTitulo, ChipAltura);
            if (_rbTipoApu != null && _rbTipoApu.Visible)
            {
                // Altura NATURAL del chip (botón de radio con Aparición botón): se mide
                // con su fuente ACTIVA (125 % → crece), no con constantes.
                altoFilaHeader = Math.Max(altoFilaHeader, _rbTipoApu.GetPreferredSize(Size.Empty).Height);
            }
            // Los chips siempre están presentes; nav y acceso elevan si están visibles.
            if (_btnAnterior != null && _btnAnterior.Visible)
                altoFilaHeader = Math.Max(altoFilaHeader, _btnAnterior.Height);
            if (_btnNuevaMatriz != null && _btnNuevaMatriz.Visible)
                altoFilaHeader = Math.Max(altoFilaHeader, _btnNuevaMatriz.Height);
            int hHeader = 4 + altoFilaHeader + (estadoVisible ? 2 + _lblInfoMatriz.Height : 0);
            if (_lblInfoMatriz != null && !estadoVisible)
                _lblInfoMatriz.Height = 0;

            int altoCampo = _txtClaveMatriz != null ? _txtClaveMatriz.PreferredHeight + 4 : 27;
            int hDatos = _panelDatos != null && _panelDatos.Visible
                ? 6 + (_camposApilados ? altoCampo * 2 + 3 : altoCampo)
                : 0;

            // La fila de Agregar envuelve cuando el ancho aprieta: se mide el número
            // de líneas con las mismas métricas que el FlowLayoutPanel (ancho
            // preferido de etiqueta/botones + margen) para fijar la altura de la fila
            // de modo que NINGUNA acción quede recortada.
            IgualarAlturaAgregar();
            int hAgregar = 0;
            if (_panelBotonesAgregar != null && _panelBotonesAgregar.Visible)
            {
                int altoBotonAgregar = _btnAgregarMaterial != null ? _btnAgregarMaterial.Height : 24;
                int lineasAgregar = CalcularFilasAgregado();
                hAgregar = 6 + lineasAgregar * altoBotonAgregar;
            }

            int hTotales = 2;
            if (_panelTotales != null && _panelTotales.Visible)
            {
                foreach (var lbl in new[] { _lblMat, _lblMO, _lblMaq, _lblBas, _lblDir })
                {
                    if (lbl == null || !lbl.Visible) continue;
                    hTotales = Math.Max(hTotales, 4 + lbl.Font.Height);
                }
            }

            var filas = _rootLayout.RowStyles;
            filas[0].Height = hHeader;
            filas[1].Height = hDatos;
            filas[2].Height = hAgregar;
            filas[4].Height = hTotales;
            // La fila del grid (índice 3) permanece en porcentaje: absorbe el resto.

            // Con viewport estrecho el modo Fill de Descripción no siempre logra que la
            // barra horizontal del grid se materialice: conmutar ese modo a ancho fijo
            // hace que el contenido supere claramente el viewport y que la HScrollBar
            // del propio grid (sin barra paralela) quede visible y utilizable.
            AjustarColumnasSegunViewport();

            // Sincronizar las alturas internas de los paneles estáticos con el cálculo.
            if (_panelHeader != null) _panelHeader.Height = hHeader;
            if (_panelDatos != null) _panelDatos.Height = hDatos;
            if (_panelBotonesAgregar != null) _panelBotonesAgregar.Height = hAgregar;
            if (_panelTotales != null) _panelTotales.Height = hTotales;
        }

        /// <summary>
        /// Colapsado de totales en ancho estrecho (fallback accesible sin rediseño):
        /// el desglose (Mat/M.O./Maq./Bas.) solo cabe con el ancho funcional; en
        /// anchos menores la fila muestra únicamente el Costo Directo (el desglose no
        /// se pierde: reaparece al ensanchar, igual que el modo compacto lo re-muestra).
        /// </summary>
        private static bool DesgloseTotalesVisibleSegunAncho(bool compacto, int ancho) => !compacto && ancho >= 560;

        /// <summary>
        /// Filas que ocupará la banda "Agregar" con el ancho interior actual,
        /// replicando el envolvimiento del FlowLayoutPanel (ancho preferido de cada
        /// etiqueta/botón + su margen derecho).
        /// </summary>
        private int CalcularFilasAgregado()
        {
            if (_panelBotonesAgregar == null) return 1;
            int anchoInterior = Math.Max(120, this.Width - SoproUiMetrics.StandardMargin * 2);
            var anchos = new List<int>();
            if (_lblAgregar != null)
                anchos.Add(TextRenderer.MeasureText(_lblAgregar.Text, _lblAgregar.Font).Width + _lblAgregar.Margin.Right);
            foreach (var b in new[] { _btnAgregarMaterial, _btnAgregarMO, _btnAgregarMaquinaria, _btnAgregarHerramienta, _btnAgregarBasico })
            {
                if (b == null) continue;
                anchos.Add(b.PreferredSize.Width + b.Margin.Right);
            }
            int filas = 1;
            int x = 0;
            foreach (var w in anchos)
            {
                if (x > 0 && x + w > anchoInterior)
                {
                    filas++;
                    x = 0;
                }
                x += w;
            }
            return filas;
        }

        /// <summary>
        /// Modo de la columna Descripción según viewport: Fill cuando el espacio
        /// restante basta para su mínimo (ancho holgado); ancho FIJO (sin Fill)
        /// cuando la suma de las anchuras mínimas de todas las columnas excede
        /// el panel: en ese caso el contenido deja de ser ajustable y la barra de
        /// desplazamiento horizontal del propio grid se materializa de forma fiable
        /// (el modo Fill la puede ocultar aunque su mínimo no cupa). No rediseña
        /// columnas ni añade barras paralelas.
        /// </summary>
        private void AjustarColumnasSegunViewport()
        {
            if (_dgvComponentes == null) return;
            var colDesc = _dgvComponentes.Columns["ColDesc"];
            if (colDesc == null) return;

            int sumaMinimos = colDesc.MinimumWidth;
            foreach (DataGridViewColumn columna in _dgvComponentes.Columns)
            {
                if (columna == colDesc || !columna.Visible) continue;
                sumaMinimos += columna.MinimumWidth;
            }

            bool fillViable = _dgvComponentes.ClientSize.Width >= sumaMinimos;
            if (fillViable && colDesc.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
            {
                colDesc.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                colDesc.MinimumWidth = 180;
            }
            else if (!fillViable && colDesc.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
            {
                // Ancho de lectura constante (el usuario lo puede ensanchar/estrechar
                // con el propio grid ya en scroll horizontal).
                colDesc.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                colDesc.MinimumWidth = 180;
                colDesc.Width = Math.Max(180, Math.Min(240, _dgvComponentes.ClientSize.Width / 3));
            }
        }

        private int AlturaCromoCalculada()
        {
            if (_rootLayout == null) return 0;
            return (int)_rootLayout.RowStyles[0].Height
                 + (int)_rootLayout.RowStyles[1].Height
                 + (int)_rootLayout.RowStyles[2].Height
                 + (int)_rootLayout.RowStyles[4].Height;
        }

        /// <summary>
        /// Fallback explícito: si el chrome + (encabezado de grid + una fila) no caben,
        /// el TLP fija esa altura de contenido y el envoltorio da desplazamiento
        /// vertical. Sin overflow la altura del TLP es exactamente la del panel (sin
        /// scrollbar); nunca hay solapes ni un grid de 1 px.
        /// </summary>
        private void AplicarModoScroll()
        {
            if (_scrollHost == null || _rootLayout == null) return;
            int altoDisponible = this.ClientSize.Height;
            if (altoDisponible <= 0) altoDisponible = this.Height;
            if (altoDisponible <= 0) return;

            int cromo = AlturaCromoCalculada();
            // Una fila útil exige encabezado + alto real de fila + la barra horizontal
            // del propio grid (obligatoria a 320/478 px) + bordes. Sin la barra en el
            // mínimo, el grid queda de "encabezado + una fila" y la barra se dibuja
            // sobre la primera fila, tapando la columna Clave.
            int altoFila = _dgvComponentes.Rows.Count > 0
                ? _dgvComponentes.Rows[0].Height
                : _dgvComponentes.RowTemplate.Height;
            int anchoMinimoColumnas = 0;
            foreach (DataGridViewColumn columna in _dgvComponentes.Columns)
            {
                if (columna.Visible)
                    anchoMinimoColumnas += columna.MinimumWidth;
            }
            // La barra horizontal del grid depende de los mínimos de columna, no solo
            // de su visibilidad actual: en el primer pase aún no está materializada.
            bool necesitaBarraHorizontalGrid = ScrollHorizontalGridVisibleParaPrueba
                || (_dgvComponentes.ClientSize.Width > 0
                    && anchoMinimoColumnas > _dgvComponentes.ClientSize.Width);
            int altoBarraHorizontalGrid = necesitaBarraHorizontalGrid
                ? SystemInformation.HorizontalScrollBarHeight
                : 0;
            int totalMinimoGrid = _dgvComponentes.ColumnHeadersHeight
                + altoFila
                + altoBarraHorizontalGrid
                + 2;
            bool desborda = cromo + totalMinimoGrid > altoDisponible;
            int altoContenido = desborda ? cromo + totalMinimoGrid : altoDisponible;

            if (_scrollHost.AutoScroll != desborda)
                _scrollHost.AutoScroll = desborda;

            // El fallback del panel es SOLO vertical: sin extensión horizontal en
            // AutoScrollMinSize, el host no dibuja una barra propia sobre el grid.
            var minimoScroll = desborda ? new Size(0, altoContenido) : Size.Empty;
            if (_scrollHost.AutoScrollMinSize != minimoScroll)
                _scrollHost.AutoScrollMinSize = minimoScroll;

            if (_rootLayout.Height != altoContenido)
                _rootLayout.Height = altoContenido;

            // Cinturón: si un re-layout transitorio dejó la barra del host visible,
            // se retira; el grid conserva la suya para alcanzar todas las columnas.
            if (_scrollHost.HorizontalScroll.Visible)
            {
                _scrollHost.HorizontalScroll.Enabled = false;
                _scrollHost.HorizontalScroll.Visible = false;
            }
        }

        /// <summary>
        /// Re-agenda los ajustes de layout (apilado/compacto/scroll) después de que el
        /// TableLayoutPanel haya reasentado alturas naturales (p. ej. tras mostrar la
        /// banda de campos); evita medir alturas frescas dentro del propio evento.
        /// </summary>
        private void ReprogramarLayoutVertical()
        {
            // Sincronizar los ajustes (restack/compacto/altura) tras un cambio de
            // chrome; se ejecuta cuando el TLP ya reasentó sus alturas.
            if (!IsHandleCreated)
            {
                RecalibrarAlturasCromo();
                AplicarModoScroll();
                return;
            }

            // Coalescencia fuera de la supresión: un pase ya agendado verá el estado
            // acumulado y basta. Con supresión activa cada pase se agenda y se cuenta,
            // para liberar el redibujado exactamente cuando se agoten.
            if (_reprogramacionPendiente && !_redibujoSuprimido)
                return;

            _reprogramacionPendiente = true;
            int generacion = _generacionLayout;
            _pasesReprogramacionPendientes++;
            try
            {
                BeginInvoke(new Action(() => EjecutarPaseReprogramacion(generacion)));
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                // El panel se cerró antes del re-agendado: no es parte del contrato.
                _reprogramacionPendiente = false;
                _pasesReprogramacionPendientes--;
                if (_redibujoSuprimido && _profundidadSupresion == 0 && _pasesReprogramacionPendientes == 0)
                    RestaurarRedibujoSubarbol();
            }
        }

        private void EjecutarPaseReprogramacion(int generacion)
        {
            // Un callback de una generación vieja (handle destruido/recreado) NO toca
            // el estado de la generación nueva: ni flag, ni contadores, ni trabajo.
            bool vigente = generacion == _generacionLayout;
            if (vigente)
                _reprogramacionPendiente = false;

            try
            {
                if (IsDisposed || !vigente)
                    return;

                ApilarCamposSegunAncho();
                AjustarNavegacionSegunAncho();
                AplicarModoCompacto();
                RecalibrarAlturasCromo();
                AplicarModoScroll();
                PerformLayout();
                // Los pases diferidos son el último punto donde la geometría ya está
                // asentada: reafirma la visibilidad nativa de lo que la presentación
                // dejó oculto (WinForms puede retener el bit WS_VISIBLE de un control
                // ocultado mientras un ancestro aún no tenía handle o estaba oculto).
                GridRedrawHelper.SincronizarVisibilidadNativa(this);
            }
            finally
            {
                if (vigente)
                {
                    _pasesReprogramacionPendientes--;
                    if (_redibujoSuprimido && _profundidadSupresion == 0 && _pasesReprogramacionPendientes == 0)
                        RestaurarRedibujoSubarbol();
                }
            }
        }

        /// <summary>
        /// Adquiere un turno de supresión de redibujado del panel y TODOS sus
        /// descendientes (WM_SETREDRAW) para que la ráfaga de cambio de concepto se
        /// presente en una sola pintura final. Llamadas anidadas (transición dentro de
        /// transición, bombeo de un aviso dentro de la ráfaga) sólo suben la
        /// profundidad: el redibujado se restaura al salir del burst externo con los
        /// pases agotados. Es no-op (y no toma turno) sin handle. Devuelve true si el
        /// llamador adquirió un turno que debe liberar en su finally.
        /// </summary>
        private bool SuprimirRedibujoSubarbol()
        {
            if (!IsHandleCreated)
                return false;

            _profundidadSupresion++;
            if (_redibujoSuprimido)
                return true;

            _redibujoSuprimido = true;
            bool completo;
            try
            {
                completo = GridRedrawHelper.SetRedrawArbol(this, habilitar: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"PanelMatricesEmbebido.SuprimirRedibujoSubarbol: {ex.Message}");
                completo = false;
            }
            if (!completo)
            {
                // Fallo parcial: mejor soltar la supresión (re-habilita best-effort y
                // limpia el flag) que dejar controles congelados.
                RestaurarRedibujoSubarbol();
                _profundidadSupresion = Math.Max(0, _profundidadSupresion - 1);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Restablece el redibujado del subárbol (hijos → raíz), invalida y repinta el
        /// estado final. Idempotente: el flag se limpia SIEMPRE en finally, ante
        /// fallo, disposición o ausencia de handle.
        /// </summary>
        private void RestaurarRedibujoSubarbol()
        {
            if (!_redibujoSuprimido)
                return;

            try
            {
                if (!IsDisposed)
                    GridRedrawHelper.SetRedrawArbol(this, habilitar: true);
                if (!IsDisposed && IsHandleCreated)
                {
                    // Sincroniza el bit nativo de los controles ocultos durante la
                    // ráfaga y repinta el subárbol completo en una sola pasada: un
                    // Update() de la raíz pinta su fondo y deja un frame en blanco
                    // hasta que los hijos reciben sus WM_PAINT.
                    GridRedrawHelper.SincronizarVisibilidadNativa(this);
                    GridRedrawHelper.RedibujarArbol(this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"PanelMatricesEmbebido.RestaurarRedibujoSubarbol: {ex.Message}");
            }
            finally
            {
                _redibujoSuprimido = false;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Tras un recreate el chrome puede quedar con alturas de la generación
            // anterior: agendar un pase final de reasentado.
            ReprogramarLayoutVertical();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            // Fin del ciclo de handle: un recreate no debe heredar supresión,
            // profundidad, contadores ni callbacks de una generación vieja. El
            // re-habilitado es best-effort (hijos que aún conserven handle).
            if (_redibujoSuprimido)
                RestaurarRedibujoSubarbol();
            _generacionLayout++;
            _profundidadSupresion = 0;
            _pasesReprogramacionPendientes = 0;
            _reprogramacionPendiente = false;
            base.OnHandleDestroyed(e);
        }

        // Iguala la altura de la fila "Agregar" midiendo en tiempo de ejecución la
        // altura NATURAL de los 5 botones (la que necesita su texto en negrita) y
        // aplicándola también a la etiqueta, para compartir línea base. Respeta el
        // escalado por DPI porque se mide en runtime.
        private void IgualarAlturaAgregar()
        {
            var botones = new[]
            {
                _btnAgregarMaterial, _btnAgregarMO, _btnAgregarMaquinaria,
                _btnAgregarHerramienta, _btnAgregarBasico
            };

            // Mide la altura preferida (natural) de cada botón; se parte de un piso 0 para
            // que PreferredSize no devuelva un piso previo ya aplicado.
            int altoComun = 0;
            foreach (var boton in botones)
            {
                if (boton == null) continue;
                boton.MinimumSize = new Size(boton.MinimumSize.Width, 0);
                altoComun = System.Math.Max(altoComun, boton.PreferredSize.Height);
            }
            if (altoComun <= 0) return;

            foreach (var boton in botones)
            {
                if (boton == null) continue;
                // Piso de altura = altura natural; sin tope que recorte el texto.
                boton.MinimumSize = new Size(boton.MinimumSize.Width, altoComun);
                boton.MaximumSize = Size.Empty;
            }

            if (_lblAgregar != null)
            {
                // El label con AutoSize no centra su texto cuando la caja crece por
                // MinimumSize (el texto se ancla arriba). Se mide el ancho natural con
                // AutoSize aún activo y luego se fija la MISMA altura de caja que los
                // botones, con el texto centrado verticalmente (MiddleLeft), de modo que
                // ambos compartan línea base.
                int anchoEtiqueta = _lblAgregar.AutoSize
                    ? _lblAgregar.PreferredSize.Width
                    : _lblAgregar.Width;
                if (anchoEtiqueta <= 0)
                {
                    anchoEtiqueta = TextRenderer.MeasureText(_lblAgregar.Text, _lblAgregar.Font).Width;
                }
                _lblAgregar.AutoSize = false;
                _lblAgregar.TextAlign = ContentAlignment.MiddleLeft;
                _lblAgregar.MinimumSize = Size.Empty;
                _lblAgregar.Height = altoComun;
                _lblAgregar.Width = anchoEtiqueta;
                // Mismo margen superior (0) que los botones: sus textos comparten línea base.
                _lblAgregar.Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0);
            }
        }

        // Ajusta alturas, rellenos y alineación de los botones de contenido según el modo
        // compacto; los chips de tipo viven en el header y no participan del rellenado.
        private void AplicarMetricasContenido(bool compacto)
        {
            var rellenoAgregar = compacto
                ? new Padding(SoproUiMetrics.CompactButtonPadding.Left, 0, SoproUiMetrics.CompactButtonPadding.Right, 0)
                : new Padding(SoproUiMetrics.ContentButtonPadding.Left, 0, SoproUiMetrics.ContentButtonPadding.Right, 0);

            foreach (var boton in new[]
                     {
                         _btnAgregarMaterial, _btnAgregarMO, _btnAgregarMaquinaria,
                         _btnAgregarHerramienta, _btnAgregarBasico
                     })
            {
                if (boton == null) continue;
                boton.Padding = rellenoAgregar;
            }

            IgualarAlturaAgregar();
        }
    }
}
