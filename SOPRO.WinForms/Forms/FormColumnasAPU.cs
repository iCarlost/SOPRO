using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormColumnasAPU : Form
    {
        public enum ModoColumnas
        {
            Matrices,
            Materiales,
            ManoObra,
            Herramientas,
            Maquinaria,
            ProgramaObra,
            ProgramaInsumos,
            Financiamiento
        }

        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly ModoColumnas _modo;
        private bool _cargandoFormato = false;
        private bool _recargandoGrid = false;

        public bool CambiosRealizados { get; private set; }

        public FormColumnasAPU(SOPROContext context, int proyectoId)
            : this(context, proyectoId, ModoColumnas.Matrices)
        {
        }

        public FormColumnasAPU(SOPROContext context, int proyectoId, ModoColumnas modo)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _modo = modo;
            InitializeComponent();
            Shown += (_, __) => splitMain.SplitterDistance = splitMain.Width - 310;
            lblTitulo.Text = _modo switch
            {
                ModoColumnas.Matrices => "Configuración de columnas - Matrices",
                ModoColumnas.Materiales => "Configuración de columnas - Materiales",
                ModoColumnas.ManoObra => "Configuración de columnas - Mano de Obra",
                ModoColumnas.Herramientas => "Configuración de columnas - Herramientas",
                ModoColumnas.Maquinaria => "Configuración de columnas - Maquinaria",
                ModoColumnas.ProgramaObra => "Configuración de columnas - Programa de Obra",
                ModoColumnas.ProgramaInsumos => "Configuración de columnas - Programa de Insumos",
                ModoColumnas.Financiamiento => "Configuración de columnas - Financiamiento",
                _ => "Configuración de columnas"
            };
            CargarFuentes();
            CargarColumnas();
        }

        private void CargarFuentes()
        {
            cboFuente.Items.Clear();
            cboFuenteContenido.Items.Clear();
            using var familias = new System.Drawing.Text.InstalledFontCollection();
            foreach (var f in familias.Families.OrderBy(f => f.Name))
            {
                cboFuente.Items.Add(f.Name);
                cboFuenteContenido.Items.Add(f.Name);
            }
        }

        private void CargarColumnas()
        {
            _recargandoGrid = true;
            dgvColumnas.Rows.Clear();
            var columnas = _modo switch
            {
                ModoColumnas.Matrices => ColumnasMatrizHelper.ObtenerColumnas(_context, _proyectoId).Cast<object>().ToList(),
                ModoColumnas.Materiales => ColumnasMaterialHelper.ObtenerColumnas(_context, _proyectoId).Cast<object>().ToList(),
                ModoColumnas.ManoObra => ColumnasManoObraHelper.ObtenerColumnas(_context, _proyectoId).Cast<object>().ToList(),
                ModoColumnas.Herramientas => ColumnasHerramientaHelper.ObtenerColumnas(_context, _proyectoId).Cast<object>().ToList(),
                ModoColumnas.Maquinaria => ColumnasMaquinariaHelper.ObtenerColumnas(_context, _proyectoId).Cast<object>().ToList(),
                ModoColumnas.ProgramaObra => ColumnasProgramaObraHelper.ObtenerColumnas(_context, _proyectoId).Cast<object>().ToList(),
                ModoColumnas.ProgramaInsumos => ColumnasProgramaInsumosHelper.ObtenerColumnas(_context, _proyectoId).Cast<object>().ToList(),
                ModoColumnas.Financiamiento => ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyectoId).Cast<object>().ToList(),
                _ => Enumerable.Empty<object>().ToList()
            };

            foreach (var item in columnas.OrderBy(GetOrden))
            {
                int idx = dgvColumnas.Rows.Add(GetId(item), GetNombreInterno(item), GetNombre(item), GetOrden(item), GetAncho(item), GetVisible(item));
                dgvColumnas.Rows[idx].Tag = item;

                var color = GetColorFondo(item);
                if (!string.IsNullOrWhiteSpace(color) && color != "#FFFFFF")
                {
                    try { dgvColumnas.Rows[idx].DefaultCellStyle.BackColor = ColorTranslator.FromHtml(color); } catch { }
                }
                if (!GetVisible(item))
                    dgvColumnas.Rows[idx].DefaultCellStyle.ForeColor = Color.Gray;
            }

            lblStatus.Text = $"{columnas.Count} columnas  |  {columnas.Count(c => GetVisible(c))} visibles";
            if (dgvColumnas.Rows.Count > 0)
                dgvColumnas.Rows[0].Selected = true;

            _recargandoGrid = false;
        }

        private static int GetId(object item) => item switch
        {
            ColumnaMatriz c => c.Id,
            ColumnaMaterial c => c.Id,
            ColumnaManoObra c => c.Id,
            ColumnaHerramienta c => c.Id,
            ColumnaMaquinaria c => c.Id,
            ColumnaProgramaObra c => c.Id,
            ColumnaProgramaInsumos c => c.Id,
            ColumnaFinanciamiento c => c.Id,
            _ => 0
        };
        private static string GetNombreInterno(object item) => item switch
        {
            ColumnaMatriz c => c.NombreInterno,
            ColumnaMaterial c => c.NombreInterno,
            ColumnaManoObra c => c.NombreInterno,
            ColumnaHerramienta c => c.NombreInterno,
            ColumnaMaquinaria c => c.NombreInterno,
            ColumnaProgramaObra c => c.NombreInterno,
            ColumnaProgramaInsumos c => c.NombreInterno,
            ColumnaFinanciamiento c => c.NombreInterno,
            _ => string.Empty
        };
        private static string GetNombre(object item) => item switch
        {
            ColumnaMatriz c => c.Nombre,
            ColumnaMaterial c => c.Nombre,
            ColumnaManoObra c => c.Nombre,
            ColumnaHerramienta c => c.Nombre,
            ColumnaMaquinaria c => c.Nombre,
            ColumnaProgramaObra c => c.Nombre,
            ColumnaProgramaInsumos c => c.Nombre,
            ColumnaFinanciamiento c => c.Nombre,
            _ => string.Empty
        };
        private static int GetOrden(object item) => item switch
        {
            ColumnaMatriz c => c.Orden,
            ColumnaMaterial c => c.Orden,
            ColumnaManoObra c => c.Orden,
            ColumnaHerramienta c => c.Orden,
            ColumnaMaquinaria c => c.Orden,
            ColumnaProgramaObra c => c.Orden,
            ColumnaProgramaInsumos c => c.Orden,
            ColumnaFinanciamiento c => c.Orden,
            _ => 0
        };
        private static int GetAncho(object item) => item switch
        {
            ColumnaMatriz c => c.AnchoColumna,
            ColumnaMaterial c => c.AnchoColumna,
            ColumnaManoObra c => c.AnchoColumna,
            ColumnaHerramienta c => c.AnchoColumna,
            ColumnaMaquinaria c => c.AnchoColumna,
            ColumnaProgramaObra c => c.AnchoColumna,
            ColumnaProgramaInsumos c => c.AnchoColumna,
            ColumnaFinanciamiento c => c.AnchoColumna,
            _ => 100
        };
        private static bool GetVisible(object item) => item switch
        {
            ColumnaMatriz c => c.Visible,
            ColumnaMaterial c => c.Visible,
            ColumnaManoObra c => c.Visible,
            ColumnaHerramienta c => c.Visible,
            ColumnaMaquinaria c => c.Visible,
            ColumnaProgramaObra c => c.Visible,
            ColumnaProgramaInsumos c => c.Visible,
            ColumnaFinanciamiento c => c.Visible,
            _ => true
        };
        private static string GetColorFondo(object item) => item switch
        {
            ColumnaMatriz c => c.ColorFondo,
            ColumnaMaterial c => c.ColorFondo,
            ColumnaManoObra c => c.ColorFondo,
            ColumnaHerramienta c => c.ColorFondo,
            ColumnaMaquinaria c => c.ColorFondo,
            ColumnaProgramaObra c => c.ColorFondo,
            ColumnaProgramaInsumos c => c.ColorFondo,
            ColumnaFinanciamiento c => c.ColorFondo,
            _ => "#FFFFFF"
        };
        private static string GetNombreFuente(object item) => item switch
        {
            ColumnaMatriz c => c.NombreFuente,
            ColumnaMaterial c => c.NombreFuente,
            ColumnaManoObra c => c.NombreFuente,
            ColumnaHerramienta c => c.NombreFuente,
            ColumnaMaquinaria c => c.NombreFuente,
            ColumnaProgramaObra c => c.NombreFuente,
            ColumnaProgramaInsumos c => c.NombreFuente,
            ColumnaFinanciamiento c => c.NombreFuente,
            _ => "Segoe UI"
        };
        private static decimal GetTamanoFuente(object item) => item switch
        {
            ColumnaMatriz c => c.TamanoFuente,
            ColumnaMaterial c => c.TamanoFuente,
            ColumnaManoObra c => c.TamanoFuente,
            ColumnaHerramienta c => c.TamanoFuente,
            ColumnaMaquinaria c => c.TamanoFuente,
            ColumnaProgramaObra c => c.TamanoFuente,
            ColumnaProgramaInsumos c => c.TamanoFuente,
            ColumnaFinanciamiento c => c.TamanoFuente,
            _ => 9
        };
        private static bool GetNegrita(object item) => item switch
        {
            ColumnaMatriz c => c.Negrita,
            ColumnaMaterial c => c.Negrita,
            ColumnaManoObra c => c.Negrita,
            ColumnaHerramienta c => c.Negrita,
            ColumnaMaquinaria c => c.Negrita,
            ColumnaProgramaObra c => c.Negrita,
            ColumnaProgramaInsumos c => c.Negrita,
            ColumnaFinanciamiento c => c.Negrita,
            _ => false
        };
        private static string GetColorFuente(object item) => item switch
        {
            ColumnaMatriz c => c.ColorFuente,
            ColumnaMaterial c => c.ColorFuente,
            ColumnaManoObra c => c.ColorFuente,
            ColumnaHerramienta c => c.ColorFuente,
            ColumnaMaquinaria c => c.ColorFuente,
            ColumnaProgramaObra c => c.ColorFuente,
            ColumnaProgramaInsumos c => c.ColorFuente,
            ColumnaFinanciamiento c => c.ColorFuente,
            _ => "#000000"
        };
        private static AlineacionColumna GetAlineacion(object item) => item switch
        {
            ColumnaMatriz c => c.Alineacion,
            ColumnaMaterial c => c.Alineacion,
            ColumnaManoObra c => c.Alineacion,
            ColumnaHerramienta c => c.Alineacion,
            ColumnaMaquinaria c => c.Alineacion,
            ColumnaProgramaObra c => c.Alineacion,
            ColumnaProgramaInsumos c => c.Alineacion,
            ColumnaFinanciamiento c => c.Alineacion,
            _ => AlineacionColumna.Izquierda
        };

        private void dgvColumnas_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvColumnas.SelectedRows.Count == 0) return;
            var item = dgvColumnas.SelectedRows[0].Tag;
            if (item == null) return;

            lblColumnaSeleccionada.Text = $"Formato de: {GetNombre(item)}";
            CargarFormatoEnPanel(item);
            panelFormato.Enabled = true;
        }

        private void CargarFormatoEnPanel(object item)
        {
            _cargandoFormato = true;
            int idx = cboFuente.FindStringExact(GetNombreFuente(item) ?? "Segoe UI");
            cboFuente.SelectedIndex = idx >= 0 ? idx : 0;
            int idxCont = cboFuenteContenido.FindStringExact(GetNombreFuente(item) ?? "Segoe UI");
            cboFuenteContenido.SelectedIndex = idxCont >= 0 ? idxCont : 0;
            nudTamaño.Value = GetTamanoFuente(item) > 0 ? GetTamanoFuente(item) : 9;
            nudTamañoContenido.Value = GetTamanoFuente(item) > 0 ? GetTamanoFuente(item) : 9;
            chkNegrita.Checked = GetNegrita(item);
            var idxAlineacion = Math.Max(0, Math.Min(2, (int)GetAlineacion(item)));
            cboAlineacion.SelectedIndex = idxAlineacion;
            btnColorFondo.BackColor = TryParseColor(GetColorFondo(item), Color.White);
            btnColorTexto.BackColor = TryParseColor(GetColorFuente(item), Color.Black);
            btnColorFondoContenido.BackColor = TryParseColor(GetColorFondo(item), Color.White);
            btnColorTextoContenido.BackColor = TryParseColor(GetColorFuente(item), Color.Black);
            ActualizarPreview();
            _cargandoFormato = false;
        }

        private void ActualizarPreview()
        {
            try
            {
                string fuente = cboFuente.SelectedItem?.ToString() ?? "Segoe UI";
                float tam = (float)nudTamaño.Value;
                FontStyle fs = chkNegrita.Checked ? FontStyle.Bold : FontStyle.Regular;
                lblPreviewEnc.Font = new Font(fuente, tam, fs);
                lblPreviewEnc.ForeColor = btnColorTexto.BackColor;
                lblPreviewEnc.BackColor = btnColorFondo.BackColor;

                string fuenteCon = cboFuenteContenido.SelectedItem?.ToString() ?? fuente;
                float tamCon = (float)nudTamañoContenido.Value;
                lblPreviewCon.Font = new Font(fuenteCon, tamCon, fs);
                lblPreviewCon.ForeColor = btnColorTextoContenido.BackColor;
                lblPreviewCon.BackColor = btnColorFondoContenido.BackColor;
            }
            catch { }
        }

        private void AplicarFormatoASeleccionada()
        {
            if (_cargandoFormato || dgvColumnas.SelectedRows.Count == 0) return;
            var item = dgvColumnas.SelectedRows[0].Tag;
            if (item == null) return;

            GuardarFormatoDesdePanel(item);
            _context.SaveChanges();
            CambiosRealizados = true;
            try { dgvColumnas.SelectedRows[0].DefaultCellStyle.BackColor = btnColorFondoContenido.BackColor; } catch { }
            ActualizarPreview();
        }

        private void GuardarFormatoDesdePanel(object item)
        {
            string fuente = cboFuenteContenido.SelectedItem?.ToString() ?? cboFuente.SelectedItem?.ToString() ?? "Segoe UI";
            int tam = (int)nudTamañoContenido.Value;
            bool negrita = chkNegrita.Checked;
            var alineacion = (AlineacionColumna)cboAlineacion.SelectedIndex;
            string colorFondo = $"#{btnColorFondoContenido.BackColor.R:X2}{btnColorFondoContenido.BackColor.G:X2}{btnColorFondoContenido.BackColor.B:X2}";
            string colorFuente = $"#{btnColorTextoContenido.BackColor.R:X2}{btnColorTextoContenido.BackColor.G:X2}{btnColorTextoContenido.BackColor.B:X2}";

            switch (item)
            {
                case ColumnaMatriz c:
                    c.NombreFuente = fuente; c.TamanoFuente = tam; c.Negrita = negrita; c.Cursiva = false; c.Alineacion = alineacion; c.ColorFondo = colorFondo; c.ColorFuente = colorFuente; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaMaterial c:
                    c.NombreFuente = fuente; c.TamanoFuente = tam; c.Negrita = negrita; c.Cursiva = false; c.Alineacion = alineacion; c.ColorFondo = colorFondo; c.ColorFuente = colorFuente; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaManoObra c:
                    c.NombreFuente = fuente; c.TamanoFuente = tam; c.Negrita = negrita; c.Cursiva = false; c.Alineacion = alineacion; c.ColorFondo = colorFondo; c.ColorFuente = colorFuente; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaHerramienta c:
                    c.NombreFuente = fuente; c.TamanoFuente = tam; c.Negrita = negrita; c.Cursiva = false; c.Alineacion = alineacion; c.ColorFondo = colorFondo; c.ColorFuente = colorFuente; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaMaquinaria c:
                    c.NombreFuente = fuente; c.TamanoFuente = tam; c.Negrita = negrita; c.Cursiva = false; c.Alineacion = alineacion; c.ColorFondo = colorFondo; c.ColorFuente = colorFuente; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaProgramaObra c:
                    c.NombreFuente = fuente; c.TamanoFuente = tam; c.Negrita = negrita; c.Cursiva = false; c.Alineacion = alineacion; c.ColorFondo = colorFondo; c.ColorFuente = colorFuente; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaProgramaInsumos c:
                    c.NombreFuente = fuente; c.TamanoFuente = tam; c.Negrita = negrita; c.Cursiva = false; c.Alineacion = alineacion; c.ColorFondo = colorFondo; c.ColorFuente = colorFuente; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaFinanciamiento c:
                    c.NombreFuente = fuente; c.TamanoFuente = tam; c.Negrita = negrita; c.Cursiva = false; c.Alineacion = alineacion; c.ColorFondo = colorFondo; c.ColorFuente = colorFuente; c.FechaModificacion = DateTime.Now;
                    break;
            }
        }

        private void GuardarCambiosDeFila(DataGridViewRow row)
        {
            if (row.Tag is not object item) return;
            string encabezado = Convert.ToString(row.Cells["colEncabezado"].Value)?.Trim() ?? GetNombre(item);
            int orden = ToInt(row.Cells["colOrden"].Value, GetOrden(item));
            int ancho = ToInt(row.Cells["colAncho"].Value, GetAncho(item));
            bool visible = ToBool(row.Cells["colVisible"].Value, GetVisible(item));

            switch (item)
            {
                case ColumnaMatriz c:
                    c.Nombre = encabezado; c.Orden = orden; c.AnchoColumna = Math.Max(40, ancho); c.Visible = visible; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaMaterial c:
                    c.Nombre = encabezado; c.Orden = orden; c.AnchoColumna = Math.Max(40, ancho); c.Visible = visible; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaManoObra c:
                    c.Nombre = encabezado; c.Orden = orden; c.AnchoColumna = Math.Max(40, ancho); c.Visible = visible; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaHerramienta c:
                    c.Nombre = encabezado; c.Orden = orden; c.AnchoColumna = Math.Max(40, ancho); c.Visible = visible; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaMaquinaria c:
                    c.Nombre = encabezado; c.Orden = orden; c.AnchoColumna = Math.Max(40, ancho); c.Visible = visible; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaProgramaObra c:
                    c.Nombre = encabezado; c.Orden = orden; c.AnchoColumna = Math.Max(40, ancho); c.Visible = visible; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaProgramaInsumos c:
                    c.Nombre = encabezado; c.Orden = orden; c.AnchoColumna = Math.Max(40, ancho); c.Visible = visible; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaFinanciamiento c:
                    c.Nombre = encabezado; c.Orden = orden; c.AnchoColumna = Math.Max(40, ancho); c.Visible = visible; c.FechaModificacion = DateTime.Now;
                    break;
            }
        }

        private static int ToInt(object value, int fallback) => int.TryParse(Convert.ToString(value), out var n) ? n : fallback;
        private static bool ToBool(object value, bool fallback) => value is bool b ? b : bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;

        private void ConfirmarEdicionPendiente()
        {
            try
            {
                if (dgvColumnas.IsCurrentCellInEditMode)
                    dgvColumnas.EndEdit();
            }
            catch { }
        }

        private void ReordenarSecuencial()
        {
            var filas = dgvColumnas.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .OrderBy(r => ToInt(r.Cells["colOrden"].Value, int.MaxValue))
                .ThenBy(r => Convert.ToString(r.Cells["colNombreInterno"].Value))
                .ToList();

            for (int i = 0; i < filas.Count; i++)
            {
                filas[i].Cells["colOrden"].Value = i + 1;
                GuardarCambiosDeFila(filas[i]);
            }
            _context.SaveChanges();
            CambiosRealizados = true;
            CargarColumnas();
        }

        private void btnRestaurar_Click(object sender, EventArgs e)
        {
            if (dgvColumnas.SelectedRows.Count == 0) return;
            var item = dgvColumnas.SelectedRows[0].Tag;
            if (item == null) return;

            switch (item)
            {
                case ColumnaMatriz c:
                    c.NombreFuente = "Segoe UI"; c.TamanoFuente = 9; c.Negrita = false; c.Cursiva = false; c.Alineacion = AlineacionColumna.Izquierda; c.ColorFondo = "#FFFFFF"; c.ColorFuente = "#000000"; c.FechaModificacion = DateTime.Now;
                    break;
                case ColumnaMaterial c:
                    c.NombreFuente = "Segoe UI"; c.TamanoFuente = 9; c.Negrita = false; c.Cursiva = false; c.Alineacion = AlineacionColumna.Izquierda; c.ColorFondo = "#FFFFFF"; c.ColorFuente = "#000000"; c.FechaModificacion = DateTime.Now;
                    break;
            }
            _context.SaveChanges();
            CambiosRealizados = true;
            CargarFormatoEnPanel(item);
        }

        private void DgvColumnas_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvColumnas.IsCurrentCellDirty)
                dgvColumnas.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void DgvColumnas_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_recargandoGrid || e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var nombreColumna = dgvColumnas.Columns[e.ColumnIndex].Name;
            if (nombreColumna != "colVisible") return;

            GuardarFilaSinRecargar(e.RowIndex, recargarSiOrden: false);
        }

        private void DgvColumnas_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_recargandoGrid || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            GuardarFilaSinRecargar(e.RowIndex, recargarSiOrden: true, columnaEditada: dgvColumnas.Columns[e.ColumnIndex].Name);
        }

        private void GuardarFilaSinRecargar(int rowIndex, bool recargarSiOrden, string columnaEditada = null)
        {
            try
            {
                var row = dgvColumnas.Rows[rowIndex];
                GuardarCambiosDeFila(row);

                if (recargarSiOrden && string.Equals(columnaEditada, "colOrden", StringComparison.Ordinal))
                {
                    ReordenarSecuencial();
                    return;
                }

                _context.SaveChanges();
                CambiosRealizados = true;
                ActualizarEstadoFila(row);
                ActualizarStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar configuración de columnas:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ActualizarEstadoFila(DataGridViewRow row)
        {
            if (row?.Tag == null) return;

            bool visible = GetVisible(row.Tag);
            row.DefaultCellStyle.ForeColor = visible ? Color.Black : Color.Gray;

            var color = GetColorFondo(row.Tag);
            try
            {
                row.DefaultCellStyle.BackColor = string.IsNullOrWhiteSpace(color) || color == "#FFFFFF"
                    ? Color.White
                    : ColorTranslator.FromHtml(color);
            }
            catch
            {
                row.DefaultCellStyle.BackColor = Color.White;
            }
        }

        private void ActualizarStatus()
        {
            var total = dgvColumnas.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow);
            var visibles = dgvColumnas.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow && ToBool(r.Cells["colVisible"].Value, false));
            lblStatus.Text = $"{total} columnas  |  {visibles} visibles";
        }

        private void cboFuente_SelectedIndexChanged(object sender, EventArgs e) => AplicarFormatoASeleccionada();
        private void nudTamaño_ValueChanged(object sender, EventArgs e) => AplicarFormatoASeleccionada();
        private void chkNegrita_CheckedChanged(object sender, EventArgs e) => AplicarFormatoASeleccionada();
        private void cboFuenteContenido_SelectedIndexChanged(object sender, EventArgs e) => AplicarFormatoASeleccionada();
        private void nudTamañoContenido_ValueChanged(object sender, EventArgs e) => AplicarFormatoASeleccionada();
        private void cboAlineacion_SelectedIndexChanged(object sender, EventArgs e) => AplicarFormatoASeleccionada();
        private void btnColorFondo_Click(object sender, EventArgs e) => SeleccionarColor(btnColorFondo, AplicarFormatoASeleccionada);
        private void btnColorTexto_Click(object sender, EventArgs e) => SeleccionarColor(btnColorTexto, AplicarFormatoASeleccionada);
        private void btnColorFondoContenido_Click(object sender, EventArgs e) => SeleccionarColor(btnColorFondoContenido, AplicarFormatoASeleccionada);
        private void btnColorTextoContenido_Click(object sender, EventArgs e) => SeleccionarColor(btnColorTextoContenido, AplicarFormatoASeleccionada);

        private void SeleccionarColor(Button btn, Action despues)
        {
            using var dlg = new ColorDialog { Color = btn.BackColor };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                btn.BackColor = dlg.Color;
                despues();
            }
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            DialogResult = CambiosRealizados ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }

        private static Color TryParseColor(string html, Color fallback)
        {
            try { return string.IsNullOrWhiteSpace(html) ? fallback : ColorTranslator.FromHtml(html); }
            catch { return fallback; }
        }
    }
}
