using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Forms
{
    public partial class FormExplosionInsumos : Form, IGridFormato, IBusquedaGrid, IRecalculable
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private Proyecto _proyecto;

        // Diccionarios calculados en la última explosión — reutilizados por el Excel
        private Dictionary<int, DatosInsumo> _ultMateriales;
        private Dictionary<int, DatosInsumo> _ultManoObra;
        private Dictionary<int, DatosInsumo> _ultMaquinaria;
        private Dictionary<int, DatosInsumo> _ultHerramientas;
        private decimal _ultCostoDirectoTotal;
        private readonly ExplosionInsumosService _explosionService = new();
        private readonly ExplosionGridRenderService _gridRenderService = new();


        // ── IGridFormato ──────────────────────────────────────────────────────
        public System.Windows.Forms.DataGridView GridPrincipal => dgvExplosion;
        public System.Windows.Forms.DataGridView GridBusqueda => dgvExplosion;

        public bool GenerarReporteExcel()
        {
            btnExportar_Click(this, EventArgs.Empty);
            return true;
        }
        public event EventHandler ColumnaSeleccionadaCambiada;

        // _columnaExpRibbon es la entidad real de BD; _columnaRibbon es el DTO para la interfaz
        private SOPRO.Core.Entities.ColumnaExplosion _columnaExpRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_columnaExpRibbon == null) return;

            _columnaExpRibbon.NombreFuente      = fmt.NombreFuente;
            _columnaExpRibbon.TamanoFuente      = fmt.TamanoFuente;
            _columnaExpRibbon.Negrita           = fmt.Negrita;
            _columnaExpRibbon.Cursiva           = fmt.Cursiva;
            _columnaExpRibbon.Alineacion        = fmt.Alineacion;
            _columnaExpRibbon.ColorFondo        = fmt.ColorFondo;
            _columnaExpRibbon.ColorFuente       = fmt.ColorFuente;
            _columnaExpRibbon.WrapTexto         = fmt.WrapTexto;
            _columnaExpRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _columnaExpRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvExplosion.Columns)
            {
                if (col.Tag == _columnaExpRibbon)
                {
                    AplicarEstiloDesdeColExp((DataGridViewTextBoxColumn)col, _columnaExpRibbon);
                    break;
                }
            }
            dgvExplosion.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvExplosion.Columns)
            {
                if (col.Tag is not SOPRO.Core.Entities.ColumnaExplosion colExp) continue;
                colExp.NombreFuente      = fmt.NombreFuente;
                colExp.TamanoFuente      = fmt.TamanoFuente;
                colExp.Negrita           = fmt.Negrita;
                colExp.Cursiva           = fmt.Cursiva;
                colExp.Alineacion        = fmt.Alineacion;
                colExp.ColorFuente       = fmt.ColorFuente;
                colExp.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColExp((DataGridViewTextBoxColumn)col, colExp);
            }
            _context.SaveChanges();
            dgvExplosion.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _columnaExpRibbon = null;
            _columnaRibbon    = null;
            if (colIndex >= 0 && colIndex < dgvExplosion.Columns.Count)
            {
                var col = dgvExplosion.Columns[colIndex];
                if (col.Tag is SOPRO.Core.Entities.ColumnaExplosion ce)
                {
                    _columnaExpRibbon = ce;
                    // DTO para el ribbon (solo campos de formato)
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre       = ce.Nombre,
                        NombreFuente = ce.NombreFuente,
                        TamanoFuente = ce.TamanoFuente,
                        Negrita      = ce.Negrita,
                        Cursiva      = ce.Cursiva,
                        Alineacion   = ce.Alineacion,
                        ColorFondo   = ce.ColorFondo,
                        ColorFuente  = ce.ColorFuente,
                        WrapTexto    = ce.WrapTexto,
                        AlineacionVertical = ce.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

                public FormExplosionInsumos(SOPROContext context, int proyectoId)
        {
            _context = context;
            _proyectoId = proyectoId;
            InitializeComponent();
            
            // Cargar proyecto
            _proyecto = _context.Proyectos.Find(_proyectoId);
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId, ReportTitleModuleKeys.ExplosionInsumos).Attach();
            if (_proyecto != null)
            {
                this.Text = $"Explosión de Insumos - {_proyecto.Nombre}";
                FormatoHelper.EstablecerProyecto(_proyecto);
            }
            
            // Suscribirse a cambios de configuración
            FormatoHelper.ConfiguracionCambiada += OnConfiguracionCambiada;
            // Ribbon: detectar click en encabezado de columna
            dgvExplosion.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvExplosion.ColumnWidthChanged += DgvExplosion_ColumnWidthChanged;

            ControlRenderHelper.HabilitarDobleBuffer(this);
            ControlRenderHelper.HabilitarDobleBuffer(dgvExplosion);
        }
        
        private void FormExplosionInsumos_Load(object sender, EventArgs e)
        {
            ConfigurarGrid();
            CargarFiltros(); // Ya dispara GenerarExplosion() al hacer SelectedIndex = 0
        }
        
        private void ConfigurarGrid()
        {
            dgvExplosion.SuspendLayout();
            dgvExplosion.AutoGenerateColumns = false;
            dgvExplosion.AllowUserToAddRows = false;
            dgvExplosion.AllowUserToDeleteRows = false;
            dgvExplosion.ReadOnly = true;
            dgvExplosion.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvExplosion.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvExplosion.EnableHeadersVisualStyles = false;
            dgvExplosion.BackgroundColor = Color.White;
            dgvExplosion.GridColor = Color.FromArgb(230, 230, 230);
            dgvExplosion.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 247, 255);
            dgvExplosion.DefaultCellStyle.SelectionForeColor = Color.Black;

            dgvExplosion.AplicarEstiloSOPRO();
            Helpers.DgvCeldaHelper.Aplicar(dgvExplosion);
            _gridRenderService.ConfigureGrid(dgvExplosion);

            // Columnas desde BD
            dgvExplosion.Columns.Clear();

            var columnasBD = Helpers.ColumnasExplosionHelper.ObtenerColumnas(_context, _proyectoId);

            // Mapeo NombreInterno -> Name fijo que usa el código de llenado
            var mapaNames = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Clave",          "colClave"         },
                { "Descripcion",    "colDescripcion"   },
                { "Unidad",         "colUnidad"        },
                { "Cantidad",       "colCantidad"      },
                { "PrecioUnitario", "colPrecioUnitario"},
                { "Importe",        "colImporte"       },
                { "Porcentaje",     "colPorcentaje"    },
            };

            foreach (var colExp in columnasBD)
            {
                if (!mapaNames.TryGetValue(colExp.NombreInterno, out string colName)) continue;

                var dgvCol = new DataGridViewTextBoxColumn
                {
                    Name       = colName,
                    HeaderText = colExp.Nombre,
                    Width      = colExp.AnchoColumna,
                    Visible    = colExp.Visible,
                    Tag        = colExp,
                    SortMode   = DataGridViewColumnSortMode.NotSortable,
                    DefaultCellStyle =
                    {
                        Alignment = AlineacionADGV(colExp.Alineacion),
                        Format    = colExp.FormatoNumerico ?? string.Empty,
                    }
                };
                AplicarEstiloDesdeColExp(dgvCol, colExp);
                dgvExplosion.Columns.Add(dgvCol);
            }

            // Columna dummy al final para llenar espacio
            dgvExplosion.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name         = "colDummy",
                HeaderText   = "",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly     = true,
                SortMode     = DataGridViewColumnSortMode.NotSortable,
            });

            // Estilo de encabezados (base — CellPainting lo sobreescribe por columna)
            dgvExplosion.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvExplosion.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvExplosion.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvExplosion.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvExplosion.ColumnHeadersHeight = 35;

            // Suscribir CellPainting para encabezados con formato personalizado
            dgvExplosion.CellPainting -= DgvExplosion_CellPainting;
            dgvExplosion.CellPainting += DgvExplosion_CellPainting;
            dgvExplosion.ResumeLayout();
        }
        

        private void DgvExplosion_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (e?.Column == null) return;
            if (e.Column.Name == "colDummy") return;

            try
            {
                if (e.Column.Tag is not SOPRO.Core.Entities.ColumnaExplosion cfg) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                cfg.AnchoColumna = nuevoAncho;
                cfg.FechaModificacion = DateTime.Now;

                var columnaDb = _context.ColumnasExplosion.Find(cfg.Id);
                if (columnaDb == null)
                {
                    _context.SaveChanges();
                    return;
                }

                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en explosión: {ex.Message}");
            }
        }


        // ── Helpers de formato ────────────────────────────────────────────────

        private static void AplicarEstiloDesdeColExp(DataGridViewTextBoxColumn dgvCol, SOPRO.Core.Entities.ColumnaExplosion col)
        {
            try
            {
                FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                string fuente = !string.IsNullOrEmpty(col.NombreFuente) ? col.NombreFuente : "Segoe UI";
                float tam = col.TamanoFuente > 0 ? col.TamanoFuente : 9f;
                dgvCol.DefaultCellStyle.Font      = new Font(fuente, tam, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColor(col.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColor(col.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
            }
            catch { }
        }

        private static DataGridViewContentAlignment AlineacionADGV(AlineacionColumna alin) =>
            alin switch
            {
                AlineacionColumna.Centro      => DataGridViewContentAlignment.MiddleCenter,
                AlineacionColumna.Derecha     => DataGridViewContentAlignment.MiddleRight,
                AlineacionColumna.Justificado => DataGridViewContentAlignment.MiddleLeft,
                _                             => DataGridViewContentAlignment.MiddleLeft,
            };

        private static Color TryColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }

        private void DgvExplosion_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return;

            var col = dgvExplosion.Columns[e.ColumnIndex];
            if (col.Tag is not SOPRO.Core.Entities.ColumnaExplosion colDef)
            {
                // colDummy u otros sin Tag: pintar normal
                return;
            }

            _gridRenderService.PaintHeader(e, colDef);
        }

                private void CargarFiltros()
        {
            cmbFiltro.Items.Clear();
            cmbFiltro.Items.Add("Todos");
            cmbFiltro.Items.Add("Materiales");
            cmbFiltro.Items.Add("Mano de Obra");
            cmbFiltro.Items.Add("Maquinaria");
            cmbFiltro.Items.Add("Herramientas");
            cmbFiltro.SelectedIndex = 0;
        }
        
        private void GenerarExplosion()
        {
            var estado = DataGridViewStateHelper.Capture(dgvExplosion);

            SuspendLayout();
            dgvExplosion.SuspendLayout();
            try
            {
                dgvExplosion.Rows.Clear();

                string filtro = cmbFiltro.SelectedItem?.ToString() ?? "Todos";
                var data = _explosionService.Calculate(_context, _proyectoId, filtro);

                if (!data.TieneConceptos)
                {
                    MessageBox.Show("No hay conceptos con matrices en el presupuesto.", "Aviso",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _ultMateriales   = ConvertirDatos(data.Materiales);
                _ultManoObra     = ConvertirDatos(data.ManoObra);
                _ultMaquinaria   = ConvertirDatos(data.Maquinaria);
                _ultHerramientas = ConvertirDatos(data.Herramientas);
                _ultCostoDirectoTotal = data.CostoDirectoTotal;

                RenderRows(data.Rows);

                DataGridViewStateHelper.Restore(dgvExplosion, estado);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar la explosión:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                dgvExplosion.ResumeLayout(true);
                ResumeLayout(true);
            }
        }

        private static Dictionary<int, DatosInsumo> ConvertirDatos(Dictionary<int, ExplosionInsumoAccumulated> source)
        {
            var result = new Dictionary<int, DatosInsumo>();
            foreach (var item in source)
            {
                result[item.Key] = new DatosInsumo
                {
                    Clave = item.Value.Clave,
                    Descripcion = item.Value.Descripcion,
                    Unidad = item.Value.Unidad,
                    Cantidad = item.Value.Cantidad,
                    CantidadFisica = item.Value.CantidadFisica,
                    PrecioUnitario = item.Value.PrecioUnitario,
                    EsPorcentual = item.Value.EsPorcentual
                };
            }
            return result;
        }

        private void RenderRows(List<ExplosionRowDisplay> rows)
        {
            if (rows == null || rows.Count == 0) return;

            var gridRows = _gridRenderService.BuildRows(dgvExplosion, rows);
            if (gridRows.Length > 0)
                dgvExplosion.Rows.AddRange(gridRows);
        }

        // ── TIPOS PARA ACUMULACIÓN ────────────────────────────────────────────
        // DatosInsumo está en Helpers/DatosInsumo.cs (tipo público compartido con el generador Excel)

        /// <summary>
        /// Recorre recursivamente una matriz acumulando IMPORTES (método OPUS PLANET).
        /// Para auxiliares anidados, escala los sub-importes proporcionalmente al importe
        /// guardado en la APU, garantizando que la explosión sea consistente con el presupuesto.
        /// importeBase = importe guardado en ComponentesMatriz para este nivel
        /// </summary>
        private void ExplotarMatriz(
            Matriz matriz,
            decimal importeBase,       // importe guardado en el nivel padre (ya × cantidadConcepto)
            decimal cantidadBase,      // cantidad física del nivel padre
            Dictionary<int, DatosInsumo> materiales,
            Dictionary<int, DatosInsumo> manoObra,
            Dictionary<int, DatosInsumo> maquinaria,
            Dictionary<int, DatosInsumo> herramientas)
        {
            if (matriz?.Componentes == null || matriz.Componentes.Count == 0) return;

            // Suma de importes guardados en esta matriz
            decimal sumaImportesMatriz = matriz.Componentes.Sum(c => c.Importe);
            if (sumaImportesMatriz == 0) return;

            // Factor de escala: ajusta los sub-importes para que sumen exactamente importeBase
            decimal escala = importeBase / sumaImportesMatriz;

            foreach (var comp in matriz.Componentes)
            {
                decimal importe = comp.Importe * escala;
                if (importe == 0) continue;

                // Cantidad física real: cantidadBase × comp.Cantidad (del insumo en el APU)
                decimal cantFisica = cantidadBase * comp.Cantidad;

                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        if (comp.Material != null)
                            AcumularImporte(materiales, comp.MaterialId.Value,
                                comp.Material.Clave, comp.Material.Descripcion,
                                comp.Material.Unidad, importe, cantFisica, comp.Material.PrecioUnitario);
                        break;

                    case TipoComponenteMatriz.ManoDeObra:
                        if (comp.ManoDeObra != null)
                            AcumularImporte(manoObra, comp.ManoDeObraId.Value,
                                comp.ManoDeObra.Clave, comp.ManoDeObra.Descripcion,
                                comp.ManoDeObra.Unidad, importe, cantFisica,
                                comp.ManoDeObra.EsPorcentajeMO ? 0m : comp.ManoDeObra.SalarioReal);
                        break;

                    case TipoComponenteMatriz.Maquinaria:
                        if (comp.Maquinaria != null)
                            AcumularImporte(maquinaria, comp.MaquinariaId.Value,
                                comp.Maquinaria.Clave, comp.Maquinaria.Descripcion,
                                "hr", importe, cantFisica, comp.Maquinaria.CostoHorario);
                        break;

                    case TipoComponenteMatriz.Herramienta:
                        if (comp.Herramienta != null)
                            AcumularImporte(herramientas, comp.HerramientaId.Value,
                                comp.Herramienta.Clave, comp.Herramienta.Descripcion,
                                comp.Herramienta.Unidad, importe, cantFisica,
                                comp.Herramienta.EsPorcentajeMO ? 0m : comp.Herramienta.PrecioUnitario);
                        break;

                    case TipoComponenteMatriz.Auxiliar:
                        // Recursivo: el importeBase y cantidadBase del auxiliar ya están escalados
                        if (comp.Auxiliar != null)
                            ExplotarMatriz(comp.Auxiliar, importe, cantFisica,
                                materiales, manoObra, maquinaria, herramientas);
                        break;
                }
            }
        }

        private static void Acumular(Dictionary<int, DatosInsumo> dic, int key,
            string clave, string desc, string unidad, decimal cant, decimal pu)
        {
            // Mantener compatibilidad - convierte a importe y llama AcumularImporte
            // cantidadFisica = cant (ya es cantidad física en este path)
            AcumularImporte(dic, key, clave, desc, unidad, cant * pu, cant, pu);
        }

        /// <summary>
        /// Acumula por IMPORTE (método OPUS PLANET).
        /// PU=0 indica insumo %MO cuyo PU varía por concepto — la cantidad se mostrará como "—".
        /// </summary>
        private static void AcumularImporte(Dictionary<int, DatosInsumo> dic, int key,
            string clave, string desc, string unidad, decimal importe, decimal cantidadFisica, decimal pu)
        {
            if (dic.TryGetValue(key, out var existing))
            {
                existing.Cantidad       += importe;         // Importe acumulado
                existing.CantidadFisica += cantidadFisica;  // Cantidad física real
                dic[key] = existing;
            }
            else
            {
                dic[key] = new DatosInsumo
                { Clave = clave, Descripcion = desc, Unidad = unidad,
                  Cantidad = importe,
                  CantidadFisica = cantidadFisica,
                  PrecioUnitario = pu };
            }
        }

        /// <summary>
        /// Construye los 4 diccionarios de insumos explotando todos los conceptos.
        /// </summary>
        private void ExplotarConceptos(
            List<ConceptoPresupuesto> conceptos,
            out Dictionary<int, DatosInsumo> materiales,
            out Dictionary<int, DatosInsumo> manoObra,
            out Dictionary<int, DatosInsumo> maquinaria,
            out Dictionary<int, DatosInsumo> herramientas)
        {
            materiales   = new Dictionary<int, DatosInsumo>();
            manoObra     = new Dictionary<int, DatosInsumo>();
            maquinaria   = new Dictionary<int, DatosInsumo>();
            herramientas = new Dictionary<int, DatosInsumo>();

            foreach (var concepto in conceptos)
            {
                if (concepto.Matriz == null) continue;
                // Calcular con el mismo redondeo que usa el presupuesto en pantalla
                decimal importeConcepto = new MotorCalculoSopro(_proyecto).Multiplicar(
                    concepto.Cantidad, concepto.CostoDirectoUnitario);
                ExplotarMatriz(concepto.Matriz, importeConcepto, concepto.Cantidad,
                    materiales, manoObra, maquinaria, herramientas);
            }
        }

        private void GenerarSeccionDesdeDict(
            string titulo,
            Dictionary<int, DatosInsumo> dic,
            decimal costoDirectoTotal)
        {
            if (dic.Count == 0) return;

            AgregarFilaEncabezado(titulo);

            decimal total = 0;
            foreach (var kvp in dic.OrderBy(x => x.Value.Clave))
            {
                var ins = kvp.Value;
                // ins.Cantidad = importe acumulado (método OPUS PLANET)
                decimal importe = ins.Cantidad;
                total += importe;
                decimal pct = costoDirectoTotal > 0 ? importe / costoDirectoTotal : 0;

                // Inferir cantidad: importe / PU  (como hace OPUS PLANET)
                // PU=0 indica %MO — cantidad no aplica
                string cantStr, puStr;
                if (ins.PrecioUnitario == 0m || ins.Unidad?.Trim().ToUpper() == "%MO")
                {
                    cantStr = "—";
                    puStr   = "—";
                }
                else
                {
                    cantStr = ins.CantidadFisica.ToStringCantidad();
                    puStr   = ins.PrecioUnitario.ToStringImporte();
                }

                dgvExplosion.Rows.Add(
                    ins.Clave, ins.Descripcion, ins.Unidad,
                    cantStr, puStr,
                    importe.ToStringImporte(),
                    pct);
            }

            decimal pctTotal = costoDirectoTotal > 0 ? total / costoDirectoTotal : 0;
            AgregarFilaTotal($"TOTAL {titulo}", total, pctTotal);
            AgregarFilaVacia();
        }

        // Mantener métodos originales de sección como wrappers del nuevo sistema
        private void GenerarSeccionMateriales(List<ConceptoPresupuesto> conceptos, decimal costoDirectoTotal,
            Dictionary<int, DatosInsumo> dic) => GenerarSeccionDesdeDict("MATERIALES", dic, costoDirectoTotal);

        private void GenerarSeccionManoDeObra(List<ConceptoPresupuesto> conceptos, decimal costoDirectoTotal,
            Dictionary<int, DatosInsumo> dic) => GenerarSeccionDesdeDict("MANO DE OBRA", dic, costoDirectoTotal);

        private void GenerarSeccionMaquinaria(List<ConceptoPresupuesto> conceptos, decimal costoDirectoTotal,
            Dictionary<int, DatosInsumo> dic) => GenerarSeccionDesdeDict("MAQUINARIA", dic, costoDirectoTotal);

        private void GenerarSeccionHerramientas(List<ConceptoPresupuesto> conceptos, decimal costoDirectoTotal,
            Dictionary<int, DatosInsumo> dic) => GenerarSeccionDesdeDict("HERRAMIENTAS", dic, costoDirectoTotal);

                private void AgregarFilaEncabezado(string texto)
        {
            int rowIndex = dgvExplosion.Rows.Add("", texto, "", "", "", "", "");
            var row = dgvExplosion.Rows[rowIndex];
            
            // Estilo de encabezado
            row.DefaultCellStyle.BackColor = Color.FromArgb(70, 130, 180);
            row.DefaultCellStyle.ForeColor = Color.White;
            row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            row.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }
        
        private void AgregarFilaTotal(string texto, decimal importe, decimal porcentaje)
        {
            int rowIndex = dgvExplosion.Rows.Add("", texto, "", "", "", importe.ToStringImporte(), porcentaje);
            var row = dgvExplosion.Rows[rowIndex];
            
            // Estilo de total
            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            row.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            row.Cells["colImporte"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            row.Cells["colPorcentaje"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        
        private void AgregarFilaTotalGeneral(decimal costoDirectoTotal)
        {
            int rowIndex = dgvExplosion.Rows.Add("", "TOTAL DEL REPORTE", "", "", "", costoDirectoTotal.ToStringImporte(), 1.0m);
            var row = dgvExplosion.Rows[rowIndex];
            
            // Estilo especial para total general
            row.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            row.DefaultCellStyle.ForeColor = Color.White;
            row.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            row.Cells["colImporte"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            row.Cells["colPorcentaje"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        
        private void AgregarFilaVacia()
        {
            int rowIndex = dgvExplosion.Rows.Add("", "", "", "", "", "", "");
            var row = dgvExplosion.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = Color.White;
            row.Height = 10;
        }

        private void AgregarFilaReferencia(string etiqueta, decimal valor, decimal porcentaje)
        {
            int rowIndex = dgvExplosion.Rows.Add("", etiqueta, "", "", "",
                valor.ToStringImporte(), porcentaje);
            var row = dgvExplosion.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
            row.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
        }
        
        private void cmbFiltro_SelectedIndexChanged(object sender, EventArgs e)
        {
            GenerarExplosion();
        }
        


        public void GenerarPdfExplosion()
        {
            try
            {
                if (_ultMateriales == null)
                {
                    MessageBox.Show("No hay datos para exportar. Genere la explosión primero.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyectoId);
                var columnas = Helpers.ColumnasExplosionHelper.ObtenerColumnas(_context, _proyectoId);
                string filtro = cmbFiltro.SelectedItem?.ToString() ?? "Todos";

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF de Explosión de Insumos",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"ExplosionInsumos_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId, ReportTitleModuleKeys.ExplosionInsumos, lblTitulo.Text);
                var generador = new Services.GeneradorPdfExplosion(svc);
                string ruta = generador.Generar(
                    _proyecto, plantilla, columnas, filtro,
                    _ultMateriales, _ultManoObra, _ultMaquinaria, _ultHerramientas,
                    _ultCostoDirectoTotal, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte PDF:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                if (_ultMateriales == null)
                {
                    MessageBox.Show("No hay datos para exportar. Genere la explosión primero.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var svc      = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyectoId);
                var columnas  = Helpers.ColumnasExplosionHelper.ObtenerColumnas(_context, _proyectoId);
                string filtro = cmbFiltro.SelectedItem?.ToString() ?? "Todos";

                using var dlg = new SaveFileDialog
                {
                    Title      = "Guardar reporte Explosión de Insumos",
                    Filter     = "Excel (*.xlsx)|*.xlsx",
                    FileName   = $"ExplosionInsumos_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId, ReportTitleModuleKeys.ExplosionInsumos, lblTitulo.Text);
                var generador = new Services.GeneradorExcelExplosion(svc);
                string ruta = generador.Generar(
                    _proyecto, plantilla, columnas, filtro,
                    _ultMateriales, _ultManoObra, _ultMaquinaria, _ultHerramientas,
                    _ultCostoDirectoTotal, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show($"Reporte generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void btnCerrar_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // ── IRecalculable ────────────────────────────────────────────────────
        public void RecalcularTodo() => GenerarExplosion();
        
        /// <summary>
        /// Maneja el evento de cambio de configuración de decimales
        /// </summary>
        private void OnConfiguracionCambiada(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // Recargar proyecto con nuevos decimales y regenerar aunque el tab no esté activo
            _proyecto = _context.Proyectos.Find(_proyectoId);
            if (IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) GenerarExplosion(); }));
            else
                GenerarExplosion();
        }
    }
}
