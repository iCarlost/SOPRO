using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormEditarMatriz : Form
    {
        /// <summary>
        /// Se dispara cuando se guarda una matriz (nueva o editada).
        /// FormMatrices y FormPresupuesto se suscriben para refrescarse.
        /// </summary>
        public static event EventHandler MatrizGuardada;

        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private readonly Matriz _matriz;
        private readonly bool _esNuevo;
        private readonly List<ComponenteMatriz> _componentesTemp = new List<ComponenteMatriz>();
        
        private decimal _totalMaterial = 0;
        private decimal _baseManoObra = 0;
        private decimal _totalManoObra = 0;
        private decimal _totalManoObraResumen = 0;
        private decimal _totalMaquinaria = 0;
        private decimal _totalBasicos = 0;
        private decimal _costoDirectoTotal = 0;
        
        public FormEditarMatriz(SOPROContext context, int proyectoId, Matriz matriz = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _proyectoId = proyectoId;
            _matriz = matriz;
            _esNuevo = (matriz == null);
            
            // Establecer proyecto para formateo
            var proyecto = context.Proyectos.Find(proyectoId);
            if (proyecto != null)
                FormatoHelper.EstablecerProyecto(proyecto);
            
            // Suscribirse a cambios de configuración
            FormatoHelper.ConfiguracionCambiada += OnConfiguracionCambiada;
            
            InitializeComponent();
            
            dgvComponentes.AplicarEstiloSOPRO();
ConfigurarGrid();
            
            if (!_esNuevo)
            {
                CargarDatosMatriz();
            }
            else
            {
                AplicarConfiguracionTipo();
            }
        }
        
        private void ConfigurarGrid()
        {
            dgvComponentes.AutoGenerateColumns = false;
            dgvComponentes.AllowUserToAddRows = false;
            dgvComponentes.AllowUserToDeleteRows = false;
            dgvComponentes.ReadOnly = false;          // ← ahora editable
            dgvComponentes.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            dgvComponentes.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvComponentes);
            dgvComponentes.RowHeadersVisible = false;
            dgvComponentes.BackgroundColor = Color.White;
            dgvComponentes.RowTemplate.Height = 30;
            
            dgvComponentes.Columns.Clear();
            
            // Tipo - solo lectura siempre
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTipo", HeaderText = "Tipo", Width = 100, ReadOnly = true
            });
            
            // Clave - solo lectura (viene del catálogo)
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colClave", HeaderText = "Clave", Width = 100, ReadOnly = true
            });
            
            // Descripción - EDITABLE (doble click)
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDescripcion", HeaderText = "Descripción",
                Width = 250, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = false
            });
            
            // Unidad - EDITABLE
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUnidad", HeaderText = "Unidad", Width = 70, ReadOnly = false
            });
            
            // Cantidad - EDITABLE directo para Material; doble click abre Rendimiento para M.O./Maq.
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCantidad", HeaderText = "Cantidad", Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N6" },
                ReadOnly = false
            });
            
            // P.U. - EDITABLE para Material; solo lectura para M.O./Maq./Básico (viene del catálogo)
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPU", HeaderText = "P.U.", Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                ReadOnly = false
            });
            
            // Importe - solo lectura, calculado
            dgvComponentes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colImporte", HeaderText = "Importe", Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                ReadOnly = true
            });
            
            var colEliminar = new DataGridViewButtonColumn
            {
                Name = "colEliminar", HeaderText = "", Text = "🗑",
                UseColumnTextForButtonValue = true, Width = 40
            };
            colEliminar.DefaultCellStyle.ForeColor = Color.Red;
            dgvComponentes.Columns.Add(colEliminar);
            
            dgvComponentes.CellClick         += DgvComponentes_CellClick;
            dgvComponentes.CellDoubleClick    += DgvComponentes_CellDoubleClick;
            dgvComponentes.CellEndEdit        += DgvComponentes_CellEndEdit;
            dgvComponentes.CellBeginEdit      += DgvComponentes_CellBeginEdit;
            dgvComponentes.CellFormatting     += DgvComponentes_CellFormatting;
        }
        
        private void CargarDatosMatriz()
        {
            var componentes = _context.ComponentesMatriz
                .Include(c => c.Material)
                .Include(c => c.ManoDeObra)
                .Include(c => c.Maquinaria)
                .Include(c => c.Auxiliar)
                .Include(c => c.Herramienta)
                .Where(c => c.MatrizId == _matriz.Id)
                .ToList();

            var loadState = MatrixEditorInitializationService.BuildLoadState(_matriz, componentes);

            txtClave.Text = loadState.Clave;
            txtDescripcion.Text = loadState.Descripcion;
            cboUnidad.Text = loadState.Unidad;

            rbAPU.Checked = loadState.Tipo == TipoMatriz.APU;
            rbCuadrilla.Checked = loadState.Tipo == TipoMatriz.Cuadrilla;
            rbBasico.Checked = loadState.Tipo == TipoMatriz.Basico;

            MatrixComponentCollectionService.LoadInto(_componentesTemp, loadState.Componentes);
            AplicarConfiguracionTipo();
            ActualizarGridComponentes();
            Recalcular();
        }
        
        private void btnAgregarMaterial_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.Material);
        }

        private void btnAgregarManoObra_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.ManoDeObra);
        }

        private void btnAgregarMaquinaria_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.Maquinaria);
        }

        private void btnAgregarBasico_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.Auxiliar);
        }

        private void btnAgregarHerramienta_Click(object sender, EventArgs e)
        {
            AgregarComponentesDesdeSelector(TipoComponenteMatriz.Herramienta);
        }

        private void AgregarComponentesDesdeSelector(TipoComponenteMatriz tipoComponente)
        {
            var selectedComponents = ComponentSelectorDialogService.SelectComponents(this, _context, _proyectoId, tipoComponente);
            var selectionResult = MatrixComponentSelectionFlowService.AddSelectedComponents(_componentesTemp, selectedComponents);

            if (!selectionResult.HasComponents)
            {
                return;
            }

            ActualizarGridComponentes();

            if (selectionResult.RequiresRecalculation)
            {
                Recalcular();
            }
        }

        private int DecimalesImporte => _context.Proyectos.Find(_proyectoId)?.DecimalesImporte ?? 2;

        private void DgvComponentes_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.Value == null) return;
            var col = dgvComponentes.Columns[e.ColumnIndex].Name;
            if ((col == "colPU" || col == "colImporte") && e.Value is decimal v)
            {
                e.Value = v.ToString($"C{DecimalesImporte}",
                    System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private void ActualizarGridComponentes()
        {
            dgvComponentes.Rows.Clear();

            var rows = MatrixComponentPresentationService.BuildRows(_componentesTemp, _baseManoObra);
            foreach (var row in rows)
            {
                dgvComponentes.Rows.Add(
                    row.Tipo,
                    row.Clave,
                    row.Descripcion,
                    row.Unidad,
                    row.Cantidad,
                    row.PrecioUnitario,
                    row.Importe);
            }
        }
        
        private void Recalcular()
        {
            var totales = MatrixComponentCalculationService.Recalculate(_componentesTemp, FormatoHelper.DecimalesImporte);

            _totalMaterial = totales.TotalMaterial;
            _baseManoObra = totales.BaseManoObra;
            _totalManoObra = totales.TotalManoObra;
            _totalManoObraResumen = totales.TotalManoObraResumen;
            _totalMaquinaria = totales.TotalMaquinaria;
            _totalBasicos = totales.TotalBasicos;
            _costoDirectoTotal = totales.CostoDirectoTotal;

            ActualizarLabelsResumen();
            ActualizarGridComponentes();
        }

        private void ActualizarLabelsResumen()
        {
            lblTotalMaterial.Text = _totalMaterial.ToStringImporte();
            lblTotalManoObra.Text = _totalManoObraResumen.ToStringImporte();
            lblTotalMaquinaria.Text = _totalMaquinaria.ToStringImporte();
            lblTotalBasicos.Text = _totalBasicos.ToStringImporte();
            lblCostoDirecto.Text = _costoDirectoTotal.ToStringImporte();
        }
        
        private void DgvComponentes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (!MatrixComponentGridFlowService.IsDeleteRequest(
                    e.RowIndex,
                    e.ColumnIndex,
                    dgvComponentes.Columns["colEliminar"].Index))
            {
                return;
            }

            var result = MessageBox.Show(
                "¿Eliminar este componente?",
                "Confirmar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes
                && MatrixComponentCollectionService.RemoveAt(_componentesTemp, e.RowIndex))
            {
                ActualizarGridComponentes();
                Recalcular();
            }
        }
        
        /// <summary>
        /// Doble click en la columna Cantidad de M.O. o Maquinaria o Cuadrilla → abre FormRendimiento.
        /// Para Material → edición directa (no hace nada especial aquí).
        /// </summary>
        private void DgvComponentes_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!MatrixComponentCollectionService.TryGetAt(_componentesTemp, e.RowIndex, out var comp) || comp == null)
            {
                return;
            }

            var columnName = dgvComponentes.Columns[e.ColumnIndex].Name;
            if (!MatrixComponentGridFlowService.ShouldOpenRendimientoDialog(comp, columnName))
            {
                return;
            }

            // Cancelar la edición directa que inicia el doble click
            dgvComponentes.CancelEdit();

            var nombre = MatrixComponentInteractionService.GetRendimientoDialogName(comp);

            using var frmRend = new FormRendimiento(nombre, comp.Cantidad);
            if (frmRend.ShowDialog(this) == DialogResult.OK)
            {
                var editResult = MatrixComponentEditingService.UpdateQuantityFromDialog(comp, frmRend.Cantidad);
                if (editResult.Success)
                {
                    BeginInvoke(new Action(() => { ActualizarGridComponentes(); Recalcular(); }));
                }
            }
        }
        
        /// <summary>
        /// Al empezar a editar: bloquear Cantidad en M.O./Maquinaria (se edita con FormRendimiento).
        /// EXCEPTO MO con %MO que se edita directamente.
        /// P.U. es editable para todos los tipos.
        /// </summary>
        private void DgvComponentes_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!MatrixComponentCollectionService.TryGetAt(_componentesTemp, e.RowIndex, out var comp) || comp == null)
            {
                return;
            }

            var columnName = dgvComponentes.Columns[e.ColumnIndex].Name;
            if (!MatrixComponentGridFlowService.CanBeginEdit(comp, columnName))
            {
                e.Cancel = true;
            }
        }
        
        /// <summary>
        /// Al terminar de editar: actualizar componente y recalcular.
        /// Usa BeginInvoke para evitar error reentrante de SetCurrentCellAddressCore.
        /// </summary>
        private void DgvComponentes_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (!MatrixComponentCollectionService.TryGetAt(_componentesTemp, e.RowIndex, out var comp) || comp == null)
            {
                return;
            }

            var cell = dgvComponentes.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var value = cell.Value?.ToString() ?? string.Empty;
            var columnName = dgvComponentes.Columns[e.ColumnIndex].Name;

            var editResult = MatrixComponentGridFlowService.ApplyCellEdit(
                comp,
                columnName,
                value,
                FormatoHelper.DecimalesImporte);

            if (editResult == null)
            {
                return;
            }

            if (!editResult.Success)
            {
                BeginInvoke(new Action(ActualizarGridComponentes));
                return;
            }

            if (editResult.RequiresRecalculation)
            {
                BeginInvoke(new Action(Recalcular));
            }
            else
            {
                BeginInvoke(new Action(ActualizarGridComponentes));
            }
        }
        
        private async void btnGuardar_Click(object sender, EventArgs e)
        {
            var validation = MatrixSaveFlowService.ValidateBeforeSave(txtClave.Text, txtDescripcion.Text, _componentesTemp);
            if (!validation.IsValid)
            {
                MessageBox.Show(validation.Message, "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                FocusValidationField(validation.Field);
                return;
            }

            try
            {
                var claveDuplicada = await MatrixApplicationService.ExistsByKeyAsync(
                    _context,
                    _proyectoId,
                    txtClave.Text,
                    _esNuevo ? null : _matriz?.Id);

                if (claveDuplicada)
                {
                    MessageBox.Show(MatrixSaveFlowService.GetDuplicateKeyMessage(), "Clave Duplicada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtClave.Focus();
                    return;
                }

                var dto = BuildMatrixEditDto();
                var result = await MatrixApplicationService.SaveAsync(_context, dto, _esNuevo ? null : _matriz);

                MessageBox.Show(MatrixSaveFlowService.GetSuccessMessage(result.IsNew), "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                MatrizGuardada?.Invoke(null, EventArgs.Empty);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(MatrixSaveFlowService.BuildErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FocusValidationField(MatrixEditorField field)
        {
            switch (field)
            {
                case MatrixEditorField.Clave:
                    txtClave.Focus();
                    break;
                case MatrixEditorField.Descripcion:
                    txtDescripcion.Focus();
                    break;
                case MatrixEditorField.Componentes:
                    dgvComponentes.Focus();
                    break;
            }
        }

        private MatrixEditDto BuildMatrixEditDto()
        {
            return MatrixEditorService.BuildEditDto(
                _proyectoId,
                txtClave.Text,
                txtDescripcion.Text,
                cboUnidad.Text,
                rbCuadrilla.Checked,
                rbAPU.Checked,
                _costoDirectoTotal,
                _componentesTemp);
        }

        /// <summary>
        /// Event handler cuando cambia el tipo de matriz (APU/Basico/Cuadrilla)
        /// </summary>
        private void rbTipo_CheckedChanged(object sender, EventArgs e)
        {
            AplicarConfiguracionTipo();
        }

        private void AplicarConfiguracionTipo()
        {
            var tipoSeleccionado = MatrixEditorInitializationService.ResolveSelectedType(rbCuadrilla.Checked, rbAPU.Checked);
            var configuration = MatrixEditorInitializationService.BuildTypeConfiguration(tipoSeleccionado, cboUnidad.Text);

            btnAgregarMaterial.Visible = configuration.ShowMaterialButton;
            btnAgregarManoObra.Visible = configuration.ShowManoObraButton;
            btnAgregarMaquinaria.Visible = configuration.ShowMaquinariaButton;
            btnAgregarBasico.Visible = configuration.ShowBasicoButton;
            btnAgregarHerramienta.Visible = configuration.ShowHerramientaButton;

            if (string.IsNullOrWhiteSpace(cboUnidad.Text) && !string.IsNullOrWhiteSpace(configuration.SuggestedUnit))
            {
                cboUnidad.Text = configuration.SuggestedUnit;
            }
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        
        /// <summary>
        /// Maneja el evento de cambio de configuración de decimales
        /// </summary>
        private void OnConfiguracionCambiada(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // Refrescar el grid con los nuevos decimales aunque el tab no esté activo
            if (IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) ActualizarGridComponentes(); }));
            else
                ActualizarGridComponentes();
        }
    }
}
