using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    public partial class FormCatalogoMaquinaria : Form, IGridFormato, IBusquedaGrid, IRecalculable
    {
        public static event EventHandler InsumosModificados;

        private readonly SOPROContext _context;
        private readonly Repository<Maquinaria> _repository;
        private readonly CatalogLoadService _catalogLoadService = new();
        private readonly int? _proyectoId;
        private Maquinaria _maquinariaSeleccionada;
        private List<ColumnaMaquinaria> _columnasConfig = new List<ColumnaMaquinaria>();
        private bool _cargandoColumnas = false;
        private List<Maquinaria> _listaActual = new List<Maquinaria>();

        // ── IGridFormato ──────────────────────────────────────────────────────
        public DataGridView GridPrincipal => dgvMaquinaria;
        public DataGridView GridBusqueda => dgvMaquinaria;

        public bool GenerarReporteExcel()
        {
            ExportarCostoHorario();
            return true;
        }

        public void GenerarPdfCostoHorario()
        {
            if (_listaActual == null || !_listaActual.Any())
            {
                MessageBox.Show("No hay registros en la vista actual para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar análisis de costo horario en PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"CostoHorario_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                DefaultExt = "pdf"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Maquinaria" };

                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoMaquinaria, lblTitulo.Text)
                    : null;
                var generador = new GeneradorPdfCostoHorario(svcRep);
                var ruta = generador.Generar(proyecto, _listaActual, plantilla, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show($"Reporte PDF generado con {_listaActual.Count} análisis de costo horario.\n\n¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte PDF:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaMaquinaria _colMaqRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMaqRibbon == null) return;

            _colMaqRibbon.NombreFuente = fmt.NombreFuente;
            _colMaqRibbon.TamanoFuente = fmt.TamanoFuente;
            _colMaqRibbon.Negrita = fmt.Negrita;
            _colMaqRibbon.Cursiva = fmt.Cursiva;
            _colMaqRibbon.Alineacion = fmt.Alineacion;
            _colMaqRibbon.ColorFondo = fmt.ColorFondo;
            _colMaqRibbon.ColorFuente = fmt.ColorFuente;
            _colMaqRibbon.WrapTexto = fmt.WrapTexto;
            _colMaqRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMaqRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvMaquinaria.Columns)
            {
                if (col.Tag == _colMaqRibbon)
                {
                    AplicarEstiloDesdeColMaq((DataGridViewTextBoxColumn)col, _colMaqRibbon);
                    break;
                }
            }
            dgvMaquinaria.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMaquinaria.Columns)
            {
                if (col.Tag is not ColumnaMaquinaria colMaq) continue;
                colMaq.NombreFuente = fmt.NombreFuente;
                colMaq.TamanoFuente = fmt.TamanoFuente;
                colMaq.Negrita = fmt.Negrita;
                colMaq.Cursiva = fmt.Cursiva;
                colMaq.Alineacion = fmt.Alineacion;
                colMaq.ColorFuente = fmt.ColorFuente;
                colMaq.WrapTexto = fmt.WrapTexto;
                colMaq.AlineacionVertical = fmt.AlineacionVertical;
                colMaq.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMaq((DataGridViewTextBoxColumn)col, colMaq);
            }
            _context.SaveChanges();
            dgvMaquinaria.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMaqRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvMaquinaria.Columns.Count)
            {
                var col = dgvMaquinaria.Columns[colIndex];
                if (col.Tag is ColumnaMaquinaria cm)
                {
                    _colMaqRibbon = cm;
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
        public FormCatalogoMaquinaria(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);
            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvMaquinaria.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Maquinaria>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoMaquinaria).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasMaquinariaHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            this.Load += (s, e) => CargarMaquinaria();

            dgvMaquinaria.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvMaquinaria.ColumnWidthChanged += DgvMaquinaria_ColumnWidthChanged;
        }

        // ── Grid ──────────────────────────────────────────────────────────────
        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvMaquinaria.AutoGenerateColumns = false;
            dgvMaquinaria.AllowUserToAddRows = false;
            dgvMaquinaria.AllowUserToDeleteRows = false;
            dgvMaquinaria.ReadOnly = true;
            dgvMaquinaria.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvMaquinaria, conMenuCopia: false);
            dgvMaquinaria.CellFormatting += DgvMaquinaria_CellFormatting;
            dgvMaquinaria.KeyDown += DgvMaquinaria_KeyDown;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Cat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Cat;
            dgvMaquinaria.CellMouseDown += DgvMaquinaria_CellMouseDown;
            dgvMaquinaria.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvMaquinaria.BackgroundColor = Color.White;
            dgvMaquinaria.BorderStyle = BorderStyle.None;
            dgvMaquinaria.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvMaquinaria.GridColor = Color.FromArgb(230, 230, 230);
            dgvMaquinaria.ColumnHeadersHeight = 40;
            dgvMaquinaria.RowTemplate.Height = 35;

            dgvMaquinaria.EnableHeadersVisualStyles = false;
            dgvMaquinaria.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvMaquinaria.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMaquinaria.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvMaquinaria.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMaquinaria.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvMaquinaria.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvMaquinaria.ScrollBars = ScrollBars.Both;

            dgvMaquinaria.Columns.Clear();

            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    // Columnas calculadas/manuales no tienen DataPropertyName directa
                    bool esManual = cfg.NombreInterno == "Combustible"
                                 || cfg.NombreInterno == "TipoCosto"
                                 || cfg.NombreInterno == "Origen";

                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = esManual ? null : cfg.NombreInterno,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvMaquinaria.Columns.Add(col);
                    AplicarEstiloDesdeColMaq(col, cfg);
                }

                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn
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
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = "Clave", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPotencia", HeaderText = "Potencia (HP)", DataPropertyName = "PotenciaNominal", Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCombustible", HeaderText = "Combustible", Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCostoHorario", HeaderText = "Costo Horario", DataPropertyName = "CostoHorario", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTipoCosto", HeaderText = "Tipo", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "colDummy",
                    HeaderText = "",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }

            _cargandoColumnas = false;
        }


        private void DgvMaquinaria_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaMaquinaria cfg) return;

                var columnaDb = _context.ColumnasMaquinaria.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en maquinaria: {ex.Message}");
            }
        }

        private int DecimalesImporte => _proyectoId.HasValue
            ? (_context.Proyectos.Find(_proyectoId.Value)?.DecimalesImporte ?? 2) : 2;

        private void DgvMaquinaria_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvMaquinaria.Rows[e.RowIndex].DataBoundItem is not Maquinaria maq) return;

            var colName = dgvMaquinaria.Columns[e.ColumnIndex].Name;

            if (colName == "col_Combustible" || colName == "colCombustible")
            {
                e.Value = maq.TipoCombustible.ToString();
                e.FormattingApplied = true;
            }
            else if (colName == "col_TipoCosto" || colName == "colTipoCosto")
            {
                e.Value = maq.EsCostoCalculado ? "🧮 Calc" : "✏ Man";
                e.FormattingApplied = true;
            }
            else if (colName == "col_Origen" || colName == "colOrigen")
            {
                e.Value = ImportOriginStampService.BuildOriginDisplay(maq.Notas, maq.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
                e.FormattingApplied = true;
            }
            else if ((colName == "colCostoHorario" || colName == "col_CostoHorario") && e.Value is decimal ch)
            {
                e.Value = ch.ToString($"C{DecimalesImporte}", System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private static void AplicarEstiloDesdeColMaq(DataGridViewTextBoxColumn dgvCol, ColumnaMaquinaria col)
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

        // ── Carga de datos ────────────────────────────────────────────────────
        public void RecargarCatalogo() => CargarMaquinaria();

        private async void CargarMaquinaria()
        {
            try
            {
                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando maquinaria...";
                var _gridState = DataGridViewStateHelper.Capture(dgvMaquinaria);

                _listaActual = await _catalogLoadService.LoadMaquinariaAsync(_context, new CatalogFilterInput
                {
                    ProyectoId = _proyectoId,
                    SoloProyecto = chkSoloProyecto.Checked,
                    SoloMaestros = chkSoloMaestros.Checked,
                    SearchText = txtBuscar.Text
                });

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvMaquinaria))
                {
                    dgvMaquinaria.DataSource = null;
                    dgvMaquinaria.DataSource = _listaActual;
                    DataGridViewStateHelper.Restore(dgvMaquinaria, _gridState);
                }
                ResumeLayout();

                lblStatus.Text = $"{_listaActual.Count} registro(s) encontrado(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar maquinaria:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar maquinaria";
            }
            finally
            {
                btnRefrescar.Enabled = true;
            }
        }

        // ── Botones CRUD ──────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarMaquinaria(_context, _proyectoId);
            if (form.ShowDialog() == DialogResult.OK) CargarMaquinaria();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_maquinariaSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _maquinariaSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show(
                    "No puede editar registros del catálogo maestro desde un proyecto.\n\n" +
                    "Para modificar este registro, ábralo desde Catálogos Maestros o cree una copia.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new FormEditarMaquinaria(_context, _proyectoId, _maquinariaSeleccionada);
            if (form.ShowDialog() == DialogResult.OK) CargarMaquinaria();
        }

        private void btnCalcularCosto_Click(object sender, EventArgs e)
        {
            if (_maquinariaSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para calcular su costo horario.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _maquinariaSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show(
                    "No puede calcular costos de registros del catálogo maestro desde un proyecto.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new FormCalculoCostoHorario(_context, _maquinariaSeleccionada);
            if (form.ShowDialog() == DialogResult.OK) CargarMaquinaria();
        }

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_maquinariaSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _maquinariaSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show("No puede eliminar registros del catálogo maestro desde un proyecto.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(
                $"¿Está seguro de eliminar el registro?\n\nClave: {_maquinariaSeleccionada.Clave}\n" +
                $"Descripción: {_maquinariaSeleccionada.Descripcion}\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                var compsMaq = _context.ComponentesMatriz
                    .Where(c => c.MaquinariaId == _maquinariaSeleccionada.Id).ToList();
                if (compsMaq.Any())
                {
                    int nm = compsMaq.Select(c => c.MatrizId).Distinct().Count();
                    if (MessageBox.Show(
                        $"Este insumo está usado en {compsMaq.Count} componente(s) de {nm} matriz/matrices.\n\n"
                        + "Al eliminarlo, esos componentes también serán eliminados.\n\n¿Desea continuar?",
                        "Insumo en uso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    _context.ComponentesMatriz.RemoveRange(compsMaq);
                }

                // Primero borrar el insumo y guardar
                var enBDMaq = _context.Maquinaria.Find(_maquinariaSeleccionada.Id);
                if (enBDMaq != null) _context.Maquinaria.Remove(enBDMaq);
                _context.SaveChanges();

                // Ahora propagar a matrices afectadas (el contexto ya refleja el estado real)
                if (compsMaq.Any())
                {
                    var matrizIds = compsMaq.Select(c => c.MatrizId).Distinct().ToList();
                    RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(_context, matrizIds);
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                }

                InsumosModificados?.Invoke(null, EventArgs.Empty);
                MessageBox.Show("Registro eliminado exitosamente.", "Eliminado",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarMaquinaria();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar el registro:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRefrescar_Click(object sender, EventArgs e) => CargarMaquinaria();
        private void btnCerrar_Click(object sender, EventArgs e) => Close();
        private void btnExportarExcel_Click(object sender, EventArgs e) => ExportarCostoHorario();
        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Función de importación desde Excel próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void dgvMaquinaria_SelectionChanged(object sender, EventArgs e)
        {
            _maquinariaSeleccionada = dgvMaquinaria.SelectedRows.Count > 0
                ? dgvMaquinaria.SelectedRows[0].DataBoundItem as Maquinaria
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvMaquinaria_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarMaquinaria();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            CargarMaquinaria();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            CargarMaquinaria();
        }

        private void ActualizarEstadoBotones()
        {
            var haySeleccion = _maquinariaSeleccionada != null;
            var esMaestro = haySeleccion && _maquinariaSeleccionada.Origen == OrigenInsumo.Maestro;
            var enProyecto = _proyectoId.HasValue;

            btnEditar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            btnEliminar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            btnCalcularCosto.Enabled = haySeleccion && !(enProyecto && esMaestro);
        }

        // ── Exportar Costo Horario ────────────────────────────────────────────
        private void ExportarCostoHorario()
        {
            if (_listaActual == null || !_listaActual.Any())
            {
                MessageBox.Show("No hay registros en la vista actual para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar análisis de costo horario",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"CostoHorario_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                // Proyecto, plantilla y encabezado
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Maquinaria" };

                using var wb = new XLWorkbook();
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoMaquinaria, lblTitulo.Text)
                    : null;
                GeneradorExcelCostoHorario.Generar(wb, _listaActual, proyecto, plantilla, svcRep, tituloCfg);
                wb.SaveAs(dlg.FileName);

                if (MessageBox.Show(
                    $"Reporte generado con {_listaActual.Count} análisis de costo horario.\n\n¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el reporte:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── MENÚ CONTEXTUAL ───────────────────────────────────────────────────
        private void DgvMaquinaria_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvMaquinaria.Rows[e.RowIndex];
            if (!clickedRow.Selected)
            {
                dgvMaquinaria.ClearSelection();
                clickedRow.Selected = true;
            }

            if (e.ColumnIndex >= 0)
                dgvMaquinaria.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _maquinariaSeleccionada = clickedRow.DataBoundItem as Maquinaria;
            if (_maquinariaSeleccionada == null) return;

            bool puedeEditar = !(_proyectoId.HasValue && _maquinariaSeleccionada?.Origen == OrigenInsumo.Maestro);

            var menu = new ContextMenuStrip();

            var itemEditar = menu.Items.Add("✏️  Editar");
            itemEditar.Enabled = puedeEditar;
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var itemEliminar = menu.Items.Add("🗑️  Eliminar");
            itemEliminar.Enabled = puedeEditar;
            itemEliminar.Click += (_, __) => btnEliminar_Click(sender, EventArgs.Empty);

            menu.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            var matrices = BuscarMatricesDondeSeUsa_Maquinaria(_maquinariaSeleccionada.Id);

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
                    item.Click += (_, __) => AbrirEditorMatriz_Maquinaria(cap);
                }
            }
            menu.Items.Add(itemDonde);
            menu.Items.Add(new ToolStripSeparator());

            var itemCopiarCelda = menu.Items.Add("📋  Copiar celda");
            itemCopiarCelda.Click += (_, __) =>
            {
                var cell = dgvMaquinaria.CurrentCell;
                if (cell != null)
                {
                    string txt = cell.FormattedValue?.ToString() ?? "";
                    if (txt.Length > 0) Clipboard.SetText(txt);
                }
            };

            int _nFilas = dgvMaquinaria.SelectedRows.Count;
            string _labelFila = _nFilas > 1 ? $"📄  Copiar {_nFilas} filas" : "📄  Copiar fila";
            var itemCopiarFila = menu.Items.Add(_labelFila);
            itemCopiarFila.Click += (_, __) =>
            {
                var cols = Enumerable.Range(0, dgvMaquinaria.ColumnCount)
                    .Where(i => dgvMaquinaria.Columns[i].Visible)
                    .OrderBy(i => dgvMaquinaria.Columns[i].DisplayIndex).ToList();
                var sb = new System.Text.StringBuilder();
                foreach (DataGridViewRow r in dgvMaquinaria.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
                    sb.AppendLine(string.Join("\t", cols.Select(i => r.Cells[i].FormattedValue?.ToString() ?? "")));
                if (sb.Length > 0) Clipboard.SetText(sb.ToString().TrimEnd());
            };

            menu.Show(dgvMaquinaria, dgvMaquinaria.PointToClient(Cursor.Position));
        }

        private List<Matriz> BuscarMatricesDondeSeUsa_Maquinaria(int insumoId)
        {
            var matrizIds = _context.Set<ComponenteMatriz>()
                .Where(c => c.MaquinariaId == insumoId)
                .Select(c => c.MatrizId)
                .Distinct()
                .ToList();

            if (matrizIds.Count == 0) return new List<Matriz>();

            return _context.Matrices
                .Where(m => matrizIds.Contains(m.Id) && (_proyectoId == null || m.ProyectoId == _proyectoId))
                .OrderBy(m => m.Clave)
                .ToList();
        }

        private void AbrirEditorMatriz_Maquinaria(Matriz matriz)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId ?? 0, matriz);
            form.ShowDialog(this);
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Para configurar columnas primero debe existir un proyecto activo.", "Columnas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormColumnasAPU(_context, _proyectoId.Value, FormColumnasAPU.ModoColumnas.Maquinaria);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasMaquinariaHelper.ObtenerColumnas(_context, _proyectoId.Value);
                ConfigurarGrid();
                CargarMaquinaria();
            }
        }


        // ── IRecalculable ────────────────────────────────────────────────────
        public void RecalcularTodo() => CargarMaquinaria();

        private void OnDecimalesActualizados_Cat(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) dgvMaquinaria.Refresh(); }));
        }

        private void DgvMaquinaria_KeyDown(object sender, KeyEventArgs e)
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
