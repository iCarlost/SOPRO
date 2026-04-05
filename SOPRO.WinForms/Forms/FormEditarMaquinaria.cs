using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
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
            
            if (_esNuevo)
                txtClave.Text = KeySuggestionService.Generate();
            else
            {
                CargarDatos();
                // Valores por defecto
                nudCostoHorario.Value = 0;
                rbCostoManual.Checked = true;
            }
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
            
            if (_maquinaria.Origen == OrigenInsumo.Maestro)
                rbMaestro.Checked = true;
            else
                rbProyecto.Checked = true;
                
            lblFechaCalculo.Text = _maquinaria.FechaCalculoCosto?.ToString("dd/MMM/yyyy HH:mm") ?? "N/A";
        }
        
        private void rbCostoCalculado_CheckedChanged(object sender, EventArgs e)
        {
            if (rbCostoCalculado.Checked)
            {
                nudCostoHorario.Enabled = false;
                btnCalcular.Enabled = true;
                lblAdvertencia.Text = "⚠ Use el botón 'Calcular' para determinar el costo horario";
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
            // Si es nuevo, guardar primero para tener el objeto en BD con Id
            if (_esNuevo)
            {
                if (!ValidarYGuardar())
                    return;

                try
                {
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
                        ProyectoId = _proyectoId
                    };

                    var result = await CatalogItemService.SaveMaquinariaAsync(_context, dto);

                    _maquinaria = _context.Maquinaria
                        .First(x => x.Id == result.EntityId);
                    _esNuevo = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error al guardar antes de calcular:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // Abrir calculadora con el objeto ya persistido en BD
            using var form = new FormCalculoCostoHorario(_context, _maquinaria);
            if (form.ShowDialog() == DialogResult.OK)
            {
                CargarDatos();
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
        
        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            if (ValidarYGuardar())
            {
                try
                {
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
                        ProyectoId = _proyectoId
                    };

                    var result = await CatalogItemService.SaveMaquinariaAsync(_context, dto, _esNuevo ? null : _maquinaria);

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
                    MessageBox.Show(
                        $"Error al guardar:\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
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