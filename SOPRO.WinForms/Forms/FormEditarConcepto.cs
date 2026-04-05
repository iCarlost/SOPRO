using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarConcepto : Form
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly ConceptoPresupuesto _concepto;
        private readonly bool _esNuevo;
        private readonly int? _padreId;
        
        public ConceptoPresupuesto ConceptoResultado { get; private set; }
        
        /// <summary>
        /// Constructor para nuevo concepto/agrupador
        /// </summary>
        public FormEditarConcepto(SOPROContext context, int proyectoId, bool esAgrupador, int? padreId = null)
        {
            _context = context;
            _proyectoId = proyectoId;
            _esNuevo = true;
            _padreId = padreId;
            
            InitializeComponent();
            ConfigurarModo(esAgrupador);
        }
        
        /// <summary>
        /// Constructor para editar concepto existente
        /// </summary>
        public FormEditarConcepto(SOPROContext context, ConceptoPresupuesto concepto)
        {
            _context = context;
            _concepto = concepto;
            _proyectoId = concepto.ProyectoId;
            _esNuevo = false;
            
            InitializeComponent();
            ConfigurarModo(concepto.EsAgrupador);
            CargarDatos();
        }
        
        private void ConfigurarModo(bool esAgrupador)
        {
            if (esAgrupador)
            {
                this.Text = _esNuevo ? "Nuevo Agrupador" : "Editar Agrupador";
                lblTitulo.Text = _esNuevo ? "NUEVO AGRUPADOR" : "EDITAR AGRUPADOR";
                
                // Ocultar campos de concepto
                lblMatriz.Visible = false;
                cboMatriz.Visible = false;
                lblCantidad.Visible = false;
                nudCantidad.Visible = false;
                lblUnidad.Visible = false;
                lblCostoUnitario.Visible = false;
                lblCostoUnitarioValor.Visible = false;
                lblImporte.Visible = false;
                lblImporteValor.Visible = false;
            }
            else
            {
                this.Text = _esNuevo ? "Nuevo Concepto" : "Editar Concepto";
                lblTitulo.Text = _esNuevo ? "NUEVO CONCEPTO" : "EDITAR CONCEPTO";
                
                CargarMatrices();
            }
        }
        
        private void CargarMatrices()
        {
            var matrices = _context.Matrices
                .Where(m => m.ProyectoId == _proyectoId)
                .OrderBy(m => m.Clave)
                .ToList();
            
            cboMatriz.DataSource = matrices;
            cboMatriz.DisplayMember = "Descripcion";
            cboMatriz.ValueMember = "Id";
        }
        
        private void CargarDatos()
        {
            txtClave.Text = _concepto.Clave;
            txtDescripcion.Text = _concepto.Descripcion;
            
            if (!_concepto.EsAgrupador)
            {
                if (_concepto.MatrizId.HasValue)
                {
                    cboMatriz.SelectedValue = _concepto.MatrizId.Value;
                }
                nudCantidad.Value = _concepto.Cantidad;
                lblCostoUnitarioValor.Text = _concepto.CostoDirectoUnitario.ToString("C4");
                lblImporteValor.Text = _concepto.CostoDirectoTotal.ToString("C2");
            }
        }
        
        private void cboMatriz_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboMatriz.SelectedItem is Matriz matriz)
            {
                lblUnidad.Text = matriz.Unidad;
                lblCostoUnitarioValor.Text = matriz.CostoDirecto.ToString("C4");
                CalcularImporte();
            }
        }
        
        private void nudCantidad_ValueChanged(object sender, EventArgs e)
        {
            CalcularImporte();
        }
        
        private void CalcularImporte()
        {
            if (cboMatriz.SelectedItem is Matriz matriz)
            {
                var importe = nudCantidad.Value * matriz.CostoDirecto;
                lblImporteValor.Text = importe.ToString("C2");
            }
        }
        
        private void btnGuardar_Click(object sender, EventArgs e)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(txtClave.Text))
            {
                MessageBox.Show("La clave es obligatoria.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtClave.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                MessageBox.Show("La descripción es obligatoria.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDescripcion.Focus();
                return;
            }
            
            // Validar concepto con matriz
            bool esAgrupador = lblMatriz.Visible == false;
            if (!esAgrupador)
            {
                if (cboMatriz.SelectedItem == null)
                {
                    MessageBox.Show("Seleccione una matriz (APU).", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cboMatriz.Focus();
                    return;
                }
                
                if (nudCantidad.Value <= 0)
                {
                    MessageBox.Show("La cantidad debe ser mayor a cero.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    nudCantidad.Focus();
                    return;
                }
            }
            
            try
            {
                if (_esNuevo)
                {
                    var orden = _context.ConceptosPresupuesto
                        .Where(c => c.ProyectoId == _proyectoId && c.PadreId == _padreId)
                        .Max(c => (int?)c.Orden) ?? 0;
                    
                    ConceptoResultado = new ConceptoPresupuesto
                    {
                        ProyectoId = _proyectoId,
                        PadreId = _padreId,
                        Clave = txtClave.Text.Trim(),
                        Descripcion = txtDescripcion.Text.Trim(),
                        EsAgrupador = esAgrupador,
                        Orden = orden + 1,
                        Notas = string.Empty,
                        Unidad = string.Empty,
                        ColumnasPersonalizadasJSON = string.Empty
                    };
                    
                    if (!esAgrupador)
                    {
                        var matriz = cboMatriz.SelectedItem as Matriz;
                        ConceptoResultado.MatrizId = matriz.Id;
                        ConceptoResultado.Unidad = matriz.Unidad; // Sobrescribir con unidad de la matriz
                        ConceptoResultado.Cantidad = nudCantidad.Value;
                        ConceptoResultado.CostoDirectoUnitario = matriz.CostoDirecto;
                        ConceptoResultado.CostoDirectoTotal = nudCantidad.Value * matriz.CostoDirecto;
                    }
                    
                    _context.ConceptosPresupuesto.Add(ConceptoResultado);
                }
                else
                {
                    _concepto.Clave = txtClave.Text.Trim();
                    _concepto.Descripcion = txtDescripcion.Text.Trim();
                    
                    if (!esAgrupador)
                    {
                        var matriz = cboMatriz.SelectedItem as Matriz;
                        _concepto.MatrizId = matriz.Id;
                        _concepto.Unidad = matriz.Unidad;
                        _concepto.Cantidad = nudCantidad.Value;
                        _concepto.CostoDirectoUnitario = matriz.CostoDirecto;
                        _concepto.CostoDirectoTotal = nudCantidad.Value * matriz.CostoDirecto;
                    }
                    
                    ConceptoResultado = _concepto;
                }
                
                _context.SaveChanges();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                var mensaje = $"Error al guardar:\n{ex.Message}";
                if (ex.InnerException != null)
                {
                    mensaje += $"\n\nDetalle:\n{ex.InnerException.Message}";
                }
                MessageBox.Show(mensaje, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
