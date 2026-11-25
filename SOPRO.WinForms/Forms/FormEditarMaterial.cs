using SOPRO.WinForms.Helpers;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarMaterial : Form
    {
        private readonly ProjectSessionInfo _sessionInfo;
        private readonly int? _proyectoId;
        private readonly MaterialListItem _material;
        private readonly bool _esNuevo;
        private readonly IProjectDbContextFactory _factory;
        private int _generacionAutocompletadoClave;

        /// <summary>
        /// Seam por instancia de los diálogos del autocompletado por clave. En
        /// producción conserva los <see cref="MessageBox"/> existentes; las pruebas
        /// STA lo sustituyen para ejercitar el flujo sin abrir ventanas modales.
        /// No es un hook estático: cada editor tiene el suyo.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> MostrarDialogo
        { get; set; } = static (mensaje, titulo, botones, icono) =>
            MessageBox.Show(mensaje, titulo, botones, icono);

        /// <summary>Id del material persistido por el último guardado exitoso (para el selector de insumos).</summary>
        public int? UltimoMaterialIdGuardado { get; private set; }

        /// <summary>Si el último guardado fue en el catálogo maestro (su Id no existe en el proyecto).</summary>
        public bool UltimoGuardadoEnMaestro { get; private set; }

        public FormEditarMaterial(
            ProjectSessionInfo sessionInfo,
            MaterialListItem material = null,
            IProjectDbContextFactory? factory = null)
        {
            InitializeComponent();

            _sessionInfo = sessionInfo ?? throw new ArgumentNullException(nameof(sessionInfo));
            _proyectoId = sessionInfo.Project.ProjectId;
            _material = material;
            _esNuevo = material == null;
            _factory = factory ?? new ProjectDbContextFactory();

            ConfigurarFormulario();
            AplicarCromoVisual();
            SeleccionTotalNumericUpDown.Configurar(nudPrecio);

            // Cualquier edición posterior de la clave invalida la consulta en curso
            // (A→B→A no puede aceptar la respuesta vieja de A). El descarte/cierre
            // también la invalida para no tocar controles dispuestos.
            txtClave.TextChanged += (_, _) => InvalidarAutocompletadoClave();
            Disposed += (_, _) => InvalidarAutocompletadoClave();

            if (_esNuevo)
                txtClave.Text = KeySuggestionService.Generate();
            else
                CargarDatos();
        }

        private void ConfigurarFormulario()
        {
            this.Text = _esNuevo ? "Nuevo Material" : "Editar Material";

            // Si estamos en proyecto, ofrecer las dos opciones
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
                // Si no hay proyecto, solo catálogo maestro
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
                new Size(660, 440),
                Text,
                _esNuevo ? "Agrega un material al catálogo" : "Actualiza los datos del material",
                CrearTablaCampos(),
                btnGuardar,
                btnCancelar);
        }

        /// <summary>
        /// Rejilla de campos dentro de la tarjeta. Filas y columnas medidas por
        /// el propio contenedor (AutoSize) con la fuente real en el DPI del
        /// monitor: sin posiciones absolutas que se rompan a 125/150/200 %.
        /// El orden de filas conserva el TabIndex del diseñador.
        /// </summary>
        private TableLayoutPanel CrearTablaCampos()
        {
            var tabla = SoproDialogTheme.CrearRejillaCampos();
            tabla.RowCount = 6;
            tabla.RowStyles.Clear();
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            foreach (var rotulo in new[] { lblClave, lblDescripcion, lblUnidad, lblPrecio, lblNotas })
                SoproDialogTheme.AplicarEstiloRotulo(rotulo);

            foreach (var campo in new Control[] { txtClave, txtDescripcion, txtUnidad, nudPrecio, txtNotas })
                SoproDialogTheme.AplicarEstiloCampo(campo);

            txtClave.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtDescripcion.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtUnidad.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            nudPrecio.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            nudPrecio.Margin = new Padding(0, 4, 0, 4);
            txtNotas.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtNotas.Margin = new Padding(0, 4, 0, 0);

            tabla.Controls.Add(lblClave, 0, 0);
            tabla.Controls.Add(txtClave, 1, 0);
            tabla.Controls.Add(lblDescripcion, 0, 1);
            tabla.Controls.Add(txtDescripcion, 1, 1);
            tabla.SetColumnSpan(txtDescripcion, 3);
            tabla.Controls.Add(lblUnidad, 0, 2);
            tabla.Controls.Add(txtUnidad, 1, 2);
            tabla.Controls.Add(lblPrecio, 2, 2);
            tabla.Controls.Add(nudPrecio, 3, 2);
            tabla.Controls.Add(lblNotas, 0, 3);
            tabla.SetColumnSpan(lblNotas, 4);
            tabla.Controls.Add(txtNotas, 0, 4);
            tabla.SetColumnSpan(txtNotas, 4);

            return tabla;
        }

        private void CargarDatos()
        {
            txtClave.Text = _material.Clave;
            txtDescripcion.Text = _material.Descripcion;
            txtUnidad.Text = _material.Unidad;
            nudPrecio.Value = _material.PrecioUnitario;
            txtNotas.Text = _material.Notas;

            // El destino del guardado se decide por alcance, no por Origen: en
            // sesión de proyecto ListMaterials devuelve únicamente filas del
            // proyecto, aunque una copia importada conserve Origen = Maestro.
            // Marcar rbMaestro a partir de ese Origen desactualizado escribiría
            // en el catálogo maestro sin que el usuario lo pida (el grupo está
            // oculto, no es una elección explícita).
            rbProyecto.Checked = _proyectoId.HasValue;
            rbMaestro.Checked = !_proyectoId.HasValue;
        }

        private async void txtClave_Leave(object sender, EventArgs e)
        {
            // Auto-completar desde material existente con la misma clave
            // (versión headless del flujo legacy; la consulta vive en el caso de uso).
            if (!_proyectoId.HasValue) return;

            string clave = txtClave.Text.Trim();
            if (string.IsNullOrWhiteSpace(clave)) return;

            int generacion = ++_generacionAutocompletadoClave;

            Result<MaterialListItem?> result;
            try
            {
                result = await new FindMaterialByKey(_factory).Execute(
                    _sessionInfo, new FindMaterialByKeyRequest(clave));
            }
            catch (OperationCanceledException)
            {
                // Cierre o cancelación esperada: no es un fallo visible.
                return;
            }
            catch (Exception ex)
            {
                if (EsBusquedaVigente(generacion, clave))
                    ReportarErrorBusqueda(clave, ex.Message);
                return;
            }

            // La respuesta pudo llegar después de cerrar el editor, de cambiar la
            // clave o de iniciar otra consulta: no debe tocar los controles.
            if (!EsBusquedaVigente(generacion, clave)) return;

            if (!result.IsSuccess)
            {
                // Un fallo inesperado de la consulta es recuperable: se informa y el
                // editor sigue utilizable. "No encontrado" (Ok con null) es silencioso.
                ReportarErrorBusqueda(clave, result.Error!.Message);
                return;
            }

            if (result.Value == null) return;

            var materialExistente = result.Value;
            if (!_esNuevo && materialExistente.Id == _material.Id) return;

            // Si es NUEVO → auto-completar silenciosamente
            if (_esNuevo)
            {
                AplicarMaterialExistente(materialExistente);
                return;
            }

            // Si es EDICIÓN → confirmar reemplazo
            var resultMensaje = MostrarDialogo(
                $"La clave '{clave}' ya está asignada a:\n\n" +
                $"  → {materialExistente.Descripcion}\n" +
                $"  → Precio: {materialExistente.PrecioUnitario:C2}\n\n" +
                $"¿Desea REEMPLAZAR los datos actuales con los de este material?",
                "Clave Duplicada - Confirmar Reemplazo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            // La clave pudo cambiar mientras el diálogo estaba abierto.
            if (!EsBusquedaVigente(generacion, clave)) return;

            if (resultMensaje == DialogResult.Yes)
            {
                AplicarMaterialExistente(materialExistente);
            }
            else
            {
                // Restaurar clave anterior
                txtClave.Text = _material != null ? _material.Clave : string.Empty;
                txtClave.Focus();
            }
        }

        private void AplicarMaterialExistente(MaterialListItem material)
        {
            txtDescripcion.Text = material.Descripcion;
            txtUnidad.Text = material.Unidad;
            nudPrecio.Value = material.PrecioUnitario;
            txtNotas.Text = material.Notas;
        }

        private void ReportarErrorBusqueda(string clave, string detalle)
        {
            MostrarDialogo(
                $"No se pudo buscar la clave '{clave}'.\n\n{detalle}\n\n" +
                "El editor sigue disponible; puede continuar capturando el material.",
                "Búsqueda de material",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        private bool EsBusquedaVigente(int generacion, string clave)
        {
            if (IsDisposed || Disposing) return false;
            if (generacion != _generacionAutocompletadoClave) return false;
            return string.Equals(txtClave.Text.Trim(), clave, StringComparison.OrdinalIgnoreCase);
        }

        private void InvalidarAutocompletadoClave() => _generacionAutocompletadoClave++;

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            InvalidarAutocompletadoClave();
            base.OnFormClosed(e);
        }

        private bool _guardando;

        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            if (_guardando) return;
            _guardando = true;
            btnGuardar.Enabled = false;
            try
            {
                // Validaciones y persistencia viven en el caso de uso SaveMaterial
                // (probables sin formularios, PLAN-01 §12).
                var result = await new SaveMaterial(_factory).Execute(_sessionInfo, new SaveMaterialRequest(
                    MaterialId: _esNuevo ? null : _material.Id,
                    Clave: txtClave.Text,
                    Descripcion: txtDescripcion.Text,
                    Unidad: txtUnidad.Text,
                    PrecioUnitario: nudPrecio.Value,
                    Notas: txtNotas.Text,
                    SaveToMaster: rbMaestro.Checked,
                    ProjectId: _proyectoId));

                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.Error!.Message,
                        result.Error!.Code == AppErrorCode.Conflict ? "Clave Duplicada" : "Error al guardar el material",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                UltimoMaterialIdGuardado = result.Value!.MaterialId;
                UltimoGuardadoEnMaestro = rbMaestro.Checked;

                if (result.Value.TriggeredRecalculation)
                    OpenFormsRefreshHelper.RefrescarPresupuestosAbiertos();

                OpenFormsRefreshHelper.RefrescarCatalogosMaterialesAbiertos();

                MessageBox.Show(
                    result.Value.IsNew ? "Material creado exitosamente." : "Material actualizado exitosamente.",
                    "Guardado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException)
            {
                // Carga cancelada o guardado cancelado: no mostrar error.
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al guardar el material:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
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