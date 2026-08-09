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
    }
}
