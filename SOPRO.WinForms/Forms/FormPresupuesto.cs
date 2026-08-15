using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormPresupuesto : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private Proyecto _proyecto;
        private bool _cargando = false;
        private bool _asignandoMatriz = false; // Suprime CellValueChanged durante asignación programática
        private Controls.PanelMatricesEmbebido _panelMatricesEmbebido;
        private SelectorApuEmbebidoControl? _selectorApuEmbebido;
        private int _selectorApuEmbebidoRowIndex = -1;
        private EventHandler _onInsumosModificados; // Guardado para poder desuscribir al cerrar
        private bool _validacionAperturaMostrada = false;
        private readonly ExternalMatrixImportService _externalMatrixImportService = new();
        private readonly ProjectIndexService _projectIndexService;
        private readonly ProjectUsageService _projectUsageService;
        private readonly CatalogSearchService _catalogSearchService;
        private ListBox? _lstApuAutocomplete;
        private TextBox? _txtDescripcionEnEdicion;
        private List<CatalogSearchResultDto> _apuAutocompleteSource = new();
        private bool _mouseDownEnAutocomplete;
        private bool _confirmandoSeleccionAutocomplete;
        private bool _autocompleteUserNavigated;
        private int _autocompleteRowIndex = -1;
        private int _autocompleteColumnIndex = -1;
        private Panel? _pnlApuPreview;
        private Label? _lblApuPreviewTitulo;
        private Label? _lblApuPreviewProyecto;
        private Label? _lblApuPreviewMeta;
        private Label? _lblApuPreviewCosto;
        private TextBox? _txtApuPreviewComponentes;
        private readonly Dictionary<string, MatrixPreviewInfo> _cachePreviewApu = new();
        private int _workspacePanelHeight = 0;
        private string WorkspacePanelStatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SOPRO", $"presupuesto_workspace_{_proyecto.Id}.txt");
        private readonly UndoManager _undoManager = new();
        private bool _isUndoRedo;
        private int _undoBudgetRowIndex = -1;
        private int _undoBudgetColumnIndex = -1;
        private string _undoBudgetColumnName = string.Empty;
        private string _undoBudgetOldValue = string.Empty;
        private ToolStripButton? _btnReajustarCosto;


        private sealed class MatrixPreviewInfo
        {
            public string Titulo { get; set; } = string.Empty;
            public string Proyecto { get; set; } = string.Empty;
            public string Meta { get; set; } = string.Empty;
            public string Costo { get; set; } = string.Empty;
            public string Componentes { get; set; } = string.Empty;
        }

        private bool DebeInterceptarTeclasAutocomplete(Keys keyData)
        {
            if (_lstApuAutocomplete is not { Visible: true } || dgvPresupuesto.CurrentCell == null)
                return false;

            if (dgvPresupuesto.CurrentCell.OwningColumn?.Tag is not ColumnaPersonalizada colDef)
                return false;

            if (!string.Equals(colDef.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase))
                return false;

            var keyCode = keyData & Keys.KeyCode;
            return keyCode == Keys.Up || keyCode == Keys.Down || keyCode == Keys.Enter || keyCode == Keys.Escape;
        }

        private bool ProcesarTeclaAutocomplete(Keys keyData)
        {
            if (!DebeInterceptarTeclasAutocomplete(keyData) || _lstApuAutocomplete == null)
                return false;

            var keyCode = keyData & Keys.KeyCode;

            if (keyCode == Keys.Down || keyCode == Keys.Up)
            {
                if (_lstApuAutocomplete.Items.Count <= 0)
                    return true;

                int currentIndex = _lstApuAutocomplete.SelectedIndex;
                int nextIndex;

                if (keyCode == Keys.Down)
                    nextIndex = currentIndex < 0 ? 0 : Math.Min(currentIndex + 1, _lstApuAutocomplete.Items.Count - 1);
                else
                    nextIndex = currentIndex < 0 ? Math.Max(_lstApuAutocomplete.Items.Count - 1, 0) : Math.Max(currentIndex - 1, 0);

                _autocompleteUserNavigated = true;
                _lstApuAutocomplete.SelectedIndex = nextIndex;
                return true;
            }

            if (keyCode == Keys.Enter)
            {
                if (!_autocompleteUserNavigated || _lstApuAutocomplete.SelectedIndex < 0)
                    return false;

                ConfirmarSeleccionAutocompleteApu(true);
                return true;
            }

            if (keyCode == Keys.Escape)
            {
                OcultarAutocompleteApu(true);
                return true;
            }

            return false;
        }

        private bool ProcesarTeclaEspecialPresupuesto(Keys keyData)
        {
            var keyCode = keyData & Keys.KeyCode;

            if (_lstApuAutocomplete is { Visible: true }
                && dgvPresupuesto.CurrentCell != null
                && dgvPresupuesto.IsCurrentCellInEditMode
                && dgvPresupuesto.CurrentCell.OwningColumn?.Tag is ColumnaPersonalizada colDef
                && string.Equals(colDef.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase)
                && (keyCode == Keys.Up || keyCode == Keys.Down || keyCode == Keys.Enter || keyCode == Keys.Escape))
            {
                return ProcesarTeclaAutocomplete(keyData);
            }

            return false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V)
                && _panelMatricesEmbebido != null
                && _panelMatricesEmbebido.Visible
                && _panelMatricesEmbebido.ContainsFocus
                && _panelMatricesEmbebido.TieneFocoEnEntradaTexto())
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (ProcesarTeclaEspecialPresupuesto(keyData))
                return true;

            if (keyData == (Keys.Control | Keys.Z))
            {
                if (_panelMatricesEmbebido != null && _panelMatricesEmbebido.Visible && _panelMatricesEmbebido.ContainsFocus && _panelMatricesEmbebido.TryUndo())
                    return true;

                if (TryUndoBudgetEdit())
                    return true;
            }

            if (keyData == (Keys.Control | Keys.Y))
            {
                if (_panelMatricesEmbebido != null && _panelMatricesEmbebido.Visible && _panelMatricesEmbebido.ContainsFocus && _panelMatricesEmbebido.TryRedo())
                    return true;

                if (TryRedoBudgetEdit())
                    return true;
            }

            var keyCode = keyData & Keys.KeyCode;
            if (keyCode == Keys.Escape
                && !splitContainer.Panel2Collapsed
                && (_selectorApuEmbebido == null || !_selectorApuEmbebido.Visible)
                && _panelMatricesEmbebido != null
                && _panelMatricesEmbebido.Visible
                && !_panelMatricesEmbebido.ContainsFocus
                && !dgvPresupuesto.IsCurrentCellInEditMode)
            {
                btnWorkspaceCerrar_Click(btnWorkspaceCerrar, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ── IGridFormato ──────────────────────────────────────────────────────
        public System.Windows.Forms.DataGridView GridPrincipal => dgvPresupuesto;
        public System.Windows.Forms.DataGridView GridBusqueda => dgvPresupuesto;

        public bool GenerarReporteExcel()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return false;

            // Abre el mismo mini-form que el botón individual
            // (permite elegir entre Presupuesto o P.U.)
            using var opciones = new FormExportarReporte(_context, _proyecto);
            opciones.ShowDialog();
            return true;
        }
        public event EventHandler ColumnaSeleccionadaCambiada;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_columnaRibbon == null) return;
            _columnaRibbon.NombreFuente = fmt.NombreFuente;
            _columnaRibbon.TamanoFuente = fmt.TamanoFuente;
            _columnaRibbon.Negrita = fmt.Negrita;
            _columnaRibbon.Cursiva = fmt.Cursiva;
            _columnaRibbon.Alineacion = fmt.Alineacion;
            _columnaRibbon.ColorFondo = fmt.ColorFondo;
            _columnaRibbon.ColorFuente = fmt.ColorFuente;
            _columnaRibbon.WrapTexto = fmt.WrapTexto;
            _columnaRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _columnaRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is not ColumnaPersonalizada cfg || !ReferenceEquals(cfg, _columnaRibbon)) continue;

                col.DefaultCellStyle.WrapMode = cfg.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                col.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(cfg.Alineacion, cfg.AlineacionVertical);

                var style = new FontStyle();
                if (cfg.Negrita) style |= FontStyle.Bold;
                if (cfg.Cursiva) style |= FontStyle.Italic;
                col.DefaultCellStyle.Font = new Font(cfg.NombreFuente ?? dgvPresupuesto.Font.Name,
                    cfg.TamanoFuente > 0 ? cfg.TamanoFuente : dgvPresupuesto.Font.Size,
                    style == 0 ? FontStyle.Regular : style);

                if (!string.IsNullOrWhiteSpace(cfg.ColorFuente))
                {
                    try { col.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml(cfg.ColorFuente); } catch { }
                }

                break;
            }

            FormatoHelper.AjustarAutoAlturaFilas(dgvPresupuesto);
            dgvPresupuesto.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (var col in _context.ColumnasPersonalizadas
                .Where(c => c.ProyectoId == _proyecto.Id))
            {
                col.NombreFuente = fmt.NombreFuente;
                col.TamanoFuente = fmt.TamanoFuente;
                col.Negrita = fmt.Negrita;
                col.Cursiva = fmt.Cursiva;
                col.Alineacion = fmt.Alineacion;
                col.ColorFuente = fmt.ColorFuente;
                col.WrapTexto = fmt.WrapTexto;
                col.AlineacionVertical = fmt.AlineacionVertical;
                col.FechaModificacion = DateTime.Now;
                // ColorFondo NO se aplica globalmente
            }
            _context.SaveChanges();

            FormatoHelper.AplicarWrapYAlineacionPersistidos(dgvPresupuesto);
            FormatoHelper.AjustarAutoAlturaFilas(dgvPresupuesto);
            dgvPresupuesto.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvPresupuesto.Columns.Count)
                if (dgvPresupuesto.Columns[colIndex].Tag is ColumnaPersonalizada cp)
                    _columnaRibbon = cp;
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }


        public FormPresupuesto(SOPROContext context, Proyecto proyecto)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            var workspaceService = new ProjectWorkspaceService();
            _projectIndexService = new ProjectIndexService(workspaceService);
            _projectUsageService = new ProjectUsageService(workspaceService);
            _catalogSearchService = new CatalogSearchService(_projectIndexService, _projectUsageService, new ProjectDbContextFactory());
            _projectIndexService.RefreshKnownProjects(context.DatabasePath, force: true);
            // Recargar proyecto fresco desde BD para tener los % actualizados
            _proyecto = context.Proyectos.Find(proyecto.Id) ?? proyecto;

            // Establecer proyecto para formateo global
            FormatoHelper.EstablecerProyecto(_proyecto);

            // Suscribirse a cambios de configuración
            FormatoHelper.ConfiguracionCambiada += OnConfiguracionCambiada;

            InitializeComponent();
            InicializarBotonReajustarCosto();
            LoadWorkspacePanelState();
            splitContainer.SplitterMoved += (_, __) =>
            {
                SaveWorkspacePanelState();
                if (!splitContainer.Panel2Collapsed)
                    ProgramarAsegurarFilaActualVisibleEnPresupuesto();
            };
            InicializarAutocompleteApu();
            InicializarPreviewApu();

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.Presupuesto).Attach();

            // Aplicar estilo consistente al grid
            dgvPresupuesto.AplicarEstiloSOPRO();

            // Selección de FILA completa con color suave + borde azul en celda activa + Ctrl+C por celda
            dgvPresupuesto.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvPresupuesto);

            // Agregar columna ID oculta
            dgvPresupuesto.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                HeaderText = "ID",
                Visible = false,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // Columna de número de concepto (solo para conceptos, no agrupadores)
            dgvPresupuesto.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNumero",
                HeaderText = "#",
                Width = 40,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 8F)
                }
            });

            ActualizarTitulo();
            ConfigurarEventos();
            InicializarFormMatricesEmbebido();

            // Asegura que Presupuesto abra con el mismo set de columnas
            // que genera el boton "Predeterminadas" del FormColumnasPersonalizadas.
            ColumnasPresupuestoHelper.CrearColumnasPredeterminadas(_context, _proyecto.Id);

            CargarPresupuesto();
            RefrescarFuenteAutocompleteApu();
            dgvPresupuesto.InterceptarTeclaEspecial = ProcesarTeclaEspecialPresupuesto;
            this.Shown += FormPresupuesto_Shown;

            // Suscribirse a eventos de actualización de porcentajes
            FormPorcentajes.PorcentajesActualizados += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
            FormIndirectos.IndirectosTransferidos += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
            FormFinanciamiento.FinanciamientoTransferido += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
            FormUtilidad.UtilidadTransferida += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
            FormDatosProyecto.DecimalesActualizados += (s, e) =>
            {
                // Recargar proyecto y refrescar formateo
                var proyFresco = _context.Proyectos.Find(_proyecto.Id);
                if (proyFresco != null)
                {
                    _proyecto.DecimalesCantidad = proyFresco.DecimalesCantidad;
                    _proyecto.DecimalesImporte = proyFresco.DecimalesImporte;
                    _proyecto.DecimalesPorcentaje = proyFresco.DecimalesPorcentaje;
                    FormatoHelper.EstablecerProyecto(_proyecto);
                    RecalcularPreciosUnitarios(); // Refrescar con nuevos decimales
                }
            };

            // Refrescar cuando se elimina/modifica un insumo desde cualquier catálogo
            _onInsumosModificados = (s, e) =>
            {
                RefrescarPreciosDesdeDB();
                RefrescarFuenteAutocompleteApu();
            };
            FormCatalogoMateriales.InsumosModificados += _onInsumosModificados;
            FormCatalogoManoObra.InsumosModificados += _onInsumosModificados;
            FormCatalogoHerramientas.InsumosModificados += _onInsumosModificados;
            FormCatalogoMaquinaria.InsumosModificados += _onInsumosModificados;
            FormEditarMatriz.MatrizGuardada += _onInsumosModificados;

            // También refrescar cuando el form gana foco (por si vienen de otra ventana)
            this.Activated += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
        }


        private void FormPresupuesto_Shown(object? sender, EventArgs e)
        {
            this.Shown -= FormPresupuesto_Shown;
            MostrarAdvertenciaValidacionEnApertura();
        }


        private void AsignarMatrizAPresupuesto(
            int rowIndex,
            Matriz matriz,
            decimal cantidad,
            bool preserveCurrentTexts,
            bool forceMatrixDescription = false,
            bool preserveKeyIfPresent = false,
            bool preserveUnitIfPresent = false,
            bool preserveDescriptionIfPresent = false)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count) return;

            var row = dgvPresupuesto.Rows[rowIndex];
            string? claveActual = (preserveCurrentTexts || preserveKeyIfPresent)
                ? ObtenerCeldaPorNombreInterno(rowIndex, "Clave")?.Value?.ToString()
                : null;
            string? descripcionActual = ((preserveCurrentTexts || preserveDescriptionIfPresent) && !forceMatrixDescription)
                ? ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion")?.Value?.ToString()
                : null;

            // Si la descripción actual parece ser solo el texto usado para buscar (ej. "TR", "TRAZO"),
            // permitimos que se tome la descripción completa de la matriz. Si ya parece una descripción
            // elaborada del concepto, se conserva lo capturado por el usuario.
            if (!forceMatrixDescription && DebeUsarDescripcionDeMatriz(descripcionActual, matriz.Descripcion))
                descripcionActual = null;

            string? unidadActual = (preserveCurrentTexts || preserveUnitIfPresent)
                ? ObtenerCeldaPorNombreInterno(rowIndex, "Unidad")?.Value?.ToString()
                : null;

            var draft = BudgetConceptAssignmentService.BuildDraftFromSelectedMatrix(
                _proyecto,
                matriz,
                cantidad,
                claveActual,
                descripcionActual,
                unidadActual);

            ApplyAssignmentDraftToGrid(rowIndex, draft);

            var concepto = BudgetConceptAssignmentService.ApplyDraft(
                _context,
                _proyecto.Id,
                CountConceptRowsBefore(rowIndex),
                row.Tag as ConceptoPresupuesto,
                draft);

            row.Tag = concepto;
            FinalizeBudgetConceptAssignment(rowIndex);
            GuardarCambios();
        }

        private void InicializarFormMatricesEmbebido()
        {
            _panelMatricesEmbebido = new Controls.PanelMatricesEmbebido(_context, _proyecto.Id);
            panelMatricesHost.Controls.Add(_panelMatricesEmbebido);

            // Conectar el panel al grid para que reaccione a la selección
            _panelMatricesEmbebido.ConectarPresupuesto(dgvPresupuesto);

            // Cuando se edita la matriz desde el panel, recalcular la jerarquía
            _panelMatricesEmbebido.MatrizActualizada += (sender, filaActualizada) =>
            {
                int indicePadre = ObtenerIndicePadre(filaActualizada);
                while (indicePadre >= 0)
                {
                    ActualizarTotalAgrupador(indicePadre);
                    indicePadre = ObtenerIndicePadre(indicePadre);
                }
                ReasignarNumerosConceptos();
                dgvPresupuesto.Refresh();
            };
            _panelMatricesEmbebido.SolicitudCerrarWorkspace += (sender, e) =>
            {
                if (!splitContainer.Panel2Collapsed)
                    btnWorkspaceCerrar_Click(btnWorkspaceCerrar, EventArgs.Empty);
            };

            ActualizarEstadoWorkspace(false);
        }

        /// <summary>
        /// Recarga los precios de TODOS los conceptos del presupuesto desde BD.
        /// Llamar después de cerrar el APU Editor (FormMatrices → FormEditarMatriz).
        /// </summary>
        public void RefrescarPreciosDesdeDB()
        {
            int filaActual = -1;
            int colActual = -1;
            try
            {
                if (dgvPresupuesto.CurrentCell != null)
                {
                    filaActual = dgvPresupuesto.CurrentCell.RowIndex;
                    colActual = dgvPresupuesto.CurrentCell.ColumnIndex;
                }

                for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
                {
                    var concepto = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                    if (concepto == null || concepto.EsAgrupador || !concepto.MatrizId.HasValue) continue;

                    // Recargar la matriz fresca desde BD
                    var matriz = _context.Matrices
                        .AsNoTracking()
                        .FirstOrDefault(m => m.Id == concepto.MatrizId.Value);
                    if (matriz == null) continue;

                    var _motorSync = new MotorCalculoSopro(_proyecto);
                    decimal cdUnit = _motorSync.RedondearImporte(matriz.CostoDirecto);
                    decimal nuevoPU = CalcularPU(cdUnit);
                    decimal nuevoImp = _motorSync.Multiplicar(concepto.Cantidad, nuevoPU);
                    concepto.CostoDirectoUnitario = cdUnit;
                    concepto.CostoDirectoTotal = _motorSync.Multiplicar(concepto.Cantidad, cdUnit);
                    concepto.PrecioUnitario = nuevoPU;   // persistir
                    concepto.ImporteTotal = nuevoImp;  // persistir

                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            if (colDef.NombreInterno == "PrecioUnitario")
                                dgvPresupuesto.Rows[i].Cells[col.Index].Value = nuevoPU.ToStringImporte();
                            else if (colDef.NombreInterno == "Importe" || colDef.NombreInterno == "ImporteTotal")
                                dgvPresupuesto.Rows[i].Cells[col.Index].Value = nuevoImp.ToStringImporte();
                        }
                    }
                }
                _context.SaveChanges(); // Guardar PU e ImporteTotal calculados
                RecalcularTodosLosTotales();
                GuardarCambios();       // Persistir totales de agrupadores en BD
                dgvPresupuesto.Refresh();

                if (filaActual >= 0 && filaActual < dgvPresupuesto.Rows.Count)
                {
                    try
                    {
                        int colRestaurar = (colActual >= 0 && colActual < dgvPresupuesto.Columns.Count) ? colActual : 0;
                        dgvPresupuesto.CurrentCell = dgvPresupuesto.Rows[filaActual].Cells[colRestaurar];
                        dgvPresupuesto.ClearSelection();
                        dgvPresupuesto.Rows[filaActual].Selected = true;
                    }
                    catch { }
                }

                // Refrescar también el panel embebido con la fila actual restaurada
                if (filaActual >= 0 && filaActual < dgvPresupuesto.Rows.Count)
                    _panelMatricesEmbebido?.NotificarFilaCambiada(filaActual);
                else if (dgvPresupuesto.CurrentRow != null)
                    _panelMatricesEmbebido?.NotificarFilaCambiada(dgvPresupuesto.CurrentRow.Index);

                // Actualizar el header con los % del proyecto
                ActualizarInfoPorcentajes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefrescarPrecios: {ex.Message}");
            }
        }

        /// <summary>
        /// Maneja el evento de cambio de configuración de decimales
        /// </summary>
        private void OnConfiguracionCambiada(object sender, EventArgs e)
        {
            if (!this.Visible) return;
            // Recargar proyecto desde BD para tener los decimales actualizados
            _proyecto = _context.Proyectos.Find(_proyecto.Id);

            // Actualizar formato de la columna Cantidad con los nuevos decimales
            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef &&
                    string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
                {
                    col.DefaultCellStyle.Format = $"N{_proyecto.DecimalesCantidad}";
                }
            }

            var motorCfg = new MotorCalculoSopro(_proyecto);

            // Reformatear TODAS las celdas del grid con los nuevos decimales
            foreach (DataGridViewRow row in dgvPresupuesto.Rows)
            {
                var concepto = row.Tag as ConceptoPresupuesto;
                if (concepto == null) continue;

                // Recalcular valores con los nuevos decimales
                if (!concepto.EsAgrupador && concepto.MatrizId.HasValue)
                {
                    decimal pu = CalcularPU(concepto.CostoDirectoUnitario);
                    decimal importe = motorCfg.Multiplicar(concepto.Cantidad, pu);

                    // Actualizar cada celda con el nuevo formato
                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            switch (colDef.NombreInterno)
                            {
                                case "Cantidad":
                                    // Re-formatear con los nuevos decimales de cantidad
                                    row.Cells[col.Index].Value = motorCfg.RedondearCantidad(concepto.Cantidad);
                                    break;
                                case "PrecioUnitario":
                                    row.Cells[col.Index].Value = pu.ToStringImporte();
                                    break;
                                case "Importe":
                                    row.Cells[col.Index].Value = importe.ToStringImporte();
                                    break;
                            }
                        }
                    }
                }
            }

            // Recalcular totales con nuevos decimales
            RecalcularTodosLosTotales();
            GuardarCambios();       // Persistir totales de agrupadores en BD
            dgvPresupuesto.Refresh();
        }

        private void ActualizarTitulo()
        {
            var info = BudgetPricingService.BuildHeaderInfo(_proyecto);
            lblProyecto.Text = info.ProyectoNombre;
            lblUbicacion.Text = info.UbicacionTexto;
            ActualizarInfoPorcentajes();
        }

        /// <summary>
        /// Muestra en el header los porcentajes activos del proyecto.
        /// </summary>
        private void ActualizarInfoPorcentajes()
        {
            BudgetPricingService.RefreshProjectPercentages(_context, _proyecto);
            var info = BudgetPricingService.BuildHeaderInfo(_proyecto);
            lblPorcentajesInfo.Text = info.PorcentajesTexto;
        }

        /// <summary>
        /// Calcula el factor multiplicador total para convertir CD → P.U.
        /// </summary>
        private decimal CalcularFactorPU()
        {
            return BudgetPricingService.CalculateFactor(_proyecto);
        }

        /// <summary>
        /// Convierte un Costo Directo en Precio Unitario aplicando la cadena de porcentajes.
        /// </summary>
        private decimal CalcularPU(decimal costoDirecto)
        {
            return BudgetPricingService.CalculateUnitPrice(_proyecto, costoDirecto);
        }

        /// <summary>
        /// Recalcula las columnas P.U. e Importe del grid usando los porcentajes actuales del proyecto.
        /// Llamar después de cambiar porcentajes en FormPorcentajes/FormIndirectos.
        /// </summary>
        private void RecalcularPreciosUnitarios()
        {
            try
            {
                var _motorRecalc = new MotorCalculoSopro(_proyecto);
                bool huboCambios = false;

                for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
                {
                    var concepto = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                    if (concepto == null || concepto.EsAgrupador) continue;

                    decimal pu = CalcularPU(concepto.CostoDirectoUnitario);
                    decimal imp = _motorRecalc.Multiplicar(concepto.Cantidad, pu);

                    // Actualizar grid
                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            if (colDef.NombreInterno == "PrecioUnitario")
                                dgvPresupuesto.Rows[i].Cells[col.Index].Value = pu.ToStringImporte();
                            else if (colDef.NombreInterno == "Importe" || colDef.NombreInterno == "ImporteTotal")
                                dgvPresupuesto.Rows[i].Cells[col.Index].Value = imp.ToStringImporte();
                        }
                    }

                    // Persistir en entidad y BD si cambió
                    if (concepto.PrecioUnitario != pu || concepto.ImporteTotal != imp)
                    {
                        concepto.PrecioUnitario = pu;
                        concepto.ImporteTotal = imp;
                        huboCambios = true;
                    }
                }

                if (huboCambios)
                    _context.SaveChanges();

                RecalcularTodosLosTotales(); // actualiza entidades agrupadoras en memoria
                GuardarCambios();            // persiste los totales de agrupadores en BD
                dgvPresupuesto.Refresh();
            }
            catch { /* silenciar errores en caso de que el grid no esté listo */ }
        }

        private void ConfigurarEventos()
        {
            dgvPresupuesto.CellValueChanged += DgvPresupuesto_CellValueChanged;
            dgvPresupuesto.CellFormatting += DgvPresupuesto_CellFormatting;
            dgvPresupuesto.CurrentCellDirtyStateChanged += DgvPresupuesto_CurrentCellDirtyStateChanged;
            dgvPresupuesto.Scroll += (s, e) =>
            {
                if (_lstApuAutocomplete is { Visible: true })
                    PosicionarAutocompleteApu();
                else
                    OcultarAutocompleteApu(false);
            };
            dgvPresupuesto.CellDoubleClick += DgvPresupuesto_CellDoubleClick;
            dgvPresupuesto.KeyDown += DgvPresupuesto_KeyDown;
            dgvPresupuesto.KeyPress += DgvPresupuesto_KeyPress; // Comportamiento Excel
            dgvPresupuesto.CellMouseDown += DgvPresupuesto_CellMouseDown;
            dgvPresupuesto.CellBeginEdit += DgvPresupuesto_CellBeginEdit;
            dgvPresupuesto.CellValidating += DgvPresupuesto_CellValidating;
            dgvPresupuesto.CellEndEdit += DgvPresupuesto_CellEndEdit;
            dgvPresupuesto.EditingControlShowing += DgvPresupuesto_EditingControlShowing;
            dgvPresupuesto.ColumnWidthChanged += DgvPresupuesto_ColumnWidthChanged;
            dgvPresupuesto.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);

            dgvPresupuesto.DataError += DgvPresupuesto_DataError;

            // ── Drag & Drop para reordenar filas ──────────────────────
            dgvPresupuesto.AllowDrop = true;
            dgvPresupuesto.MouseDown += DgvPresupuesto_MouseDown;
            dgvPresupuesto.MouseUp += DgvPresupuesto_MouseUp;
            dgvPresupuesto.MouseMove += DgvPresupuesto_MouseMove;
            dgvPresupuesto.DragOver += DgvPresupuesto_DragOver;
            dgvPresupuesto.DragDrop += DgvPresupuesto_DragDrop;
            dgvPresupuesto.DragLeave += DgvPresupuesto_DragLeave;
            dgvPresupuesto.Paint += DgvPresupuesto_Paint;

            Resize += (s, e) =>
            {
                if (_lstApuAutocomplete is { Visible: true })
                    PosicionarAutocompleteApu();
            };
        }

        private int _dragFilaOrigen = -1;
        private int _dragLineaInsercion = -1;
        private int _dragFilaMouseDown = -1;
        private Point _dragMouseDownLocation = Point.Empty;
        private Rectangle _dragStartRect = Rectangle.Empty;
        private const int DragThresholdPixels = 6;
        private DataGridViewCell _celdaAnterior = null;
        private ContextMenuStrip? _menuPresupuesto;
        private string _valorAnteriorClaveEnEdicion = string.Empty;
        private int _filaClaveEnEdicion = -1;
        private int _columnaClaveEnEdicion = -1;
        private bool _ultimoIntentoClaveInvalida;






        /// <summary>
        /// Asigna números consecutivos solo a los conceptos (no agrupadores)
        /// según el orden en que aparecen en el grid.
        /// También guarda el Orden en BD para que la navegación ▲▼ del panel sea correcta.
        /// </summary>
        public void ReasignarNumerosConceptos()
        {
            if (!dgvPresupuesto.Columns.Contains("colNumero")) return;

            var snapshot = BuildHierarchyRowsSnapshot();
            var sequenceMap = BudgetHierarchyService.BuildConceptSequenceMap(snapshot);

            foreach (var row in snapshot)
            {
                dgvPresupuesto.Rows[row.RowIndex].Cells["colNumero"].Value = sequenceMap[row.RowIndex]?.ToString() ?? string.Empty;
            }
        }



        private int ObtenerNivelDesdeTipo(string tipo)
        {
            return BudgetHierarchyService.GetLevelFromType(tipo);
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnExplosion_Click(object sender, EventArgs e)
        {
            using var formExplosion = new FormExplosionInsumos(_context, _proyecto.Id);
            formExplosion.ShowDialog(this);
        }

        /// <summary>
        /// Calcula el valor de una columna calculada basándose en su fórmula
        /// </summary>
        private object CalcularColumnaCalculada(ColumnaPersonalizada columna, int rowIndex)
        {
            if (string.IsNullOrWhiteSpace(columna.Formula)) return null;

            try
            {
                string formula = columna.Formula;

                // Reemplazar nombres de columnas por sus valores
                var regex = new System.Text.RegularExpressions.Regex(@"\b[A-Za-z_][A-Za-z0-9_]*\b");
                var matches = regex.Matches(formula);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    string nombreColumna = match.Value;

                    // Obtener valor de la columna referenciada
                    var cell = ObtenerCeldaPorNombreInterno(rowIndex, nombreColumna);
                    if (cell?.Value != null)
                    {
                        string valorStr = cell.Value.ToString()
                            .Replace("$", "")
                            .Replace(",", "")
                            .Replace("%", "")
                            .Trim();

                        if (decimal.TryParse(valorStr, out decimal valor))
                        {
                            formula = formula.Replace(nombreColumna, valor.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                    else
                    {
                        formula = formula.Replace(nombreColumna, "0");
                    }
                }

                // Evaluar expresión matemática
                var dataTable = new System.Data.DataTable();
                var resultado = dataTable.Compute(formula, "");

                if (resultado != null && decimal.TryParse(resultado.ToString(), out decimal resultadoDecimal))
                {
                    return resultadoDecimal;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Guardar orden visual de columnas antes de cerrar
            try
            {
                foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                {
                    if (col.Tag is ColumnaPersonalizada colDef)
                    {
                        var columnaDB = _context.ColumnasPersonalizadas.Find(colDef.Id);
                        if (columnaDB != null)
                        {
                            columnaDB.Orden = col.DisplayIndex;
                        }
                    }
                }
                _context.SaveChanges();
            }
            catch
            {
                // Silencioso - no bloquear el cierre
            }

            SaveWorkspacePanelState();
            base.OnFormClosing(e);
        }

        private void btnToggleMatrices_Click(object sender, EventArgs e)
        {
            if (!splitContainer.Panel2Collapsed)
                SaveWorkspacePanelState();

            splitContainer.Panel2Collapsed = !splitContainer.Panel2Collapsed;

            if (splitContainer.Panel2Collapsed)
            {
                btnToggleMatrices.Text = "📐 Matrices ▼";
            }
            else
            {
                btnToggleMatrices.Text = "📐 Matrices ▲";
                var targetHeight = _workspacePanelHeight > 0 ? _workspacePanelHeight : Math.Max(splitContainer.Panel2MinSize, (int)(splitContainer.Height * 0.34));
                splitContainer.SplitterDistance = Math.Max(splitContainer.Panel1MinSize, splitContainer.Height - targetHeight);
                ProgramarAsegurarFilaActualVisibleEnPresupuesto();
            }
        }

        private void btnExportar_Click(object sender, EventArgs e)
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            using var opciones = new FormExportarReporte(_context, _proyecto);
            opciones.ShowDialog();
        }


        public SOPROContext Contexto => _context;
        public Proyecto ProyectoActual => _proyecto;


        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Desuscribir eventos ESTÁTICOS para evitar "disposed context" si el form
            // se cierra mientras otros módulos siguen activos
            FormCatalogoMateriales.InsumosModificados -= _onInsumosModificados;
            FormCatalogoManoObra.InsumosModificados -= _onInsumosModificados;
            FormCatalogoHerramientas.InsumosModificados -= _onInsumosModificados;
            FormCatalogoMaquinaria.InsumosModificados -= _onInsumosModificados;
            FormEditarMatriz.MatrizGuardada -= _onInsumosModificados;
            base.OnFormClosed(e);
        }

        public void RecalcularTodo()
        {
            // Recargar proyecto para tener decimales frescos
            var proyFresco = _context.Proyectos.Find(_proyecto.Id);
            if (proyFresco != null)
            {
                _proyecto.DecimalesCantidad  = proyFresco.DecimalesCantidad;
                _proyecto.DecimalesImporte   = proyFresco.DecimalesImporte;
                _proyecto.DecimalesPorcentaje = proyFresco.DecimalesPorcentaje;
                FormatoHelper.EstablecerProyecto(_proyecto);
            }

            // Actualizar formato de la columna Cantidad con los decimales actuales
            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef &&
                    string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
                {
                    col.DefaultCellStyle.Format = $"N{_proyecto.DecimalesCantidad}";
                }
            }

            // Re-formatear celdas de Cantidad con el nuevo número de decimales
            var motorRecalc = new MotorCalculoSopro(_proyecto);
            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var concepto = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                if (concepto == null || concepto.EsAgrupador) continue;

                foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                {
                    if (col.Tag is ColumnaPersonalizada colDef &&
                        string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
                    {
                        // Re-formatear con los nuevos decimales
                        dgvPresupuesto.Rows[i].Cells[col.Index].Value =
                            motorRecalc.RedondearCantidad(concepto.Cantidad);
                    }
                }
            }

            RefrescarPreciosDesdeDB();
            ValidarPresupuestoAntesDeContinuar(bloquear: false, titulo: "Validación de presupuesto");
        }
    }
}
