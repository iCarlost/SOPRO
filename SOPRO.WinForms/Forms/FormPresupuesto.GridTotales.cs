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
    /// <summary>
    /// Formato condicional y totales de la jerarquía (agrupadores y recálculo global).
    /// </summary>
    public partial class FormPresupuesto
    {

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
    }
}
