using System.Drawing;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Controls
{
    public partial class PanelMatricesEmbebido
    {
        // Altura (px) por debajo de la cual se activa el modo compacto.
        private const int AlturaModoCompacto = 210;

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
        private static readonly Color ColorGuardar = Color.FromArgb(56, 142, 60);
        private static readonly Color ColorNeutro = Color.FromArgb(120, 120, 120);
        private static readonly Color ColorCostoDirecto = Color.FromArgb(46, 125, 50);
        private static readonly Color ColorEliminar = Color.FromArgb(150, 0, 0);

        private TableLayoutPanel _rootLayout;
        private bool _modoCompacto;
        private Label _lblAgregar;

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = new Font("Segoe UI", 9F);

            // Estructura vertical declarativa: header / datos / agregar / grid / totales.
            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // header
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // datos (solo edición)
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // agregar
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // grid: única fila flexible
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // totales

            ConstruirHeader();
            ConstruirDatos();
            ConstruirAgregar();
            ConstruirGrid();
            ConstruirTotales();

            // Se fija cada control a su celda explícita (columna, fila) para que el
            // layout no dependa del z-order de inserción: header, grid y totales quedan
            // separados por fila, no por orden de Controls.Add/BringToFront.
            _rootLayout.Controls.Add(_panelHeader, 0, 0);
            _rootLayout.Controls.Add(_panelDatos, 0, 1);
            _rootLayout.Controls.Add(_panelBotonesAgregar, 0, 2);
            _rootLayout.Controls.Add(_dgvComponentes, 0, 3);
            _rootLayout.Controls.Add(_panelTotales, 0, 4);

            this.Controls.Add(_rootLayout);

            this.Resize += (s, e) => AplicarModoCompacto();
            // Re-medir tras cargar para afinar la alineación si el escalado por DPI cambió la fuente.
            this.Load += (s, e) =>
            {
                IgualarAlturaAcciones();
                IgualarAlturaAgregar();
            };
            AplicarModoCompacto();
        }

        private void ConstruirHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = ColorHeaderOscuro,
                Margin = new Padding(0),
                Padding = new Padding(SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var textos = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            textos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            textos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textos.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblTituloMatriz = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                Margin = new Padding(0),
                Text = string.Empty
            };
            _lblInfoMatriz = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTextoSobreOscuro,
                Margin = new Padding(0),
                Text = string.Empty
            };
            textos.Controls.Add(_lblTituloMatriz, 0, 0);
            textos.Controls.Add(_lblInfoMatriz, 0, 1);

            var botonesNav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _btnAnterior = new Button
            {
                Text = "▲",
                AutoSize = false,
                Size = new Size(32, SoproUiMetrics.CompactButtonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorHeaderBoton,
                ForeColor = Color.White,
                Enabled = false,
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0)
            };
            _btnSiguiente = new Button
            {
                Text = "▼",
                AutoSize = false,
                Size = new Size(32, SoproUiMetrics.CompactButtonHeight),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorHeaderBoton,
                ForeColor = Color.White,
                Enabled = false,
                Margin = new Padding(0)
            };
            _btnAnterior.FlatAppearance.BorderColor = _btnSiguiente.FlatAppearance.BorderColor = ColorHeaderBotonBorde;
            _btnAnterior.Click += BtnAnterior_Click;
            _btnSiguiente.Click += BtnSiguiente_Click;
            botonesNav.Controls.Add(_btnAnterior);
            botonesNav.Controls.Add(_btnSiguiente);

            header.Controls.Add(textos, 0, 0);
            header.Controls.Add(botonesNav, 1, 0);
            _panelHeader = header;
        }

        private void ConstruirDatos()
        {
            var datos = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = ColorSuperficie,
                Visible = false,
                Margin = new Padding(0),
                Padding = new Padding(SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap)
            };
            datos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            datos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            datos.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Fila 1: campos. La descripción usa columna Percent flexible (sin piso duro).
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

            var lblClave = CrearEtiquetaCampo("Clave");
            _txtClaveMatriz = new TextBox
            {
                Dock = DockStyle.Fill,
                CharacterCasing = CharacterCasing.Upper,
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0)
            };
            var lblUnidad = CrearEtiquetaCampo("Unidad");
            _txtUnidadMatriz = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, SoproUiMetrics.HorizontalGap, 0)
            };
            var lblDesc = CrearEtiquetaCampo("Descripción");
            _txtDescripcionMatriz = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                MaxLength = 2000
            };

            campos.Controls.Add(lblClave, 0, 0);
            campos.Controls.Add(_txtClaveMatriz, 1, 0);
            campos.Controls.Add(lblUnidad, 2, 0);
            campos.Controls.Add(_txtUnidadMatriz, 3, 0);
            campos.Controls.Add(lblDesc, 4, 0);
            campos.Controls.Add(_txtDescripcionMatriz, 5, 0);

            // Fila 2: radios y botones en flujo autosize (nunca se encima con la descripción).
            var acciones = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _rbTipoApu = CrearRadioTipo("APU", true);
            _rbTipoBasico = CrearRadioTipo("Básico", false);
            _rbTipoCuadrilla = CrearRadioTipo("Cuadrilla", false);
            _btnGuardarMatriz = new Button
            {
                Text = "Guardar",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                // Sin piso de altura (0): su altura natural la mide IgualarAlturaAcciones,
                // que además crece los radios a esa misma altura.
                MinimumSize = new Size(SoproUiMetrics.ContentButtonMinWidth, 0),
                Padding = new Padding(SoproUiMetrics.ContentButtonPadding.Left, 0, SoproUiMetrics.ContentButtonPadding.Right, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = ColorGuardar,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Visible = false,
                Margin = new Padding(0, SoproUiMetrics.StandardMargin, SoproUiMetrics.HorizontalGap, 0)
            };
            _btnGuardarMatriz.FlatAppearance.BorderSize = 0;
            _btnCancelarMatriz = new Button
            {
                Text = "Cancelar",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                // Sin piso de altura (0): su altura natural la mide IgualarAlturaAcciones,
                // que además crece los radios a esa misma altura.
                MinimumSize = new Size(SoproUiMetrics.ContentButtonMinWidth, 0),
                Padding = new Padding(SoproUiMetrics.ContentButtonPadding.Left, 0, SoproUiMetrics.ContentButtonPadding.Right, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = ColorNeutro,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Visible = false,
                Margin = new Padding(0, SoproUiMetrics.StandardMargin, 0, 0)
            };
            _btnCancelarMatriz.FlatAppearance.BorderSize = 0;
            _btnGuardarMatriz.Click += BtnGuardarMatriz_Click;
            _btnCancelarMatriz.Click += BtnCancelarMatriz_Click;

            acciones.Controls.Add(_rbTipoApu);
            acciones.Controls.Add(_rbTipoBasico);
            acciones.Controls.Add(_rbTipoCuadrilla);
            acciones.Controls.Add(_btnGuardarMatriz);
            acciones.Controls.Add(_btnCancelarMatriz);

            datos.Controls.Add(campos, 0, 0);
            datos.Controls.Add(acciones, 0, 1);
            _panelDatos = datos;

            // Iguala la altura de la fila de acciones: los radios crecen hasta la altura
            // natural de Guardar/Cancelar (que es la que cabe su texto) para que todo quede
            // en la misma línea y la fila no se estire por los botones.
            IgualarAlturaAcciones();
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
                Padding = new Padding(SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap)
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
                Margin = new Padding(0)
            };
            // Estilo SOPRO: header claro 245,245,248 con texto 60,60,60 y filas alternas sutiles.
            _dgvComponentes.AplicarEstiloSOPRO();
            // Estilo de selección del presupuesto: fila completa azul claro + contorno azul de 2px
            // en la celda activa, sin tocar el comportamiento de portapapeles del grid.
            _dgvComponentes.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 240, 255);
            _dgvComponentes.DefaultCellStyle.SelectionForeColor = Color.Black;
            // Reafirmar la selección en filas alternas para que el zebra no se
            // imponga sobre el fondo de la fila seleccionada (selección uniforme).
            _dgvComponentes.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 240, 255);
            _dgvComponentes.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.Black;
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
            _dgvComponentes.CellMouseUp += DgvComponentes_CellMouseUp;
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
                Padding = new Padding(SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, SoproUiMetrics.VerticalGap)
            };

            var fuenteNormal = new Font("Segoe UI", 9F);
            var fuenteTotal = new Font("Segoe UI", 10F, FontStyle.Bold);

            _lblMat = CrearEtiquetaTotal(fuenteNormal, ColorTextoPrimario, new Padding(0, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, 0));
            _lblMO = CrearEtiquetaTotal(fuenteNormal, ColorTextoPrimario, new Padding(0, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, 0));
            _lblMaq = CrearEtiquetaTotal(fuenteNormal, ColorTextoPrimario, new Padding(0, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, 0));
            _lblBas = CrearEtiquetaTotal(fuenteNormal, ColorTextoPrimario, new Padding(0, SoproUiMetrics.VerticalGap, SoproUiMetrics.StandardMargin, 0));
            _lblDir = CrearEtiquetaTotal(fuenteTotal, ColorCostoDirecto, new Padding(0, SoproUiMetrics.VerticalGap, 0, 0));

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

        private static RadioButton CrearRadioTipo(string texto, bool seleccionado)
        {
            return new RadioButton
            {
                Text = texto,
                AutoSize = true,
                Checked = seleccionado,
                Margin = new Padding(0, SoproUiMetrics.StandardMargin, SoproUiMetrics.HorizontalGap, 0)
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
                // IgualarAlturaAgregar, igual que Guardar/Cancelar.
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

        // Modo compacto: en alturas bajas preserva grid, comando principal y totales esenciales,
        // y oculta lo secundario (línea de información y totales desglosados).
        private void AplicarModoCompacto()
        {
            bool compacto = this.Height > 0 && this.Height < AlturaModoCompacto;
            if (compacto == _modoCompacto) return;
            _modoCompacto = compacto;

            AplicarMetricasContenido(compacto);

            if (_lblInfoMatriz != null)
                _lblInfoMatriz.Visible = !compacto;

            if (_lblMat != null) _lblMat.Visible = !compacto;
            if (_lblMO != null) _lblMO.Visible = !compacto;
            if (_lblMaq != null) _lblMaq.Visible = !compacto;
            if (_lblBas != null) _lblBas.Visible = !compacto;
            if (_lblDir != null) _lblDir.Visible = true;
        }

        // Iguala la altura de la fila de acciones tomando como referencia la altura NATURAL
        // de Guardar/Cancelar (la que necesita su texto en negrita). Esa altura común se
        // aplica también a los radios, de modo que crecen a la par de los botones y sus
        // textos quedan en la misma línea sin recortarse.
        // La altura se mide en tiempo de ejecución para respetar el escalado por DPI.
        private void IgualarAlturaAcciones()
        {
            int altoComun = 0;
            foreach (var boton in new[] { _btnGuardarMatriz, _btnCancelarMatriz })
            {
                if (boton == null) continue;
                altoComun = System.Math.Max(altoComun, boton.PreferredSize.Height);
            }
            if (altoComun <= 0) return;

            foreach (var boton in new[] { _btnGuardarMatriz, _btnCancelarMatriz })
            {
                if (boton == null) continue;
                // Piso de altura = altura natural del botón; sin MaximumSize que recorte el
                // texto (Size.Empty = sin tope). El ancho AutoSize se conserva.
                boton.MinimumSize = new Size(SoproUiMetrics.ContentButtonMinWidth, altoComun);
                boton.MaximumSize = Size.Empty;
            }

            foreach (var radio in new[] { _rbTipoApu, _rbTipoBasico, _rbTipoCuadrilla })
            {
                if (radio == null) continue;
                // El radio crece a la altura del botón; AutoSize conserva su ancho natural.
                radio.MinimumSize = new Size(0, altoComun);
            }
        }

        // Replica el tratamiento de Guardar/Cancelar en los 5 botones "Agregar": su altura
        // es la NATURAL (la que necesita el texto) y va gobernada por el propio contenido,
        // sin piso de 30/28px ni relleno vertical. Se mide en tiempo de ejecución para
        // respetar el escalado por DPI y se aplica como piso uniforme a los 5 botones y a
        // la etiqueta "Agregar:", de modo que todos sus textos queden en la misma línea.
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

        // Ajusta alturas, rellenos y alineación de los controles de contenido según el modo.
        private void AplicarMetricasContenido(bool compacto)
        {
            int margenRadio = compacto ? SoproUiMetrics.VerticalGap : SoproUiMetrics.StandardMargin;

            // Los 5 botones "Agregar" usan el mismo tratamiento que Guardar/Cancelar: solo
            // relleno horizontal (normal vs compacto) y altura natural, sin piso de 30/28px
            // ni relleno vertical. IgualarAlturaAgregar fija la altura natural uniforme.
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

            foreach (var radio in new[] { _rbTipoApu, _rbTipoBasico, _rbTipoCuadrilla })
            {
                if (radio == null) continue;
                radio.Margin = new Padding(0, margenRadio, SoproUiMetrics.HorizontalGap, 0);
            }

            // Botones de matriz: relleno horizontal (sin relleno vertical) y el mismo margen
            // superior que los radios; su altura común la gobierna IgualarAlturaAcciones.
            var rellenoMatriz = compacto
                ? new Padding(SoproUiMetrics.CompactButtonPadding.Left, 0, SoproUiMetrics.CompactButtonPadding.Right, 0)
                : new Padding(SoproUiMetrics.ContentButtonPadding.Left, 0, SoproUiMetrics.ContentButtonPadding.Right, 0);

            if (_btnGuardarMatriz != null)
            {
                _btnGuardarMatriz.Padding = rellenoMatriz;
                _btnGuardarMatriz.Margin = new Padding(0, margenRadio, SoproUiMetrics.HorizontalGap, 0);
            }
            if (_btnCancelarMatriz != null)
            {
                _btnCancelarMatriz.Padding = rellenoMatriz;
                _btnCancelarMatriz.Margin = new Padding(0, margenRadio, 0, 0);
            }

            IgualarAlturaAcciones();
        }
    }
}
