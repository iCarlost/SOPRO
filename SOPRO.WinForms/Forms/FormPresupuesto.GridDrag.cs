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
    /// Interacción del grid: navegación, drag &amp; drop de filas, ancho/orden de columnas
    /// y asignación de conceptos por clave (draft).
    /// </summary>
    public partial class FormPresupuesto
    {
        private BudgetGridInteractionService.RowDragBlock _dragBloqueArrastre =
            BudgetGridInteractionService.RowDragBlock.Empty;
        private List<int> _dragFilasSeleccionadasMouseDown = new();
        private bool _dragSeleccionMultiple;

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

            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (dgvPresupuesto.Columns[e.ColumnIndex].Tag is not ColumnaPersonalizada colDefEnter
                    || !string.Equals(colDefEnter.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase))
                {
                    OcultarAutocompleteApu(false);
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
                LimpiarEstadoArrastre();
                return;
            }

            var hit = dgvPresupuesto.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0)
            {
                LimpiarEstadoArrastre();
                return;
            }

            var tipoCell = ObtenerCeldaPorNombreInterno(hit.RowIndex, "Tipo");
            if (tipoCell?.Value == null || string.IsNullOrWhiteSpace(tipoCell.Value.ToString()))
            {
                LimpiarEstadoArrastre();
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

            // WinForms colapsa la selección justo después de MouseDown: aquí todavía
            // se puede capturar la multi-selección completa de filas.
            _dragFilasSeleccionadasMouseDown = ObtenerFilasCompletamenteSeleccionadas();
            _dragBloqueArrastre = BudgetGridInteractionService.RowDragBlock.Empty;
            _dragSeleccionMultiple = false;
        }

        /// <summary>
        /// Captura la selección de filas completas vigente antes de que WinForms la
        /// colapse y resuelve el bloque de arrastre con la jerarquía actual. La usa
        /// MouseMove (y las pruebas) una vez conocido el origen real del arrastre.
        /// </summary>
        internal BudgetGridInteractionService.RowDragBlock CapturarBloqueArrastre(int originRow)
        {
            _dragFilasSeleccionadasMouseDown = ObtenerFilasCompletamenteSeleccionadas();
            return ConstruirBloqueArrastre(originRow, _dragFilasSeleccionadasMouseDown);
        }

        private BudgetGridInteractionService.RowDragBlock ConstruirBloqueArrastre(
            int originRow,
            IReadOnlyList<int> filasSeleccionadas)
        {
            var bloque = BudgetGridInteractionService.BuildRowDragBlock(
                originRow,
                filasSeleccionadas,
                BuildHierarchyRowsSnapshot());

            _dragBloqueArrastre = bloque;
            _dragSeleccionMultiple = bloque.IsMultiSelection;
            return bloque;
        }

        private List<int> ObtenerFilasCompletamenteSeleccionadas()
        {
            var filasSeleccionadas = new List<int>();
            var incluidas = new HashSet<int>();
            foreach (DataGridViewRow fila in dgvPresupuesto.SelectedRows)
            {
                if (fila.Index >= 0 && incluidas.Add(fila.Index))
                    filasSeleccionadas.Add(fila.Index);
            }

            // Bajo CellSelect una fila completa seleccionada por encabezado no
            // siempre aparece en SelectedRows; se reconoce por tener todas sus
            // celdas visibles seleccionadas. Un rango parcial no cuenta como fila
            // completa. Solo se revisan las filas con alguna celda seleccionada.
            var filasConCeldas = new HashSet<int>();
            foreach (DataGridViewCell celda in dgvPresupuesto.SelectedCells)
            {
                if (celda.RowIndex >= 0)
                    filasConCeldas.Add(celda.RowIndex);
            }

            foreach (int indice in filasConCeldas)
            {
                if (incluidas.Contains(indice)) continue;

                var fila = dgvPresupuesto.Rows[indice];
                bool completa = true;
                int celdasVisibles = 0;
                foreach (DataGridViewColumn columna in dgvPresupuesto.Columns)
                {
                    if (!columna.Visible) continue;

                    celdasVisibles++;
                    if (!fila.Cells[columna.Index].Selected)
                    {
                        completa = false;
                        break;
                    }
                }

                if (completa && celdasVisibles > 0 && incluidas.Add(indice))
                    filasSeleccionadas.Add(indice);
            }

            filasSeleccionadas.Sort();
            return filasSeleccionadas;
        }

        private void LimpiarEstadoArrastre()
        {
            _dragFilaOrigen = -1;
            _dragFilaMouseDown = -1;
            _dragMouseDownLocation = Point.Empty;
            _dragStartRect = Rectangle.Empty;
            _dragBloqueArrastre = BudgetGridInteractionService.RowDragBlock.Empty;
            _dragFilasSeleccionadasMouseDown = new List<int>();
            _dragSeleccionMultiple = false;

            if (_dragLineaInsercion != -1)
            {
                _dragLineaInsercion = -1;
                dgvPresupuesto.Invalidate();
            }
        }

        private void DgvPresupuesto_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragFilaMouseDown = -1;
                _dragMouseDownLocation = Point.Empty;
                _dragStartRect = Rectangle.Empty;

                // Si no hubo arrastre, la captura de MouseDown ya no sirve. Durante
                // un arrastre real el bloque se conserva hasta que DragDrop termina.
                if (_dragFilaOrigen < 0)
                {
                    _dragBloqueArrastre = BudgetGridInteractionService.RowDragBlock.Empty;
                    _dragFilasSeleccionadasMouseDown = new List<int>();
                    _dragSeleccionMultiple = false;
                }

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
            if (!_dragBloqueArrastre.IsValid || _dragBloqueArrastre.OriginRow != _dragFilaOrigen)
                ConstruirBloqueArrastre(_dragFilaOrigen, _dragFilasSeleccionadasMouseDown);

            _dragFilaMouseDown = -1;
            _dragMouseDownLocation = Point.Empty;
            _dragStartRect = Rectangle.Empty;
            try
            {
                dgvPresupuesto.DoDragDrop(_dragFilaOrigen, DragDropEffects.Move);
            }
            finally
            {
                LimpiarEstadoArrastre();
            }
        }

        private void DgvPresupuesto_DragOver(object sender, DragEventArgs e)
        {
            var pt = dgvPresupuesto.PointToClient(new Point(e.X, e.Y));
            var hit = dgvPresupuesto.HitTest(pt.X, pt.Y);

            int lineaDestino = -1;
            if (hit.RowIndex >= 0 && _dragBloqueArrastre.IsValid)
            {
                var ubicacion = BudgetGridInteractionService.ResolveRowDragPlacement(
                    _dragBloqueArrastre.Rows,
                    hit.RowIndex,
                    ObtenerUltimaFilaConDatos(),
                    dgvPresupuesto.Rows.Count);

                if (ubicacion.IsValid && !ubicacion.IsNoOp)
                    lineaDestino = ubicacion.IndicatorRow;
            }

            // Soltar dentro del propio bloque es un no-op: no se muestra un
            // indicador engañoso dentro de las filas que se arrastran.
            e.Effect = lineaDestino >= 0 ? DragDropEffects.Move : DragDropEffects.None;

            if (_dragLineaInsercion != lineaDestino)
            {
                _dragLineaInsercion = lineaDestino;
                dgvPresupuesto.Invalidate(); // Redibujar para mostrar línea
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

            var pt = dgvPresupuesto.PointToClient(new Point(e.X, e.Y));
            var hit = dgvPresupuesto.HitTest(pt.X, pt.Y);
            if (hit.RowIndex < 0)
            {
                LimpiarEstadoArrastre();
                return;
            }

            AplicarSoltarArrastre(hit.RowIndex);
        }

        /// <summary>
        /// Aplica el soltar del bloque capturado en MouseDown: reordena la vista,
        /// restaura la selección y persiste UNA sola vez vía
        /// <see cref="EjecutarReordenPersistido"/>. Devuelve <c>false</c> cuando el
        /// destino no produce movimiento (incluido soltar dentro del propio bloque).
        /// </summary>
        internal bool AplicarSoltarArrastre(int filaDestino)
        {
            if (!_dragBloqueArrastre.IsValid || filaDestino < 0)
            {
                LimpiarEstadoArrastre();
                return false;
            }

            var ubicacion = BudgetGridInteractionService.ResolveRowDragPlacement(
                _dragBloqueArrastre.Rows,
                filaDestino,
                ObtenerUltimaFilaConDatos(),
                dgvPresupuesto.Rows.Count);

            if (!ubicacion.IsValid || ubicacion.IsNoOp)
            {
                LimpiarEstadoArrastre();
                return false;
            }

            var filasAMover = _dragBloqueArrastre.Rows
                .Where(i => i >= 0 && i < dgvPresupuesto.Rows.Count)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (filasAMover.Count == 0)
            {
                LimpiarEstadoArrastre();
                return false;
            }

            bool seleccionMultiple = _dragSeleccionMultiple;

            // Estrategia: mover DataGridViewRow completas (no solo valores) para no
            // perder Tags, estilos ni fórmulas de celda.
            var rowsAMover = new List<DataGridViewRow>(filasAMover.Count);
            foreach (int idx in filasAMover)
                rowsAMover.Add(dgvPresupuesto.Rows[idx]);

            for (int i = filasAMover.Count - 1; i >= 0; i--)
                dgvPresupuesto.Rows.RemoveAt(filasAMover[i]);

            int insertEn = Math.Clamp(ubicacion.InsertionIndex, 0, dgvPresupuesto.Rows.Count);
            foreach (var row in rowsAMover)
                dgvPresupuesto.Rows.Insert(insertEn++, row);

            LimpiarEstadoArrastre();

            // LIMPIAR solo filas vacías que están ENTRE contenido (gaps)
            RemoveIntermediateEmptyRows();

            // Restaurar la selección sobre las filas realmente movidas. Un arrastre
            // simple conserva el comportamiento histórico (solo la primera fila).
            var indicesMovidos = rowsAMover
                .Where(r => r.Index >= 0)
                .Select(r => r.Index)
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            if (indicesMovidos.Count > 0)
            {
                int primerIndice = indicesMovidos[0];
                dgvPresupuesto.ClearSelection();
                dgvPresupuesto.CurrentCell = dgvPresupuesto.Rows[primerIndice].Cells[ObtenerColumnaFoco()];

                // Bajo CellSelect hay que marcar las celdas: asignar
                // DataGridViewRow.Selected no puebla la selección de celdas.
                var filasASeleccionar = seleccionMultiple
                    ? indicesMovidos
                    : new List<int> { primerIndice };

                foreach (int indice in filasASeleccionar)
                {
                    foreach (DataGridViewColumn columna in dgvPresupuesto.Columns)
                    {
                        if (columna.Visible)
                            dgvPresupuesto.Rows[indice].Cells[columna.Index].Selected = true;
                    }
                }
            }

            // Persistir el nuevo orden como comando de una sola escritura sobre un
            // contexto nuevo. Los Tags no se tocan antes de conocer el resultado; si
            // hay conflicto el borrador visual se conserva y no se guarda nada.
            EjecutarReordenPersistido();
            dgvPresupuesto.Refresh();
            return true;
        }

        private int ObtenerColumnaFoco()
        {
            int columnaFoco = dgvPresupuesto.CurrentCell?.ColumnIndex ?? 0;
            if (columnaFoco < 0 || columnaFoco >= dgvPresupuesto.Columns.Count)
                columnaFoco = 0;

            if (columnaFoco < dgvPresupuesto.Columns.Count && !dgvPresupuesto.Columns[columnaFoco].Visible)
            {
                columnaFoco = dgvPresupuesto.Columns
                    .Cast<DataGridViewColumn>()
                    .Where(c => c.Visible)
                    .Select(c => c.Index)
                    .DefaultIfEmpty(0)
                    .First();
            }

            return columnaFoco;
        }

        private void DgvPresupuesto_Paint(object sender, PaintEventArgs e)
        {
            // Dibujar línea de inserción durante drag & drop. Un indicador igual a
            // Rows.Count representa insertar al final, debajo de la última fila.
            if (_dragLineaInsercion >= 0 && dgvPresupuesto.Rows.Count > 0)
            {
                try
                {
                    int y;
                    if (_dragLineaInsercion < dgvPresupuesto.Rows.Count)
                    {
                        var rect = dgvPresupuesto.GetRowDisplayRectangle(_dragLineaInsercion, false);
                        if (rect.Height <= 0) return;
                        y = rect.Top;
                    }
                    else
                    {
                        var rectUltima = dgvPresupuesto.GetRowDisplayRectangle(dgvPresupuesto.Rows.Count - 1, false);
                        if (rectUltima.Height <= 0) return;
                        y = rectUltima.Bottom;
                    }

                    using (var pen = new Pen(Color.FromArgb(33, 150, 243), 3))
                    {
                        // Línea horizontal gruesa en el borde de inserción
                        e.Graphics.DrawLine(pen, 0, y, dgvPresupuesto.Width, y);

                        // Triángulos en los extremos (estilo Excel)
                        var triangleSize = 6;
                        Point[] leftTriangle = {
                            new Point(0, y - triangleSize),
                            new Point(triangleSize, y),
                            new Point(0, y + triangleSize)
                        };
                        Point[] rightTriangle = {
                            new Point(dgvPresupuesto.Width, y - triangleSize),
                            new Point(dgvPresupuesto.Width - triangleSize, y),
                            new Point(dgvPresupuesto.Width, y + triangleSize)
                        };

                        using (var brush = new SolidBrush(Color.FromArgb(33, 150, 243)))
                        {
                            e.Graphics.FillPolygon(brush, leftTriangle);
                            e.Graphics.FillPolygon(brush, rightTriangle);
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

        private void DgvPresupuesto_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            OcultarAutocompleteApu(false);
            // Guardar el nuevo ancho cuando el usuario lo cambia
            if (_cargando)
            {
                System.Diagnostics.Debug.WriteLine($"ColumnWidthChanged: Ignorado porque _cargando=true");
                return;
            }

            // Tras un resultado incierto no reconciliado no se persiste nada con los
            // Ids de columna que la vista conserva.
            if (_columnasBloqueadasPorResultadoIncierto)
            {
                System.Diagnostics.Debug.WriteLine(
                    "ColumnWidthChanged: ignorado por resultado incierto de columnas");
                return;
            }

            try
            {
                if (e.Column.Tag is ColumnaPersonalizada colDef && colDef.Id > 0)
                {
                    // Escritura por servicio con contexto propio: no se toca el
                    // contexto compartido mientras el usuario redimensiona.
                    var resultado = BudgetColumnConfigurationService.UpdateWidth(
                        _factory, _context.DatabasePath, _proyecto.Id, colDef.Id, e.Column.Width);

                    if (!resultado.IsCommitted)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"ColumnWidthChanged: no confirmado para '{colDef.Nombre}' (ID={colDef.Id}): {resultado.Status}");
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

        private void ApplyAssignmentDraftToGrid(int rowIndex, SOPRO.Application.Models.Presupuesto.BudgetConceptAssignmentDraft draft)
        {
            var claveCell = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
            var descCell = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion");
            var unidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Unidad");
            var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
            var puCell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
            var importeCell = ObtenerCeldaPorNombreInterno(rowIndex, "Importe");

            // Estas asignaciones son el borrador del propio asignador de matriz: no
            // deben disparar los comandos por celda (cantidad/texto) antes de que el
            // servicio de asignación aplique el draft completo.
            bool asignandoPrevio = _asignandoMatriz;
            _asignandoMatriz = true;
            try
            {
                if (claveCell != null) claveCell.Value = draft.Clave;
                if (descCell != null) descCell.Value = draft.Descripcion;
                if (unidadCell != null) unidadCell.Value = draft.Unidad;
                if (cantidadCell != null) cantidadCell.Value = draft.Cantidad;
                if (puCell != null) puCell.Value = draft.PrecioUnitario.ToStringImporte();
                if (importeCell != null) importeCell.Value = draft.ImporteTotal.ToStringImporte();
            }
            finally
            {
                _asignandoMatriz = asignandoPrevio;
            }
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
    }
}
