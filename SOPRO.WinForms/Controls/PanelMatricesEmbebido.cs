using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Controls
{
    public class PanelMatricesEmbebido : UserControl
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private DataGridView _dgvPresupuesto;
        private int _filaActual = -1;
        private int _ultimaFilaCargada = -1;
        private int? _ultimoConceptoId = null;
        private int? _ultimaMatrizId = null;
        private Matriz _matrizActual = null;

        public event EventHandler<int> MatrizActualizada;

        private Panel        _panelHeader;
        private Label        _lblTituloMatriz;
        private Label        _lblInfoMatriz;
        private Button       _btnAnterior;
        private Button       _btnSiguiente;
        private Panel        _panelBotonesAgregar;
        private Button       _btnAgregarMaterial;
        private Button       _btnAgregarMO;
        private Button       _btnAgregarMaquinaria;
        private Button       _btnAgregarHerramienta;
        private Button       _btnAgregarBasico;
        private DataGridView _dgvComponentes;
        private Panel        _panelTotales;
        private Label        _lblMat, _lblMO, _lblMaq, _lblBas, _lblDir;

        public PanelMatricesEmbebido(SOPROContext context, int proyectoId)
        {
            _context    = context;
            _proyectoId = proyectoId;
            
            // Suscribirse a cambios de configuración de decimales
            FormatoHelper.ConfiguracionCambiada += OnConfiguracionCambiada;
            
            InicializarComponentes();
            MostrarSinSeleccion();
        }

        public void ConectarPresupuesto(DataGridView dgv)
        {
            _dgvPresupuesto = dgv;
            dgv.CurrentCellChanged += (s, e) =>
            {
                if (dgv.CurrentRow != null)
                    CargarMatrizDeFila(dgv.CurrentRow.Index, false);
                else
                    MostrarSinSeleccion();
            };
        }
        
        /// <summary>
        /// Fuerza la recarga del panel para la fila indicada.
        /// Llamar después de asignar o cambiar la matriz de un concepto.
        /// </summary>
        public void NotificarFilaCambiada(int fila)
        {
            CargarMatrizDeFila(fila, true);
        }

        private void CargarMatrizDeFila(int fila, bool forceReload)
        {
            if (_dgvPresupuesto == null || fila < 0 || fila >= _dgvPresupuesto.Rows.Count)
            { MostrarSinSeleccion(); return; }

            _filaActual = fila;
            var concepto = _dgvPresupuesto.Rows[fila].Tag as ConceptoPresupuesto;

            if (concepto == null)
            {
                MostrarSinSeleccion("Selecciona un concepto del presupuesto para ver su APU", "");
                return;
            }

            if (concepto.EsAgrupador)
            {
                MostrarSinSeleccion("El elemento seleccionado es un agrupador", "Selecciona una fila tipo concepto para consultar o editar su APU.");
                return;
            }

            if (concepto.MatrizId == null)
            {
                MostrarSinSeleccion("Concepto sin APU asignada", "Usa la columna P.U. o la búsqueda inteligente en Descripción para vincular una matriz.");
                return;
            }

            bool mismaFila = _ultimaFilaCargada == fila;
            bool mismoConcepto = _ultimoConceptoId == concepto.Id;
            bool mismaMatriz = _ultimaMatrizId == concepto.MatrizId;

            if (!forceReload && mismaFila && mismoConcepto && mismaMatriz && _matrizActual != null)
            {
                ActualizarInfoConcepto(concepto);
                ActualizarNavegacion();
                return;
            }

            // Cargar la matriz FRESCA desde BD solo cuando realmente cambia el concepto/matriz
            _matrizActual = _context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .AsNoTracking()
                .FirstOrDefault(m => m.Id == concepto.MatrizId);

            if (_matrizActual == null)
            {
                MostrarSinSeleccion("No se pudo cargar la APU asociada", "La fila apunta a una matriz inexistente o no disponible.");
                return;
            }

            _ultimaFilaCargada = fila;
            _ultimoConceptoId = concepto.Id;
            _ultimaMatrizId = concepto.MatrizId;

            MostrarMatriz(_matrizActual, concepto);
            ActualizarNavegacion();
        }

        private void MostrarMatriz(Matriz matriz, ConceptoPresupuesto concepto)
        {
            _lblTituloMatriz.Text = $"APU:  {matriz.Clave}  —  {matriz.Descripcion}";
            ActualizarInfoConcepto(concepto, matriz);
            _panelBotonesAgregar.Enabled = true;

            _dgvComponentes.Rows.Clear();
            decimal totalMat = 0, totalMO = 0, totalMaq = 0, totalBas = 0;

            foreach (var comp in matriz.Componentes.OrderBy(c => c.TipoComponente).ThenBy(c => c.Orden))
            {
                string tipo = "", clave = "", desc = "", unidad = "";
                decimal pu = 0;

                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        tipo = "📦 Material"; clave = comp.Material?.Clave ?? ""; desc = comp.Material?.Descripcion ?? "";
                        unidad = comp.Material?.Unidad ?? ""; pu = comp.Material?.PrecioUnitario ?? 0;
                        totalMat += comp.Importe; break;
                    case TipoComponenteMatriz.ManoDeObra:
                        tipo = "👷 M.O."; 
                        clave = comp.ManoDeObra?.Clave ?? ""; 
                        desc = comp.ManoDeObra?.Descripcion ?? "";
                        unidad = comp.ManoDeObra?.Unidad ?? "";
                        
                        if (comp.ManoDeObra?.EsPorcentajeMO == true)
                        {
                            // Para %MO: P.U. = Total de Mano de Obra
                            pu = totalMO;
                        }
                        else
                        {
                            pu = comp.ManoDeObra?.SalarioReal ?? 0;
                        }
                        
                        totalMO += comp.Importe; 
                        break;
                    case TipoComponenteMatriz.Maquinaria:
                        tipo = "🚜 Maq."; clave = comp.Maquinaria?.Clave ?? ""; desc = comp.Maquinaria?.Descripcion ?? "";
                        unidad = "hora"; pu = comp.Maquinaria?.CostoHorario ?? 0;
                        totalMaq += comp.Importe; break;
                    case TipoComponenteMatriz.Auxiliar:
                        // Distinguir entre Cuadrilla y Básico
                        if (comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                        {
                            tipo = "👷 Cuadrilla"; 
                            clave = comp.Auxiliar?.Clave ?? ""; 
                            desc = comp.Auxiliar?.Descripcion ?? "";
                            unidad = comp.Auxiliar?.Unidad ?? ""; 
                            pu = comp.Auxiliar?.CostoDirecto ?? 0;
                            totalMO += comp.Importe;  // Sumar como Mano de Obra
                        }
                        else
                        {
                            tipo = "🧩 Básico"; 
                            clave = comp.Auxiliar?.Clave ?? ""; 
                            desc = comp.Auxiliar?.Descripcion ?? "";
                            unidad = comp.Auxiliar?.Unidad ?? ""; 
                            pu = comp.Auxiliar?.CostoDirecto ?? 0;
                            totalBas += comp.Importe;  // Sumar como Básico
                        }
                        break;
                    case TipoComponenteMatriz.Herramienta:
                        tipo = "🛠️ Herramienta";
                        if (comp.Herramienta != null)
                        {
                            clave = comp.Herramienta.Clave;
                            desc = comp.Herramienta.Descripcion;
                            unidad = comp.Herramienta.Unidad;
                            
                            if (comp.Herramienta.EsPorcentajeMO)
                            {
                                // Para %MO: P.U. = Total de Mano de Obra
                                pu = totalMO;
                            }
                            else
                            {
                                pu = comp.Herramienta.PrecioUnitario;
                            }
                        }
                        // Las herramientas se suman al total pero no tienen categoría propia
                        totalBas += comp.Importe;
                        break;
                }

                _dgvComponentes.Rows.Add(tipo, clave, desc, unidad,
                    comp.Cantidad.ToStringCantidad(), pu.ToStringImporte(),
                    comp.Importe.ToStringImporte(), comp.Id, "🗑");
            }

            decimal dir = totalMat + totalMO + totalMaq + totalBas;
            _lblMat.Text = $"Mat: {totalMat.ToStringImporte()}"; _lblMO.Text = $"M.O.: {totalMO.ToStringImporte()}";
            _lblMaq.Text = $"Maq.: {totalMaq.ToStringImporte()}"; _lblBas.Text = $"Bas.: {totalBas.ToStringImporte()}";
            _lblDir.Text = $"Costo Directo: {dir.ToStringImporte()}";
        }

        private void ActualizarInfoConcepto(ConceptoPresupuesto concepto, Matriz matriz = null)
        {
            var matrizInfo = matriz ?? _matrizActual;
            string unidad = matrizInfo?.Unidad ?? concepto?.Unidad ?? "—";
            string claveConcepto = string.IsNullOrWhiteSpace(concepto?.Clave) ? "(sin clave)" : concepto.Clave;
            string descripcionConcepto = string.IsNullOrWhiteSpace(concepto?.Descripcion) ? "(sin descripción)" : concepto.Descripcion;
            _lblInfoMatriz.Text = $"Unidad: {unidad}   |   Concepto en presupuesto: {claveConcepto} - {descripcionConcepto}";
        }

        private void MostrarSinSeleccion(string titulo = "Selecciona un concepto en el presupuesto para ver y editar su APU", string info = "")
        {
            _matrizActual = null;
            _ultimaFilaCargada = -1;
            _ultimoConceptoId = null;
            _ultimaMatrizId = null;
            _lblTituloMatriz.Text = titulo;
            _lblInfoMatriz.Text   = info;
            _panelBotonesAgregar.Enabled = false;
            _dgvComponentes.Rows.Clear();
            _lblMat.Text = "Mat: —"; _lblMO.Text = "M.O.: —";
            _lblMaq.Text = "Maq.: —"; _lblBas.Text = "Bas.: —";
            _lblDir.Text = "Costo Directo: —";
            _btnAnterior.Enabled = _btnSiguiente.Enabled = false;
        }

        // ── Guardar y propagar al presupuesto ─────────────────────────
        // Se llama automáticamente después de agregar/eliminar. No hay botón Guardar.
        private void GuardarYPropagar()
        {
            if (_matrizActual == null || _filaActual < 0) return;
            try
            {
                // Recargar la matriz CON TRACKING para calcular y guardar
                var matrizTracked = _context.Matrices
                    .Include(m => m.Componentes).ThenInclude(c => c.Material)
                    .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                    .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                    .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                    .FirstOrDefault(m => m.Id == _matrizActual.Id);

                if (matrizTracked == null) return;

                // Calcular CostoDirecto con el motor del proyecto
                var _proyectoPME = _context.Proyectos.Find(_proyectoId);
                if (_proyectoPME == null) return; // Sin proyecto no podemos calcular correctamente

                var motor = new SOPRO.Application.Services.MotorCalculoSopro(_proyectoPME);
                var totals = SOPRO.Application.Services.MatrixComponentCalculationService.Recalculate(
                    matrizTracked.Componentes.ToList(), _proyectoPME.DecimalesImporte);
                matrizTracked.CostoDirecto = motor.RedondearImporte(totals.CostoDirectoTotal);

                decimal nuevoCostoDirecto = matrizTracked.CostoDirecto;

                // Calcular PrecioUnitario usando el motor (redondeo por paso en cascada de porcentajes)
                var pctInput = new SOPRO.Application.Models.Presupuesto.BudgetPercentageInput
                {
                    IndirectosCentral      = _proyectoPME.PorcentajeIndirectosCentral,
                    IndirectosCampo        = _proyectoPME.PorcentajeIndirectosCampo,
                    Financiamiento         = _proyectoPME.PorcentajeFinanciamiento,
                    Utilidad               = _proyectoPME.PorcentajeUtilidad,
                    CargosAdicionales      = _proyectoPME.PorcentajeCargosAdicionales,
                    ModoCalculoPorcentajes = _proyectoPME.ModoCalculoPorcentajes ?? "Acumulables"
                };
                decimal nuevoPrecioUnitario = motor.CalcularPrecioUnitario(nuevoCostoDirecto, pctInput).PrecioUnitario;

                // Actualizar el concepto en la fila del presupuesto usando el motor
                var concepto = _dgvPresupuesto.Rows[_filaActual].Tag as ConceptoPresupuesto;
                if (concepto != null)
                {
                    concepto.CostoDirectoUnitario = nuevoCostoDirecto;
                    concepto.CostoDirectoTotal    = motor.Multiplicar(concepto.Cantidad, nuevoCostoDirecto);
                    concepto.PrecioUnitario        = nuevoPrecioUnitario;
                    concepto.ImporteTotal          = motor.Multiplicar(concepto.Cantidad, nuevoPrecioUnitario);
                }

                _context.SaveChanges();

                // Actualizar celdas visualmente en el grid del presupuesto
                // para TODOS los conceptos que usen esta matriz (no solo el actual)
                if (concepto != null)
                {
                    for (int i = 0; i < _dgvPresupuesto.Rows.Count; i++)
                    {
                        var c = _dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                        if (c == null || c.MatrizId != _matrizActual.Id) continue;

                        c.CostoDirectoUnitario = nuevoCostoDirecto;
                        c.CostoDirectoTotal    = motor.Multiplicar(c.Cantidad, nuevoCostoDirecto);
                        c.PrecioUnitario       = nuevoPrecioUnitario;
                        c.ImporteTotal         = motor.Multiplicar(c.Cantidad, nuevoPrecioUnitario);

                        foreach (DataGridViewColumn col in _dgvPresupuesto.Columns)
                        {
                            if (col.Tag is ColumnaPersonalizada colDef)
                            {
                                if (colDef.NombreInterno == "PrecioUnitario")
                                    _dgvPresupuesto.Rows[i].Cells[col.Index].Value = nuevoPrecioUnitario.ToStringImporte();
                                else if (colDef.NombreInterno == "Importe")
                                    _dgvPresupuesto.Rows[i].Cells[col.Index].Value = c.ImporteTotal.ToStringImporte();
                            }
                        }
                    }
                    _context.SaveChanges();
                    _dgvPresupuesto.Refresh();
                    // Notificar para recalcular la jerarquía de totales
                    MatrizActualizada?.Invoke(this, _filaActual);
                }

                // Refrescar el panel con datos frescos
                CargarMatrizDeFila(_filaActual, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Agregar insumos ────────────────────────────────────────────
        private void BtnAgregarMaterial_Click(object sender, EventArgs e)
        {
            if (_matrizActual == null) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.Material);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }

        private void BtnAgregarMO_Click(object sender, EventArgs e)
        {
            if (_matrizActual == null) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.ManoDeObra);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }

        private void BtnAgregarMaquinaria_Click(object sender, EventArgs e)
        {
            if (_matrizActual == null) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.Maquinaria);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }

        private void BtnAgregarBasico_Click(object sender, EventArgs e)
        {
            if (_matrizActual == null) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.Auxiliar);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }
        
        private void BtnAgregarHerramienta_Click(object sender, EventArgs e)
        {
            if (_matrizActual == null) return;
            using var dlg = new Forms.FormSeleccionarInsumo(_context, _proyectoId, TipoComponenteMatriz.Herramienta);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            
            foreach (var componente in dlg.ComponentesSeleccionados)
            {
                componente.MatrizId = _matrizActual.Id;
                componente.Notas ??= "";
                _context.ComponentesMatriz.Add(componente);
            }
            
            _context.SaveChanges();
            GuardarYPropagar();
        }

        // ── Eliminar componente ────────────────────────────────────────
        private void DgvComponentes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvComponentes.Columns["ColEliminar"] == null) return;
            if (e.ColumnIndex != _dgvComponentes.Columns["ColEliminar"].Index) return;
            if (_matrizActual == null) return;

            int compId = Convert.ToInt32(_dgvComponentes.Rows[e.RowIndex].Cells["ColId"].Value);
            if (MessageBox.Show("¿Eliminar este componente?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var comp = _context.ComponentesMatriz.Find(compId);
            if (comp == null) return;
            _context.ComponentesMatriz.Remove(comp);
            _context.SaveChanges();
            GuardarYPropagar();
        }

        // ── Navegación ─────────────────────────────────────────────────
        private void ActualizarNavegacion()
        {
            _btnAnterior.Enabled  = FilaConceptoAnterior() >= 0;
            _btnSiguiente.Enabled = FilaConceptoSiguiente() >= 0;
        }

        // Busca por Orden del ConceptoPresupuesto, no por posición en el grid
        private int FilaConceptoAnterior()
        {
            if (_dgvPresupuesto == null || _filaActual < 0) return -1;
            var conceptoActual = _dgvPresupuesto.Rows[_filaActual].Tag as ConceptoPresupuesto;
            if (conceptoActual == null) return -1;

            // Buscar en el grid el concepto cuyo Orden es menor y más cercano al actual
            int ordenActual = conceptoActual.Orden;
            int filaCandidata = -1;
            int ordenCandidato = int.MinValue;

            for (int i = 0; i < _dgvPresupuesto.Rows.Count; i++)
            {
                var c = _dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                if (c == null || c.EsAgrupador || c.MatrizId == null) continue;
                if (c.Orden < ordenActual && c.Orden > ordenCandidato)
                {
                    ordenCandidato = c.Orden;
                    filaCandidata = i;
                }
            }
            return filaCandidata;
        }

        private int FilaConceptoSiguiente()
        {
            if (_dgvPresupuesto == null || _filaActual < 0) return -1;
            var conceptoActual = _dgvPresupuesto.Rows[_filaActual].Tag as ConceptoPresupuesto;
            if (conceptoActual == null) return -1;

            int ordenActual = conceptoActual.Orden;
            int filaCandidata = -1;
            int ordenCandidato = int.MaxValue;

            for (int i = 0; i < _dgvPresupuesto.Rows.Count; i++)
            {
                var c = _dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                if (c == null || c.EsAgrupador || c.MatrizId == null) continue;
                if (c.Orden > ordenActual && c.Orden < ordenCandidato)
                {
                    ordenCandidato = c.Orden;
                    filaCandidata = i;
                }
            }
            return filaCandidata;
        }

        private void NavegerAFila(int fila)
        {
            try
            {
                if (fila < 0 || fila >= _dgvPresupuesto.Rows.Count) return;
                
                // Primera celda VISIBLE para evitar error de celda invisible
                foreach (DataGridViewColumn col in _dgvPresupuesto.Columns)
                {
                    if (col.Visible)
                    {
                        _dgvPresupuesto.ClearSelection();
                        _dgvPresupuesto.CurrentCell = _dgvPresupuesto.Rows[fila].Cells[col.Index];
                        break;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Ignorar error de celda invisible
            }
        }

        private void BtnAnterior_Click(object sender, EventArgs e)  { int f = FilaConceptoAnterior();  if (f >= 0) NavegerAFila(f); }
        private void BtnSiguiente_Click(object sender, EventArgs e) { int f = FilaConceptoSiguiente(); if (f >= 0) NavegerAFila(f); }

        // ── Construcción de controles ──────────────────────────────────
        private void InicializarComponentes()
        {
            this.Dock = DockStyle.Fill; this.BackColor = Color.White;

            _panelHeader = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(40, 40, 65) };
            _lblTituloMatriz = new Label { Location = new Point(10, 5),  Size = new Size(770, 20), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.White };
            _lblInfoMatriz   = new Label { Location = new Point(10, 28), Size = new Size(770, 16), Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Silver };

            _btnAnterior  = new Button { Text = "▲", Size = new Size(30, 20), Location = new Point(888, 6),  FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60,60,90), ForeColor = Color.White, Enabled = false };
            _btnSiguiente = new Button { Text = "▼", Size = new Size(30, 20), Location = new Point(920, 6),  FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60,60,90), ForeColor = Color.White, Enabled = false };
            _btnAnterior.FlatAppearance.BorderColor = _btnSiguiente.FlatAppearance.BorderColor = Color.FromArgb(90,90,130);
            _btnAnterior.Click  += BtnAnterior_Click;
            _btnSiguiente.Click += BtnSiguiente_Click;

            _panelHeader.Controls.AddRange(new Control[] { _lblTituloMatriz, _lblInfoMatriz, _btnAnterior, _btnSiguiente });

            _panelBotonesAgregar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(245,245,250), Enabled = false };
            var lblAg = new Label { Text = "Agregar:", Location = new Point(8,10), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(80,80,80) };
            _btnAgregarMaterial     = CrearBtn("📦 Material",    new Point(70,  5), Color.FromArgb(76,175,80));
            _btnAgregarMO           = CrearBtn("👷 M.O.",         new Point(180, 5), Color.FromArgb(33,150,243));
            _btnAgregarMaquinaria   = CrearBtn("🚜 Maquinaria",   new Point(290, 5), Color.FromArgb(255,152,0));
            _btnAgregarHerramienta  = CrearBtn("🛠️ Herramienta", new Point(400, 5), Color.FromArgb(96,125,139));
            _btnAgregarBasico       = CrearBtn("🧩 Básico",       new Point(510, 5), Color.FromArgb(156,39,176));
            _btnAgregarMaterial.Click     += BtnAgregarMaterial_Click;
            _btnAgregarMO.Click           += BtnAgregarMO_Click;
            _btnAgregarMaquinaria.Click   += BtnAgregarMaquinaria_Click;
            _btnAgregarHerramienta.Click  += BtnAgregarHerramienta_Click;
            _btnAgregarBasico.Click       += BtnAgregarBasico_Click;
            _panelBotonesAgregar.Controls.AddRange(new Control[] { lblAg, _btnAgregarMaterial, _btnAgregarMO, _btnAgregarMaquinaria, _btnAgregarHerramienta, _btnAgregarBasico });

            _dgvComponentes = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
                ColumnHeadersHeight = 34, RowTemplate = { Height = 28 }, Font = new Font("Segoe UI", 8.5F),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Color.FromArgb(225,225,235)
            };
            _dgvComponentes.EnableHeadersVisualStyles = false;
            _dgvComponentes.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55,55,85);
            _dgvComponentes.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvComponentes.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _dgvComponentes.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248,248,255);

            var right  = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
            var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
            _dgvComponentes.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "ColTipo",     HeaderText = "Tipo",       Width = 105, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColClave",    HeaderText = "C",           Width = 100, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColDesc",     HeaderText = "Descripción", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColUnidad",   HeaderText = "Unidad",      Width = 65,  DefaultCellStyle = center, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColCantidad", HeaderText = "Cantidad",    Width = 95,  DefaultCellStyle = right, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColPU",       HeaderText = "P.U.",        Width = 115, DefaultCellStyle = right, ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "ColImporte",  HeaderText = "Importe",     Width = 115, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) }, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "ColId",       HeaderText = "ID",          Visible = false },
                new DataGridViewButtonColumn  { Name = "ColEliminar", HeaderText = "",            Width = 36 }
            });
            _dgvComponentes.CellClick       += DgvComponentes_CellClick;
            _dgvComponentes.CellDoubleClick += DgvComponentes_PanelCellDoubleClick;
            _dgvComponentes.CellBeginEdit   += DgvComponentes_PanelCellBeginEdit;
            _dgvComponentes.CellEndEdit     += DgvComponentes_PanelCellEndEdit;

            _panelTotales = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.FromArgb(235,235,248) };
            _lblMat = new Label { AutoSize = true, Location = new Point(8,   5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblMO  = new Label { AutoSize = true, Location = new Point(185, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblMaq = new Label { AutoSize = true, Location = new Point(362, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblBas = new Label { AutoSize = true, Location = new Point(539, 5), Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(50,50,50) };
            _lblDir = new Label { AutoSize = true, Location = new Point(716, 5), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(20,100,20) };
            _panelTotales.Controls.AddRange(new Control[] { _lblMat, _lblMO, _lblMaq, _lblBas, _lblDir });

            this.Controls.Add(_dgvComponentes);
            this.Controls.Add(_panelBotonesAgregar);
            this.Controls.Add(_panelTotales);
            this.Controls.Add(_panelHeader);
        }

        private Button CrearBtn(string texto, Point loc, Color color)
        {
            var btn = new Button { Text = texto, Location = loc, Size = new Size(105, 26), FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = Color.White, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ── Edición en el panel ────────────────────────────────────────
        // Obtiene el ComponenteMatriz de BD a partir del ID en la fila
        private ComponenteMatriz ObtenerComponentePorFila(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _dgvComponentes.Rows.Count) return null;
            var idCell = _dgvComponentes.Rows[rowIndex].Cells["ColId"];
            if (idCell?.Value == null) return null;
            int id = Convert.ToInt32(idCell.Value);
            return _context.ComponentesMatriz
                .Include(c => c.Material).Include(c => c.ManoDeObra)
                .Include(c => c.Maquinaria).Include(c => c.Auxiliar)
                .FirstOrDefault(c => c.Id == id);
        }

        private void DgvComponentes_PanelCellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var comp = ObtenerComponentePorFila(e.RowIndex);
            if (comp == null) return;

            bool esMOoMaq = comp.TipoComponente == TipoComponenteMatriz.ManoDeObra ||
                            comp.TipoComponente == TipoComponenteMatriz.Maquinaria;
            bool esCuadrilla = comp.TipoComponente == TipoComponenteMatriz.Auxiliar && 
                              comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla;
            bool esMaterial = comp.TipoComponente == TipoComponenteMatriz.Material;
            int colCant = _dgvComponentes.Columns["ColCantidad"].Index;

            // Cantidad de M.O./Maquinaria/Cuadrilla → solo por FormRendimiento (doble click)
            if (e.ColumnIndex == colCant && (esMOoMaq || esCuadrilla)) { e.Cancel = true; }
            // P.U. de M.O./Maquinaria/Auxiliar → no editable (viene del catálogo)
            int colPU   = _dgvComponentes.Columns["ColPU"].Index;
            if (e.ColumnIndex == colPU && !esMaterial) { e.Cancel = true; }
        }

        private void DgvComponentes_PanelCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex != _dgvComponentes.Columns["ColCantidad"].Index) return;

            var comp = ObtenerComponentePorFila(e.RowIndex);
            if (comp == null) return;
            
            bool esCuadrilla = comp.TipoComponente == TipoComponenteMatriz.Auxiliar && 
                              comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla;
            
            if (comp.TipoComponente != TipoComponenteMatriz.ManoDeObra &&
                comp.TipoComponente != TipoComponenteMatriz.Maquinaria &&
                !esCuadrilla) return;

            _dgvComponentes.CancelEdit();

            string nombre = "";
            if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra)
                nombre = comp.ManoDeObra?.Descripcion ?? "M.O.";
            else if (comp.TipoComponente == TipoComponenteMatriz.Maquinaria)
                nombre = comp.Maquinaria?.Descripcion ?? "Maquinaria";
            else if (esCuadrilla)
                nombre = comp.Auxiliar?.Descripcion ?? "Cuadrilla";

            using var frmRend = new Forms.FormRendimiento(nombre, comp.Cantidad);
            if (frmRend.ShowDialog() == DialogResult.OK)
            {
                decimal pu = 0;
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra)
                    pu = comp.ManoDeObra?.SalarioReal ?? 0;
                else if (comp.TipoComponente == TipoComponenteMatriz.Maquinaria)
                    pu = comp.Maquinaria?.CostoHorario ?? 0;
                else if (esCuadrilla)
                    pu = comp.Auxiliar?.CostoDirecto ?? 0;

                comp.Cantidad = frmRend.Cantidad;
                // Usar motor para redondeo correcto en lugar de multiplicación cruda
                var _mPME1 = _context.Proyectos.Find(_proyectoId);
                int _decPME1 = _mPME1?.DecimalesImporte ?? 2;
                comp.Importe = new SOPRO.Application.Services.MotorCalculoSopro(_decPME1, _decPME1, 4)
                    .Multiplicar(comp.Cantidad, pu);
                _context.SaveChanges();
                // Diferir para evitar error reentrante
                BeginInvoke(new Action(GuardarYPropagar));
            }
        }

        private void DgvComponentes_PanelCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var comp = ObtenerComponentePorFila(e.RowIndex);
            if (comp == null) return;

            var cell = _dgvComponentes.Rows[e.RowIndex].Cells[e.ColumnIndex];
            // Leer el valor ANTES de que el grid lo revierta
            string val = cell.Value?.ToString() ?? "";

            int colDesc = _dgvComponentes.Columns["ColDesc"].Index;
            int colUnid = _dgvComponentes.Columns["ColUnidad"].Index;
            int colCant = _dgvComponentes.Columns["ColCantidad"].Index;
            int colPU   = _dgvComponentes.Columns["ColPU"].Index;

            bool cambio = false;

            if (e.ColumnIndex == colDesc)
            {
                if (comp.Material   != null) { comp.Material.Descripcion   = val; cambio = true; }
                if (comp.ManoDeObra != null) { comp.ManoDeObra.Descripcion = val; cambio = true; }
                if (comp.Maquinaria != null) { comp.Maquinaria.Descripcion = val; cambio = true; }
            }
            else if (e.ColumnIndex == colUnid)
            {
                if (comp.Material   != null) { comp.Material.Unidad   = val; cambio = true; }
                if (comp.ManoDeObra != null) { comp.ManoDeObra.Unidad = val; cambio = true; }
            }
            else if (e.ColumnIndex == colCant)
            {
                string clean = val.Replace("$","").Replace(",","").Trim();
                if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal cant) && cant > 0)
                {
                    comp.Cantidad = cant;
                    
                    // Calcular importe con motor (redondeo correcto por paso)
                    var _mPME2 = _context.Proyectos.Find(_proyectoId);
                    var _motorPME = new SOPRO.Application.Services.MotorCalculoSopro(
                        _mPME2?.DecimalesImporte ?? 2, _mPME2?.DecimalesImporte ?? 2, 4);
                    switch (comp.TipoComponente)
                    {
                        case TipoComponenteMatriz.Material:
                            comp.Importe = _motorPME.Multiplicar(cant, comp.Material?.PrecioUnitario ?? 0);
                            break;
                        case TipoComponenteMatriz.Maquinaria:
                            comp.Importe = _motorPME.Multiplicar(cant, comp.Maquinaria?.CostoHorario ?? 0);
                            break;
                        case TipoComponenteMatriz.Auxiliar:
                            comp.Importe = _motorPME.Multiplicar(cant, comp.Auxiliar?.CostoDirecto ?? 0);
                            break;
                        case TipoComponenteMatriz.ManoDeObra:
                            if (comp.ManoDeObra?.EsPorcentajeMO != true)
                                comp.Importe = _motorPME.Multiplicar(cant, comp.ManoDeObra?.SalarioReal ?? 0);
                            // %MO: GuardarYPropagar recalculará con el servicio central
                            break;
                        case TipoComponenteMatriz.Herramienta:
                            // GuardarYPropagar recalculará con el servicio central
                            break;
                    }
                    
                    cambio = true;
                }
            }
            else if (e.ColumnIndex == colPU)
            {
                // Solo Material puede editar el P.U. directamente
                if (comp.TipoComponente != TipoComponenteMatriz.Material) return;
                string clean = val.Replace("$","").Replace(",","").Trim();
                if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal pu) && pu >= 0)
                {
                    if (comp.Material != null) comp.Material.PrecioUnitario = pu;
                    var _mPME3 = _context.Proyectos.Find(_proyectoId);
                    comp.Importe = new SOPRO.Application.Services.MotorCalculoSopro(
                        _mPME3?.DecimalesImporte ?? 2, _mPME3?.DecimalesImporte ?? 2, 4)
                        .Multiplicar(comp.Cantidad, pu);
                    cambio = true;
                }
            }

            if (cambio)
            {
                _context.SaveChanges();
                // Diferir GuardarYPropagar para evitar error reentrante de SetCurrentCellAddressCore
                BeginInvoke(new Action(GuardarYPropagar));
            }
        }
        
        /// <summary>
        /// Calcula el factor de indirectos (igual que en FormPresupuesto)
        /// </summary>
        private decimal CalcularFactorPU()
        {
            var proyecto = _context.Proyectos.Find(_proyectoId);
            if (proyecto == null) return 1m;
            
            decimal pInd    = proyecto.PorcentajeIndirectosCentral + proyecto.PorcentajeIndirectosCampo;
            decimal pFin    = proyecto.PorcentajeFinanciamiento;
            decimal pUtil   = proyecto.PorcentajeUtilidad;
            decimal pCargos = proyecto.PorcentajeCargosAdicionales;

            if (proyecto.ModoCalculoPorcentajes == "SobreCD")
                return 1m + (pInd + pFin + pUtil + pCargos) / 100m;

            decimal f = 1m;
            f *= (1m + pInd    / 100m);
            f *= (1m + pFin    / 100m);
            f *= (1m + pUtil   / 100m);
            f *= (1m + pCargos / 100m);
            return f;
        }
        
        /// <summary>
        /// Maneja el evento de cambio de configuración de decimales
        /// </summary>
        private void OnConfiguracionCambiada(object sender, EventArgs e)
        {
            // Recargar la fila actual con los nuevos formatos
            if (_dgvPresupuesto?.CurrentRow != null)
            {
                NotificarFilaCambiada(_dgvPresupuesto.CurrentRow.Index);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Desuscribirse del evento
                FormatoHelper.ConfiguracionCambiada -= OnConfiguracionCambiada;
            }
            base.Dispose(disposing);
        }
    }
}
