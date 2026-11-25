using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarHerramienta : Form
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly Herramienta _herramienta;
        private readonly bool _esNuevo;
        
        public FormEditarHerramienta(SOPROContext context, int proyectoId, Herramienta herramienta = null)
        {
            InitializeComponent();
            
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _herramienta = herramienta;
            _esNuevo = herramienta == null;
            
            ConfigurarFormulario();
            AplicarCromoVisual();
            CargarUnidades();
            SeleccionTotalNumericUpDown.Configurar(nudPrecio);
            
            if (!_esNuevo)
                CargarDatos();
            else
                txtClave.Text = KeySuggestionService.Generate();
        }
        
        private void ConfigurarFormulario()
        {
            this.Text = _esNuevo ? "Nueva Herramienta" : "Editar Herramienta";
            lblTitulo.Text = _esNuevo ? "NUEVA HERRAMIENTA" : "EDITAR HERRAMIENTA";
        }
        
        /// <summary>
        /// Compone el cromo del diálogo con el tema compartido (encabezado,
        /// tarjeta de contenido y pie de acciones) y reacomoda dentro de esa
        /// estructura los controles declarados en el diseñador. Solo
        /// presentación: no altera datos, eventos ni la semántica de guardado.
        /// El encabezado legacy (panelHeader/lblTitulo) queda oculto y sin
        /// consumo de layout, pero VIVO dentro del ownership del form:
        /// ConfigurarFormulario sigue enlazándolo y nada se dispone aquí.
        /// </summary>
        private void AplicarCromoVisual()
        {
            panelHeader.Visible = false;

            SoproDialogTheme.AplicarCromoEditor(
                this,
                new Size(640, 400),
                Text,
                _esNuevo ? "Agrega una herramienta al catálogo" : "Actualiza los datos de la herramienta",
                CrearTablaCampos(),
                btnGuardar,
                btnCancelar);
        }

        /// <summary>
        /// Rejilla de campos dentro de la tarjeta. Filas y columnas medidas por
        /// el propio contenedor (AutoSize) con la fuente real en el DPI del
        /// monitor: sin posiciones absolutas que se rompan a 125/150/200 %.
        /// Con unidad %MO la fila de precio se oculta
        /// (ActualizarEtiquetaPrecio) y la rejilla colapsa sin dejar huecos;
        /// la descripción es multilínea aquí (txtDescripcion).
        /// </summary>
        private TableLayoutPanel CrearTablaCampos()
        {
            var tabla = SoproDialogTheme.CrearRejillaCampos();
            tabla.RowCount = 5;
            for (int fila = 0; fila < 4; fila++)
                tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            foreach (var rotulo in new[] { lblClave, lblDescripcion, lblUnidad, lblPrecio })
                SoproDialogTheme.AplicarEstiloRotulo(rotulo);

            foreach (var campo in new Control[] { txtClave, txtDescripcion, cboUnidad, nudPrecio })
                SoproDialogTheme.AplicarEstiloCampo(campo);

            // Hint de captura de porcentaje (antes gris cursiva): ayuda del tema.
            lblHint.Font = SoproTheme.Fonts.Subtitle;
            lblHint.ForeColor = SoproTheme.Colors.Muted;
            lblHint.Anchor = AnchorStyles.Left;

            txtClave.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtDescripcion.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDescripcion.Margin = new Padding(0, 4, 0, 4);
            cboUnidad.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cboUnidad.Margin = new Padding(0, 4, 0, 4);
            nudPrecio.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            nudPrecio.Margin = new Padding(0, 4, 0, 4);

            tabla.Controls.Add(lblClave, 0, 0);
            tabla.Controls.Add(txtClave, 1, 0);
            tabla.Controls.Add(lblUnidad, 2, 0);
            tabla.Controls.Add(cboUnidad, 3, 0);
            tabla.Controls.Add(lblDescripcion, 0, 1);
            tabla.Controls.Add(txtDescripcion, 1, 1);
            tabla.SetColumnSpan(txtDescripcion, 3);
            tabla.Controls.Add(lblPrecio, 0, 2);
            tabla.Controls.Add(nudPrecio, 1, 2);
            tabla.Controls.Add(lblHint, 0, 3);
            tabla.SetColumnSpan(lblHint, 4);

            return tabla;
        }

        private void CargarUnidades()
        {
            cboUnidad.Items.Clear();
            cboUnidad.Items.Add("pza");
            cboUnidad.Items.Add("hr");
            cboUnidad.Items.Add("%MO");
            cboUnidad.SelectedIndex = 0;
        }
        
        private void CargarDatos()
        {
            txtClave.Text = _herramienta.Clave;
            txtDescripcion.Text = _herramienta.Descripcion;
            cboUnidad.Text = _herramienta.Unidad;

            // Cambiar la unidad ajusta el rango del precio; la etiqueta y el rango
            // deben quedar aplicados antes de asignar el valor heredado.
            ActualizarEtiquetaPrecio();
            AsignarPrecioUnitario(_herramienta.PrecioUnitario);
        }

        private void AsignarPrecioUnitario(decimal valor)
        {
            // Un registro %MO heredado puede traer un porcentaje fuera del rango
            // ordinario (0..100). Se amplía el máximo solo para conservar el dato
            // persistido sin recortarlo ni normalizarlo.
            if (cboUnidad.Text == "%MO" && valor > nudPrecio.Maximum)
                nudPrecio.Maximum = valor;

            nudPrecio.Value = valor;
        }
        
        private void cboUnidad_SelectedIndexChanged(object sender, EventArgs e)
        {
            ActualizarEtiquetaPrecio();
        }
        
        private void ActualizarEtiquetaPrecio()
        {
            if (cboUnidad.Text == "%MO")
            {
                // En una herramienta %MO el porcentaje real vive en la cantidad de
                // cada matriz; el editor no captura precio ni porcentaje. El rango
                // se mantiene tolerante para cargar registros heredados sin recortes.
                lblPrecio.Visible = false;
                nudPrecio.Visible = false;
                lblHint.Visible = false;
                lblPrecio.Text = "Porcentaje (%):";
                lblHint.Text = "Ejemplo: 3.00 para 3% de la mano de obra total";
                nudPrecio.DecimalPlaces = 4;
                nudPrecio.Maximum = 100;  // 100%
                nudPrecio.Minimum = 0;
                nudPrecio.Increment = 0.01m;
            }
            else
            {
                lblPrecio.Visible = true;
                nudPrecio.Visible = true;
                lblPrecio.Text = "Precio Unitario:";
                lblHint.Text = "";
                lblHint.Visible = false;
                nudPrecio.DecimalPlaces = 2;
                nudPrecio.Maximum = 999999;
                nudPrecio.Minimum = 0;
            }
        }
        
        private bool _guardando;

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            if (_guardando) return;
            _guardando = true;
            btnGuardar.Enabled = false;
            try
            {
                Guardar();
            }
            finally
            {
                _guardando = false;
                if (!IsDisposed && !Disposing)
                    btnGuardar.Enabled = true;
            }
        }

        private void Guardar()
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(txtClave.Text))
            {
                MessageBox.Show(
                    "La clave es obligatoria.",
                    "Validación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtClave.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(txtDescripcion.Text))
            {
                MessageBox.Show(
                    "La descripción es obligatoria.",
                    "Validación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtDescripcion.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(cboUnidad.Text))
            {
                MessageBox.Show(
                    "La unidad es obligatoria.",
                    "Validación",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                cboUnidad.Focus();
                return;
            }
            
            // Validar clave única
            var claveExistente = _context.Herramientas
                .Any(h => h.ProyectoId == _proyectoId && 
                         h.Clave == txtClave.Text.Trim() &&
                         (_esNuevo || h.Id != _herramienta.Id));
            
            if (claveExistente)
            {
                MessageBox.Show(
                    $"Ya existe una herramienta con la clave '{txtClave.Text}'.",
                    "Clave Duplicada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtClave.Focus();
                return;
            }
            
            try
            {
                var unidad = cboUnidad.Text.Trim();
                var esPorcentajeMO = string.Equals(unidad, "%MO", StringComparison.OrdinalIgnoreCase);

                if (_esNuevo)
                {
                    var nueva = new Herramienta
                    {
                        ProyectoId = _proyectoId,
                        Clave = txtClave.Text.Trim(),
                        Descripcion = txtDescripcion.Text.Trim(),
                        Unidad = unidad,
                        // Una herramienta %MO no captura precio: su porcentaje vive
                        // en la cantidad de cada matriz.
                        PrecioUnitario = esPorcentajeMO ? 0m : nudPrecio.Value,
                        FechaCreacion = DateTime.Now
                    };
                    
                    _context.Herramientas.Add(nueva);
                }
                else
                {
                    _herramienta.Clave = txtClave.Text.Trim();
                    _herramienta.Descripcion = txtDescripcion.Text.Trim();
                    _herramienta.Unidad = unidad;
                    // Al seguir en %MO se conserva el valor persistido (el control
                    // está oculto); al cambiar de unidad se guarda el precio capturado.
                    if (!esPorcentajeMO)
                        _herramienta.PrecioUnitario = nudPrecio.Value;
                    _herramienta.FechaModificacion = DateTime.Now;
                }
                
                _context.SaveChanges();

                OpenFormsRefreshHelper.RefrescarCatalogosHerramientasAbiertos();
                OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();
                
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
