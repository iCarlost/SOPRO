using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Controls
{
    public partial class PanelMatricesEmbebido : UserControl
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private DataGridView _dgvPresupuesto;
        private int _filaActual = -1;
        private int _ultimaFilaCargada = -1;
        private int? _ultimoConceptoId = null;
        private int? _ultimaMatrizId = null;
        private Matriz _matrizActual = null;
        private readonly System.Collections.Generic.List<ComponenteMatriz> _componentesTemp = new System.Collections.Generic.List<ComponenteMatriz>();

        public event EventHandler<int> MatrizActualizada;
        public event EventHandler SolicitudCerrarWorkspace;

        private Panel        _panelHeader;
        private Label        _lblTituloMatriz;
        private Label        _lblInfoMatriz;
        private Button       _btnAnterior;
        private Button       _btnSiguiente;
        private Panel        _panelBotonesAgregar;
        private Button       _btnAgregarMaterial;
        private Button       _btnAgregarMO;
        private Button       _btnAgregarMaquinaria;
        private Button       _btnAgregarHerramienta;
        private Button       _btnAgregarBasico;
        private DataGridView _dgvComponentes;
        private Panel        _panelTotales;
        private Label        _lblMat, _lblMO, _lblMaq, _lblBas, _lblDir;
        private Panel        _panelDatos;
        private TextBox      _txtClaveMatriz;
        private TextBox      _txtDescripcionMatriz;
        private TextBox      _txtUnidadMatriz;
        private RadioButton  _rbTipoApu;
        private RadioButton  _rbTipoBasico;
        private RadioButton  _rbTipoCuadrilla;
        private Button       _btnGuardarMatriz;
        private Button       _btnCancelarMatriz;
        private bool         _modoEdicionCabecera;
        private bool         _modoCreacionMatriz;
        private int?         _matrizEditandoId;
        private Action<Matriz>? _onMatrixHeaderSaved;
        private Action?         _onMatrixHeaderCancelled;
        private readonly Color  _headerColorNormal = Color.FromArgb(40, 40, 65);
        private readonly Color  _headerColorSubedicion = Color.FromArgb(86, 43, 129);
        private ContextMenuStrip _menuContextualComponentes;
        private ToolStripMenuItem _mnuEditarMatrizComponente;
        private int _filaContextualComponentes = -1;
        private bool _modoSubedicionComponente;
        private int? _matrizPadreIdSubedicion;
        private int? _filaPadreSubedicion;
        private readonly UndoManager _undoManager = new();
        private bool _isUndoRedo;
        private int _undoPanelRowIndex = -1;
        private int _undoPanelColumnIndex = -1;
        private string _undoPanelColumnName = string.Empty;
        private string _undoPanelOldValue = string.Empty;
        private string _cabeceraBaseClave = string.Empty;
        private string _cabeceraBaseDescripcion = string.Empty;
        private string _cabeceraBaseUnidad = string.Empty;
        private TipoMatriz _cabeceraBaseTipo = TipoMatriz.APU;

        public PanelMatricesEmbebido(SOPROContext context, int proyectoId)
        {
            _context    = context;
            _proyectoId = proyectoId;
            
            // Suscribirse a cambios de configuración de decimales
            FormatoHelper.ConfiguracionCambiada += OnConfiguracionCambiada;
            
            InitializeComponent();
            ConfigurarMenuContextualComponentes();
            MostrarSinSeleccion();
        }

        public bool TieneFocoEnEntradaTexto()
        {
            return (_txtClaveMatriz?.Focused ?? false)
                || (_txtDescripcionMatriz?.Focused ?? false)
                || (_txtUnidadMatriz?.Focused ?? false)
                || ((_dgvComponentes?.IsCurrentCellInEditMode ?? false) && _dgvComponentes.EditingControl is TextBox);
        }


private bool UsaComponentesTemporales() => _modoCreacionMatriz;

private ComponenteMatriz ObtenerComponentePorFilaTrabajo(int rowIndex)
{
    if (UsaComponentesTemporales())
    {
        MatrixComponentCollectionService.TryGetAt(_componentesTemp, rowIndex, out var tempComp);
        return tempComp;
    }

    return ObtenerComponentePorFila(rowIndex);
}

private MatrixEditDto BuildCurrentMatrixEditDto()
{
    return MatrixEditorService.BuildEditDto(
        _proyectoId,
        _txtClaveMatriz.Text,
        _txtDescripcionMatriz.Text,
        _txtUnidadMatriz.Text,
        _rbTipoCuadrilla.Checked,
        _rbTipoApu.Checked,
        _lblDir != null ? 0m : 0m,
        _componentesTemp);
}

private void PintarComponentesEnGrid(System.Collections.Generic.IEnumerable<ComponenteMatriz> componentes)
{
    int? currentRow = null;
    int? currentCol = null;
    int firstDisplayed = -1;
    try
    {
        if (_dgvComponentes.CurrentCell != null)
        {
            currentRow = _dgvComponentes.CurrentCell.RowIndex;
            currentCol = _dgvComponentes.CurrentCell.ColumnIndex;
        }
        if (_dgvComponentes.Rows.Count > 0)
            firstDisplayed = _dgvComponentes.FirstDisplayedScrollingRowIndex;
    }
    catch { }

    var lista = componentes?.ToList() ?? new System.Collections.Generic.List<ComponenteMatriz>();
    var totales = MatrixComponentCalculationService.Recalculate(lista, FormatoHelper.DecimalesImporte);
    var rows = MatrixComponentPresentationService.BuildRows(lista, totales.BaseManoObra);

    _dgvComponentes.Rows.Clear();
    for (int i = 0; i < lista.Count && i < rows.Count; i++)
    {
        var comp = lista[i];
        var row = rows[i];
        _dgvComponentes.Rows.Add(
            row.Tipo,
            row.Clave,
            row.Descripcion,
            row.Unidad,
            row.Cantidad.ToStringCantidad(),
            row.PrecioUnitario.ToStringImporte(),
            row.Importe.ToStringImporte(),
            comp.Id,
            "🗑");
    }

    _lblMat.Text = $"Mat: {totales.TotalMaterial.ToStringImporte()}";
    _lblMO.Text = $"M.O.: {totales.TotalManoObraResumen.ToStringImporte()}";
    _lblMaq.Text = $"Maq.: {totales.TotalMaquinaria.ToStringImporte()}";
    _lblBas.Text = $"Bas.: {totales.TotalBasicos.ToStringImporte()}";
    _lblDir.Text = $"Costo Directo: {totales.CostoDirectoTotal.ToStringImporte()}";

    try
    {
        if (_dgvComponentes.Rows.Count > 0)
        {
            if (firstDisplayed >= 0 && firstDisplayed < _dgvComponentes.Rows.Count)
                _dgvComponentes.FirstDisplayedScrollingRowIndex = firstDisplayed;

            if (currentRow.HasValue && currentCol.HasValue &&
                currentRow.Value >= 0 && currentRow.Value < _dgvComponentes.Rows.Count &&
                currentCol.Value >= 0 && currentCol.Value < _dgvComponentes.Columns.Count)
            {
                _dgvComponentes.CurrentCell = _dgvComponentes.Rows[currentRow.Value].Cells[currentCol.Value];
            }
        }
    }
    catch { }
}

private void RefrescarVistaTemporal()
{
    var totals = MatrixComponentCalculationService.Recalculate(_componentesTemp, FormatoHelper.DecimalesImporte);
    PintarComponentesEnGrid(_componentesTemp);
    _lblDir.Text = $"Costo Directo: {totals.CostoDirectoTotal.ToStringImporte()}";
}

private void AgregarComponentesTemporales(TipoComponenteMatriz tipoComponente)
{
    using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, tipoComponente);
    if (dlg.ShowDialog() != DialogResult.OK) return;

    var selectionResult = MatrixComponentSelectionFlowService.AddSelectedComponents(_componentesTemp, dlg.ComponentesSeleccionados);
    if (!selectionResult.HasComponents) return;

    foreach (var componente in _componentesTemp)
    {
        componente.Notas ??= string.Empty;
    }

    RefrescarVistaTemporal();
}

        public void ConectarPresupuesto(DataGridView dgv)
        {
            _dgvPresupuesto = dgv;
            dgv.CurrentCellChanged += (s, e) =>
            {
                if (dgv.CurrentRow != null)
                    CargarMatrizDeFila(dgv.CurrentRow.Index, false);
                else
                    MostrarSinSeleccion();
            };
        }

        private void CapturarEstadoBaseCabeceraDesdeUI()
        {
            _cabeceraBaseClave = (_txtClaveMatriz.Text ?? string.Empty).Trim();
            _cabeceraBaseDescripcion = (_txtDescripcionMatriz.Text ?? string.Empty).Trim();
            _cabeceraBaseUnidad = (_txtUnidadMatriz.Text ?? string.Empty).Trim();
            _cabeceraBaseTipo = GetTipoCabeceraSeleccionado();
        }

        private void CapturarEstadoBaseCabeceraDesdeMatriz(Matriz matriz)
        {
            if (matriz == null)
                return;

            _cabeceraBaseClave = (matriz.Clave ?? string.Empty).Trim();
            _cabeceraBaseDescripcion = (matriz.Descripcion ?? string.Empty).Trim();
            _cabeceraBaseUnidad = (matriz.Unidad ?? string.Empty).Trim();
            _cabeceraBaseTipo = matriz.Tipo;
        }

        private bool TieneCambiosPendientesCabecera()
        {
            if (!_modoEdicionCabecera || _panelDatos == null || !_panelDatos.Visible)
                return false;

            if (_modoCreacionMatriz)
            {
                return !string.IsNullOrWhiteSpace(_txtClaveMatriz.Text)
                    || !string.IsNullOrWhiteSpace(_txtDescripcionMatriz.Text)
                    || !string.IsNullOrWhiteSpace(_txtUnidadMatriz.Text)
                    || _componentesTemp.Count > 0;
            }

            if (!_matrizEditandoId.HasValue)
                return false;

            return !string.Equals((_txtClaveMatriz.Text ?? string.Empty).Trim(), _cabeceraBaseClave, StringComparison.Ordinal)
                || !string.Equals((_txtDescripcionMatriz.Text ?? string.Empty).Trim(), _cabeceraBaseDescripcion, StringComparison.Ordinal)
                || !string.Equals((_txtUnidadMatriz.Text ?? string.Empty).Trim(), _cabeceraBaseUnidad, StringComparison.Ordinal)
                || GetTipoCabeceraSeleccionado() != _cabeceraBaseTipo;
        }

        private bool GuardarCabeceraEdicionActual()
        {
            if (_modoCreacionMatriz || !_matrizEditandoId.HasValue)
                return false;

            string clave = (_txtClaveMatriz.Text ?? string.Empty).Trim();
            string descripcion = (_txtDescripcionMatriz.Text ?? string.Empty).Trim();
            string unidad = (_txtUnidadMatriz.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(descripcion) || string.IsNullOrWhiteSpace(unidad))
            {
                MessageBox.Show("Clave, Descripción y Unidad son obligatorios.", "Matriz", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            var matrizEdit = _context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .FirstOrDefault(m => m.Id == _matrizEditandoId.Value);
            if (matrizEdit == null)
                return false;

            bool claveDuplicada = _context.Matrices.Any(m => m.ProyectoId == _proyectoId
                && m.Id != matrizEdit.Id
                && m.Clave == clave);
            if (claveDuplicada)
            {
                MessageBox.Show(MatrixSaveFlowService.GetDuplicateKeyMessage(), "Clave Duplicada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtClaveMatriz.Focus();
                return false;
            }

            matrizEdit.Clave = clave;
            matrizEdit.Descripcion = descripcion;
            matrizEdit.Unidad = unidad;
            matrizEdit.Tipo = GetTipoCabeceraSeleccionado();
            matrizEdit.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            _matrizActual = matrizEdit;
            CapturarEstadoBaseCabeceraDesdeUI();
            return true;
        }

        private void LimpiarEstadoEdicionParaCambioExterno()
        {
            _modoSubedicionComponente = false;
            _matrizPadreIdSubedicion = null;
            _filaPadreSubedicion = null;
            _modoCreacionMatriz = false;
            _modoEdicionCabecera = false;
            _matrizEditandoId = null;
            _onMatrixHeaderSaved = null;
            _onMatrixHeaderCancelled = null;
            _cabeceraBaseClave = string.Empty;
            _cabeceraBaseDescripcion = string.Empty;
            _cabeceraBaseUnidad = string.Empty;
            _cabeceraBaseTipo = TipoMatriz.APU;
            MostrarDatosCabecera(false);
            _panelHeader.BackColor = _headerColorNormal;
            _filaContextualComponentes = -1;
        }

        private void RestaurarFilaActualEnPresupuesto()
        {
            if (_dgvPresupuesto == null || _filaActual < 0 || _filaActual >= _dgvPresupuesto.Rows.Count)
                return;

            try
            {
                var row = _dgvPresupuesto.Rows[_filaActual];
                if (row.Cells.Count > 0)
                {
                    _dgvPresupuesto.CurrentCell = row.Cells[Math.Max(0, _dgvPresupuesto.CurrentCell?.ColumnIndex ?? 0) < row.Cells.Count ? Math.Max(0, _dgvPresupuesto.CurrentCell?.ColumnIndex ?? 0) : 0];
                    _dgvPresupuesto.ClearSelection();
                    row.Selected = true;
                    _dgvPresupuesto.Focus();
                }
            }
            catch { }
        }

        private bool ConfirmarCambioDeContextoExterno(int filaDestino)
        {
            bool cambiaFila = filaDestino != _filaActual;
            bool hayPendientes = TieneCambiosPendientesCabecera();
            bool estaEnSubedicion = _modoSubedicionComponente;

            if (!cambiaFila)
                return true;

            if (!estaEnSubedicion && !hayPendientes)
                return true;

            if (_modoCreacionMatriz)
            {
                var rCrear = MessageBox.Show(
                    "Hay una matriz nueva sin guardar en el panel. " + "\n" +
                    "Sí: descartar y cambiar de concepto. " + "\n" +
                    "No: permanecer en la matriz actual.",
                    "Cambiar de concepto",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (rCrear == DialogResult.Yes)
                {
                    LimpiarEstadoEdicionParaCambioExterno();
                    return true;
                }

                RestaurarFilaActualEnPresupuesto();
                return false;
            }

            var mensaje = estaEnSubedicion
                ? "Estás editando una matriz componente dentro del panel."
                : "Hay cambios pendientes en la matriz mostrada en el panel.";

            var r = MessageBox.Show(
                mensaje + "\n" +
                "\n" +
                "Sí: guardar cabecera y cambiar de concepto. " + "\n" +
                "No: descartar cambios de cabecera y cambiar de concepto. " + "\n" +
                "Cancelar: permanecer en el concepto actual.",
                "Cambiar de concepto",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (r == DialogResult.Cancel)
            {
                RestaurarFilaActualEnPresupuesto();
                return false;
            }

            if (r == DialogResult.Yes && hayPendientes && !GuardarCabeceraEdicionActual())
            {
                RestaurarFilaActualEnPresupuesto();
                return false;
            }

            LimpiarEstadoEdicionParaCambioExterno();
            return true;
        }
        
        /// <summary>
        /// Fuerza la recarga del panel para la fila indicada.
        /// Llamar después de asignar o cambiar la matriz de un concepto.
        /// </summary>
        public void NotificarFilaCambiada(int fila)
        {
            try
            {
                // Si el panel está creando/editando una matriz o subeditando un componente,
                // ignorar recargas externas del presupuesto para no pisar el estado visual
                // ni ocultar la cabecera de captura detrás de formularios modales.
                if (_modoCreacionMatriz || _modoEdicionCabecera || _modoSubedicionComponente)
                    return;

                if (_dgvPresupuesto == null || fila < 0 || fila >= _dgvPresupuesto.Rows.Count)
                    return;

                CargarMatrizDeFila(fila, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PanelMatricesEmbebido.NotificarFilaCambiada: {ex.Message}");
            }
        }

        private void ConfigurarMenuContextualComponentes()
        {
            _menuContextualComponentes = new ContextMenuStrip();
            _mnuEditarMatrizComponente = new ToolStripMenuItem("Editar matriz componente");
            _mnuEditarMatrizComponente.Click += (_, __) => EditarMatrizComponenteDesdeContexto();
            _menuContextualComponentes.Items.Add(_mnuEditarMatrizComponente);
            _menuContextualComponentes.Opening += MenuContextualComponentes_Opening;
            _menuContextualComponentes.Closed += (_, __) => _filaContextualComponentes = -1;
        }

        private void MenuContextualComponentes_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var comp = ObtenerComponenteSeleccionadoParaContexto();
            bool puedeEditar = !_modoCreacionMatriz && !_modoSubedicionComponente && EsComponenteMatrizEditable(comp);
            _mnuEditarMatrizComponente.Enabled = puedeEditar;
            e.Cancel = !puedeEditar;
        }

        private ComponenteMatriz ObtenerComponenteSeleccionadoParaContexto()
        {
            if (_dgvComponentes == null)
                return null;

            int fila = _filaContextualComponentes;
            if (fila < 0)
                fila = _dgvComponentes.CurrentRow?.Index ?? -1;

            if (fila < 0)
                return null;

            return ObtenerComponentePorFilaTrabajo(fila);
        }

        private bool EsComponenteMatrizEditable(ComponenteMatriz comp)
        {
            return comp?.TipoComponente == TipoComponenteMatriz.Auxiliar &&
                   comp.Auxiliar != null &&
                   (comp.Auxiliar.Tipo == TipoMatriz.Basico || comp.Auxiliar.Tipo == TipoMatriz.Cuadrilla);
        }

        private void EditarMatrizComponenteDesdeContexto()
        {
            var comp = ObtenerComponenteSeleccionadoParaContexto();
            if (!EsComponenteMatrizEditable(comp) || comp?.AuxiliarId == null || _matrizActual == null)
                return;

            if (_modoSubedicionComponente)
                return;

            _matrizPadreIdSubedicion = _matrizActual.Id;
            _filaPadreSubedicion = _dgvComponentes.CurrentRow?.Index;
            _modoSubedicionComponente = true;

            BeginEditMatrix(
                comp.AuxiliarId.Value,
                onSaved: _ => FinalizarSubedicionComponente(true),
                onCancelled: () => FinalizarSubedicionComponente(false));
        }

        private void FinalizarSubedicionComponente(bool refrescarPadre)
        {
            if (!_modoSubedicionComponente || !_matrizPadreIdSubedicion.HasValue)
                return;

            int matrizPadreId = _matrizPadreIdSubedicion.Value;
            int? filaPadre = _filaPadreSubedicion;

            _modoSubedicionComponente = false;
            _matrizPadreIdSubedicion = null;
            _filaPadreSubedicion = null;
            _onMatrixHeaderSaved = null;
            _onMatrixHeaderCancelled = null;
            _modoEdicionCabecera = false;

            if (refrescarPadre)
                RecalcularMatrizYConceptos(matrizPadreId);

            var matrizPadre = CargarMatrizConComponentes(matrizPadreId, false);
            if (matrizPadre == null)
            {
                MostrarSinSeleccion("No se pudo regresar a la matriz padre", "La matriz padre ya no está disponible.");
                return;
            }

            _matrizActual = matrizPadre;
            _matrizEditandoId = matrizPadreId;
            MostrarDatosCabecera(true);
            MostrarMatriz(matrizPadre, null);
            _panelBotonesAgregar.Enabled = true;

            try
            {
                if (filaPadre.HasValue && filaPadre.Value >= 0 && filaPadre.Value < _dgvComponentes.Rows.Count)
                    _dgvComponentes.CurrentCell = _dgvComponentes.Rows[filaPadre.Value].Cells[0];
            }
            catch { }
        }

        private Matriz CargarMatrizConComponentes(int matrizId, bool asNoTracking)
        {
            IQueryable<Matriz> query = _context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta);

            if (asNoTracking)
                query = query.AsNoTracking();

            return query.FirstOrDefault(m => m.Id == matrizId);
        }

        private void RecalcularMatrizYConceptos(int matrizId)
        {
            var matrizTracked = CargarMatrizConComponentes(matrizId, false);
            if (matrizTracked == null)
                return;

            var proyecto = _context.Proyectos.Find(_proyectoId);
            if (proyecto == null)
                return;

            var totals = MatrixComponentCalculationService.Recalculate(
                matrizTracked.Componentes.ToList(), proyecto.DecimalesImporte);
            matrizTracked.CostoDirecto = BudgetPricingService.RoundImporte(proyecto, totals.CostoDirectoTotal);
            matrizTracked.FechaModificacion = DateTime.Now;

            decimal nuevoPrecioUnitario = BudgetPricingService.CalculateUnitPrice(proyecto, matrizTracked.CostoDirecto);

            foreach (var row in _dgvPresupuesto?.Rows.Cast<DataGridViewRow>() ?? Enumerable.Empty<DataGridViewRow>())
            {
                if (row.Tag is not ConceptoPresupuesto c || c.MatrizId != matrizId)
                    continue;

                c.CostoDirectoUnitario = matrizTracked.CostoDirecto;
                c.CostoDirectoTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, c.Cantidad, matrizTracked.CostoDirecto);
                c.PrecioUnitario = nuevoPrecioUnitario;
                c.ImporteTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, c.Cantidad, nuevoPrecioUnitario);

                foreach (DataGridViewColumn col in _dgvPresupuesto.Columns)
                {
                    if (col.Tag is not ColumnaPersonalizada colDef)
                        continue;

                    if (colDef.NombreInterno == "PrecioUnitario")
                        row.Cells[col.Index].Value = nuevoPrecioUnitario.ToStringImporte();
                    else if (colDef.NombreInterno == "Importe")
                        row.Cells[col.Index].Value = c.ImporteTotal.ToStringImporte();
                }
            }

            _context.SaveChanges();
            _dgvPresupuesto?.Refresh();

            if (_filaActual >= 0)
                MatrizActualizada?.Invoke(this, _filaActual);
        }

        private void CargarMatrizDeFila(int fila, bool forceReload)
        {
            if (_dgvPresupuesto == null || fila < 0 || fila >= _dgvPresupuesto.Rows.Count)
            { MostrarSinSeleccion(); return; }

            if (!ConfirmarCambioDeContextoExterno(fila))
                return;

            _filaActual = fila;
            var concepto = _dgvPresupuesto.Rows[fila].Tag as ConceptoPresupuesto;

            if (concepto == null)
            {
                MostrarSinSeleccion("Selecciona un concepto del presupuesto para ver su APU", "");
                return;
            }

            if (concepto.EsAgrupador)
            {
                MostrarSinSeleccion("El elemento seleccionado es un agrupador", "Selecciona una fila tipo concepto para consultar o editar su APU.");
                return;
            }

            if (concepto.MatrizId == null)
            {
                MostrarSinSeleccion("Concepto sin APU asignada", "Usa la columna P.U. o la búsqueda inteligente en Descripción para vincular una matriz.");
                return;
            }

            bool mismaFila = _ultimaFilaCargada == fila;
            bool mismoConcepto = _ultimoConceptoId == concepto.Id;
            bool mismaMatriz = _ultimaMatrizId == concepto.MatrizId;

            if (!forceReload && mismaFila && mismoConcepto && mismaMatriz && _matrizActual != null)
            {
                ActualizarInfoConcepto(concepto);
                ActualizarNavegacion();
                return;
            }

            // Cargar la matriz FRESCA desde BD solo cuando realmente cambia el concepto/matriz
            _matrizActual = CargarMatrizConComponentes(concepto.MatrizId.Value, true);

            if (_matrizActual == null)
            {
                MostrarSinSeleccion("No se pudo cargar la APU asociada", "La fila apunta a una matriz inexistente o no disponible.");
                return;
            }

            _ultimaFilaCargada = fila;
            _ultimoConceptoId = concepto.Id;
            _ultimaMatrizId = concepto.MatrizId;

            MostrarMatriz(_matrizActual, concepto);
            ActualizarNavegacion();
        }

        private void MostrarMatriz(Matriz matriz, ConceptoPresupuesto concepto)
        {
            _matrizActual = matriz;
            _matrizEditandoId = matriz?.Id;
            _modoCreacionMatriz = false;
            _modoEdicionCabecera = true;

            string tipoTexto = matriz.Tipo == TipoMatriz.Cuadrilla ? "Cuadrilla" : (matriz.Tipo == TipoMatriz.Basico ? "Básico" : "APU");
            if (_modoSubedicionComponente && _matrizPadreIdSubedicion.HasValue)
            {
                _panelHeader.BackColor = _headerColorSubedicion;
                var padre = _context.Matrices.AsNoTracking().FirstOrDefault(m => m.Id == _matrizPadreIdSubedicion.Value);
                string padreClave = string.IsNullOrWhiteSpace(padre?.Clave) ? "(sin clave)" : padre!.Clave;
                _lblTituloMatriz.Text = $"Matriz: {padreClave} → {tipoTexto}: {matriz.Clave}";
                _lblInfoMatriz.Text = string.IsNullOrWhiteSpace(matriz.Descripcion)
                    ? "Subedición de matriz componente dentro del panel."
                    : matriz.Descripcion;
            }
            else
            {
                _panelHeader.BackColor = _headerColorNormal;
                _lblTituloMatriz.Text = $"Matriz: {matriz.Clave}";
                if (concepto != null) ActualizarInfoConcepto(concepto, matriz);
                else _lblInfoMatriz.Text = string.IsNullOrWhiteSpace(matriz.Descripcion)
                    ? $"{tipoTexto} lista para edición."
                    : $"{tipoTexto}: {matriz.Descripcion}";
            }
            _txtClaveMatriz.Text = matriz.Clave ?? string.Empty;
            _txtDescripcionMatriz.Text = matriz.Descripcion ?? string.Empty;
            _txtUnidadMatriz.Text = matriz.Unidad ?? string.Empty;
            _rbTipoApu.Checked = matriz.Tipo == TipoMatriz.APU;
            _rbTipoBasico.Checked = matriz.Tipo == TipoMatriz.Basico;
            _rbTipoCuadrilla.Checked = matriz.Tipo == TipoMatriz.Cuadrilla;
            CapturarEstadoBaseCabeceraDesdeMatriz(matriz);
            _panelBotonesAgregar.Enabled = true;
            MostrarDatosCabecera(true);

            PintarComponentesEnGrid(matriz.Componentes);
        }

        private void ActualizarInfoConcepto(ConceptoPresupuesto concepto, Matriz matriz = null)
        {
            var matrizInfo = matriz ?? _matrizActual;
            string unidad = matrizInfo?.Unidad ?? concepto?.Unidad ?? "—";
            string claveConcepto = string.IsNullOrWhiteSpace(concepto?.Clave) ? "(sin clave)" : concepto.Clave;
            string descripcionConcepto = string.IsNullOrWhiteSpace(concepto?.Descripcion) ? "(sin descripción)" : concepto.Descripcion;
            _lblInfoMatriz.Text = $"Unidad: {unidad}   |   Concepto en presupuesto: {claveConcepto} - {descripcionConcepto}";
        }

        private void MostrarSinSeleccion(string titulo = "Selecciona un concepto en el presupuesto para ver y editar su APU", string info = "")
        {
            _matrizActual = null;
            _ultimaFilaCargada = -1;
            _ultimoConceptoId = null;
            _ultimaMatrizId = null;
            _panelHeader.BackColor = _headerColorNormal;
            _lblTituloMatriz.Text = titulo;
            _lblInfoMatriz.Text   = info;
            _panelBotonesAgregar.Enabled = false;
            MostrarDatosCabecera(false);
            _dgvComponentes.Rows.Clear();
            _lblMat.Text = "Mat: —"; _lblMO.Text = "M.O.: —";
            _lblMaq.Text = "Maq.: —"; _lblBas.Text = "Bas.: —";
            _lblDir.Text = "Costo Directo: —";
            _btnAnterior.Enabled = _btnSiguiente.Enabled = false;
        }

        // ── Guardar y propagar al presupuesto ─────────────────────────
        // Se llama automáticamente después de agregar/eliminar. No hay botón Guardar.
        private void GuardarYPropagar()
        {
            if (_matrizActual == null || _filaActual < 0) return;
            try
            {
                // Recargar la matriz CON TRACKING para calcular y guardar
                var matrizTracked = _context.Matrices
                    .Include(m => m.Componentes).ThenInclude(c => c.Material)
                    .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                    .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                    .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                    .FirstOrDefault(m => m.Id == _matrizActual.Id);

                if (matrizTracked == null) return;

                // Calcular CostoDirecto con el motor del proyecto
                var _proyectoPME = _context.Proyectos.Find(_proyectoId);
                if (_proyectoPME == null) return; // Sin proyecto no podemos calcular correctamente

                var totals = SOPRO.Application.Services.MatrixComponentCalculationService.Recalculate(
                    matrizTracked.Componentes.ToList(), _proyectoPME.DecimalesImporte);
                matrizTracked.CostoDirecto = BudgetPricingService.RoundImporte(_proyectoPME, totals.CostoDirectoTotal);

                decimal nuevoCostoDirecto = matrizTracked.CostoDirecto;

                // Calcular PrecioUnitario usando la precisión configurada del proyecto.
                decimal nuevoPrecioUnitario = BudgetPricingService.CalculateUnitPrice(_proyectoPME, nuevoCostoDirecto);

                // Actualizar el concepto en la fila del presupuesto usando el motor
                var concepto = _dgvPresupuesto.Rows[_filaActual].Tag as ConceptoPresupuesto;
                if (concepto != null)
                {
                    concepto.CostoDirectoUnitario = nuevoCostoDirecto;
                    concepto.CostoDirectoTotal    = BudgetPricingService.MultiplyUsingDisplayPrecision(_proyectoPME, concepto.Cantidad, nuevoCostoDirecto);
                    concepto.PrecioUnitario        = nuevoPrecioUnitario;
                    concepto.ImporteTotal          = BudgetPricingService.MultiplyUsingDisplayPrecision(_proyectoPME, concepto.Cantidad, nuevoPrecioUnitario);
                }

                _context.SaveChanges();

                // Actualizar celdas visualmente en el grid del presupuesto
                // para TODOS los conceptos que usen esta matriz (no solo el actual)
                if (concepto != null)
                {
                    for (int i = 0; i < _dgvPresupuesto.Rows.Count; i++)
                    {
                        var c = _dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                        if (c == null || c.MatrizId != _matrizActual.Id) continue;

                        c.CostoDirectoUnitario = nuevoCostoDirecto;
                        c.CostoDirectoTotal    = BudgetPricingService.MultiplyUsingDisplayPrecision(_proyectoPME, c.Cantidad, nuevoCostoDirecto);
                        c.PrecioUnitario       = nuevoPrecioUnitario;
                        c.ImporteTotal         = BudgetPricingService.MultiplyUsingDisplayPrecision(_proyectoPME, c.Cantidad, nuevoPrecioUnitario);

                        foreach (DataGridViewColumn col in _dgvPresupuesto.Columns)
                        {
                            if (col.Tag is ColumnaPersonalizada colDef)
                            {
                                if (colDef.NombreInterno == "PrecioUnitario")
                                    _dgvPresupuesto.Rows[i].Cells[col.Index].Value = nuevoPrecioUnitario.ToStringImporte();
                                else if (colDef.NombreInterno == "Importe")
                                    _dgvPresupuesto.Rows[i].Cells[col.Index].Value = c.ImporteTotal.ToStringImporte();
                            }
                        }
                    }
                    _context.SaveChanges();
                    _dgvPresupuesto.Refresh();
                    // Notificar para recalcular la jerarquía de totales
                    MatrizActualizada?.Invoke(this, _filaActual);
                }

                // Refrescar el panel con datos frescos. Si estamos en edición explícita
                // desde el workspace (por ejemplo, abierta desde el selector APU), debemos
                // permanecer sobre la matriz editada aunque la fila actual del presupuesto
                // apunte a otra matriz distinta.
                if (_modoEdicionCabecera && _matrizEditandoId.HasValue)
                {
                    var matrizRefrescada = _context.Matrices
                        .Include(m => m.Componentes).ThenInclude(c => c.Material)
                        .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                        .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                        .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                        .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                        .AsNoTracking()
                        .FirstOrDefault(m => m.Id == _matrizEditandoId.Value);

                    if (matrizRefrescada != null)
                    {
                        _matrizActual = matrizRefrescada;
                        MostrarMatriz(matrizRefrescada, null);
                    }
                }
                else
                {
                    CargarMatrizDeFila(_filaActual, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private bool EnsureMatrixSavedForComponents()
        {
            if (_matrizActual != null) return true;

            string clave = (_txtClaveMatriz.Text ?? string.Empty).Trim();
            string descripcion = (_txtDescripcionMatriz.Text ?? string.Empty).Trim();
            string unidad = (_txtUnidadMatriz.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(descripcion) || string.IsNullOrWhiteSpace(unidad))
            {
                MessageBox.Show("Para agregar componentes primero captura Clave, Descripción y Unidad.", "Matriz", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _txtClaveMatriz.Focus();
                return false;
            }

            return true;
        }

        // ── Agregar insumos ────────────────────────────────────────────
        private void BtnAgregarMaterial_Click(object sender, EventArgs e)
        {
            if (_modoCreacionMatriz)
            {
                if (!EnsureMatrixSavedForComponents()) return;
                AgregarComponentesTemporales(TipoComponenteMatriz.Material);
                return;
            }
            if (!EnsureMatrixSavedForComponents()) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.Material);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }

        private void BtnAgregarMO_Click(object sender, EventArgs e)
        {
            if (_modoCreacionMatriz)
            {
                if (!EnsureMatrixSavedForComponents()) return;
                AgregarComponentesTemporales(TipoComponenteMatriz.ManoDeObra);
                return;
            }
            if (!EnsureMatrixSavedForComponents()) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.ManoDeObra);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }

        private void BtnAgregarMaquinaria_Click(object sender, EventArgs e)
        {
            if (_modoCreacionMatriz)
            {
                if (!EnsureMatrixSavedForComponents()) return;
                AgregarComponentesTemporales(TipoComponenteMatriz.Maquinaria);
                return;
            }
            if (!EnsureMatrixSavedForComponents()) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.Maquinaria);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }

        private void BtnAgregarBasico_Click(object sender, EventArgs e)
        {
            if (_modoCreacionMatriz)
            {
                if (!EnsureMatrixSavedForComponents()) return;
                AgregarComponentesTemporales(TipoComponenteMatriz.Auxiliar);
                return;
            }
            if (!EnsureMatrixSavedForComponents()) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.Auxiliar);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }
        
        private void BtnAgregarHerramienta_Click(object sender, EventArgs e)
        {
            if (_modoCreacionMatriz)
            {
                if (!EnsureMatrixSavedForComponents()) return;
                AgregarComponentesTemporales(TipoComponenteMatriz.Herramienta);
                return;
            }
            if (!EnsureMatrixSavedForComponents()) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.Herramienta);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }

        // ── Eliminar componente ────────────────────────────────────────
        private void DgvComponentes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvComponentes.Columns["ColEliminar"] == null) return;
            if (e.ColumnIndex != _dgvComponentes.Columns["ColEliminar"].Index) return;
            if (MessageBox.Show("¿Eliminar este componente?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            if (_modoCreacionMatriz)
            {
                if (MatrixComponentCollectionService.RemoveAt(_componentesTemp, e.RowIndex))
                {
                    RefrescarVistaTemporal();
                }
                return;
            }

            if (_matrizActual == null) return;
            int compId = Convert.ToInt32(_dgvComponentes.Rows[e.RowIndex].Cells["ColId"].Value);

            var comp = _context.ComponentesMatriz.Find(compId);
            if (comp == null) return;
            _context.ComponentesMatriz.Remove(comp);
            _context.SaveChanges();
            GuardarYPropagar();
        }

        // ── Navegación ─────────────────────────────────────────────────
        private void ActualizarNavegacion()
        {
            _btnAnterior.Enabled  = FilaConceptoAnterior() >= 0;
            _btnSiguiente.Enabled = FilaConceptoSiguiente() >= 0;
        }

        // Busca por Orden del ConceptoPresupuesto, no por posición en el grid
        private int FilaConceptoAnterior()
        {
            if (_dgvPresupuesto == null || _filaActual < 0) return -1;
            var conceptoActual = _dgvPresupuesto.Rows[_filaActual].Tag as ConceptoPresupuesto;
            if (conceptoActual == null) return -1;

            // Buscar en el grid el concepto cuyo Orden es menor y más cercano al actual
            int ordenActual = conceptoActual.Orden;
            int filaCandidata = -1;
            int ordenCandidato = int.MinValue;

            for (int i = 0; i < _dgvPresupuesto.Rows.Count; i++)
            {
                var c = _dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                if (c == null || c.EsAgrupador || c.MatrizId == null) continue;
                if (c.Orden < ordenActual && c.Orden > ordenCandidato)
                {
                    ordenCandidato = c.Orden;
                    filaCandidata = i;
                }
            }
            return filaCandidata;
        }

        private int FilaConceptoSiguiente()
        {
            if (_dgvPresupuesto == null || _filaActual < 0) return -1;
            var conceptoActual = _dgvPresupuesto.Rows[_filaActual].Tag as ConceptoPresupuesto;
            if (conceptoActual == null) return -1;

            int ordenActual = conceptoActual.Orden;
            int filaCandidata = -1;
            int ordenCandidato = int.MaxValue;

            for (int i = 0; i < _dgvPresupuesto.Rows.Count; i++)
            {
                var c = _dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                if (c == null || c.EsAgrupador || c.MatrizId == null) continue;
                if (c.Orden > ordenActual && c.Orden < ordenCandidato)
                {
                    ordenCandidato = c.Orden;
                    filaCandidata = i;
                }
            }
            return filaCandidata;
        }

        private void NavegerAFila(int fila)
        {
            try
            {
                if (fila < 0 || fila >= _dgvPresupuesto.Rows.Count) return;
                
                // Primera celda VISIBLE para evitar error de celda invisible
                foreach (DataGridViewColumn col in _dgvPresupuesto.Columns)
                {
                    if (col.Visible)
                    {
                        _dgvPresupuesto.ClearSelection();
                        _dgvPresupuesto.CurrentCell = _dgvPresupuesto.Rows[fila].Cells[col.Index];
                        break;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Ignorar error de celda invisible
            }
        }

        private void BtnAnterior_Click(object sender, EventArgs e)  { int f = FilaConceptoAnterior();  if (f >= 0) NavegerAFila(f); }
        private void BtnSiguiente_Click(object sender, EventArgs e) { int f = FilaConceptoSiguiente(); if (f >= 0) NavegerAFila(f); }

        public void BeginCreateMatrix(TipoMatriz tipo, Action<Matriz>? onSaved = null, Action? onCancelled = null)
        {
            _modoCreacionMatriz = true;
            _modoEdicionCabecera = true;
            _matrizEditandoId = null;
            _onMatrixHeaderSaved = onSaved;
            _onMatrixHeaderCancelled = onCancelled;
            _matrizActual = null;
            _componentesTemp.Clear();
            _dgvComponentes.Rows.Clear();
            _panelBotonesAgregar.Enabled = true;
            _txtClaveMatriz.Text = string.Empty;
            _txtDescripcionMatriz.Text = string.Empty;
            _txtUnidadMatriz.Text = string.Empty;
            _rbTipoApu.Checked = tipo == TipoMatriz.APU;
            _rbTipoBasico.Checked = tipo == TipoMatriz.Basico;
            _rbTipoCuadrilla.Checked = tipo == TipoMatriz.Cuadrilla;
            _lblTituloMatriz.Text = tipo == TipoMatriz.Cuadrilla ? "Nueva cuadrilla" : (tipo == TipoMatriz.Basico ? "Nueva matriz básica" : "Nueva APU");
            _lblInfoMatriz.Text = "Captura los datos generales y agrega componentes. Guarda la matriz al finalizar.";
            _lblMat.Text = "Mat: 0.00"; _lblMO.Text = "M.O.: 0.00"; _lblMaq.Text = "Maq.: 0.00"; _lblBas.Text = "Bas.: 0.00"; _lblDir.Text = "Costo Directo: 0.00";
            CapturarEstadoBaseCabeceraDesdeUI();
            MostrarDatosCabecera(true);
            _txtClaveMatriz.Focus();
        }

        public void BeginEditMatrix(int matrizId, Action<Matriz>? onSaved = null, Action? onCancelled = null)
        {
            var matriz = CargarMatrizConComponentes(matrizId, false);
            if (matriz == null) return;

            _modoCreacionMatriz = false;
            _modoEdicionCabecera = true;
            _matrizEditandoId = matrizId;
            _onMatrixHeaderSaved = onSaved;
            _onMatrixHeaderCancelled = onCancelled;
            _componentesTemp.Clear();
            _matrizActual = matriz;
            _txtClaveMatriz.Text = matriz.Clave ?? string.Empty;
            _txtDescripcionMatriz.Text = matriz.Descripcion ?? string.Empty;
            _txtUnidadMatriz.Text = matriz.Unidad ?? string.Empty;
            _rbTipoApu.Checked = matriz.Tipo == TipoMatriz.APU;
            _rbTipoBasico.Checked = matriz.Tipo == TipoMatriz.Basico;
            _rbTipoCuadrilla.Checked = matriz.Tipo == TipoMatriz.Cuadrilla;
            MostrarDatosCabecera(true);
            MostrarMatriz(matriz, null);
            _panelBotonesAgregar.Enabled = true;
            _txtClaveMatriz.Focus();
        }

        private void MostrarDatosCabecera(bool visible)
        {
            _panelDatos.Visible = visible;
            _btnGuardarMatriz.Visible = visible;
            _btnCancelarMatriz.Visible = visible;
        }

        private TipoMatriz GetTipoCabeceraSeleccionado()
        {
            if (_rbTipoBasico.Checked) return TipoMatriz.Basico;
            if (_rbTipoCuadrilla.Checked) return TipoMatriz.Cuadrilla;
            return TipoMatriz.APU;
        }


private async void BtnGuardarMatriz_Click(object sender, EventArgs e)
{
    string clave = (_txtClaveMatriz.Text ?? string.Empty).Trim();
    string descripcion = (_txtDescripcionMatriz.Text ?? string.Empty).Trim();
    string unidad = (_txtUnidadMatriz.Text ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(descripcion) || string.IsNullOrWhiteSpace(unidad))
    {
        MessageBox.Show("Clave, Descripción y Unidad son obligatorios.", "Matriz", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
    }

    try
    {
        if (_modoCreacionMatriz || !_matrizEditandoId.HasValue)
        {
            var validation = MatrixSaveFlowService.ValidateBeforeSave(clave, descripcion, _componentesTemp);
            if (!validation.IsValid)
            {
                MessageBox.Show(validation.Message, "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var claveDuplicada = await MatrixApplicationService.ExistsByKeyAsync(_context, _proyectoId, clave, null);
            if (claveDuplicada)
            {
                MessageBox.Show(MatrixSaveFlowService.GetDuplicateKeyMessage(), "Clave Duplicada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtClaveMatriz.Focus();
                return;
            }

            var totals = MatrixComponentCalculationService.Recalculate(_componentesTemp, FormatoHelper.DecimalesImporte);
            var dto = MatrixEditorService.BuildEditDto(
                _proyectoId,
                clave,
                descripcion,
                unidad,
                _rbTipoCuadrilla.Checked,
                _rbTipoApu.Checked,
                totals.CostoDirectoTotal,
                _componentesTemp);

            var result = await MatrixApplicationService.SaveAsync(_context, dto, null);
            var matriz = _context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .FirstOrDefault(m => m.Id == result.MatrixId);
            if (matriz == null) return;

            _matrizActual = matriz;
            _matrizEditandoId = matriz.Id;
            _modoCreacionMatriz = false;
            _componentesTemp.Clear();
            _panelBotonesAgregar.Enabled = true;
            MostrarDatosCabecera(true);
            MostrarMatriz(matriz, null);
            _onMatrixHeaderSaved?.Invoke(matriz);
            return;
        }

        Matriz matrizEdit = _context.Matrices.Include(m => m.Componentes).ThenInclude(c => c.Material)
            .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
            .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
            .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
            .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
            .FirstOrDefault(m => m.Id == _matrizEditandoId.Value);
        if (matrizEdit == null) return;
        matrizEdit.Clave = clave;
        matrizEdit.Descripcion = descripcion;
        matrizEdit.Unidad = unidad;
        matrizEdit.Tipo = GetTipoCabeceraSeleccionado();
        matrizEdit.FechaModificacion = DateTime.Now;
        _context.SaveChanges();

        _matrizActual = matrizEdit;
        _panelBotonesAgregar.Enabled = true;
        MostrarDatosCabecera(true);
        MostrarMatriz(matrizEdit, null);
        _onMatrixHeaderSaved?.Invoke(matrizEdit);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error al guardar la matriz: {ex.Message}", "Matriz", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

private void BtnCancelarMatriz_Click(object sender, EventArgs e)
        {
            if (_modoSubedicionComponente)
            {
                _onMatrixHeaderCancelled?.Invoke();
                return;
            }

            this.SuspendLayout();
            _panelDatos.SuspendLayout();
            try
            {
                _modoCreacionMatriz = false;
                _modoEdicionCabecera = false;
                _onMatrixHeaderCancelled?.Invoke();

                if (_matrizActual != null)
                {
                    MostrarMatriz(_matrizActual, null);
                }
                else
                {
                    MostrarDatosCabecera(false);
                    MostrarSinSeleccion();
                }
            }
            finally
            {
                _panelDatos.ResumeLayout(true);
                this.ResumeLayout(true);
                Invalidate(true);
            }
        }


        public bool TryUndo()
        {
            if (!_undoManager.CanUndo)
                return false;

            try
            {
                _isUndoRedo = true;
                return _undoManager.Undo();
            }
            finally
            {
                _isUndoRedo = false;
            }
        }

        public bool TryRedo()
        {
            if (!_undoManager.CanRedo)
                return false;

            try
            {
                _isUndoRedo = true;
                return _undoManager.Redo();
            }
            finally
            {
                _isUndoRedo = false;
            }
        }

        private static bool EsColumnaUndoPanel(string columnName)
        {
            return columnName == "colDescripcion"
                || columnName == "colUnidad"
                || columnName == "colCantidad"
                || columnName == "colPU";
        }

        private void AplicarUndoRedoPanelCelda(int rowIndex, int columnIndex, string value)
        {
            if (rowIndex < 0 || rowIndex >= _dgvComponentes.Rows.Count)
                return;
            if (columnIndex < 0 || columnIndex >= _dgvComponentes.Columns.Count)
                return;

            var comp = ObtenerComponentePorFilaTrabajo(rowIndex);
            if (comp == null)
                return;

            var columnName = _dgvComponentes.Columns[columnIndex].Name switch
            {
                "ColDesc" => "colDescripcion",
                "ColUnidad" => "colUnidad",
                "ColCantidad" => "colCantidad",
                "ColPU" => "colPU",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(columnName))
                return;

            var editResult = MatrixComponentGridFlowService.ApplyCellEdit(
                comp,
                columnName,
                value,
                FormatoHelper.DecimalesImporte);

            if (editResult == null || !editResult.Success)
                return;

            _dgvComponentes.CurrentCell = _dgvComponentes.Rows[rowIndex].Cells[columnIndex];

            if (_modoCreacionMatriz)
            {
                RefrescarVistaTemporal();
            }
            else
            {
                _context.SaveChanges();
                GuardarYPropagar();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && TryUndo())
                return true;

            if (keyData == (Keys.Control | Keys.Y) && TryRedo())
                return true;

            var keyCode = keyData & Keys.KeyCode;
            if (keyCode == Keys.Escape)
            {
                if (_panelDatos != null && _panelDatos.Visible)
                {
                    BtnCancelarMatriz_Click(this, EventArgs.Empty);
                    return true;
                }

                SolicitudCerrarWorkspace?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }


// ── Construcción de controles ──────────────────────────────────
        private void InicializarComponentes()
        {
            this.Dock = DockStyle.Fill; this.BackColor = Color.White;

            _panelHeader = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(40, 40, 65) };
            _lblTituloMatriz = new Label { Location = new Point(10, 5),  Size = new Size(770, 20), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.White };
            _lblInfoMatriz   = new Label { Location = new Point(10, 28), Size = new Size(770, 16), Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Silver };

            _btnAnterior  = new Button { Text = "▲", Size = new Size(30, 20), Location = new Point(888, 6),  FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60,60,90), ForeColor = Color.White, Enabled = false };
            _btnSiguiente = new Button { Text = "▼", Size = new Size(30, 20), Location = new Point(920, 6),  FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60,60,90), ForeColor = Color.White, Enabled = false };
            _btnAnterior.FlatAppearance.BorderColor = _btnSiguiente.FlatAppearance.BorderColor = Color.FromArgb(90,90,130);
            _btnAnterior.Click  += BtnAnterior_Click;
            _btnSiguiente.Click += BtnSiguiente_Click;

            _panelHeader.Controls.AddRange(new Control[] { _lblTituloMatriz, _lblInfoMatriz, _btnAnterior, _btnSiguiente });

            _panelDatos = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(250,250,252), Visible = false };
            var lblClave = new Label { Text = "Clave", Location = new Point(10, 10), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtClaveMatriz = new TextBox { Location = new Point(60, 7), Size = new Size(120, 24), CharacterCasing = CharacterCasing.Upper };
            var lblUnidad = new Label { Text = "Unidad", Location = new Point(190, 10), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtUnidadMatriz = new TextBox { Location = new Point(245, 7), Size = new Size(90, 24) };
            var lblDesc = new Label { Text = "Descripción", Location = new Point(345, 10), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _txtDescripcionMatriz = new TextBox { Location = new Point(425, 7), Size = new Size(360, 24) };
            _rbTipoApu = new RadioButton { Text = "APU", Location = new Point(60, 40), AutoSize = true, Checked = true };
            _rbTipoBasico = new RadioButton { Text = "Básico", Location = new Point(130, 40), AutoSize = true };
            _rbTipoCuadrilla = new RadioButton { Text = "Cuadrilla", Location = new Point(220, 40), AutoSize = true };
            _btnGuardarMatriz = new Button { Text = "Guardar", Location = new Point(690, 38), Size = new Size(95, 26), BackColor = Color.FromArgb(31, 122, 67), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Visible = false };
            _btnGuardarMatriz.FlatAppearance.BorderSize = 0;
            _btnCancelarMatriz = new Button { Text = "Cancelar", Location = new Point(790, 38), Size = new Size(95, 26), BackColor = Color.FromArgb(120,120,120), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Visible = false };
            _btnCancelarMatriz.FlatAppearance.BorderSize = 0;
            _btnGuardarMatriz.Click += BtnGuardarMatriz_Click;
            _btnCancelarMatriz.Click += BtnCancelarMatriz_Click;
            _panelDatos.Controls.AddRange(new Control[] { lblClave, _txtClaveMatriz, lblUnidad, _txtUnidadMatriz, lblDesc, _txtDescripcionMatriz, _rbTipoApu, _rbTipoBasico, _rbTipoCuadrilla, _btnGuardarMatriz, _btnCancelarMatriz });

            _panelBotonesAgregar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(245,245,250), Enabled = false };
            var lblAg = new Label { Text = "Agregar:", Location = new Point(8,10), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(80,80,80) };
            _btnAgregarMaterial     = CrearBtn("📦 Material",    new Point(70,  5), Color.FromArgb(76,175,80));
            _btnAgregarMO           = CrearBtn("👷 M.O.",         new Point(180, 5), Color.FromArgb(33,150,243));
            _btnAgregarMaquinaria   = CrearBtn("🚜 Maquinaria",   new Point(290, 5), Color.FromArgb(255,152,0));
            _btnAgregarHerramienta  = CrearBtn("🛠️ Herramienta", new Point(400, 5), Color.FromArgb(96,125,139));
            _btnAgregarBasico       = CrearBtn("🧩 Básico",       new Point(510, 5), Color.FromArgb(156,39,176));
            _btnAgregarMaterial.Click     += BtnAgregarMaterial_Click;
            _btnAgregarMO.Click           += BtnAgregarMO_Click;
            _btnAgregarMaquinaria.Click   += BtnAgregarMaquinaria_Click;
            _btnAgregarHerramienta.Click  += BtnAgregarHerramienta_Click;
            _btnAgregarBasico.Click       += BtnAgregarBasico_Click;
            _panelBotonesAgregar.Controls.AddRange(new Control[] { lblAg, _btnAgregarMaterial, _btnAgregarMO, _btnAgregarMaquinaria, _btnAgregarHerramienta, _btnAgregarBasico });

            _dgvComponentes = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
                ColumnHeadersHeight = 34, RowTemplate = { Height = 28 }, Font = new Font("Segoe UI", 8.5F),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Color.FromArgb(225,225,235)
            };
            _dgvComponentes.EnableHeadersVisualStyles = false;
            _dgvComponentes.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55,55,85);
            _dgvComponentes.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvComponentes.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _dgvComponentes.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248,248,255);

            var right  = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
            var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
            _dgvComponentes.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "ColTipo",     HeaderText = "Tipo",       Width = 105, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColClave",    HeaderText = "C",           Width = 100, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColDesc",     HeaderText = "Descripción", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColUnidad",   HeaderText = "Unidad",      Width = 65,  DefaultCellStyle = center, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColCantidad", HeaderText = "Cantidad",    Width = 95,  DefaultCellStyle = right, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColPU",       HeaderText = "P.U.",        Width = 115, DefaultCellStyle = right, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColImporte",  HeaderText = "Importe",     Width = 115, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColId",       HeaderText = "ID",          Visible = false },
                new DataGridViewButtonColumn  { Name = "ColEliminar", HeaderText = "",            Width = 36 }
            });
            _dgvComponentes.CellClick       += DgvComponentes_CellClick;
            _dgvComponentes.CellDoubleClick += DgvComponentes_PanelCellDoubleClick;
            _dgvComponentes.CellBeginEdit   += DgvComponentes_PanelCellBeginEdit;
            _dgvComponentes.CellEndEdit     += DgvComponentes_PanelCellEndEdit;
            _dgvComponentes.CellMouseDown    += DgvComponentes_CellMouseDown;
            _dgvComponentes.MouseDown        += DgvComponentes_MouseDown;
            _dgvComponentes.CellMouseUp      += DgvComponentes_CellMouseUp;
            _dgvComponentes.MouseUp          += DgvComponentes_MouseUp;
            

            _panelTotales = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.FromArgb(235,235,248) };
            _lblMat = new Label { AutoSize = true, Location = new Point(8,   5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblMO  = new Label { AutoSize = true, Location = new Point(185, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblMaq = new Label { AutoSize = true, Location = new Point(362, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblBas = new Label { AutoSize = true, Location = new Point(539, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblDir = new Label { AutoSize = true, Location = new Point(716, 5), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(20,100,20) };
            _panelTotales.Controls.AddRange(new Control[] { _lblMat, _lblMO, _lblMaq, _lblBas, _lblDir });

            this.Controls.Add(_dgvComponentes);
            this.Controls.Add(_panelBotonesAgregar);
            this.Controls.Add(_panelDatos);
            this.Controls.Add(_panelTotales);
            this.Controls.Add(_panelHeader);
        }

        private Button CrearBtn(string texto, Point loc, Color color)
        {
            var btn = new Button
            {
                Text = texto,
                Location = loc,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ── Edición en el panel ────────────────────────────────────────
        // Obtiene el ComponenteMatriz de BD a partir del ID en la fila
        private ComponenteMatriz ObtenerComponentePorFila(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _dgvComponentes.Rows.Count) return null;
            var idCell = _dgvComponentes.Rows[rowIndex].Cells["ColId"];
            if (idCell?.Value == null) return null;
            int id = Convert.ToInt32(idCell.Value);
            return _context.ComponentesMatriz
                .Include(c => c.Material).Include(c => c.ManoDeObra)
                .Include(c => c.Maquinaria).Include(c => c.Auxiliar)
                .FirstOrDefault(c => c.Id == id);
        }

        private void DgvComponentes_PanelCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            _undoPanelRowIndex = e.RowIndex;
            _undoPanelColumnIndex = e.ColumnIndex;
            _undoPanelColumnName = _dgvComponentes.Columns[e.ColumnIndex].Name switch
            {
                "ColDesc" => "colDescripcion",
                "ColUnidad" => "colUnidad",
                "ColCantidad" => "colCantidad",
                "ColPU" => "colPU",
                _ => string.Empty
            };
            _undoPanelOldValue = _dgvComponentes.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            var comp = ObtenerComponentePorFilaTrabajo(e.RowIndex);
            if (comp == null) return;

            var columnName = _dgvComponentes.Columns[e.ColumnIndex].Name switch
            {
                "ColDesc" => "colDescripcion",
                "ColUnidad" => "colUnidad",
                "ColCantidad" => "colCantidad",
                "ColPU" => "colPU",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(columnName) && !MatrixComponentGridFlowService.CanBeginEdit(comp, columnName))
            {
                e.Cancel = true;
            }
        }

        private void DgvComponentes_PanelCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var comp = ObtenerComponentePorFilaTrabajo(e.RowIndex);
            if (comp == null) return;

            var columnName = _dgvComponentes.Columns[e.ColumnIndex].Name switch
            {
                "ColCantidad" => "colCantidad",
                _ => string.Empty
            };

            if (!MatrixComponentGridFlowService.ShouldOpenRendimientoDialog(comp, columnName))
                return;

            _dgvComponentes.CancelEdit();
            var nombre = MatrixComponentInteractionService.GetRendimientoDialogName(comp);

            using var frmRend = new Forms.FormRendimiento(nombre, comp.Cantidad);
            if (frmRend.ShowDialog() == DialogResult.OK)
            {
                var editResult = MatrixComponentEditingService.UpdateQuantityFromDialog(comp, frmRend.Cantidad);
                if (editResult.Success)
                {
                    if (_modoCreacionMatriz)
                    {
                        BeginInvoke(new Action(RefrescarVistaTemporal));
                    }
                    else
                    {
                        _context.SaveChanges();
                        BeginInvoke(new Action(GuardarYPropagar));
                    }
                }
            }
        }

        private void DgvComponentes_PanelCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var comp = ObtenerComponentePorFilaTrabajo(e.RowIndex);
            if (comp == null) return;

            var cell = _dgvComponentes.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var value = cell.Value?.ToString() ?? string.Empty;
            var columnName = _dgvComponentes.Columns[e.ColumnIndex].Name switch
            {
                "ColDesc" => "colDescripcion",
                "ColUnidad" => "colUnidad",
                "ColCantidad" => "colCantidad",
                "ColPU" => "colPU",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(columnName)) return;

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
                BeginInvoke(new Action(() =>
                {
                    if (_modoCreacionMatriz)
                        RefrescarVistaTemporal();
                    else
                        GuardarYPropagar();
                }));
                return;
            }

            var valorNuevoUndo = _dgvComponentes.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            if (!_isUndoRedo
                && e.RowIndex == _undoPanelRowIndex
                && e.ColumnIndex == _undoPanelColumnIndex
                && EsColumnaUndoPanel(_undoPanelColumnName)
                && !string.Equals(_undoPanelOldValue, valorNuevoUndo, StringComparison.Ordinal))
            {
                int rowIndex = e.RowIndex;
                int columnIndex = e.ColumnIndex;
                string oldValue = _undoPanelOldValue;
                string newValue = valorNuevoUndo;
                string descripcion = $"Editar componente ({_undoPanelColumnName})";
                _undoManager.Push(new DelegateUndoableAction(
                    descripcion,
                    () => AplicarUndoRedoPanelCelda(rowIndex, columnIndex, oldValue),
                    () => AplicarUndoRedoPanelCelda(rowIndex, columnIndex, newValue)));
            }

            _undoPanelRowIndex = -1;
            _undoPanelColumnIndex = -1;
            _undoPanelColumnName = string.Empty;
            _undoPanelOldValue = string.Empty;

            if (_modoCreacionMatriz)
            {
                BeginInvoke(new Action(RefrescarVistaTemporal));
            }
            else
            {
                _context.SaveChanges();
                BeginInvoke(new Action(GuardarYPropagar));
            }
        }
        

        private void DgvComponentes_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || _dgvComponentes == null)
                return;

            var hit = _dgvComponentes.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0)
                return;

            _filaContextualComponentes = hit.RowIndex;

            try
            {
                var clickedRow = _dgvComponentes.Rows[hit.RowIndex];
                if (!clickedRow.Selected)
                {
                    _dgvComponentes.ClearSelection();
                    clickedRow.Selected = true;
                }

                if (hit.ColumnIndex >= 0)
                    _dgvComponentes.CurrentCell = clickedRow.Cells[hit.ColumnIndex];
                else if (clickedRow.Cells.Count > 0)
                    _dgvComponentes.CurrentCell = clickedRow.Cells[0];
            }
            catch { }
        }

        private void DgvComponentes_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0)
                return;

            _filaContextualComponentes = e.RowIndex;

            try
            {
                var clickedRow = _dgvComponentes.Rows[e.RowIndex];
                if (!clickedRow.Selected)
                {
                    _dgvComponentes.ClearSelection();
                    clickedRow.Selected = true;
                }

                if (e.ColumnIndex >= 0)
                    _dgvComponentes.CurrentCell = clickedRow.Cells[e.ColumnIndex];
                else if (clickedRow.Cells.Count > 0)
                    _dgvComponentes.CurrentCell = clickedRow.Cells[0];
            }
            catch { }
        }


        private void DgvComponentes_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || _dgvComponentes == null)
                return;

            var hit = _dgvComponentes.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0)
                return;

            MostrarMenuContextualComponentesEnFila(hit.RowIndex, hit.ColumnIndex, new Point(e.X, e.Y));
        }

        private void DgvComponentes_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0)
                return;

            MostrarMenuContextualComponentesEnFila(e.RowIndex, e.ColumnIndex, _dgvComponentes.PointToClient(Cursor.Position));
        }

        private void MostrarMenuContextualComponentesEnFila(int rowIndex, int columnIndex, Point menuLocation)
        {
            if (_dgvComponentes == null || _menuContextualComponentes == null || rowIndex < 0 || rowIndex >= _dgvComponentes.Rows.Count)
                return;

            _filaContextualComponentes = rowIndex;

            try
            {
                var clickedRow = _dgvComponentes.Rows[rowIndex];
                _dgvComponentes.ClearSelection();
                clickedRow.Selected = true;

                int targetCol = columnIndex >= 0 && columnIndex < clickedRow.Cells.Count ? columnIndex : 0;
                _dgvComponentes.CurrentCell = clickedRow.Cells[targetCol];
                _dgvComponentes.Focus();
                _menuContextualComponentes.Show(_dgvComponentes, menuLocation);
            }
            catch { }
        }

        /// <summary>
        /// Calcula el factor de indirectos (igual que en FormPresupuesto)
        /// </summary>
        private decimal CalcularFactorPU()
        {
            var proyecto = _context.Proyectos.Find(_proyectoId);
            if (proyecto == null) return 1m;
            
            decimal pInd    = proyecto.PorcentajeIndirectosCentral + proyecto.PorcentajeIndirectosCampo;
            decimal pFin    = proyecto.PorcentajeFinanciamiento;
            decimal pUtil   = proyecto.PorcentajeUtilidad;
            decimal pCargos = proyecto.PorcentajeCargosAdicionales;

            if (proyecto.ModoCalculoPorcentajes == "SobreCD")
                return 1m + (pInd + pFin + pUtil + pCargos) / 100m;

            decimal f = 1m;
            f *= (1m + pInd    / 100m);
            f *= (1m + pFin    / 100m);
            f *= (1m + pUtil   / 100m);
            f *= (1m + pCargos / 100m);
            return f;
        }
        
        /// <summary>
        /// Maneja el evento de cambio de configuración de decimales
        /// </summary>
        private void OnConfiguracionCambiada(object sender, EventArgs e)
        {
            // Recargar la fila actual con los nuevos formatos
            if (_dgvPresupuesto?.CurrentRow != null)
            {
                NotificarFilaCambiada(_dgvPresupuesto.CurrentRow.Index);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Desuscribirse del evento
                FormatoHelper.ConfiguracionCambiada -= OnConfiguracionCambiada;
            }
            base.Dispose(disposing);
        }
    }
}
