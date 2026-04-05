using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormMatrices : Form, IGridFormato, IBusquedaGrid, IRecalculable
    {
        private readonly SOPROContext _context;
        private readonly Repository<Matriz> _repository;
        private readonly MatrixCatalogViewService _matrixCatalogViewService = new();
        private readonly MatrixUsageLookupService _matrixUsageLookupService = new();
        private readonly MatrixDeleteFlowService _matrixDeleteFlowService = new();
        private readonly int _proyectoId;
        private readonly bool _modoEmbebido;
        private readonly EventHandler _onInsumos;
        private ContextMenuStrip? _menuMatrices;

        private Matriz _matrizSeleccionada;
        private List<ColumnaMatriz> _columnasConfig = new();
        private List<Matriz> _listaActual = new();
        private List<MatrixGridRowDisplay> _rowsActuales = new();
        private bool _cargandoColumnas = false;

        public DataGridView GridPrincipal => dgvMatrices;
        public DataGridView GridBusqueda => dgvMatrices;
        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaMatriz _colMatRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public FormMatrices(SOPROContext context, int proyectoId, bool modoEmbebido = false)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);

            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Matriz>(_context);
            _proyectoId = proyectoId;
            _modoEmbebido = modoEmbebido;

            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId, ReportTitleModuleKeys.CatalogoMatrices).Attach();

            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvMatrices.AplicarEstiloSOPRO();

            _columnasConfig = ColumnasMatrizHelper.ObtenerColumnas(_context, _proyectoId);

            ConfigurarModoEmbebido();
            ConfigurarGrid();

            Load += (_, __) => CargarMatrices();
            dgvMatrices.ColumnHeaderMouseClick += (_, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvMatrices.ColumnWidthChanged += DgvMatrices_ColumnWidthChanged;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Mat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Mat;
            dgvMatrices.KeyDown += dgvMatrices_KeyDown;
            dgvMatrices.CellFormatting += DgvMatrices_CellFormatting;
            dgvMatrices.CellMouseDown += dgvMatrices_CellMouseDown;

            _onInsumos = (_, __) => CargarMatrices();
            FormCatalogoMateriales.InsumosModificados += _onInsumos;
            FormCatalogoManoObra.InsumosModificados += _onInsumos;
            FormCatalogoHerramientas.InsumosModificados += _onInsumos;
            FormCatalogoMaquinaria.InsumosModificados += _onInsumos;
            FormEditarMatriz.MatrizGuardada += _onInsumos;
        }

        public bool GenerarReporteExcel()
        {
            ExportarCatalogoMatrices();
            return true;
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMatRibbon == null) return;

            _colMatRibbon.NombreFuente = fmt.NombreFuente;
            _colMatRibbon.TamanoFuente = fmt.TamanoFuente;
            _colMatRibbon.Negrita = fmt.Negrita;
            _colMatRibbon.Cursiva = fmt.Cursiva;
            _colMatRibbon.Alineacion = fmt.Alineacion;
            _colMatRibbon.ColorFondo = fmt.ColorFondo;
            _colMatRibbon.ColorFuente = fmt.ColorFuente;
            _colMatRibbon.WrapTexto = fmt.WrapTexto;
            _colMatRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMatRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvMatrices.Columns)
            {
                if (col.Tag == _colMatRibbon && col is DataGridViewTextBoxColumn textCol)
                {
                    AplicarEstiloDesdeColMat(textCol, _colMatRibbon);
                    break;
                }
            }
            dgvMatrices.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMatrices.Columns)
            {
                if (col.Tag is not ColumnaMatriz cm || col is not DataGridViewTextBoxColumn textCol) continue;
                cm.NombreFuente = fmt.NombreFuente;
                cm.TamanoFuente = fmt.TamanoFuente;
                cm.Negrita = fmt.Negrita;
                cm.Cursiva = fmt.Cursiva;
                cm.Alineacion = fmt.Alineacion;
                cm.ColorFuente = fmt.ColorFuente;
                cm.WrapTexto = fmt.WrapTexto;
                cm.AlineacionVertical = fmt.AlineacionVertical;
                cm.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMat(textCol, cm);
            }
            _context.SaveChanges();
            dgvMatrices.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMatRibbon = null;
            _columnaRibbon = null;

            if (colIndex >= 0 && colIndex < dgvMatrices.Columns.Count)
            {
                var col = dgvMatrices.Columns[colIndex];
                if (col.Tag is ColumnaMatriz cm)
                {
                    _colMatRibbon = cm;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = cm.Nombre,
                        NombreFuente = cm.NombreFuente,
                        TamanoFuente = cm.TamanoFuente,
                        Negrita = cm.Negrita,
                        Cursiva = cm.Cursiva,
                        Alineacion = cm.Alineacion,
                        ColorFondo = cm.ColorFondo,
                        ColorFuente = cm.ColorFuente,
                        WrapTexto = cm.WrapTexto,
                        AlineacionVertical = cm.AlineacionVertical,
                    };
                }
            }

            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        private void ConfigurarModoEmbebido()
        {
            if (!_modoEmbebido) return;
            if (panelTop != null) { panelTop.Visible = false; panelTop.Height = 0; }
            if (btnCerrar != null) btnCerrar.Visible = false;
            if (dgvMatrices != null) { dgvMatrices.Dock = DockStyle.Fill; dgvMatrices.BringToFront(); }
        }

        public void RecargarMatrices() => CargarMatrices();

        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvMatrices.AutoGenerateColumns = false;
            dgvMatrices.AllowUserToAddRows = false;
            dgvMatrices.AllowUserToDeleteRows = false;
            dgvMatrices.ReadOnly = true;
            dgvMatrices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMatrices.MultiSelect = true;
            dgvMatrices.BackgroundColor = Color.White;
            dgvMatrices.BorderStyle = BorderStyle.None;
            dgvMatrices.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvMatrices.GridColor = Color.FromArgb(230, 230, 230);
            dgvMatrices.ColumnHeadersHeight = 40;
            dgvMatrices.RowTemplate.Height = 35;
            dgvMatrices.EnableHeadersVisualStyles = false;
            dgvMatrices.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvMatrices.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMatrices.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvMatrices.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMatrices.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvMatrices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvMatrices.ScrollBars = ScrollBars.Both;

            DgvCeldaHelper.Aplicar(dgvMatrices, conMenuCopia: false);

            dgvMatrices.Columns.Clear();
            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    string dataProperty = cfg.NombreInterno switch
                    {
                        "Tipo" => nameof(MatrixGridRowDisplay.TipoTexto),
                        "NumInsumos" => nameof(MatrixGridRowDisplay.NumInsumos),
                        _ => cfg.NombreInterno,
                    };

                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = dataProperty,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvMatrices.Columns.Add(col);
                    AplicarEstiloDesdeColMat(col, cfg);
                }
            }
            else
            {
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = nameof(MatrixGridRowDisplay.Clave), Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = nameof(MatrixGridRowDisplay.Descripcion), Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = nameof(MatrixGridRowDisplay.Unidad), Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTipo", HeaderText = "Tipo", DataPropertyName = nameof(MatrixGridRowDisplay.TipoTexto), Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", DataPropertyName = nameof(MatrixGridRowDisplay.OrigenDetalle), Width = 180, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCostoDirecto", HeaderText = "Costo Directo", DataPropertyName = nameof(MatrixGridRowDisplay.CostoDirecto), Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colComponentes", HeaderText = "# Insumos", DataPropertyName = nameof(MatrixGridRowDisplay.NumInsumos), Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            }

            dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDummy",
                HeaderText = string.Empty,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });

            _cargandoColumnas = false;
        }

        private void DgvMatrices_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaMatriz cfg) return;

                var columnaDb = _context.ColumnasMatriz.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en matrices: {ex.Message}");
            }
        }

        private static void AplicarEstiloDesdeColMat(DataGridViewTextBoxColumn dgvCol, ColumnaMatriz col)
        {
            try
            {
                FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                string fuente = !string.IsNullOrEmpty(col.NombreFuente) ? col.NombreFuente : "Segoe UI";
                float tam = col.TamanoFuente > 0 ? col.TamanoFuente : 9f;
                dgvCol.DefaultCellStyle.Font = new Font(fuente, tam, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColor(col.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColor(col.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                if (!string.IsNullOrEmpty(col.FormatoNumerico))
                    dgvCol.DefaultCellStyle.Format = col.FormatoNumerico;
            }
            catch
            {
            }
        }

        private static DataGridViewContentAlignment AlineacionADGV(AlineacionColumna alin) =>
            alin switch
            {
                AlineacionColumna.Centro => DataGridViewContentAlignment.MiddleCenter,
                AlineacionColumna.Derecha => DataGridViewContentAlignment.MiddleRight,
                AlineacionColumna.Justificado => DataGridViewContentAlignment.MiddleLeft,
                _ => DataGridViewContentAlignment.MiddleLeft,
            };

        private static Color TryColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }

        private async void CargarMatrices()
        {
            try
            {
                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando matrices...";
                var _gridState = DataGridViewStateHelper.Capture(dgvMatrices);

                TipoMatriz? tipo = null;
                if (rbSoloAPU.Checked) tipo = TipoMatriz.APU;
                else if (rbSoloBasicos.Checked) tipo = TipoMatriz.Basico;
                else if (rbSoloCuadrillas.Checked) tipo = TipoMatriz.Cuadrilla;

                var result = await _matrixCatalogViewService.LoadAsync(_context, new MatrixCatalogFilterInput
                {
                    ProyectoId = _proyectoId,
                    SearchText = txtBuscar.Text,
                    Tipo = tipo
                });

                _rowsActuales = result.Rows;
                _listaActual = _rowsActuales.Select(r => r.Source).ToList();

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvMatrices))
                {
                    dgvMatrices.DataSource = null;
                    dgvMatrices.DataSource = _rowsActuales;
                    DataGridViewStateHelper.Restore(dgvMatrices, _gridState);
                }
                ResumeLayout();

                lblStatus.Text = result.StatusText;
                ActualizarEstadoBotones();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar matrices:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar matrices";
            }
            finally
            {
                btnRefrescar.Enabled = true;
            }
        }

        private void btnConfigReporte_Click(object sender, EventArgs e)
        {
            using var form = new FormColumnasAPU(_context, _proyectoId, FormColumnasAPU.ModoColumnas.Matrices);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasMatrizHelper.ObtenerColumnas(_context, _proyectoId);
                ConfigurarGrid();
                CargarMatrices();
            }
        }

        private async Task EliminarMatricesSeleccionadasAsync()
        {
            var seleccionadas = dgvMatrices.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .Select(r => (r.DataBoundItem as MatrixGridRowDisplay)?.Source)
                .Where(x => x != null)
                .DistinctBy(x => x!.Id)
                .Cast<Matriz>()
                .ToList();

            if (!seleccionadas.Any())
            {
                if (_matrizSeleccionada == null)
                {
                    MessageBox.Show("Seleccione una o más matrices para eliminar.", "Selección Requerida",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                seleccionadas.Add(_matrizSeleccionada);
            }

            string mensaje = seleccionadas.Count == 1
                ? _matrixDeleteFlowService.BuildPreview(seleccionadas[0]).ConfirmationMessage
                : $"¿Desea eliminar las {seleccionadas.Count} matrices seleccionadas?\n\nEsta acción no se puede deshacer.";

            if (MessageBox.Show(mensaje, "Confirmar Eliminación",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                foreach (var matriz in seleccionadas)
                    await _repository.DeleteAsync(matriz);

                await _repository.SaveChangesAsync();
                MessageBox.Show(seleccionadas.Count == 1 ? "Matriz eliminada exitosamente." : "Matrices eliminadas exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMatrices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar la(s) matriz(ces):\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int DecimalesImporte => _context.Proyectos.Find(_proyectoId)?.DecimalesImporte ?? 2;

        private void DgvMatrices_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Value == null) return;
            var col = dgvMatrices.Columns[e.ColumnIndex].Name;
            if ((col == "colCostoDirecto" || col == "col_CostoDirecto") && e.Value is decimal v)
            {
                e.Value = v.ToString($"C{DecimalesImporte}",
                    System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private void dgvMatrices_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Insert && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnNuevo_Click(sender, EventArgs.Empty);
                return;
            }

            if (e.Control && e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                _ = EliminarMatricesSeleccionadasAsync();
                return;
            }

            if (e.KeyCode == Keys.Enter && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (btnEditar.Enabled)
                    btnEditar_Click(sender, EventArgs.Empty);
            }
        }

        private void dgvMatrices_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvMatrices.Rows[e.RowIndex];
            if (!clickedRow.Selected)
            {
                dgvMatrices.ClearSelection();
                clickedRow.Selected = true;
            }

            if (e.ColumnIndex >= 0)
                dgvMatrices.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _matrizSeleccionada = (clickedRow.DataBoundItem as MatrixGridRowDisplay)?.Source;
            ActualizarEstadoBotones();

            _menuMatrices?.Dispose();
            _menuMatrices = new ContextMenuStrip();
            int n = dgvMatrices.SelectedRows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow);
            var itemEditar = _menuMatrices.Items.Add("✏️  Editar");
            itemEditar.Enabled = n == 1 && _matrizSeleccionada != null;
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var itemEliminar = _menuMatrices.Items.Add(n > 1 ? $"🗑️  Eliminar {n} matrices" : "🗑️  Eliminar");
            itemEliminar.Enabled = _matrizSeleccionada != null;
            itemEliminar.Click += async (_, __) => await EliminarMatricesSeleccionadasAsync();

            _menuMatrices.Show(Cursor.Position);
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId);
            if (form.ShowDialog() == DialogResult.OK)
            {
                CargarMatrices();
                PropagrarCambiosAlPresupuesto();
            }
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_matrizSeleccionada == null)
            {
                MessageBox.Show("Seleccione una matriz para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormEditarMatriz(_context, _proyectoId, _matrizSeleccionada);
            if (form.ShowDialog() == DialogResult.OK)
            {
                CargarMatrices();
                PropagrarCambiosAlPresupuesto();
            }
        }

        private void PropagrarCambiosAlPresupuesto()
        {
            OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_matrizSeleccionada == null)
            {
                MessageBox.Show("Seleccione una matriz para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var preview = _matrixDeleteFlowService.BuildPreview(_matrizSeleccionada);
            if (MessageBox.Show(preview.ConfirmationMessage, "Confirmar Eliminación",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                await _repository.DeleteAsync(_matrizSeleccionada);
                await _repository.SaveChangesAsync();
                MessageBox.Show("Matriz eliminada exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMatrices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar la matriz:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCopiar_Click(object sender, EventArgs e)
        {
            if (_matrizSeleccionada == null)
            {
                MessageBox.Show("Seleccione una matriz para copiar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show("Función de copiar matriz próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnRefrescar_Click(object sender, EventArgs e) => CargarMatrices();
        private void btnCerrar_Click(object sender, EventArgs e) => Close();
        private void btnExportarExcel_Click(object sender, EventArgs e) => ExportarCatalogoMatrices();

        public void GenerarPdfCatalogoMatrices()
        {
            if (_listaActual == null || !_listaActual.Any())
            {
                MessageBox.Show("No hay matrices en la vista actual para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string filtroTitulo = "Todos";
            if (rbSoloAPU.Checked) filtroTitulo = "APU";
            else if (rbSoloBasicos.Checked) filtroTitulo = "Básicos";
            else if (rbSoloCuadrillas.Checked) filtroTitulo = "Cuadrillas";

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar catálogo de matrices en PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"CatalogoMatrices_{filtroTitulo}_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var proyecto = _context.Proyectos.Find(_proyectoId) ?? new Proyecto { Nombre = "Proyecto" };
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId, ReportTitleModuleKeys.CatalogoMatrices, lblTitulo.Text);
                var generador = new GeneradorPdfCatalogoMatrices(svcRep, _context);

                generador.Generar(proyecto, _listaActual, plantilla, filtroTitulo, dlg.FileName, tituloCfg);

                if (MessageBox.Show(
                    $"Catálogo PDF generado con {_listaActual.Count} matrices.\n\n¿Desea abrir el archivo?",
                    "PDF generado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF del catálogo:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Función de importación desde Excel próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void dgvMatrices_SelectionChanged(object sender, EventArgs e)
        {
            _matrizSeleccionada = dgvMatrices.SelectedRows.Count > 0
                ? (dgvMatrices.SelectedRows[0].DataBoundItem as MatrixGridRowDisplay)?.Source
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvMatrices_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                btnEditar_Click(sender, e);
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarMatrices();
        private void rbTodos_CheckedChanged(object sender, EventArgs e) { if (rbTodos.Checked) CargarMatrices(); }
        private void rbSoloAPU_CheckedChanged(object sender, EventArgs e) { if (rbSoloAPU.Checked) CargarMatrices(); }
        private void rbSoloBasicos_CheckedChanged(object sender, EventArgs e) { if (rbSoloBasicos.Checked) CargarMatrices(); }
        private void rbSoloCuadrillas_CheckedChanged(object sender, EventArgs e) { if (rbSoloCuadrillas.Checked) CargarMatrices(); }

        private void ActualizarEstadoBotones()
        {
            bool hay = _matrizSeleccionada != null;
            btnEditar.Enabled = hay;
            btnEliminar.Enabled = hay;
            btnCopiar.Enabled = hay;
        }

        private void ExportarCatalogoMatrices()
        {
            if (_listaActual == null || !_listaActual.Any())
            {
                MessageBox.Show("No hay matrices en la vista actual para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string filtroTitulo = "Todos";
            if (rbSoloAPU.Checked) filtroTitulo = "APU";
            else if (rbSoloBasicos.Checked) filtroTitulo = "Básicos";
            else if (rbSoloCuadrillas.Checked) filtroTitulo = "Cuadrillas";

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar catálogo de matrices",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"CatalogoMatrices_{filtroTitulo}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var proyecto = _context.Proyectos.Find(_proyectoId) ?? new Proyecto { Nombre = "Proyecto" };
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId, ReportTitleModuleKeys.CatalogoMatrices, lblTitulo.Text);
                var generador = new GeneradorExcelCatalogoMatrices(svcRep, _context);

                generador.Generar(proyecto, _listaActual, plantilla, filtroTitulo, dlg.FileName, tituloCfg);

                if (MessageBox.Show(
                    $"Catálogo generado con {_listaActual.Count} matrices.\n\n¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el catálogo:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            FormCatalogoMateriales.InsumosModificados -= _onInsumos;
            FormCatalogoManoObra.InsumosModificados -= _onInsumos;
            FormCatalogoHerramientas.InsumosModificados -= _onInsumos;
            FormCatalogoMaquinaria.InsumosModificados -= _onInsumos;
            FormEditarMatriz.MatrizGuardada -= _onInsumos;
            base.OnFormClosed(e);
        }
        // ── IRecalculable ────────────────────────────────────────────────────
        public void RecalcularTodo() => CargarMatrices();

        private void OnDecimalesActualizados_Mat(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) dgvMatrices.Refresh(); }));
        }
    }
}
