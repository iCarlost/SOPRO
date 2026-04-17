using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormImportarPresupuestoExcel : Form
    {
        private readonly BudgetExcelImportService _service = new();
        private List<string> _columnNames = new();
        private readonly BindingSource _agrupadoresBinding = new();
        private bool _agrupadoresPendientesRevision;

        public List<BudgetExcelImportRowDto> FilasImportadas { get; private set; } = new();

        public FormImportarPresupuestoExcel()
        {
            InitializeComponent();
            ConfigurarFormulario();
        }

        private void ConfigurarFormulario()
        {
            dgvPreview.AutoGenerateColumns = true;
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.ReadOnly = true;
            dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvPreview.MultiSelect = false;

            dgvAgrupadores.AutoGenerateColumns = false;
            dgvAgrupadores.AllowUserToAddRows = false;
            dgvAgrupadores.AllowUserToDeleteRows = false;
            dgvAgrupadores.RowHeadersVisible = false;
            dgvAgrupadores.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvAgrupadores.MultiSelect = false;
            dgvAgrupadores.DataSource = _agrupadoresBinding;

            chkPrimeraFilaEncabezados.Checked = true;
            panelAgrupadores.Visible = false;
            InicializarGridAgrupadores();
        }

        private void InicializarGridAgrupadores()
        {
            dgvAgrupadores.Columns.Clear();

            dgvAgrupadores.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFila",
                HeaderText = "Fila",
                DataPropertyName = nameof(BudgetExcelImportRowDto.SourceRowNumber),
                Width = 52,
                ReadOnly = true
            });

            dgvAgrupadores.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colClaveAgr",
                HeaderText = "Clave",
                DataPropertyName = nameof(BudgetExcelImportRowDto.Clave),
                Width = 70,
                ReadOnly = true
            });

            dgvAgrupadores.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDescAgr",
                HeaderText = "Descripción",
                DataPropertyName = nameof(BudgetExcelImportRowDto.Descripcion),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });

            var combo = new DataGridViewComboBoxColumn
            {
                Name = "colTipoFinal",
                HeaderText = "Tipo",
                DataPropertyName = nameof(BudgetExcelImportRowDto.TipoTexto),
                Width = 110,
                FlatStyle = FlatStyle.Flat
            };
            combo.Items.AddRange("No importar", "Capitulo", "Subcapitulo", "Nivel 1", "Nivel 2", "Nivel 3");
            dgvAgrupadores.Columns.Add(combo);
        }

        private void btnExaminar_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Archivos de Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|Todos los archivos (*.*)|*.*",
                Title = "Seleccionar archivo de Excel"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            txtRutaArchivo.Text = ofd.FileName;
            CargarHojas();
        }

        private void chkPrimeraFilaEncabezados_CheckedChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtRutaArchivo.Text) && cmbHojas.SelectedItem != null)
                CargarVistaPrevia();
        }

        private void cmbHojas_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarVistaPrevia();
        }

        private void btnRecargar_Click(object sender, EventArgs e)
        {
            CargarVistaPrevia();
        }

        private void btnImportar_Click(object sender, EventArgs e)
        {
            if (_agrupadoresPendientesRevision && FilasImportadas != null && FilasImportadas.Count > 0)
            {
                _agrupadoresPendientesRevision = false;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtRutaArchivo.Text) || !File.Exists(txtRutaArchivo.Text))
            {
                MessageBox.Show("Seleccione un archivo de Excel válido.", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbHojas.SelectedItem == null)
            {
                MessageBox.Show("Seleccione una hoja para importar.", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbDescripcion.SelectedIndex < 0)
            {
                MessageBox.Show("Debe mapear al menos la columna Descripción.", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var mapping = new BudgetExcelImportColumnMapping
                {
                    PrimeraFilaEsEncabezado = chkPrimeraFilaEncabezados.Checked,
                    ColumnaClave = ObtenerIndiceCombo(cmbClave),
                    ColumnaDescripcion = ObtenerIndiceCombo(cmbDescripcion) ?? 0,
                    ColumnaUnidad = ObtenerIndiceCombo(cmbUnidad),
                    ColumnaCantidad = ObtenerIndiceCombo(cmbCantidad),
                    ColumnaTipo = ObtenerIndiceCombo(cmbTipo)
                };

                var hoja = cmbHojas.SelectedItem?.ToString() ?? string.Empty;
                var filas = _service.ImportarFilas(txtRutaArchivo.Text, hoja, mapping);
                if (filas.Count == 0)
                {
                    MessageBox.Show("No se encontraron filas válidas para importar. Revise la hoja o el mapeo de columnas.", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                FilasImportadas = filas;
                PrepararAgrupadores(FilasImportadas, mapping.ColumnaTipo.HasValue);

                if (_agrupadoresPendientesRevision)
                {
                    btnImportar.Text = "Confirmar importación";
                    lblStatus.Text = "Revise los agrupadores detectados y confirme la importación.";
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar el archivo de Excel:{ex.Message}", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void CargarHojas()
        {
            try
            {
                cmbHojas.Items.Clear();
                dgvPreview.DataSource = null;
                panelAgrupadores.Visible = false;
                _agrupadoresBinding.DataSource = null;
                _agrupadoresPendientesRevision = false;
                btnImportar.Text = "Importar";
                LimpiarMapeos();

                var hojas = _service.ObtenerHojas(txtRutaArchivo.Text);
                foreach (var hoja in hojas)
                    cmbHojas.Items.Add(hoja);

                if (cmbHojas.Items.Count > 0)
                    cmbHojas.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No fue posible leer el archivo de Excel:\n{ex.Message}", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarVistaPrevia()
        {
            if (string.IsNullOrWhiteSpace(txtRutaArchivo.Text) || !File.Exists(txtRutaArchivo.Text) || cmbHojas.SelectedItem == null)
                return;

            try
            {
                panelAgrupadores.Visible = false;
                _agrupadoresBinding.DataSource = null;
                _agrupadoresPendientesRevision = false;
                btnImportar.Text = "Importar";
                var dt = _service.LeerVistaPrevia(txtRutaArchivo.Text, cmbHojas.SelectedItem.ToString() ?? string.Empty, 25, chkPrimeraFilaEncabezados.Checked);
                dgvPreview.DataSource = dt;
                _columnNames = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                CargarCombosMapeo();
                SugerirMapeos();
                lblStatus.Text = $"Vista previa cargada: {dt.Rows.Count} fila(s).";
            }
            catch (Exception ex)
            {
                dgvPreview.DataSource = null;
                _columnNames.Clear();
                LimpiarMapeos();
                MessageBox.Show($"No fue posible cargar la vista previa:\n{ex.Message}", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarCombosMapeo()
        {
            var combos = new[] { cmbClave, cmbDescripcion, cmbUnidad, cmbCantidad, cmbTipo };
            foreach (var combo in combos)
            {
                var selected = combo.SelectedItem?.ToString();
                combo.BeginUpdate();
                combo.Items.Clear();
                combo.Items.Add("(No importar)");
                foreach (var name in _columnNames)
                    combo.Items.Add(name);
                combo.EndUpdate();
                combo.SelectedIndex = 0;

                if (!string.IsNullOrWhiteSpace(selected))
                {
                    int idx = combo.Items.IndexOf(selected);
                    if (idx >= 0)
                        combo.SelectedIndex = idx;
                }
            }
        }

        private void LimpiarMapeos()
        {
            foreach (var combo in new[] { cmbClave, cmbDescripcion, cmbUnidad, cmbCantidad, cmbTipo })
            {
                combo.Items.Clear();
                combo.Text = string.Empty;
            }
        }

        private void SugerirMapeos()
        {
            SugerirCombo(cmbClave, new[] { "clave", "codigo", "código" });
            SugerirCombo(cmbDescripcion, new[] { "descripcion", "descripción", "concepto" });
            SugerirCombo(cmbUnidad, new[] { "unidad", "u.m", "um" });
            SugerirCombo(cmbCantidad, new[] { "cantidad", "cant" });
            SugerirCombo(cmbTipo, new[] { "tipo", "renglon", "renglón", "nivel" });

            if (cmbDescripcion.SelectedIndex <= 0 && _columnNames.Count > 0)
                cmbDescripcion.SelectedIndex = Math.Min(1, cmbDescripcion.Items.Count - 1);
        }

        private void SugerirCombo(ComboBox combo, string[] keys)
        {
            foreach (var name in _columnNames)
            {
                var normalized = name.Trim().ToLowerInvariant();
                if (keys.Any(k => normalized.Contains(k)))
                {
                    int idx = combo.Items.IndexOf(name);
                    if (idx >= 0)
                    {
                        combo.SelectedIndex = idx;
                        return;
                    }
                }
            }
        }


        private void PrepararAgrupadores(List<BudgetExcelImportRowDto> filas, bool vieneTipoMapeado)
        {
            foreach (var fila in filas)
            {
                bool esAgrupador = EsFilaAgrupador(fila);
                fila.EsAgrupadorDetectado = esAgrupador;
            }

            var agrupadores = filas.Where(f => f.EsAgrupadorDetectado).ToList();
            if (agrupadores.Count == 0)
            {
                panelAgrupadores.Visible = false;
                _agrupadoresBinding.DataSource = null;
                _agrupadoresPendientesRevision = false;
                btnImportar.Text = "Importar";
                return;
            }

            bool primerInferido = false;
            foreach (var fila in agrupadores)
            {
                string tipoSugerido = ResolverTipoAgrupadorSugerido(fila, vieneTipoMapeado, ref primerInferido);
                fila.TipoTexto = tipoSugerido;
            }

            _agrupadoresBinding.DataSource = agrupadores;
            panelAgrupadores.Visible = true;
            _agrupadoresPendientesRevision = true;
            lblStatus.Text = $"Agrupadores detectados: {agrupadores.Count}. Revise el tipo sugerido si lo desea.";
        }

        private static bool EsFilaAgrupador(BudgetExcelImportRowDto fila)
        {
            bool hasUnidad = !string.IsNullOrWhiteSpace(fila.Unidad);
            bool hasCantidad = fila.Cantidad.HasValue && fila.Cantidad.Value != 0m;

            if (!string.IsNullOrWhiteSpace(fila.TipoTexto))
            {
                string normalized = fila.TipoTexto.Trim().ToLowerInvariant();
                if (normalized.Contains("concept"))
                    return false;
                if (normalized.Contains("cap") || normalized.Contains("subcap") || normalized.Contains("nivel") || normalized.Contains("titulo") || normalized.Contains("título") || normalized.Contains("encabezado"))
                    return true;
            }

            return !hasUnidad && !hasCantidad;
        }

        private static string ResolverTipoAgrupadorSugerido(BudgetExcelImportRowDto fila, bool vieneTipoMapeado, ref bool primerInferido)
        {
            if (vieneTipoMapeado && !string.IsNullOrWhiteSpace(fila.TipoTexto))
            {
                string mapped = NormalizarTipoAgrupador(fila.TipoTexto);
                if (!string.IsNullOrWhiteSpace(mapped))
                    return mapped;
            }

            if (!primerInferido)
            {
                primerInferido = true;
                return "Capitulo";
            }

            return "Subcapitulo";
        }

        private static string NormalizarTipoAgrupador(string? tipoTexto)
        {
            string normalized = (tipoTexto ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            if (normalized.Contains("no import") || normalized == "omit" || normalized == "omitir") return "No importar";
            if (normalized.Contains("subcap")) return "Subcapitulo";
            if (normalized.Contains("nivel 3") || normalized == "n3") return "Nivel 3";
            if (normalized.Contains("nivel 2") || normalized == "n2") return "Nivel 2";
            if (normalized.Contains("nivel 1") || normalized == "n1") return "Nivel 1";
            if (normalized.Contains("cap")) return "Capitulo";
            if (normalized.Contains("titulo") || normalized.Contains("título") || normalized.Contains("encabezado")) return "Capitulo";
            return string.Empty;
        }

        private int? ObtenerIndiceCombo(ComboBox combo)
        {
            if (combo.SelectedIndex <= 0)
                return null;

            var selected = combo.SelectedItem?.ToString();
            int idx = _columnNames.FindIndex(c => string.Equals(c, selected, StringComparison.Ordinal));
            return idx >= 0 ? idx : null;
        }
    }
}
