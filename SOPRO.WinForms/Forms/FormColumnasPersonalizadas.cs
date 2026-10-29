using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Diálogo de columnas del presupuesto. LEE con contexto propio y ESCRIBE
    /// exclusivamente por <see cref="BudgetColumnConfigurationService"/> (una
    /// operación, un SaveChanges): nunca toca el contexto compartido del formulario
    /// de presupuesto. Tras cada operación confirmada recarga la lista de solo
    /// lectura; un fallo no deja cambios visuales ni anuncia éxito.
    /// </summary>
    public partial class FormColumnasPersonalizadas : Form
    {
        private readonly IProjectDbContextFactory _factory;
        private readonly string _databasePath;
        private readonly int _proyectoId;
        private readonly int _columnaActualIndex;
        private readonly IReadOnlyList<BudgetColumnDefinitionDto> _definicionesPredeterminadas;
        private readonly Func<string, bool> _confirmar;

        private IReadOnlyList<BudgetColumnSnapshot> _columnas = Array.Empty<BudgetColumnSnapshot>();
        private bool _cargandoFormato;
        private bool _cargandoLista;
        private bool _aplicandoFormato;

        /// <summary>
        /// Bloqueo irreversible de la superficie mutante del diálogo tras un resultado
        /// incierto que no pudo reconciliarse. No se libera dentro del diálogo: el padre
        /// ya queda bloqueado y ejecuta su ruta segura al cerrarlo.
        /// </summary>
        private bool _mutacionesBloqueadas;

        public bool CambiosRealizados { get; private set; }

        /// <summary>
        /// El diálogo sufrió un <see cref="BudgetOperationStatus.OutcomeUnknown"/> y pudo
        /// releer la lista desde la base: el padre debe recargar su configuración al
        /// cerrarse aunque el usuario cancele, porque los Ids pueden haber cambiado.
        /// </summary>
        internal bool RequiereRecargaAlCerrar { get; private set; }

        /// <summary>
        /// El diálogo sufrió un <see cref="BudgetOperationStatus.OutcomeUnknown"/> y NO
        /// pudo releer la lista: el padre debe bloquear sus mutaciones de columnas hasta
        /// una recarga segura. Nunca se sigue trabajando con Ids potencialmente viejos.
        /// </summary>
        internal bool RequiereBloqueoParent { get; private set; }

        /// <summary>Seam de prueba por instancia: sustituye los avisos modales del diálogo.</summary>
        internal Action<string>? MostrarMensajeParaPrueba { get; set; }

        public FormColumnasPersonalizadas(
            IProjectDbContextFactory factory,
            string databasePath,
            int proyectoId,
            IReadOnlyList<BudgetColumnDefinitionDto>? definicionesPredeterminadas = null,
            int columnaActualIndex = -1)
            : this(factory, databasePath, proyectoId, definicionesPredeterminadas, columnaActualIndex, null)
        {
        }

        /// <summary>
        /// Seam de pruebas: permite sustituir la confirmación del reset. La
        /// confirmación SIEMPRE ocurre antes de mutar.
        /// </summary>
        internal FormColumnasPersonalizadas(
            IProjectDbContextFactory factory,
            string databasePath,
            int proyectoId,
            IReadOnlyList<BudgetColumnDefinitionDto>? definicionesPredeterminadas,
            int columnaActualIndex,
            Func<string, bool>? confirmar)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
            if (proyectoId <= 0)
                throw new ArgumentOutOfRangeException(nameof(proyectoId));

            _databasePath = databasePath;
            _proyectoId = proyectoId;
            _columnaActualIndex = columnaActualIndex;
            _definicionesPredeterminadas = definicionesPredeterminadas
                ?? ColumnasPresupuestoHelper.CrearDefinicionesPredeterminadas();
            _confirmar = confirmar ?? (mensaje =>
                MessageBox.Show(mensaje, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);

            InitializeComponent();
            this.Shown += (s, e) => splitMain.SplitterDistance = splitMain.Width - 300;
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

        private bool CargarColumnas(int? seleccionarId = null, bool mostrarError = true)
        {
            try
            {
                _columnas = BudgetColumnConfigurationService.LoadColumns(
                    _factory, _databasePath, _proyectoId);

                _cargandoLista = true;
                dgvColumnas.Rows.Clear();

                foreach (var col in _columnas)
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

                lblStatus.Text = $"{_columnas.Count} columnas  |  {_columnas.Count(c => c.Visible)} visibles";

                int indiceSeleccion = 0;
                if (seleccionarId.HasValue)
                {
                    int encontrado = _columnas
                        .Select((c, i) => new { c.Id, i })
                        .Where(x => x.Id == seleccionarId.Value)
                        .Select(x => x.i)
                        .DefaultIfEmpty(0)
                        .First();
                    indiceSeleccion = encontrado;
                }

                if (dgvColumnas.Rows.Count > 0)
                    dgvColumnas.Rows[indiceSeleccion].Selected = true;

                panelFormato.Enabled = _columnas.Count > 0 && !_mutacionesBloqueadas;

                return true;
            }
            catch (Exception ex)
            {
                if (mostrarError)
                    MostrarMensaje($"Error al cargar columnas:\n{ex.Message}", MessageBoxIcon.Error);

                return false;
            }
            finally
            {
                _cargandoLista = false;
            }
        }

        private void CargarFormatoEnPanel(BudgetColumnSnapshot col)
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
            if (dgvColumnas.SelectedRows[0].Tag is BudgetColumnSnapshot col)
            {
                lblColumnaSeleccionada.Text = $"Formato de: {col.Nombre}";
                CargarFormatoEnPanel(col);
                // El bloqueo por resultado incierto es irreversible: seleccionar otra
                // fila no puede reactivar la superficie de mutación.
                panelFormato.Enabled = !_mutacionesBloqueadas;
            }
        }

        private void AplicarFormatoASeleccionada()
        {
            if (_mutacionesBloqueadas) return;
            if (_cargandoFormato || _aplicandoFormato || _cargandoLista) return;
            if (dgvColumnas.SelectedRows.Count == 0) return;
            if (dgvColumnas.SelectedRows[0].Tag is not BudgetColumnSnapshot col) return;

            _aplicandoFormato = true;
            try
            {
                var resultado = BudgetColumnConfigurationService.ApplyColumnFormat(
                    _factory, _databasePath, _proyectoId, col.Id, BuildFormatoDesdePanel(col));

                if (!resultado.IsCommitted)
                {
                    MostrarFallo(resultado, "No se pudo aplicar el formato de la columna.");
                    CargarColumnas(col.Id);
                    return;
                }

                CambiosRealizados = true;
                CargarColumnas(col.Id);
            }
            finally
            {
                _aplicandoFormato = false;
            }
        }

        private BudgetColumnFormatDto BuildFormatoDesdePanel(BudgetColumnSnapshot? columnaActual = null)
        {
            return new BudgetColumnFormatDto
            {
                NombreFuente = cboFuente.SelectedItem?.ToString() ?? "Segoe UI",
                TamanoFuente = (int)nudTamaño.Value,
                Negrita = chkNegrita.Checked,
                Cursiva = chkCursiva.Checked,
                Alineacion = (AlineacionColumna)cboAlineacion.SelectedIndex,
                ColorFondo = $"#{btnColorFondo.BackColor.R:X2}{btnColorFondo.BackColor.G:X2}{btnColorFondo.BackColor.B:X2}",
                ColorFuente = $"#{btnColorTexto.BackColor.R:X2}{btnColorTexto.BackColor.G:X2}{btnColorTexto.BackColor.B:X2}",
                // El diálogo no edita wrap ni alineación vertical: se conservan los
                // valores vigentes de la columna, como antes.
                WrapTexto = columnaActual?.WrapTexto ?? false,
                AlineacionVertical = columnaActual?.AlineacionVertical ?? 1
            };
        }

        private void cboFuente_SelectedIndexChanged(object s, EventArgs e)     => AplicarFormatoASeleccionada();
        private void nudTamaño_ValueChanged(object s, EventArgs e)              => AplicarFormatoASeleccionada();
        private void chkNegrita_CheckedChanged(object s, EventArgs e)           => AplicarFormatoASeleccionada();
        private void chkCursiva_CheckedChanged(object s, EventArgs e)           => AplicarFormatoASeleccionada();
        private void cboAlineacion_SelectedIndexChanged(object s, EventArgs e)  => AplicarFormatoASeleccionada();

        private void btnColorFondo_Click(object sender, EventArgs e)
        {
            if (_mutacionesBloqueadas) return;

            using var dlg = new ColorDialog { Color = btnColorFondo.BackColor };
            if (dlg.ShowDialog() == DialogResult.OK) { btnColorFondo.BackColor = dlg.Color; AplicarFormatoASeleccionada(); }
        }

        private void btnColorTexto_Click(object sender, EventArgs e)
        {
            if (_mutacionesBloqueadas) return;

            using var dlg = new ColorDialog { Color = btnColorTexto.BackColor };
            if (dlg.ShowDialog() == DialogResult.OK) { btnColorTexto.BackColor = dlg.Color; AplicarFormatoASeleccionada(); }
        }

        private void btnAplicarATodas_Click(object sender, EventArgs e)
        {
            if (_mutacionesBloqueadas) return;
            if (dgvColumnas.SelectedRows.Count == 0) return;
            if (dgvColumnas.SelectedRows[0].Tag is not BudgetColumnSnapshot origen) return;

            // Alcance FontOnly: mismo comportamiento histórico del diálogo (fuente,
            // tamaño, negrita, cursiva y color de texto; el fondo NO se copia).
            var resultado = BudgetColumnConfigurationService.ApplyGlobalColumnFormat(
                _factory, _databasePath, _proyectoId, BuildFormatoDesdePanel(origen),
                BudgetGlobalColumnFormatScope.FontOnly);

            if (!resultado.IsCommitted)
            {
                MostrarFallo(resultado, "No se pudo aplicar el formato a todas las columnas.");
                CargarColumnas(origen.Id);
                return;
            }

            CambiosRealizados = true;
            CargarColumnas(origen.Id);
            MessageBox.Show("Fuente, tamaño y color de texto aplicados a todas las columnas.\n(Color de fondo no modificado)",
                "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnRestaurarFormato_Click(object sender, EventArgs e)
        {
            if (_mutacionesBloqueadas) return;
            if (dgvColumnas.SelectedRows.Count == 0) return;
            if (dgvColumnas.SelectedRows[0].Tag is not BudgetColumnSnapshot col) return;

            var formatoPredeterminado = new BudgetColumnFormatDto
            {
                NombreFuente = "Segoe UI",
                TamanoFuente = 9,
                Negrita = false,
                Cursiva = false,
                Alineacion = AlineacionColumna.Izquierda,
                ColorFondo = "#FFFFFF",
                ColorFuente = "#000000",
                WrapTexto = col.WrapTexto,
                AlineacionVertical = col.AlineacionVertical
            };

            var resultado = BudgetColumnConfigurationService.ApplyColumnFormat(
                _factory, _databasePath, _proyectoId, col.Id, formatoPredeterminado);

            if (!resultado.IsCommitted)
            {
                MostrarFallo(resultado, "No se pudo restaurar el formato de la columna.");
                CargarColumnas(col.Id);
                return;
            }

            CambiosRealizados = true;
            CargarColumnas(col.Id);
        }

        private void btnNueva_Click(object sender, EventArgs e)
        {
            if (_mutacionesBloqueadas) return;

            string nombre = Microsoft.VisualBasic.Interaction.InputBox("Nombre de la nueva columna:", "Nueva Columna", "");
            if (string.IsNullOrWhiteSpace(nombre)) return;

            var resultado = BudgetColumnConfigurationService.AddColumn(
                _factory, _databasePath, _proyectoId,
                ColumnasPresupuestoHelper.CrearDefinicionPersonal(nombre.Trim()));

            if (!resultado.IsCommitted)
            {
                MostrarFallo(resultado, "No se pudo crear la columna.");
                return;
            }

            CambiosRealizados = true;
            var nueva = resultado.ConfirmedColumns
                .OrderByDescending(c => c.Orden)
                .ThenByDescending(c => c.Id)
                .FirstOrDefault();
            CargarColumnas(nueva?.Id);
        }

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            if (_mutacionesBloqueadas) return;

            if (dgvColumnas.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecciona una columna.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (dgvColumnas.SelectedRows[0].Tag is not BudgetColumnSnapshot col) return;

            if (!_confirmar("¿Eliminar esta columna?\nSe perderán todos sus datos."))
                return;

            var resultado = BudgetColumnConfigurationService.DeleteColumn(
                _factory, _databasePath, _proyectoId, col.Id);

            if (!resultado.IsCommitted)
            {
                MostrarFallo(resultado, "No se pudo eliminar la columna.");
                CargarColumnas(col.Id);
                return;
            }

            CambiosRealizados = true;
            CargarColumnas();
        }

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        private void btnPredeterminadas_Click(object sender, EventArgs e)
        {
            if (_mutacionesBloqueadas) return;

            if (_columnas.Count > 0)
            {
                // Confirmación ANTES de cualquier mutación.
                if (!_confirmar("¿Eliminar columnas existentes y crear las predeterminadas?"))
                    return;
            }

            var resultado = BudgetColumnConfigurationService.ResetToDefaults(
                _factory, _databasePath, _proyectoId, _definicionesPredeterminadas);

            if (!resultado.IsCommitted)
            {
                MostrarFallo(resultado, "No se pudieron crear las columnas predeterminadas.");
                CargarColumnas();
                return;
            }

            CambiosRealizados = true;
            CargarColumnas();
            MessageBox.Show("23 columnas predeterminadas creadas.", "Listo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DgvColumnas_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvColumnas.IsCurrentCellDirty) dgvColumnas.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void DgvColumnas_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_mutacionesBloqueadas || _cargandoLista || e.RowIndex < 0) return;

            try
            {
                if (dgvColumnas.Rows[e.RowIndex].Tag is not BudgetColumnSnapshot columna) return;

                if (e.ColumnIndex == dgvColumnas.Columns["colVisible"].Index)
                {
                    bool visible = Convert.ToBoolean(dgvColumnas.Rows[e.RowIndex].Cells["colVisible"].Value);

                    var resultado = BudgetColumnConfigurationService.SetColumnVisibility(
                        _factory, _databasePath, _proyectoId, columna.Id, visible,
                        _columnaActualIndex >= 0 ? _columnaActualIndex : null);

                    if (!resultado.IsCommitted)
                    {
                        MostrarFallo(resultado, "No se pudo cambiar la visibilidad de la columna.");
                        CargarColumnas(columna.Id);
                        return;
                    }

                    CambiosRealizados = true;
                    CargarColumnas(columna.Id);
                }
                else if (e.ColumnIndex == dgvColumnas.Columns["colNombre"].Index)
                {
                    string nuevoNombre = dgvColumnas.Rows[e.RowIndex].Cells["colNombre"].Value?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(nuevoNombre)) { CargarColumnas(columna.Id); return; }

                    var resultado = BudgetColumnConfigurationService.RenameColumn(
                        _factory, _databasePath, _proyectoId, columna.Id, nuevoNombre);

                    if (!resultado.IsCommitted)
                    {
                        MostrarFallo(resultado, "No se pudo renombrar la columna.");
                        CargarColumnas(columna.Id);
                        return;
                    }

                    CambiosRealizados = true;
                    CargarColumnas(columna.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CargarColumnas();
            }
        }

        private void MostrarFallo(BudgetColumnConfigurationResult resultado, string mensajePorDefecto)
        {
            string mensaje = resultado.Message ?? mensajePorDefecto;

            if (resultado.Status == BudgetOperationStatus.OutcomeUnknown)
            {
                // El commit pudo ocurrir: la lista local puede tener Ids obsoletos.
                // Se reconcilia con una lectura fresca antes de permitir más cambios.
                if (IntentarReconciliarTrasResultadoIncierto())
                {
                    mensaje += "\nLos cambios se leyeron de nuevo desde la base; " +
                               "el presupuesto se actualizará al cerrar.";
                }
                else
                {
                    mensaje += "\nNo se pudo confirmar si el cambio se guardó ni recargar la lista: " +
                               "recargue el presupuesto antes de seguir editando columnas.";
                }
            }

            MostrarMensaje(mensaje, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Reconciliación tras un <see cref="BudgetOperationStatus.OutcomeUnknown"/>:
        /// relee la lista desde la base. Si la lectura se completa, el diálogo queda
        /// utilizable y el padre debe recargar la configuración al cerrarse. Si no, se
        /// bloquean las mutaciones del diálogo y el padre debe bloquear las suyas hasta
        /// una recarga segura: no se continúa con Ids potencialmente viejos.
        /// </summary>
        private bool IntentarReconciliarTrasResultadoIncierto()
        {
            if (CargarColumnas(mostrarError: false))
            {
                RequiereRecargaAlCerrar = true;
                return true;
            }

            RequiereBloqueoParent = true;
            BloquearMutaciones();
            return false;
        }

        private void BloquearMutaciones()
        {
            _mutacionesBloqueadas = true;

            dgvColumnas.ReadOnly = true;
            dgvColumnas.Enabled = false;
            panelFormato.Enabled = false;
            btnNueva.Enabled = false;
            btnEliminar.Enabled = false;
            btnPredeterminadas.Enabled = false;
            btnAplicarATodas.Enabled = false;
            btnRestaurarFormato.Enabled = false;
            btnColorFondo.Enabled = false;
            btnColorTexto.Enabled = false;
        }

        /// <summary>
        /// Seam de lectura para pruebas: el bloqueo por resultado incierto es
        /// irreversible dentro del diálogo.
        /// </summary>
        internal bool MutacionesBloqueadas => _mutacionesBloqueadas;

        private void MostrarMensaje(string mensaje, MessageBoxIcon icono)
        {
            if (MostrarMensajeParaPrueba is not null)
            {
                MostrarMensajeParaPrueba(mensaje);
                return;
            }

            MessageBox.Show(mensaje, "Columnas", MessageBoxButtons.OK, icono);
        }

        private Color TryParseColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }
    }
}
