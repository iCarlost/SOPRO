using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    public partial class FormCatalogoHerramientas : Form, IGridFormato, IBusquedaGrid, IRecalculable
    {
        public static event EventHandler InsumosModificados;

        private readonly SOPROContext _context;
        private readonly Repository<Herramienta> _repository;
        private readonly int? _proyectoId;
        private Herramienta _herramientaSeleccionada;
        private List<ColumnaHerramienta> _columnasConfig = new List<ColumnaHerramienta>();
        private bool _cargandoColumnas = false;

        // ── IGridFormato ──────────────────────────────────────────────────────
        public DataGridView GridPrincipal => dgvHerramientas;
        public DataGridView GridBusqueda => dgvHerramientas;

        public bool GenerarReporteExcel()
        {
            ExportarCatalogoExcel();
            return true;
        }

        public void GenerarPdfCatalogoHerramientas()
        {
            try
            {
                var herramientas = dgvHerramientas.DataSource as List<Herramienta>;
                if (herramientas == null || !herramientas.Any())
                {
                    MessageBox.Show("No hay herramientas para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de herramientas en PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Catalogo_Herramientas_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Herramientas" };
                var colsVis = _columnasConfig.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoHerramientas, lblTitulo.Text)
                    : null;
                var generador = new GeneradorPdfCatalogoHerramientas(svcRep);
                var ruta = generador.Generar(proyecto, herramientas, plantilla, colsVis, dlg.FileName, tituloCfg);
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

        private ColumnaHerramienta _colHerRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colHerRibbon == null) return;

            _colHerRibbon.NombreFuente = fmt.NombreFuente;
            _colHerRibbon.TamanoFuente = fmt.TamanoFuente;
            _colHerRibbon.Negrita = fmt.Negrita;
            _colHerRibbon.Cursiva = fmt.Cursiva;
            _colHerRibbon.Alineacion = fmt.Alineacion;
            _colHerRibbon.ColorFondo = fmt.ColorFondo;
            _colHerRibbon.ColorFuente = fmt.ColorFuente;
            _colHerRibbon.WrapTexto = fmt.WrapTexto;
            _colHerRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colHerRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvHerramientas.Columns)
            {
                if (col.Tag == _colHerRibbon)
                {
                    AplicarEstiloDesdeColHer((DataGridViewTextBoxColumn)col, _colHerRibbon);
                    break;
                }
            }
            dgvHerramientas.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvHerramientas.Columns)
            {
                if (col.Tag is not ColumnaHerramienta colHer) continue;
                colHer.NombreFuente = fmt.NombreFuente;
                colHer.TamanoFuente = fmt.TamanoFuente;
                colHer.Negrita = fmt.Negrita;
                colHer.Cursiva = fmt.Cursiva;
                colHer.Alineacion = fmt.Alineacion;
                colHer.ColorFuente = fmt.ColorFuente;
                colHer.WrapTexto = fmt.WrapTexto;
                colHer.AlineacionVertical = fmt.AlineacionVertical;
                colHer.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColHer((DataGridViewTextBoxColumn)col, colHer);
            }
            _context.SaveChanges();
            dgvHerramientas.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colHerRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvHerramientas.Columns.Count)
            {
                var col = dgvHerramientas.Columns[colIndex];
                if (col.Tag is ColumnaHerramienta ch)
                {
                    _colHerRibbon = ch;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = ch.Nombre,
                        NombreFuente = ch.NombreFuente,
                        TamanoFuente = ch.TamanoFuente,
                        Negrita = ch.Negrita,
                        Cursiva = ch.Cursiva,
                        Alineacion = ch.Alineacion,
                        ColorFondo = ch.ColorFondo,
                        ColorFuente = ch.ColorFuente,
                        WrapTexto = ch.WrapTexto,
                        AlineacionVertical = ch.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public FormCatalogoHerramientas(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            dgvHerramientas.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Herramienta>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelHeader, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoHerramientas).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasHerramientaHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            this.Load += (s, e) => CargarHerramientas();

            dgvHerramientas.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvHerramientas.ColumnWidthChanged += DgvHerramientas_ColumnWidthChanged;
        }

        // ── Grid ──────────────────────────────────────────────────────────────
        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvHerramientas.AutoGenerateColumns = false;
            dgvHerramientas.AllowUserToAddRows = false;
            dgvHerramientas.AllowUserToDeleteRows = false;
            dgvHerramientas.ReadOnly = true;
            dgvHerramientas.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvHerramientas, conMenuCopia: false);
            dgvHerramientas.CellMouseDown += DgvHerramientas_CellMouseDown;
            dgvHerramientas.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvHerramientas.BackgroundColor = Color.White;
            dgvHerramientas.BorderStyle = BorderStyle.None;
            dgvHerramientas.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvHerramientas.GridColor = Color.FromArgb(230, 230, 230);
            dgvHerramientas.ColumnHeadersHeight = 40;
            dgvHerramientas.RowTemplate.Height = 35;

            dgvHerramientas.EnableHeadersVisualStyles = false;
            dgvHerramientas.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvHerramientas.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvHerramientas.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvHerramientas.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvHerramientas.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvHerramientas.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvHerramientas.ScrollBars = ScrollBars.Both;

            dgvHerramientas.Columns.Clear();

            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = (cfg.NombreInterno == "PrecioUnitario" || cfg.NombreInterno == "OrigenDetalle") ? null : cfg.NombreInterno,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvHerramientas.Columns.Add(col);
                    AplicarEstiloDesdeColHer(col, cfg);
                }

                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn
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
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = "Clave", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = "Unidad", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrecio", HeaderText = "Precio/Porcentaje", Width = 150, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 180, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "colDummy",
                    HeaderText = "",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }

            // CellFormatting especial: precio vs porcentaje según EsPorcentajeMO
            dgvHerramientas.CellFormatting += DgvHerramientas_CellFormatting;
            dgvHerramientas.KeyDown += DgvHerramientas_KeyDown;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Cat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Cat;

            _cargandoColumnas = false;
        }

        private void DgvHerramientas_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaHerramienta cfg) return;

                var columnaDb = _context.ColumnasHerramienta.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                cfg.AnchoColumna = nuevoAncho;
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en herramientas: {ex.Message}");
            }
        }

        private void DgvHerramientas_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var colName = dgvHerramientas.Columns[e.ColumnIndex].Name;
            if (dgvHerramientas.Rows[e.RowIndex].DataBoundItem is not Herramienta h) return;

            if (colName == "col_OrigenDetalle" || colName == "colOrigen")
            {
                e.Value = ImportOriginStampService.BuildOriginDisplay(h.Notas, h.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
                e.FormattingApplied = true;
                return;
            }

            if (e.Value == null) return;
            if (colName != "col_PrecioUnitario" && colName != "colPrecio") return;

            decimal valor;
            if (e.Value is decimal d)
            {
                valor = d;
            }
            else if (e.Value is IConvertible)
            {
                try
                {
                    valor = Convert.ToDecimal(e.Value);
                }
                catch
                {
                    var texto = e.Value?.ToString()?.Trim() ?? string.Empty;
                    texto = texto.Replace("%", string.Empty)
                                 .Replace("$", string.Empty)
                                 .Replace(",", string.Empty);

                    if (!decimal.TryParse(texto, out valor))
                        return;
                }
            }
            else
            {
                var texto = e.Value?.ToString()?.Trim() ?? string.Empty;
                texto = texto.Replace("%", string.Empty)
                             .Replace("$", string.Empty)
                             .Replace(",", string.Empty);

                if (!decimal.TryParse(texto, out valor))
                    return;
            }

            int _decHer = _proyectoId.HasValue ? (_context.Proyectos.Find(_proyectoId.Value)?.DecimalesImporte ?? 2) : 2;
            e.Value = h.EsPorcentajeMO ? $"{valor:N2}%" : valor.ToString($"C{_decHer}", System.Globalization.CultureInfo.CurrentCulture);
            e.FormattingApplied = true;
        }

        private static void AplicarEstiloDesdeColHer(DataGridViewTextBoxColumn dgvCol, ColumnaHerramienta col)
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

        // ── Carga de datos ────────────────────────────────────────────────────
        public void RecargarCatalogo() => CargarHerramientas();

        private void CargarHerramientas()
        {
            try
            {
                var _gridState = DataGridViewStateHelper.Capture(dgvHerramientas);
                IQueryable<Herramienta> query = _context.Herramientas;

                if (_proyectoId.HasValue)
                    query = query.Where(h => h.ProyectoId == _proyectoId.Value);

                if (!string.IsNullOrWhiteSpace(txtBuscar.Text))
                {
                    var term = txtBuscar.Text.Trim().ToLower();
                    query = query.Where(h =>
                        h.Clave.ToLower().Contains(term) ||
                        h.Descripcion.ToLower().Contains(term));
                }

                var lista = query.OrderBy(h => h.Clave).ToList();

                if (chkSoloProyecto.Checked)
                    lista = lista.Where(h => string.IsNullOrWhiteSpace(ImportOriginStampService.ExtractProjectName(h.Notas))).ToList();
                else if (chkSoloMaestros.Checked)
                    lista = lista.Where(h => !string.IsNullOrWhiteSpace(ImportOriginStampService.ExtractProjectName(h.Notas))).ToList();

                using (GridRedrawHelper.Suspend(dgvHerramientas))
                {
                    dgvHerramientas.DataSource = null;
                    dgvHerramientas.DataSource = lista;
                    DataGridViewStateHelper.Restore(dgvHerramientas, _gridState);
                }

                string colPrecioName = dgvHerramientas.Columns.Contains("col_PrecioUnitario") ? "col_PrecioUnitario" : "colPrecio";
                if (dgvHerramientas.Columns.Contains(colPrecioName))
                {
                    foreach (DataGridViewRow row in dgvHerramientas.Rows)
                        if (row.DataBoundItem is Herramienta h)
                            row.Cells[colPrecioName].Value = h.PrecioUnitario;
                }

                lblStatus.Text = $"{lista.Count} herramienta(s) encontrada(s)";
                ActualizarEstadoBotones();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar herramientas:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Botones CRUD ──────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Debe seleccionar un proyecto primero.", "Proyecto Requerido",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dialog = new FormEditarHerramienta(_context, _proyectoId.Value);
            if (dialog.ShowDialog(this) == DialogResult.OK) CargarHerramientas();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_herramientaSeleccionada == null)
            {
                MessageBox.Show("Seleccione una herramienta para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dialog = new FormEditarHerramienta(_context, _proyectoId.Value, _herramientaSeleccionada);
            if (dialog.ShowDialog(this) == DialogResult.OK) CargarHerramientas();
        }

        private async void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_herramientaSeleccionada == null)
            {
                MessageBox.Show("Seleccione una herramienta para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(
                $"¿Está seguro de eliminar la herramienta '{_herramientaSeleccionada.Clave}'?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            try
            {
                var compsH = _context.ComponentesMatriz
                    .Where(c => c.HerramientaId == _herramientaSeleccionada.Id).ToList();
                if (compsH.Any())
                {
                    int nm = compsH.Select(c => c.MatrizId).Distinct().Count();
                    if (MessageBox.Show(
                        $"Esta herramienta está usada en {compsH.Count} componente(s) de {nm} matriz/matrices.\n\n"
                        + "Al eliminarla, esos componentes también serán eliminados.\n\n¿Desea continuar?",
                        "Herramienta en uso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    _context.ComponentesMatriz.RemoveRange(compsH);
                }

                // Primero borrar el insumo y guardar
                var enBDH = _context.Herramientas.Find(_herramientaSeleccionada.Id);
                if (enBDH != null) _context.Herramientas.Remove(enBDH);
                _context.SaveChanges();

                // Ahora propagar a matrices afectadas (el contexto ya refleja el estado real)
                if (compsH.Any())
                {
                    var matrizIds = compsH.Select(c => c.MatrizId).Distinct().ToList();
                    RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(_context, matrizIds);
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                }

                InsumosModificados?.Invoke(null, EventArgs.Empty);
                MessageBox.Show("Herramienta eliminada exitosamente.", "Éxito",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarHerramientas();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar herramienta:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Para configurar columnas primero debe existir un proyecto activo.", "Columnas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormColumnasAPU(_context, _proyectoId.Value, FormColumnasAPU.ModoColumnas.Herramientas);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasHerramientaHelper.ObtenerColumnas(_context, _proyectoId.Value);
                ConfigurarGrid();
                CargarHerramientas();
            }
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarHerramientas();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            CargarHerramientas();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            CargarHerramientas();
        }

        private void dgvHerramientas_SelectionChanged(object sender, EventArgs e)
        {
            _herramientaSeleccionada = dgvHerramientas.SelectedRows.Count > 0
                ? dgvHerramientas.SelectedRows[0].DataBoundItem as Herramienta
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvHerramientas_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void ActualizarEstadoBotones()
        {
            bool haySeleccion = _herramientaSeleccionada != null;
            btnEditar.Enabled = haySeleccion;
            btnEliminar.Enabled = haySeleccion;
        }

        // ── Exportar Excel ────────────────────────────────────────────────────
        private void ExportarCatalogoExcel()
        {
            try
            {
                var lista = dgvHerramientas.DataSource as List<Herramienta>;
                if (lista == null || !lista.Any())
                {
                    MessageBox.Show("No hay herramientas para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de herramientas",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Catalogo_Herramientas_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Herramientas");

                // ── Encabezado estándar SOPRO ─────────────────────────────
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Herramientas" };
                var colsVis = _columnasConfig.Where(c => c.Visible).ToList();
                int numCols = colsVis.Any() ? colsVis.Count : 4;

                // ── Encabezado + Título ─────────────────────────────────
                int fila = 1;
                if (_proyectoId.HasValue)
                    fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, svcRep);

                var titulo = ws.Range(fila, 1, fila, numCols);
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoHerramientas, lblTitulo.Text)
                    : null;
                ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "CATÁLOGO DE HERRAMIENTAS");
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
                    string[] hdrs = { "Clave", "Descripción", "Unidad", "Precio/Porcentaje" };
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
                foreach (var h2 in lista)
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
                                case "Clave": cell.Value = h2.Clave ?? ""; break;
                                case "Descripcion": cell.Value = h2.Descripcion ?? ""; break;
                                case "Unidad": cell.Value = h2.Unidad ?? ""; break;
                                case "PrecioUnitario":
                                    if (h2.EsPorcentajeMO)
                                    {
                                        cell.Value = $"{h2.PrecioUnitario:N2}%";
                                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    }
                                    else
                                    {
                                        cell.Value = h2.PrecioUnitario;
                                        cell.Style.NumberFormat.Format = "#,##0.00";
                                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        ws.Cell(fila, 1).Value = h2.Clave ?? "";
                        ws.Cell(fila, 2).Value = h2.Descripcion ?? "";
                        ws.Cell(fila, 3).Value = h2.Unidad ?? "";
                        if (h2.EsPorcentajeMO)
                            ws.Cell(fila, 4).Value = $"{h2.PrecioUnitario:N2}%";
                        else
                        {
                            ws.Cell(fila, 4).Value = h2.PrecioUnitario;
                            ws.Cell(fila, 4).Style.NumberFormat.Format = "#,##0.00";
                        }
                        ws.Range(fila, 1, fila, 4).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
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
        private void DgvHerramientas_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvHerramientas.Rows[e.RowIndex];
            if (!clickedRow.Selected)
            {
                dgvHerramientas.ClearSelection();
                clickedRow.Selected = true;
            }

            if (e.ColumnIndex >= 0)
                dgvHerramientas.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _herramientaSeleccionada = clickedRow.DataBoundItem as Herramienta;
            if (_herramientaSeleccionada == null) return;

            var menu = new ContextMenuStrip();

            var itemEditar = menu.Items.Add("✏️  Editar");
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var itemEliminar = menu.Items.Add("🗑️  Eliminar");
            itemEliminar.Click += (_, __) => btnEliminar_Click(sender, EventArgs.Empty);

            menu.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            var matrices = BuscarMatricesDondeSeUsa_Herramienta(_herramientaSeleccionada.Id);

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
                    item.Click += (_, __) => AbrirEditorMatriz_Herramienta(cap);
                }
            }
            menu.Items.Add(itemDonde);
            menu.Items.Add(new ToolStripSeparator());

            var itemCopiarCelda = menu.Items.Add("📋  Copiar celda");
            itemCopiarCelda.Click += (_, __) =>
            {
                var cell = dgvHerramientas.CurrentCell;
                if (cell != null)
                {
                    string txt = cell.FormattedValue?.ToString() ?? "";
                    if (txt.Length > 0) Clipboard.SetText(txt);
                }
            };

            int _nFilas = dgvHerramientas.SelectedRows.Count;
            string _labelFila = _nFilas > 1 ? $"📄  Copiar {_nFilas} filas" : "📄  Copiar fila";
            var itemCopiarFila = menu.Items.Add(_labelFila);
            itemCopiarFila.Click += (_, __) =>
            {
                var cols = Enumerable.Range(0, dgvHerramientas.ColumnCount)
                    .Where(i => dgvHerramientas.Columns[i].Visible)
                    .OrderBy(i => dgvHerramientas.Columns[i].DisplayIndex).ToList();
                var sb = new System.Text.StringBuilder();
                foreach (DataGridViewRow r in dgvHerramientas.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
                    sb.AppendLine(string.Join("\t", cols.Select(i => r.Cells[i].FormattedValue?.ToString() ?? "")));
                if (sb.Length > 0) Clipboard.SetText(sb.ToString().TrimEnd());
            };

            menu.Show(dgvHerramientas, dgvHerramientas.PointToClient(Cursor.Position));
        }

        private List<Matriz> BuscarMatricesDondeSeUsa_Herramienta(int insumoId)
        {
            var matrizIds = _context.Set<ComponenteMatriz>()
                .Where(c => c.HerramientaId == insumoId)
                .Select(c => c.MatrizId)
                .Distinct()
                .ToList();

            if (matrizIds.Count == 0) return new List<Matriz>();

            return _context.Matrices
                .Where(m => matrizIds.Contains(m.Id) && (_proyectoId == null || m.ProyectoId == _proyectoId))
                .OrderBy(m => m.Clave)
                .ToList();
        }

        private void AbrirEditorMatriz_Herramienta(Matriz matriz)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId ?? 0, matriz);
            form.ShowDialog(this);
        }

        private static double CalcularAlturaFilaCatalogo(List<ColumnaHerramienta> colsVis, int fila, IXLWorksheet ws, double alturaBase)
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
        public void RecalcularTodo() => CargarHerramientas();

        private void OnDecimalesActualizados_Cat(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) dgvHerramientas.Refresh(); }));
        }

        private void DgvHerramientas_KeyDown(object sender, KeyEventArgs e)
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