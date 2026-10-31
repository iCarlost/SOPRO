using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Menú contextual: búsqueda de uso en matrices y configuración de columnas.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        private void DgvMateriales_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;

            var clickedRow = dgvMateriales.Rows[e.RowIndex];
            // Bajo CellSelect `clickedRow.Selected` es FALSO aunque haya celdas de esa
            // fila seleccionadas; con el helper detectamos si el clic cayó DENTRO de
            // la selección para no destruirla (regresión H7a).
            if (!DgvCeldaHelper.ClicDentroDeSeleccion(dgvMateriales, e.RowIndex))
            {
                dgvMateriales.ClearSelection();
                // Bajo CellSelect `clickedRow.Selected = true` es un no-op; hay que
                // seleccionar la celda clicada (o la fila completa si se pulsó el
                // encabezado) para que la copia del menú tenga contenido.
                if (e.ColumnIndex >= 0)
                    clickedRow.Cells[e.ColumnIndex].Selected = true;
                else
                    DgvCeldaHelper.SeleccionarFilaCompleta(dgvMateriales, e.RowIndex);
            }

            if (e.ColumnIndex >= 0)
                dgvMateriales.CurrentCell = clickedRow.Cells[e.ColumnIndex];

            _materialSeleccionado = clickedRow.DataBoundItem as MaterialListItem;
            if (_materialSeleccionado == null) return;

            bool puedeEditar = !(_proyectoId.HasValue && _materialSeleccionado.Origen == OrigenInsumo.Maestro);

            var menu = new ContextMenuStrip();

            var itemEditar = menu.Items.Add("✏️  Editar");
            itemEditar.Enabled = puedeEditar;
            itemEditar.Click += (_, __) => btnEditar_Click(sender, EventArgs.Empty);

            var idsEliminar = ObtenerIdsSeleccionados();
            var itemEliminar = menu.Items.Add($"🗑️  {CatalogoBatchDeletionHelper.EtiquetaEliminar(CatalogItemKind.Material, idsEliminar.Count)}");
            // La eliminación no se filtra por Origen: la elegibilidad real la decide
            // la previsualización con el ProyectoId de cada fila.
            itemEliminar.Enabled = idsEliminar.Count > 0;
            itemEliminar.Click += (_, __) => btnEliminar_Click(sender, EventArgs.Empty);

            menu.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            itemDonde.DropDownOpening += async (_, __) =>
                await CargarDondeSeUsaAsync(itemDonde, _materialSeleccionado.Id);
            menu.Items.Add(itemDonde);
            menu.Items.Add(new ToolStripSeparator());

            // H8: copia tipo Excel (bloque exacto de celdas o fila[s] completa[s] por
            // encabezado). La etiqueta refleja la selección actual.
            string etiquetaCopiar = DgvCeldaHelper.EtiquetaCopiar(dgvMateriales);
            if (etiquetaCopiar != "Copiar celda")
            {
                var itemCopiarCelda = menu.Items.Add("📋  Copiar celda");
                itemCopiarCelda.Click += (_, __) =>
                {
                    var cell = dgvMateriales.CurrentCell;
                    if (cell != null)
                    {
                        string txt = cell.FormattedValue?.ToString() ?? "";
                        if (txt.Length > 0) Clipboard.SetText(txt);
                    }
                };
            }

            var itemCopiar = menu.Items.Add($"📄  {etiquetaCopiar}");
            itemCopiar.Click += (_, __) => DgvCeldaHelper.CopiarSeleccionAlPortapapeles(dgvMateriales);

            menu.Show(dgvMateriales, dgvMateriales.PointToClient(Cursor.Position));
        }

        /// <summary>
        /// "Dónde se usa": la consulta vive en el caso de uso headless
        /// (FindMatricesUsingMaterial) y devuelve DTOs, no entidades rastreadas.
        /// </summary>
        private async Task CargarDondeSeUsaAsync(ToolStripMenuItem itemDonde, int materialId)
        {
            itemDonde.DropDownItems.Clear();

            var result = await new FindMatricesUsingMaterial(_factory).Execute(
                _sessionInfo, new FindMatricesUsingMaterialRequest(materialId));

            if (!result.IsSuccess)
            {
                var errorItem = itemDonde.DropDownItems.Add(result.Error!.Message);
                errorItem.Enabled = false;
                return;
            }

            var matrices = result.Value!;
            if (matrices.Count == 0)
            {
                var sinUso = itemDonde.DropDownItems.Add("(Sin uso en ninguna matriz)");
                sinUso.Enabled = false;
                return;
            }

            foreach (var uso in matrices)
            {
                string label = uso.Clave + " — " + (uso.Descripcion?.Length > 50
                    ? uso.Descripcion[..50] + "…" : uso.Descripcion ?? "");
                var item = itemDonde.DropDownItems.Add(label);
                int matrizId = uso.MatrizId;
                item.Click += (_, __) =>
                {
                    var matriz = _context.Matrices.Find(matrizId);
                    if (matriz != null)
                        AbrirEditorMatriz(matriz);
                };
            }
        }

        private void AbrirEditorMatriz(Matriz matriz)
        {
            using var form = new FormEditarMatriz(_context, _proyectoId ?? 0, matriz);
            form.ShowDialog(this);
        }

        private static double CalcularAlturaFilaCatalogo(List<ColumnaMaterial> colsVis, int fila, IXLWorksheet ws, double alturaBase)
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
