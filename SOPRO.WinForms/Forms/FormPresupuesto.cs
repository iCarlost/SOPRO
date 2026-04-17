using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    public partial class FormPresupuesto : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private Proyecto _proyecto;
        private bool _cargando = false;
        private bool _asignandoMatriz = false; // Suprime CellValueChanged durante asignación programática
        private Controls.PanelMatricesEmbebido _panelMatricesEmbebido;
        private SelectorApuEmbebidoControl? _selectorApuEmbebido;
        private int _selectorApuEmbebidoRowIndex = -1;
        private EventHandler _onInsumosModificados; // Guardado para poder desuscribir al cerrar
        private bool _validacionAperturaMostrada = false;

        // ── IGridFormato ──────────────────────────────────────────────────────
        public System.Windows.Forms.DataGridView GridPrincipal => dgvPresupuesto;
        public System.Windows.Forms.DataGridView GridBusqueda => dgvPresupuesto;

        public bool GenerarReporteExcel()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return false;

            // Abre el mismo mini-form que el botón individual
            // (permite elegir entre Presupuesto o P.U.)
            using var opciones = new FormExportarReporte(_context, _proyecto);
            opciones.ShowDialog();
            return true;
        }
        public event EventHandler ColumnaSeleccionadaCambiada;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_columnaRibbon == null) return;
            _columnaRibbon.NombreFuente = fmt.NombreFuente;
            _columnaRibbon.TamanoFuente = fmt.TamanoFuente;
            _columnaRibbon.Negrita = fmt.Negrita;
            _columnaRibbon.Cursiva = fmt.Cursiva;
            _columnaRibbon.Alineacion = fmt.Alineacion;
            _columnaRibbon.ColorFondo = fmt.ColorFondo;
            _columnaRibbon.ColorFuente = fmt.ColorFuente;
            _columnaRibbon.WrapTexto = fmt.WrapTexto;
            _columnaRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _columnaRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is not ColumnaPersonalizada cfg || !ReferenceEquals(cfg, _columnaRibbon)) continue;

                col.DefaultCellStyle.WrapMode = cfg.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                col.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(cfg.Alineacion, cfg.AlineacionVertical);

                var style = new FontStyle();
                if (cfg.Negrita) style |= FontStyle.Bold;
                if (cfg.Cursiva) style |= FontStyle.Italic;
                col.DefaultCellStyle.Font = new Font(cfg.NombreFuente ?? dgvPresupuesto.Font.Name,
                    cfg.TamanoFuente > 0 ? cfg.TamanoFuente : dgvPresupuesto.Font.Size,
                    style == 0 ? FontStyle.Regular : style);

                if (!string.IsNullOrWhiteSpace(cfg.ColorFuente))
                {
                    try { col.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml(cfg.ColorFuente); } catch { }
                }

                break;
            }

            FormatoHelper.AjustarAutoAlturaFilas(dgvPresupuesto);
            dgvPresupuesto.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (var col in _context.ColumnasPersonalizadas
                .Where(c => c.ProyectoId == _proyecto.Id))
            {
                col.NombreFuente = fmt.NombreFuente;
                col.TamanoFuente = fmt.TamanoFuente;
                col.Negrita = fmt.Negrita;
                col.Cursiva = fmt.Cursiva;
                col.Alineacion = fmt.Alineacion;
                col.ColorFuente = fmt.ColorFuente;
                col.WrapTexto = fmt.WrapTexto;
                col.AlineacionVertical = fmt.AlineacionVertical;
                col.FechaModificacion = DateTime.Now;
                // ColorFondo NO se aplica globalmente
            }
            _context.SaveChanges();

            FormatoHelper.AplicarWrapYAlineacionPersistidos(dgvPresupuesto);
            FormatoHelper.AjustarAutoAlturaFilas(dgvPresupuesto);
            dgvPresupuesto.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvPresupuesto.Columns.Count)
                if (dgvPresupuesto.Columns[colIndex].Tag is ColumnaPersonalizada cp)
                    _columnaRibbon = cp;
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        private List<PresupuestoValidationService.ValidationIssue> ObtenerInconsistenciasPresupuesto()
        {
            return PresupuestoValidationService.Validate(_context, _proyecto.Id);
        }

        private bool ValidarPresupuestoAntesDeContinuar(bool bloquear, string titulo)
        {
            var issues = ObtenerInconsistenciasPresupuesto();
            if (issues.Count == 0)
                return true;

            MostrarDialogoInconsistencias(issues, titulo, bloquear);
            return !bloquear;
        }

        private void MostrarDialogoInconsistencias(List<PresupuestoValidationService.ValidationIssue> issues, string titulo, bool bloquear)
        {
            using var dialog = new Form
            {
                Text = titulo,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                Width = 760,
                Height = 300
            };

            var lblInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 42,
                Text = bloquear
                    ? "Se detectaron inconsistencias en el presupuesto. Corrija antes de continuar."
                    : "Se detectaron inconsistencias en el presupuesto.",
                Padding = new Padding(12, 12, 12, 0)
            };

            var listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 9F)
            };

            foreach (var issue in issues)
                listBox.Items.Add(issue);

            listBox.Format += (s, e) =>
            {
                if (e.ListItem is PresupuestoValidationService.ValidationIssue issue)
                {
                    var line = issue.ToSingleLine();
                    e.Value = line.Length > 120 ? line.Substring(0, 117) + "..." : line;
                }
            };

            listBox.DoubleClick += (s, e) =>
            {
                if (listBox.SelectedItem is not PresupuestoValidationService.ValidationIssue issue)
                    return;

                IrAConceptoConInconsistencia(issue);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };

            var lblHint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Padding = new Padding(12, 0, 12, 8),
                Text = "Doble clic en una línea para ir al concepto.",
                ForeColor = SystemColors.GrayText
            };

            var panelBotones = new Panel { Dock = DockStyle.Bottom, Height = 46 };
            var btnCerrar = new Button
            {
                Text = bloquear ? "Corregir" : "Aceptar",
                DialogResult = DialogResult.OK,
                Width = 100,
                Height = 30,
                Left = 640,
                Top = 8,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            panelBotones.Controls.Add(btnCerrar);

            dialog.Controls.Add(listBox);
            dialog.Controls.Add(lblHint);
            dialog.Controls.Add(panelBotones);
            dialog.Controls.Add(lblInfo);
            dialog.AcceptButton = btnCerrar;

            dialog.ShowDialog(this);
        }

        private void IrAConceptoConInconsistencia(PresupuestoValidationService.ValidationIssue issue)
        {
            if (issue?.ConceptoId == null)
                return;

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var row = dgvPresupuesto.Rows[i];
                if (row.Tag is not ConceptoPresupuesto concepto || concepto.Id != issue.ConceptoId.Value)
                    continue;

                dgvPresupuesto.ClearSelection();
                row.Selected = true;
                var targetCell = ObtenerCeldaPorNombreInterno(i, "Cantidad")
                    ?? ObtenerCeldaPorNombreInterno(i, "PrecioUnitario")
                    ?? row.Cells.Cast<DataGridViewCell>().FirstOrDefault();
                if (targetCell != null)
                    dgvPresupuesto.CurrentCell = targetCell;
                dgvPresupuesto.FirstDisplayedScrollingRowIndex = i;
                dgvPresupuesto.Focus();
                break;
            }
        }

        private void MostrarAdvertenciaValidacionEnApertura()
        {
            if (_validacionAperturaMostrada)
                return;

            _validacionAperturaMostrada = true;
            ValidarPresupuestoAntesDeContinuar(bloquear: false, titulo: "Advertencia de presupuesto");
        }

        private void InicializarBotonReajustarCosto()
        {
            _btnReajustarCosto = new ToolStripButton
            {
                BackColor = SystemColors.Control,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Black,
                Name = "btnReajustarCosto",
                Size = new Size(111, 19),
                Text = "🎯 Reajustar costo"
            };
            _btnReajustarCosto.Click += btnReajustarCosto_Click;

            var indexImportar = panelToolbar.Items.IndexOf(btnImportarExcel);
            if (indexImportar >= 0)
                panelToolbar.Items.Insert(indexImportar + 1, _btnReajustarCosto);
            else
                panelToolbar.Items.Add(_btnReajustarCosto);
        }

        private void btnReajustarCosto_Click(object? sender, EventArgs e)
        {
            try
            {
                if (dgvPresupuesto.CurrentRow == null)
                {
                    MessageBox.Show("Seleccione un concepto del presupuesto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var rowIndex = dgvPresupuesto.CurrentRow.Index;
                var concepto = dgvPresupuesto.CurrentRow.Tag as ConceptoPresupuesto;
                var tipo = ObtenerCeldaTexto(rowIndex, "Tipo", "Concepto");
                var currentInternalName = (dgvPresupuesto.CurrentCell?.OwningColumn?.Tag as ColumnaPersonalizada)?.NombreInterno ?? string.Empty;
                bool ajustarPorImporte = string.Equals(currentInternalName, "Importe", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(currentInternalName, "ImporteTotal", StringComparison.OrdinalIgnoreCase);

                if (concepto == null)
                {
                    MessageBox.Show("Seleccione un concepto o agrupador del presupuesto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (concepto.EsAgrupador || !string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ajustarPorImporte)
                    {
                        MessageBox.Show("Para agrupadores, el reajuste solo está disponible seleccionando la celda Importe.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    AjustarAgrupadorSeleccionado(rowIndex, concepto);
                    return;
                }

                if (!concepto.MatrizId.HasValue)
                {
                    MessageBox.Show("Por ahora el reajuste solo está disponible para conceptos con matriz asignada.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var matriz = _context.Matrices
                    .Include(m => m.Componentes).ThenInclude(c => c.Material)
                    .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                    .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                    .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                    .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .FirstOrDefault(m => m.Id == concepto.MatrizId.Value);

                if (matriz == null)
                {
                    MessageBox.Show("No se pudo cargar la matriz asignada al concepto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var motorAjuste = new MotorCalculoSopro(_proyecto);
                decimal cantidadConceptoOriginal = concepto.Cantidad;
                decimal factorPu = CalcularFactorPU();
                decimal actualMostrado = ajustarPorImporte
                    ? motorAjuste.Multiplicar(cantidadConceptoOriginal, CalcularPU(matriz.CostoDirecto))
                    : CalcularPU(matriz.CostoDirecto);
                string contexto = ajustarPorImporte
                    ? $"Concepto: {concepto.Descripcion} \n"+
"Se reajustará la matriz por rendimiento/cantidad para acercar el IMPORTE del concepto al monto objetivo. La cantidad del concepto en presupuesto no se modificará."
                    : $"Concepto: {concepto.Descripcion} \n"+
"Se reajustará la matriz por rendimiento/cantidad para acercar el P.U. del concepto al monto objetivo. La cantidad del concepto en presupuesto no se modificará.";

                using var frm = new FormReajustarCosto("Reajustar costo", actualMostrado, contexto);
                if (frm.ShowDialog(this) != DialogResult.OK)
                    return;

                if (factorPu <= 0m)
                {
                    MessageBox.Show("No se pudo calcular el factor del precio unitario del proyecto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                decimal targetCd = ajustarPorImporte
                    ? (cantidadConceptoOriginal > 0m ? frm.TargetAmount / cantidadConceptoOriginal / factorPu : 0m)
                    : (frm.TargetAmount / factorPu);

                var result = MatrixCostAdjustmentService.AdjustMatrixByTargetCost(matriz, _proyecto, frm.SelectedScopes, targetCd);
                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _context.SaveChanges();
                RefrescarPreciosDesdeDB();
                RecalcularTodosLosTotales();
                GuardarCambios();
                dgvPresupuesto.Refresh();

                decimal actualCoherente = ajustarPorImporte
                    ? motorAjuste.Multiplicar(cantidadConceptoOriginal, CalcularPU(result.CurrentCost))
                    : CalcularPU(result.CurrentCost);
                decimal logradoMostrado = ajustarPorImporte
                    ? motorAjuste.Multiplicar(cantidadConceptoOriginal, CalcularPU(result.AchievedCost))
                    : CalcularPU(result.AchievedCost);

                MessageBox.Show(
                    $"Monto actual: {actualCoherente:C2} \n" +
                    $"Objetivo: {frm.TargetAmount:C2} \n" +
                    $"Resultado: {logradoMostrado:C2} \n" +
                    $"Factor aplicado sobre rendimientos/cantidades de la matriz: {result.FactorApplied:N4}",
                    "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al reajustar costo:{ex.Message}", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AjustarAgrupadorSeleccionado(int rowIndex, ConceptoPresupuesto agrupador)
        {
            decimal currentImporte = ObtenerCeldaDecimal(rowIndex, "Importe");
            string contexto = $"Agrupador: {agrupador.Descripcion} \n"+
"Se reajustarán recursivamente los conceptos descendientes por rendimiento/cantidad dentro de sus matrices. La cantidad de los conceptos en presupuesto no se modificará.";
            using var frm = new FormReajustarCosto("Reajustar costo de agrupador", currentImporte, contexto);
            if (frm.ShowDialog(this) != DialogResult.OK)
                return;

            decimal factorPu = CalcularFactorPU();
            if (factorPu <= 0m)
            {
                MessageBox.Show("No se pudo calcular el factor del precio unitario del proyecto.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var descendientes = ObtenerConceptosDescendientesHoja(rowIndex, agrupador);
            if (descendientes.Count == 0)
            {
                MessageBox.Show("El agrupador seleccionado no contiene conceptos hoja con matriz asignada.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var matrixIds = descendientes.Where(c => c.MatrizId.HasValue).Select(c => c.MatrizId!.Value).Distinct().ToList();
            var matrices = _context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => matrixIds.Contains(m.Id))
                .ToDictionary(m => m.Id);

            var candidatos = new List<(ConceptoPresupuesto Concepto, MatrixAdjustmentBasis Basis)>();
            int omitidos = 0;
            foreach (var conceptoHijo in descendientes)
            {
                if (!conceptoHijo.MatrizId.HasValue || !matrices.TryGetValue(conceptoHijo.MatrizId.Value, out var matriz))
                {
                    omitidos++;
                    continue;
                }

                var basis = MatrixCostAdjustmentService.GetAdjustmentBasis(matriz, _proyecto, frm.SelectedScopes);
                if (basis == null || !basis.HasAdjustableScope)
                {
                    omitidos++;
                    continue;
                }

                candidatos.Add((conceptoHijo, basis));
            }

            if (candidatos.Count == 0)
            {
                MessageBox.Show("No se encontraron conceptos ajustables con los rubros seleccionados dentro del agrupador.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal targetCdTotal = frm.TargetAmount / factorPu;
            decimal fixedTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.FixedCost);
            decimal adjustableTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.AdjustableCost);
            decimal minTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.MinCost);
            decimal currentTotal = candidatos.Sum(x => x.Concepto.Cantidad * x.Basis.CurrentCost);

            if (adjustableTotal <= 0m)
            {
                MessageBox.Show("Los rubros seleccionados no tienen importe ajustable dentro del agrupador.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (targetCdTotal < minTotal - 0.01m)
            {
                MessageBox.Show($"No es posible bajar el agrupador al monto solicitado con los rubros seleccionados. El mínimo alcanzable es {(minTotal * factorPu):C2}.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal factor = (targetCdTotal - fixedTotal) / adjustableTotal;
            if (factor < 0m)
            {
                MessageBox.Show("Con los rubros seleccionados no es posible alcanzar el monto solicitado.", "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var matricesAjustables = candidatos
                .Select(x => x.Concepto.MatrizId!.Value)
                .Distinct()
                .Select(id => matrices[id])
                .ToList();

            foreach (var matriz in matricesAjustables)
            {
                var result = MatrixCostAdjustmentService.AdjustMatrixByFactor(matriz, _proyecto, frm.SelectedScopes, factor);
                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _context.SaveChanges();
            RefrescarPreciosDesdeDB();
            RecalcularTodosLosTotales();
            GuardarCambios();
            dgvPresupuesto.Refresh();

            decimal logrado = ObtenerCeldaDecimal(rowIndex, "Importe");
            var conceptosIds = descendientes.Select(x => x.Id).ToHashSet();
            int compartidasFuera = _context.ConceptosPresupuesto.Count(c => c.ProyectoId == _proyecto.Id && !c.EsAgrupador && c.MatrizId.HasValue && matrixIds.Contains(c.MatrizId.Value) && !conceptosIds.Contains(c.Id));
            string notaCompartidas = compartidasFuera > 0
                ? $"Nota: {compartidasFuera} concepto(s) fuera del agrupador comparten alguna matriz reajustada."
                : string.Empty;

            MessageBox.Show(
                $"Monto actual: {currentImporte:C2}\n" +
                $"Objetivo: {frm.TargetAmount:C2}\n" +
                $"Resultado: {logrado:C2}\n" +
                $"Conceptos hoja encontrados: {descendientes.Count}\n" +
                $"Conceptos ajustados: {candidatos.Count}\n" +
                $"Conceptos omitidos: {omitidos}\n" +
                $"Factor global aplicado sobre rendimientos/cantidades: {factor:N4}" +
                notaCompartidas,
                "Reajustar costo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private List<ConceptoPresupuesto> ObtenerConceptosDescendientesHoja(int rowIndex, ConceptoPresupuesto agrupador)
        {
            var result = new List<ConceptoPresupuesto>();
            int nivelPadre = agrupador.Nivel;
            for (int i = rowIndex + 1; i < dgvPresupuesto.Rows.Count; i++)
            {
                var row = dgvPresupuesto.Rows[i];
                var concepto = row.Tag as ConceptoPresupuesto;
                string tipo = ObtenerCeldaTexto(i, "Tipo");
                string descripcion = ObtenerCeldaTexto(i, "Descripcion");
                string clave = ObtenerCeldaTexto(i, "Clave");
                bool hasContent = concepto != null || !string.IsNullOrWhiteSpace(tipo) || !string.IsNullOrWhiteSpace(descripcion) || !string.IsNullOrWhiteSpace(clave);
                if (!hasContent)
                    continue;

                int nivel = concepto?.Nivel ?? ObtenerNivelDesdeTipoPresupuesto(tipo);
                if (nivel <= nivelPadre)
                    break;

                if (concepto != null && !concepto.EsAgrupador && concepto.MatrizId.HasValue)
                    result.Add(concepto);
            }

            return result;
        }

        private int ObtenerNivelDesdeTipoPresupuesto(string? tipo)
        {
            return tipo switch
            {
                "Capitulo" => 0,
                "Subcapitulo" => 1,
                "Nivel 1" => 2,
                "Nivel 2" => 3,
                "Nivel 3" => 4,
                _ => 5
            };
        }

        public FormPresupuesto(SOPROContext context, Proyecto proyecto)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            var workspaceService = new ProjectWorkspaceService();
            _projectIndexService = new ProjectIndexService(workspaceService);
            _projectUsageService = new ProjectUsageService(workspaceService);
            _catalogSearchService = new CatalogSearchService(_projectIndexService, _projectUsageService);
            _projectIndexService.RefreshKnownProjects(context.DatabasePath, force: true);
            // Recargar proyecto fresco desde BD para tener los % actualizados
            _proyecto = context.Proyectos.Find(proyecto.Id) ?? proyecto;

            // Establecer proyecto para formateo global
            FormatoHelper.EstablecerProyecto(_proyecto);

            // Suscribirse a cambios de configuración
            FormatoHelper.ConfiguracionCambiada += OnConfiguracionCambiada;

            InitializeComponent();

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyecto.Id, ReportTitleModuleKeys.Presupuesto).Attach();

            // Aplicar estilo consistente al grid
            dgvPresupuesto.AplicarEstiloSOPRO();

            // Selección de FILA completa con color suave + borde azul en celda activa + Ctrl+C por celda
            dgvPresupuesto.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvPresupuesto);

            // Agregar columna ID oculta
            dgvPresupuesto.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                HeaderText = "ID",
                Visible = false,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // Columna de número de concepto (solo para conceptos, no agrupadores)
            dgvPresupuesto.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNumero",
                HeaderText = "#",
                Width = 40,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 8F)
                }
            });

            // NO crear columnas automáticamente - el usuario decide cuáles quiere
            // Si no hay columnas, CargarColumnasPersonalizadas mostrará columnas base temporales

            ActualizarTitulo();
            ConfigurarEventos();
            InicializarFormMatricesEmbebido();
            CargarPresupuesto();
            RefrescarFuenteAutocompleteApu();
            dgvPresupuesto.InterceptarTeclaEspecial = ProcesarTeclaEspecialPresupuesto;
            this.Shown += FormPresupuesto_Shown;

            // Suscribirse a eventos de actualización de porcentajes
            FormPorcentajes.PorcentajesActualizados += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
            FormIndirectos.IndirectosTransferidos += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
            FormFinanciamiento.FinanciamientoTransferido += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
            FormUtilidad.UtilidadTransferida += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
            FormDatosProyecto.DecimalesActualizados += (s, e) =>
            {
                // Recargar proyecto y refrescar formateo
                var proyFresco = _context.Proyectos.Find(_proyecto.Id);
                if (proyFresco != null)
                {
                    _proyecto.DecimalesCantidad = proyFresco.DecimalesCantidad;
                    _proyecto.DecimalesImporte = proyFresco.DecimalesImporte;
                    _proyecto.DecimalesPorcentaje = proyFresco.DecimalesPorcentaje;
                    FormatoHelper.EstablecerProyecto(_proyecto);
                    RecalcularPreciosUnitarios(); // Refrescar con nuevos decimales
                }
            };

            // Refrescar cuando se elimina/modifica un insumo desde cualquier catálogo
            _onInsumosModificados = (s, e) =>
            {
                RefrescarPreciosDesdeDB();
                RefrescarFuenteAutocompleteApu();
            };
            FormCatalogoMateriales.InsumosModificados += _onInsumosModificados;
            FormCatalogoManoObra.InsumosModificados += _onInsumosModificados;
            FormCatalogoHerramientas.InsumosModificados += _onInsumosModificados;
            FormCatalogoMaquinaria.InsumosModificados += _onInsumosModificados;
            FormEditarMatriz.MatrizGuardada += _onInsumosModificados;

            // También refrescar cuando el form gana foco (por si vienen de otra ventana)
            this.Activated += (s, e) =>
            {
                ActualizarInfoPorcentajes();
                RecalcularPreciosUnitarios();
            };
        }


        private void FormPresupuesto_Shown(object? sender, EventArgs e)
        {
            this.Shown -= FormPresupuesto_Shown;
            MostrarAdvertenciaValidacionEnApertura();
        }

        private void InicializarFormMatricesEmbebido()
        {
            _panelMatricesEmbebido = new Controls.PanelMatricesEmbebido(_context, _proyecto.Id);
            panelMatricesHost.Controls.Add(_panelMatricesEmbebido);

            // Conectar el panel al grid para que reaccione a la selección
            _panelMatricesEmbebido.ConectarPresupuesto(dgvPresupuesto);

            // Cuando se edita la matriz desde el panel, recalcular la jerarquía
            _panelMatricesEmbebido.MatrizActualizada += (sender, filaActualizada) =>
            {
                int indicePadre = ObtenerIndicePadre(filaActualizada);
                while (indicePadre >= 0)
                {
                    ActualizarTotalAgrupador(indicePadre);
                    indicePadre = ObtenerIndicePadre(indicePadre);
                }
                ReasignarNumerosConceptos();
                dgvPresupuesto.Refresh();
            };
            _panelMatricesEmbebido.SolicitudCerrarWorkspace += (sender, e) =>
            {
                if (!splitContainer.Panel2Collapsed)
                    btnWorkspaceCerrar_Click(btnWorkspaceCerrar, EventArgs.Empty);
            };

            ActualizarEstadoWorkspace(false);
        }

        /// <summary>
        /// Recarga los precios de TODOS los conceptos del presupuesto desde BD.
        /// Llamar después de cerrar el APU Editor (FormMatrices → FormEditarMatriz).
        /// </summary>
        public void RefrescarPreciosDesdeDB()
        {
            int filaActual = -1;
            int colActual = -1;
            try
            {
                if (dgvPresupuesto.CurrentCell != null)
                {
                    filaActual = dgvPresupuesto.CurrentCell.RowIndex;
                    colActual = dgvPresupuesto.CurrentCell.ColumnIndex;
                }

                for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
                {
                    var concepto = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                    if (concepto == null || concepto.EsAgrupador || !concepto.MatrizId.HasValue) continue;

                    // Recargar la matriz fresca desde BD
                    var matriz = _context.Matrices
                        .AsNoTracking()
                        .FirstOrDefault(m => m.Id == concepto.MatrizId.Value);
                    if (matriz == null) continue;

                    var _motorSync = new MotorCalculoSopro(_proyecto);
                    decimal cdUnit = _motorSync.RedondearImporte(matriz.CostoDirecto);
                    decimal nuevoPU = CalcularPU(cdUnit);
                    decimal nuevoImp = _motorSync.Multiplicar(concepto.Cantidad, nuevoPU);
                    concepto.CostoDirectoUnitario = cdUnit;
                    concepto.CostoDirectoTotal = _motorSync.Multiplicar(concepto.Cantidad, cdUnit);
                    concepto.PrecioUnitario = nuevoPU;   // persistir
                    concepto.ImporteTotal = nuevoImp;  // persistir

                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            if (colDef.NombreInterno == "PrecioUnitario")
                                dgvPresupuesto.Rows[i].Cells[col.Index].Value = nuevoPU.ToStringImporte();
                            else if (colDef.NombreInterno == "Importe" || colDef.NombreInterno == "ImporteTotal")
                                dgvPresupuesto.Rows[i].Cells[col.Index].Value = nuevoImp.ToStringImporte();
                        }
                    }
                }
                _context.SaveChanges(); // Guardar PU e ImporteTotal calculados
                RecalcularTodosLosTotales();
                GuardarCambios();       // Persistir totales de agrupadores en BD
                dgvPresupuesto.Refresh();

                if (filaActual >= 0 && filaActual < dgvPresupuesto.Rows.Count)
                {
                    try
                    {
                        int colRestaurar = (colActual >= 0 && colActual < dgvPresupuesto.Columns.Count) ? colActual : 0;
                        dgvPresupuesto.CurrentCell = dgvPresupuesto.Rows[filaActual].Cells[colRestaurar];
                        dgvPresupuesto.ClearSelection();
                        dgvPresupuesto.Rows[filaActual].Selected = true;
                    }
                    catch { }
                }

                // Refrescar también el panel embebido con la fila actual restaurada
                if (filaActual >= 0 && filaActual < dgvPresupuesto.Rows.Count)
                    _panelMatricesEmbebido?.NotificarFilaCambiada(filaActual);
                else if (dgvPresupuesto.CurrentRow != null)
                    _panelMatricesEmbebido?.NotificarFilaCambiada(dgvPresupuesto.CurrentRow.Index);

                // Actualizar el header con los % del proyecto
                ActualizarInfoPorcentajes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefrescarPrecios: {ex.Message}");
            }
        }

        /// <summary>
        /// Maneja el evento de cambio de configuración de decimales
        /// </summary>
        private void OnConfiguracionCambiada(object sender, EventArgs e)
        {
            if (!this.Visible) return;
            // Recargar proyecto desde BD para tener los decimales actualizados
            _proyecto = _context.Proyectos.Find(_proyecto.Id);

            // Actualizar formato de la columna Cantidad con los nuevos decimales
            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef &&
                    string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
                {
                    col.DefaultCellStyle.Format = $"N{_proyecto.DecimalesCantidad}";
                }
            }

            var motorCfg = new MotorCalculoSopro(_proyecto);

            // Reformatear TODAS las celdas del grid con los nuevos decimales
            foreach (DataGridViewRow row in dgvPresupuesto.Rows)
            {
                var concepto = row.Tag as ConceptoPresupuesto;
                if (concepto == null) continue;

                // Recalcular valores con los nuevos decimales
                if (!concepto.EsAgrupador && concepto.MatrizId.HasValue)
                {
                    decimal pu = CalcularPU(concepto.CostoDirectoUnitario);
                    decimal importe = motorCfg.Multiplicar(concepto.Cantidad, pu);

                    // Actualizar cada celda con el nuevo formato
                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            switch (colDef.NombreInterno)
                            {
                                case "Cantidad":
                                    // Re-formatear con los nuevos decimales de cantidad
                                    row.Cells[col.Index].Value = motorCfg.RedondearCantidad(concepto.Cantidad);
                                    break;
                                case "PrecioUnitario":
                                    row.Cells[col.Index].Value = pu.ToStringImporte();
                                    break;
                                case "Importe":
                                    row.Cells[col.Index].Value = importe.ToStringImporte();
                                    break;
                            }
                        }
                    }
                }
            }

            // Recalcular totales con nuevos decimales
            RecalcularTodosLosTotales();
            GuardarCambios();       // Persistir totales de agrupadores en BD
            dgvPresupuesto.Refresh();
        }

        private void ActualizarTitulo()
        {
            var info = BudgetPricingService.BuildHeaderInfo(_proyecto);
            lblProyecto.Text = info.ProyectoNombre;
            lblUbicacion.Text = info.UbicacionTexto;
            ActualizarInfoPorcentajes();
        }

        /// <summary>
        /// Muestra en el header los porcentajes activos del proyecto.
        /// </summary>
        private void ActualizarInfoPorcentajes()
        {
            BudgetPricingService.RefreshProjectPercentages(_context, _proyecto);
            var info = BudgetPricingService.BuildHeaderInfo(_proyecto);
            lblPorcentajesInfo.Text = info.PorcentajesTexto;
        }

        /// <summary>
        /// Calcula el factor multiplicador total para convertir CD → P.U.
        /// </summary>
        private decimal CalcularFactorPU()
        {
            return BudgetPricingService.CalculateFactor(_proyecto);
        }

        /// <summary>
        /// Convierte un Costo Directo en Precio Unitario aplicando la cadena de porcentajes.
        /// </summary>
        private decimal CalcularPU(decimal costoDirecto)
        {
            return BudgetPricingService.CalculateUnitPrice(_proyecto, costoDirecto);
        }

        /// <summary>
        /// Recalcula las columnas P.U. e Importe del grid usando los porcentajes actuales del proyecto.
        /// Llamar después de cambiar porcentajes en FormPorcentajes/FormIndirectos.
        /// </summary>
        private void RecalcularPreciosUnitarios()
        {
            try
            {
                var _motorRecalc = new MotorCalculoSopro(_proyecto);
                bool huboCambios = false;

                for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
                {
                    var concepto = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                    if (concepto == null || concepto.EsAgrupador) continue;

                    decimal pu = CalcularPU(concepto.CostoDirectoUnitario);
                    decimal imp = _motorRecalc.Multiplicar(concepto.Cantidad, pu);

                    // Actualizar grid
                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            if (colDef.NombreInterno == "PrecioUnitario")
                                dgvPresupuesto.Rows[i].Cells[col.Index].Value = pu.ToStringImporte();
                            else if (colDef.NombreInterno == "Importe" || colDef.NombreInterno == "ImporteTotal")
                                dgvPresupuesto.Rows[i].Cells[col.Index].Value = imp.ToStringImporte();
                        }
                    }

                    // Persistir en entidad y BD si cambió
                    if (concepto.PrecioUnitario != pu || concepto.ImporteTotal != imp)
                    {
                        concepto.PrecioUnitario = pu;
                        concepto.ImporteTotal = imp;
                        huboCambios = true;
                    }
                }

                if (huboCambios)
                    _context.SaveChanges();

                RecalcularTodosLosTotales(); // actualiza entidades agrupadoras en memoria
                GuardarCambios();            // persiste los totales de agrupadores en BD
                dgvPresupuesto.Refresh();
            }
            catch { /* silenciar errores en caso de que el grid no esté listo */ }
        }

        private void ConfigurarEventos()
        {
            dgvPresupuesto.CellValueChanged += DgvPresupuesto_CellValueChanged;
            dgvPresupuesto.CellFormatting += DgvPresupuesto_CellFormatting;
            dgvPresupuesto.CurrentCellDirtyStateChanged += DgvPresupuesto_CurrentCellDirtyStateChanged;
            dgvPresupuesto.Scroll += (s, e) =>
            {
                if (_lstApuAutocomplete is { Visible: true })
                    PosicionarAutocompleteApu();
                else
                    OcultarAutocompleteApu(false);
            };
            dgvPresupuesto.CellDoubleClick += DgvPresupuesto_CellDoubleClick;
            dgvPresupuesto.KeyDown += DgvPresupuesto_KeyDown;
            dgvPresupuesto.KeyPress += DgvPresupuesto_KeyPress; // Comportamiento Excel
            dgvPresupuesto.CellMouseDown += DgvPresupuesto_CellMouseDown;
            dgvPresupuesto.CellBeginEdit += DgvPresupuesto_CellBeginEdit;
            dgvPresupuesto.CellValidating += DgvPresupuesto_CellValidating;
            dgvPresupuesto.CellEndEdit += DgvPresupuesto_CellEndEdit;
            dgvPresupuesto.EditingControlShowing += DgvPresupuesto_EditingControlShowing;
            dgvPresupuesto.ColumnWidthChanged += DgvPresupuesto_ColumnWidthChanged;
            dgvPresupuesto.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);

            dgvPresupuesto.DataError += DgvPresupuesto_DataError;

            // ── Drag & Drop para reordenar filas ──────────────────────
            dgvPresupuesto.AllowDrop = true;
            dgvPresupuesto.MouseDown += DgvPresupuesto_MouseDown;
            dgvPresupuesto.MouseUp += DgvPresupuesto_MouseUp;
            dgvPresupuesto.MouseMove += DgvPresupuesto_MouseMove;
            dgvPresupuesto.DragOver += DgvPresupuesto_DragOver;
            dgvPresupuesto.DragDrop += DgvPresupuesto_DragDrop;
            dgvPresupuesto.DragLeave += DgvPresupuesto_DragLeave;
            dgvPresupuesto.Paint += DgvPresupuesto_Paint;

            Resize += (s, e) =>
            {
                if (_lstApuAutocomplete is { Visible: true })
                    PosicionarAutocompleteApu();
            };
        }

        private int _dragFilaOrigen = -1;
        private int _dragLineaInsercion = -1;
        private int _dragFilaMouseDown = -1;
        private Point _dragMouseDownLocation = Point.Empty;
        private Rectangle _dragStartRect = Rectangle.Empty;
        private const int DragThresholdPixels = 6;
        private DataGridViewCell _celdaAnterior = null;
        private ContextMenuStrip? _menuPresupuesto;
        private string _valorAnteriorClaveEnEdicion = string.Empty;
        private int _filaClaveEnEdicion = -1;
        private int _columnaClaveEnEdicion = -1;
        private bool _ultimoIntentoClaveInvalida;

        private void DgvPresupuesto_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            // Invalidar celda anterior para quitar borde
            if (_celdaAnterior != null)
            {
                dgvPresupuesto.InvalidateCell(_celdaAnterior);
            }

            // Guardar nueva celda actual
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                _celdaAnterior = dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex];
            }

            // Invalidar celda nueva para dibujar borde
            if (_celdaAnterior != null)
            {
                dgvPresupuesto.InvalidateCell(_celdaAnterior);
            }
        }

                ActualizarEstadoWorkspace(_selectorApuEmbebido != null);
            }
            else
            {
                OcultarAutocompleteApu(false);
            }
        }

        private void DgvPresupuesto_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                _dragFilaMouseDown = -1;
                _dragMouseDownLocation = Point.Empty;
                _dragStartRect = Rectangle.Empty;
                return;
            }

            var hit = dgvPresupuesto.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0)
            {
                _dragFilaMouseDown = -1;
                _dragMouseDownLocation = Point.Empty;
                _dragStartRect = Rectangle.Empty;
                return;
            }

            var tipoCell = ObtenerCeldaPorNombreInterno(hit.RowIndex, "Tipo");
            if (tipoCell?.Value == null || string.IsNullOrWhiteSpace(tipoCell.Value.ToString()))
            {
                _dragFilaMouseDown = -1;
                _dragMouseDownLocation = Point.Empty;
                _dragStartRect = Rectangle.Empty;
                return;
            }

            _dragFilaMouseDown = hit.RowIndex;
            _dragMouseDownLocation = e.Location;
            var dragSize = SystemInformation.DragSize;
            _dragStartRect = new Rectangle(
                e.X - dragSize.Width / 2,
                e.Y - dragSize.Height / 2,
                dragSize.Width,
                dragSize.Height);
        }

        private void DgvPresupuesto_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragFilaMouseDown = -1;
                _dragMouseDownLocation = Point.Empty;
                _dragStartRect = Rectangle.Empty;
                if (_dragLineaInsercion != -1)
                {
                    _dragLineaInsercion = -1;
                    dgvPresupuesto.Invalidate();
                }
            }
        }

        private void DgvPresupuesto_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _dragFilaMouseDown < 0) return;
            if (_dragStartRect != Rectangle.Empty && _dragStartRect.Contains(e.Location)) return;

            var tipoCell = ObtenerCeldaPorNombreInterno(_dragFilaMouseDown, "Tipo");
            if (tipoCell?.Value == null || string.IsNullOrWhiteSpace(tipoCell.Value.ToString())) return;

            _dragFilaOrigen = _dragFilaMouseDown;
            _dragFilaMouseDown = -1;
            _dragMouseDownLocation = Point.Empty;
            _dragStartRect = Rectangle.Empty;
            try
            {
                dgvPresupuesto.DoDragDrop(_dragFilaOrigen, DragDropEffects.Move);
            }
            finally
            {
                if (_dragLineaInsercion != -1)
                {
                    _dragLineaInsercion = -1;
                    dgvPresupuesto.Invalidate();
                }
                _dragFilaOrigen = -1;
            }
        }

        private void DgvPresupuesto_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;

            // Calcular dónde caerá y dibujar línea indicadora
            var pt = dgvPresupuesto.PointToClient(new Point(e.X, e.Y));
            var hit = dgvPresupuesto.HitTest(pt.X, pt.Y);

            if (hit.RowIndex >= 0)
            {
                // Encontrar última fila con datos
                int ultimaFilaConDatos = -1;
                for (int i = dgvPresupuesto.Rows.Count - 1; i >= 0; i--)
                {
                    var tipoCell = ObtenerCeldaPorNombreInterno(i, "Tipo");
                    if (tipoCell != null && tipoCell.Value != null && !string.IsNullOrWhiteSpace(tipoCell.Value.ToString()))
                    {
                        ultimaFilaConDatos = i;
                        break;
                    }
                }

                // Limitar al rango válido
                int lineaDestino = hit.RowIndex;
                if (lineaDestino > ultimaFilaConDatos + 1)
                {
                    lineaDestino = ultimaFilaConDatos + 1;
                }

                if (_dragLineaInsercion != lineaDestino)
                {
                    _dragLineaInsercion = lineaDestino;
                    dgvPresupuesto.Invalidate(); // Redibujar para mostrar línea
                }
            }
        }


        private void DgvPresupuesto_DragLeave(object? sender, EventArgs e)
        {
            if (_dragLineaInsercion != -1)
            {
                _dragLineaInsercion = -1;
                dgvPresupuesto.Invalidate();
            }
        }

        private void DgvPresupuesto_DragDrop(object sender, DragEventArgs e)
        {
            if (_dragFilaOrigen < 0) return;

            // Determinar fila destino
            var pt = dgvPresupuesto.PointToClient(new Point(e.X, e.Y));
            var hit = dgvPresupuesto.HitTest(pt.X, pt.Y);
            int filaDestino = hit.RowIndex;

            if (filaDestino < 0 || filaDestino == _dragFilaOrigen)
            {
                _dragFilaOrigen = -1;
                _dragLineaInsercion = -1;
                _dragFilaMouseDown = -1;
                _dragMouseDownLocation = Point.Empty;
                _dragStartRect = Rectangle.Empty;
                dgvPresupuesto.Invalidate();
                return;
            }

            // RESTRICCIÓN: No permitir crear huecos intermedios
            // Buscar última fila con contenido
            int ultimaFilaConDatos = -1;
            for (int i = dgvPresupuesto.Rows.Count - 1; i >= 0; i--)
            {
                var tipoCell = ObtenerCeldaPorNombreInterno(i, "Tipo");
                if (tipoCell != null && tipoCell.Value != null && !string.IsNullOrWhiteSpace(tipoCell.Value.ToString()))
                {
                    ultimaFilaConDatos = i;
                    break;
                }
            }

            // Solo permitir arrastrar dentro del bloque continuo de datos
            // Verificar que NO hay hueco entre origen y destino
            if (filaDestino > ultimaFilaConDatos + 1)
            {
                filaDestino = ultimaFilaConDatos + 1;
            }

            // VALIDACIÓN ADICIONAL: Si el destino está ENTRE dos filas con contenido,
            // verificar que NO haya hueco
            if (filaDestino > 0 && filaDestino <= ultimaFilaConDatos)
            {
                var filaAnterior = ObtenerCeldaPorNombreInterno(filaDestino - 1, "Tipo");
                var filaDestTipo = ObtenerCeldaPorNombreInterno(filaDestino, "Tipo");

                bool anteriorVacia = filaAnterior == null || filaAnterior.Value == null || string.IsNullOrWhiteSpace(filaAnterior.Value.ToString());
                bool destinoVacio = filaDestTipo == null || filaDestTipo.Value == null || string.IsNullOrWhiteSpace(filaDestTipo.Value.ToString());

                // Si ambas están vacías, no permitir (sería crear hueco)
                if (anteriorVacia && destinoVacio)
                {
                    _dragFilaOrigen = -1;
                    _dragLineaInsercion = -1;
                    _dragFilaMouseDown = -1;
                    _dragMouseDownLocation = Point.Empty;
                    _dragStartRect = Rectangle.Empty;
                    dgvPresupuesto.Invalidate();
                    return;
                }
            }

            // RECOLECTAR TODAS LAS FILAS A MOVER (jerárquico)
            var filasAMover = BudgetHierarchyService.CollectHierarchicalBlock(BuildHierarchyRowsSnapshot(), _dragFilaOrigen);

            // MOVER filas en el grid (sin tocar BD aún)
            // Estrategia: copiar DataGridViewRow completas (no solo valores)
            var rowsAMover = new List<DataGridViewRow>();
            foreach (int idx in filasAMover)
            {
                rowsAMover.Add(dgvPresupuesto.Rows[idx]);
            }

            // Determinar posición de inserción
            int insertEn = filaDestino > _dragFilaOrigen ? filaDestino + 1 : filaDestino;

            // Remover filas de sus posiciones originales (de atrás hacia adelante)
            for (int i = filasAMover.Count - 1; i >= 0; i--)
            {
                dgvPresupuesto.Rows.RemoveAt(filasAMover[i]);
            }

            // Ajustar índice de inserción si movemos hacia abajo
            if (filaDestino > _dragFilaOrigen)
            {
                insertEn -= filasAMover.Count;
            }

            // Insertar filas en nueva posición
            int indiceInicioInsertado = insertEn;
            foreach (var row in rowsAMover)
            {
                dgvPresupuesto.Rows.Insert(insertEn, row);
                insertEn++;
            }

            // Mantener el foco en la primera fila del bloque movido
            if (indiceInicioInsertado >= 0 && indiceInicioInsertado < dgvPresupuesto.Rows.Count)
            {
                dgvPresupuesto.ClearSelection();
                var columnaFoco = dgvPresupuesto.CurrentCell?.ColumnIndex ?? 0;
                if (columnaFoco < 0 || columnaFoco >= dgvPresupuesto.Columns.Count)
                    columnaFoco = 0;
                if (dgvPresupuesto.Columns[columnaFoco].Visible == false)
                {
                    columnaFoco = dgvPresupuesto.Columns
                        .Cast<DataGridViewColumn>()
                        .Where(c => c.Visible)
                        .Select(c => c.Index)
                        .DefaultIfEmpty(0)
                        .First();
                }

                var focusCell = dgvPresupuesto.Rows[indiceInicioInsertado].Cells[columnaFoco];
                dgvPresupuesto.CurrentCell = focusCell;
                dgvPresupuesto.Rows[indiceInicioInsertado].Selected = true;
            }

            _dragFilaOrigen = -1;
            _dragLineaInsercion = -1; // Limpiar línea
            _dragFilaMouseDown = -1;
            _dragMouseDownLocation = Point.Empty;
            _dragStartRect = Rectangle.Empty;
            dgvPresupuesto.Invalidate();

            // LIMPIAR solo filas vacías que están ENTRE contenido (gaps)
            RemoveIntermediateEmptyRows();

            // Ahora SÍ guardar el nuevo orden en BD
            ReasignarNumerosConceptos();
            GuardarOrdenConceptos();
            RecalcularTodosLosTotales();
            GuardarCambios();       // Persistir totales de agrupadores en BD
            dgvPresupuesto.Refresh();
        }

        private void DgvPresupuesto_Paint(object sender, PaintEventArgs e)
        {
            // Dibujar línea de inserción durante drag & drop
            if (_dragLineaInsercion >= 0 && _dragLineaInsercion < dgvPresupuesto.Rows.Count)
            {
                try
                {
                    var rect = dgvPresupuesto.GetRowDisplayRectangle(_dragLineaInsercion, false);
                    if (rect.Height > 0)
                    {
                        using (var pen = new Pen(Color.FromArgb(33, 150, 243), 3))
                        {
                            // Línea horizontal gruesa en la parte superior de la fila
                            e.Graphics.DrawLine(pen, 0, rect.Top, dgvPresupuesto.Width, rect.Top);

                            // Triángulos en los extremos (estilo Excel)
                            var triangleSize = 6;
                            Point[] leftTriangle = {
                                new Point(0, rect.Top - triangleSize),
                                new Point(triangleSize, rect.Top),
                                new Point(0, rect.Top + triangleSize)
                            };
                            Point[] rightTriangle = {
                                new Point(dgvPresupuesto.Width, rect.Top - triangleSize),
                                new Point(dgvPresupuesto.Width - triangleSize, rect.Top),
                                new Point(dgvPresupuesto.Width, rect.Top + triangleSize)
                            };

                            using (var brush = new SolidBrush(Color.FromArgb(33, 150, 243)))
                            {
                                e.Graphics.FillPolygon(brush, leftTriangle);
                                e.Graphics.FillPolygon(brush, rightTriangle);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignorar errores de dibujo
                }
            }
        }

        private void DgvPresupuesto_CellPainting_ConBorde(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // ── ENCABEZADOS (RowIndex == -1): pintar con formato de columna ──
            if (e.RowIndex == -1 && e.ColumnIndex >= 0)
            {
                var col = dgvPresupuesto.Columns[e.ColumnIndex];
                if (col.Tag is ColumnaPersonalizada colDef)
                {
                    // Fondo del encabezado
                    Color fondo = Color.FromArgb(245, 245, 248); // default SOPRO
                    Color texto = Color.FromArgb(60, 60, 60);

                    if (!string.IsNullOrEmpty(colDef.ColorFondo) && colDef.ColorFondo != "#FFFFFF")
                    {
                        try { fondo = ColorTranslator.FromHtml(colDef.ColorFondo); }
                        catch { }
                    }
                    if (!string.IsNullOrEmpty(colDef.ColorFuente) && colDef.ColorFuente != "#000000")
                    {
                        try { texto = ColorTranslator.FromHtml(colDef.ColorFuente); }
                        catch { }
                    }

                    e.Graphics.FillRectangle(new SolidBrush(fondo), e.CellBounds);

                    // Borde inferior del encabezado
                    using var penBorde = new Pen(Color.FromArgb(200, 200, 200));
                    e.Graphics.DrawLine(penBorde,
                        e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right, e.CellBounds.Bottom - 1);
                    e.Graphics.DrawLine(penBorde,
                        e.CellBounds.Right - 1, e.CellBounds.Top,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom);

                    // Fuente del encabezado
                    string nombreFuente = !string.IsNullOrEmpty(colDef.NombreFuente)
                        ? colDef.NombreFuente : "Segoe UI";
                    float tamaño = colDef.TamanoFuente > 0 ? colDef.TamanoFuente : 9f;
                    FontStyle fs = FontStyle.Bold
                                 | (colDef.Cursiva ? FontStyle.Italic : FontStyle.Regular);

                    using var fuente = new Font(nombreFuente, tamaño, fs);

                    // Alineación del texto del encabezado
                    var sf = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        Alignment = colDef.Alineacion switch
                        {
                            AlineacionColumna.Derecha => StringAlignment.Far,
                            AlineacionColumna.Centro => StringAlignment.Center,
                            _ => StringAlignment.Near,
                        }
                    };

                    var rectTexto = new RectangleF(
                        e.CellBounds.Left + 4, e.CellBounds.Top,
                        e.CellBounds.Width - 8, e.CellBounds.Height);

                    e.Graphics.DrawString(e.Value?.ToString() ?? "", fuente,
                        new SolidBrush(texto), rectTexto, sf);

                    e.Handled = true;
                    return;
                }
            }

            // ── CELDAS DE DATOS: el borde azul lo maneja DgvCeldaHelper ──────
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            // Dejar que el helper pinte; aquí no hacemos nada para celdas de datos.
        }

        /// <summary>
        /// Guarda el campo Orden de todos los ConceptoPresupuesto según su posición actual en el grid.
        /// </summary>
        private void GuardarOrdenConceptos()
        {
            try
            {
                int orden = 0;
                for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
                {
                    if (dgvPresupuesto.Rows[i].Tag is ConceptoPresupuesto c && c.Id > 0)
                    {
                        var enBD = _context.ConceptosPresupuesto.Find(c.Id);
                        if (enBD != null)
                        {
                            enBD.Orden = orden;
                            c.Orden = orden;
                        }
                        orden++;
                    }
                }
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando orden: {ex.Message}");
            }
        }

        private void DgvPresupuesto_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            OcultarAutocompleteApu(false);
            // Guardar el nuevo ancho cuando el usuario lo cambia
            if (_cargando)
            {
                System.Diagnostics.Debug.WriteLine($"ColumnWidthChanged: Ignorado porque _cargando=true");
                return;
            }

            try
            {
                if (e.Column.Tag is ColumnaPersonalizada colDef)
                {
                    var columnaDB = _context.ColumnasPersonalizadas.Find(colDef.Id);
                    if (columnaDB != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"ColumnWidthChanged: Guardando ancho {e.Column.Width} para columna '{colDef.Nombre}' (ID={colDef.Id})");
                        columnaDB.AnchoColumna = e.Column.Width;
                        _context.SaveChanges();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"ColumnWidthChanged: No se encontró columna en BD con ID={colDef.Id}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ColumnWidthChanged: Columna '{e.Column.Name}' no tiene Tag de ColumnaPersonalizada");
                }
            }
            catch (Exception ex)
            {
                // Silencioso - no molestar al usuario mientras redimensiona
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna: {ex.Message}");
            }
        }

        private void DgvPresupuesto_ColumnDisplayIndexChanged(object sender, DataGridViewColumnEventArgs e)
        {
            // Guardar el nuevo orden de columnas en la BD
            if (_cargando) return; // No guardar durante la carga inicial

            try
            {
                // Actualizar el campo Orden de cada columna según su DisplayIndex
                foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                {
                    if (col.Tag is ColumnaPersonalizada colDef)
                    {
                        var columnaDB = _context.ColumnasPersonalizadas.Find(colDef.Id);
                        if (columnaDB != null)
                        {
                            columnaDB.Orden = col.DisplayIndex;
                        }
                    }
                }

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                // Silencioso - no molestar al usuario mientras arrastra
                System.Diagnostics.Debug.WriteLine($"Error guardando orden de columnas: {ex.Message}");
            }
        }

        private List<SOPRO.Application.Models.Presupuesto.BudgetConceptKeyRowSnapshot> BuildKeyRowSnapshots()
        {
            var rows = new List<SOPRO.Application.Models.Presupuesto.BudgetConceptKeyRowSnapshot>();

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                rows.Add(new SOPRO.Application.Models.Presupuesto.BudgetConceptKeyRowSnapshot
                {
                    RowIndex = i,
                    Key = ObtenerCeldaTexto(i, "Clave"),
                    Description = ObtenerCeldaTexto(i, "Descripcion"),
                    Concept = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto
                });
            }

            return rows;
        }

        private int CountConceptRowsBefore(int rowIndex)
        {
            int conceptosAntes = 0;
            for (int i = 0; i < rowIndex; i++)
            {
                if (dgvPresupuesto.Rows[i].Tag != null)
                {
                    conceptosAntes++;
                }
            }
            return conceptosAntes;
        }

        private void ApplyAssignmentDraftToGrid(int rowIndex, SOPRO.Application.Models.Presupuesto.BudgetConceptAssignmentDraft draft)
        {
            var claveCell = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
            var descCell = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion");
            var unidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Unidad");
            var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
            var puCell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
            var importeCell = ObtenerCeldaPorNombreInterno(rowIndex, "Importe");

            if (claveCell != null) claveCell.Value = draft.Clave;
            if (descCell != null) descCell.Value = draft.Descripcion;
            if (unidadCell != null) unidadCell.Value = draft.Unidad;
            if (cantidadCell != null) cantidadCell.Value = draft.Cantidad;
            if (puCell != null) puCell.Value = draft.PrecioUnitario.ToStringImporte();
            if (importeCell != null) importeCell.Value = draft.ImporteTotal.ToStringImporte();
        }

        private void FinalizeBudgetConceptAssignment(int rowIndex)
        {
            int indicePadre = ObtenerIndicePadre(rowIndex);
            while (indicePadre >= 0)
            {
                ActualizarTotalAgrupador(indicePadre);
                indicePadre = ObtenerIndicePadre(indicePadre);
            }

            ReasignarNumerosConceptos();
            _panelMatricesEmbebido?.NotificarFilaCambiada(rowIndex);
            dgvPresupuesto.Refresh();
        }

        private void ProgramarAsegurarFilaActualVisibleEnPresupuesto()
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(new Action(AsegurarFilaActualVisibleEnPresupuesto));
            }
            catch
            {
                // Ignorar si el formulario ya se está cerrando.
            }
        }

        private void AsegurarFilaActualVisibleEnPresupuesto()
        {
            if (dgvPresupuesto == null || dgvPresupuesto.IsDisposed || dgvPresupuesto.CurrentCell == null)
                return;

            int rowIndex = dgvPresupuesto.CurrentCell.RowIndex;
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                return;

            if (!dgvPresupuesto.Rows[rowIndex].Visible)
                return;

            try
            {
                var rect = dgvPresupuesto.GetRowDisplayRectangle(rowIndex, false);
                int visibleTop = 0;
                int visibleBottom = dgvPresupuesto.ClientSize.Height - 4;

                bool fueraPorArriba = rect.Height <= 0 || rect.Top < visibleTop;
                bool fueraPorAbajo = rect.Height <= 0 || rect.Bottom > visibleBottom;

                if (!fueraPorArriba && !fueraPorAbajo)
                    return;

                int displayedRows = Math.Max(1, dgvPresupuesto.DisplayedRowCount(false));
                int targetRow;

                if (fueraPorArriba)
                {
                    targetRow = rowIndex;
                }
                else
                {
                    int margenFilas = Math.Max(1, Math.Min(3, displayedRows / 3));
                    targetRow = Math.Max(0, rowIndex - Math.Max(0, displayedRows - margenFilas));
                }

                if (targetRow >= 0 && targetRow < dgvPresupuesto.Rows.Count)
                    dgvPresupuesto.FirstDisplayedScrollingRowIndex = targetRow;
            }
            catch
            {
                // Ignorar si el grid todavía no puede desplazar la fila.
            }
        }

        private void LoadWorkspacePanelState()
        {
            try
            {
                var dir = Path.GetDirectoryName(WorkspacePanelStatePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(WorkspacePanelStatePath))
                {
                    var raw = File.ReadAllText(WorkspacePanelStatePath).Trim();
                    if (int.TryParse(raw, out var h) && h > 120)
                        _workspacePanelHeight = h;
                }
            }
            catch { }
        }

        private void SaveWorkspacePanelState()
        {
            try
            {
                var dir = Path.GetDirectoryName(WorkspacePanelStatePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (!splitContainer.Panel2Collapsed)
                {
                    int height = splitContainer.Height - splitContainer.SplitterDistance;
                    if (height > splitContainer.Panel2MinSize)
                    {
                        _workspacePanelHeight = height;
                        File.WriteAllText(WorkspacePanelStatePath, _workspacePanelHeight.ToString());
                    }
                }
            }
            catch { }
        }

        private void EnsureWorkspaceExpanded()
        {
            if (splitContainer.Panel2Collapsed)
            {
                splitContainer.Panel2Collapsed = false;
                var targetHeight = _workspacePanelHeight > 0 ? _workspacePanelHeight : Math.Max(splitContainer.Panel2MinSize, (int)(splitContainer.Height * 0.34));
                splitContainer.SplitterDistance = Math.Max(splitContainer.Panel1MinSize, splitContainer.Height - targetHeight);
            }
            btnToggleMatrices.Text = "📐 Matrices ▲";
            ProgramarAsegurarFilaActualVisibleEnPresupuesto();
        }

        private void ActualizarEstadoWorkspace(bool mostrandoSelector)
        {
            lblWorkspaceTitulo.Text = mostrandoSelector ? "Área de trabajo: Selector APU" : "Área de trabajo: Matriz";
            btnWorkspaceMatriz.BackColor = mostrandoSelector ? Color.Transparent : Color.FromArgb(221, 235, 247);
            btnWorkspaceSelector.BackColor = mostrandoSelector ? Color.FromArgb(221, 235, 247) : Color.Transparent;
            btnWorkspaceMatriz.Font = new Font(btnWorkspaceMatriz.Font, mostrandoSelector ? FontStyle.Regular : FontStyle.Bold);
            btnWorkspaceSelector.Font = new Font(btnWorkspaceSelector.Font, mostrandoSelector ? FontStyle.Bold : FontStyle.Regular);
            btnWorkspaceSelector.Enabled = dgvPresupuesto.CurrentRow != null && ((ObtenerCeldaPorNombreInterno(dgvPresupuesto.CurrentRow.Index, "Tipo")?.Value?.ToString()) == "Concepto");
            btnWorkspaceCerrar.Enabled = !splitContainer.Panel2Collapsed;
        }

        private void MostrarPanelMatrizEmbebido(bool recargarFilaActual)
        {
            EnsureWorkspaceExpanded();

            if (_selectorApuEmbebido != null)
            {
                try
                {
                    panelMatricesHost.Controls.Remove(_selectorApuEmbebido);
                    _selectorApuEmbebido.Dispose();
                }
                catch { }
                _selectorApuEmbebido = null;
            }

            _selectorApuEmbebidoRowIndex = -1;
            if (_panelMatricesEmbebido != null)
            {
                if (!panelMatricesHost.Controls.Contains(_panelMatricesEmbebido))
                    panelMatricesHost.Controls.Add(_panelMatricesEmbebido);

                _panelMatricesEmbebido.Dock = DockStyle.Fill;
                _panelMatricesEmbebido.Visible = true;
                _panelMatricesEmbebido.BringToFront();

                if (recargarFilaActual && dgvPresupuesto.CurrentRow != null)
                    _panelMatricesEmbebido.NotificarFilaCambiada(dgvPresupuesto.CurrentRow.Index);
            }

            ActualizarEstadoWorkspace(false);
        }

        private void MostrarSelectorApuEmbebido(int rowIndex, decimal cantidadActual, int? matrizIdActual, string? filtroInicial)
        {
            EnsureWorkspaceExpanded();

            if (_panelMatricesEmbebido != null)
                _panelMatricesEmbebido.Visible = false;

            if (_selectorApuEmbebido != null)
            {
                try
                {
                    panelMatricesHost.Controls.Remove(_selectorApuEmbebido);
                    _selectorApuEmbebido.Dispose();
                }
                catch { }
                _selectorApuEmbebido = null;
            }

            _selectorApuEmbebidoRowIndex = rowIndex;
            var targetRowIndex = rowIndex;
            var selector = new SelectorApuEmbebidoControl();
            selector.InitializeSelector(_context, _proyecto.Id, cantidadActual, matrizIdActual, filtroInicial);
            selector.Accepted += (_, __) =>
            {
                try
                {
                    if (targetRowIndex >= 0 && selector.MatrizSeleccionada != null)
                        AsignarMatrizAPresupuesto(targetRowIndex, selector.MatrizSeleccionada, selector.Cantidad, preserveCurrentTexts: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al asignar APU: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    MostrarPanelMatrizEmbebido(true);
                    if (targetRowIndex >= 0 && targetRowIndex < dgvPresupuesto.Rows.Count)
                    {
                        var cell = ObtenerCeldaPorNombreInterno(targetRowIndex, "Descripcion")
                                   ?? ObtenerCeldaPorNombreInterno(targetRowIndex, "PrecioUnitario");
                        if (cell != null)
                            dgvPresupuesto.CurrentCell = cell;
                        dgvPresupuesto.Focus();
                    }
                }
            };
            selector.Cancelled += (_, __) =>
            {
                MostrarPanelMatrizEmbebido(true);
                if (rowIndex >= 0 && rowIndex < dgvPresupuesto.Rows.Count)
                {
                    var cell = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion")
                               ?? ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
                    if (cell != null)
                        dgvPresupuesto.CurrentCell = cell;
                    dgvPresupuesto.Focus();
                }
            };
            selector.RequestNewMatrix += (_, __) =>
            {
                MostrarPanelMatrizEmbebido(false);
                _panelMatricesEmbebido?.BeginCreateMatrix(selector.SelectedTipo,
                    onSaved: matrizCreada =>
                    {
                        try
                        {
                            if (targetRowIndex >= 0)
                                AsignarMatrizAPresupuesto(targetRowIndex, matrizCreada, cantidadActual, preserveCurrentTexts: true);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error al asignar la matriz creada: {ex.Message}", "Presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        finally
                        {
                            MostrarPanelMatrizEmbebido(true);
                            if (targetRowIndex >= 0 && targetRowIndex < dgvPresupuesto.Rows.Count)
                            {
                                var cell = ObtenerCeldaPorNombreInterno(targetRowIndex, "Descripcion")
                                           ?? ObtenerCeldaPorNombreInterno(targetRowIndex, "PrecioUnitario");
                                if (cell != null)
                                    dgvPresupuesto.CurrentCell = cell;
                                dgvPresupuesto.Focus();
                            }
                        }
                    },
                    onCancelled: () => MostrarSelectorApuEmbebido(targetRowIndex, cantidadActual, matrizIdActual, filtroInicial));
            };
            selector.RequestEditMatrix += (_, __) =>
            {
                var selectedMatrixId = selector.SelectedMatrixId;
                if (!selectedMatrixId.HasValue)
                {
                    MessageBox.Show("Selecciona una matriz para editar.", "Selector APU", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                MostrarPanelMatrizEmbebido(false);
                _panelMatricesEmbebido?.BeginEditMatrix(selectedMatrixId.Value,
                    onSaved: null,
                    onCancelled: () => MostrarSelectorApuEmbebido(targetRowIndex, cantidadActual, selectedMatrixId, filtroInicial));
            };

            _selectorApuEmbebido = selector;
            selector.Dock = DockStyle.Fill;
            panelMatricesHost.Controls.Add(selector);
            selector.BringToFront();
            selector.Focus();

            ActualizarEstadoWorkspace(true);
        }

        private void btnWorkspaceMatriz_Click(object sender, EventArgs e)
        {
            MostrarPanelMatrizEmbebido(true);
        }

        private void btnWorkspaceCerrar_Click(object sender, EventArgs e)
        {
            SaveWorkspacePanelState();
            splitContainer.Panel2Collapsed = true;
            btnToggleMatrices.Text = "📐 Matrices ▼";
            btnWorkspaceCerrar.Enabled = false;
            dgvPresupuesto.Focus();
        }

        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            using var frm = new FormImportarPresupuestoExcel();
            if (frm.ShowDialog(this) != DialogResult.OK || frm.FilasImportadas == null || frm.FilasImportadas.Count == 0)
                return;

            ImportarFilasPresupuestoDesdeExcel(frm.FilasImportadas);
        }

        private void ImportarFilasPresupuestoDesdeExcel(List<BudgetExcelImportRowDto> filas)
        {
            try
            {
                if (filas == null || filas.Count == 0)
                    return;

                int ordenBase = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .Select(c => (int?)c.Orden)
                    .Max() ?? -1;

                var nuevosConceptos = new List<ConceptoPresupuesto>();
                int offset = 1;
                bool yaSeAsignoCapituloInferido = false;
                foreach (var fila in filas)
                {
                    string tipo = ResolverTipoImportado(fila, ref yaSeAsignoCapituloInferido);
                    if (string.Equals(tipo, "No importar", StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool esAgrupador = !string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase);

                    var concepto = new ConceptoPresupuesto
                    {
                        ProyectoId = _proyecto.Id,
                        Clave = fila.Clave?.Trim() ?? string.Empty,
                        Descripcion = fila.Descripcion?.Trim() ?? string.Empty,
                        Unidad = esAgrupador ? string.Empty : (fila.Unidad?.Trim() ?? string.Empty),
                        Cantidad = esAgrupador ? 0m : (fila.Cantidad ?? 0m),
                        EsAgrupador = esAgrupador,
                        Nivel = BudgetPersistenceService.ResolveLevelFromType(tipo),
                        Orden = ordenBase + offset++,
                        MatrizId = null,
                        CostoDirectoUnitario = 0m,
                        CostoDirectoTotal = 0m,
                        Indirectos = 0m,
                        Financiamiento = 0m,
                        Utilidad = 0m,
                        CargosAdicionales = 0m,
                        PrecioUnitario = 0m,
                        ImporteTotal = 0m,
                        ColumnasPersonalizadasJSON = string.Empty,
                        Notas = string.Empty,
                        FechaCreacion = DateTime.Now,
                        FechaModificacion = DateTime.Now
                    };

                    nuevosConceptos.Add(concepto);
                }

                if (nuevosConceptos.Count == 0)
                {
                    MessageBox.Show("No se generaron conceptos válidos para importar.", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _context.ConceptosPresupuesto.AddRange(nuevosConceptos);
                _context.SaveChanges();

                RecargarPresupuestoPreservandoEstado();
                MessageBox.Show($"Se importaron {nuevosConceptos.Count} renglones desde Excel\n\nNo se importaron P.U. ni Importe.", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar el presupuesto desde Excel:\n{ex.Message}", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ResolverTipoImportado(BudgetExcelImportRowDto fila, ref bool yaSeAsignoCapituloInferido)
        {
            string tipoTexto = (fila.TipoTexto ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(tipoTexto))
            {
                string normalized = tipoTexto.ToLowerInvariant();
                if (normalized.Contains("no import") || normalized == "omit" || normalized == "omitir") return "No importar";
                if (normalized.Contains("subcap")) return "Subcapitulo";
                if (normalized.Contains("nivel 3") || normalized == "n3") return "Nivel 3";
                if (normalized.Contains("nivel 2") || normalized == "n2") return "Nivel 2";
                if (normalized.Contains("nivel 1") || normalized == "n1") return "Nivel 1";
                if (normalized.Contains("cap")) return "Capitulo";
                if (normalized.Contains("titulo") || normalized.Contains("título") || normalized.Contains("encabezado")) return "Capitulo";
                if (normalized.Contains("concept")) return "Concepto";
            }

            bool hasUnidad = !string.IsNullOrWhiteSpace(fila.Unidad);
            bool hasCantidad = fila.Cantidad.HasValue && fila.Cantidad.Value != 0m;
            if (hasUnidad || hasCantidad)
                return "Concepto";

            if (!yaSeAsignoCapituloInferido)
            {
                yaSeAsignoCapituloInferido = true;
                return "Capitulo";
            }

            return "Subcapitulo";
        }

        private void btnWorkspaceSelector_Click(object sender, EventArgs e)
        {
            if (dgvPresupuesto.CurrentRow == null)
                return;

            var rowIndex = dgvPresupuesto.CurrentRow.Index;
            var tipo = ObtenerCeldaPorNombreInterno(rowIndex, "Tipo")?.Value?.ToString();
            if (!string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Seleccione una fila tipo Concepto para cambiar su APU.", "Selector APU", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            decimal cantidadActual = 1m;
            var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
            if (cantidadCell?.Value != null)
                decimal.TryParse(cantidadCell.Value.ToString(), out cantidadActual);

            int? matrizIdActual = (dgvPresupuesto.Rows[rowIndex].Tag as ConceptoPresupuesto)?.MatrizId;
            var descripcionFiltro = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion")?.Value?.ToString()?.Trim();
            var filtroInicial = matrizIdActual.HasValue ? null : (!string.IsNullOrWhiteSpace(descripcionFiltro) ? descripcionFiltro : null);

            MostrarSelectorApuEmbebido(rowIndex, cantidadActual, matrizIdActual, filtroInicial);
        }

        private void OpenApuSelectorForRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count) return;

            var row = dgvPresupuesto.Rows[rowIndex];
            var tipoCell = ObtenerCeldaPorNombreInterno(rowIndex, "Tipo");
            if (tipoCell?.Value?.ToString() != "Concepto") return;

            decimal cantidadActual = 1m;
            var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
            if (cantidadCell?.Value != null)
                decimal.TryParse(cantidadCell.Value.ToString(), out cantidadActual);

            int? matrizIdActual = (row.Tag as ConceptoPresupuesto)?.MatrizId;
            var descripcionFiltro = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion")?.Value?.ToString()?.Trim();
            var filtroInicial = matrizIdActual.HasValue
                ? null
                : !string.IsNullOrWhiteSpace(descripcionFiltro)
                    ? descripcionFiltro
                    : null;

            var selection = ApuSelectorDialogService.SelectMatrix(this, _context, _proyecto.Id, cantidadActual, true, matrizIdActual, filtroInicial);
            if (!selection.Accepted || selection.Matriz == null)
                return;

            var claveActual = ObtenerCeldaPorNombreInterno(rowIndex, "Clave")?.Value?.ToString();
            var descripcionActual = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion")?.Value?.ToString();
            var unidadActual = ObtenerCeldaPorNombreInterno(rowIndex, "Unidad")?.Value?.ToString();

            var draft = BudgetConceptAssignmentService.BuildDraftFromSelectedMatrix(
                _proyecto,
                selection.Matriz,
                selection.Cantidad,
                claveActual,
                descripcionActual,
                unidadActual);

            ApplyAssignmentDraftToGrid(rowIndex, draft);

            try
            {
                var concepto = BudgetConceptAssignmentService.ApplyDraft(
                    _context,
                    _proyecto.Id,
                    CountConceptRowsBefore(rowIndex),
                    row.Tag as ConceptoPresupuesto,
                    draft);

                row.Tag = concepto;
                FinalizeBudgetConceptAssignment(rowIndex);
                GuardarCambios();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al asignar APU: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            var filtroInicial = !string.IsNullOrWhiteSpace(descripcionFiltro)
                ? descripcionFiltro
                : null;

            MostrarSelectorApuEmbebido(rowIndex, cantidadActual, null, filtroInicial);
        }

        private bool TryApplyBudgetConceptAssignmentByKey(int rowIndex, bool showErrors = true)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count) return false;

            var tipoCell = ObtenerCeldaPorNombreInterno(rowIndex, "Tipo");
            if (tipoCell?.Value?.ToString() != "Concepto") return false;

            string claveNueva = ObtenerCeldaPorNombreInterno(rowIndex, "Clave")?.Value?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(claveNueva)) return false;

            var row = dgvPresupuesto.Rows[rowIndex];
            var conceptoActual = row.Tag as ConceptoPresupuesto;

            var assignment = BudgetConceptAssignmentService.ResolveByKey(
                _context,
                _proyecto,
                rowIndex,
                claveNueva,
                conceptoActual,
                ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad")?.Value?.ToString(),
                BuildKeyRowSnapshots());

            if (assignment.IsInvalidMatrixTypeSelection)
            {
                if (showErrors)
                {
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(assignment.InvalidSelectionMessage)
                            ? "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. Seleccione una matriz de tipo APU."
                            : assignment.InvalidSelectionMessage,
                        "Selección no válida",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                var claveCellInvalida = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
                if (claveCellInvalida != null)
                    claveCellInvalida.Value = _valorAnteriorClaveEnEdicion ?? conceptoActual?.Clave ?? string.Empty;

                return false;
            }

            if (!assignment.HasAssignment || assignment.Draft == null)
                return false;

            if (assignment.RequiresConfirmation)
            {
                var result = MessageBox.Show(
                    assignment.ConfirmationMessage,
                    "Clave Duplicada - Confirmar Reemplazo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    var claveCell = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
                    if (claveCell != null)
                        claveCell.Value = assignment.RestoreKeyValue;
                    return false;
                }
            }

            ApplyAssignmentDraftToGrid(rowIndex, assignment.Draft);

            try
            {
                var concepto = BudgetConceptAssignmentService.ApplyDraft(
                    _context,
                    _proyecto.Id,
                    CountConceptRowsBefore(rowIndex),
                    conceptoActual,
                    assignment.Draft);

                row.Tag = concepto;
                FinalizeBudgetConceptAssignment(rowIndex);
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors)
                {
                    MessageBox.Show($"Error al aplicar clave: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return false;
            }
        }

        private void HandleClipboardPaste()
        {
            if (dgvPresupuesto.CurrentCell == null) return;

            var currentColumn = dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex];
            string? startInternalName = (currentColumn.Tag as ColumnaPersonalizada)?.NombreInterno;
            var plan = BudgetClipboardPasteService.BuildPlan(startInternalName, Clipboard.GetText());

            if (!plan.IsValid)
            {
                MessageBox.Show(plan.ErrorMessage, "Pegado no permitido", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int startRowIndex = dgvPresupuesto.CurrentCell.RowIndex;
            int skippedCells = 0;
            bool hadChanges = false;

            dgvPresupuesto.SuspendLayout();
            try
            {
                foreach (var pasteRow in plan.Rows.OrderBy(r => r.RowOffset))
                {
                    int rowIndex = startRowIndex + pasteRow.RowOffset;
                    while (rowIndex >= dgvPresupuesto.Rows.Count)
                    {
                        dgvPresupuesto.Rows.Add();
                    }

                    string tipo = ObtenerCeldaTexto(rowIndex, "Tipo");
                    bool hasKey = !string.IsNullOrWhiteSpace(ObtenerCeldaTexto(rowIndex, "Clave"));
                    bool rowWillHaveKeyAfterPaste = hasKey || pasteRow.ValuesByColumn.ContainsKey("Clave");

                    foreach (var columnName in BudgetClipboardPasteService.GetColumnOrder())
                    {
                        if (!pasteRow.ValuesByColumn.TryGetValue(columnName, out var rawValue))
                            continue;

                        if (!BudgetClipboardPasteService.CanPasteIntoColumn(tipo, columnName, hasKey, rowWillHaveKeyAfterPaste))
                        {
                            skippedCells++;
                            continue;
                        }

                        if (!BudgetClipboardPasteService.TryNormalizeValue(columnName, rawValue, out var normalizedValue))
                        {
                            skippedCells++;
                            continue;
                        }

                        var cell = ObtenerCeldaPorNombreInterno(rowIndex, columnName);
                        if (cell == null)
                        {
                            skippedCells++;
                            continue;
                        }

                        cell.Value = normalizedValue;
                        hadChanges = true;

                        if (string.Equals(columnName, "Clave", StringComparison.OrdinalIgnoreCase))
                        {
                            hasKey = !string.IsNullOrWhiteSpace(normalizedValue);
                            rowWillHaveKeyAfterPaste = hasKey;
                            TryApplyBudgetConceptAssignmentByKey(rowIndex);
                        }
                    }
                }
            }
            finally
            {
                dgvPresupuesto.ResumeLayout();
            }

            if (hadChanges)
            {
                GuardarCambios();
                ActualizarEstadisticas();
                dgvPresupuesto.Refresh();
            }

            if (skippedCells > 0)
            {
                MessageBox.Show($"Se omitieron {skippedCells} celdas por restricciones de edición o valores inválidos.", "Pegado parcial", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DgvPresupuesto_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var columnaEditada = dgvPresupuesto.Columns[e.ColumnIndex];
            if (!(columnaEditada.Tag is ColumnaPersonalizada colDefClave) || colDefClave.NombreInterno != "Clave")
                return;

            var tipoCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Tipo");
            if (tipoCell?.Value?.ToString() != "Concepto")
                return;

            string claveNueva = (e.FormattedValue?.ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(claveNueva))
            {
                _ultimoIntentoClaveInvalida = false;
                return;
            }

            string claveNormalizada = claveNueva.ToUpperInvariant();
            var matriz = _context.Matrices
                .FirstOrDefault(m => m.ProyectoId == _proyecto.Id
                    && m.Clave != null
                    && m.Clave.Trim().ToUpper() == claveNormalizada);

            if (matriz == null || matriz.Tipo == TipoMatriz.APU)
            {
                _ultimoIntentoClaveInvalida = false;
                return;
            }

            _ultimoIntentoClaveInvalida = true;
            e.Cancel = true;
            MessageBox.Show(
                "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. Seleccione una matriz de tipo APU.",
                "Selección no válida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            if (dgvPresupuesto.EditingControl is TextBox tb)
            {
                tb.SelectionStart = 0;
                tb.SelectionLength = tb.TextLength;
            }
        }

        private void DgvPresupuesto_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb)
            {
                tb.KeyDown -= DgvPresupuesto_EditingControl_KeyDown;

                if (dgvPresupuesto.CurrentCell?.OwningColumn?.Tag is ColumnaPersonalizada colDefEdit
                    && colDefEdit.NombreInterno == "Clave")
                {
                    tb.KeyDown += DgvPresupuesto_EditingControl_KeyDown;
                }
            }
        }

                OcultarAutocompleteApu(false);
            }));
        }

        private void DgvPresupuesto_EditingControl_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (_lstApuAutocomplete is not { Visible: true })
                return;

            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape)
                e.IsInputKey = true;
        }

        private void DgvPresupuesto_EditingControl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (ProcesarTeclaAutocomplete(e.KeyData))
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.KeyCode != Keys.Escape || !_ultimoIntentoClaveInvalida)
                return;

            _ultimoIntentoClaveInvalida = false;

            if (sender is TextBox tb)
            {
                tb.Text = _valorAnteriorClaveEnEdicion ?? string.Empty;
                tb.SelectionStart = tb.TextLength;
                tb.SelectionLength = 0;
            }

            e.SuppressKeyPress = true;
            e.Handled = true;

            BeginInvoke(new Action(() =>
            {
                dgvPresupuesto.CancelEdit();

                if (_filaClaveEnEdicion >= 0
                    && _filaClaveEnEdicion < dgvPresupuesto.Rows.Count
                    && _columnaClaveEnEdicion >= 0
                    && _columnaClaveEnEdicion < dgvPresupuesto.Columns.Count)
                {
                    var celdaClave = dgvPresupuesto.Rows[_filaClaveEnEdicion].Cells[_columnaClaveEnEdicion];
                    celdaClave.Value = _valorAnteriorClaveEnEdicion ?? string.Empty;
                    dgvPresupuesto.CurrentCell = celdaClave;
                }
            }));
        }

        private static bool EsColumnaUndoPresupuesto(string nombreInterno)
        {
            return string.Equals(nombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "Unidad", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryUndoBudgetEdit()
        {
            if (!_undoManager.CanUndo)
                return false;

            try
            {
                _isUndoRedo = true;
                return _undoManager.Undo();
            }
            finally
            {
                _isUndoRedo = false;
            }
        }

        private bool TryRedoBudgetEdit()
        {
            if (!_undoManager.CanRedo)
                return false;

            try
            {
                _isUndoRedo = true;
                return _undoManager.Redo();
            }
            finally
            {
                _isUndoRedo = false;
            }
        }

        private void AplicarUndoRedoPresupuestoCelda(int rowIndex, int columnIndex, string value)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                return;
            if (columnIndex < 0 || columnIndex >= dgvPresupuesto.Columns.Count)
                return;

            var cell = dgvPresupuesto.Rows[rowIndex].Cells[columnIndex];
            dgvPresupuesto.CurrentCell = cell;
            cell.Value = value;

            if (dgvPresupuesto.Columns[columnIndex].Tag is ColumnaPersonalizada colDef
                && string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
            {
                DgvPresupuesto_CellValueChanged(dgvPresupuesto, new DataGridViewCellEventArgs(columnIndex, rowIndex));
            }
            else
            {
                GuardarCambios();
                ActualizarEstadisticas();
                dgvPresupuesto.Refresh();
            }
        }

        private void DgvPresupuesto_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var valorNuevoUndo = dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            if (!_isUndoRedo
                && e.RowIndex == _undoBudgetRowIndex
                && e.ColumnIndex == _undoBudgetColumnIndex
                && EsColumnaUndoPresupuesto(_undoBudgetColumnName)
                && !string.Equals(_undoBudgetOldValue, valorNuevoUndo, StringComparison.Ordinal))
            {
                int rowIndex = e.RowIndex;
                int columnIndex = e.ColumnIndex;
                string oldValue = _undoBudgetOldValue;
                string newValue = valorNuevoUndo;
                string descripcion = $"Editar {_undoBudgetColumnName} en presupuesto";

                _undoManager.Push(new DelegateUndoableAction(
                    descripcion,
                    () => AplicarUndoRedoPresupuestoCelda(rowIndex, columnIndex, oldValue),
                    () => AplicarUndoRedoPresupuestoCelda(rowIndex, columnIndex, newValue)));
            }

            _undoBudgetRowIndex = -1;
            _undoBudgetColumnIndex = -1;
            _undoBudgetColumnName = string.Empty;
            _undoBudgetOldValue = string.Empty;

            // Restaurar color de fondo al terminar edición
            dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.White;
            bool cursorSobreLista = _lstApuAutocomplete != null && _lstApuAutocomplete.Visible && _lstApuAutocomplete.Bounds.Contains(PointToClient(Cursor.Position));
            bool mantenerAutocomplete = _confirmandoSeleccionAutocomplete || _mouseDownEnAutocomplete || cursorSobreLista || (_lstApuAutocomplete?.Focused ?? false) || (_lstApuAutocomplete?.ContainsFocus ?? false);
            if (!mantenerAutocomplete)
                OcultarAutocompleteApu(false);

            if (e.RowIndex == _filaClaveEnEdicion && e.ColumnIndex == _columnaClaveEnEdicion)
            {
                _filaClaveEnEdicion = -1;
                _columnaClaveEnEdicion = -1;
                _ultimoIntentoClaveInvalida = false;
            }
        }

        private void DgvPresupuesto_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var columnaEditada = dgvPresupuesto.Columns[e.ColumnIndex];
                if (columnaEditada.Tag is ColumnaPersonalizada colDefClaveEdit && colDefClaveEdit.NombreInterno == "Clave")
                {
                    _filaClaveEnEdicion = e.RowIndex;
                    _columnaClaveEnEdicion = e.ColumnIndex;
                    _valorAnteriorClaveEnEdicion = dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
                    _ultimoIntentoClaveInvalida = false;
                }
            }

            // Validar que no se salten filas vacías
            int primeraFilaVacia = ObtenerPrimeraFilaVacia();

            if (e.RowIndex > primeraFilaVacia)
            {
                e.Cancel = true;

                // Mover silenciosamente a la primera fila vacía
                BeginInvoke(new Action(() =>
                {
                    if (primeraFilaVacia >= 0 && primeraFilaVacia < dgvPresupuesto.Rows.Count)
                    {
                        var tipoCell = ObtenerCeldaPorNombreInterno(primeraFilaVacia, "Tipo");
                        if (tipoCell != null)
                        {
                            dgvPresupuesto.CurrentCell = tipoCell;
                        }
                    }
                }));
                return;
            }

            // Si está cerca del final (quedan menos de 10 filas), agregar más
            int filasRestantes = dgvPresupuesto.Rows.Count - e.RowIndex;
            if (filasRestantes < 10)
            {
                for (int i = 0; i < 50; i++)
                {
                    dgvPresupuesto.Rows.Add();
                }
            }

            // Validar qué columnas son editables según el tipo de fila
            var tipoCell = dgvPresupuesto.Rows[e.RowIndex].Cells.Cast<DataGridViewCell>()
                .FirstOrDefault(c => c.OwningColumn.Tag is ColumnaPersonalizada col && col.NombreInterno == "Tipo");

            // Obtener la columna que se está editando
            var colActual = dgvPresupuesto.Columns[e.ColumnIndex];
            if (!(colActual.Tag is ColumnaPersonalizada colDef)) return;

            string nombreInterno = colDef.NombreInterno;

            // Si NO hay Tipo seleccionado, solo permitir editar Tipo
            if (tipoCell?.Value == null || string.IsNullOrWhiteSpace(tipoCell.Value.ToString()))
            {
                if (nombreInterno != "Tipo")
                {
                    e.Cancel = true;
                    MessageBox.Show("Primero debe seleccionar un Tipo.", "Campo Requerido",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            string tipo = tipoCell.Value.ToString();

            bool cancelarEdicion = false;

            if (tipo == "Capitulo" || tipo == "Subcapitulo" || tipo == "Nivel 1" || tipo == "Nivel 2" || tipo == "Nivel 3")
            {
                // AGRUPADORES: Solo pueden editar Tipo, Clave, Descripción
                if (nombreInterno != "Tipo" && nombreInterno != "Clave" && nombreInterno != "Descripcion")
                {
                    cancelarEdicion = true;
                }
            }
            else if (tipo == "Concepto")
            {
                // CONCEPTOS: No pueden editar P.U. e Importe (calculados)
                if (nombreInterno == "PrecioUnitario" || nombreInterno == "Importe")
                {
                    cancelarEdicion = true;
                }

                // Si no tiene clave, solo puede editar Tipo y Clave
                var claveCell = dgvPresupuesto.Rows[e.RowIndex].Cells.Cast<DataGridViewCell>()
                    .FirstOrDefault(c => c.OwningColumn.Tag is ColumnaPersonalizada col && col.NombreInterno == "Clave");

                if ((claveCell?.Value == null || string.IsNullOrWhiteSpace(claveCell.Value.ToString())) &&
                    nombreInterno != "Tipo" && nombreInterno != "Clave" && nombreInterno != "Descripcion")
                {
                    cancelarEdicion = true;
                    MessageBox.Show("Primero debe asignar una Clave al concepto.", "Campo Requerido",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            if (cancelarEdicion)
            {
                e.Cancel = true;

                // Marcar visualmente que no es editable
                dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.FromArgb(245, 245, 245);

                BeginInvoke(new Action(() =>
                {
                    dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Style.BackColor = Color.White;
                }));
            }
        }

        private int ObtenerUltimaFilaConDatos()
        {
            for (int i = dgvPresupuesto.Rows.Count - 1; i >= 0; i--)
            {
                var tipoCell = ObtenerCeldaPorNombreInterno(i, "Tipo");
                var descCell = ObtenerCeldaPorNombreInterno(i, "Descripcion");
                var claveCell = ObtenerCeldaPorNombreInterno(i, "Clave");

                if ((tipoCell != null && !string.IsNullOrWhiteSpace(tipoCell.Value?.ToString()))
                    || (descCell != null && !string.IsNullOrWhiteSpace(descCell.Value?.ToString()))
                    || (claveCell != null && !string.IsNullOrWhiteSpace(claveCell.Value?.ToString())))
                {
                    return i;
                }
            }

            return -1;
        }

        private int ObtenerPrimeraFilaVacia()
        {
            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var tipoCell = ObtenerCeldaPorNombreInterno(i, "Tipo");
                var descCell = ObtenerCeldaPorNombreInterno(i, "Descripcion");

                // Fila vacía = no tiene tipo ni descripción
                if ((tipoCell == null || tipoCell.Value == null || string.IsNullOrWhiteSpace(tipoCell.Value.ToString())) &&
                    (descCell == null || descCell.Value == null || string.IsNullOrWhiteSpace(descCell.Value.ToString())))
                {
                    return i;
                }
            }
            return dgvPresupuesto.Rows.Count; // Todas están llenas
        }

        private List<SOPRO.Application.Models.Presupuesto.BudgetHierarchyRow> BuildHierarchyRowsSnapshot()
        {
            var rows = new List<SOPRO.Application.Models.Presupuesto.BudgetHierarchyRow>();

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                string tipo = ObtenerCeldaTexto(i, "Tipo");
                string clave = ObtenerCeldaTexto(i, "Clave");
                string descripcion = ObtenerCeldaTexto(i, "Descripcion");
                decimal importe = ObtenerCeldaDecimal(i, "Importe");
                var concepto = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;

                bool hasContent = !string.IsNullOrWhiteSpace(tipo)
                    || !string.IsNullOrWhiteSpace(clave)
                    || !string.IsNullOrWhiteSpace(descripcion)
                    || concepto != null;

                rows.Add(new SOPRO.Application.Models.Presupuesto.BudgetHierarchyRow
                {
                    RowIndex = i,
                    Tipo = string.IsNullOrWhiteSpace(tipo) ? "Concepto" : tipo,
                    Importe = importe,
                    HasContent = hasContent,
                    IsConcept = string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase)
                        || (string.IsNullOrWhiteSpace(tipo) && concepto is { EsAgrupador: false })
                });
            }

            return rows;
        }

        private void RemoveIntermediateEmptyRows()
        {
            var rows = BuildHierarchyRowsSnapshot();
            var indexes = BudgetHierarchyService.GetIntermediateEmptyRowIndexes(rows);
            for (int i = indexes.Count - 1; i >= 0; i--)
            {
                dgvPresupuesto.Rows.RemoveAt(indexes[i]);
            }
        }

        /// <summary>
        /// Obtiene una celda por el nombre interno de su columna
        /// </summary>
        private DataGridViewCell ObtenerCeldaPorNombreInterno(int rowIndex, string nombreInterno)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count) return null;

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef && colDef.NombreInterno == nombreInterno)
                {
                    return dgvPresupuesto.Rows[rowIndex].Cells[col.Index];
                }
            }
            return null;
        }

        /// <summary>
        /// Obtiene el índice de una columna por su nombre interno
        /// </summary>
        private int ObtenerIndiceColumnaPorNombreInterno(string nombreInterno)
        {
            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef && colDef.NombreInterno == nombreInterno)
                {
                    return col.Index;
                }
            }
            return -1;
        }

        private void RecargarPresupuestoPreservandoEstado()
        {
            var state = DataGridViewStateHelper.Capture(dgvPresupuesto);
            CargarPresupuesto(state);
        }

        private void CargarPresupuesto(DataGridViewStateSnapshot? stateToRestore = null)
        {
            try
            {
                _cargando = true;

                var loadState = BudgetLoadService.BuildLoadState(_context, _proyecto);
                CargarColumnasPersonalizadas(loadState);

                dgvPresupuesto.Rows.Clear();

                foreach (var rowDisplay in loadState.RowDisplays)
                {
                    AgregarFilaConcepto(rowDisplay);
                }

                for (int i = 0; i < loadState.EmptyRowsToAppend; i++)
                {
                    dgvPresupuesto.Rows.Add();
                }

                RecalcularTodosLosTotales();
                GuardarCambios();       // Persistir totales de agrupadores recalculados en BD
                ReasignarNumerosConceptos();
                ActualizarEstadisticas();

                if (stateToRestore != null && dgvPresupuesto.Rows.Count > 0)
                {
                    DataGridViewStateHelper.Restore(dgvPresupuesto, stateToRestore);
                }

                _cargando = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar presupuesto:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _cargando = false;
            }
        }

        private void CargarColumnasPersonalizadas(SOPRO.Application.Models.Presupuesto.BudgetGridLoadState? loadState = null)
        {
            var columnasAEliminar = dgvPresupuesto.Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Name != "colId" && c.Name != "colNumero")
                .ToList();

            foreach (var col in columnasAEliminar)
            {
                dgvPresupuesto.Columns.Remove(col);
            }

            loadState ??= BudgetLoadService.BuildLoadState(_context, _proyecto);

            foreach (var definition in loadState.ColumnDefinitions)
            {
                DataGridViewColumn nuevaColumna;

                if (definition.IsFillColumn)
                {
                    nuevaColumna = new DataGridViewTextBoxColumn
                    {
                        Name = definition.Name,
                        HeaderText = definition.HeaderText,
                        ReadOnly = true,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 250, 250) }
                    };
                    dgvPresupuesto.Columns.Add(nuevaColumna);
                    continue;
                }

                if (definition.IsTypeSelector)
                {
                    var comboCol = new DataGridViewComboBoxColumn
                    {
                        Name = definition.Name,
                        HeaderText = definition.HeaderText,
                        Width = definition.Width,
                        DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                        Tag = definition.SourceColumn,
                        Visible = definition.IsVisible,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        ReadOnly = definition.IsReadOnly
                    };
                    comboCol.Items.AddRange(new object[] { "Capitulo", "Subcapitulo", "Nivel 1", "Nivel 2", "Nivel 3", "Concepto" });
                    nuevaColumna = comboCol;
                }
                else
                {
                    nuevaColumna = definition.TipoDato == TipoDatoColumna.Booleano
                        ? new DataGridViewCheckBoxColumn()
                        : new DataGridViewTextBoxColumn();

                    nuevaColumna.Name = definition.Name;
                    nuevaColumna.HeaderText = definition.HeaderText;
                    nuevaColumna.Width = definition.Width;
                    nuevaColumna.Tag = definition.SourceColumn;
                    nuevaColumna.Visible = definition.IsVisible;
                    nuevaColumna.SortMode = DataGridViewColumnSortMode.NotSortable;
                    nuevaColumna.ReadOnly = definition.IsReadOnly;

                    if (definition.AlignRight)
                    {
                        nuevaColumna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    else if (definition.AlignCenter)
                    {
                        nuevaColumna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }

                    if (definition.UseCalculatedBackColor)
                    {
                        nuevaColumna.DefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
                    }

                    if (definition.SourceColumn != null && string.Equals(definition.SourceColumn.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
                    {
                        nuevaColumna.ValueType = typeof(decimal);
                        nuevaColumna.DefaultCellStyle.Format = $"N{_proyecto.DecimalesCantidad}";
                    }
                }

                dgvPresupuesto.Columns.Add(nuevaColumna);
            }

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef)
                {
                    int displayIndex = colDef.Orden;
                    if (displayIndex >= 0 && displayIndex < dgvPresupuesto.Columns.Count)
                    {
                        col.DisplayIndex = displayIndex;
                    }
                }
            }

            dgvPresupuesto.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvPresupuesto.ScrollBars = ScrollBars.Both;

            if (dgvPresupuesto.Columns["colNumero"] != null)
                dgvPresupuesto.Columns["colNumero"].Width = 40;

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada)
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }
            }

            FormatoHelper.AplicarWrapYAlineacionPersistidos(dgvPresupuesto);
            FormatoHelper.AjustarAutoAlturaFilas(dgvPresupuesto);
        }

        private void AgregarFilaConcepto(SOPRO.Application.Models.Presupuesto.BudgetGridRowDisplay rowDisplay)
        {
            var row = dgvPresupuesto.Rows[dgvPresupuesto.Rows.Add()];
            row.Tag = rowDisplay.Concepto;

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is not ColumnaPersonalizada colDef) continue;

                if (rowDisplay.ValuesByInternalName.TryGetValue(colDef.NombreInterno, out var valor) && valor != null)
                {
                    row.Cells[col.Index].Value = valor;
                }
            }
        }

        private void DgvPresupuesto_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (!dgvPresupuesto.IsCurrentCellDirty || dgvPresupuesto.CurrentCell == null)
                return;

            var cell = dgvPresupuesto.CurrentCell;
            var col = dgvPresupuesto.Columns[cell.ColumnIndex];

            bool requiereCommitInmediato = col is DataGridViewCheckBoxColumn || col is DataGridViewComboBoxColumn;
            if (!requiereCommitInmediato)
                return;

            dgvPresupuesto.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void DgvPresupuesto_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            MessageBox.Show("El valor capturado no tiene un formato válido.", "Presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void DgvPresupuesto_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var tipoCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Tipo");
            if (tipoCell?.Value == null) return;

            string tipo = tipoCell.Value.ToString();

            // ── 1. Formato por tipo de renglón (agrupadores) ──────────────────
            Color backColor = Color.White;
            Color foreColor = Color.Black;
            FontStyle fontStyle = FontStyle.Regular;

            switch (tipo)
            {
                case "Capitulo":
                    backColor = Color.Purple;
                    foreColor = Color.White;
                    fontStyle = FontStyle.Bold;
                    break;
                case "Subcapitulo":
                    backColor = Color.LightBlue;
                    foreColor = Color.Black;
                    fontStyle = FontStyle.Bold;
                    break;
                case "Nivel 1":
                case "Nivel 2":
                case "Nivel 3":
                    backColor = Color.LightGray;
                    foreColor = Color.Black;
                    fontStyle = FontStyle.Bold;
                    break;
                case "Concepto":
                    backColor = Color.White;
                    foreColor = Color.Black;
                    fontStyle = FontStyle.Regular;
                    break;
            }

            // Aplicar el estilo de renglón base
            e.CellStyle.BackColor = backColor;
            e.CellStyle.ForeColor = foreColor;
            e.CellStyle.Font = new Font(dgvPresupuesto.Font, fontStyle);

            // ── 2. Sobreponer formato de columna (solo para conceptos) ─────────
            // Para agrupadores mantenemos su color de fondo, pero sí aplicamos
            // fuente y alineación de la columna si está definida
            var col = dgvPresupuesto.Columns[e.ColumnIndex];
            if (col.Tag is ColumnaPersonalizada colDef)
            {
                // Alineación siempre se aplica
                e.CellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(colDef.Alineacion, colDef.AlineacionVertical);
                e.CellStyle.WrapMode = colDef.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;

                // Para conceptos: aplicar color de fondo y fuente de la columna
                if (tipo == "Concepto")
                {
                    // Color de fondo de la columna (si no es blanco puro, tiene precedencia)
                    if (!string.IsNullOrEmpty(colDef.ColorFondo))
                    {
                        try { e.CellStyle.BackColor = ColorTranslator.FromHtml(colDef.ColorFondo); }
                        catch { }
                    }

                    // Color de fuente de la columna
                    if (!string.IsNullOrEmpty(colDef.ColorFuente))
                    {
                        try { e.CellStyle.ForeColor = ColorTranslator.FromHtml(colDef.ColorFuente); }
                        catch { }
                    }

                    // Fuente de la columna
                    try
                    {
                        string nombreFuente = !string.IsNullOrEmpty(colDef.NombreFuente)
                            ? colDef.NombreFuente : dgvPresupuesto.Font.Name;
                        float tamaño = colDef.TamanoFuente > 0 ? colDef.TamanoFuente : dgvPresupuesto.Font.Size;
                        FontStyle fs = (colDef.Negrita ? FontStyle.Bold : FontStyle.Regular)
                                     | (colDef.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                        e.CellStyle.Font = new Font(nombreFuente, tamaño, fs);
                    }
                    catch { }
                }
                else
                {
                    // Para agrupadores: mantener su fondo pero aplicar fuente de columna
                    try
                    {
                        string nombreFuente = !string.IsNullOrEmpty(colDef.NombreFuente)
                            ? colDef.NombreFuente : dgvPresupuesto.Font.Name;
                        float tamaño = colDef.TamanoFuente > 0 ? colDef.TamanoFuente : dgvPresupuesto.Font.Size;
                        // Para agrupadores siempre negrita + configuración de columna
                        FontStyle fs = FontStyle.Bold
                                     | (colDef.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                        e.CellStyle.Font = new Font(nombreFuente, tamaño, fs);
                    }
                    catch { }
                }
            }
        }

        private void DgvPresupuesto_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_cargando || _asignandoMatriz || e.RowIndex < 0) return;

            var colActual = dgvPresupuesto.Columns[e.ColumnIndex];
            if (!(colActual.Tag is ColumnaPersonalizada colDef)) return;

            // Si cambió el tipo → CREAR/ACTUALIZAR CONCEPTO INMEDIATAMENTE
            if (colDef.NombreInterno == "Tipo")
            {
                var tipoCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Tipo");
                if (tipoCell?.Value == null) return;

                string tipo = tipoCell.Value.ToString()?.Trim() ?? string.Empty;
                var row = dgvPresupuesto.Rows[e.RowIndex];

                var typeResult = BudgetRowEditFlowService.HandleTypeCellChange(
                    _context,
                    _proyecto,
                    new SOPRO.Application.Models.Presupuesto.BudgetRowTypeChangeInput
                    {
                        RowIndex = e.RowIndex,
                        Tipo = tipo,
                        Descripcion = ObtenerCeldaPorNombreInterno(e.RowIndex, "Descripcion")?.Value?.ToString() ?? string.Empty,
                        Clave = ObtenerCeldaPorNombreInterno(e.RowIndex, "Clave")?.Value?.ToString() ?? string.Empty,
                        Unidad = ObtenerCeldaPorNombreInterno(e.RowIndex, "Unidad")?.Value?.ToString() ?? string.Empty,
                        ExistingConcept = row.Tag as ConceptoPresupuesto
                    });

                if (typeResult.Handled)
                {
                    row.Tag = typeResult.Concept;

                    var unidadCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Unidad");
                    var cantidadCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Cantidad");
                    var puCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "PrecioUnitario");
                    var importeCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Importe");

                    if (unidadCell != null)
                    {
                        unidadCell.ReadOnly = typeResult.UnitReadOnly;
                        if (typeResult.IsAggregator) unidadCell.Value = typeResult.UnitValue;
                    }

                    if (cantidadCell != null)
                    {
                        cantidadCell.ReadOnly = typeResult.QuantityReadOnly;
                        if (typeResult.IsAggregator) cantidadCell.Value = typeResult.QuantityValue;
                    }

                    if (typeResult.ClearCalculatedCells)
                    {
                        if (puCell != null) puCell.Value = typeResult.UnitPriceValue;
                        if (importeCell != null) importeCell.Value = typeResult.AmountValue;
                    }

                    if (typeResult.RequiresRowInvalidate)
                        dgvPresupuesto.InvalidateRow(e.RowIndex);

                    if (typeResult.RequiresReassignSequence)
                        ReasignarNumerosConceptos();

                    if (typeResult.IgnoredBecauseStateIsAlreadyCorrect)
                        return;
                }
            }

            // Si cambió cantidad en un concepto
            if (colDef.NombreInterno == "Cantidad")
            {
                var row = dgvPresupuesto.Rows[e.RowIndex];
                var quantityResult = BudgetRowEditFlowService.HandleQuantityCellChange(
                    _proyecto,
                    row.Tag as ConceptoPresupuesto,
                    ObtenerCeldaPorNombreInterno(e.RowIndex, "Cantidad")?.Value?.ToString());

                if (quantityResult.HasChanges && row.Tag is ConceptoPresupuesto concepto)
                {
                    var importeCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Importe");
                    var subtotalCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Subtotal");
                    var ivaCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "IVA");
                    var totalCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "Total");
                    var puLetraCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "PrecioUnitarioLetra");
                    var totalLetraCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "TotalLetra");
                    var puCell = ObtenerCeldaPorNombreInterno(e.RowIndex, "PrecioUnitario");

                    if (puCell != null) puCell.Value = quantityResult.PrecioUnitario.ToStringImporte();
                    if (importeCell != null) importeCell.Value = quantityResult.Importe.ToStringImporte();
                    if (subtotalCell != null) subtotalCell.Value = quantityResult.Subtotal.ToStringImporte();
                    if (ivaCell != null) ivaCell.Value = quantityResult.Iva.ToStringImporte();
                    if (totalCell != null) totalCell.Value = quantityResult.Total.ToStringImporte();
                    if (puLetraCell != null) puLetraCell.Value = quantityResult.PrecioUnitarioLetra;
                    if (totalLetraCell != null) totalLetraCell.Value = quantityResult.TotalLetra;

                    concepto.Cantidad = quantityResult.Cantidad;
                    concepto.PrecioUnitario = quantityResult.PrecioUnitario;
                    concepto.ImporteTotal = quantityResult.Importe;
                    concepto.CostoDirectoTotal = new MotorCalculoSopro(_proyecto).Multiplicar(
                        quantityResult.Cantidad, concepto.CostoDirectoUnitario);

                    // Actualizar totales de padres
                    ActualizarTotalesJerarquia(e.RowIndex);
                }
            }

            if (colDef.NombreInterno == "Clave")
            {
                if (!TryApplyBudgetConceptAssignmentByKey(e.RowIndex))
                    return;
            }

            GuardarCambios();

            // Actualizar estadísticas solo si cambió algo relevante
            if (colDef.NombreInterno == "Tipo" ||
                colDef.NombreInterno == "Cantidad" ||
                colDef.NombreInterno == "PrecioUnitario" ||
                colDef.NombreInterno == "Importe" ||
                colDef.NombreInterno == "Descripcion" ||
                colDef.NombreInterno == "Clave")
            {
                ActualizarEstadisticas();
            }
        }

        private void ActualizarTotalesJerarquia(int filaConcepto)
        {
            var rows = BuildHierarchyRowsSnapshot();
            if (filaConcepto < 0 || filaConcepto >= rows.Count || !rows[filaConcepto].IsConcept) return;

            foreach (var indicePadre in BudgetHierarchyService.GetAncestorIndexes(rows, filaConcepto))
            {
                ActualizarTotalAgrupador(indicePadre, rows);
            }
        }

        private void ActualizarTotalAgrupador(int filaAgrupador)
        {
            ActualizarTotalAgrupador(filaAgrupador, null);
        }

        private void ActualizarTotalAgrupador(int filaAgrupador, List<SOPRO.Application.Models.Presupuesto.BudgetHierarchyRow>? snapshot)
        {
            snapshot ??= BuildHierarchyRowsSnapshot();
            if (filaAgrupador < 0 || filaAgrupador >= snapshot.Count) return;
            if (snapshot[filaAgrupador].IsConcept) return;

            decimal totalAgrupador = BudgetHierarchyService.CalculateAggregatorTotal(snapshot, filaAgrupador);

            // Actualizar celda del grid
            var importeAgrupadorCell = ObtenerCeldaPorNombreInterno(filaAgrupador, "Importe");
            if (importeAgrupadorCell != null)
            {
                importeAgrupadorCell.Value = totalAgrupador.ToStringImporte();
            }

            // CRÍTICO: actualizar también la entidad en memoria para que
            // ConstruirFilasPresupuesto → ApplyAutoSaveChanges persista el total en BD.
            // Sin esto, los agrupadores siempre muestran $0 al recargar.
            var concepto = dgvPresupuesto.Rows[filaAgrupador].Tag as ConceptoPresupuesto;
            if (concepto != null)
            {
                concepto.CostoDirectoTotal = totalAgrupador;
                concepto.ImporteTotal = totalAgrupador;
            }
        }

        private int ObtenerIndicePadre(int fila)
        {
            return BudgetHierarchyService.FindParentIndex(BuildHierarchyRowsSnapshot(), fila);
        }

        private void RecalcularTodosLosTotales()
        {
            var snapshot = BuildHierarchyRowsSnapshot();
            var totals = BudgetHierarchyService.CalculateAggregatorTotals(snapshot);

            foreach (var pair in totals)
            {
                var importeCell = ObtenerCeldaPorNombreInterno(pair.Key, "Importe");
                if (importeCell != null)
                {
                    importeCell.Value = pair.Value.ToStringImporte();
                }

                // CRÍTICO: sincronizar la entidad en memoria para que auto-guardado persista el total
                var concepto = dgvPresupuesto.Rows[pair.Key].Tag as ConceptoPresupuesto;
                if (concepto != null)
                {
                    concepto.CostoDirectoTotal = pair.Value;
                    concepto.ImporteTotal = pair.Value;
                }
            }

            ActualizarEstadisticas();
        }

        /// <summary>
        /// Asigna números consecutivos solo a los conceptos (no agrupadores)
        /// según el orden en que aparecen en el grid.
        /// También guarda el Orden en BD para que la navegación ▲▼ del panel sea correcta.
        /// </summary>
        public void ReasignarNumerosConceptos()
        {
            if (!dgvPresupuesto.Columns.Contains("colNumero")) return;

            var snapshot = BuildHierarchyRowsSnapshot();
            var sequenceMap = BudgetHierarchyService.BuildConceptSequenceMap(snapshot);

            foreach (var row in snapshot)
            {
                dgvPresupuesto.Rows[row.RowIndex].Cells["colNumero"].Value = sequenceMap[row.RowIndex]?.ToString() ?? string.Empty;
            }
        }

        private void DgvPresupuesto_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var colClickeada = dgvPresupuesto.Columns[e.ColumnIndex];
            var interaction = BudgetGridInteractionService.HandleCellDoubleClick(
                e.RowIndex,
                e.ColumnIndex,
                colClickeada.ReadOnly,
                colClickeada.Name,
                (colClickeada.Tag as ColumnaPersonalizada)?.NombreInterno,
                ObtenerCeldaTexto(e.RowIndex, "Tipo"));

            if (interaction.ShouldOpenApuSelector)
            {
                OpenApuSelectorForRow(e.RowIndex);
                return;
            }

            if (interaction.ShouldBeginEdit)
            {
                dgvPresupuesto.BeginEdit(true);
            }
        }

        private void DgvPresupuesto_KeyDown(object sender, KeyEventArgs e)
        {

            if (dgvPresupuesto.CurrentCell == null) return;


            int rowIndex = dgvPresupuesto.CurrentCell.RowIndex;
            var currentColumn = dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex];
            string? currentInternalName = (currentColumn.Tag as ColumnaPersonalizada)?.NombreInterno;
            string currentTipo = ObtenerCeldaTexto(rowIndex, "Tipo");

            if (e.Control && e.KeyCode == Keys.V)
            {
                // Si el foco real está en el panel embebido o en un TextBox de edición,
                // dejar que el control activo procese el pegado nativo.
                if ((_panelMatricesEmbebido != null
                     && _panelMatricesEmbebido.Visible
                     && _panelMatricesEmbebido.ContainsFocus
                     && _panelMatricesEmbebido.TieneFocoEnEntradaTexto())
                    || (dgvPresupuesto.IsCurrentCellInEditMode && dgvPresupuesto.EditingControl is TextBox))
                {
                    return;
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
                HandleClipboardPaste();
                return;
            }

            if (e.KeyCode == Keys.F2)
            {
                var interaction = BudgetGridInteractionService.HandleF2(rowIndex, currentTipo, currentInternalName);
                e.Handled = interaction.Handled;
                e.SuppressKeyPress = interaction.SuppressKeyPress;

                if (interaction.ShouldOpenApuSelector)
                {
                    OpenApuSelectorForRow(rowIndex);
                }
                return;
            }

            if (e.KeyCode == Keys.Insert && !e.Control && !e.Shift)
            {
                var interaction = BudgetGridInteractionService.HandleInsert(rowIndex, ObtenerPrimeraFilaVacia(), ObtenerUltimaFilaConDatos());
                e.Handled = interaction.Handled;
                e.SuppressKeyPress = interaction.SuppressKeyPress;

                if (interaction.FocusRowIndex.HasValue && !interaction.ShouldInsertConceptRow)
                {
                    var focusCell = ObtenerCeldaPorNombreInterno(interaction.FocusRowIndex.Value, "Tipo");
                    if (focusCell != null)
                    {
                        dgvPresupuesto.CurrentCell = focusCell;
                    }
                    return;
                }

                if (interaction.ShouldInsertConceptRow && interaction.InsertRowIndex.HasValue)
                {
                    int insertIndex = interaction.InsertRowIndex.Value;
                    dgvPresupuesto.Rows.Insert(insertIndex);
                    var nuevaTipoCell = ObtenerCeldaPorNombreInterno(insertIndex, "Tipo");
                    if (nuevaTipoCell != null)
                    {
                        nuevaTipoCell.Value = "Concepto";
                        dgvPresupuesto.CurrentCell = nuevaTipoCell;
                    }
                }
                return;
            }

            if (e.KeyCode == Keys.Delete && e.Control)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                DeleteCurrentSelectionFromBudget();
                return;
            }
        }

        private void DgvPresupuesto_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvPresupuesto.Rows[e.RowIndex];
            if (!clickedRow.Selected)
            {
                dgvPresupuesto.ClearSelection();
                clickedRow.Selected = true;
            }

            if (e.ColumnIndex >= 0)
                dgvPresupuesto.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _menuPresupuesto?.Dispose();
            _menuPresupuesto = new ContextMenuStrip();

            var itemCopiarCelda = _menuPresupuesto.Items.Add("📋  Copiar celda");
            itemCopiarCelda.Click += (_, __) =>
            {
                var cell = dgvPresupuesto.CurrentCell;
                if (cell != null)
                {
                    string txt = cell.FormattedValue?.ToString() ?? "";
                    if (txt.Length > 0) Clipboard.SetText(txt);
                }
            };

            int n = dgvPresupuesto.SelectedRows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow);
            string labelCopiarFila = n > 1 ? $"📄  Copiar {n} filas" : "📄  Copiar fila";
            var itemCopiarFila = _menuPresupuesto.Items.Add(labelCopiarFila);
            itemCopiarFila.Click += (_, __) =>
            {
                var cols = Enumerable.Range(0, dgvPresupuesto.ColumnCount)
                    .Where(i => dgvPresupuesto.Columns[i].Visible)
                    .OrderBy(i => dgvPresupuesto.Columns[i].DisplayIndex)
                    .ToList();
                var sb = new System.Text.StringBuilder();
                foreach (DataGridViewRow r in dgvPresupuesto.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
                    sb.AppendLine(string.Join("	", cols.Select(i => r.Cells[i].FormattedValue?.ToString() ?? "")));
                if (sb.Length > 0) Clipboard.SetText(sb.ToString().TrimEnd());
            };

            _menuPresupuesto.Items.Add(new ToolStripSeparator());

            var itemEliminar = _menuPresupuesto.Items.Add(n > 1 ? $"🗑️  Eliminar {n} filas" : "🗑️  Eliminar");
            itemEliminar.Click += (_, __) => DeleteCurrentSelectionFromBudget();
            _menuPresupuesto.Show(Cursor.Position);
        }

        private void DeleteCurrentSelectionFromBudget()
        {
            if (dgvPresupuesto.CurrentCell == null) return;

            if (TryDeleteMultipleSelectedConceptRows())
                return;

            int rowIndex = dgvPresupuesto.CurrentCell.RowIndex;
            bool rowHasContent = !string.IsNullOrWhiteSpace(ObtenerCeldaTexto(rowIndex, "Tipo"))
                || !string.IsNullOrWhiteSpace(ObtenerCeldaTexto(rowIndex, "Descripcion"));

            var interaction = BudgetGridInteractionService.BuildDeletionPlan(rowIndex, rowHasContent, BuildHierarchyRowsSnapshot());
            if (!interaction.HasDeletionPlan)
                return;

            var result = MessageBox.Show(
                interaction.ConfirmationMessage,
                "Confirmar Eliminación Jerárquica",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            for (int i = interaction.RowsToDelete.Count - 1; i >= 0; i--)
            {
                int idx = interaction.RowsToDelete[i];
                var conceptoTag = dgvPresupuesto.Rows[idx].Tag as ConceptoPresupuesto;
                if (conceptoTag != null && conceptoTag.Id > 0)
                {
                    var enBD = _context.ConceptosPresupuesto.Find(conceptoTag.Id);
                    if (enBD != null) _context.ConceptosPresupuesto.Remove(enBD);
                }
                dgvPresupuesto.Rows.RemoveAt(idx);
            }

            _context.SaveChanges();
            ReasignarNumerosConceptos();
            RecalcularTodosLosTotales();
            GuardarCambios();       // Persistir totales de agrupadores en BD
            ActualizarEstadisticas();
            dgvPresupuesto.Refresh();
        }

        private bool TryDeleteMultipleSelectedConceptRows()
        {
            if (dgvPresupuesto.SelectedRows.Count <= 1)
                return false;

            var selectedIndexes = dgvPresupuesto.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .Select(r => r.Index)
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            if (selectedIndexes.Count <= 1)
                return false;

            var snapshot = BuildHierarchyRowsSnapshot();
            var selectedRows = snapshot.Where(r => selectedIndexes.Contains(r.RowIndex)).ToList();
            if (selectedRows.Count != selectedIndexes.Count)
                return false;

            bool allAreConcepts = selectedRows.All(r => string.Equals(r.Tipo, "Concepto", StringComparison.OrdinalIgnoreCase));
            if (!allAreConcepts)
            {
                MessageBox.Show(
                    "La eliminación múltiple con Ctrl + Supr solo está disponible cuando todas las filas seleccionadas son conceptos.\n\nPara agrupadores, use la eliminación jerárquica normal sobre una sola fila.",
                    "Selección no compatible",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return true;
            }

            var result = MessageBox.Show(
                $"¿Desea eliminar los {selectedIndexes.Count} conceptos seleccionados?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación Múltiple",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return true;

            for (int i = selectedIndexes.Count - 1; i >= 0; i--)
            {
                int idx = selectedIndexes[i];
                var conceptoTag = dgvPresupuesto.Rows[idx].Tag as ConceptoPresupuesto;
                if (conceptoTag != null && conceptoTag.Id > 0)
                {
                    var enBD = _context.ConceptosPresupuesto.Find(conceptoTag.Id);
                    if (enBD != null) _context.ConceptosPresupuesto.Remove(enBD);
                }
                dgvPresupuesto.Rows.RemoveAt(idx);
            }

            _context.SaveChanges();
            ReasignarNumerosConceptos();
            RecalcularTodosLosTotales();
            GuardarCambios();       // Persistir totales de agrupadores en BD
            ActualizarEstadisticas();
            dgvPresupuesto.Refresh();
            return true;
        }

        private int ObtenerNivelIndentacion(int rowIndex)
        {
            return BudgetHierarchyService.GetIndentLevel(ObtenerCeldaTexto(rowIndex, "Tipo"));
        }

        /// <summary>
        /// Comportamiento estilo Excel: empezar a escribir activa edición automática.
        /// </summary>
        private void DgvPresupuesto_KeyPress(object sender, KeyPressEventArgs e)
        {
            bool shouldStartEdit = BudgetGridInteractionService.ShouldStartTypingEdit(
                dgvPresupuesto.CurrentCell != null,
                dgvPresupuesto.CurrentCell != null && dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex].ReadOnly,
                dgvPresupuesto.CurrentCell != null ? dgvPresupuesto.Columns[dgvPresupuesto.CurrentCell.ColumnIndex].Name : string.Empty,
                e.KeyChar,
                dgvPresupuesto.IsCurrentCellInEditMode);

            if (!shouldStartEdit) return;

            dgvPresupuesto.BeginEdit(true);

            if (dgvPresupuesto.EditingControl is TextBox txt)
            {
                txt.Text = e.KeyChar.ToString();
                txt.SelectionStart = 1;
            }

            e.Handled = true;
        }

        private void GuardarCambios()
        {
            try
            {
                var rows = ConstruirFilasPresupuesto(includeOnlyExisting: true);
                BudgetPersistenceService.ApplyAutoSaveChanges(_context, rows);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-guardado: {ex.Message}");
            }
        }

        private List<BudgetConceptRowDto> ConstruirFilasPresupuesto(bool includeOnlyExisting = false)
        {
            var rows = new List<BudgetConceptRowDto>();

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var row = dgvPresupuesto.Rows[i];
                var concepto = row.Tag as ConceptoPresupuesto;
                if (includeOnlyExisting && concepto == null) continue;

                var tipo = ObtenerCeldaTexto(row.Index, "Tipo", "Concepto");
                var descripcion = ObtenerCeldaTexto(row.Index, "Descripcion");
                if (!includeOnlyExisting && string.IsNullOrWhiteSpace(descripcion)) continue;

                var dto = new BudgetConceptRowDto
                {
                    ExistingConceptId = concepto?.Id > 0 ? concepto.Id : null,
                    Tipo = tipo,
                    Clave = ObtenerCeldaTexto(row.Index, "Clave"),
                    Descripcion = descripcion,
                    Unidad = ObtenerCeldaTexto(row.Index, "Unidad"),
                    Cantidad = string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase)
                        ? ObtenerCeldaDecimal(row.Index, "Cantidad")
                        : 0m,
                    MatrizId = concepto?.MatrizId,
                    CostoDirectoUnitario = concepto?.CostoDirectoUnitario ?? 0m,
                    CostoDirectoTotal = concepto?.CostoDirectoTotal ?? 0m,
                    PrecioUnitario = concepto?.PrecioUnitario ?? ObtenerCeldaDecimal(row.Index, "PrecioUnitario"),
                    ImporteTotal = concepto?.ImporteTotal ?? ObtenerCeldaDecimal(row.Index, "Importe"),
                    Orden = i
                };

                rows.Add(dto);
            }

            return rows;
        }

        private string ObtenerCeldaTexto(int rowIndex, string nombreInterno, string defaultValue = "")
        {
            return ObtenerCeldaPorNombreInterno(rowIndex, nombreInterno)?.Value?.ToString() ?? defaultValue;
        }

        private decimal ObtenerCeldaDecimal(int rowIndex, string nombreInterno)
        {
            var cell = ObtenerCeldaPorNombreInterno(rowIndex, nombreInterno);
            if (cell?.Value == null) return 0m;

            if (cell.Value is decimal dec)
                return dec;

            if (cell.Value is int i)
                return i;

            if (cell.Value is double d)
                return (decimal)d;

            var value = cell.Value.ToString();
            if (string.IsNullOrWhiteSpace(value)) return 0m;

            string limpio = value.Replace("$", string.Empty).Trim();
            if (decimal.TryParse(limpio, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal result))
                return result;

            decimal.TryParse(limpio.Replace(",", string.Empty), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result);
            return result;
        }

        private void ActualizarEstadisticas()
        {
            // Contar conceptos directamente del grid (no de BD)
            int totalConceptos = 0;
            decimal totalImporte = 0;

            foreach (DataGridViewRow row in dgvPresupuesto.Rows)
            {
                var tipoCell = ObtenerCeldaPorNombreInterno(row.Index, "Tipo");
                if (tipoCell?.Value?.ToString() == "Concepto")
                {
                    totalConceptos++;

                    // Sumar importe
                    var importeCell = ObtenerCeldaPorNombreInterno(row.Index, "Importe");
                    if (importeCell?.Value != null)
                    {
                        // Limpiar formato de moneda ($, comas, etc.)
                        string valorStr = importeCell.Value.ToString()
                            .Replace("$", "")
                            .Replace(",", "")
                            .Trim();

                        if (decimal.TryParse(valorStr, out decimal importe))
                        {
                            totalImporte += importe;
                        }
                    }
                }
            }

            lblTotalConceptos.Text = $"{totalConceptos} conceptos";
            lblCostoDirecto.Text = totalImporte.ToStringImporte();
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                // Limpiar conceptos existentes
                var conceptosExistentes = _context.ConceptosPresupuesto.Where(c => c.ProyectoId == _proyecto.Id);
                _context.ConceptosPresupuesto.RemoveRange(conceptosExistentes);

                int orden = 0;
                foreach (DataGridViewRow row in dgvPresupuesto.Rows)
                {
                    var tipoCell = ObtenerCeldaPorNombreInterno(row.Index, "Tipo");
                    if (tipoCell?.Value == null) continue;

                    string tipo = tipoCell.Value.ToString();

                    var claveCell = ObtenerCeldaPorNombreInterno(row.Index, "Clave");
                    var descCell = ObtenerCeldaPorNombreInterno(row.Index, "Descripcion");

                    string clave = claveCell?.Value?.ToString() ?? "";
                    string desc = descCell?.Value?.ToString() ?? "";

                    if (string.IsNullOrWhiteSpace(desc)) continue;

                    bool esAgrupador = tipo != "Concepto";
                    int nivel = ObtenerNivelDesdeTipo(tipo);

                    var unidadCell = ObtenerCeldaPorNombreInterno(row.Index, "Unidad");

                    var concepto = new ConceptoPresupuesto
                    {
                        ProyectoId = _proyecto.Id,
                        Clave = clave,
                        Descripcion = desc,
                        EsAgrupador = esAgrupador,
                        Nivel = nivel,
                        Orden = orden++,
                        Unidad = unidadCell?.Value?.ToString() ?? string.Empty,
                        ColumnasPersonalizadasJSON = string.Empty,
                        Notas = string.Empty
                    };

                    if (!esAgrupador)
                    {
                        var cantidadCell = ObtenerCeldaPorNombreInterno(row.Index, "Cantidad");
                        decimal.TryParse(cantidadCell?.Value?.ToString(), out decimal cantidad);
                        concepto.Cantidad = cantidad;

                        // Obtener matriz del Tag si existe
                        if (row.Tag is ConceptoPresupuesto conceptoTemp && conceptoTemp.MatrizId.HasValue)
                        {
                            concepto.MatrizId = conceptoTemp.MatrizId;
                            concepto.CostoDirectoUnitario = conceptoTemp.CostoDirectoUnitario;
                            concepto.CostoDirectoTotal = conceptoTemp.CostoDirectoTotal;
                        }
                        else
                        {
                            // Intentar parsear P.U. e Importe de las celdas
                            var puCell = ObtenerCeldaPorNombreInterno(row.Index, "PrecioUnitario");
                            var impCell = ObtenerCeldaPorNombreInterno(row.Index, "Importe");

                            string puStr = puCell?.Value?.ToString().Replace("$", "").Replace(",", "") ?? "0";
                            string impStr = impCell?.Value?.ToString().Replace("$", "").Replace(",", "") ?? "0";
                            decimal.TryParse(puStr, out decimal pu);
                            decimal.TryParse(impStr, out decimal imp);
                            concepto.CostoDirectoUnitario = pu;
                            concepto.CostoDirectoTotal = imp;
                        }
                    }

                    _context.ConceptosPresupuesto.Add(concepto);
                }

                _context.SaveChanges();
                MessageBox.Show("Presupuesto guardado exitosamente.", "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ActualizarEstadisticas();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int ObtenerNivelDesdeTipo(string tipo)
        {
            return BudgetHierarchyService.GetLevelFromType(tipo);
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnExplosion_Click(object sender, EventArgs e)
        {
            using var formExplosion = new FormExplosionInsumos(_context, _proyecto.Id);
            formExplosion.ShowDialog(this);
        }

        /// <summary>
        /// Calcula el valor de una columna calculada basándose en su fórmula
        /// </summary>
        private object CalcularColumnaCalculada(ColumnaPersonalizada columna, int rowIndex)
        {
            if (string.IsNullOrWhiteSpace(columna.Formula)) return null;

            try
            {
                string formula = columna.Formula;

                // Reemplazar nombres de columnas por sus valores
                var regex = new System.Text.RegularExpressions.Regex(@"\b[A-Za-z_][A-Za-z0-9_]*\b");
                var matches = regex.Matches(formula);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    string nombreColumna = match.Value;

                    // Obtener valor de la columna referenciada
                    var cell = ObtenerCeldaPorNombreInterno(rowIndex, nombreColumna);
                    if (cell?.Value != null)
                    {
                        string valorStr = cell.Value.ToString()
                            .Replace("$", "")
                            .Replace(",", "")
                            .Replace("%", "")
                            .Trim();

                        if (decimal.TryParse(valorStr, out decimal valor))
                        {
                            formula = formula.Replace(nombreColumna, valor.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                    else
                    {
                        formula = formula.Replace(nombreColumna, "0");
                    }
                }

                // Evaluar expresión matemática
                var dataTable = new System.Data.DataTable();
                var resultado = dataTable.Compute(formula, "");

                if (resultado != null && decimal.TryParse(resultado.ToString(), out decimal resultadoDecimal))
                {
                    return resultadoDecimal;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Guardar orden visual de columnas antes de cerrar
            try
            {
                foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                {
                    if (col.Tag is ColumnaPersonalizada colDef)
                    {
                        var columnaDB = _context.ColumnasPersonalizadas.Find(colDef.Id);
                        if (columnaDB != null)
                        {
                            columnaDB.Orden = col.DisplayIndex;
                        }
                    }
                }
                _context.SaveChanges();
            }
            catch
            {
                // Silencioso - no bloquear el cierre
            }

            SaveWorkspacePanelState();
            base.OnFormClosing(e);
        }

        private void btnToggleMatrices_Click(object sender, EventArgs e)
        {
            if (!splitContainer.Panel2Collapsed)
                SaveWorkspacePanelState();

            splitContainer.Panel2Collapsed = !splitContainer.Panel2Collapsed;

            if (splitContainer.Panel2Collapsed)
            {
                btnToggleMatrices.Text = "📐 Matrices ▼";
            }
            else
            {
                btnToggleMatrices.Text = "📐 Matrices ▲";
                var targetHeight = _workspacePanelHeight > 0 ? _workspacePanelHeight : Math.Max(splitContainer.Panel2MinSize, (int)(splitContainer.Height * 0.34));
                splitContainer.SplitterDistance = Math.Max(splitContainer.Panel1MinSize, splitContainer.Height - targetHeight);
                ProgramarAsegurarFilaActualVisibleEnPresupuesto();
            }
        }

        private void btnExportar_Click(object sender, EventArgs e)
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            using var opciones = new FormExportarReporte(_context, _proyecto);
            opciones.ShowDialog();
        }


        public SOPROContext Contexto => _context;
        public Proyecto ProyectoActual => _proyecto;

        public void GenerarPdfPresupuesto()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnasBD = svc.ObtenerOCrearColumnas(_proyecto.Id, "Presupuesto");

                foreach (var cfg in columnasBD)
                    cfg.Visible = false;

                var colsEnReporte = dgvPresupuesto.Columns
                    .Cast<System.Windows.Forms.DataGridViewColumn>()
                    .Where(col => col.Visible)
                    .Where(col => col.Name != "colRelleno")
                    .Where(col =>
                    {
                        if (col.Name == "colNumero") return true;
                        if (col.Tag is not Core.Entities.ColumnaPersonalizada cd2) return false;
                        return !string.Equals(cd2.NombreInterno, "Tipo", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(col => col.DisplayIndex)
                    .ToList();

                int ordenReporte = 0;
                foreach (var col in colsEnReporte)
                {
                    string nombreInternoGrid = col.Name == "colNumero"
                        ? "Numero"
                        : ((Core.Entities.ColumnaPersonalizada)col.Tag).NombreInterno;

                    string nombreInternoReporte = ObtenerNombreInternoReporteDesdeGrid(nombreInternoGrid);
                    var cfg = columnasBD.FirstOrDefault(r => r.NombreInterno == nombreInternoReporte);
                    if (cfg == null)
                    {
                        cfg = CrearConfigColumnaReporteDesdeGrid(col, nombreInternoReporte);
                        cfg.ProyectoId = _proyecto.Id;
                        cfg.TipoReporte = "Presupuesto";
                        columnasBD.Add(cfg);
                    }

                    cfg.Visible = true;
                    cfg.Orden = ordenReporte++;
                    cfg.Ancho = col.Width;
                    cfg.Encabezado = col.HeaderText;
                    cfg.WrapTexto = (col.Tag as Core.Entities.ColumnaPersonalizada)?.WrapTexto == true
                        || col.DefaultCellStyle.WrapMode == DataGridViewTriState.True;
                    ActualizarConfigColumnaReporteDesdeGrid(cfg, col);
                }

                svc.GuardarColumnas(columnasBD);

                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show("El presupuesto no tiene conceptos.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Presupuesto_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                decimal factorPU = CalcularFactorPU();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Presupuesto, lblTitulo.Text);
                var gen = new Services.GeneradorPdfPresupuesto(svc);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, columnasBD, dlg.FileName, factorPU, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte PDF:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GenerarPdfAPU()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                var conceptosConAPU = conceptos
                    .Where(c => !c.EsAgrupador && c.MatrizId.HasValue)
                    .ToList();

                if (!conceptosConAPU.Any())
                {
                    MessageBox.Show(
                        "No hay conceptos con APU vinculado en este presupuesto." +
                        "Vincula una Matriz APU a cada concepto desde la columna correspondiente.",
                        "Sin APU", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte APU PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = "APU_" + _proyecto.Nombre + "_" + DateTime.Now.ToString("yyyyMMdd") + ".pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var estiloDescripcionApu = ObtenerEstiloDescripcionPresupuestoParaApu(svc);
                var gen = new Services.GeneradorPdfAPU(svc, _context);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, dlg.FileName, estiloDescripcionApu);
                Cursor = Cursors.Default;

                if (MessageBox.Show("APUs PDF generados:" + ruta + "¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar APUs PDF:" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GenerarExcelPresupuesto()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);

                // Tomar las columnas visibles en el orden actual del grid
                // en lugar de las de la BD, para respetar lo que el usuario ve.
                // Se excluye intencionalmente la columna Tipo.
                var columnasBD = svc.ObtenerOCrearColumnas(_proyecto.Id, "Presupuesto");

                foreach (var cfg in columnasBD)
                    cfg.Visible = false;

                var colsEnReporte = dgvPresupuesto.Columns
                    .Cast<System.Windows.Forms.DataGridViewColumn>()
                    .Where(col => col.Visible)
                    .Where(col => col.Name != "colRelleno")
                    .Where(col =>
                    {
                        if (col.Name == "colNumero") return true;
                        if (col.Tag is not Core.Entities.ColumnaPersonalizada cd2) return false;
                        return !string.Equals(cd2.NombreInterno, "Tipo", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(col => col.DisplayIndex)
                    .ToList();

                int ordenReporte = 0;
                foreach (var col in colsEnReporte)
                {
                    string nombreInternoGrid = col.Name == "colNumero"
                        ? "Numero"
                        : ((Core.Entities.ColumnaPersonalizada)col.Tag).NombreInterno;

                    string nombreInternoReporte = ObtenerNombreInternoReporteDesdeGrid(nombreInternoGrid);
                    var cfg = columnasBD.FirstOrDefault(r => r.NombreInterno == nombreInternoReporte);
                    if (cfg == null)
                    {
                        cfg = CrearConfigColumnaReporteDesdeGrid(col, nombreInternoReporte);
                        cfg.ProyectoId = _proyecto.Id;
                        cfg.TipoReporte = "Presupuesto";
                        columnasBD.Add(cfg);
                    }

                    cfg.Visible = true;
                    cfg.Orden = ordenReporte++;
                    cfg.Ancho = col.Width;
                    cfg.Encabezado = col.HeaderText;
                    cfg.WrapTexto = (col.Tag as Core.Entities.ColumnaPersonalizada)?.WrapTexto == true
                        || col.DefaultCellStyle.WrapMode == DataGridViewTriState.True;
                    ActualizarConfigColumnaReporteDesdeGrid(cfg, col);
                }

                svc.GuardarColumnas(columnasBD);

                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show("El presupuesto no tiene conceptos.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Excel",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Presupuesto_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                decimal factorPU = CalcularFactorPU();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Presupuesto, lblTitulo.Text);
                var gen = new Services.GeneradorExcelPresupuesto(svc);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, columnasBD, dlg.FileName, factorPU, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte generado:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private static string ObtenerNombreInternoReporteDesdeGrid(string nombreInternoGrid)
        {
            return nombreInternoGrid switch
            {
                "Importe" => "ImporteTotal",
                _ => nombreInternoGrid
            };
        }

        private static Core.Entities.ConfigColumnaReporte CrearConfigColumnaReporteDesdeGrid(DataGridViewColumn col, string nombreInternoReporte)
        {
            var sourceColumn = col.Tag as Core.Entities.ColumnaPersonalizada;
            var tipoDato = sourceColumn?.TipoDato ?? Core.Entities.TipoDatoColumna.Texto;
            var alineacion = sourceColumn?.Alineacion ?? Core.Entities.AlineacionColumna.Izquierda;

            var cfg = new Core.Entities.ConfigColumnaReporte
            {
                NombreInterno = nombreInternoReporte,
                Encabezado = col.HeaderText,
                Visible = col.Visible,
                Orden = col.DisplayIndex,
                Ancho = col.Width,
                EncFuente = "Segoe UI",
                EncTamaño = 9f,
                EncNegrita = true,
                EncAlineacion = "Centro",
                EncColorFondo = "#1565C0",
                EncColorTexto = "#FFFFFF",
                ConFuente = "Segoe UI",
                ConTamaño = 9f,
                ConNegrita = false,
                ConAlineacion = alineacion switch
                {
                    Core.Entities.AlineacionColumna.Centro => "Centro",
                    Core.Entities.AlineacionColumna.Derecha => "Derecha",
                    _ => tipoDato == Core.Entities.TipoDatoColumna.Moneda || tipoDato == Core.Entities.TipoDatoColumna.Numerico || tipoDato == Core.Entities.TipoDatoColumna.Porcentaje
                        ? "Derecha"
                        : "Izquierda"
                },
                ConColorFondo = "#FFFFFF",
                ConColorTexto = "#000000",
                WrapTexto = (sourceColumn?.WrapTexto ?? false) || col.DefaultCellStyle.WrapMode == DataGridViewTriState.True,
                FormatoNumero = ObtenerFormatoNumeroReporte(nombreInternoReporte, tipoDato)
            };

            ActualizarConfigColumnaReporteDesdeGrid(cfg, col);
            return cfg;
        }

        private static void ActualizarConfigColumnaReporteDesdeGrid(Core.Entities.ConfigColumnaReporte cfg, DataGridViewColumn col)
        {
            var sourceColumn = col.Tag as Core.Entities.ColumnaPersonalizada;
            var estiloColumna = col.DefaultCellStyle;
            var fuente = estiloColumna.Font;

            cfg.ConFuente = !string.IsNullOrWhiteSpace(sourceColumn?.NombreFuente)
                ? sourceColumn.NombreFuente
                : (!string.IsNullOrWhiteSpace(fuente?.Name) ? fuente.Name : "Segoe UI");
            cfg.ConTamaño = sourceColumn?.TamanoFuente > 0
                ? sourceColumn.TamanoFuente
                : (fuente?.Size ?? 9f);
            cfg.ConNegrita = sourceColumn?.Negrita ?? (fuente?.Bold ?? false);
            cfg.ConCursiva = sourceColumn?.Cursiva ?? (fuente?.Italic ?? false);
            cfg.ConColorFondo = !string.IsNullOrWhiteSpace(sourceColumn?.ColorFondo)
                ? sourceColumn.ColorFondo
                : ColorAHex(estiloColumna.BackColor.IsEmpty ? Color.White : estiloColumna.BackColor);
            cfg.ConColorTexto = !string.IsNullOrWhiteSpace(sourceColumn?.ColorFuente)
                ? sourceColumn.ColorFuente
                : ColorAHex(estiloColumna.ForeColor.IsEmpty ? Color.Black : estiloColumna.ForeColor);
        }


        private static string ColorAHex(Color color)
        {
            if (color.IsEmpty) color = Color.Black;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private Core.Entities.ConfigColumnaReporte ObtenerEstiloDescripcionPresupuestoParaApu(Services.ReporteService svc)
        {
            var columnasReporte = svc.ObtenerOCrearColumnas(_proyecto.Id, "Presupuesto");
            var descripcionReporte = columnasReporte.FirstOrDefault(c => c.NombreInterno == "Descripcion");

            var columnaGridDescripcion = dgvPresupuesto.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(col => col.Tag is Core.Entities.ColumnaPersonalizada cp &&
                    string.Equals(cp.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase));

            if (descripcionReporte == null)
            {
                descripcionReporte = new Core.Entities.ConfigColumnaReporte
                {
                    NombreInterno = "Descripcion",
                    Encabezado = "Descripción",
                    ConFuente = "Segoe UI",
                    ConTamaño = 9f,
                    ConColorFondo = "#FFFFFF",
                    ConColorTexto = "#000000",
                    ConAlineacion = "Izquierda"
                };
            }

            if (columnaGridDescripcion != null)
                ActualizarConfigColumnaReporteDesdeGrid(descripcionReporte, columnaGridDescripcion);

            return descripcionReporte;
        }

        private static string ObtenerFormatoNumeroReporte(string nombreInternoReporte, Core.Entities.TipoDatoColumna tipoDato)
        {
            return nombreInternoReporte switch
            {
                "Cantidad" => "N3",
                "PrecioUnitario" or "ImporteTotal" or "Subtotal" or "IVA" or "Total" or "Indirectos" or "Financiamiento" or "Utilidad" => "N2",
                _ => tipoDato switch
                {
                    Core.Entities.TipoDatoColumna.Moneda => "N2",
                    Core.Entities.TipoDatoColumna.Numerico => "N3",
                    _ => string.Empty
                }
            };
        }

        public void GenerarExcelAPU()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id && !c.EsAgrupador && c.MatrizId.HasValue)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show(
                        "No hay conceptos con APU vinculado en este presupuesto.\n\n" +
                        "Vincula una Matriz APU a cada concepto desde la columna correspondiente.",
                        "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnas = svc.ObtenerOCrearColumnas(_proyecto.Id, "APU");

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte APU",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = "APU_" + _proyecto.Nombre + "_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var estiloDescripcionApu = ObtenerEstiloDescripcionPresupuestoParaApu(svc);
                var gen = new Services.GeneradorExcelAPU(svc, _context);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, columnas, dlg.FileName, estiloDescripcionApu);
                Cursor = Cursors.Default;

                if (MessageBox.Show("APUs generados:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar APUs:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnColumnas_Click(object sender, EventArgs e)
        {
            // Guardar la columna actual para insertar nuevas columnas a su derecha
            int columnaActualIndex = dgvPresupuesto.CurrentCell?.ColumnIndex ?? -1;

            using var form = new FormColumnasPersonalizadas(_context, _proyecto.Id, columnaActualIndex);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                try
                {
                    // Solo guardar el orden visual actual de columnas
                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            var columnaDB = _context.ColumnasPersonalizadas.Find(colDef.Id);
                            if (columnaDB != null)
                                columnaDB.Orden = col.DisplayIndex;
                        }
                    }
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al guardar orden de columnas:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Recargar presupuesto con nuevas columnas, preservando fila/scroll/celda cuando sea posible
                RecargarPresupuestoPreservandoEstado();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Desuscribir eventos ESTÁTICOS para evitar "disposed context" si el form
            // se cierra mientras otros módulos siguen activos
            FormCatalogoMateriales.InsumosModificados -= _onInsumosModificados;
            FormCatalogoManoObra.InsumosModificados -= _onInsumosModificados;
            FormCatalogoHerramientas.InsumosModificados -= _onInsumosModificados;
            FormCatalogoMaquinaria.InsumosModificados -= _onInsumosModificados;
            FormEditarMatriz.MatrizGuardada -= _onInsumosModificados;
            base.OnFormClosed(e);
        }

        public void RecalcularTodo()
        {
            // Recargar proyecto para tener decimales frescos
            var proyFresco = _context.Proyectos.Find(_proyecto.Id);
            if (proyFresco != null)
            {
                _proyecto.DecimalesCantidad  = proyFresco.DecimalesCantidad;
                _proyecto.DecimalesImporte   = proyFresco.DecimalesImporte;
                _proyecto.DecimalesPorcentaje = proyFresco.DecimalesPorcentaje;
                FormatoHelper.EstablecerProyecto(_proyecto);
            }

            // Actualizar formato de la columna Cantidad con los decimales actuales
            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef &&
                    string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
                {
                    col.DefaultCellStyle.Format = $"N{_proyecto.DecimalesCantidad}";
                }
            }

            // Re-formatear celdas de Cantidad con el nuevo número de decimales
            var motorRecalc = new MotorCalculoSopro(_proyecto);
            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var concepto = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;
                if (concepto == null || concepto.EsAgrupador) continue;

                foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                {
                    if (col.Tag is ColumnaPersonalizada colDef &&
                        string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
                    {
                        // Re-formatear con los nuevos decimales
                        dgvPresupuesto.Rows[i].Cells[col.Index].Value =
                            motorRecalc.RedondearCantidad(concepto.Cantidad);
                    }
                }
            }

            RefrescarPreciosDesdeDB();
            ValidarPresupuestoAntesDeContinuar(bloquear: false, titulo: "Validación de presupuesto");
        }
    }
}