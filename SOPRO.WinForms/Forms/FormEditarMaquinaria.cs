using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Services;
using SOPRO.Application.DTOs.Catalog;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarMaquinaria : Form
    {
        private readonly SOPROContext _context;
        private readonly int? _proyectoId;
        private Maquinaria _maquinaria;
        private bool _esNuevo;
        
        public FormEditarMaquinaria(SOPROContext context, int? proyectoId, Maquinaria maquinaria = null)
        {
            InitializeComponent();
            
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _maquinaria = maquinaria;
            _esNuevo = maquinaria == null;
            
            ConfigurarFormulario();
            AplicarCromoVisual();
            SeleccionTotalNumericUpDown.Configurar(nudCostoHorario);
            
            if (_esNuevo)
                txtClave.Text = KeySuggestionService.Generate();
            else
                CargarDatos();
        }
        
        private void ConfigurarFormulario()
        {
            this.Text = _esNuevo ? "Nueva Maquinaria" : "Editar Maquinaria";
            
            // Cargar tipos de combustible
            cboTipoCombustible.DataSource = Enum.GetValues(typeof(TipoCombustible));
            
            if (_proyectoId.HasValue)
            {
                rbProyecto.Enabled = true;
                rbMaestro.Enabled = true;
                
                if (_esNuevo)
                {
                    rbProyecto.Checked = true;
                }
            }
            else
            {
                rbMaestro.Checked = true;
                rbProyecto.Enabled = false;
            }
        }
        
        /// <summary>
        /// Compone el cromo del diálogo con el tema compartido (encabezado,
        /// tarjeta de contenido y pie de acciones) y reacomoda dentro de esa
        /// estructura los controles declarados en el diseñador. Solo
        /// presentación: no altera datos, eventos ni la semántica de guardado
        /// ni del guard compartido de calcular/guardar.
        /// </summary>
        private void AplicarCromoVisual()
        {
            // Acción secundaria dentro del grupo de costo: comparte la
            // presentación de los botones de diálogo del tema. Se alinea al
            // margen derecho del grupo DESPUÉS de que el estilo fija su ancho
            // definitivo, y anclada a la derecha para seguir cabiendo completa
            // si el grupo cambia con el DPI.
            btnCalcular.SoproIcon = SoproIconType.Recalcular;
            SoproDialogTheme.AplicarEstiloBotonSecundario(btnCalcular);
            btnCalcular.Left = grpCosto.ClientSize.Width - btnCalcular.Width - 16;
            btnCalcular.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            SoproDialogTheme.AplicarCromoEditor(
                this,
                new Size(800, 660),
                Text,
                _esNuevo ? "Agrega una maquinaria al catálogo" : "Actualiza los datos de la maquinaria",
                CrearTablaCampos(),
                btnGuardar,
                btnCancelar);
        }

        /// <summary>
        /// Rejilla de campos dentro de la tarjeta. Filas y columnas medidas por
        /// el propio contenedor (AutoSize) con la fuente real en el DPI del
        /// monitor: sin posiciones absolutas que se rompan a 125/150/200 %.
        /// El orden de filas conserva el TabIndex del diseñador. El grupo de
        /// costo horario conserva su geometría interna y solo recibe tokens
        /// del tema.
        /// </summary>
        private TableLayoutPanel CrearTablaCampos()
        {
            var tabla = SoproDialogTheme.CrearRejillaCampos();
            tabla.RowCount = 7;
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            // Fila ABSOLUTA para el grupo de costo: el alto preferido del
            // GroupBox se infla con el DPI y desplazaría el resto de la rejilla.
            // El grupo mide 170 px (campo de costo en 12pt bold + fecha +
            // advertencia) y su fila lo aloja sin recortes.
            tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 175));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            foreach (var rotulo in new[] { lblClave, lblDescripcion, lblPotencia, lblTipoCombustible, lblNotas })
                SoproDialogTheme.AplicarEstiloRotulo(rotulo);

            foreach (var campo in new Control[] { txtClave, txtDescripcion, nudPotencia, cboTipoCombustible, txtNotas })
                SoproDialogTheme.AplicarEstiloCampo(campo);

            // Grupo de costo horario: mismo look de tarjeta del tema; su
            // disposición interna (radios, costo, cálculo, fecha) no cambia.
            grpCosto.Font = SoproTheme.Fonts.GroupTitle;
            grpCosto.ForeColor = SoproTheme.Colors.Ink2;
            label4.Font = SoproTheme.Fonts.Subtitle;
            label4.ForeColor = SoproTheme.Colors.Muted;
            label6.Font = SoproTheme.Fonts.Subtitle;
            label6.ForeColor = SoproTheme.Colors.Muted;
            lblFechaCalculo.Font = SoproTheme.Fonts.Subtitle;
            lblFechaCalculo.ForeColor = SoproTheme.Colors.Muted;
            lblAdvertencia.Font = SoproTheme.Fonts.Subtitle;
            lblAdvertencia.ForeColor = SoproTheme.Colors.Muted;
            rbCostoCalculado.Font = SoproTheme.Fonts.Subtitle;
            rbCostoManual.Font = SoproTheme.Fonts.Subtitle;
            rbCostoCalculado.ForeColor = SoproTheme.Colors.Ink2;
            rbCostoManual.ForeColor = SoproTheme.Colors.Ink2;

            txtClave.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtDescripcion.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            nudPotencia.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboTipoCombustible.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboTipoCombustible.Margin = new Padding(0, 4, 0, 4);
            txtNotas.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtNotas.Margin = new Padding(0, 4, 0, 0);

            tabla.Controls.Add(lblClave, 0, 0);
            tabla.Controls.Add(txtClave, 1, 0);
            tabla.Controls.Add(lblDescripcion, 0, 1);
            tabla.Controls.Add(txtDescripcion, 1, 1);
            tabla.SetColumnSpan(txtDescripcion, 3);
            tabla.Controls.Add(lblPotencia, 0, 2);
            tabla.Controls.Add(nudPotencia, 1, 2);
            tabla.Controls.Add(lblTipoCombustible, 2, 2);
            tabla.Controls.Add(cboTipoCombustible, 3, 2);
            tabla.Controls.Add(grpCosto, 0, 3);
            tabla.SetColumnSpan(grpCosto, 4);
            tabla.Controls.Add(lblNotas, 0, 4);
            tabla.SetColumnSpan(lblNotas, 4);
            tabla.Controls.Add(txtNotas, 0, 5);
            tabla.SetColumnSpan(txtNotas, 4);

            return tabla;
        }

        private void CargarDatos()
        {
            txtClave.Text = _maquinaria.Clave;
            txtDescripcion.Text = _maquinaria.Descripcion;
            nudPotencia.Value = _maquinaria.PotenciaNominal;
            cboTipoCombustible.SelectedItem = _maquinaria.TipoCombustible;
            nudCostoHorario.Value = _maquinaria.CostoHorario;
            txtNotas.Text = _maquinaria.Notas;
            
            if (_maquinaria.EsCostoCalculado)
                rbCostoCalculado.Checked = true;
            else
                rbCostoManual.Checked = true;
            
            // El destino del guardado se decide por propiedad (ProyectoId), no
            // por Origen: una copia importada del maestro vive en el proyecto y
            // conserva Origen = Maestro; su edición debe seguir guardándose en
            // el proyecto, nunca en el catálogo maestro.
            bool esDelProyectoActual = _proyectoId.HasValue && _maquinaria.ProyectoId == _proyectoId;
            rbProyecto.Checked = esDelProyectoActual;
            rbMaestro.Checked = !esDelProyectoActual;
                
            lblFechaCalculo.Text = _maquinaria.FechaCalculoCosto?.ToString("dd/MMM/yyyy HH:mm") ?? "N/A";
        }
        
        private void rbCostoCalculado_CheckedChanged(object sender, EventArgs e)
        {
            if (rbCostoCalculado.Checked)
            {
                nudCostoHorario.Enabled = false;
                btnCalcular.Enabled = true;
                lblAdvertencia.Text = "Use el botón 'Calcular' para determinar el costo horario";
                lblAdvertencia.Visible = true;
            }
        }
        
        private void rbCostoManual_CheckedChanged(object sender, EventArgs e)
        {
            if (rbCostoManual.Checked)
            {
                nudCostoHorario.Enabled = true;
                btnCalcular.Enabled = false;
                lblAdvertencia.Visible = false;
            }
        }
        
        private async void btnCalcular_Click(object sender, EventArgs e)
        {
            if (!IniciarOperacion())
                return;

            try
            {
                // Si es nuevo, guardar primero para tener el objeto en BD con Id
                if (_esNuevo)
                {
                    if (!ValidarYGuardar())
                        return;

                    var dto = new MaquinariaEditDto
                    {
                        Clave = txtClave.Text,
                        Descripcion = txtDescripcion.Text,
                        PotenciaNominal = nudPotencia.Value,
                        TipoCombustible = (TipoCombustible)cboTipoCombustible.SelectedItem,
                        CostoHorario = 0,
                        EsCostoCalculado = true,
                        Notas = txtNotas.Text,
                        GuardarEnMaestro = rbMaestro.Checked,
                        ProyectoId = _proyectoId,
                        MaterialCombustibleId = _maquinaria?.MaterialCombustibleId,
                        MaterialAceiteId = _maquinaria?.MaterialAceiteId,
                        ManoDeObraOperadorId = _maquinaria?.ManoDeObraOperadorId
                    };

                    var result = await CatalogItemService.SaveMaquinariaAsync(_context, dto);

                    if (IsDisposed || Disposing) return;

                    _maquinaria = _context.Maquinaria
                        .First(x => x.Id == result.EntityId);
                    _esNuevo = false;
                }

                // Abrir calculadora con el objeto ya persistido en BD; nunca tras el cierre.
                if (IsDisposed || Disposing) return;

                using var form = new FormCalculoCostoHorario(_context, _maquinaria);
                if (form.ShowDialog() == DialogResult.OK && !IsDisposed && !Disposing)
                {
                    CargarDatos();
                }
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;

                MessageBox.Show(
                    $"Error al calcular el costo horario:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                FinalizarOperacion();
            }
        }
        
        private void txtClave_Leave(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue) return;
            
            string clave = txtClave.Text.Trim();
            if (string.IsNullOrWhiteSpace(clave)) return;
            
            var maqExistente = _context.Maquinaria
                .Where(m => m.ProyectoId == _proyectoId.Value)
                .Where(m => m.Clave.ToUpper() == clave.ToUpper())
                .Where(m => _esNuevo || m.Id != _maquinaria.Id)
                .FirstOrDefault();
            
            if (maqExistente != null)
            {
                if (_esNuevo)
                {
                    txtDescripcion.Text = maqExistente.Descripcion;
                    nudCostoHorario.Value = maqExistente.CostoHorario;
                    txtNotas.Text = maqExistente.Notas ?? "";
                    return;
                }
                
                var result = MessageBox.Show(
                    $"La clave '{clave}' ya está asignada a:\n\n" +
                    $"  → {maqExistente.Descripcion}\n" +
                    $"  → Costo Horario: {maqExistente.CostoHorario:C2}\n\n" +
                    $"¿Desea REEMPLAZAR los datos actuales?",
                    "Clave Duplicada",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.Yes)
                {
                    txtDescripcion.Text = maqExistente.Descripcion;
                    nudCostoHorario.Value = maqExistente.CostoHorario;
                    txtNotas.Text = maqExistente.Notas ?? "";
                }
                else
                {
                    if (_maquinaria != null)
                        txtClave.Text = _maquinaria.Clave;
                    else
                        txtClave.Text = "";
                    txtClave.Focus();
                }
            }
        }
        
        // Guard compartido por cálculo y guardado: una sola operación riesgosa a la
        // vez en este editor. Se toma antes de la primera validación/escritura y se
        // libera en el finally, sin tocar controles si el form ya se liberó.
        private bool _operacionEnCurso;

        private bool IniciarOperacion()
        {
            if (_operacionEnCurso) return false;

            _operacionEnCurso = true;

            if (!IsDisposed && !Disposing)
            {
                btnGuardar.Enabled = false;
                btnCalcular.Enabled = false;
            }

            return true;
        }

        private void FinalizarOperacion()
        {
            _operacionEnCurso = false;

            if (IsDisposed || Disposing) return;

            btnGuardar.Enabled = true;
            btnCalcular.Enabled = rbCostoCalculado.Checked;
        }

        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            if (!IniciarOperacion())
                return;

            try
            {
                if (!ValidarYGuardar())
                    return;

                var dto = new MaquinariaEditDto
                {
                    Clave = txtClave.Text,
                    Descripcion = txtDescripcion.Text,
                    PotenciaNominal = nudPotencia.Value,
                    TipoCombustible = (TipoCombustible)cboTipoCombustible.SelectedItem,
                    CostoHorario = rbCostoManual.Checked ? nudCostoHorario.Value : _maquinaria?.CostoHorario ?? nudCostoHorario.Value,
                    EsCostoCalculado = rbCostoCalculado.Checked,
                    Notas = txtNotas.Text,
                    GuardarEnMaestro = rbMaestro.Checked,
                    ProyectoId = _proyectoId,
                    MaterialCombustibleId = _maquinaria?.MaterialCombustibleId,
                    MaterialAceiteId = _maquinaria?.MaterialAceiteId,
                    ManoDeObraOperadorId = _maquinaria?.ManoDeObraOperadorId
                };

                var result = await CatalogItemService.SaveMaquinariaAsync(_context, dto, _esNuevo ? null : _maquinaria);

                if (IsDisposed || Disposing) return;

                if (result.TriggeredRecalculation)
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                OpenFormsRefreshHelper.RefrescarCatalogosMaquinariaAbiertos();

                MessageBox.Show(
                    result.IsNew ? "Registro creado exitosamente." : "Registro actualizado exitosamente.",
                    "Guardado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;

                MessageBox.Show(
                    $"Error al guardar:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                FinalizarOperacion();
            }
        }
        
        private bool ValidarYGuardar()
        {
            if (string.IsNullOrWhiteSpace(txtClave.Text))
            {
                MessageBox.Show("La clave es requerida.", "Campo Requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtClave.Focus();
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                MessageBox.Show("La descripción es requerida.", "Campo Requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDescripcion.Focus();
                return false;
            }
            
            if (rbCostoCalculado.Checked && nudCostoHorario.Value == 0 && !_esNuevo)
            {
                MessageBox.Show(
                    "Debe calcular el costo horario usando el botón 'Calcular'.",
                    "Costo No Calculado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return false;
            }

            if (_proyectoId.HasValue)
            {
                var errorClave = KeyValidationService.ValidateUniqueKey(
                    _context,
                    txtClave.Text.Trim(),
                    _proyectoId.Value,
                    _esNuevo ? null : _maquinaria?.Id,
                    CatalogItemType.Maquinaria
                );

                if (errorClave != null)
                {
                    MessageBox.Show(errorClave, "Clave Duplicada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtClave.Focus();
                    return false;
                }
            }
            
            return true;
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
