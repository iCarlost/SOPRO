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
        // ── Estilos por ROL de fila persistidos (paso 4.3) ────────────────────
        // Overrides semánticos del usuario indexados por rol (CargarEstilosFila),
        // memoizados para no consultar la BD en cada CellFormatting.
        private Dictionary<string, ReportStyleOverlay>? _estilosFilaFormato;
        private int _estilosFilaFormatoProyectoId = -1;

        /// <summary>
        /// Overrides de estilo por rol persistidos del (proyecto, tipo), cargados
        /// con UNA sola consulta y memoizados por proyecto. El pintado los combina
        /// sobre los defaults de <see cref="EstilosFilaPredeterminados"/>
        /// (override del usuario &gt; default).
        /// </summary>
        private IReadOnlyDictionary<string, ReportStyleOverlay> ObtenerEstilosFilaUsuario()
        {
            if (_estilosFilaFormato is not null && _estilosFilaFormatoProyectoId == _proyecto.Id)
                return _estilosFilaFormato;

            try
            {
                _estilosFilaFormato = new Dictionary<string, ReportStyleOverlay>(
                    _formatoReporteService.CargarEstilosFila(_proyecto.Id, FormatoTipoReporte),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ObtenerEstilosFilaUsuario: {ex.Message}");
                _estilosFilaFormato = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);
            }

            _estilosFilaFormatoProyectoId = _proyecto.Id;
            return _estilosFilaFormato;
        }

        /// <summary>
        /// Rol funcional de fila (ver <see cref="RolFilaReporte"/>) correspondiente
        /// al tipo de renglón del grid. Coincide con el que expone
        /// <see cref="Application.UseCases.Reporting.PresupuestoReportSnapshotBuilder.RolFila"/>
        /// para PDF/Excel, de modo que los tres renderers resuelven el MISMO default.
        /// </summary>
        internal static string? ObtenerRolFilaFormato(string? tipo) => tipo switch
        {
            "Capitulo" => RolFilaReporte.Capitulo,
            "Subcapitulo" => RolFilaReporte.Subcapitulo,
            "Nivel 1" => RolFilaReporte.Nivel1,
            "Nivel 2" => RolFilaReporte.Nivel2,
            "Nivel 3" => RolFilaReporte.Nivel3,
            "Concepto" => RolFilaReporte.Concepto,
            _ => null,
        };

        /// <summary>
        /// Vuelca el estilo EFECTIVO del rol (default + override persistido del
        /// usuario) sobre el estilo de la celda. Sin configuración reproduce el
        /// pintado histórico del grid.
        /// </summary>
        internal static void AplicarEstiloRolPresupuesto(
            DataGridViewCellStyle estilo,
            Font fontBase,
            string? tipo,
            IReadOnlyDictionary<string, ReportStyleOverlay>? estilosFilaUsuario)
        {
            if (estilo is null) return;

            var overlay = EstilosFilaPredeterminados.CombinarPresupuesto(
                ObtenerRolFilaFormato(tipo), estilosFilaUsuario);

            var backColor = Color.White;
            var foreColor = Color.Black;

            if (!string.IsNullOrWhiteSpace(overlay.ColorFondo))
            {
                try { backColor = ColorTranslator.FromHtml(overlay.ColorFondo); } catch { }
            }

            if (!string.IsNullOrWhiteSpace(overlay.ColorFuente))
            {
                try { foreColor = ColorTranslator.FromHtml(overlay.ColorFuente); } catch { }
            }

            var fontStyle = FontStyle.Regular;
            if (overlay.Negrita == true) fontStyle |= FontStyle.Bold;
            if (overlay.Cursiva == true) fontStyle |= FontStyle.Italic;

            var nombre = string.IsNullOrWhiteSpace(overlay.Fuente) ? fontBase.Name : overlay.Fuente!;
            var tamano = overlay.TamanoFuente is > 0 ? overlay.TamanoFuente!.Value : fontBase.Size;

            estilo.BackColor = backColor;
            estilo.ForeColor = foreColor;
            try { estilo.Font = new Font(nombre, tamano, fontStyle); } catch { }
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

            // ── 1. Estilo semántico por ROL de fila ───────────────────────────
            // Los colores/estilos de agrupador (Capitulo/Subcapitulo/Nivel) y de
            // cierre salen de EstilosFilaPredeterminados (ÚNICA fuente de verdad,
            // compartida por grid/PDF/Excel) combinados con el override PERSISTIDO
            // del usuario. El override de celda del paso 3 SIGUE ganando a todo.
            AplicarEstiloRolPresupuesto(e.CellStyle, dgvPresupuesto.Font, tipo, ObtenerEstilosFilaUsuario());

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
                        // La negrita/cursiva proviene del ROL (default o config del
                        // usuario) ya aplicado en el paso 1; la columna sólo ajusta
                        // la fuente/tamaño y puede añadir cursiva.
                        FontStyle fs = (e.CellStyle.Font?.Style ?? FontStyle.Regular)
                                     | (colDef.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                        e.CellStyle.Font = new Font(nombreFuente, tamaño, fs);
                    }
                    catch { }
                }
            }

            // ── 3. Override por celda (máxima precedencia) ────────────────────
            // Si la celda tiene un override persistido/aplicado, GANA sobre el
            // color hardcodeado del agrupador y sobre el formato de columna.
            AplicarOverrideFormatoCelda(e);
        }

        /// <summary>
        /// Aplica, si existe, el override en memoria de la celda (fila × columna)
        /// sobre el estilo del evento. Sólo consulta el diccionario en memoria: no
        /// hace ninguna consulta a la base de datos por celda.
        /// </summary>
        private void AplicarOverrideFormatoCelda(DataGridViewCellFormattingEventArgs e)
        {
            if (_overlaysFormato.Count == 0) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var clave = ObtenerClaveFilaFormato(dgvPresupuesto, e.RowIndex);
            var columna = ObtenerNombreColumnaFormato(dgvPresupuesto, e.ColumnIndex);
            if (clave is null || columna is null) return;

            var direccion = new ReportCellAddress(FormatoVistaPresupuesto, clave, columna);
            if (!_overlaysFormato.TryGetValue(direccion, out var overlay) || overlay is null) return;

            AplicarOverlayACellStyle(e.CellStyle, overlay);
        }

        /// <summary>
        /// Vuelca un <see cref="ReportStyleOverlay"/> neutral sobre un
        /// <see cref="DataGridViewCellStyle"/>: sólo pisa las propiedades NO nulas
        /// (null = heredar el estilo base que ya trae la celda).
        /// </summary>
        internal static void AplicarOverlayACellStyle(DataGridViewCellStyle estilo, ReportStyleOverlay overlay)
        {
            if (estilo is null || overlay is null) return;

            if (!string.IsNullOrWhiteSpace(overlay.ColorFondo))
            {
                try { estilo.BackColor = ColorTranslator.FromHtml(overlay.ColorFondo); } catch { }
            }

            if (!string.IsNullOrWhiteSpace(overlay.ColorFuente))
            {
                try { estilo.ForeColor = ColorTranslator.FromHtml(overlay.ColorFuente); } catch { }
            }

            if (overlay.Negrita is not null || overlay.Cursiva is not null
                || !string.IsNullOrWhiteSpace(overlay.Fuente) || overlay.TamanoFuente is not null)
            {
                var baseFont = estilo.Font ?? SystemFonts.DefaultFont;
                var estiloFuente = FontStyle.Regular;
                if (overlay.Negrita == true) estiloFuente |= FontStyle.Bold;
                if (overlay.Cursiva == true) estiloFuente |= FontStyle.Italic;

                var nombre = string.IsNullOrWhiteSpace(overlay.Fuente) ? baseFont.Name : overlay.Fuente!;
                var tamano = overlay.TamanoFuente is > 0 ? overlay.TamanoFuente!.Value : baseFont.Size;
                try { estilo.Font = new Font(nombre, tamano, estiloFuente); } catch { }
            }

            if (overlay.AlineacionHorizontal is not null || overlay.AlineacionVertical is not null)
                estilo.Alignment = CombinarAlineacionDgv(overlay.AlineacionHorizontal, overlay.AlineacionVertical);

            if (overlay.Wrap is not null)
                estilo.WrapMode = overlay.Wrap.Value ? DataGridViewTriState.True : DataGridViewTriState.False;
        }

        private static DataGridViewContentAlignment CombinarAlineacionDgv(
            ReportTextAlignment? horizontal,
            ReportVerticalAlignment? vertical)
        {
            bool arriba = vertical == ReportVerticalAlignment.Superior;
            bool abajo = vertical == ReportVerticalAlignment.Inferior;

            return horizontal switch
            {
                ReportTextAlignment.Centro => arriba ? DataGridViewContentAlignment.TopCenter
                    : abajo ? DataGridViewContentAlignment.BottomCenter
                    : DataGridViewContentAlignment.MiddleCenter,
                ReportTextAlignment.Derecha => arriba ? DataGridViewContentAlignment.TopRight
                    : abajo ? DataGridViewContentAlignment.BottomRight
                    : DataGridViewContentAlignment.MiddleRight,
                _ => arriba ? DataGridViewContentAlignment.TopLeft
                    : abajo ? DataGridViewContentAlignment.BottomLeft
                    : DataGridViewContentAlignment.MiddleLeft,
            };
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
                    concepto.CostoDirectoTotal = BudgetPricingService.MultiplyUsingDisplayPrecision(_proyecto,
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
