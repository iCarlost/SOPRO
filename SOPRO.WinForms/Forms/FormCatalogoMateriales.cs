using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    public partial class FormCatalogoMateriales : Form, IGridFormato, IBusquedaGrid, IRecalculable, IConsolidacionInsumos
    {
        public static event EventHandler InsumosModificados;

        private readonly SOPROContext _context;
        private readonly Repository<Material> _repository;
        private readonly CatalogLoadService _catalogLoadService = new();
        private readonly int? _proyectoId;
        private Material _materialSeleccionado;
        private List<ColumnaMaterial> _columnasConfig = new List<ColumnaMaterial>();
        private bool _cargandoColumnas = false;
        private readonly InsumoConsolidationService _consolidationService = new();

        public event EventHandler EstadoConsolidacionCambiado;

        // ── IGridFormato ──────────────────────────────────────────────────────
        public DataGridView GridPrincipal => dgvMateriales;
        public DataGridView GridBusqueda => dgvMateriales;

        public bool GenerarReporteExcel()
        {
            ExportarCatalogoExcel();
            return true;
        }


        public void GenerarPdfCatalogoMateriales()
        {
            try
            {
                var materiales = dgvMateriales.DataSource as List<Material>;
                if (materiales == null || !materiales.Any())
                {
                    MessageBox.Show("No hay materiales para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de materiales en PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Catalogo_Materiales_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Materiales" };
                var colsVis = _columnasConfig.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoMateriales, lblTitulo.Text)
                    : null;
                var generador = new GeneradorPdfCatalogoMateriales(svcRep);
                var ruta = generador.Generar(proyecto, materiales, plantilla, colsVis, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Catálogo PDF exportado.¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al exportar PDF:{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public event EventHandler ColumnaSeleccionadaCambiada;

        // _colMatRibbon es la entidad real de BD; _columnaRibbon es el DTO para la interfaz
        private ColumnaMaterial _colMatRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

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

            foreach (DataGridViewColumn col in dgvMateriales.Columns)
            {
                if (col.Tag == _colMatRibbon)
                {
                    AplicarEstiloDesdeColMat((DataGridViewTextBoxColumn)col, _colMatRibbon);
                    break;
                }
            }
            dgvMateriales.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMateriales.Columns)
            {
                if (col.Tag is not ColumnaMaterial colMat) continue;
                colMat.NombreFuente = fmt.NombreFuente;
                colMat.TamanoFuente = fmt.TamanoFuente;
                colMat.Negrita = fmt.Negrita;
                colMat.Cursiva = fmt.Cursiva;
                colMat.Alineacion = fmt.Alineacion;
                colMat.ColorFuente = fmt.ColorFuente;
                colMat.WrapTexto = fmt.WrapTexto;
                colMat.AlineacionVertical = fmt.AlineacionVertical;
                colMat.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMat((DataGridViewTextBoxColumn)col, colMat);
            }
            _context.SaveChanges();
            dgvMateriales.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMatRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvMateriales.Columns.Count)
            {
                var col = dgvMateriales.Columns[colIndex];
                if (col.Tag is ColumnaMaterial cm)
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

        // ── Constructor ───────────────────────────────────────────────────────
        public FormCatalogoMateriales(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);
            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvMateriales.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Material>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoMateriales).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasMaterialHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            this.Load += (s, e) => CargarMateriales();

            dgvMateriales.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvMateriales.ColumnWidthChanged += DgvMateriales_ColumnWidthChanged;
        }

        public bool ConsolidacionDisponible => _proyectoId.HasValue && dgvMateriales.SelectedRows.Count >= 2;
        public string NombreTipoConsolidacion => "Materiales";

        public IReadOnlyList<ConsolidacionInsumoItem> ObtenerSeleccionConsolidable()
        {
            return dgvMateriales.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as Material)
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .Select(x => new ConsolidacionInsumoItem
                {
                    Id = x.Id,
                    Clave = x.Clave ?? string.Empty,
                    Descripcion = x.Descripcion ?? string.Empty,
                    Unidad = x.Unidad ?? string.Empty,
                    Precio = x.PrecioUnitario,
                    PrecioEtiqueta = string.Format("P.U.: {0:N4}", x.PrecioUnitario)
                })
                .ToList();
        }

        public void EjecutarConsolidacion()
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("La consolidación solo está disponible dentro de un proyecto.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var seleccion = dgvMateriales.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as Material)
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();

            if (seleccion.Count < 2)
            {
                MessageBox.Show("Seleccione al menos dos registros del proyecto para consolidar.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (seleccion.Any(x => x.Origen == OrigenInsumo.Maestro))
            {
                MessageBox.Show("La consolidación solo admite registros del proyecto actual.\n\nQuite de la selección cualquier insumo del catálogo maestro e intente de nuevo.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var items = ObtenerSeleccionConsolidable();
            using var dlg = new FormConsolidarInsumos(NombreTipoConsolidacion, items, baseId =>
                _consolidationService.ObtenerPreview(_context, _proyectoId.Value, ConsolidacionInsumoTipo.Material, baseId, seleccion.Select(x => x.Id).ToList()));

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resultado = _consolidationService.Consolidar(_context, _proyectoId.Value, ConsolidacionInsumoTipo.Material, dlg.InsumoBaseId, seleccion.Select(x => x.Id).ToList());
                CargarMateriales();
                InsumosModificados?.Invoke(this, EventArgs.Empty);
                EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
                MessageBox.Show($"Consolidación completada.\n\nRegistros sustituidos: {resultado.RegistrosConsolidados}\nComponentes actualizados: {resultado.ComponentesActualizados}\nMatrices afectadas: {resultado.MatricesAfectadas}\nConceptos impactados: {resultado.ConceptosAfectados}", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No fue posible consolidar los insumos:\n{ex.Message}", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ── Grid ──────────────────────────────────────────────────────────────
        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;

            dgvMateriales.AutoGenerateColumns = false;
            dgvMateriales.AllowUserToAddRows = false;
            dgvMateriales.AllowUserToDeleteRows = false;
            dgvMateriales.ReadOnly = true;
            dgvMateriales.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvMateriales, conMenuCopia: false);
            dgvMateriales.CellFormatting += DgvMateriales_CellFormatting;
            dgvMateriales.KeyDown += DgvMateriales_KeyDown;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Cat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Cat;
            dgvMateriales.CellMouseDown += DgvMateriales_CellMouseDown;
            dgvMateriales.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvMateriales.BackgroundColor = Color.White;
            dgvMateriales.BorderStyle = BorderStyle.None;
            dgvMateriales.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvMateriales.GridColor = Color.FromArgb(230, 230, 230);
            dgvMateriales.ColumnHeadersHeight = 40;
            dgvMateriales.RowTemplate.Height = 35;

            dgvMateriales.EnableHeadersVisualStyles = false;
            dgvMateriales.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvMateriales.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMateriales.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvMateriales.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMateriales.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvMateriales.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvMateriales.ScrollBars = ScrollBars.Both;

            dgvMateriales.Columns.Clear();

            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    var alin = cfg.Alineacion switch
                    {
                        AlineacionColumna.Centro => DataGridViewContentAlignment.MiddleCenter,
                        AlineacionColumna.Derecha => DataGridViewContentAlignment.MiddleRight,
                        _ => DataGridViewContentAlignment.MiddleLeft,
                    };

                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = cfg.NombreInterno == "Origen" ? null : cfg.NombreInterno,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                        DefaultCellStyle = new DataGridViewCellStyle
                        {
                            Alignment = alin,
                            Format = cfg.FormatoNumerico ?? "",
                        },
                    };


                    dgvMateriales.Columns.Add(col);
                }

                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "colDummy",
                    HeaderText = "",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }
            else
            {
                // Fallback sin proyecto
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = "Clave", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = "Unidad", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrecioUnitario", HeaderText = "Precio Unitario", DataPropertyName = "PrecioUnitario", Width = 140, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "colDummy",
                    HeaderText = "",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }

            // Aplicar estilo inicial desde BD
            foreach (DataGridViewColumn col in dgvMateriales.Columns)
                if (col.Tag is ColumnaMaterial cm)
                    AplicarEstiloDesdeColMat((DataGridViewTextBoxColumn)col, cm);

            _cargandoColumnas = false;
        }

        private void DgvMateriales_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaMaterial cfg) return;

                var columnaDb = _context.ColumnasMaterial.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en materiales: {ex.Message}");
            }
        }

        private int DecimalesImporte => _proyectoId.HasValue
            ? (_context.Proyectos.Find(_proyectoId.Value)?.DecimalesImporte ?? 2) : 2;

        private void DgvMateriales_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvMateriales.Rows[e.RowIndex].DataBoundItem is not Material mat) return;
            var colName = dgvMateriales.Columns[e.ColumnIndex].Name;
            if (colName == "col_Origen" || colName == "colOrigen")
            {
                e.Value = ImportOriginStampService.BuildOriginDisplay(mat.Notas, mat.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
                e.FormattingApplied = true;
                return;
            }
            if ((colName == "colPrecioUnitario" || colName == "col_PrecioUnitario") && e.Value is decimal pu)
            {
                e.Value = pu.ToString($"C{DecimalesImporte}", System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private static void AplicarEstiloDesdeColMat(DataGridViewTextBoxColumn dgvCol, ColumnaMaterial col)
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
            }
            catch { }
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
            try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }

        public void RecargarCatalogo() => CargarMateriales();

        private async void CargarMateriales()
        {
            try
            {
                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando materiales...";
                var _gridState = DataGridViewStateHelper.Capture(dgvMateriales);

                var materiales = await _catalogLoadService.LoadMaterialesAsync(_context, new CatalogFilterInput
                {
                    ProyectoId = _proyectoId,
                    SoloProyecto = chkSoloProyecto.Checked,
                    SoloMaestros = chkSoloMaestros.Checked,
                    SearchText = txtBuscar.Text
                });

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvMateriales))
                {
                    dgvMateriales.DataSource = null;
                    dgvMateriales.DataSource = materiales;
                    DataGridViewStateHelper.Restore(dgvMateriales, _gridState);
                }
                ResumeLayout();

                lblStatus.Text = $"{materiales.Count} material(es) encontrado(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar materiales:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar materiales";
            }
            finally
            {
                btnRefrescar.Enabled = true;
            }
        }

        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarMaterial(_context, _proyectoId);
            if (form.ShowDialog() == DialogResult.OK)
                CargarMateriales();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_materialSeleccionado == null)
            {
                MessageBox.Show("Seleccione un material para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_proyectoId.HasValue && _materialSeleccionado.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show(
                    "No puede editar materiales del catálogo maestro desde un proyecto.\n\n" +
                    "Para modificar este material, ábralo desde Catálogos Maestros o cree una copia para este proyecto.",
                    "Material del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormEditarMaterial(_context, _proyectoId, _materialSeleccionado);
            if (form.ShowDialog() == DialogResult.OK)
                CargarMateriales();
        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_materialSeleccionado == null)
            {
                MessageBox.Show("Seleccione un material para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_proyectoId.HasValue && _materialSeleccionado.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show("No puede eliminar materiales del catálogo maestro desde un proyecto.",
                    "Material del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"¿Está seguro de eliminar el material?\n\n" +
                $"Clave: {_materialSeleccionado.Clave}\n" +
                $"Descripción: {_materialSeleccionado.Descripcion}\n\n" +
                "Esta acción no se puede deshacer.",
                "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // Verificar si está en uso en matrices y borrar componentes primero
                    var componentesEnUso = _context.ComponentesMatriz
                        .Where(c => c.MaterialId == _materialSeleccionado.Id)
                        .ToList();

                    if (componentesEnUso.Any())
                    {
                        int numMatrices = componentesEnUso.Select(c => c.MatrizId).Distinct().Count();
                        var confirmar = MessageBox.Show(
                            $"Este insumo está usado en {componentesEnUso.Count} componente(s) de {numMatrices} matriz/matrices.\n\n" +
                            "Al eliminarlo, esos componentes también serán eliminados.\n\n¿Desea continuar?",
                            "Insumo en uso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (confirmar != DialogResult.Yes) return;
                        _context.ComponentesMatriz.RemoveRange(componentesEnUso);
                    }


                    // Primero borrar el insumo y guardar
                    var enBD = _context.Materiales.Find(_materialSeleccionado.Id);
                    if (enBD != null) _context.Materiales.Remove(enBD);
                    _context.SaveChanges();

                    // Ahora propagar a matrices afectadas (el contexto ya refleja el estado real)
                    if (componentesEnUso.Any())
                    {
                        var matrizIds = componentesEnUso.Select(c => c.MatrizId).Distinct().ToList();
                        RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(_context, matrizIds);
                        OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                    }

                    InsumosModificados?.Invoke(null, EventArgs.Empty);

                    MessageBox.Show("Material eliminado exitosamente.", "Eliminado",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    CargarMateriales();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar el material:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Para configurar columnas primero debe existir un proyecto activo.", "Columnas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormColumnasAPU(_context, _proyectoId.Value, FormColumnasAPU.ModoColumnas.Materiales);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasMaterialHelper.ObtenerColumnas(_context, _proyectoId.Value);
                ConfigurarGrid();
                CargarMateriales();
            }
        }

        private void btnRefrescar_Click(object sender, EventArgs e) => CargarMateriales();

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void dgvMateriales_SelectionChanged(object sender, EventArgs e)
        {
            _materialSeleccionado = dgvMateriales.SelectedRows.Count > 0
                ? dgvMateriales.SelectedRows[0].DataBoundItem as Material
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvMateriales_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarMateriales();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            CargarMateriales();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            CargarMateriales();
        }

        private void ActualizarEstadoBotones()
        {
            var haySeleccion = _materialSeleccionado != null;
            var esMaestro = haySeleccion && _materialSeleccionado.Origen == OrigenInsumo.Maestro;
            var enProyecto = _proyectoId.HasValue;

            btnEditar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            btnEliminar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
        }

        private void btnExportarExcel_Click(object sender, EventArgs e) => ExportarCatalogoExcel();

        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Función de importación desde Excel próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportarCatalogoExcel()
        {
            try
            {
                var materiales = dgvMateriales.DataSource as List<Material>;
                if (materiales == null || !materiales.Any())
                {
                    MessageBox.Show("No hay materiales para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de materiales",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Catalogo_Materiales_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Materiales");

                // ── Encabezado estándar SOPRO ─────────────────────────────
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Materiales" };
                var colsVis = _columnasConfig.Where(c => c.Visible).ToList();
                int numCols = colsVis.Any() ? colsVis.Count : 5;

                // ── Encabezado + Título ─────────────────────────────────
                int fila = 1;
                if (_proyectoId.HasValue)
                    fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, svcRep);

                var titulo = ws.Range(fila, 1, fila, numCols);
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoMateriales, lblTitulo.Text)
                    : null;
                ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "CATÁLOGO DE MATERIALES");
                ws.Row(fila).Height = 24;
                fila++;

                // Encabezados de columnas
                if (colsVis.Any())
                {
                    for (int i = 0; i < colsVis.Count; i++)
                    {
                        var h = ws.Cell(fila, i + 1);
                        h.Value = colsVis[i].Nombre;
                        h.Style.Font.Bold = true;
                        h.Style.Fill.BackgroundColor = XLColor.FromHtml("#4A4A6A");
                        h.Style.Font.FontColor = XLColor.White;
                        h.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Column(i + 1).Width = colsVis[i].AnchoColumna / 7.0;
                    }
                }
                else
                {
                    string[] hdrs = { "Clave", "Descripción", "Unidad", "Precio Unitario", "Origen" };
                    for (int i = 0; i < hdrs.Length; i++)
                    {
                        var h = ws.Cell(fila, i + 1);
                        h.Value = hdrs[i];
                        h.Style.Font.Bold = true;
                        h.Style.Fill.BackgroundColor = XLColor.FromHtml("#4A4A6A");
                        h.Style.Font.FontColor = XLColor.White;
                        h.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }
                ws.Row(fila).Height = 18;
                fila++;

                // Datos
                bool alt = false;
                foreach (var m in materiales)
                {
                    string fondo = alt ? "#F5F5F5" : "#FFFFFF";
                    alt = !alt;

                    if (colsVis.Any())
                    {
                        for (int i = 0; i < colsVis.Count; i++)
                        {
                            var cell = ws.Cell(fila, i + 1);
                            cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                            if (colsVis[i].Negrita) cell.Style.Font.Bold = true;
                            if (colsVis[i].Cursiva) cell.Style.Font.Italic = true;
                            if (!string.IsNullOrEmpty(colsVis[i].NombreFuente))
                                cell.Style.Font.FontName = colsVis[i].NombreFuente;
                            if (colsVis[i].TamanoFuente > 0)
                                cell.Style.Font.FontSize = colsVis[i].TamanoFuente;
                            cell.Style.Alignment.WrapText = colsVis[i].WrapTexto;
                            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                            switch (colsVis[i].NombreInterno)
                            {
                                case "Clave": cell.Value = m.Clave ?? ""; break;
                                case "Descripcion": cell.Value = m.Descripcion ?? ""; break;
                                case "Unidad": cell.Value = m.Unidad ?? ""; break;
                                case "PrecioUnitario":
                                    cell.Value = m.PrecioUnitario;
                                    cell.Style.NumberFormat.Format = "#,##0.0000";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    break;
                                case "Origen":
                                    cell.Value = m.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                    break;
                            }
                        }
                    }
                    else
                    {
                        ws.Cell(fila, 1).Value = m.Clave ?? "";
                        ws.Cell(fila, 2).Value = m.Descripcion ?? "";
                        ws.Cell(fila, 3).Value = m.Unidad ?? "";
                        ws.Cell(fila, 4).Value = m.PrecioUnitario;
                        ws.Cell(fila, 4).Style.NumberFormat.Format = "#,##0.0000";
                        ws.Cell(fila, 5).Value = m.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";
                        ws.Range(fila, 1, fila, 5).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                    }

                    ws.Row(fila).Height = CalcularAlturaFilaCatalogo(colsVis, fila, ws, 14);
                    fila++;
                }

                ws.Range(2, 1, fila - 1, numCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(2, 1, fila - 1, numCols).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

                wb.SaveAs(dlg.FileName);

                if (MessageBox.Show("Catálogo exportado.\n\n¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── MENÚ CONTEXTUAL ───────────────────────────────────────────────────
        private void DgvMateriales_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvMateriales.Rows[e.RowIndex];
            if (!clickedRow.Selected)
            {
                dgvMateriales.ClearSelection();
                clickedRow.Selected = true;
            }

            if (e.ColumnIndex >= 0)
                dgvMateriales.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _materialSeleccionado = clickedRow.DataBoundItem as Material;
            if (_materialSeleccionado == null) return;

            bool puedeEditar = !(_proyectoId.HasValue && _materialSeleccionado.Origen == OrigenInsumo.Maestro);

            var menu = new ContextMenuStrip();

            var itemEditar = menu.Items.Add("✏️  Editar");
            itemEditar.Enabled = puedeEditar;
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var itemEliminar = menu.Items.Add("🗑️  Eliminar");
            itemEliminar.Enabled = puedeEditar;
            itemEliminar.Click += (_, __) => btnEliminar_Click(sender, EventArgs.Empty);

            menu.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            var matrices = BuscarMatricesDondeSeUsa(_materialSeleccionado.Id, TipoInsumoContexto.Material);

            if (matrices.Count == 0)
            {
                var sinUso = itemDonde.DropDownItems.Add("(Sin uso en ninguna matriz)");
                sinUso.Enabled = false;
            }
            else
            {
                foreach (var m in matrices)
                {
                    string label = m.Clave + " — " + (m.Descripcion?.Length > 50
                        ? m.Descripcion[..50] + "…" : m.Descripcion ?? "");
                    var item = itemDonde.DropDownItems.Add(label);
                    var cap = m;
                    item.Click += (_, __) => AbrirEditorMatriz(cap);
                }
            }
            menu.Items.Add(itemDonde);
            menu.Items.Add(new ToolStripSeparator());

            var itemCopiarCelda = menu.Items.Add("📋  Copiar celda");
            itemCopiarCelda.Click += (_, __) =>
            {
                var cell = dgvMateriales.CurrentCell;
                if (cell != null)
                {
                    string txt = cell.FormattedValue?.ToString() ?? "";
                    if (txt.Length > 0) Clipboard.SetText(txt);
                }
            };

            int _nFilas = dgvMateriales.SelectedRows.Count;
            string _labelFila = _nFilas > 1 ? $"📄  Copiar {_nFilas} filas" : "📄  Copiar fila";
            var itemCopiarFila = menu.Items.Add(_labelFila);
            itemCopiarFila.Click += (_, __) =>
            {
                var cols = Enumerable.Range(0, dgvMateriales.ColumnCount)
                    .Where(i => dgvMateriales.Columns[i].Visible)
                    .OrderBy(i => dgvMateriales.Columns[i].DisplayIndex).ToList();
                var sb = new System.Text.StringBuilder();
                foreach (DataGridViewRow r in dgvMateriales.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
                    sb.AppendLine(string.Join("\t", cols.Select(i => r.Cells[i].FormattedValue?.ToString() ?? "")));
                if (sb.Length > 0) Clipboard.SetText(sb.ToString().TrimEnd());
            };

            menu.Show(dgvMateriales, dgvMateriales.PointToClient(Cursor.Position));
        }

        private List<Matriz> BuscarMatricesDondeSeUsa(int insumoId, TipoInsumoContexto tipo)
        {
            var matrizIds = _context.Set<ComponenteMatriz>()
                .Where(c => tipo == TipoInsumoContexto.Material ? c.MaterialId == insumoId
                          : tipo == TipoInsumoContexto.ManoDeObra ? c.ManoDeObraId == insumoId
                          : tipo == TipoInsumoContexto.Maquinaria ? c.MaquinariaId == insumoId
                          : c.HerramientaId == insumoId)
                .Select(c => c.MatrizId)
                .Distinct()
                .ToList();

            if (matrizIds.Count == 0) return new List<Matriz>();

            return _context.Matrices
                .Where(m => matrizIds.Contains(m.Id) &&
                            (_proyectoId == null || m.ProyectoId == _proyectoId))
                .OrderBy(m => m.Clave)
                .ToList();
        }

        private void AbrirEditorMatriz(Matriz matriz)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId ?? 0, matriz);
            form.ShowDialog(this);
        }

        private static double CalcularAlturaFilaCatalogo(List<ColumnaMaterial> colsVis, int fila, IXLWorksheet ws, double alturaBase)
        {
            if (colsVis == null || colsVis.Count == 0) return alturaBase;

            double altura = alturaBase;
            for (int i = 0; i < colsVis.Count; i++)
            {
                var col = colsVis[i];
                if (!col.WrapTexto) continue;

                var valor = ws.Cell(fila, i + 1).GetFormattedString();
                if (string.IsNullOrWhiteSpace(valor)) continue;

                using var font = new Font(
                    string.IsNullOrWhiteSpace(col.NombreFuente) ? "Segoe UI" : col.NombreFuente,
                    Math.Max(8f, col.TamanoFuente > 0 ? col.TamanoFuente : 9f),
                    col.Negrita ? FontStyle.Bold : FontStyle.Regular);

                int anchoPx = Math.Max(24, (int)Math.Round(col.AnchoColumna - 8d));
                var proposed = new Size(anchoPx, int.MaxValue);
                var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
                var measured = TextRenderer.MeasureText(valor, font, proposed, flags);
                double alturaPts = Math.Max(alturaBase, measured.Height * 72.0 / 96.0 + 6);
                if (alturaPts > altura) altura = alturaPts;
            }

            return altura;
        }


        // ── IRecalculable ────────────────────────────────────────────────────
        public void RecalcularTodo() => CargarMateriales();

        private void OnDecimalesActualizados_Cat(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) dgvMateriales.Refresh(); }));
        }

        private void DgvMateriales_KeyDown(object sender, KeyEventArgs e)
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
                btnEliminar_Click(sender, EventArgs.Empty);
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
    }
}