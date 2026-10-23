using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Reporting.Columns;
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
    /// <summary>
    /// Edición de celdas de la grilla (validación, autocompletado de clave, undo/redo, clipboard).
    /// </summary>
    public partial class FormPresupuesto
    {

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
                // RIESGO CONOCIDO (ADR-0001, migración parcial): el pegado aún persiste
                // por la ruta heredada. Las celdas de texto disparan sus comandos al
                // asignarse, pero este flush puede volver a escribir Descripción/Unidad
                // y confirmar un borrador cuyo comando hubiera fallado. Pendiente de
                // migrar el pegado como una sola operación.
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
            if (e.Control is not TextBox tb)
                return;

            tb.KeyDown -= DgvPresupuesto_EditingControl_KeyDown;
            tb.PreviewKeyDown -= DgvPresupuesto_EditingControl_PreviewKeyDown;
            tb.TextChanged -= DgvPresupuesto_DescripcionTextChanged;
            tb.LostFocus -= DgvPresupuesto_DescripcionLostFocus;

            _txtDescripcionEnEdicion = null;

            if (dgvPresupuesto.CurrentCell?.OwningColumn?.Tag is not ColumnaPersonalizada colDefEdit)
                return;

            if (colDefEdit.NombreInterno == "Clave")
            {
                OcultarAutocompleteApu(false);
                tb.KeyDown += DgvPresupuesto_EditingControl_KeyDown;
                tb.PreviewKeyDown += DgvPresupuesto_EditingControl_PreviewKeyDown;
                return;
            }

            OcultarAutocompleteApu(false);
            _autocompleteRowIndex = -1;
            _autocompleteColumnIndex = -1;

            if (colDefEdit.NombreInterno == "Descripcion")
            {
                tb.KeyDown += DgvPresupuesto_EditingControl_KeyDown;
                tb.PreviewKeyDown += DgvPresupuesto_EditingControl_PreviewKeyDown;
                _txtDescripcionEnEdicion = tb;
                _autocompleteRowIndex = dgvPresupuesto.CurrentCell.RowIndex;
                _autocompleteColumnIndex = dgvPresupuesto.CurrentCell.ColumnIndex;
                tb.TextChanged += DgvPresupuesto_DescripcionTextChanged;
                tb.LostFocus += DgvPresupuesto_DescripcionLostFocus;
                MostrarAutocompleteApuParaTexto(tb.Text);
            }
        }

        private void DgvPresupuesto_DescripcionTextChanged(object? sender, EventArgs e)
        {
            if (sender is not TextBox tb)
                return;

            if (dgvPresupuesto.CurrentCell?.OwningColumn?.Tag is not ColumnaPersonalizada colActual
                || !string.Equals(colActual.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase)
                || _autocompleteRowIndex < 0
                || ObtenerCeldaTexto(_autocompleteRowIndex, "Tipo") != "Concepto")
            {
                OcultarAutocompleteApu(false);
                return;
            }

            MostrarAutocompleteApuParaTexto(tb.Text);
        }

        private void DgvPresupuesto_DescripcionLostFocus(object? sender, EventArgs e)
        {
            BeginInvoke(new Action(() =>
            {
                if (_lstApuAutocomplete != null)
                {
                    bool cursorSobreLista = _lstApuAutocomplete.Visible && _lstApuAutocomplete.Bounds.Contains(PointToClient(Cursor.Position));
                    if (_lstApuAutocomplete.Focused || _lstApuAutocomplete.ContainsFocus || _mouseDownEnAutocomplete || _confirmandoSeleccionAutocomplete || cursorSobreLista)
                        return;
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

        private static bool EsColumnaTextoPresupuesto(string? nombreInterno)
        {
            return string.Equals(nombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "Unidad", StringComparison.OrdinalIgnoreCase);
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

            var colDefUndo = dgvPresupuesto.Columns[columnIndex].Tag as ColumnaPersonalizada;
            if (string.Equals(colDefUndo?.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
            {
                DgvPresupuesto_CellValueChanged(dgvPresupuesto, new DataGridViewCellEventArgs(columnIndex, rowIndex));
            }
            else if (EsColumnaTextoPresupuesto(colDefUndo?.NombreInterno))
            {
                // Asignar cell.Value ya disparó CellValueChanged, que consume la
                // edición con el comando por operación; no se vuelve a guardar la
                // misma fila por la ruta heredada.
                ActualizarEstadisticas();
                dgvPresupuesto.Refresh();
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
            _autocompleteRowIndex = e.RowIndex;
            _undoBudgetRowIndex = e.RowIndex;
            _undoBudgetColumnIndex = e.ColumnIndex;
            _undoBudgetColumnName = (dgvPresupuesto.Columns[e.ColumnIndex].Tag as ColumnaPersonalizada)?.NombreInterno ?? string.Empty;
            _undoBudgetOldValue = dgvPresupuesto.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            _autocompleteColumnIndex = e.ColumnIndex;
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

        // ═══════════════════ Formato de celdas (paso 4.1) ═══════════════════
        // Mapeo selección → direcciones ESTABLES (sin índices visuales), carga
        // única del diccionario en memoria, persistencia por lote con
        // FormatoReporteService y undo/redo como UNA acción compuesta.

        /// <summary>
        /// Clave RESTABLE de la fila para el formato de celdas, o <c>null</c> si la
        /// fila no tiene una entidad persistida (fila vacía / sin concepto). Los
        /// conceptos usan <c>C:{Id}</c>; los agrupadores (Capítulo/Subcapítulo/Nivel)
        /// usan <c>SUB:{Id}</c> (único por fila), de modo que editar la celda de un
        /// agrupador NO afecta a los demás aunque compartan <c>Clave</c>.
        /// </summary>
        internal static string? ObtenerClaveFilaFormato(DataGridView grid, int rowIndex)
        {
            if (grid is null) return null;
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;
            if (grid.Rows[rowIndex].Tag is not ConceptoPresupuesto concepto) return null;
            if (concepto.Id <= 0) return null;

            return concepto.EsAgrupador
                ? ClaveFilaReporte.Agrupador(concepto.Id)
                : ClaveFilaReporte.Concepto(concepto.Id);
        }

        /// <summary>
        /// Nombre del control de la columna sintética de numeración ("#") del grid.
        /// No es data-bound (no tiene <see cref="ColumnaPersonalizada"/>), pero SÍ es
        /// formateable y exportable con la clave canónica <c>Numero</c>.
        /// </summary>
        internal const string ColNumeroNombre = "colNumero";

        /// <summary>
        /// Clave ESTABLE de una columna del grid para el formato por celda
        /// (direcciones <see cref="ReportCellAddress"/>), o <c>null</c> si la columna
        /// no es exportable/formateable.
        ///
        /// Las columnas personalizadas usan su <see cref="ColumnaPersonalizada.NombreInterno"/>.
        /// La columna sintética de numeración "#" (<c>colNumero</c>) no es data-bound
        /// pero SÍ es exportable: se le asigna la clave canónica
        /// <see cref="ReportColumnDefaults.NumeroIdentificador"/> (<c>Numero</c>), la
        /// MISMA que anteponen PDF/Excel, de modo que el formato aplicado en el grid
        /// también aparece en los reportes.
        /// </summary>
        internal static string? ObtenerClaveColumnaFormato(DataGridViewColumn? columna)
        {
            if (columna is null) return null;

            if (columna.Tag is ColumnaPersonalizada col && !string.IsNullOrWhiteSpace(col.NombreInterno))
                return col.NombreInterno;

            if (string.Equals(columna.Name, ColNumeroNombre, StringComparison.OrdinalIgnoreCase))
                return ReportColumnDefaults.NumeroIdentificador;

            return null;
        }

        /// <summary>
        /// Identificador ESTABLE de la columna, o <c>null</c> si la columna no es
        /// exportable/formateable (p. ej. las columnas auxiliares sin
        /// <c>ColumnaPersonalizada</c>: colId). La columna de numeración "#" SÍ lo es
        /// (clave canónica <c>Numero</c>).
        /// </summary>
        internal static string? ObtenerNombreColumnaFormato(DataGridView grid, int columnIndex)
        {
            if (grid is null) return null;
            if (columnIndex < 0 || columnIndex >= grid.Columns.Count) return null;

            return ObtenerClaveColumnaFormato(grid.Columns[columnIndex]);
        }

        /// <summary>
        /// <c>true</c> si alguna columna del grid tiene la clave estable
        /// <paramref name="nombreColumna"/>. Es la lógica de <c>EsCeldaFormateable</c>
        /// extraída como función pura (verificable sin UI) y reutilizada por el grid.
        /// </summary>
        internal static bool EsNombreColumnaFormateable(DataGridView grid, string? nombreColumna)
        {
            if (grid is null || string.IsNullOrWhiteSpace(nombreColumna)) return false;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                var clave = ObtenerClaveColumnaFormato(col);
                if (clave is not null && string.Equals(clave, nombreColumna, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Convierte las celdas seleccionadas en direcciones estables, descartando
        /// filas sin entidad y columnas no exportables. NO deduplica: el
        /// coordinador (Ctrl / rangos discontinuos) es quien deduplica y filtra.
        /// </summary>
        internal static IReadOnlyList<ReportCellAddress> ConstruirDireccionesSeleccionadas(
            DataGridView grid,
            IEnumerable<DataGridViewCell> celdas)
        {
            var direcciones = new List<ReportCellAddress>();
            if (grid is null || celdas is null) return direcciones;

            foreach (var celda in celdas)
            {
                if (celda is null || celda.RowIndex < 0 || celda.ColumnIndex < 0) continue;

                var clave = ObtenerClaveFilaFormato(grid, celda.RowIndex);
                if (clave is null) continue;

                var columna = ObtenerNombreColumnaFormato(grid, celda.ColumnIndex);
                if (columna is null) continue;

                direcciones.Add(new ReportCellAddress(FormatoVistaPresupuesto, clave, columna));
            }

            return direcciones;
        }

        /// <summary>
        /// Construye el lote que <see cref="FormatoReporteService.AplicarCeldas"/>
        /// persiste: con <paramref name="aplicarAntes"/> se toma el estado "antes"
        /// (undo) y en otro caso el "después" (do/redo). Un overlay <c>null</c>
        /// indica BORRAR el override de esa celda.
        /// </summary>
        internal static IReadOnlyList<CambioFormatoCelda> ConstruirLoteCambios(
            IEnumerable<CambioFormatoSeleccion> cambios,
            bool aplicarAntes)
        {
            var lote = new List<CambioFormatoCelda>();
            if (cambios is null) return lote;

            foreach (var cambio in cambios)
            {
                if (cambio is null || cambio.Direccion is null) continue;

                var vista = string.IsNullOrEmpty(cambio.Direccion.ClaveVista)
                    ? FormatoVistaPresupuesto
                    : cambio.Direccion.ClaveVista;

                lote.Add(new CambioFormatoCelda(
                    vista,
                    cambio.Direccion.ClaveFila,
                    cambio.Direccion.NombreColumna,
                    aplicarAntes ? cambio.Antes : cambio.Despues));
            }

            return lote;
        }

        /// <summary>
        /// Carga (UNA sola consulta por proyecto/tipo) los overrides persistidos en
        /// el diccionario en memoria que consume el pintado. Se llama al abrir el
        /// form y en cada recarga del presupuesto; nunca por celda.
        /// </summary>
        private void CargarOverlaysFormato()
        {
            try
            {
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>(
                    _formatoReporteService.CargarCeldas(_proyecto.Id, FormatoTipoReporte));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CargarOverlaysFormato: {ex.Message}");
                _overlaysFormato = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
            }
        }

        /// <summary>
        /// Agrupa y persiste el lote aplicado desde el ribbon como UNA acción
        /// compuesta de undo. Los pares antes/después se capturaron en
        /// <see cref="AplicarOverlayCelda"/>; el flush se difiere un ciclo de
        /// mensajes para que TODAS las celdas de la selección formen un solo lote.
        /// </summary>
        private void ProgramarFlushFormato()
        {
            if (_flushFormatoProgramado) return;
            _flushFormatoProgramado = true;

            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(FlushFormatoPendiente));
            else
                FlushFormatoPendiente();
        }

        private void FlushFormatoPendiente()
        {
            _flushFormatoProgramado = false;
            if (_pendientesFormato.Count == 0) return;

            var cambios = ColapsarCambiosFormato(_pendientesFormato);
            _pendientesFormato.Clear();

            var cambiosUndo = cambios
                .Select(c => new UndoChange(c.Antes, c.Despues))
                .ToList();

            var accion = new CompositeUndoableAction(
                $"Formato de {cambios.Count} celda(s) en presupuesto",
                cambiosUndo,
                _ => AplicarCambiosFormato(cambios, aplicarAntes: true),
                _ => AplicarCambiosFormato(cambios, aplicarAntes: false));

            try
            {
                // Execute aplica el "después" (persiste + repinta) y sólo si tiene
                // éxito apila la acción; ante un fallo no queda nada a medias.
                _undoManager.Execute(accion);
            }
            catch (Exception ex)
            {
                RevertirCambiosFormato(cambios);
                MessageBox.Show(
                    $"No se pudo guardar el formato de las celdas:\n{ex.Message}",
                    "Formato",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Colapsa los cambios pendientes a UNA entrada por dirección: el primer
        /// "antes" observado y el último "después".
        /// </summary>
        private static List<CambioFormatoSeleccion> ColapsarCambiosFormato(
            IEnumerable<CambioFormatoSeleccion> pendientes)
        {
            var orden = new List<ReportCellAddress>();
            var antes = new Dictionary<ReportCellAddress, ReportStyleOverlay?>();
            var despues = new Dictionary<ReportCellAddress, ReportStyleOverlay>();

            foreach (var cambio in pendientes)
            {
                if (cambio is null || cambio.Direccion is null) continue;

                if (!antes.ContainsKey(cambio.Direccion))
                {
                    orden.Add(cambio.Direccion);
                    antes[cambio.Direccion] = cambio.Antes;
                }
                despues[cambio.Direccion] = cambio.Despues;
            }

            var resultado = new List<CambioFormatoSeleccion>(orden.Count);
            foreach (var direccion in orden)
                resultado.Add(new CambioFormatoSeleccion(direccion, antes[direccion], despues[direccion]));

            return resultado;
        }

        /// <summary>
        /// Persiste el lote (estado "antes" o "después") y actualiza el diccionario
        /// en memoria + el pintado. Usado tanto por el "do" como por undo/redo.
        /// </summary>
        private void AplicarCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios, bool aplicarAntes)
        {
            _formatoReporteService.AplicarCeldas(
                _proyecto.Id,
                FormatoTipoReporte,
                ConstruirLoteCambios(cambios, aplicarAntes));

            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, aplicarAntes ? cambio.Antes : cambio.Despues);

            dgvPresupuesto.Invalidate();
        }

        private void RevertirCambiosFormato(IReadOnlyList<CambioFormatoSeleccion> cambios)
        {
            foreach (var cambio in cambios)
                EstablecerOverlayEnMemoria(cambio.Direccion, cambio.Antes);

            dgvPresupuesto.Invalidate();
        }

        private void EstablecerOverlayEnMemoria(ReportCellAddress direccion, ReportStyleOverlay? overlay)
        {
            if (overlay is null)
                _overlaysFormato.Remove(direccion);
            else
                _overlaysFormato[direccion] = overlay;
        }
    }
}
