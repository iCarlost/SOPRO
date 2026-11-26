using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SOPRO.Application.Contracts;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.UI.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarMatriz : Form
    {
        /// <summary>
        /// Se dispara cuando se guarda una matriz (nueva o editada).
        /// FormMatrices y FormPresupuesto se suscriben para refrescarse.
        /// </summary>
        public static event EventHandler MatrizGuardada;

        private readonly SOPROContext _context;
        private readonly IProjectDbContextFactory _dbContextFactory;
        private readonly int _proyectoId;
        private readonly Matriz _matriz;
        private readonly bool _esNuevo;
        private readonly TipoMatriz? _tipoInicialForzado;
        private readonly IDiagnosticsSink? _diagnosticsSink;
        private readonly string? _productVersion;
        private readonly List<ComponenteMatriz> _componentesTemp = new List<ComponenteMatriz>();

        // Staging de descripciones por fuente (insumo o auxiliar): clave
        // (TipoComponente, SourceId), con la descripción original como baseline y
        // la propuesta normalizada más reciente. El staging NO muta la entidad de
        // la fuente en la sesión; viaja como parche explícito en el DTO del
        // guardado y la Application lo confirma en la misma transacción (#121).
        private sealed record EdicionDescripcionFuente(string Original, string Propuesta);
        private readonly Dictionary<(TipoComponenteMatriz Tipo, int SourceId), EdicionDescripcionFuente> _descripcionFuentes = new();
        private Color _grpDatosBackColorOriginal;
        private readonly Color _colorApu = Color.FromArgb(245, 248, 252);
        private readonly Color _colorBasico = Color.FromArgb(255, 248, 220);
        private readonly Color _colorCuadrilla = Color.FromArgb(230, 242, 255);

        // Cromo compartido de fase 1 (issue #177): encabezado flotante, panel
        // de contenido desplazable y pie de acciones. La geometría interna de
        // los grupos existentes no cambia; solo su piel y su contenedor.
        private ChromeFlotanteHost? _hostEncabezado;
        private Panel? _panelContenido;
        private Panel? _pie;
        private Label? _rotuloEncabezado;
        private bool _organizandoContenido;

        /// <summary>Aire entre bandas y grupos del contenido (px lógicos).</summary>
        private const int AireContenido = 12;

        private decimal _totalMaterial = 0;
        private decimal _baseManoObra = 0;
        private decimal _totalManoObra = 0;
        private decimal _totalManoObraResumen = 0;
        private decimal _totalMaquinaria = 0;
        private decimal _totalBasicos = 0;
        private decimal _costoDirectoTotal = 0;
        
        public FormEditarMatriz(SOPROContext context, int proyectoId, Matriz matriz = null, TipoMatriz? tipoInicial = null, IProjectDbContextFactory? dbContextFactory = null)
            : this(context, proyectoId, matriz, tipoInicial, dbContextFactory, diagnosticsSink: null, productVersion: null)
        {
        }

        /// <summary>
        /// Seam de composición: el editor recibe el sink/versión opcionales del
        /// catálogo de matrices para propagarlos al selector de insumos que abre y
        /// al evento de un fallo de reversión del guardado. El constructor público
        /// conserva el comportamiento sin diagnóstico.
        /// </summary>
        internal FormEditarMatriz(
            SOPROContext context,
            int proyectoId,
            Matriz? matriz,
            TipoMatriz? tipoInicial,
            IProjectDbContextFactory? dbContextFactory,
            IDiagnosticsSink? diagnosticsSink,
            string? productVersion)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbContextFactory = dbContextFactory ?? new ProjectDbContextFactory();
            _proyectoId = proyectoId;
            _matriz = matriz!;
            _esNuevo = (matriz == null);
            _tipoInicialForzado = _esNuevo ? tipoInicial : null;
            _diagnosticsSink = diagnosticsSink;
            _productVersion = productVersion;
            
            // Establecer proyecto para formateo
            var proyecto = context.Proyectos.Find(proyectoId);
            if (proyecto != null)
                FormatoHelper.EstablecerProyecto(proyecto);
            
            // Suscribirse a cambios de configuración
            FormatoHelper.ConfiguracionCambiada += OnConfiguracionCambiada;
            
            InitializeComponent();
            _grpDatosBackColorOriginal = grpDatos.BackColor;
            
            dgvComponentes.AplicarEstiloSOPRO();
ConfigurarGrid();
            
            if (!_esNuevo)
            {
                CargarDatosMatriz();
            }
            else
            {
                AplicarTipoInicialSiCorresponde();
                AplicarConfiguracionTipo();
            }

            // El cromo se aplica DESPUÉS de resolver/cargar el tipo, para que el
            // título del encabezado naciente ya refleje la identidad correcta.
            AplicarCromoVisual();
        }
        

        /// <summary>
        /// Piel del editor con el lenguaje compartido de fase 1: encabezado
        /// flotante (host de módulos), panel de contenido desplazable y pie de
        /// acciones. Cambio de piel, no de esqueleto: los grupos conservan su
        /// geometría interna, tintes funcionales de tipo, nombres y eventos, y
        /// el formulario conserva su redimensionado (sin FixedDialog ni
        /// Accept/Cancel, que este editor no tenía). Se invoca tras resolver el
        /// tipo, para que el título naciente sea el correcto.
        /// </summary>
        private void AplicarCromoVisual()
        {
            BackColor = SoproTheme.Colors.CenterBg;
            SoproDialogTheme.AplicarIconoSOPRO(this);

            var encabezado = SoproDialogTheme.CrearEncabezado(
                Text,
                "Datos generales, componentes y totales de la matriz",
                out var rotuloTitulo);
            _rotuloEncabezado = rotuloTitulo;

            _pie = SoproDialogTheme.CrearPie();
            SoproDialogTheme.AplicarEstiloBotonPrimario(btnGuardar);
            SoproDialogTheme.AplicarEstiloBotonSecundario(btnCancelar);
            void AlinearAcciones() => SoproDialogTheme.AlinearBotonesPie(_pie, btnGuardar, btnCancelar);
            _pie.Resize += (_, _) => AlinearAcciones();
            btnGuardar.SizeChanged += (_, _) => AlinearAcciones();
            btnCancelar.SizeChanged += (_, _) => AlinearAcciones();
            _pie.Controls.Add(btnGuardar);
            _pie.Controls.Add(btnCancelar);

            // Los grupos existentes se reacomodan DENTRO del panel desplazable:
            // su tamaño y su contenido no cambian; en pantallas o DPI donde el
            // editor completo no cabe, el panel se recorre en vez de recortar.
            _panelContenido = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = SoproTheme.Colors.CenterBg
            };
            _panelContenido.Controls.Add(grpDatos);
            _panelContenido.Controls.Add(grpComponentes);
            _panelContenido.Controls.Add(grpResumen);
            // El reflow (apilar el resumen si el ancho no alcanza) responde a
            // ClientSizeChanged: cubre redimensionado del usuario Y los
            // altibajos de las barras de desplazamiento.
            _panelContenido.ClientSizeChanged += (_, _) => OrganizarContenido();

            Controls.Add(_panelContenido);
            Controls.Add(_pie);
            Controls.Add(encabezado);
            _hostEncabezado = ChromeFlotanteHost.Hospedar(encabezado, this, "encabezadoMatrizHost", BackColor);

            AplicarPielGrupos();

            // Conservar el ClientSize y StartPosition del diseñador (1200×800,
            // centrado). El contenido que no quepa se alcanza con scroll vertical.
            OrganizarContenido();
        }

        /// <summary>
        /// Regla compartida entre el ajuste del cliente y el reflow: los
        /// componentes y el resumen caben lado a lado en el ancho de panel
        /// indicado, reservando el ancho de la barra vertical para no oscilar.
        /// </summary>
        private static bool CabenLadoALado(int anchoPanel, int margenIzquierdo, int anchoComponentes, int anchoResumen)
            => anchoPanel >= margenIzquierdo + anchoComponentes + AireContenido
                + anchoResumen + AireContenido + SystemInformation.VerticalScrollBarWidth;

        /// <summary>
        /// Colocación de los grupos dentro del panel desplazable: el resumen
        /// va al lado de los componentes SOLO si ambos caben sin solape ni
        /// scroll horizontal; si no, se apila debajo. El resumen se alinea a
        /// la derecha del panel, así que jamás desborda por sí mismo.
        /// </summary>
        private void OrganizarContenido()
        {
            // Solo con manejo creado: pre-manejo el panel no tiene tamaño real
            // y encoger los grupos a su contenido corrompería sus anchos.
            if (_organizandoContenido
                || !IsHandleCreated
                || _panelContenido == null || _panelContenido.IsDisposed
                || grpDatos.IsDisposed)
            {
                return;
            }

            _organizandoContenido = true;
            try
            {
                int disponible = Math.Max(0, _panelContenido.ClientRectangle.Width - AireContenido);

                // Los grupos encogen hasta su contenido (sin recortar nada)
                // cuando el panel es más angosto: el ancho exterior sobrante
                // no genera scroll horizontal.
                foreach (var grupo in new[] { grpDatos, grpComponentes, grpResumen })
                {
                    grupo.Width = Math.Min(
                        grupo.Width,
                        Math.Max(AnchoMinimoVisible(grupo), disponible - grupo.Left));
                }

                grpDatos.Location = new Point(grpDatos.Left, AireContenido);
                int yContenido = grpDatos.Bottom + AireContenido;

                grpComponentes.Location = new Point(grpComponentes.Left, yContenido);

                bool ladoALado = CabenLadoALado(
                    // El umbral ya reserva el ancho de la barra vertical: se
                    // decide contra el ancho total del panel, no contra el
                    // cliente (que se merma si la barra vertical está viva por
                    // un estado previo), para que el reflow no oscile.
                    _panelContenido.Width,
                    grpComponentes.Left,
                    grpComponentes.Width,
                    grpResumen.Width);

                grpResumen.Location = ladoALado
                    ? new Point(_panelContenido.ClientSize.Width - AireContenido - grpResumen.Width, yContenido)
                    : new Point(grpComponentes.Left, grpComponentes.Bottom + AireContenido);

                // Mover los hijos deja la visibilidad de las barras pendiente
                // de un layout ajeno: forzar el recálculo aquí para que el
                // estado de scroll quede determinista tras cada reflow.
                _panelContenido.PerformLayout();
            }
            finally
            {
                _organizandoContenido = false;
            }
        }

        /// <summary>Ancho mínimo de un grupo: el hijo visible más a la derecha + aire interno.</summary>
        private static int AnchoMinimoVisible(GroupBox grupo)
        {
            int derecho = 0;
            foreach (Control hijo in grupo.Controls)
            {
                if (hijo.Visible)
                    derecho = Math.Max(derecho, hijo.Right);
            }

            return derecho + 12;
        }

        /// <summary>
        /// Tokens del tema sobre los grupos y controles existentes: rótulos
        /// Muted, campos Ink, resumen con acento de marca y botones SOPRO. El
        /// tinte funcional de <c>grpDatos</c> lo sigue gestionando
        /// <see cref="ActualizarIdentidadVisual"/>.
        /// </summary>
        private void AplicarPielGrupos()
        {
            grpDatos.Font = SoproTheme.Fonts.GroupTitle;
            grpDatos.ForeColor = SoproTheme.Colors.Ink2;
            foreach (var rotulo in new[] { lblClave, lblDescripcion, lblTipo, lblUnidad })
            {
                rotulo.Font = SoproTheme.Fonts.Subtitle;
                rotulo.ForeColor = SoproTheme.Colors.Muted;
            }
            foreach (var campo in new Control[] { txtClave, txtDescripcion, cboUnidad })
            {
                campo.Font = SoproTheme.Fonts.DialogBody;
                campo.ForeColor = SoproTheme.Colors.Ink;
            }
            foreach (var radio in new[] { rbAPU, rbBasico, rbCuadrilla })
            {
                radio.Font = SoproTheme.Fonts.Subtitle;
                radio.ForeColor = SoproTheme.Colors.Ink;
            }

            grpComponentes.Font = SoproTheme.Fonts.GroupTitle;
            grpComponentes.ForeColor = SoproTheme.Colors.Ink2;
            grpComponentes.BackColor = SoproTheme.Colors.Surface;
            foreach (var boton in new[] { btnAgregarMaterial, btnAgregarManoObra, btnAgregarMaquinaria, btnAgregarBasico, btnAgregarHerramienta })
            {
                boton.SoproIcon = SoproIconType.Mas;
                SoproDialogTheme.AplicarEstiloBotonSecundario(boton);
            }

            // Fila de altas en flujo: al ocultar botones según el tipo de
            // matriz (APU/Básico/Cuadrilla), los visibles se compactan sin
            // dejar huecos. El grid no se toca.
            var filaAltas = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(20, 30),
                Margin = new Padding(0),
                BackColor = SoproTheme.Colors.Surface
            };
            foreach (var boton in new[] { btnAgregarMaterial, btnAgregarManoObra, btnAgregarMaquinaria, btnAgregarBasico, btnAgregarHerramienta })
            {
                boton.Margin = new Padding(0, 0, 8, 0);
                filaAltas.Controls.Add(boton);
            }
            grpComponentes.Controls.Add(filaAltas);

            grpResumen.Font = SoproTheme.Fonts.GroupTitle;
            grpResumen.ForeColor = SoproTheme.Colors.Ink2;
            grpResumen.BackColor = SoproTheme.Colors.Surface;
            foreach (var rotulo in new[]
                     {
                         lblTotalMaterialLabel, lblTotalManoObraLabel, lblTotalMaquinariaLabel,
                         lblTotalBasicosLabel, lblCostoDirectoLabel
                     })
            {
                rotulo.Font = SoproTheme.Fonts.Subtitle;
                rotulo.ForeColor = SoproTheme.Colors.Muted;
            }
            foreach (var valor in new[] { lblTotalMaterial, lblTotalManoObra, lblTotalMaquinaria, lblTotalBasicos })
            {
                valor.Font = SoproTheme.Fonts.GridHeader;
                valor.ForeColor = SoproTheme.Colors.Ink;
            }
            lblCostoDirectoLabel.Font = SoproTheme.Fonts.Subtitle;
            lblCostoDirectoLabel.ForeColor = SoproTheme.Colors.Muted;
            lblCostoDirecto.Font = SoproTheme.Fonts.DialogTitle;
            lblCostoDirecto.ForeColor = SoproTheme.Colors.BrandMd;
            label1.BackColor = SoproTheme.Colors.SurfaceBorder;
        }

        private void AplicarTipoInicialSiCorresponde()
        {
            if (!_esNuevo || !_tipoInicialForzado.HasValue)
                return;

            switch (_tipoInicialForzado.Value)
            {
                case TipoMatriz.Cuadrilla:
                    rbCuadrilla.Checked = true;
                    break;
                case TipoMatriz.Basico:
                    rbBasico.Checked = true;
                    break;
                default:
                    rbAPU.Checked = true;
                    break;
            }
        }

        private void ConfigurarGrid()
        {
            dgvComponentes.AutoGenerateColumns = false;
            dgvComponentes.AllowUserToAddRows = false;
            dgvComponentes.AllowUserToDeleteRows = false;
            dgvComponentes.ReadOnly = false;          // ← ahora editable
            dgvComponentes.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            SeleccionGridHelper.ConfigurarSeleccionPorCelda(dgvComponentes);
            DgvCeldaHelper.Aplicar(
                dgvComponentes,
                conMenuCopia: true,
                SoproTheme.Colors.SelBg,
                Color.FromArgb(0, 120, 215),
                mostrarRowHeaders: false);
            dgvComponentes.RowHeadersVisible = false;
            dgvComponentes.BackgroundColor = Color.White;
            dgvComponentes.RowTemplate.Height = 30;
            
            dgvComponentes.Columns.Clear();
            
            // Tipo - solo lectura siempre
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTipo", HeaderText = "Tipo", Width = 100, ReadOnly = true
            });
            
            // Clave - solo lectura (viene del catálogo)
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colClave", HeaderText = "Clave", Width = 100, ReadOnly = true
            });
            
            // Descripción - EDITABLE (doble click)
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDescripcion", HeaderText = "Descripción",
                Width = 250, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = false
            });
            
            // Unidad - SOLO LECTURA en este editor (#121): viene del insumo y
            // editarla aquí mutaría la fuente compartida rastreada por la sesión.
            // El panel embebido conserva su propia edición (CanBeginEdit no cambia).
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUnidad", HeaderText = "Unidad", Width = 70, ReadOnly = true
            });
            
            // Cantidad - EDITABLE directo para Material; doble click abre Rendimiento para M.O./Maq.
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCantidad", HeaderText = "Cantidad", Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N6" },
                ReadOnly = false
            });
            
            // P.U. - SOLO LECTURA en este editor (#121): viene del insumo; igual que Unidad.
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPU", HeaderText = "P.U.", Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                ReadOnly = true
            });
            
            // Importe - solo lectura, calculado
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colImporte", HeaderText = "Importe", Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                ReadOnly = true
            });
            
            var colEliminar = new DataGridViewButtonColumn
            {
                Name = "colEliminar", HeaderText = "", Text = "🗑",
                UseColumnTextForButtonValue = true, Width = 40
            };
            colEliminar.DefaultCellStyle.ForeColor = Color.Red;
            dgvComponentes.Columns.Add(colEliminar);

            foreach (DataGridViewColumn columna in dgvComponentes.Columns)
                columna.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvComponentes.CellClick         += DgvComponentes_CellClick;
            dgvComponentes.CellDoubleClick    += DgvComponentes_CellDoubleClick;
            dgvComponentes.CellEndEdit        += DgvComponentes_CellEndEdit;
            dgvComponentes.CellBeginEdit      += DgvComponentes_CellBeginEdit;
            dgvComponentes.CellFormatting     += DgvComponentes_CellFormatting;
        }
        
        private void CargarDatosMatriz()
        {
            // Copias de trabajo DESACOPLADAS (#121): los componentes y sus
            // fuentes se cargan con AsNoTracking para que las ediciones del
            // editor (cantidad, importes, staging de descripción) jamás muten
            // entidades rastreadas por la sesión. Sin esto, una edición seguida
            // de Cancelar quedaba como pendiente en el tracker y un SaveChanges
            // ajeno posterior la confirmaba a escondidas. La cabecera sigue en
            // los controles y la entidad Matriz permanece rastreada (sólo se
            // lee). Con no-tracking la consulta materializa valores de la BASE,
            // así que un precio/costo pendiente ajeno en la sesión no contamina
            // los totales ni el DTO del editor.
            var componentes = _context.ComponentesMatriz
                .AsNoTracking()
                .Include(c => c.Material)
                .Include(c => c.ManoDeObra)
                .Include(c => c.Maquinaria)
                .Include(c => c.Auxiliar)
                .Include(c => c.Herramienta)
                .Where(c => c.MatrizId == _matriz.Id)
                .ToList();

            var loadState = MatrixEditorInitializationService.BuildLoadState(_matriz, componentes);

            txtClave.Text = loadState.Clave;
            txtDescripcion.Text = loadState.Descripcion;
            cboUnidad.Text = loadState.Unidad;

            rbAPU.Checked = loadState.Tipo == TipoMatriz.APU;
            rbCuadrilla.Checked = loadState.Tipo == TipoMatriz.Cuadrilla;
            rbBasico.Checked = loadState.Tipo == TipoMatriz.Basico;

            MatrixComponentCollectionService.LoadInto(_componentesTemp, loadState.Componentes);
            AplicarConfiguracionTipo();
            ActualizarGridComponentes();
            Recalcular();
        }
        
        private void btnAgregarMaterial_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.Material);
        }

        private void btnAgregarManoObra_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.ManoDeObra);
        }

        private void btnAgregarMaquinaria_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.Maquinaria);
        }

        private void btnAgregarBasico_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.Auxiliar);
        }

        private void btnAgregarHerramienta_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.Herramienta);
        }

        private void AgregarComponentesDesdeSelector(TipoComponenteMatriz tipoComponente)
        {
            var selectedComponents = ComponentSelectorDialogService.SelectComponents(
                this, _context, _proyectoId, tipoComponente, _diagnosticsSink, _productVersion);

            // Los componentes del selector pueden traer navegaciones rastreadas
            // por la sesión: se reconstruyen con instantáneas frescas de la base
            // (AsNoTracking) ANTES de entrar a la lista de trabajo, conservando
            // cantidad/orden/notas seleccionadas, para que los totales y el DTO
            // usen el precio/costo persistido y no un pendiente ajeno (#121).
            NormalizarFuentesSeleccionadas(selectedComponents);

            var selectionResult = MatrixComponentSelectionFlowService.AddSelectedComponents(_componentesTemp, selectedComponents);

            if (!selectionResult.HasComponents)
            {
                return;
            }

            ActualizarGridComponentes();

            if (selectionResult.RequiresRecalculation)
            {
                Recalcular();
            }
        }

        /// <summary>
        /// Rebuilds the sources of the components selected by the selector with
        /// detached DB snapshots (AsNoTracking): it does not alter the shared
        /// selection services, and it ensures the editor's working copies never
        /// reference entities tracked by the session. A pending source price
        /// modification of another flow in the tracker stays pending and out of
        /// the editor's totals/DTO.
        /// </summary>
        internal void NormalizarFuentesSeleccionadas(IReadOnlyList<ComponenteMatriz> componentes)
        {
            if (componentes == null) throw new ArgumentNullException(nameof(componentes));

            foreach (var comp in componentes)
            {
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material when comp.MaterialId > 0:
                        comp.Material = _context.Materiales.AsNoTracking().Single(m => m.Id == comp.MaterialId);
                        break;
                    case TipoComponenteMatriz.ManoDeObra when comp.ManoDeObraId > 0:
                        comp.ManoDeObra = _context.ManoDeObra.AsNoTracking().Single(m => m.Id == comp.ManoDeObraId);
                        break;
                    case TipoComponenteMatriz.Maquinaria when comp.MaquinariaId > 0:
                        comp.Maquinaria = _context.Maquinaria.AsNoTracking().Single(m => m.Id == comp.MaquinariaId);
                        break;
                    case TipoComponenteMatriz.Auxiliar when comp.AuxiliarId > 0:
                        comp.Auxiliar = _context.Matrices.AsNoTracking().Single(m => m.Id == comp.AuxiliarId);
                        break;
                    case TipoComponenteMatriz.Herramienta when comp.HerramientaId > 0:
                        comp.Herramienta = _context.Herramientas.AsNoTracking().Single(m => m.Id == comp.HerramientaId);
                        break;
                }
            }
        }

        private int DecimalesImporte => _context.Proyectos.Find(_proyectoId)?.DecimalesImporte ?? 2;

        internal DataGridView DgvComponentesParaPrueba => dgvComponentes;

        internal IDiagnosticsSink? DiagnosticsSink => _diagnosticsSink;

        internal new string? ProductVersion => _productVersion;

        private void DgvComponentes_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Value == null) return;
            var col = dgvComponentes.Columns[e.ColumnIndex].Name;
            if ((col == "colPU" || col == "colImporte") && e.Value is decimal v)
            {
                e.Value = v.ToString($"C{DecimalesImporte}",
                    System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private void ActualizarGridComponentes(DataGridViewStateSnapshot? estado = null)
        {
            DataGridViewStateSnapshot estadoActual = estado ?? DataGridViewStateHelper.Capture(dgvComponentes);

            dgvComponentes.Rows.Clear();

            var rows = MatrixComponentPresentationService.BuildRows(_componentesTemp, _baseManoObra);
            foreach (var row in rows)
            {
                dgvComponentes.Rows.Add(
                    row.Tipo,
                    row.Clave,
                    row.Descripcion,
                    row.Unidad,
                    row.Cantidad,
                    row.PrecioUnitario,
                    row.Importe);
            }

            SuperponerDescripcionesEditadas();
            DataGridViewStateHelper.Restore(dgvComponentes, estadoActual);
        }

        /// <summary>
        /// Superpone sobre el grid la descripción en borrador de cada fuente con
        /// edición pendiente: la presentación lee la descripción sin mutar de la
        /// fuente, así que el rebuild debe reponer el texto propuesto en todas
        /// las filas que referencian la misma fuente (#121).
        /// </summary>
        private void SuperponerDescripcionesEditadas()
        {
            if (_descripcionFuentes.Count == 0)
            {
                return;
            }

            int columna = dgvComponentes.Columns["colDescripcion"].Index;
            int filas = Math.Min(dgvComponentes.Rows.Count, _componentesTemp.Count);

            for (int fila = 0; fila < filas; fila++)
            {
                if (TryObtenerFuente(_componentesTemp[fila], out var clave, out _)
                    && _descripcionFuentes.TryGetValue(clave, out var edicion))
                {
                    dgvComponentes.Rows[fila].Cells[columna].Value = edicion.Propuesta;
                }
            }
        }

        private void Recalcular(DataGridViewStateSnapshot? estado = null)
        {
            var totales = MatrixComponentCalculationService.RecalculateParaVista(_componentesTemp, FormatoHelper.DecimalesImporte);

            _totalMaterial = totales.TotalMaterial;
            _baseManoObra = totales.BaseManoObra;
            _totalManoObra = totales.TotalManoObra;
            _totalManoObraResumen = totales.TotalManoObraResumen;
            _totalMaquinaria = totales.TotalMaquinaria;
            _totalBasicos = totales.TotalBasicos;
            _costoDirectoTotal = totales.CostoDirectoTotal;

            ActualizarLabelsResumen();
            ActualizarGridComponentes(estado);
        }

        private void ActualizarLabelsResumen()
        {
            lblTotalMaterial.Text = _totalMaterial.ToStringImporte();
            lblTotalManoObra.Text = _totalManoObraResumen.ToStringImporte();
            lblTotalMaquinaria.Text = _totalMaquinaria.ToStringImporte();
            lblTotalBasicos.Text = _totalBasicos.ToStringImporte();
            lblCostoDirecto.Text = _costoDirectoTotal.ToStringImporte();
        }
        
        private void DgvComponentes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (!MatrixComponentGridFlowService.IsDeleteRequest(
                    e.RowIndex,
                    e.ColumnIndex,
                    dgvComponentes.Columns["colEliminar"].Index))
            {
                return;
            }

            var result = MessageBox.Show(
                "¿Eliminar este componente?",
                "Confirmar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes
                && MatrixComponentCollectionService.RemoveAt(_componentesTemp, e.RowIndex))
            {
                DataGridViewStateSnapshot estado = DataGridViewStateHelper.Capture(dgvComponentes);
                ActualizarGridComponentes(estado);
                Recalcular(estado);
            }
        }
        
        /// <summary>
        /// Doble click en la columna Cantidad de M.O. o Maquinaria o Cuadrilla → abre FormRendimiento.
        /// Para Material → edición directa (no hace nada especial aquí).
        /// </summary>
        private void DgvComponentes_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!MatrixComponentCollectionService.TryGetAt(_componentesTemp, e.RowIndex, out var comp) || comp == null)
            {
                return;
            }

            var columnName = dgvComponentes.Columns[e.ColumnIndex].Name;
            if (!MatrixComponentGridFlowService.ShouldOpenRendimientoDialog(comp, columnName))
            {
                return;
            }

            // Cancelar la edición directa que inicia el doble click
            dgvComponentes.CancelEdit();

            var nombre = MatrixComponentInteractionService.GetRendimientoDialogName(comp);

            using var frmRend = new FormRendimiento(nombre, comp.Cantidad);
            if (frmRend.ShowDialog(this) == DialogResult.OK)
            {
                var editResult = MatrixComponentEditingService.UpdateQuantityFromDialog(comp, frmRend.Cantidad);
                if (editResult.Success)
                {
                    DataGridViewStateSnapshot estado = DataGridViewStateHelper.Capture(dgvComponentes);
                    BeginInvoke(new Action(() => { ActualizarGridComponentes(estado); Recalcular(estado); }));
                }
            }
        }
        
        /// <summary>
        /// Al empezar a editar: bloquear Cantidad en M.O./Maquinaria (se edita con FormRendimiento).
        /// EXCEPTO MO con %MO que se edita directamente.
        /// P.U. es editable para todos los tipos.
        /// </summary>
        private void DgvComponentes_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!MatrixComponentCollectionService.TryGetAt(_componentesTemp, e.RowIndex, out var comp) || comp == null)
            {
                return;
            }

            var columnName = dgvComponentes.Columns[e.ColumnIndex].Name;

            // Sólo lectura en ESTE editor (#121): Unidad y P.U. provienen del
            // insumo y su edición por aquí mutaría la fuente compartida en la
            // sesión. El panel embebido conserva su propia edición: no se toca
            // MatrixComponentGridFlowService.CanBeginEdit.
            if (columnName is "colUnidad" or "colPU")
            {
                e.Cancel = true;
                return;
            }

            if (!MatrixComponentGridFlowService.CanBeginEdit(comp, columnName))
            {
                e.Cancel = true;
            }
        }
        
        /// <summary>
        /// Al terminar de editar: actualizar componente y recalcular.
        /// Usa BeginInvoke para evitar error reentrante de SetCurrentCellAddressCore.
        /// </summary>
        private void DgvComponentes_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            DataGridViewStateSnapshot estado = DataGridViewStateHelper.Capture(dgvComponentes);

            if (!MatrixComponentCollectionService.TryGetAt(_componentesTemp, e.RowIndex, out var comp) || comp == null)
            {
                return;
            }

            var cell = dgvComponentes.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var value = cell.Value?.ToString() ?? string.Empty;
            var columnName = dgvComponentes.Columns[e.ColumnIndex].Name;

            // La descripción NO muta la fuente en la sesión (#121): se registra
            // en el staging por (tipo, id de fuente) y viaja como parche en el
            // DTO del guardado. El rebuild superpone el borrador en todas las
            // filas que referencian la misma fuente.
            if (columnName == "colDescripcion")
            {
                EstablecerDescripcionComponente(comp, value);
                BeginInvoke(new Action(() => ActualizarGridComponentes(estado)));
                return;
            }

            var editResult = MatrixComponentGridFlowService.ApplyCellEdit(
                comp,
                columnName,
                value,
                FormatoHelper.DecimalesImporte);

            if (editResult == null)
            {
                return;
            }

            if (!editResult.Success)
            {
                BeginInvoke(new Action(() => ActualizarGridComponentes(estado)));
                return;
            }

            if (editResult.RequiresRecalculation)
            {
                BeginInvoke(new Action(() => Recalcular(estado)));
            }
            else
            {
                BeginInvoke(new Action(() => ActualizarGridComponentes(estado)));
            }
        }
        
        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            var validation = MatrixSaveFlowService.ValidateBeforeSave(txtClave.Text, txtDescripcion.Text, _componentesTemp);
            if (!validation.IsValid)
            {
                MessageBox.Show(validation.Message, "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                FocusValidationField(validation.Field);
                return;
            }

            // La matriz editada puede llegar con cambios pendientes de otra edición
            // en el contexto de sesión (p. ej. una escritura heredada del panel
            // embebido aún sin confirmar). El commit por operación reescribiría esa
            // fila y la reconciliación posterior la marcaría Unchanged con los
            // valores de esta edición, descartando silenciosamente el trabajo
            // ajeno: se bloquea ANTES de cualquier escritura, sin mutar el tracker.
            if (TieneCambiosPendientesEnLaMatriz())
            {
                MessageBox.Show(MensajeMatrizConCambiosPendientes,
                    "Cambios sin guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var claveDuplicada = await MatrixApplicationService.ExistsByKeyAsync(
                    _context,
                    _proyectoId,
                    txtClave.Text,
                    _esNuevo ? null : _matriz?.Id);

                if (claveDuplicada)
                {
                    MessageBox.Show(MatrixSaveFlowService.GetDuplicateKeyMessage(), "Clave Duplicada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtClave.Focus();
                    return;
                }

                var dto = BuildMatrixEditDto();
                // La escritura ocurre en un contexto por operación sobre la ruta de la
                // base; el contexto de la sesión no se guarda ni se limpia. Se aporta
                // como sessionContext para que la guarda previa del servicio rechace,
                // antes de escribir, cualquier edición pendiente ajena sobre la
                // matriz objetivo o sobre las matrices que dependen de ella (la
                // cascada las recalcularía y un guardado compartido posterior podría
                // sobrescribirlas). Las ediciones propias de los componentes de
                // trabajo del editor no son conflicto: viajan en el DTO. El refresco
                // de MatrizGuardada descarta lo rastreado si no hay cambios
                // pendientes o advierte cuando el tracker está sucio.
                var result = await MatrixApplicationService.SaveAsync(
                    _dbContextFactory,
                    _context.DatabasePath,
                    dto,
                    _esNuevo ? null : _matriz,
                    _context,
                    _diagnosticsSink,
                    _productVersion);

                // Los componentes de trabajo son copias desacopladas (#121), así
                // que normalmente no hay entradas rastreadas que reconciliar; la
                // resincronización de la matriz y la desconexión del lote se
                // conservan como defensa (p. ej. un flujo legado que aún rastree
                // instancias de la matriz objetivo), para que un SaveChanges
                // ajeno posterior no pise los valores recién confirmados.
                ReconciliarRastreadosPropios(dto);

                // Las descripciones de fuentes confirmadas viajaron como parches en
                // el DTO y la sesión nunca las mutó: resincronizar las entradas
                // rastreadas con el valor confirmado ANTES del aviso de cambios
                // pendientes y del evento de refresco (#121). Un fallo aquí no
                // deshace el commit ni invalida la base: sólo deja la vista de
                // sesión desalineada y se anuncia sin reclamar fallo del guardado.
                string? falloReconciliacionDescripciones = null;
                try
                {
                    ReconciliarDescripcionesConfirmadas();
                }
                catch (Exception ex)
                {
                    falloReconciliacionDescripciones = ex.Message;
                }

                _descripcionFuentes.Clear();

                var mensajeExito = MatrixSaveFlowService.GetSuccessMessage(result.IsNew);
                if (_context.ChangeTracker.HasChanges())
                {
                    mensajeExito += "\n\nAtención: hay cambios sin guardar en el proyecto; el catálogo de matrices no se recargará hasta confirmarlos o descartarlos.";
                }

                MessageBox.Show(mensajeExito, "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (falloReconciliacionDescripciones != null)
                {
                    MessageBox.Show(
                        "La matriz se guardó correctamente, pero la vista de la sesión no pudo " +
                        "sincronizar las descripciones editadas; recargue el catálogo para ver el " +
                        "estado confirmado.\n\n" + falloReconciliacionDescripciones,
                        "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                DialogResult = DialogResult.OK;
                MatrizGuardada?.Invoke(null, EventArgs.Empty);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(MatrixSaveFlowService.BuildErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FocusValidationField(MatrixEditorField field)
        {
            switch (field)
            {
                case MatrixEditorField.Clave:
                    txtClave.Focus();
                    break;
                case MatrixEditorField.Descripcion:
                    txtDescripcion.Focus();
                    break;
                case MatrixEditorField.Componentes:
                    dgvComponentes.Focus();
                    break;
            }
        }

        /// <summary>
        /// Mensaje del bloqueo de guardado cuando la matriz editada ya tenía
        /// cambios pendientes en el contexto de sesión, ajenos a esta edición.
        /// </summary>
        private const string MensajeMatrizConCambiosPendientes =
            "Esta matriz tiene cambios sin guardar de otra edición en la sesión de trabajo. " +
            "Guarde o descarte esos cambios y vuelva a abrir el editor antes de guardar.";

        /// <summary>
        /// Detecta cambios pendientes de la matriz editada en el contexto de sesión
        /// que no provienen de esta edición: el editor nunca muta la entidad
        /// <see cref="Matriz"/> (solo sus componentes de trabajo, que el DTO captura
        /// y el commit por operación confirma). Con ese estado previo el guardado
        /// se bloquea antes del commit: la reconciliación posterior sincronizaría
        /// la fila con los valores confirmados y descartaría silenciosamente el
        /// trabajo ajeno.
        /// </summary>
        internal bool TieneCambiosPendientesEnLaMatriz()
        {
            if (_esNuevo || _matriz == null) return false;

            _context.ChangeTracker.DetectChanges();
            var entrada = _context.Entry(_matriz);
            return entrada.State is EntityState.Modified or EntityState.Deleted;
        }

        /// <summary>
        /// Reconciliación de los rastreados PROPIOS tras el commit por operación
        /// (#121), con el mismo patrón que la ruta de comandos del panel de
        /// matrices embebido: las entidades modificadas ajenas se conservan y el
        /// ChangeTracker de la sesión jamás se limpia ni se guarda.
        ///
        /// Desde el staging desacoplado, el editor trabaja con copias AsNoTracking
        /// y normalmente no hay componentes rastreados que tocar; el método se
        /// conserva como defensa en profundidad.
        ///
        /// La matriz editada se resincroniza con los valores confirmados y queda
        /// Unchanged con OriginalValues alineados: un SaveChanges ajeno posterior
        /// la reescribe como no-op en lugar de pisar el commit. Si la entrada de la
        /// matriz ya estaba Modified/Deleted (trabajo ajeno que la guarda de
        /// guardado habría bloqueado antes del commit), NO se toca: la
        /// resincronización descartaría silenciosamente ese trabajo.
        ///
        /// El commit eliminó y reemplazó el lote COMPLETO de componentes de la
        /// matriz editada. Con copias desacopladas no hay entradas rastreadas del
        /// lote; si alguna instancia de la matriz objetivo llegara a estar
        /// rastreada por un flujo legado (en lista o fuera de ella), se
        /// desconecta, para que ningún SaveChanges ajeno intente actualizar filas
        /// inexistentes (DbUpdateConcurrencyException). Las entradas de OTRAS
        /// matrices y las ediciones ajenas no se tocan.
        /// </summary>
        internal void ReconciliarRastreadosPropios(MatrixEditDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            if (!_esNuevo && _matriz != null)
            {
                var entradaMatriz = _context.Entry(_matriz);
                bool trabajoAjenoPendiente = entradaMatriz.State is EntityState.Modified or EntityState.Deleted;

                if (!trabajoAjenoPendiente)
                {
                    _matriz.Clave = dto.Clave.Trim().ToUpperInvariant();
                    _matriz.Descripcion = dto.Descripcion.Trim();
                    _matriz.Unidad = dto.Unidad.Trim();
                    _matriz.Tipo = dto.Tipo;
                    _matriz.CostoDirecto = dto.CostoDirecto;
                    _matriz.ProyectoId = dto.ProyectoId;
                    _matriz.Notas ??= string.Empty;
                    _matriz.FechaModificacion = DateTime.Now;
                }

                if (entradaMatriz.State != EntityState.Detached && !trabajoAjenoPendiente)
                {
                    entradaMatriz.OriginalValues.SetValues(entradaMatriz.CurrentValues);
                    entradaMatriz.State = EntityState.Unchanged;
                }

                // Lote reemplazado de la matriz objetivo: en lista o fuera de ella
                // (un componente quitado de la lista sigue rastreado). ToList:
                // cambiar estados mientras se enumera el tracker no es seguro.
                int matrizIdObjetivo = _matriz.Id;
                foreach (var entrada in _context.ChangeTracker.Entries<ComponenteMatriz>().ToList())
                {
                    if (entrada.Entity.MatrizId == matrizIdObjetivo
                        && entrada.State != EntityState.Detached)
                    {
                        entrada.State = EntityState.Detached;
                    }
                }
            }

            foreach (var componente in _componentesTemp)
            {
                var entradaComponente = _context.Entry(componente);
                if (entradaComponente.State != EntityState.Detached)
                {
                    entradaComponente.State = EntityState.Detached;
                }
            }
        }

        /// <summary>
        /// Registra en el staging la descripción propuesta para la fuente del
        /// componente. La propuesta se normaliza igual que la Application
        /// (recorte); el baseline es la descripción original de la fuente, que
        /// esta edición jamás muta en la sesión. Editar de vuelta al original
        /// elimina el parche, y varias filas con la misma fuente comparten una
        /// sola entrada. Un auxiliar no puede editar la descripción de la matriz
        /// que se está editando: ese texto es el campo Descripción de la matriz.
        /// </summary>
        internal void EstablecerDescripcionComponente(ComponenteMatriz componente, string texto)
        {
            if (componente == null) throw new ArgumentNullException(nameof(componente));

            if (!TryObtenerFuente(componente, out var clave, out string? descripcionOriginal))
            {
                return;
            }

            if (!_esNuevo && _matriz != null
                && clave.Tipo == TipoComponenteMatriz.Auxiliar
                && clave.SourceId == _matriz.Id)
            {
                return;
            }

            var propuesta = (texto ?? string.Empty).Trim();

            if (!_descripcionFuentes.TryGetValue(clave, out var edicion))
            {
                if (propuesta == descripcionOriginal)
                {
                    return;
                }

                _descripcionFuentes[clave] = new EdicionDescripcionFuente(descripcionOriginal, propuesta);
                return;
            }

            if (propuesta == edicion.Original)
            {
                _descripcionFuentes.Remove(clave);
                return;
            }

            _descripcionFuentes[clave] = edicion with { Propuesta = propuesta };
        }

        /// <summary>
        /// Resuelve la identidad de la fuente del componente para TODAS las
        /// clases soportadas, incluido Auxiliar cuya fuente es otra
        /// <see cref="Matriz"/>. La identidad sale de la referencia persistida
        /// (MaterialId/ManoDeObraId/...) y sólo recurre a la navegación cuando la
        /// referencia no está asignada; la descripción se lee de la navegación
        /// SIN mutarla.
        /// </summary>
        private static bool TryObtenerFuente(
            ComponenteMatriz componente,
            out (TipoComponenteMatriz Tipo, int SourceId) clave,
            out string? descripcionActual)
        {
            clave = default;
            descripcionActual = null;

            int? sourceId = componente.TipoComponente switch
            {
                TipoComponenteMatriz.Material => IdFuente(componente.MaterialId, componente.Material?.Id),
                TipoComponenteMatriz.ManoDeObra => IdFuente(componente.ManoDeObraId, componente.ManoDeObra?.Id),
                TipoComponenteMatriz.Maquinaria => IdFuente(componente.MaquinariaId, componente.Maquinaria?.Id),
                TipoComponenteMatriz.Auxiliar => IdFuente(componente.AuxiliarId, componente.Auxiliar?.Id),
                TipoComponenteMatriz.Herramienta => IdFuente(componente.HerramientaId, componente.Herramienta?.Id),
                _ => null,
            };

            if (!sourceId.HasValue)
            {
                return false;
            }

            clave = (componente.TipoComponente, sourceId.Value);
            descripcionActual = componente.TipoComponente switch
            {
                TipoComponenteMatriz.Material => componente.Material?.Descripcion,
                TipoComponenteMatriz.ManoDeObra => componente.ManoDeObra?.Descripcion,
                TipoComponenteMatriz.Maquinaria => componente.Maquinaria?.Descripcion,
                TipoComponenteMatriz.Auxiliar => componente.Auxiliar?.Descripcion,
                TipoComponenteMatriz.Herramienta => componente.Herramienta?.Descripcion,
                _ => null,
            };

            return true;
        }

        private static int? IdFuente(int? referenciaId, int? navegacionId)
            => referenciaId.HasValue && referenciaId.Value > 0
                ? referenciaId
                : (navegacionId.HasValue && navegacionId.Value > 0 ? navegacionId : null);

        /// <summary>
        /// Reconciliación de las descripciones de fuentes confirmadas por el
        /// commit por operación (#121). La edición viajó como parche en el DTO y
        /// el editor trabaja con copias desacopladas, así que hay dos superficies
        /// que resincronizar con el valor confirmado: la entrada RASTREADA de la
        /// fuente si otra flujo de la sesión la tiene en el tracker (sólo la
        /// propiedad Descripcion, current y original, preservando cualquier otra
        /// propiedad modificada ajena), y las copias de trabajo DESACOPLADAS del
        /// propio editor (las navegaciones de _componentesTemp). Nunca se guarda,
        /// limpia ni desconecta el tracker; las fuentes no rastreadas o en estado
        /// no reconciliable se omiten.
        /// </summary>
        internal void ReconciliarDescripcionesConfirmadas()
        {
            foreach (var par in _descripcionFuentes)
            {
                var entrada = BuscarEntradaFuente(par.Key.Tipo, par.Key.SourceId);
                if (entrada == null
                    || entrada.State is EntityState.Detached or EntityState.Added or EntityState.Deleted)
                {
                    continue;
                }

                var propiedad = entrada.Property(nameof(Matriz.Descripcion));
                propiedad.CurrentValue = par.Value.Propuesta;
                propiedad.OriginalValue = par.Value.Propuesta;

                // El flag IsModified es persistente: si un DetectChanges intermedio
                // lo marcó, alinear current y original no lo limpia solo. Con los
                // valores ya alineados, quitar el flag deja la entrada exactamente
                // como estaba: Unchanged si sólo cambió la descripción, Modified si
                // otra propiedad ajena sigue pendiente.
                if (propiedad.IsModified)
                {
                    propiedad.IsModified = false;
                }
            }

            // Copias de trabajo propias del editor: el valor confirmado ya está en
            // la base; la vista del editor no debe quedarse con el texto previo.
            foreach (var componente in _componentesTemp)
            {
                if (TryObtenerFuente(componente, out var clave, out _)
                    && _descripcionFuentes.TryGetValue(clave, out var edicion))
                {
                    EstablecerDescripcionEnFuente(componente, edicion.Propuesta);
                }
            }

            _context.ChangeTracker.DetectChanges();
        }

        private static void EstablecerDescripcionEnFuente(ComponenteMatriz componente, string descripcion)
        {
            switch (componente.TipoComponente)
            {
                case TipoComponenteMatriz.Material when componente.Material != null:
                    componente.Material.Descripcion = descripcion;
                    break;
                case TipoComponenteMatriz.ManoDeObra when componente.ManoDeObra != null:
                    componente.ManoDeObra.Descripcion = descripcion;
                    break;
                case TipoComponenteMatriz.Maquinaria when componente.Maquinaria != null:
                    componente.Maquinaria.Descripcion = descripcion;
                    break;
                case TipoComponenteMatriz.Auxiliar when componente.Auxiliar != null:
                    componente.Auxiliar.Descripcion = descripcion;
                    break;
                case TipoComponenteMatriz.Herramienta when componente.Herramienta != null:
                    componente.Herramienta.Descripcion = descripcion;
                    break;
            }
        }

        private EntityEntry? BuscarEntradaFuente(TipoComponenteMatriz tipo, int sourceId)
            => tipo switch
            {
                TipoComponenteMatriz.Material => _context.ChangeTracker.Entries<Material>()
                    .FirstOrDefault(entrada => entrada.Entity.Id == sourceId),
                TipoComponenteMatriz.ManoDeObra => _context.ChangeTracker.Entries<ManoDeObra>()
                    .FirstOrDefault(entrada => entrada.Entity.Id == sourceId),
                TipoComponenteMatriz.Maquinaria => _context.ChangeTracker.Entries<Maquinaria>()
                    .FirstOrDefault(entrada => entrada.Entity.Id == sourceId),
                TipoComponenteMatriz.Auxiliar => _context.ChangeTracker.Entries<Matriz>()
                    .FirstOrDefault(entrada => entrada.Entity.Id == sourceId),
                TipoComponenteMatriz.Herramienta => _context.ChangeTracker.Entries<Herramienta>()
                    .FirstOrDefault(entrada => entrada.Entity.Id == sourceId),
                _ => null,
            };

        /// <summary>Acceso del ensamblado de pruebas a los componentes de trabajo del editor.</summary>
        internal IReadOnlyList<ComponenteMatriz> ComponentesTempParaPrueba => _componentesTemp;

        /// <summary>Acceso del ensamblado de pruebas al staging de descripciones por fuente.</summary>
        internal IReadOnlyDictionary<(TipoComponenteMatriz Tipo, int SourceId), (string Original, string Propuesta)>
            EdicionesDescripcionParaPrueba
            => _descripcionFuentes.ToDictionary(par => par.Key, par => (par.Value.Original, par.Value.Propuesta));

        private MatrixEditDto BuildMatrixEditDto()
        {
            var dto = MatrixEditorService.BuildEditDto(
                _proyectoId,
                txtClave.Text,
                txtDescripcion.Text,
                cboUnidad.Text,
                rbCuadrilla.Checked,
                rbAPU.Checked,
                _costoDirectoTotal,
                _componentesTemp);

            // Parches explícitos de descripción por fuente: la Application los
            // aplica en la misma transacción del commit, con validación
            // optimista contra la descripción original. El staging no muta la
            // fuente en la sesión (#121).
            foreach (var par in _descripcionFuentes)
            {
                dto.SourceDescriptionEdits.Add(new MatrixSourceDescriptionEditDto
                {
                    TipoComponente = par.Key.Tipo,
                    SourceId = par.Key.SourceId,
                    OriginalDescription = par.Value.Original,
                    NewDescription = par.Value.Propuesta,
                });
            }

            return dto;
        }

        /// <summary>
        /// Event handler cuando cambia el tipo de matriz (APU/Basico/Cuadrilla)
        /// </summary>
        private void rbTipo_CheckedChanged(object sender, EventArgs e)
        {
            AplicarConfiguracionTipo();
        }

        private void AplicarConfiguracionTipo()
        {
            var tipoSeleccionado = MatrixEditorInitializationService.ResolveSelectedType(rbCuadrilla.Checked, rbAPU.Checked);
            var configuration = MatrixEditorInitializationService.BuildTypeConfiguration(tipoSeleccionado, cboUnidad.Text);

            btnAgregarMaterial.Visible = configuration.ShowMaterialButton;
            btnAgregarManoObra.Visible = configuration.ShowManoObraButton;
            btnAgregarMaquinaria.Visible = configuration.ShowMaquinariaButton;
            btnAgregarBasico.Visible = configuration.ShowBasicoButton;
            btnAgregarHerramienta.Visible = configuration.ShowHerramientaButton;

            if (string.IsNullOrWhiteSpace(cboUnidad.Text) && !string.IsNullOrWhiteSpace(configuration.SuggestedUnit))
            {
                cboUnidad.Text = configuration.SuggestedUnit;
            }

            ActualizarIdentidadVisual(tipoSeleccionado);
        }

        private void ActualizarIdentidadVisual(TipoMatriz tipoSeleccionado)
        {
            switch (tipoSeleccionado)
            {
                case TipoMatriz.Cuadrilla:
                    Text = "Editor de Cuadrillas";
                    grpDatos.BackColor = _colorCuadrilla;
                    break;
                case TipoMatriz.Basico:
                    Text = "Editor de Básicos";
                    grpDatos.BackColor = _colorBasico;
                    break;
                default:
                    Text = "Editor de Matriz (APU)";
                    grpDatos.BackColor = _colorApu == Color.Empty ? _grpDatosBackColorOriginal : _colorApu;
                    break;
            }

            grpDatos.ForeColor = SystemColors.ControlText;

            // El encabezado flotante acompaña SIEMPRE a la identidad del tipo:
            // Básica/Cuadrilla no pueden quedarse con el título de APU.
            if (_rotuloEncabezado != null && !_rotuloEncabezado.IsDisposed)
                _rotuloEncabezado.Text = Text;
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        
        /// <summary>
        /// Maneja el evento de cambio de configuración de decimales
        /// </summary>
        private void OnConfiguracionCambiada(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // Refrescar el grid con los nuevos decimales aunque el tab no esté activo
            if (IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) ActualizarGridComponentes(); }));
            else
                ActualizarGridComponentes();
        }
    }
}
