using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.DTOs.Catalog;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarManoObra : Form
    {
        private readonly SOPROContext _context;
        private readonly int? _proyectoId;
        private readonly ManoDeObra _manoDeObra;
        private readonly bool _esNuevo;
        private readonly Proyecto? _proyecto; // para calcular FSR automáticamente
        private readonly IDiagnosticsSink? _diagnosticsSink;
        private readonly string? _productVersion;

        public FormEditarManoObra(SOPROContext context, int? proyectoId, ManoDeObra manoDeObra = null, Proyecto proyecto = null)
            : this(context, proyectoId, manoDeObra, proyecto, diagnosticsSink: null, productVersion: null)
        {
        }

        /// <summary>
        /// Seam de composición: sink y versión de diagnóstico opcionales, usados al
        /// registrar un FSR que no pudo deserializarse, adaptarse o calcularse. El
        /// constructor público conserva el comportamiento sin diagnóstico.
        /// </summary>
        internal FormEditarManoObra(
            SOPROContext context,
            int? proyectoId,
            ManoDeObra? manoDeObra,
            Proyecto? proyecto,
            IDiagnosticsSink? diagnosticsSink,
            string? productVersion)
        {
            InitializeComponent();
            
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _manoDeObra = manoDeObra;
            _esNuevo = manoDeObra == null;
            _proyecto = proyecto;
            _diagnosticsSink = diagnosticsSink;
            _productVersion = productVersion;
            
            ConfigurarFormulario();
            AplicarCromoVisual();
            SeleccionTotalNumericUpDown.Configurar(nudSalarioBase);
            
            if (_esNuevo)
                txtClave.Text = KeySuggestionService.Generate();
            else
            {
                CargarDatos();
                CalcularSalarioReal();
            }
        }
        
        private void ConfigurarFormulario()
        {
            this.Text = _esNuevo ? "Nueva Mano de Obra" : "Editar Mano de Obra";
            
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
        /// presentación: no altera datos, eventos ni la semántica de guardado.
        /// </summary>
        private void AplicarCromoVisual()
        {
            SoproDialogTheme.AplicarCromoEditor(
                this,
                new Size(680, 500),
                Text,
                _esNuevo ? "Agrega una mano de obra al catálogo" : "Actualiza los datos de la mano de obra",
                CrearTablaCampos(),
                btnGuardar,
                btnCancelar);
        }

        /// <summary>
        /// Rejilla de campos dentro de la tarjeta. Filas y columnas medidas por
        /// el propio contenedor (AutoSize) con la fuente real en el DPI del
        /// monitor: sin posiciones absolutas que se rompan a 125/150/200 %.
        /// El orden de filas conserva el TabIndex del diseñador. Con unidad
        /// %MO las filas de salario se ocultan (ConfigurarCamposSegunUnidad) y
        /// la rejilla colapsa sus filas sin dejar huecos.
        /// </summary>
        private TableLayoutPanel CrearTablaCampos()
        {
            var tabla = SoproDialogTheme.CrearRejillaCampos();
            tabla.RowCount = 8;
            for (int fila = 0; fila < 7; fila++)
                tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            foreach (var rotulo in new[] { lblClave, lblDescripcion, lblUnidad, lblSalarioBase, lblFSR, lblNotas })
                SoproDialogTheme.AplicarEstiloRotulo(rotulo);

            foreach (var campo in new Control[] { txtClave, txtDescripcion, cboUnidad, nudSalarioBase, nudFSR, nudSalarioReal, txtNotas })
                SoproDialogTheme.AplicarEstiloCampo(campo);

            // El salario real es un valor calculado de solo lectura: se destaca
            // con rótulo enfatizado, igual que en el diseño anterior.
            lblSalarioReal.Font = SoproTheme.Fonts.GridHeader;
            lblSalarioReal.ForeColor = SoproTheme.Colors.Ink2;

            // Hint del cálculo (antes gris cursiva): rótulo de ayuda del tema.
            lblInfo.Font = SoproTheme.Fonts.Subtitle;
            lblInfo.ForeColor = SoproTheme.Colors.Muted;

            txtClave.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtDescripcion.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboUnidad.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboUnidad.Margin = new Padding(0, 4, 0, 4);
            nudSalarioBase.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            nudFSR.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            nudSalarioReal.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            nudSalarioReal.Margin = new Padding(0, 4, 0, 4);
            txtNotas.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtNotas.Margin = new Padding(0, 4, 0, 0);

            tabla.Controls.Add(lblClave, 0, 0);
            tabla.Controls.Add(txtClave, 1, 0);
            tabla.Controls.Add(lblUnidad, 2, 0);
            tabla.Controls.Add(cboUnidad, 3, 0);
            tabla.Controls.Add(lblDescripcion, 0, 1);
            tabla.Controls.Add(txtDescripcion, 1, 1);
            tabla.SetColumnSpan(txtDescripcion, 3);
            tabla.Controls.Add(lblSalarioBase, 0, 2);
            tabla.Controls.Add(nudSalarioBase, 1, 2);
            tabla.Controls.Add(lblFSR, 2, 2);
            tabla.Controls.Add(nudFSR, 3, 2);
            tabla.Controls.Add(lblSalarioReal, 0, 3);
            tabla.Controls.Add(nudSalarioReal, 1, 3);
            // Hint en fila propia de ancho completo: nunca se envuelve ni se
            // parte, en ningún DPI.
            tabla.Controls.Add(lblInfo, 0, 4);
            tabla.SetColumnSpan(lblInfo, 4);
            tabla.Controls.Add(lblNotas, 0, 5);
            tabla.SetColumnSpan(lblNotas, 4);
            tabla.Controls.Add(txtNotas, 0, 6);
            tabla.SetColumnSpan(txtNotas, 4);

            return tabla;
        }

        private void CargarDatos()
        {
            txtClave.Text = _manoDeObra.Clave;
            txtDescripcion.Text = _manoDeObra.Descripcion;
            cboUnidad.Text = _manoDeObra.Unidad;
            nudSalarioBase.Value = _manoDeObra.SalarioBase;
            nudFSR.Value = _manoDeObra.FactorSalarioReal;
            nudSalarioReal.Value = _manoDeObra.SalarioReal;
            txtNotas.Text = _manoDeObra.Notas;
            
            // El destino del guardado se decide por propiedad (ProyectoId), no
            // por Origen: una copia importada del maestro vive en el proyecto y
            // conserva Origen = Maestro; su edición debe seguir guardándose en
            // el proyecto, nunca en el catálogo maestro.
            bool esDelProyectoActual = _proyectoId.HasValue && _manoDeObra.ProyectoId == _proyectoId;
            rbProyecto.Checked = esDelProyectoActual;
            rbMaestro.Checked = !esDelProyectoActual;
            
            ConfigurarCamposSegunUnidad(); // Configurar visibilidad según unidad
        }
        
        private void nudSalarioBase_ValueChanged(object sender, EventArgs e)
        {
            AplicarFSRAutomatico();
            CalcularSalarioReal();
        }

        /// <summary>
        /// Si el proyecto tiene parámetros FSR configurados, calcula y aplica
        /// el FSR automáticamente según el SalarioBase actual.
        /// El usuario puede sobreescribir nudFSR manualmente después.
        /// </summary>
        private void AplicarFSRAutomatico()
        {
            if (_proyecto == null) return;
            if (string.IsNullOrEmpty(_proyecto.ParametrosFSR)) return;

            var fsr = FsrCalculationService.Calcular(
                _proyecto.ParametrosFSR,
                nudSalarioBase.Value,
                _diagnosticsSink,
                _productVersion);
            if (fsr.HasValue)
            {
                var fsrRedondeado = Math.Round(fsr.Value, 6);
                var fsrSeguro = Math.Max(nudFSR.Minimum, Math.Min(nudFSR.Maximum, fsrRedondeado));

                nudFSR.ValueChanged -= nudFSR_ValueChanged;
                try
                {
                    nudFSR.Value = fsrSeguro;
                }
                finally
                {
                    nudFSR.ValueChanged += nudFSR_ValueChanged;
                }
            }
        }

        private void nudFSR_ValueChanged(object sender, EventArgs e)
        {
            CalcularSalarioReal();
        }
        
        private void cboUnidad_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigurarCamposSegunUnidad();
        }
        
        private void ConfigurarCamposSegunUnidad()
        {
            bool esPercentMO = cboUnidad.Text?.Trim().ToUpper() == "%MO";
            
            // Ocultar/mostrar campos de salario cuando es %MO
            lblSalarioBase.Visible = !esPercentMO;
            nudSalarioBase.Visible = !esPercentMO;
            lblFSR.Visible = !esPercentMO;
            nudFSR.Visible = !esPercentMO;
            lblSalarioReal.Visible = !esPercentMO;
            nudSalarioReal.Visible = !esPercentMO;
            
            if (esPercentMO)
            {
                // Para %MO, limpiar valores de salario
                nudSalarioBase.Value = 0;
                nudFSR.Value = 1;
                nudSalarioReal.Value = 0;
            }
        }
        
        private void CalcularSalarioReal()
        {
            var salarioReal = nudSalarioBase.Value * nudFSR.Value;
            if (salarioReal < nudSalarioReal.Minimum)
                salarioReal = nudSalarioReal.Minimum;
            else if (salarioReal > nudSalarioReal.Maximum)
                salarioReal = nudSalarioReal.Maximum;

            nudSalarioReal.Value = salarioReal;
        }
        
        private void txtClave_Leave(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue) return;
            
            string clave = txtClave.Text.Trim();
            if (string.IsNullOrWhiteSpace(clave)) return;
            
            var moExistente = _context.ManoDeObra
                .Where(m => m.ProyectoId == _proyectoId.Value)
                .Where(m => m.Clave.ToUpper() == clave.ToUpper())
                .Where(m => _esNuevo || m.Id != _manoDeObra.Id)
                .FirstOrDefault();
            
            if (moExistente != null)
            {
                if (_esNuevo)
                {
                    txtDescripcion.Text = moExistente.Descripcion;
                    nudSalarioBase.Value = moExistente.SalarioBase;
                    nudFSR.Value = moExistente.FactorSalarioReal;
                    txtNotas.Text = moExistente.Notas ?? "";
                    return;
                }
                
                var result = MessageBox.Show(
                    $"La clave '{clave}' ya está asignada a:\n\n" +
                    $"  → {moExistente.Descripcion}\n" +
                    $"  → Salario Real: {moExistente.SalarioReal:C2}\n\n" +
                    $"¿Desea REEMPLAZAR los datos actuales?",
                    "Clave Duplicada",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                
                if (result == DialogResult.Yes)
                {
                    txtDescripcion.Text = moExistente.Descripcion;
                    nudSalarioBase.Value = moExistente.SalarioBase;
                    nudFSR.Value = moExistente.FactorSalarioReal;
                    txtNotas.Text = moExistente.Notas ?? "";
                }
                else
                {
                    if (_manoDeObra != null)
                        txtClave.Text = _manoDeObra.Clave;
                    else
                        txtClave.Text = "";
                    txtClave.Focus();
                }
            }
        }
        
        private bool _guardando;

        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            if (_guardando) return;
            _guardando = true;
            btnGuardar.Enabled = false;
            try
            {
                // Validaciones
                if (string.IsNullOrWhiteSpace(txtClave.Text))
                {
                    MessageBox.Show(
                        "La clave es requerida.",
                        "Campo Requerido",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    txtClave.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
                {
                    MessageBox.Show(
                        "La descripción es requerida.",
                        "Campo Requerido",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    txtDescripcion.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(cboUnidad.Text))
                {
                    MessageBox.Show(
                        "La unidad es requerida.",
                        "Campo Requerido",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    cboUnidad.Focus();
                    return;
                }

                if (_proyectoId.HasValue)
                {
                    var errorClave = KeyValidationService.ValidateUniqueKey(
                        _context,
                        txtClave.Text.Trim(),
                        _proyectoId.Value,
                        _esNuevo ? null : _manoDeObra.Id,
                        CatalogItemType.ManoDeObra
                    );

                    if (errorClave != null)
                    {
                        MessageBox.Show(
                            errorClave,
                            "Clave Duplicada",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                        txtClave.Focus();
                        return;
                    }
                }

                try
                {
                    var dto = new ManoDeObraEditDto
                    {
                        Clave = txtClave.Text,
                        Descripcion = txtDescripcion.Text,
                        Unidad = cboUnidad.Text,
                        SalarioBase = nudSalarioBase.Value,
                        FactorSalarioReal = nudFSR.Value,
                        SalarioReal = nudSalarioReal.Value,
                        Notas = txtNotas.Text,
                        GuardarEnMaestro = rbMaestro.Checked,
                        ProyectoId = _proyectoId
                    };

                    var result = await CatalogItemService.SaveManoDeObraAsync(_context, dto, _esNuevo ? null : _manoDeObra);

                    if (result.TriggeredRecalculation)
                        OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                    OpenFormsRefreshHelper.RefrescarCatalogosManoDeObraAbiertos();

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
            finally
            {
                _guardando = false;
                if (!IsDisposed && !Disposing)
                    btnGuardar.Enabled = true;
            }
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}