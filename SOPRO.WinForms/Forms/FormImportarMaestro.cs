using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    public partial class FormImportarMaestro : Form
    {
        private readonly SOPROContext _projectContext;
        private readonly int _proyectoId;
        private readonly TipoInsumo _tipoInsumo;
        private SOPROContext _masterContext;
        private List<int> _selectedIds = new List<int>();
        
        public FormImportarMaestro(SOPROContext projectContext, int proyectoId, TipoInsumo tipoInsumo)
        {
            InitializeComponent();
            
            _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
            _proyectoId = proyectoId;
            _tipoInsumo = tipoInsumo;
            
            ConfigurarFormulario();
            InicializarMasterContext();
            CargarInsumosMaestros();
        }
        
        private void ConfigurarFormulario()
        {
            string tipoTexto = _tipoInsumo switch
            {
                TipoInsumo.Material => "Materiales",
                TipoInsumo.ManoDeObra => "Mano de Obra",
                TipoInsumo.Maquinaria => "Maquinaria",
                TipoInsumo.Matriz => "Matrices (APUs)",
                _ => "Insumos"
            };
            
            this.Text = $"Importar {tipoTexto} desde Catálogo Maestro";
            lblTitulo.Text = $"Importar {tipoTexto} desde Catálogo Maestro";
            
            // Configurar grid
            dgvMaestros.AutoGenerateColumns = false;
            dgvMaestros.AllowUserToAddRows = false;
            dgvMaestros.AllowUserToDeleteRows = false;
            dgvMaestros.ReadOnly = true;
            dgvMaestros.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMaestros.MultiSelect = true;
            dgvMaestros.RowHeadersVisible = false;
            dgvMaestros.BackgroundColor = Color.White;
            dgvMaestros.ColumnHeadersHeight = 35;
            dgvMaestros.RowTemplate.Height = 30;
            
            // Columnas comunes
            ConfigurarColumnas();
        }
        
        private void ConfigurarColumnas()
        {
            dgvMaestros.Columns.Clear();
            
            // Checkbox
            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colSeleccionar",
                HeaderText = "☑",
                Width = 40
            };
            dgvMaestros.Columns.Add(colCheck);
            
            // Clave
            dgvMaestros.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colClave",
                HeaderText = "Clave",
                DataPropertyName = "Clave",
                Width = 120
            });
            
            // Descripción
            dgvMaestros.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDescripcion",
                HeaderText = "Descripción",
                DataPropertyName = "Descripcion",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            
            // Unidad
            dgvMaestros.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUnidad",
                HeaderText = "Unidad",
                DataPropertyName = "Unidad",
                Width = 80
            });
            
            // Precio/Costo
            string headerPrecio = _tipoInsumo switch
            {
                TipoInsumo.Material => "Precio",
                TipoInsumo.ManoDeObra => "Salario Real",
                TipoInsumo.Maquinaria => "Costo Horario",
                _ => "Monto"
            };
            
            dgvMaestros.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPrecio",
                HeaderText = headerPrecio,
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "C2"
                }
            });
        }
        
        private void InicializarMasterContext()
        {
            var masterDbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SOPRO",
                "CatalogoMaestro.db"
            );
            
            if (!File.Exists(masterDbPath))
            {
                MessageBox.Show(
                    "No se encontró el catálogo maestro.\n\n" +
                    "El archivo debería estar en:\n" + masterDbPath,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Close();
                return;
            }
            
            _masterContext = new SOPROContext(masterDbPath);
            SOPRO.Application.Services.SchemaManager.EnsureCurrentSchema(_masterContext);
        }
        
        private async void CargarInsumosMaestros()
        {
            try
            {
                lblStatus.Text = "Cargando...";
                
                switch (_tipoInsumo)
                {
                    case TipoInsumo.Material:
                        await CargarMateriales();
                        break;
                    case TipoInsumo.ManoDeObra:
                        await CargarManoDeObra();
                        break;
                    case TipoInsumo.Maquinaria:
                        await CargarMaquinaria();
                        break;
                    case TipoInsumo.Matriz:
                        await CargarMatrices();
                        break;
                }
                
                lblStatus.Text = $"{dgvMaestros.RowCount} item(s) disponibles en catálogo maestro";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al cargar el catálogo maestro:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
        
        private async System.Threading.Tasks.Task CargarMateriales()
        {
            var materiales = await _masterContext.Materiales
                .Where(m => m.ProyectoId == null) // Solo maestros
                .OrderBy(m => m.Clave)
                .ToListAsync();
            
            dgvMaestros.DataSource = null;
            dgvMaestros.DataSource = materiales;
            
            // Configurar columna de precio
            foreach (DataGridViewRow row in dgvMaestros.Rows)
            {
                var material = row.DataBoundItem as Material;
                if (material != null)
                {
                    row.Cells["colPrecio"].Value = material.PrecioUnitario;
                }
            }
        }
        
        private async System.Threading.Tasks.Task CargarManoDeObra()
        {
            var manoDeObra = await _masterContext.ManoDeObra
                .Where(mo => mo.ProyectoId == null)
                .OrderBy(mo => mo.Clave)
                .ToListAsync();
            
            dgvMaestros.DataSource = null;
            dgvMaestros.DataSource = manoDeObra;
            
            foreach (DataGridViewRow row in dgvMaestros.Rows)
            {
                var mo = row.DataBoundItem as ManoDeObra;
                if (mo != null)
                {
                    row.Cells["colPrecio"].Value = mo.SalarioReal;
                }
            }
        }
        
        private async System.Threading.Tasks.Task CargarMaquinaria()
        {
            var maquinaria = await _masterContext.Maquinaria
                .Where(m => m.ProyectoId == null)
                .OrderBy(m => m.Clave)
                .ToListAsync();
            
            dgvMaestros.DataSource = null;
            dgvMaestros.DataSource = maquinaria;
            
            foreach (DataGridViewRow row in dgvMaestros.Rows)
            {
                var maq = row.DataBoundItem as Maquinaria;
                if (maq != null)
                {
                    row.Cells["colPrecio"].Value = maq.CostoHorario;
                }
            }
        }

        private async System.Threading.Tasks.Task CargarMatrices()
        {
            var matrices = await _masterContext.Matrices
                .Where(m => m.ProyectoId == null) // Solo maestros
                .OrderBy(m => m.Clave)
                .ToListAsync();
            
            dgvMaestros.DataSource = null;
            dgvMaestros.DataSource = matrices;
            
            foreach (DataGridViewRow row in dgvMaestros.Rows)
            {
                var matriz = row.DataBoundItem as Matriz;
                if (matriz != null)
                {
                    row.Cells["colPrecio"].Value = matriz.CostoDirecto;
                }
            }
        }
        
        private void chkSeleccionarTodos_CheckedChanged(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgvMaestros.Rows)
            {
                row.Cells["colSeleccionar"].Value = chkSeleccionarTodos.Checked;
            }
        }
        
        private async void btnImportar_Click(object sender, EventArgs e)
        {
            // Obtener IDs seleccionados
            _selectedIds.Clear();
            
            foreach (DataGridViewRow row in dgvMaestros.Rows)
            {
                var isChecked = row.Cells["colSeleccionar"].Value as bool? ?? false;
                if (isChecked)
                {
                    int id = 0;
                    
                    if (_tipoInsumo == TipoInsumo.Material)
                        id = (row.DataBoundItem as Material)?.Id ?? 0;
                    else if (_tipoInsumo == TipoInsumo.ManoDeObra)
                        id = (row.DataBoundItem as ManoDeObra)?.Id ?? 0;
                    else if (_tipoInsumo == TipoInsumo.Maquinaria)
                        id = (row.DataBoundItem as Maquinaria)?.Id ?? 0;
                    else if (_tipoInsumo == TipoInsumo.Matriz)
                        id = (row.DataBoundItem as Matriz)?.Id ?? 0;
                    
                    if (id > 0)
                        _selectedIds.Add(id);
                }
            }
            
            if (_selectedIds.Count == 0)
            {
                MessageBox.Show(
                    "Seleccione al menos un item para importar.",
                    "Selección Requerida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }
            
            var result = MessageBox.Show(
                $"¿Importar {_selectedIds.Count} item(s) al proyecto?\n\n" +
                $"Los items seleccionados se copiarán al catálogo del proyecto.",
                "Confirmar Importación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            
            if (result == DialogResult.Yes)
            {
                try
                {
                    int importados = await ImportarSeleccionados();
                    
                    MessageBox.Show(
                        $"{importados} item(s) importados exitosamente.",
                        "Importación Completa",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error al importar:\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }
        
        private async System.Threading.Tasks.Task<int> ImportarSeleccionados()
        {
            int count = 0;
            
            switch (_tipoInsumo)
            {
                case TipoInsumo.Material:
                    count = await ImportarMateriales();
                    break;
                case TipoInsumo.ManoDeObra:
                    count = await ImportarManoDeObra();
                    break;
                case TipoInsumo.Maquinaria:
                    count = await ImportarMaquinaria();
                    break;
                case TipoInsumo.Matriz:
                    count = await ImportarMatrices();
                    break;
            }
            
            return count;
        }
        
        private async System.Threading.Tasks.Task<int> ImportarMateriales()
        {
            var materiales = await _masterContext.Materiales
                .Where(m => _selectedIds.Contains(m.Id))
                .ToListAsync();
            
            int count = 0;
            
            foreach (var material in materiales)
            {
                // Verificar si ya existe en el proyecto
                var existe = await _projectContext.Materiales
                    .AnyAsync(m => m.Clave == material.Clave && m.ProyectoId == _proyectoId);
                
                if (!existe)
                {
                    var nuevoMaterial = new Material
                    {
                        Clave = material.Clave,
                        Descripcion = material.Descripcion,
                        Unidad = material.Unidad,
                        PrecioUnitario = material.PrecioUnitario,
                        Notas = material.Notas,
                        Origen = OrigenInsumo.Maestro,
                        MaterialMaestroId = material.Id,
                        ProyectoId = _proyectoId
                    };
                    
                    _projectContext.Materiales.Add(nuevoMaterial);
                    count++;
                }
            }
            
            await _projectContext.SaveChangesAsync();
            return count;
        }
        
        private async System.Threading.Tasks.Task<int> ImportarManoDeObra()
        {
            var manoDeObra = await _masterContext.ManoDeObra
                .Where(mo => _selectedIds.Contains(mo.Id))
                .ToListAsync();
            
            int count = 0;
            
            foreach (var mo in manoDeObra)
            {
                var existe = await _projectContext.ManoDeObra
                    .AnyAsync(m => m.Clave == mo.Clave && m.ProyectoId == _proyectoId);
                
                if (!existe)
                {
                    var nuevoMO = new ManoDeObra
                    {
                        Clave = mo.Clave,
                        Descripcion = mo.Descripcion,
                        Unidad = mo.Unidad,
                        SalarioBase = mo.SalarioBase,
                        FactorSalarioReal = mo.FactorSalarioReal,
                        SalarioReal = mo.SalarioReal,
                        Notas = mo.Notas,
                        Origen = OrigenInsumo.Maestro,
                        ManoDeObraMaestraId = mo.Id,
                        ProyectoId = _proyectoId
                    };
                    
                    _projectContext.ManoDeObra.Add(nuevoMO);
                    count++;
                }
            }
            
            await _projectContext.SaveChangesAsync();
            return count;
        }
        
        private async System.Threading.Tasks.Task<int> ImportarMaquinaria()
        {
            var maquinaria = await _masterContext.Maquinaria
                .Where(m => _selectedIds.Contains(m.Id))
                .ToListAsync();
            
            int count = 0;
            
            foreach (var maq in maquinaria)
            {
                var existe = await _projectContext.Maquinaria
                    .AnyAsync(m => m.Clave == maq.Clave && m.ProyectoId == _proyectoId);
                
                if (!existe)
                {
                    var nuevoMaq = new Maquinaria
                    {
                        Clave = maq.Clave,
                        Descripcion = maq.Descripcion,
                        PotenciaNominal = maq.PotenciaNominal,
                        TipoCombustible = maq.TipoCombustible,
                        CostoHorario = maq.CostoHorario,
                        EsCostoCalculado = maq.EsCostoCalculado,
                        // Copiar todos los demás campos...
                        ValorAdquisicion = maq.ValorAdquisicion,
                        ValorLlantas = maq.ValorLlantas,
                        ValorPiezasEspeciales = maq.ValorPiezasEspeciales,
                        FactorRescate = maq.FactorRescate,
                        VidaEconomica = maq.VidaEconomica,
                        TasaInteres = maq.TasaInteres,
                        HorasEfectivasAnio = maq.HorasEfectivasAnio,
                        PrimaSeguro = maq.PrimaSeguro,
                        FactorMantenimiento = maq.FactorMantenimiento,
                        CantidadCombustible = maq.CantidadCombustible,
                        PrecioCombustible = maq.PrecioCombustible,
                        CantidadAceite = maq.CantidadAceite,
                        PrecioAceite = maq.PrecioAceite,
                        NumeroLlantas = maq.NumeroLlantas,
                        VidaEconomicaLlantas = maq.VidaEconomicaLlantas,
                        VidaPiezasEspeciales = maq.VidaPiezasEspeciales,
                        SalarioOperador = maq.SalarioOperador,
                        FactorSalarioReal = maq.FactorSalarioReal,
                        HorasEfectivasTurno = maq.HorasEfectivasTurno,
                        Notas = maq.Notas,
                        Origen = OrigenInsumo.Maestro,
                        MaquinariaMaestraId = maq.Id,
                        ProyectoId = _proyectoId
                    };
                    
                    _projectContext.Maquinaria.Add(nuevoMaq);
                    count++;
                }
            }
            
            await _projectContext.SaveChangesAsync();
            return count;
        }

        private async System.Threading.Tasks.Task<int> ImportarMatrices()
        {
            return await MatrixApplicationService.ImportMatricesFromMasterAsync(
                _projectContext,
                _masterContext,
                _proyectoId,
                _selectedIds);
        }
        
        private void btnCancelar_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
