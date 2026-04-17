using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    public partial class FormIndirectos : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private ConfiguracionIndirectos _configuracion;
        private readonly UndoManager _undoManager = new();
        private bool _isUndoRedo;
        private DataGridView? _undoGrid;
        private int _undoRowIndex = -1;
        private int _undoColumnIndex = -1;
        private string _undoOldValue = string.Empty;
        private string _undoTextBoxOldValue = string.Empty;
        private TextBox? _undoTextBox;

        /// <summary>
        /// Evento que se dispara cuando se transfieren porcentajes al proyecto.
        /// </summary>
        public static event EventHandler IndirectosTransferidos;


        // ── IGridFormato ──────────────────────────────────────────────────────
        // Ambos grids comparten el mismo esquema de columnas.
        // GridPrincipal apunta a dgvOficinaCentral; el formato se aplica a ambos.
        public System.Windows.Forms.DataGridView GridPrincipal => dgvOficinaCentral;
        public System.Windows.Forms.DataGridView GridBusqueda => dgvOficinaCentral;

        public bool GenerarReporteExcel()
        {
            btnExportar_Click(this, EventArgs.Empty);
            return true;
        }
        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaIndirectos _columnaIndRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _columnaIndRibbon = null;
            _columnaRibbon = null;
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            // Mapear índice visual a NombreInterno
            var mapaIdx = new[] { "Grupo", "ImporteMensual", "Duracion", "ImporteTotal" };
            if (colIndex >= 0 && colIndex < mapaIdx.Length)
            {
                var ni = mapaIdx[colIndex];
                var col = cols.FirstOrDefault(c => c.NombreInterno == ni);
                if (col != null)
                {
                    _columnaIndRibbon = col;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = col.Nombre,
                        NombreFuente = col.NombreFuente,
                        TamanoFuente = col.TamanoFuente,
                        Negrita = col.Negrita,
                        Cursiva = col.Cursiva,
                        Alineacion = col.Alineacion,
                        ColorFondo = col.ColorFondo,
                        ColorFuente = col.ColorFuente,
                        WrapTexto = col.WrapTexto,
                        AlineacionVertical = col.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_columnaIndRibbon == null) return;
            CopiarFormato(fmt, _columnaIndRibbon);
            _context.SaveChanges();
            AplicarEstilosDesdeDB();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            foreach (var col in cols)
            {
                CopiarFormato(fmt, col, excluirFondo: true);
            }
            _context.SaveChanges();
            AplicarEstilosDesdeDB();
        }

        private void CopiarFormato(ColumnaPersonalizada fmt, ColumnaIndirectos dest,
                                    bool excluirFondo = false)
        {
            dest.NombreFuente = fmt.NombreFuente;
            dest.TamanoFuente = fmt.TamanoFuente;
            dest.Negrita = fmt.Negrita;
            dest.Cursiva = fmt.Cursiva;
            dest.Alineacion = fmt.Alineacion;
            dest.ColorFuente = fmt.ColorFuente;
            if (!excluirFondo) dest.ColorFondo = fmt.ColorFondo;
            dest.WrapTexto = fmt.WrapTexto;
            dest.AlineacionVertical = fmt.AlineacionVertical;
            dest.FechaModificacion = DateTime.Now;
        }

        private void AplicarEstilosDesdeDB()
        {
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            var mapaNames = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Grupo",          "colGrupo"          },
                { "ImporteMensual", "colImporteMensual"  },
                { "Duracion",       "colDuracion"        },
                { "ImporteTotal",   "colImporteTotal"    },
            };
            foreach (var col in cols)
            {
                if (!mapaNames.TryGetValue(col.NombreInterno, out string colName)) continue;
                AplicarEstiloAGrid(dgvOficinaCentral, colName, col);
                AplicarEstiloAGrid(dgvCampo, colName, col);
            }
            dgvOficinaCentral.Invalidate();
            dgvCampo.Invalidate();
        }

        private static void AplicarEstiloAGrid(DataGridView dgv, string colName,
                                                ColumnaIndirectos col)
        {
            if (!dgv.Columns.Contains(colName)) return;
            var dgvCol = dgv.Columns[colName] as System.Windows.Forms.DataGridViewTextBoxColumn;
            if (dgvCol == null) return;
            try
            {
                FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                dgvCol.DefaultCellStyle.Font = new Font(col.NombreFuente ?? "Segoe UI", col.TamanoFuente > 0 ? col.TamanoFuente : 9f, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColorInd(col.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColorInd(col.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                dgvCol.Width = col.AnchoColumna;
                dgvCol.Tag = col;
            }
            catch { }
        }

        private static Color TryColorInd(string hex, Color fallback)
        {
            try { return System.Drawing.ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && TryUndoIndirectos())
                return true;

            if (keyData == (Keys.Control | Keys.Y) && TryRedoIndirectos())
                return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        public FormIndirectos(SOPROContext context, Proyecto proyecto)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyecto = proyecto ?? throw new ArgumentNullException(nameof(proyecto));

            // Establecer proyecto para formateo
            FormatoHelper.EstablecerProyecto(_proyecto);

            InitializeComponent();
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.Indirectos).Attach();

            // Aplicar estilo consistente
            dgvCampo.AplicarEstiloSOPRO();
            dgvOficinaCentral.AplicarEstiloSOPRO();

            ConfigurarGrids();
        }

        private void FormIndirectos_Load(object sender, EventArgs e)
        {
            lblProyecto.Text = $"📁 {_proyecto.Nombre}";
            CargarDatos();
        }

        private void ConfigurarGrids()
        {
            ConfigurarGrid(dgvOficinaCentral);
            ConfigurarGrid(dgvCampo);
            // Ribbon: click en encabezado dispara notificación
            dgvOficinaCentral.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvCampo.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvOficinaCentral.CellBeginEdit += Dgv_CellBeginEdit;
            dgvCampo.CellBeginEdit += Dgv_CellBeginEdit;
            txtVolumenAnual.Enter += ConfigTextBox_Enter;
            txtCostoDirecto.Enter += ConfigTextBox_Enter;
            txtVolumenAnual.Leave += ConfigTextBox_Leave;
            txtCostoDirecto.Leave += ConfigTextBox_Leave;
            // Aplicar formato guardado en BD
            AplicarEstilosDesdeDB();
        }

        private void ConfigurarGrid(DataGridView dgv)
        {
            dgv.AutoGenerateColumns = false;
            dgv.AllowUserToAddRows = false;
            dgv.RowHeadersVisible = false;

            // Columna Grupo (solo lectura, en negrita)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colGrupo",
                HeaderText = "GRUPO / CONCEPTO",
                Width = 400,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // Columna Importe Mensual (editable)
            var colImporte = new DataGridViewTextBoxColumn
            {
                Name = "colImporteMensual",
                HeaderText = "IMPORTE MENSUAL $",
                Width = 180,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N2"
                }
            };
            dgv.Columns.Add(colImporte);

            // Columna Duración (editable, solo para conceptos de campo)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDuracion",
                HeaderText = "DURACIÓN (MESES)",
                Width = 150,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            // Columna Importe Total (calculado, solo lectura)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colImporteTotal",
                HeaderText = "IMPORTE TOTAL $",
                Width = 180,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N2",
                    BackColor = Color.FromArgb(240, 240, 240)
                }
            });

            // Columna oculta para el ID
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                Visible = false
            });

            // Columna oculta para el tipo (Grupo o Concepto)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTipo",
                Visible = false
            });
        }

        private void CargarDatos()
        {
            // Cargar configuración
            _configuracion = _context.ConfiguracionesIndirectos
                .FirstOrDefault(c => c.ProyectoId == _proyecto.Id);

            if (_configuracion == null)
            {
                _configuracion = new ConfiguracionIndirectos
                {
                    ProyectoId = _proyecto.Id
                };
                _context.ConfiguracionesIndirectos.Add(_configuracion);
                _context.SaveChanges();
            }

            // Calcular duración en meses desde días del proyecto
            int duracionMeses = _proyecto.PlazoEjecucion > 0
                ? (int)Math.Ceiling(_proyecto.PlazoEjecucion / 30.0)
                : 1;

            // Actualizar duración de TODOS los conceptos si es diferente
            var conceptosActualizar = _context.ConceptosIndirectos
                .Include(c => c.GrupoIndirecto)
                .Where(c => c.GrupoIndirecto.ProyectoId == _proyecto.Id && c.DuracionMeses != duracionMeses)
                .ToList();

            if (conceptosActualizar.Any())
            {
                foreach (var concepto in conceptosActualizar)
                {
                    concepto.DuracionMeses = duracionMeses;
                }
                _context.SaveChanges();
            }

            // Recalcular costo directo siempre (aplica precisión de pantalla actual)
            {
                var cd = new SOPRO.Application.Services.MotorCalculoSopro(_proyecto).SumarCostoDirecto(
                    _context.ConceptosPresupuesto
                        .Where(c => c.ProyectoId == _proyecto.Id)
                        .AsNoTracking()
                        .AsEnumerable());

                if (cd > 0)
                {
                    _configuracion.CostoDirectoObra = cd;
                    _context.SaveChanges();
                }
            }

            // Mostrar configuración
            txtVolumenAnual.Text = _configuracion.VolumenAnualObra.ToStringCantidad();
            txtCostoDirecto.Text = _configuracion.CostoDirectoObra.ToStringCantidad();

            // Cargar grupos
            CargarGrupos();
            ActualizarResumen();
        }

        private void CargarGrupos()
        {
            var gruposOC = _context.GruposIndirectos
                .Include(g => g.Conceptos)
                .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.OficinaCentral)
                .OrderBy(g => g.Orden)
                .ToList();

            var gruposCampo = _context.GruposIndirectos
                .Include(g => g.Conceptos)
                .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.Campo)
                .OrderBy(g => g.Orden)
                .ToList();

            LlenarGrid(dgvOficinaCentral, gruposOC, showDuracion: false);
            LlenarGrid(dgvCampo, gruposCampo, showDuracion: false);
        }

        private void LlenarGrid(DataGridView dgv, System.Collections.Generic.List<GrupoIndirecto> grupos, bool showDuracion)
        {
            dgv.Rows.Clear();

            foreach (var grupo in grupos)
            {
                // Fila del grupo (header)
                var rowGrupo = dgv.Rows.Add();
                dgv.Rows[rowGrupo].Cells["colGrupo"].Value = grupo.Nombre;
                dgv.Rows[rowGrupo].Cells["colImporteMensual"].Value = DBNull.Value;
                dgv.Rows[rowGrupo].Cells["colDuracion"].Value = DBNull.Value;
                dgv.Rows[rowGrupo].Cells["colImporteTotal"].Value = grupo.Total;
                dgv.Rows[rowGrupo].Cells["colId"].Value = grupo.Id;
                dgv.Rows[rowGrupo].Cells["colTipo"].Value = "GRUPO";
                dgv.Rows[rowGrupo].DefaultCellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                dgv.Rows[rowGrupo].DefaultCellStyle.BackColor = Color.FromArgb(230, 230, 230);
                dgv.Rows[rowGrupo].ReadOnly = true;

                // Filas de conceptos
                foreach (var concepto in grupo.Conceptos.OrderBy(c => c.Orden))
                {
                    var rowConcepto = dgv.Rows.Add();
                    dgv.Rows[rowConcepto].Cells["colGrupo"].Value = "    " + concepto.Concepto;
                    dgv.Rows[rowConcepto].Cells["colImporteMensual"].Value = concepto.ImporteMensual;
                    dgv.Rows[rowConcepto].Cells["colDuracion"].Value = showDuracion ? concepto.DuracionMeses : (object)DBNull.Value;
                    dgv.Rows[rowConcepto].Cells["colImporteTotal"].Value = concepto.ImporteTotal;
                    dgv.Rows[rowConcepto].Cells["colId"].Value = concepto.Id;
                    dgv.Rows[rowConcepto].Cells["colTipo"].Value = "CONCEPTO";
                }
            }

            // Ocultar columna duración si no aplica
            dgv.Columns["colDuracion"].Visible = showDuracion;
        }

        private static bool EsColumnaUndoIndirectos(string columnName)
        {
            return string.Equals(columnName, "colImporteMensual", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "colDuracion", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryUndoIndirectos()
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

        private bool TryRedoIndirectos()
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

        private void AplicarUndoRedoIndirectosCelda(DataGridView dgv, int rowIndex, int columnIndex, string value)
        {
            if (dgv == null || rowIndex < 0 || rowIndex >= dgv.Rows.Count)
                return;
            if (columnIndex < 0 || columnIndex >= dgv.Columns.Count)
                return;

            var cell = dgv.Rows[rowIndex].Cells[columnIndex];
            dgv.CurrentCell = cell;
            cell.Value = value;
            Dgv_CellEndEdit(dgv, new DataGridViewCellEventArgs(columnIndex, rowIndex));
            dgv.Refresh();
        }

        private void AplicarUndoRedoConfigTextBox(TextBox textBox, string value)
        {
            if (textBox == null)
                return;

            textBox.Text = value ?? string.Empty;
            textBox.Focus();
            textBox.SelectionStart = textBox.TextLength;
        }

        private void ConfigTextBox_Enter(object? sender, EventArgs e)
        {
            if (sender is TextBox textBox)
            {
                _undoTextBox = textBox;
                _undoTextBoxOldValue = textBox.Text ?? string.Empty;
            }
        }

        private void ConfigTextBox_Leave(object? sender, EventArgs e)
        {
            if (_isUndoRedo)
                return;

            if (sender is not TextBox textBox)
                return;

            var newValue = textBox.Text ?? string.Empty;
            var oldValue = _undoTextBox == textBox ? _undoTextBoxOldValue : string.Empty;
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                return;

            string descripcion = textBox == txtVolumenAnual
                ? "Editar volumen anual"
                : "Editar costo directo";

            _undoManager.Push(new DelegateUndoableAction(
                descripcion,
                () => AplicarUndoRedoConfigTextBox(textBox, oldValue),
                () => AplicarUndoRedoConfigTextBox(textBox, newValue)));
        }

        private void Dgv_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (sender is not DataGridView dgv)
                return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            _undoGrid = dgv;
            _undoRowIndex = e.RowIndex;
            _undoColumnIndex = e.ColumnIndex;
            _undoOldValue = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null || e.RowIndex < 0) return;

            var tipo = dgv.Rows[e.RowIndex].Cells["colTipo"].Value?.ToString();

            if (tipo == "GRUPO")
            {
                e.CellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                e.CellStyle.BackColor = Color.FromArgb(230, 230, 230);
            }
        }

        private void Dgv_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            var dgv = sender as DataGridView;
            if (dgv == null) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var tipo = dgv.Rows[e.RowIndex].Cells["colTipo"].Value?.ToString();
            if (tipo != "CONCEPTO") return;

            int conceptoId = Convert.ToInt32(dgv.Rows[e.RowIndex].Cells["colId"].Value);
            var concepto = _context.ConceptosIndirectos.Find(conceptoId);

            if (concepto == null) return;

            string valorNuevoUndo = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;

            try
            {
                // Actualizar importe mensual
                if (e.ColumnIndex == dgv.Columns["colImporteMensual"].Index)
                {
                    if (decimal.TryParse(dgv.Rows[e.RowIndex].Cells["colImporteMensual"].Value?.ToString(), out decimal importe))
                    {
                        concepto.ImporteMensual = importe;
                    }
                }

                // Actualizar duración
                if (e.ColumnIndex == dgv.Columns["colDuracion"].Index && dgv.Columns["colDuracion"].Visible)
                {
                    if (int.TryParse(dgv.Rows[e.RowIndex].Cells["colDuracion"].Value?.ToString(), out int duracion))
                    {
                        concepto.DuracionMeses = duracion;
                    }
                }

                _context.SaveChanges();

                // Actualizar solo esta celda de importe total (sin recargar todo)
                dgv.Rows[e.RowIndex].Cells["colImporteTotal"].Value = concepto.ImporteTotal;

                // Buscar y actualizar el total del grupo padre (sin recargar)
                var grupo = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .FirstOrDefault(g => g.Id == concepto.GrupoIndirectoId);

                if (grupo != null)
                {
                    // Buscar la fila del grupo en el grid
                    for (int i = e.RowIndex - 1; i >= 0; i--)
                    {
                        if (dgv.Rows[i].Cells["colTipo"].Value?.ToString() == "GRUPO")
                        {
                            dgv.Rows[i].Cells["colImporteTotal"].Value = grupo.Total;
                            break;
                        }
                    }
                }

                // Actualizar resumen sin recargar
                ActualizarResumen();

                if (!_isUndoRedo
                    && ReferenceEquals(_undoGrid, dgv)
                    && _undoRowIndex == e.RowIndex
                    && _undoColumnIndex == e.ColumnIndex
                    && EsColumnaUndoIndirectos(dgv.Columns[e.ColumnIndex].Name)
                    && !string.Equals(_undoOldValue, valorNuevoUndo, StringComparison.Ordinal))
                {
                    int rowIndex = e.RowIndex;
                    int columnIndex = e.ColumnIndex;
                    string oldValue = _undoOldValue;
                    string newValue = valorNuevoUndo;
                    _undoManager.Push(new DelegateUndoableAction(
                        $"Editar {dgv.Columns[e.ColumnIndex].HeaderText} en indirectos",
                        () => AplicarUndoRedoIndirectosCelda(dgv, rowIndex, columnIndex, oldValue),
                        () => AplicarUndoRedoIndirectosCelda(dgv, rowIndex, columnIndex, newValue)));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _undoGrid = null;
                _undoRowIndex = -1;
                _undoColumnIndex = -1;
                _undoOldValue = string.Empty;
            }
        }

        private void btnCrearPredeterminados_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "¿Crear la estructura predeterminada de indirectos según normativa mexicana?",
                "Confirmar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                try
                {
                    IndirectCostStructureService.CrearEstructuraPredeterminada(_context, _proyecto.Id);
                    CargarDatos();
                    MessageBox.Show("Estructura predeterminada creada correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnCalcular_Click(object sender, EventArgs e)
        {
            try
            {
                // Leer valores de configuración
                if (!decimal.TryParse(txtVolumenAnual.Text, out decimal volumenAnual))
                {
                    MessageBox.Show("Ingresa un volumen anual válido.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!decimal.TryParse(txtCostoDirecto.Text, out decimal costoDirecto))
                {
                    MessageBox.Show("Ingresa un costo directo válido.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Calcular totales - IMPORTANTE: ToList() primero para calcular en memoria
                var gruposOC = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.OficinaCentral)
                    .ToList(); // ← Traer a memoria primero

                var totalOC = gruposOC.Sum(g => g.Total); // ← Ahora sí calcular

                var gruposCampo = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.Campo)
                    .ToList(); // ← Traer a memoria primero

                var totalCampo = gruposCampo.Sum(g => g.Total); // ← Ahora sí calcular

                // Calcular porcentajes
                decimal porcOC = volumenAnual > 0 ? (totalOC / volumenAnual) * 100 : 0;
                decimal porcCampo = costoDirecto > 0 ? (totalCampo / costoDirecto) * 100 : 0;

                // Actualizar configuración
                _configuracion.VolumenAnualObra = volumenAnual;
                _configuracion.CostoDirectoObra = costoDirecto;
                _configuracion.TotalOficinaCentralAnual = totalOC;
                _configuracion.TotalCampo = totalCampo;
                _configuracion.PorcentajeOficinaCentral = porcOC;
                _configuracion.PorcentajeCampo = porcCampo;
                _configuracion.FechaActualizacion = DateTime.Now;

                _context.SaveChanges();
                ActualizarResumen();

                tabControl.SelectedTab = tabConfiguracion;
                MessageBox.Show("Porcentajes calculados correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al calcular: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ActualizarResumen()
        {
            lblTotalOficinaCentral.Text = $"Total Oficina Central Anual: ${_configuracion.TotalOficinaCentralAnual:N2}";
            lblTotalCampo.Text = $"Total Campo: ${_configuracion.TotalCampo:N2}";
            lblPorcentajeOC.Text = $"% Oficina Central: {_configuracion.PorcentajeOficinaCentral:N4}%";
            lblPorcentajeCampo.Text = $"% Campo: {_configuracion.PorcentajeCampo:N4}%";
            lblPorcentajeTotal.Text = $"% TOTAL INDIRECTOS: {_configuracion.PorcentajeTotal:N4}%";

            lblStatus.Text = $"Última actualización: {_configuracion.FechaActualizacion:dd/MMM/yyyy HH:mm}";
        }

        private void btnTransferir_Click(object sender, EventArgs e)
        {
            if (_configuracion.PorcentajeTotal == 0)
            {
                var r = MessageBox.Show(
                    "Los porcentajes calculados son 0%.\n¿Deseas transferir de todas formas?",
                    "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            try
            {
                // Guardar en Proyecto
                var proy = _context.Proyectos.Find(_proyecto.Id);
                if (proy == null) return;

                proy.PorcentajeIndirectosCentral = _configuracion.PorcentajeOficinaCentral;
                proy.PorcentajeIndirectosCampo = _configuracion.PorcentajeCampo;
                _context.SaveChanges();

                // Actualizar en memoria
                _proyecto.PorcentajeIndirectosCentral = _configuracion.PorcentajeOficinaCentral;
                _proyecto.PorcentajeIndirectosCampo = _configuracion.PorcentajeCampo;

                MessageBox.Show(
                    $"Transferido al proyecto:\n\n" +
                    $"  Oficina Central: {_configuracion.PorcentajeOficinaCentral:N4}%\n" +
                    $"  Campo:           {_configuracion.PorcentajeCampo:N4}%\n" +
                    $"  TOTAL:           {_configuracion.PorcentajeTotal:N4}%\n\n" +
                    "Usa el módulo de Porcentajes para asignar\n" +
                    "Financiamiento y Utilidad.",
                    "Transferido ✓", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Disparar evento para notificar a FormPresupuesto
                IndirectosTransferidos?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAgregarGrupo_Click(object sender, EventArgs e)
        {
            bool esCampo = tabControl.SelectedTab == tabCampo;
            var tipo = esCampo ? TipoIndirecto.Campo : TipoIndirecto.OficinaCentral;

            string nombre = PedirTexto("Nombre del nuevo grupo:");
            if (string.IsNullOrWhiteSpace(nombre)) return;

            int maxOrden = _context.GruposIndirectos
                .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == tipo)
                .Select(g => (int?)g.Orden).Max() ?? 0;

            var grupo = new GrupoIndirecto
            {
                ProyectoId = _proyecto.Id,
                Nombre = nombre.Trim().ToUpper(),
                Tipo = tipo,
                Orden = maxOrden + 1
            };
            _context.GruposIndirectos.Add(grupo);
            _context.SaveChanges();
            CargarGrupos();
        }

        private void btnAgregarConcepto_Click(object sender, EventArgs e)
        {
            bool esCampo = tabControl.SelectedTab == tabCampo;
            var dgv = esCampo ? dgvCampo : dgvOficinaCentral;
            var tipo = esCampo ? TipoIndirecto.Campo : TipoIndirecto.OficinaCentral;

            if (dgv.CurrentRow == null)
            {
                MessageBox.Show("Selecciona primero una fila dentro del grupo.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Encontrar el grupo padre de la fila seleccionada
            int grupoId = -1;
            for (int i = dgv.CurrentRow.Index; i >= 0; i--)
            {
                if (dgv.Rows[i].Cells["colTipo"].Value?.ToString() == "GRUPO")
                {
                    grupoId = Convert.ToInt32(dgv.Rows[i].Cells["colId"].Value);
                    break;
                }
            }
            if (grupoId < 0)
            {
                MessageBox.Show("No se encontró un grupo padre. Selecciona una fila dentro de un grupo.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string nombre = PedirTexto("Nombre del concepto:");
            if (string.IsNullOrWhiteSpace(nombre)) return;

            int maxOrden = _context.ConceptosIndirectos
                .Where(c => c.GrupoIndirectoId == grupoId)
                .Select(c => (int?)c.Orden).Max() ?? 0;

            // Calcular duración en meses desde días del proyecto
            int duracionMeses = _proyecto.PlazoEjecucion > 0
                ? (int)Math.Ceiling(_proyecto.PlazoEjecucion / 30.0)
                : 1;

            var concepto = new ConceptoIndirecto
            {
                GrupoIndirectoId = grupoId,
                Concepto = nombre.Trim(),
                Tipo = tipo,
                ImporteMensual = 0,
                DuracionMeses = duracionMeses,
                Orden = maxOrden + 1
            };
            _context.ConceptosIndirectos.Add(concepto);
            _context.SaveChanges();
            CargarGrupos();
        }

        private void btnEliminarFila_Click(object sender, EventArgs e)
        {
            bool esCampo = tabControl.SelectedTab == tabCampo;
            var dgv = esCampo ? dgvCampo : dgvOficinaCentral;

            if (dgv.CurrentRow == null) return;
            var tipoFila = dgv.CurrentRow.Cells["colTipo"].Value?.ToString();
            int id = Convert.ToInt32(dgv.CurrentRow.Cells["colId"].Value);

            string msg = tipoFila == "GRUPO"
                ? "¿Eliminar este grupo y TODOS sus conceptos?"
                : "¿Eliminar este concepto?";

            if (MessageBox.Show(msg, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                if (tipoFila == "GRUPO")
                {
                    var g = _context.GruposIndirectos
                        .Include(x => x.Conceptos)
                        .FirstOrDefault(x => x.Id == id);
                    if (g != null)
                    {
                        _context.ConceptosIndirectos.RemoveRange(g.Conceptos);
                        _context.GruposIndirectos.Remove(g);
                    }
                }
                else
                {
                    var c = _context.ConceptosIndirectos.Find(id);
                    if (c != null) _context.ConceptosIndirectos.Remove(c);
                }
                _context.SaveChanges();
                CargarGrupos();
                ActualizarResumen();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Toma el Costo Directo total del presupuesto activo y lo pone en el campo CD.
        /// </summary>
        private void btnAutoCD_Click(object sender, EventArgs e)
        {
            var cd = new SOPRO.Application.Services.MotorCalculoSopro(_proyecto).SumarCostoDirecto(
                _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .AsNoTracking()
                    .AsEnumerable());

            if (cd == 0)
            {
                MessageBox.Show("No hay costo directo en el presupuesto todavía.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            txtCostoDirecto.Text = cd.ToStringCantidad();
            MessageBox.Show($"Costo Directo cargado: ${cd:N2}", "OK",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Abre FormPorcentajes directamente desde el módulo de Indirectos.
        /// Útil cuando ya terminaste de calcular y quieres asignar Financiamiento/Utilidad.
        /// </summary>
        private void btnPorcentajesDirectos_Click(object sender, EventArgs e)
        {
            using var form = new FormPorcentajes(_context, _proyecto);
            form.ShowDialog(this);
        }

        /// <summary>
        /// Diálogo de entrada de texto simple (reemplaza Microsoft.VisualBasic.InputBox).
        /// </summary>
        private string PedirTexto(string prompt)
        {
            using var dlg = new Form
            {
                Text = "SOPRO",
                Width = 420,
                Height = 130,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var lbl = new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true };
            var txt = new TextBox { Location = new Point(12, 32), Width = 380 };
            var btnOk = new Button
            {
                Text = "Aceptar",
                DialogResult = DialogResult.OK,
                Location = new Point(220, 60),
                Width = 80
            };
            var btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(312, 60),
                Width = 80
            };
            dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            dlg.AcceptButton = btnOk; dlg.CancelButton = btnCancel;
            return dlg.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
        }



        public void GenerarPdfIndirectos()
        {
            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnas = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);

                var gruposOC = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.OficinaCentral)
                    .OrderBy(g => g.Orden).ToList();

                var gruposCampo = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.Campo)
                    .OrderBy(g => g.Orden).ToList();

                if (!gruposOC.Any() && !gruposCampo.Any())
                {
                    MessageBox.Show("No hay datos de indirectos para exportar.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF de Cálculo de Indirectos",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Indirectos_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Indirectos, lblTitulo.Text);
                var generador = new Services.GeneradorPdfIndirectos(svc);
                string ruta = generador.Generar(_proyecto, gruposOC, gruposCampo,
                    _configuracion, plantilla, columnas, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte PDF:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnas = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);

                var gruposOC = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.OficinaCentral)
                    .OrderBy(g => g.Orden).ToList();

                var gruposCampo = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.Campo)
                    .OrderBy(g => g.Orden).ToList();

                if (!gruposOC.Any() && !gruposCampo.Any())
                {
                    MessageBox.Show("No hay datos de indirectos para exportar.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Cálculo de Indirectos",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Indirectos_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Indirectos, lblTitulo.Text);
                var generador = new Services.GeneradorExcelIndirectos(svc);
                string ruta = generador.Generar(_proyecto, gruposOC, gruposCampo,
                    _configuracion, plantilla, columnas, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show($"Reporte generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        public void RecalcularTodo()
        {
            CargarDatos();
        }
    }
}