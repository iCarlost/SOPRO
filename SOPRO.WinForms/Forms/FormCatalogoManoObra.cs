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
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    public partial class FormCatalogoManoObra : Form, IGridFormato, IBusquedaGrid, IRecalculable
    {
        public static event EventHandler InsumosModificados;

        private readonly SOPROContext _context;
        private readonly Repository<ManoDeObra> _repository;
        private readonly CatalogLoadService _catalogLoadService = new();
        private readonly int? _proyectoId;
        private ManoDeObra _manoDeObraSeleccionada;
        private Proyecto ProyectoActual => _proyectoId.HasValue
            ? _context.Proyectos.Find(_proyectoId.Value)
            : null;
        private List<ColumnaManoObra> _columnasConfig = new List<ColumnaManoObra>();
        private bool _cargandoColumnas = false;

        // ── IGridFormato ──────────────────────────────────────────────────────
        public DataGridView GridPrincipal => dgvManoObra;
        public DataGridView GridBusqueda => dgvManoObra;

        public bool GenerarReporteExcel()
        {
            ExportarCatalogoExcel();
            return true;
        }

        public void GenerarPdfCatalogoManoObra()
        {
            var lista = dgvManoObra.DataSource as List<ManoDeObra>;
            if (lista == null || !lista.Any())
            {
                MessageBox.Show("No hay registros para exportar.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlgTipo = new FormSeleccionReporteMO();
            if (dlgTipo.ShowDialog(this) != DialogResult.OK) return;

            var svcRep = new ReporteService(_context);
            var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
            Proyecto proyecto = _proyectoId.HasValue ? _context.Proyectos.Find(_proyectoId.Value) : new Proyecto { Nombre = "Mano de Obra" };
            if (proyecto == null)
                proyecto = new Proyecto { Nombre = "Mano de Obra" };

            var tituloCfg = _proyectoId.HasValue
                ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoManoObra, lblTitulo.Text)
                : null;
            var gen = new GeneradorPdfCatalogoManoObra(svcRep);
            string ruta;
            if (dlgTipo.Seleccion == FormSeleccionReporteMO.TipoReporte.TabuladorFSR)
            {
                if (string.IsNullOrEmpty(proyecto.ParametrosFSR))
                {
                    MessageBox.Show(
                        "Este proyecto no tiene parámetros FSR configurados.Configure el FSR en el módulo correspondiente antes de generar este reporte.",
                        "Sin parámetros FSR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dlgPdfFsr = new SaveFileDialog
                {
                    Title = "Guardar tabulador FSR en PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"TabuladorFSR_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                };
                if (dlgPdfFsr.ShowDialog(this) != DialogResult.OK) return;

                ruta = gen.GenerarTabuladorFsr(proyecto, lista, plantilla, dlgPdfFsr.FileName);
            }
            else
            {
                using var dlgPdf = new SaveFileDialog
                {
                    Title = "Guardar catálogo de mano de obra en PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Catalogo_ManoObra_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                };
                if (dlgPdf.ShowDialog(this) != DialogResult.OK) return;

                ruta = gen.GenerarCatalogo(proyecto, lista, plantilla, _columnasConfig, dlgPdf.FileName, tituloCfg);
            }

            if (MessageBox.Show("Reporte PDF generado exitosamente.¿Desea abrirlo?", "PDF generado",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
            }
        }

        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaManoObra _colMORibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMORibbon == null) return;

            _colMORibbon.NombreFuente = fmt.NombreFuente;
            _colMORibbon.TamanoFuente = fmt.TamanoFuente;
            _colMORibbon.Negrita = fmt.Negrita;
            _colMORibbon.Cursiva = fmt.Cursiva;
            _colMORibbon.Alineacion = fmt.Alineacion;
            _colMORibbon.ColorFondo = fmt.ColorFondo;
            _colMORibbon.ColorFuente = fmt.ColorFuente;
            _colMORibbon.WrapTexto = fmt.WrapTexto;
            _colMORibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMORibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvManoObra.Columns)
            {
                if (col.Tag == _colMORibbon)
                {
                    AplicarEstiloDesdeColMO((DataGridViewTextBoxColumn)col, _colMORibbon);
                    break;
                }
            }
            dgvManoObra.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvManoObra.Columns)
            {
                if (col.Tag is not ColumnaManoObra colMO) continue;
                colMO.NombreFuente = fmt.NombreFuente;
                colMO.TamanoFuente = fmt.TamanoFuente;
                colMO.Negrita = fmt.Negrita;
                colMO.Cursiva = fmt.Cursiva;
                colMO.Alineacion = fmt.Alineacion;
                colMO.ColorFuente = fmt.ColorFuente;
                colMO.WrapTexto = fmt.WrapTexto;
                colMO.AlineacionVertical = fmt.AlineacionVertical;
                colMO.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMO((DataGridViewTextBoxColumn)col, colMO);
            }
            _context.SaveChanges();
            dgvManoObra.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMORibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvManoObra.Columns.Count)
            {
                var col = dgvManoObra.Columns[colIndex];
                if (col.Tag is ColumnaManoObra cm)
                {
                    _colMORibbon = cm;
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
        public FormCatalogoManoObra(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);
            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvManoObra.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<ManoDeObra>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoManoObra).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasManoObraHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            this.Load += (s, e) => CargarManoDeObra();

            dgvManoObra.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvManoObra.ColumnWidthChanged += DgvManoObra_ColumnWidthChanged;
        }

        // ── Grid ──────────────────────────────────────────────────────────────
        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvManoObra.AutoGenerateColumns = false;
            dgvManoObra.AllowUserToAddRows = false;
            dgvManoObra.AllowUserToDeleteRows = false;
            dgvManoObra.ReadOnly = true;
            dgvManoObra.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvManoObra, conMenuCopia: false);
            dgvManoObra.CellFormatting += DgvManoObra_CellFormatting;
            dgvManoObra.KeyDown += DgvManoObra_KeyDown;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Cat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Cat;
            dgvManoObra.CellMouseDown += DgvManoObra_CellMouseDown;
            dgvManoObra.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvManoObra.BackgroundColor = Color.White;
            dgvManoObra.BorderStyle = BorderStyle.None;
            dgvManoObra.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvManoObra.GridColor = Color.FromArgb(230, 230, 230);
            dgvManoObra.ColumnHeadersHeight = 40;
            dgvManoObra.RowTemplate.Height = 35;

            dgvManoObra.EnableHeadersVisualStyles = false;
            dgvManoObra.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvManoObra.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvManoObra.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvManoObra.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvManoObra.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvManoObra.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvManoObra.ScrollBars = ScrollBars.Both;

            dgvManoObra.Columns.Clear();

            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = cfg.NombreInterno == "Origen" ? null : cfg.NombreInterno,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvManoObra.Columns.Add(col);
                    AplicarEstiloDesdeColMO(col, cfg);
                }

                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn
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
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = "Clave", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = "Unidad", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSalarioBase", HeaderText = "Salario Base", DataPropertyName = "SalarioBase", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFSR", HeaderText = "FSR", DataPropertyName = "FactorSalarioReal", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N4" } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSalarioReal", HeaderText = "Salario Real", DataPropertyName = "SalarioReal", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn
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

        private void DgvManoObra_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaManoObra cfg) return;

                var columnaDb = _context.ColumnasManoObra.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en mano de obra: {ex.Message}");
            }
        }

        private int DecimalesImporte => ProyectoActual?.DecimalesImporte ?? 2;

        private void DgvManoObra_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvManoObra.Rows[e.RowIndex].DataBoundItem is not ManoDeObra mo) return;
            var colName = dgvManoObra.Columns[e.ColumnIndex].Name;
            if (colName == "col_Origen" || colName == "colOrigen")
            {
                e.Value = ImportOriginStampService.BuildOriginDisplay(mo.Notas, mo.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
                e.FormattingApplied = true;
                return;
            }
            if ((colName == "colSalarioBase" || colName == "col_SalarioBase" ||
                 colName == "colSalarioReal" || colName == "col_SalarioReal") && e.Value is decimal sal)
            {
                e.Value = sal.ToString($"C{DecimalesImporte}", System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private static void AplicarEstiloDesdeColMO(DataGridViewTextBoxColumn dgvCol, ColumnaManoObra col)
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
                dgvCol.DefaultCellStyle.Format = col.FormatoNumerico ?? "";
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
        public void RecargarCatalogo() => CargarManoDeObra();

        private async void CargarManoDeObra()
        {
            try
            {
                btnRefrescar.Enabled = false;
                lblStatus.Text = "Cargando mano de obra...";
                var _gridState = DataGridViewStateHelper.Capture(dgvManoObra);

                var lista = await _catalogLoadService.LoadManoDeObraAsync(_context, new CatalogFilterInput
                {
                    ProyectoId = _proyectoId,
                    SoloProyecto = chkSoloProyecto.Checked,
                    SoloMaestros = chkSoloMaestros.Checked,
                    SearchText = txtBuscar.Text
                });

                SuspendLayout();
                using (GridRedrawHelper.Suspend(dgvManoObra))
                {
                    dgvManoObra.DataSource = null;
                    dgvManoObra.DataSource = lista;
                    DataGridViewStateHelper.Restore(dgvManoObra, _gridState);
                }
                ResumeLayout();
                lblStatus.Text = $"{lista.Count} registro(s) encontrado(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar mano de obra:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error al cargar mano de obra";
            }
            finally
            {
                btnRefrescar.Enabled = true;
            }
        }

        // ── Botones CRUD ──────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, EventArgs e)
        {
            using var form = new FormEditarManoObra(_context, _proyectoId, proyecto: ProyectoActual);
            if (form.ShowDialog() == DialogResult.OK) CargarManoDeObra();
        }

        private void btnEditar_Click(object sender, EventArgs e)
        {
            if (_manoDeObraSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para editar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _manoDeObraSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show(
                    "No puede editar registros del catálogo maestro desde un proyecto.\n\n" +
                    "Para modificar este registro, ábralo desde Catálogos Maestros o cree una copia para este proyecto.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new FormEditarManoObra(_context, _proyectoId, _manoDeObraSeleccionada, ProyectoActual);
            if (form.ShowDialog() == DialogResult.OK) CargarManoDeObra();
        }

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_manoDeObraSeleccionada == null)
            {
                MessageBox.Show("Seleccione un registro para eliminar.", "Selección Requerida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_proyectoId.HasValue && _manoDeObraSeleccionada.Origen == OrigenInsumo.Maestro)
            {
                MessageBox.Show("No puede eliminar registros del catálogo maestro desde un proyecto.",
                    "Registro del Catálogo Maestro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(
                $"¿Está seguro de eliminar el registro?\n\nClave: {_manoDeObraSeleccionada.Clave}\n" +
                $"Descripción: {_manoDeObraSeleccionada.Descripcion}\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    var compsMO = _context.ComponentesMatriz
                        .Where(c => c.ManoDeObraId == _manoDeObraSeleccionada.Id).ToList();
                    if (compsMO.Any())
                    {
                        int nm = compsMO.Select(c => c.MatrizId).Distinct().Count();
                        if (MessageBox.Show(
                            $"Este insumo está usado en {compsMO.Count} componente(s) de {nm} matriz/matrices.\n\n"
                            + "Al eliminarlo, esos componentes también serán eliminados.\n\n¿Desea continuar?",
                            "Insumo en uso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                        _context.ComponentesMatriz.RemoveRange(compsMO);
                    }

                    // Primero borrar el insumo y guardar
                    var enBDMO = _context.ManoDeObra.Find(_manoDeObraSeleccionada.Id);
                    if (enBDMO != null) _context.ManoDeObra.Remove(enBDMO);
                    _context.SaveChanges();

                    // Ahora propagar a matrices afectadas (el contexto ya refleja el estado real)
                    if (compsMO.Any())
                    {
                        var matrizIds = compsMO.Select(c => c.MatrizId).Distinct().ToList();
                        RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(_context, matrizIds);
                        OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                    }

                    InsumosModificados?.Invoke(null, EventArgs.Empty);
                    MessageBox.Show("Registro eliminado exitosamente.", "Eliminado",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    CargarManoDeObra();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar el registro:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnRefrescar_Click(object sender, EventArgs e) => CargarManoDeObra();
        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void dgvManoObra_SelectionChanged(object sender, EventArgs e)
        {
            _manoDeObraSeleccionada = dgvManoObra.SelectedRows.Count > 0
                ? dgvManoObra.SelectedRows[0].DataBoundItem as ManoDeObra
                : null;
            ActualizarEstadoBotones();
        }

        private void dgvManoObra_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEditar_Click(sender, e);
        }

        private void txtBuscar_TextChanged(object sender, EventArgs e) => CargarManoDeObra();

        private void chkSoloProyecto_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloProyecto.Checked) chkSoloMaestros.Checked = false;
            CargarManoDeObra();
        }

        private void chkSoloMaestros_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSoloMaestros.Checked) chkSoloProyecto.Checked = false;
            CargarManoDeObra();
        }

        private void ActualizarEstadoBotones()
        {
            var haySeleccion = _manoDeObraSeleccionada != null;
            var esMaestro = haySeleccion && _manoDeObraSeleccionada.Origen == OrigenInsumo.Maestro;
            var enProyecto = _proyectoId.HasValue;

            btnEditar.Enabled = haySeleccion && !(enProyecto && esMaestro);
            btnEliminar.Enabled = haySeleccion && !(enProyecto && esMaestro);
        }

        private void btnExportarExcel_Click(object sender, EventArgs e) => ExportarCatalogoExcel();

        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Función de importación desde Excel próximamente...",
                "En Desarrollo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Exportar Excel ────────────────────────────────────────────────────
        private void ExportarCatalogoExcel()
        {
            try
            {
                var lista = dgvManoObra.DataSource as List<ManoDeObra>;
                if (lista == null || !lista.Any())
                {
                    MessageBox.Show("No hay registros para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Selección de tipo de reporte
                using var dlgTipo = new FormSeleccionReporteMO();
                if (dlgTipo.ShowDialog(this) != DialogResult.OK) return;

                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Mano de Obra" };

                // Reporte tabulador desglosado FSR
                if (dlgTipo.Seleccion == FormSeleccionReporteMO.TipoReporte.TabuladorFSR)
                {
                    if (proyecto == null || string.IsNullOrEmpty(proyecto.ParametrosFSR))
                    {
                        MessageBox.Show(
                            "Este proyecto no tiene parámetros FSR configurados.\n\n" +
                            "Configure el FSR en el módulo correspondiente antes de generar este reporte.",
                            "Sin parámetros FSR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    using var dlgFSR = new SaveFileDialog
                    {
                        Title = "Guardar tabulador FSR",
                        Filter = "Excel (*.xlsx)|*.xlsx",
                        FileName = $"TabuladorFSR_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    };
                    if (dlgFSR.ShowDialog() != DialogResult.OK) return;

                    using var wbFSR = new XLWorkbook();
                    GeneradorExcelFSR.GenerarAE2C(wbFSR, proyecto, lista, plantilla, svcRep);
                    wbFSR.SaveAs(dlgFSR.FileName);

                    if (MessageBox.Show("Reporte generado exitosamente.\n¿Desea abrirlo?",
                        "Reporte generado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlgFSR.FileName) { UseShellExecute = true });
                    return;
                }

                // Reporte Catálogo (flujo original)
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de mano de obra",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Catalogo_ManoObra_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Mano de Obra");

                var colsVis = _columnasConfig.Where(c => c.Visible).ToList();
                int numCols = colsVis.Any() ? colsVis.Count : 7;

                // ── Encabezado + Título ─────────────────────────────────
                int fila = 1;
                if (_proyectoId.HasValue)
                    fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, svcRep);

                var titulo = ws.Range(fila, 1, fila, numCols);
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoManoObra, lblTitulo.Text)
                    : null;
                ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "CATÁLOGO DE MANO DE OBRA");
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
                    string[] hdrs = { "Clave", "Descripción", "Unidad", "Salario Base", "FSR", "Salario Real", "Origen" };
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
                foreach (var mo in lista)
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
                                case "Clave": cell.Value = mo.Clave ?? ""; break;
                                case "Descripcion": cell.Value = mo.Descripcion ?? ""; break;
                                case "Unidad": cell.Value = mo.Unidad ?? ""; break;
                                case "SalarioBase":
                                    cell.Value = mo.SalarioBase;
                                    cell.Style.NumberFormat.Format = "#,##0.00";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    break;
                                case "FactorSalarioReal":
                                    cell.Value = mo.FactorSalarioReal;
                                    cell.Style.NumberFormat.Format = "0.0000";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    break;
                                case "SalarioReal":
                                    cell.Value = mo.SalarioReal;
                                    cell.Style.NumberFormat.Format = "#,##0.00";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    break;
                                case "Origen":
                                    cell.Value = mo.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                    break;
                            }
                        }
                    }
                    else
                    {
                        ws.Cell(fila, 1).Value = mo.Clave ?? "";
                        ws.Cell(fila, 2).Value = mo.Descripcion ?? "";
                        ws.Cell(fila, 3).Value = mo.Unidad ?? "";
                        ws.Cell(fila, 4).Value = mo.SalarioBase; ws.Cell(fila, 4).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(fila, 5).Value = mo.FactorSalarioReal; ws.Cell(fila, 5).Style.NumberFormat.Format = "0.0000";
                        ws.Cell(fila, 6).Value = mo.SalarioReal; ws.Cell(fila, 6).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(fila, 7).Value = mo.Origen == OrigenInsumo.Maestro ? "Maestro" : "Proyecto";
                        ws.Range(fila, 1, fila, 7).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
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
        private void DgvManoObra_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvManoObra.Rows[e.RowIndex];
            if (!clickedRow.Selected)
            {
                dgvManoObra.ClearSelection();
                clickedRow.Selected = true;
            }

            if (e.ColumnIndex >= 0)
                dgvManoObra.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _manoDeObraSeleccionada = clickedRow.DataBoundItem as ManoDeObra;
            if (_manoDeObraSeleccionada == null) return;

            bool puedeEditar = !(_proyectoId.HasValue && _manoDeObraSeleccionada?.Origen == OrigenInsumo.Maestro);

            var menu = new ContextMenuStrip();

            var itemEditar = menu.Items.Add("✏️  Editar");
            itemEditar.Enabled = puedeEditar;
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var itemEliminar = menu.Items.Add("🗑️  Eliminar");
            itemEliminar.Enabled = puedeEditar;
            itemEliminar.Click += (_, __) => btnEliminar_Click(sender, EventArgs.Empty);

            menu.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            var matrices = BuscarMatricesDondeSeUsa_ManoDeObra(_manoDeObraSeleccionada.Id);

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
                    item.Click += (_, __) => AbrirEditorMatriz_ManoDeObra(cap);
                }
            }
            menu.Items.Add(itemDonde);
            menu.Items.Add(new ToolStripSeparator());

            var itemCopiarCelda = menu.Items.Add("📋  Copiar celda");
            itemCopiarCelda.Click += (_, __) =>
            {
                var cell = dgvManoObra.CurrentCell;
                if (cell != null)
                {
                    string txt = cell.FormattedValue?.ToString() ?? "";
                    if (txt.Length > 0) Clipboard.SetText(txt);
                }
            };

            int _nFilas = dgvManoObra.SelectedRows.Count;
            string _labelFila = _nFilas > 1 ? $"📄  Copiar {_nFilas} filas" : "📄  Copiar fila";
            var itemCopiarFila = menu.Items.Add(_labelFila);
            itemCopiarFila.Click += (_, __) =>
            {
                var cols = Enumerable.Range(0, dgvManoObra.ColumnCount)
                    .Where(i => dgvManoObra.Columns[i].Visible)
                    .OrderBy(i => dgvManoObra.Columns[i].DisplayIndex).ToList();
                var sb = new System.Text.StringBuilder();
                foreach (DataGridViewRow r in dgvManoObra.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
                    sb.AppendLine(string.Join("\t", cols.Select(i => r.Cells[i].FormattedValue?.ToString() ?? "")));
                if (sb.Length > 0) Clipboard.SetText(sb.ToString().TrimEnd());
            };

            menu.Show(dgvManoObra, dgvManoObra.PointToClient(Cursor.Position));
        }

        private List<Matriz> BuscarMatricesDondeSeUsa_ManoDeObra(int insumoId)
        {
            var matrizIds = _context.Set<ComponenteMatriz>()
                .Where(c => c.ManoDeObraId == insumoId)
                .Select(c => c.MatrizId)
                .Distinct()
                .ToList();

            if (matrizIds.Count == 0) return new List<Matriz>();

            return _context.Matrices
                .Where(m => matrizIds.Contains(m.Id) && (_proyectoId == null || m.ProyectoId == _proyectoId))
                .OrderBy(m => m.Clave)
                .ToList();
        }

        private void AbrirEditorMatriz_ManoDeObra(Matriz matriz)
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

            using var form = new FormColumnasAPU(_context, _proyectoId.Value, FormColumnasAPU.ModoColumnas.ManoObra);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasManoObraHelper.ObtenerColumnas(_context, _proyectoId.Value);
                ConfigurarGrid();
                CargarManoDeObra();
            }
        }


        private static double CalcularAlturaFilaCatalogo(List<ColumnaManoObra> colsVis, int fila, IXLWorksheet ws, double alturaBase)
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
        public void RecalcularTodo() => CargarManoDeObra();

        private void OnDecimalesActualizados_Cat(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) dgvManoObra.Refresh(); }));
        }

        private void DgvManoObra_KeyDown(object sender, KeyEventArgs e)
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
