using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Workspace de matrices: selector embebido, panel de estado e importación desde Excel.
    /// </summary>
    public partial class FormPresupuesto
    {
        // Política de alto del workspace APU (Panel2 del split). Única fuente de verdad.
        // Valores en píxeles de diseño a 96 DPI; se escalan con EscalarPixelesDiseno.
        private const int AltoMinimoFuncionalApu = 250;    // toolbar (31) + host operativo fuera del modo compacto
        private const int AltoMinimoDegradadoApu = 150;    // piso duro en ventanas pequeñas (modo compacto)
        private const double FactorAlturaWorkspace = 0.42; // crecimiento proporcional sobre el alto del split
        private bool _politicaAltoWorkspaceInicializada;
        private bool _ajustandoAltoWorkspace;

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

            InicializarPoliticaAltoWorkspace();
        }

        private void SaveWorkspacePanelState()
        {
            // No persistir alturas fijadas por la política (evita congelar el alto y realimentar Resize).
            if (_ajustandoAltoWorkspace)
                return;

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

        /// <summary>
        /// Escala una medida de diseño (96 DPI) al DPI real del formulario.
        /// </summary>
        private int EscalarPixelesDiseno(int pixelesBase)
        {
            int dpi = DeviceDpi > 0 ? DeviceDpi : 96;
            return (int)Math.Ceiling(pixelesBase * dpi / 96d);
        }

        /// <summary>
        /// Configura Panel2MinSize escalado por DPI como piso de degradación compacta,
        /// nunca como mínimo funcional (eso lo resuelve el tope, no el mínimo duro).
        /// </summary>
        private void AplicarPanel2MinSizeEscaladoDpi()
        {
            int minimoDegradado = EscalarPixelesDiseno(AltoMinimoDegradadoApu);
            if (splitContainer.Panel2MinSize != minimoDegradado)
                splitContainer.Panel2MinSize = minimoDegradado;
        }

        /// <summary>
        /// Única fuente de la política de alto del workspace APU. Devuelve el alto
        /// objetivo (px) de Panel2: mínimo funcional escalado por DPI cuando hay
        /// espacio, crecimiento proporcional con el alto del split y tope que
        /// reserva Panel1MinSize al presupuesto. En ventanas demasiado pequeñas
        /// degrada (nunca impone un mínimo imposible).
        /// </summary>
        private int CalcularAltoObjetivoWorkspace()
        {
            int alturaSplit = splitContainer.Height;
            if (alturaSplit <= 0)
                return EscalarPixelesDiseno(AltoMinimoDegradadoApu);

            int separador = Math.Max(0, splitContainer.SplitterWidth);
            int minimoFuncional = EscalarPixelesDiseno(AltoMinimoFuncionalApu);
            int pisoDegradado = splitContainer.Panel2MinSize > 0
                ? splitContainer.Panel2MinSize
                : EscalarPixelesDiseno(AltoMinimoDegradadoApu);

            // Tope: deja al presupuesto (Panel1) su área mínima.
            int tope = alturaSplit - splitContainer.Panel1MinSize - separador;

            // Preferencia del usuario si existe; si no, mínimo funcional con
            // crecimiento proporcional según el alto disponible.
            int objetivo = _workspacePanelHeight > 0
                ? _workspacePanelHeight
                : Math.Max(minimoFuncional, (int)(alturaSplit * FactorAlturaWorkspace));

            // No invadir el área mínima del presupuesto.
            if (objetivo > tope)
                objetivo = tope;

            // Ventana pequeña: no forzar el mínimo funcional; bajar hasta el piso
            // degradado (el modo compacto del panel se encarga del resto).
            if (objetivo < pisoDegradado)
                objetivo = Math.Min(pisoDegradado, Math.Max(1, tope));

            return Math.Max(1, objetivo);
        }

        /// <summary>
        /// Recalcula y fija el alto del workspace si el panel está expandido,
        /// sin persistir el valor ni realimentar el evento Resize.
        /// </summary>
        private void AplicarAltoObjetivoWorkspace()
        {
            if (splitContainer.Panel2Collapsed || splitContainer.Height <= 0)
                return;

            int minimoCombinado = splitContainer.Panel1MinSize + splitContainer.Panel2MinSize + splitContainer.SplitterWidth;
            if (splitContainer.Height < minimoCombinado)
                return; // ventana demasiado pequeña: no forzar una geometría imposible

            int alturaObjetivo = CalcularAltoObjetivoWorkspace();
            int distancia = splitContainer.Height - alturaObjetivo - splitContainer.SplitterWidth;
            distancia = Math.Max(splitContainer.Panel1MinSize, distancia);

            if (distancia == splitContainer.SplitterDistance)
                return;

            _ajustandoAltoWorkspace = true;
            try
            {
                splitContainer.SplitterDistance = distancia;
            }
            finally
            {
                _ajustandoAltoWorkspace = false;
            }
        }

        private void InicializarPoliticaAltoWorkspace()
        {
            if (_politicaAltoWorkspaceInicializada)
                return;

            _politicaAltoWorkspaceInicializada = true;
            AplicarPanel2MinSizeEscaladoDpi();
            splitContainer.Resize += SplitContainer_WorkspaceResize;
            this.Load += (_, __) =>
            {
                AplicarPanel2MinSizeEscaladoDpi();
                if (!splitContainer.Panel2Collapsed)
                    AplicarAltoObjetivoWorkspace();
            };
            this.DpiChanged += (_, __) =>
            {
                AplicarPanel2MinSizeEscaladoDpi();
                if (!splitContainer.Panel2Collapsed)
                    AplicarAltoObjetivoWorkspace();
            };
        }

        private void SplitContainer_WorkspaceResize(object? sender, EventArgs e)
        {
            if (_ajustandoAltoWorkspace)
                return;

            AplicarAltoObjetivoWorkspace();
        }

        private void EnsureWorkspaceExpanded()
        {
            if (splitContainer.Panel2Collapsed)
            {
                splitContainer.Panel2Collapsed = false;
                AplicarAltoObjetivoWorkspace();
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

        private static bool DebeUsarDescripcionDeMatriz(string? descripcionActual, string? descripcionMatriz)
        {
            string texto = (descripcionActual ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(texto))
                return true;

            if (string.IsNullOrWhiteSpace(descripcionMatriz))
                return false;

            if (texto.Length <= 12)
                return true;

            if (!texto.Contains(' ') && texto.Length <= 25)
                return true;

            string normalActual = NormalizarTextoBusqueda(texto);
            string normalMatriz = NormalizarTextoBusqueda(descripcionMatriz);

            if (normalActual.Length <= 30 && normalMatriz.StartsWith(normalActual, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string NormalizarTextoBusqueda(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            return texto.Trim().ToUpperInvariant();
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

        /// <summary>
        /// Clasificación de la última importación ejecutada como comando, o <c>null</c>
        /// si no llegó a enviarse (lote vacío o trabajo pendiente en el contexto
        /// compartido). Observabilidad interna para las pruebas del ensamblado; no es
        /// API pública ni se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoImportacion { get; private set; }

        private void ImportarFilasPresupuestoDesdeExcel(List<BudgetExcelImportRowDto> filas)
        {
            try
            {
                var resultado = EjecutarImportacionPresupuesto(filas);
                if (resultado is null)
                {
                    MessageBox.Show("No se generaron conceptos válidos para importar.", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!resultado.IsCommitted || resultado.Confirmed is null)
                {
                    MessageBox.Show(
                        resultado.Message ?? "No se pudo importar el presupuesto; no se guardó ningún renglón.",
                        "Importar presupuesto",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show(
                    $"Se importaron {resultado.Confirmed.Rows.Count} renglones desde Excel\n\nNo se importaron P.U. ni Importe.",
                    "Importar presupuesto",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar el presupuesto desde Excel:\n{ex.Message}", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Construye el comando inmutable con las filas YA normalizadas por la vista
        /// (clasificación de agrupadores incluida), lo ejecuta sobre un contexto nuevo
        /// y solo recarga el grid si la operación se confirmó. No escribe nada en el
        /// contexto compartido ni ejecuta el guardado heredado.
        /// </summary>
        internal BudgetImportResult? EjecutarImportacionPresupuesto(IReadOnlyList<BudgetExcelImportRowDto>? filas)
        {
            UltimoEstadoImportacion = null;

            if (filas == null || filas.Count == 0)
                return null;

            var renglones = ConstruirRenglonesImportacion(filas);
            if (renglones.Count == 0)
                return null;

            // Conservador: si el contexto compartido arrastra trabajo pendiente de otras
            // rutas, no se envía el comando ni se guarda nada aquí.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                throw new InvalidOperationException(
                    "Hay cambios pendientes en el presupuesto; guarde o descarte antes de importar.");

            var command = new BudgetImportCommand(
                _proyecto.Id,
                BudgetProjectCalculationBase.FromProyecto(_proyecto),
                renglones);

            var resultado = BudgetPersistenceService.ExecuteImportCommand(
                _factory,
                _context.DatabasePath,
                command);

            UltimoEstadoImportacion = resultado.Status;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Importación no confirmada ({resultado.Status}): {resultado.Diagnostic}");
                return resultado;
            }

            // La vista se actualiza SOLO después del commit. La recarga usa la ruta de
            // carga que ya no persiste totales de agrupadores.
            RecargarPresupuestoPreservandoEstado();
            return resultado;
        }

        private List<BudgetImportRow> ConstruirRenglonesImportacion(IReadOnlyList<BudgetExcelImportRowDto> filas)
        {
            var renglones = new List<BudgetImportRow>(filas.Count);
            bool yaSeAsignoCapituloInferido = false;

            foreach (var fila in filas)
            {
                if (fila is null)
                    continue;

                string tipo = ResolverTipoImportado(fila, ref yaSeAsignoCapituloInferido);
                if (string.Equals(tipo, "No importar", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool esAgrupador = !string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase);

                renglones.Add(new BudgetImportRow(
                    fila.SourceRowNumber > 0 ? fila.SourceRowNumber : null,
                    fila.Clave,
                    fila.Descripcion,
                    esAgrupador ? string.Empty : fila.Unidad,
                    esAgrupador ? 0m : (fila.Cantidad ?? 0m),
                    tipo));
            }

            return renglones;
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

            // Si el concepto ya tiene matriz asignada, F2 / doble click en P.U. deben abrir
            // directamente el panel embebido sobre esa matriz actual. El cambio de matriz se
            // hace manualmente desde el workspace usando el selector APU.
            if (matrizIdActual.HasValue)
            {
                MostrarPanelMatrizEmbebido(false);
                _panelMatricesEmbebido?.BeginEditMatrix(matrizIdActual.Value,
                    onSaved: null,
                    onCancelled: () => MostrarPanelMatrizEmbebido(true));

                if (rowIndex >= 0 && rowIndex < dgvPresupuesto.Rows.Count)
                {
                    var cell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario")
                               ?? ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion");
                    if (cell != null)
                        dgvPresupuesto.CurrentCell = cell;
                }
                return;
            }

            var filtroInicial = !string.IsNullOrWhiteSpace(descripcionFiltro)
                ? descripcionFiltro
                : null;

            MostrarSelectorApuEmbebido(rowIndex, cantidadActual, null, filtroInicial);
        }

        internal bool TryApplyBudgetConceptAssignmentByKey(int rowIndex, bool showErrors = true)
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

            var draft = assignment.Draft;

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

            // La asignación ya no usa ApplyDraft/guardado heredado: tanto la
            // actualización de una fila persistida como el alta de una fila nueva
            // (Id 0/Tag null) viajan como comando inmutable a un contexto nuevo.
            // ResolveByKey solo resuelve la fuente y el borrador visible; el comando
            // transporta identidad/bases inmutable y recalcula en Application.
            var target = ConstruirTargetAsignacion(assignment, draft);
            if (target is null)
                return false;

            if (conceptoActual is { Id: > 0 })
            {
                return TryApplyMatrixAssignmentWithCommand(
                    rowIndex,
                    claveNueva,
                    conceptoActual,
                    target,
                    draft) == MatrixAssignmentDispatch.Sent;
            }

            var despachoNuevaFila = TryApplyMatrixAssignmentWithCommand(
                rowIndex,
                claveNueva,
                conceptoActual: null,
                target,
                draft);

            if (despachoNuevaFila == MatrixAssignmentDispatch.NotSentPendingChanges
                || UltimoEstadoAsignacionMatriz != BudgetOperationStatus.Committed)
            {
                // El alta no se confirmó (conflicto, fallo o trabajo ajeno pendiente):
                // el borrador queda visible y la fila conserva su Id 0/Tag null.
                ApplyAssignmentDraftToGrid(rowIndex, draft);
            }

            return true;
        }

        private BudgetAssignmentTarget? ConstruirTargetAsignacion(
            BudgetConceptAssignmentResult assignment,
            BudgetConceptAssignmentDraft draft)
        {
            if (assignment.SourceRowIndex >= 0 && assignment.SourceConcept is { Id: > 0 } origen)
                return BudgetAssignmentTarget.ForSourceConcept(
                    BudgetPasteSourceConceptSnapshot.FromConcepto(origen));

            if (draft.Matriz is { Id: > 0 } matriz)
                return BudgetAssignmentTarget.ForMatrix(
                    matriz.Id, BudgetMatrixAssignmentBase.FromMatriz(matriz));

            if (draft.MatrizId is int matrizId && matrizId > 0)
            {
                var matrizActual = _context.Matrices
                    .AsNoTracking()
                    .FirstOrDefault(m => m.Id == matrizId);

                if (matrizActual != null)
                    return BudgetAssignmentTarget.ForMatrix(
                        matrizActual.Id, BudgetMatrixAssignmentBase.FromMatriz(matrizActual));
            }

            return null;
        }

        /// <summary>
        /// Construye el rowset COMPLETO de filas persistidas que la vista ve antes de
        /// crear una fila, y la posición de inserción según la fila actual del grid.
        /// </summary>
        private BudgetAssignmentRowset ConstruirRowsetAsignacion(int rowIndex, out int insertionIndex)
        {
            var filas = new List<BudgetAssignmentRow>(dgvPresupuesto.Rows.Count);
            insertionIndex = 0;

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                if (dgvPresupuesto.Rows[i].Tag is not ConceptoPresupuesto concepto || concepto.Id <= 0)
                    continue;

                filas.Add(BudgetAssignmentRow.FromConcepto(concepto));
                if (i < rowIndex)
                    insertionIndex++;
            }

            return new BudgetAssignmentRowset(filas);
        }

        /// <summary>
        /// Clasificación de la última asignación de matriz ejecutada como comando, o
        /// <c>null</c> si la asignación no llegó a enviarse (trabajo pendiente en el
        /// contexto compartido o reintento bloqueado por un commit incierto).
        /// Observabilidad interna para las pruebas del ensamblado; no es API pública.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoAsignacionMatriz { get; private set; }

        private enum MatrixAssignmentDispatch
        {
            NotSentPendingChanges,
            Sent
        }

        /// <summary>
        /// Asigna una matriz/copia a un concepto persistido o crea una fila nueva con la
        /// ruta de comando: no muta el Tag compartido antes de conocer el resultado y no
        /// ejecuta el guardado heredado para este evento. Si falla o entra en conflicto,
        /// la clave editada permanece visible como borrador.
        /// </summary>
        private MatrixAssignmentDispatch TryApplyMatrixAssignmentWithCommand(
            int rowIndex,
            string claveNueva,
            ConceptoPresupuesto? conceptoActual,
            BudgetAssignmentTarget target,
            BudgetConceptAssignmentDraft draft,
            string? descriptionOverride = null,
            string? unitOverride = null)
        {
            UltimoEstadoAsignacionMatriz = null;

            // Conservador: si el contexto compartido arrastra trabajo pendiente de
            // otras rutas, no se envía el comando ni se guarda nada aquí. El llamador
            // debe conservar el borrador y NO caer a la ruta heredada de guardado.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return MatrixAssignmentDispatch.NotSentPendingChanges;

            bool esNuevaFila = conceptoActual is null || conceptoActual.Id <= 0;

            // Un commit incierto no autoriza a reintentar el alta a ciegas: pudo haber
            // insertado la fila y solo se sabe reconciliando con la base.
            if (esNuevaFila && _asignacionNuevaFilaPendienteReconciliacion)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Alta de fila bloqueada: la asignación anterior quedó en estado incierto; " +
                    "reconcilie con la base antes de reintentar.");
                return MatrixAssignmentDispatch.NotSentPendingChanges;
            }

            BudgetAssignmentRowset? rowset = null;
            int? insertionIndex = null;
            if (esNuevaFila)
            {
                rowset = ConstruirRowsetAsignacion(rowIndex, out int indice);
                insertionIndex = indice;
            }

            BudgetMatrixAssignmentResult resultado;
            try
            {
                var command = esNuevaFila
                    ? BudgetMatrixAssignmentCommand.ForNewRow(
                        _proyecto.Id,
                        BudgetProjectCalculationBase.FromProyecto(_proyecto),
                        target,
                        claveNueva,
                        descriptionOverride,
                        unitOverride,
                        draft.Cantidad,
                        rowset!,
                        insertionIndex!.Value)
                    : BudgetMatrixAssignmentCommand.ForExistingRow(
                        _proyecto.Id,
                        conceptoActual!.Id,
                        BudgetProjectCalculationBase.FromProyecto(_proyecto),
                        BudgetConceptAssignmentBase.FromConcepto(conceptoActual),
                        target,
                        claveNueva,
                        descriptionOverride,
                        unitOverride,
                        draft.Cantidad);

                resultado = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    _factory,
                    _context.DatabasePath,
                    command);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Asignación de matriz: {ex.Message}");
                return MatrixAssignmentDispatch.Sent;
            }

            UltimoEstadoAsignacionMatriz = resultado.Status;

            if (resultado.Status == BudgetOperationStatus.OutcomeUnknown && esNuevaFila)
                _asignacionNuevaFilaPendienteReconciliacion = true;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                // Conflicto, fallo o resultado incierto: el borrador de la clave queda
                // visible y el tracker compartido intacto. No se invoca el guardado
                // heredado para este evento.
                System.Diagnostics.Debug.WriteLine(
                    $"Asignación de matriz no confirmada ({resultado.Status}): {resultado.Diagnostic}");
                return MatrixAssignmentDispatch.Sent;
            }

            AplicarAsignacionMatrizConfirmada(rowIndex, conceptoActual, resultado.Confirmed);
            return MatrixAssignmentDispatch.Sent;
        }

        /// <summary>
        /// Vuelca lo CONFIRMADO por la asignación: adjunta el concepto creado cuando la
        /// fila era nueva, sincroniza Tag y celdas visibles (incluidos los Orden
        /// desplazados y los agrupadores recalculados) bajo el guard de reentrada y deja
        /// todo <c>Unchanged</c> para que el autoguardado heredado no lo reescriba.
        /// </summary>
        private void AplicarAsignacionMatrizConfirmada(
            int rowIndex,
            ConceptoPresupuesto? concepto,
            BudgetMatrixAssignmentSnapshot confirmado)
        {
            var filasPorConcepto = ConstruirIndiceFilasPorConcepto();
            bool asignandoPrevio = _asignandoMatriz;
            _asignandoMatriz = true;
            try
            {
                if (concepto is { Id: > 0 })
                {
                    SincronizarEntidadAsignacion(concepto, confirmado);
                }
                else
                {
                    var entidadCreada = AdjuntarConceptoConfirmado(confirmado.Concept);
                    dgvPresupuesto.Rows[rowIndex].Tag = entidadCreada;
                }

                var claveCell = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
                var descCell = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion");
                var unidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Unidad");
                var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
                var puCell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
                var importeCell = ObtenerCeldaPorNombreInterno(rowIndex, "Importe");

                if (claveCell != null) claveCell.Value = confirmado.Clave;
                if (descCell != null) descCell.Value = confirmado.Descripcion;
                if (unidadCell != null) unidadCell.Value = confirmado.Unidad;
                if (cantidadCell != null) cantidadCell.Value = confirmado.Cantidad;
                if (puCell != null) puCell.Value = confirmado.PrecioUnitario.ToStringImporte();
                if (importeCell != null) importeCell.Value = confirmado.ImporteTotal.ToStringImporte();

                // Las filas posteriores pudieron desplazar su Orden dentro del mismo
                // comando: la instantánea trae el rowset completo confirmado.
                foreach (var fila in confirmado.Rows)
                {
                    if (fila.ConceptId == confirmado.ConceptId)
                        continue;

                    var entidadFila = ResolverEntidadCompartida(filasPorConcepto, fila.ConceptId);
                    if (entidadFila is not null)
                        SincronizarConceptoCompartido(entidadFila, fila);
                }

                FinalizeBudgetConceptAssignment(rowIndex);

                // La instantánea confirmada manda sobre el total que haya recalculado
                // el grid: se aplica al final para que Tag y celdas coincidan con la base.
                foreach (var agrupador in confirmado.Aggregators)
                {
                    var entidad = ResolverEntidadCompartida(filasPorConcepto, agrupador.ConceptId);
                    if (entidad is not null)
                        SincronizarEntidadCompartida(entidad, agrupador);

                    if (!filasPorConcepto.TryGetValue(agrupador.ConceptId, out int filaAgrupador))
                        continue;

                    var importeAgrupadorCell = ObtenerCeldaPorNombreInterno(filaAgrupador, "Importe");
                    if (importeAgrupadorCell != null)
                        importeAgrupadorCell.Value = agrupador.ImporteTotal.ToStringImporte();
                }
            }
            finally
            {
                _asignandoMatriz = asignandoPrevio;
            }

            _asignacionNuevaFilaPendienteReconciliacion = false;
        }

        private void SincronizarEntidadAsignacion(
            ConceptoPresupuesto entidad,
            BudgetMatrixAssignmentSnapshot confirmado)
        {
            entidad.Clave = confirmado.Clave;
            entidad.Descripcion = confirmado.Descripcion;
            entidad.Unidad = confirmado.Unidad;
            entidad.Cantidad = confirmado.Cantidad;
            entidad.CostoDirectoUnitario = confirmado.CostoDirectoUnitario;
            entidad.CostoDirectoTotal = confirmado.CostoDirectoTotal;
            entidad.PrecioUnitario = confirmado.PrecioUnitario;
            entidad.ImporteTotal = confirmado.ImporteTotal;
            entidad.MatrizId = confirmado.MatrizId;
            entidad.FechaModificacion = confirmado.FechaModificacion;

            var entry = _context.Entry(entidad);
            if (entry.State == EntityState.Detached) return;

            entry.OriginalValues.SetValues(entry.CurrentValues);
            entry.State = EntityState.Unchanged;
        }
    }
}
