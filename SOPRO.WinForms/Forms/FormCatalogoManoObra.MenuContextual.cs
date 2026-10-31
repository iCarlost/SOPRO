using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Menú contextual: búsqueda de uso en matrices y configuración de columnas.
    /// </summary>
    public partial class FormCatalogoManoObra
    {

        private void DgvManoObra_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvManoObra.Rows[e.RowIndex];
            // Bajo CellSelect `clickedRow.Selected` es FALSO aunque haya celdas de esa
            // fila seleccionadas; con el helper detectamos si el clic cayó DENTRO de
            // la selección para no destruirla (regresión H7a).
            if (!DgvCeldaHelper.ClicDentroDeSeleccion(dgvManoObra, e.RowIndex))
            {
                dgvManoObra.ClearSelection();
                // Bajo CellSelect `clickedRow.Selected = true` es un no-op; hay que
                // seleccionar la celda clicada (o la fila completa si se pulsó el
                // encabezado) para que la copia del menú tenga contenido.
                if (e.ColumnIndex >= 0)
                    clickedRow.Cells[e.ColumnIndex].Selected = true;
                else
                    DgvCeldaHelper.SeleccionarFilaCompleta(dgvManoObra, e.RowIndex);
            }

            if (e.ColumnIndex >= 0)
                dgvManoObra.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _manoDeObraSeleccionada = clickedRow.DataBoundItem as ManoDeObra;
            if (_manoDeObraSeleccionada == null) return;

            bool puedeEditar = !(_proyectoId.HasValue && _manoDeObraSeleccionada?.Origen == OrigenInsumo.Maestro);

            var menu = new ContextMenuStrip();

            var itemEditar = menu.Items.Add("✏️  Editar");
            itemEditar.Enabled = puedeEditar;
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var idsEliminar = ObtenerIdsSeleccionados();
            var itemEliminar = menu.Items.Add($"🗑️  {CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.ManoDeObra, idsEliminar.Count)}");
            // La eliminación no se filtra por Origen: la elegibilidad real la decide
            // la previsualización con el ProyectoId de cada fila.
            itemEliminar.Enabled = idsEliminar.Count > 0;
            itemEliminar.Click += (_, __) => btnEliminar_Click(sender, EventArgs.Empty);

            menu.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            var matrices = BuscarMatricesDondeSeUsa_ManoDeObra(_manoDeObraSeleccionada.Id);

            if (matrices.Count == 0)
            {
                var sinUso = itemDonde.DropDownItems.Add("(Sin uso en ninguna matriz)");
                sinUso.Enabled = false;
            }
            else
            {
                foreach (var m in matrices)
                {
                    string label = m.Clave + " — " + (m.Descripcion?.Length > 50
                        ? m.Descripcion[..50] + "…" : m.Descripcion ?? "");
                    var item = itemDonde.DropDownItems.Add(label);
                    var cap = m;
                    item.Click += (_, __) => AbrirEditorMatriz_ManoDeObra(cap);
                }
            }
            menu.Items.Add(itemDonde);
            menu.Items.Add(new ToolStripSeparator());

            // H8: copia tipo Excel (bloque exacto de celdas o fila[s] completa[s] por
            // encabezado). La etiqueta refleja la selección actual.
            string etiquetaCopiar = DgvCeldaHelper.EtiquetaCopiar(dgvManoObra);
            if (etiquetaCopiar != "Copiar celda")
            {
                var itemCopiarCelda = menu.Items.Add("📋  Copiar celda");
                itemCopiarCelda.Click += (_, __) =>
                {
                    var cell = dgvManoObra.CurrentCell;
                    if (cell != null)
                    {
                        string txt = cell.FormattedValue?.ToString() ?? "";
                        if (txt.Length > 0) Clipboard.SetText(txt);
                    }
                };
            }

            var itemCopiar = menu.Items.Add($"📄  {etiquetaCopiar}");
            itemCopiar.Click += (_, __) => DgvCeldaHelper.CopiarSeleccionAlPortapapeles(dgvManoObra);

            menu.Show(dgvManoObra, dgvManoObra.PointToClient(Cursor.Position));
        }

        private List<Matriz> BuscarMatricesDondeSeUsa_ManoDeObra(int insumoId)
        {
            var matrizIds = _context.Set<ComponenteMatriz>()
                .Where(c => c.ManoDeObraId == insumoId)
                .Select(c => c.MatrizId)
                .Distinct()
                .ToList();

            if (matrizIds.Count == 0) return new List<Matriz>();

            return _context.Matrices
                .Where(m => matrizIds.Contains(m.Id) && (_proyectoId == null || m.ProyectoId == _proyectoId))
                .OrderBy(m => m.Clave)
                .ToList();
        }

        private void AbrirEditorMatriz_ManoDeObra(Matriz matriz)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId ?? 0, matriz);
            form.ShowDialog(this);
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("Para configurar columnas primero debe existir un proyecto activo.", "Columnas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var form = new FormColumnasAPU(_context, _proyectoId.Value, FormColumnasAPU.ModoColumnas.ManoObra);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                _columnasConfig = ColumnasManoObraHelper.ObtenerColumnas(_context, _proyectoId.Value);
                ConfigurarGrid();
                CargarManoDeObra();
            }
        }


        private static double CalcularAlturaFilaCatalogo(List<ColumnaManoObra> colsVis, int fila, IXLWorksheet ws, double alturaBase)
        {
            if (colsVis == null || colsVis.Count == 0) return alturaBase;

            double altura = alturaBase;
            for (int i = 0; i < colsVis.Count; i++)
            {
                var col = colsVis[i];
                if (!col.WrapTexto) continue;

                var valor = ws.Cell(fila, i + 1).GetFormattedString();
                if (string.IsNullOrWhiteSpace(valor)) continue;

                using var font = new Font(
                    string.IsNullOrWhiteSpace(col.NombreFuente) ? "Segoe UI" : col.NombreFuente,
                    Math.Max(8f, col.TamanoFuente > 0 ? col.TamanoFuente : 9f),
                    col.Negrita ? FontStyle.Bold : FontStyle.Regular);

                int anchoPx = Math.Max(24, (int)Math.Round(col.AnchoColumna - 8d));
                var proposed = new Size(anchoPx, int.MaxValue);
                var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
                var measured = TextRenderer.MeasureText(valor, font, proposed, flags);
                double alturaPts = Math.Max(alturaBase, measured.Height * 72.0 / 96.0 + 6);
                if (alturaPts > altura) altura = alturaPts;
            }

            return altura;
        }
    }
}
