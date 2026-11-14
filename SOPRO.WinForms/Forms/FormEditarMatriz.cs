using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
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
        private readonly List<ComponenteMatriz> _componentesTemp = new List<ComponenteMatriz>();
        private Color _grpDatosBackColorOriginal;
        private readonly Color _colorApu = Color.FromArgb(245, 248, 252);
        private readonly Color _colorBasico = Color.FromArgb(255, 248, 220);
        private readonly Color _colorCuadrilla = Color.FromArgb(230, 242, 255);
        
        private decimal _totalMaterial = 0;
        private decimal _baseManoObra = 0;
        private decimal _totalManoObra = 0;
        private decimal _totalManoObraResumen = 0;
        private decimal _totalMaquinaria = 0;
        private decimal _totalBasicos = 0;
        private decimal _costoDirectoTotal = 0;
        
        public FormEditarMatriz(SOPROContext context, int proyectoId, Matriz matriz = null, TipoMatriz? tipoInicial = null, IProjectDbContextFactory? dbContextFactory = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbContextFactory = dbContextFactory ?? new ProjectDbContextFactory();
            _proyectoId = proyectoId;
            _matriz = matriz;
            _esNuevo = (matriz == null);
            _tipoInicialForzado = _esNuevo ? tipoInicial : null;
            
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
            
            // Unidad - EDITABLE
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUnidad", HeaderText = "Unidad", Width = 70, ReadOnly = false
            });
            
            // Cantidad - EDITABLE directo para Material; doble click abre Rendimiento para M.O./Maq.
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCantidad", HeaderText = "Cantidad", Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N6" },
                ReadOnly = false
            });
            
            // P.U. - EDITABLE para Material; solo lectura para M.O./Maq./Básico (viene del catálogo)
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPU", HeaderText = "P.U.", Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                ReadOnly = false
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
            var componentes = _context.ComponentesMatriz
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
            var selectedComponents = ComponentSelectorDialogService.SelectComponents(this, _context, _proyectoId, tipoComponente);
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

        private int DecimalesImporte => _context.Proyectos.Find(_proyectoId)?.DecimalesImporte ?? 2;

        internal DataGridView DgvComponentesParaPrueba => dgvComponentes;

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

            DataGridViewStateHelper.Restore(dgvComponentes, estadoActual);
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
                    _context);

                // La edición mutó en sitio los ComponenteMatriz rastreados por la
                // sesión (el editor trabaja sobre las instancias cargadas con
                // seguimiento). El commit por operación los reemplazó en la base:
                // reconciliar los rastreados propios antes de notificar, para que un
                // SaveChanges ajeno posterior no intente actualizar filas inexistentes
                // ni pise los valores recién confirmados.
                ReconciliarRastreadosPropios(dto);

                var mensajeExito = MatrixSaveFlowService.GetSuccessMessage(result.IsNew);
                if (_context.ChangeTracker.HasChanges())
                {
                    mensajeExito += "\n\nAtención: hay cambios sin guardar en el proyecto; el catálogo de matrices no se recargará hasta confirmarlos o descartarlos.";
                }

                MessageBox.Show(mensajeExito, "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
        /// La matriz editada se resincroniza con los valores confirmados y queda
        /// Unchanged con OriginalValues alineados: un SaveChanges ajeno posterior
        /// la reescribe como no-op en lugar de pisar el commit. Si la entrada de la
        /// matriz ya estaba Modified/Deleted (trabajo ajeno que la guarda de
        /// guardado habría bloqueado antes del commit), NO se toca: la
        /// resincronización descartaría silenciosamente ese trabajo.
        ///
        /// El commit eliminó y reemplazó el lote COMPLETO de componentes de la
        /// matriz editada: eso incluye los componentes que el usuario quitó de la
        /// lista del editor y que la sesión sigue rastreando (quitarlos de la
        /// lista no desconecta la entrada). Se desconectan TODAS las entradas
        /// rastreadas de la matriz objetivo, estén en la lista o no, para que
        /// ningún SaveChanges ajeno intente actualizar filas inexistentes
        /// (DbUpdateConcurrencyException). Las entradas de OTRAS matrices y las
        /// ediciones ajenas no se tocan; los componentes nuevos o solo de lista no
        /// rastreados no se adjuntan (Entry los ve Detached y no hay mutación).
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

        /// <summary>Acceso del ensamblado de pruebas a los componentes de trabajo del editor.</summary>
        internal IReadOnlyList<ComponenteMatriz> ComponentesTempParaPrueba => _componentesTemp;

        private MatrixEditDto BuildMatrixEditDto()
        {
            return MatrixEditorService.BuildEditDto(
                _proyectoId,
                txtClave.Text,
                txtDescripcion.Text,
                cboUnidad.Text,
                rbCuadrilla.Checked,
                rbAPU.Checked,
                _costoDirectoTotal,
                _componentesTemp);
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
