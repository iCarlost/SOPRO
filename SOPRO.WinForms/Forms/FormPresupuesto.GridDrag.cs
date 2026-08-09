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
    }
}
