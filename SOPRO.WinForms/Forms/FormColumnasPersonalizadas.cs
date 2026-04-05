using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormColumnasPersonalizadas : Form
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly int _columnaActualIndex;
        private bool _cargandoFormato = false;

        public bool CambiosRealizados { get; private set; }

        public FormColumnasPersonalizadas(SOPROContext context, int proyectoId, int columnaActualIndex = -1)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _columnaActualIndex = columnaActualIndex;
            InitializeComponent();
            this.Load    += (s, e) => { };   // reservado
            this.Shown   += (s, e) => splitMain.SplitterDistance = splitMain.Width - 300;
            CargarFuentes();
            CargarColumnas();
        }

        private void CargarFuentes()
        {
            cboFuente.Items.Clear();
            // Cargar todas las fuentes instaladas en el sistema
            using var familias = new System.Drawing.Text.InstalledFontCollection();
            foreach (var familia in familias.Families.OrderBy(f => f.Name))
                cboFuente.Items.Add(familia.Name);
        }

        private void CargarColumnas()
        {
            try
            {
                dgvColumnas.Rows.Clear();
                var columnas = _context.ColumnasPersonalizadas
                    .Where(c => c.ProyectoId == _proyectoId)
                    .OrderBy(c => c.Visible ? 0 : 1).ThenBy(c => c.Orden)
                    .ToList();

                foreach (var col in columnas)
                {
                    int idx = dgvColumnas.Rows.Add(col.Id, col.Nombre, col.TipoDato.ToString(), col.AnchoColumna, col.Visible);
                    dgvColumnas.Rows[idx].Tag = col;
                    if (!string.IsNullOrEmpty(col.ColorFondo) && col.ColorFondo != "#FFFFFF")
                    {
                        try { dgvColumnas.Rows[idx].DefaultCellStyle.BackColor = ColorTranslator.FromHtml(col.ColorFondo); }
                        catch { }
                    }
                    if (!col.Visible)
                        dgvColumnas.Rows[idx].DefaultCellStyle.ForeColor = Color.Gray;
                }

                lblStatus.Text = $"{columnas.Count} columnas  |  {columnas.Count(c => c.Visible)} visibles";
                if (dgvColumnas.Rows.Count > 0) dgvColumnas.Rows[0].Selected = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar columnas:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarFormatoEnPanel(ColumnaPersonalizada col)
        {
            _cargandoFormato = true;
            int idx = cboFuente.FindStringExact(col.NombreFuente ?? "Segoe UI");
            cboFuente.SelectedIndex = idx >= 0 ? idx : 0;
            nudTamaño.Value = col.TamanoFuente > 0 ? col.TamanoFuente : 9;
            chkNegrita.Checked = col.Negrita;
            chkCursiva.Checked = col.Cursiva;
            cboAlineacion.SelectedIndex = Math.Max(0, Math.Min(2, (int)col.Alineacion));
            btnColorFondo.BackColor = TryParseColor(col.ColorFondo, Color.White);
            btnColorTexto.BackColor = TryParseColor(col.ColorFuente, Color.Black);
            ActualizarPreview();
            _cargandoFormato = false;
        }

        private void ActualizarPreview()
        {
            try
            {
                string nombre = cboFuente.SelectedItem?.ToString() ?? "Segoe UI";
                float tam = (float)nudTamaño.Value;
                FontStyle fs = (chkNegrita.Checked ? FontStyle.Bold : FontStyle.Regular)
                             | (chkCursiva.Checked ? FontStyle.Italic : FontStyle.Regular);
                lblPreview.Font = new Font(nombre, tam, fs);
                lblPreview.ForeColor = btnColorTexto.BackColor;
                lblPreview.BackColor = btnColorFondo.BackColor;
            }
            catch { }
        }

        private void dgvColumnas_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvColumnas.SelectedRows.Count == 0) return;
            if (dgvColumnas.SelectedRows[0].Tag is ColumnaPersonalizada col)
            {
                lblColumnaSeleccionada.Text = $"Formato de: {col.Nombre}";
                CargarFormatoEnPanel(col);
                panelFormato.Enabled = true;
            }
        }

        private void AplicarFormatoASeleccionada()
        {
            if (_cargandoFormato || dgvColumnas.SelectedRows.Count == 0) return;
            if (dgvColumnas.SelectedRows[0].Tag is not ColumnaPersonalizada col) return;
            GuardarFormatoDesdePanel(col);
            _context.SaveChanges();
            CambiosRealizados = true;
            // Actualizar preview de fondo en grid
            try
            {
                dgvColumnas.SelectedRows[0].DefaultCellStyle.BackColor =
                    col.ColorFondo != "#FFFFFF" ? ColorTranslator.FromHtml(col.ColorFondo) : Color.White;
            }
            catch { }
            ActualizarPreview();
        }

        private void GuardarFormatoDesdePanel(ColumnaPersonalizada col)
        {
            col.NombreFuente = cboFuente.SelectedItem?.ToString() ?? "Segoe UI";
            col.TamanoFuente = (int)nudTamaño.Value;
            col.Negrita      = chkNegrita.Checked;
            col.Cursiva      = chkCursiva.Checked;
            col.Alineacion   = (AlineacionColumna)cboAlineacion.SelectedIndex;
            col.ColorFondo   = $"#{btnColorFondo.BackColor.R:X2}{btnColorFondo.BackColor.G:X2}{btnColorFondo.BackColor.B:X2}";
            col.ColorFuente  = $"#{btnColorTexto.BackColor.R:X2}{btnColorTexto.BackColor.G:X2}{btnColorTexto.BackColor.B:X2}";
            col.FechaModificacion = DateTime.Now;
        }

        private void cboFuente_SelectedIndexChanged(object s, EventArgs e)     => AplicarFormatoASeleccionada();
        private void nudTamaño_ValueChanged(object s, EventArgs e)              => AplicarFormatoASeleccionada();
        private void chkNegrita_CheckedChanged(object s, EventArgs e)           => AplicarFormatoASeleccionada();
        private void chkCursiva_CheckedChanged(object s, EventArgs e)           => AplicarFormatoASeleccionada();
        private void cboAlineacion_SelectedIndexChanged(object s, EventArgs e)  => AplicarFormatoASeleccionada();

        private void btnColorFondo_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = btnColorFondo.BackColor };
            if (dlg.ShowDialog() == DialogResult.OK) { btnColorFondo.BackColor = dlg.Color; AplicarFormatoASeleccionada(); }
        }

        private void btnColorTexto_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = btnColorTexto.BackColor };
            if (dlg.ShowDialog() == DialogResult.OK) { btnColorTexto.BackColor = dlg.Color; AplicarFormatoASeleccionada(); }
        }

        private void btnAplicarATodas_Click(object sender, EventArgs e)
        {
            if (dgvColumnas.SelectedRows.Count == 0) return;
            var origen = dgvColumnas.SelectedRows[0].Tag as ColumnaPersonalizada;
            if (origen == null) return;
            GuardarFormatoDesdePanel(origen);

            foreach (var col in _context.ColumnasPersonalizadas.Where(c => c.ProyectoId == _proyectoId))
            {
                col.NombreFuente = origen.NombreFuente;
                col.TamanoFuente = origen.TamanoFuente;
                col.Negrita      = origen.Negrita;
                col.Cursiva      = origen.Cursiva;
                col.ColorFuente  = origen.ColorFuente;
                // ColorFondo intencional: NO se copia para preservar diferencias visuales
                col.FechaModificacion = DateTime.Now;
            }
            _context.SaveChanges();
            CambiosRealizados = true;
            CargarColumnas();
            MessageBox.Show("Fuente, tamaño y color de texto aplicados a todas las columnas.\n(Color de fondo no modificado)",
                "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnRestaurarFormato_Click(object sender, EventArgs e)
        {
            if (dgvColumnas.SelectedRows.Count == 0) return;
            if (dgvColumnas.SelectedRows[0].Tag is not ColumnaPersonalizada col) return;
            col.NombreFuente = "Segoe UI"; col.TamanoFuente = 9;
            col.Negrita = false; col.Cursiva = false;
            col.Alineacion = AlineacionColumna.Izquierda;
            col.ColorFondo = "#FFFFFF"; col.ColorFuente = "#000000";
            col.FechaModificacion = DateTime.Now;
            _context.SaveChanges();
            CambiosRealizados = true;
            CargarFormatoEnPanel(col);
            dgvColumnas.SelectedRows[0].DefaultCellStyle.BackColor = Color.White;
        }

        private void btnNueva_Click(object sender, EventArgs e)
        {
            string nombre = Microsoft.VisualBasic.Interaction.InputBox("Nombre de la nueva columna:", "Nueva Columna", "");
            if (string.IsNullOrWhiteSpace(nombre)) return;
            try
            {
                var orden = _context.ColumnasPersonalizadas.Where(c => c.ProyectoId == _proyectoId).Max(c => (int?)c.Orden) ?? 0;
                _context.ColumnasPersonalizadas.Add(new ColumnaPersonalizada
                {
                    ProyectoId = _proyectoId, Nombre = nombre.Trim(),
                    NombreInterno = "Col_" + new string(nombre.Where(char.IsLetterOrDigit).ToArray()),
                    TipoColumna = TipoColumnaPersonalizada.Personal, TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 100, Visible = true, Orden = orden + 1,
                    Formula = string.Empty, FormatoNumerico = string.Empty,
                    FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty
                });
                _context.SaveChanges();
                CargarColumnas();
                CambiosRealizados = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\n{ex.InnerException?.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            if (dgvColumnas.SelectedRows.Count == 0) { MessageBox.Show("Selecciona una columna.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (MessageBox.Show("¿Eliminar esta columna?\nSe perderán todos sus datos.", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    int id = Convert.ToInt32(dgvColumnas.SelectedRows[0].Cells["colId"].Value);
                    var col = _context.ColumnasPersonalizadas.Find(id);
                    if (col != null) { _context.ColumnasPersonalizadas.Remove(col); _context.SaveChanges(); CargarColumnas(); CambiosRealizados = true; }
                }
                catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void btnPredeterminadas_Click(object sender, EventArgs e)
        {
            try
            {
                if (_context.ColumnasPersonalizadas.Any(c => c.ProyectoId == _proyectoId))
                {
                    if (MessageBox.Show("¿Eliminar columnas existentes y crear las predeterminadas?", "Confirmar",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    _context.ColumnasPersonalizadas.RemoveRange(_context.ColumnasPersonalizadas.Where(c => c.ProyectoId == _proyectoId));
                    _context.SaveChanges();
                }
                ColumnasPresupuestoHelper.CrearColumnasPredeterminadas(_context, _proyectoId);
                CargarColumnas();
                CambiosRealizados = true;
                MessageBox.Show("23 columnas predeterminadas creadas.", "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void DgvColumnas_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvColumnas.IsCurrentCellDirty) dgvColumnas.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void DgvColumnas_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            try
            {
                int id = Convert.ToInt32(dgvColumnas.Rows[e.RowIndex].Cells["colId"].Value);
                var columna = _context.ColumnasPersonalizadas.Find(id);
                if (columna == null) return;

                if (e.ColumnIndex == dgvColumnas.Columns["colVisible"].Index)
                {
                    bool visible = Convert.ToBoolean(dgvColumnas.Rows[e.RowIndex].Cells["colVisible"].Value);
                    if (visible && !columna.Visible && _columnaActualIndex >= 0)
                    {
                        var enFoco = _context.ColumnasPersonalizadas.Where(c => c.ProyectoId == _proyectoId)
                            .OrderBy(c => c.Visible ? 0 : 1).ThenBy(c => c.Orden).Skip(_columnaActualIndex).FirstOrDefault();
                        int nuevoOrden = enFoco?.Orden + 1 ?? (_context.ColumnasPersonalizadas.Where(c => c.ProyectoId == _proyectoId).Max(c => (int?)c.Orden) ?? 0) + 1;
                        foreach (var col in _context.ColumnasPersonalizadas.Where(c => c.ProyectoId == _proyectoId && c.Orden >= nuevoOrden && c.Id != columna.Id))
                            col.Orden++;
                        columna.Orden = nuevoOrden;
                    }
                    columna.Visible = visible;
                    _context.SaveChanges();
                    CambiosRealizados = true;
                    CargarColumnas();
                }
                else if (e.ColumnIndex == dgvColumnas.Columns["colNombre"].Index)
                {
                    string nuevoNombre = dgvColumnas.Rows[e.RowIndex].Cells["colNombre"].Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(nuevoNombre)) { columna.Nombre = nuevoNombre.Trim(); _context.SaveChanges(); CambiosRealizados = true; }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private Color TryParseColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }
    }
}
