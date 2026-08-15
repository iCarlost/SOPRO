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
            if (!clickedRow.Selected)
            {
                dgvMateriales.ClearSelection();
                clickedRow.Selected = true;
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

            var itemEliminar = menu.Items.Add("🗑️  Eliminar");
            itemEliminar.Enabled = puedeEditar;
            itemEliminar.Click += (_, __) => btnEliminar_Click(sender, EventArgs.Empty);

            menu.Items.Add(new ToolStripSeparator());

            var itemDonde = new ToolStripMenuItem("📋  Dónde se usa");
            itemDonde.DropDownOpening += async (_, __) =>
                await CargarDondeSeUsaAsync(itemDonde, _materialSeleccionado.Id);
            menu.Items.Add(itemDonde);
            menu.Items.Add(new ToolStripSeparator());

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

            int _nFilas = dgvMateriales.SelectedRows.Count;
            string _labelFila = _nFilas > 1 ? $"📄  Copiar {_nFilas} filas" : "📄  Copiar fila";
            var itemCopiarFila = menu.Items.Add(_labelFila);
            itemCopiarFila.Click += (_, __) =>
            {
                var cols = Enumerable.Range(0, dgvMateriales.ColumnCount)
                    .Where(i => dgvMateriales.Columns[i].Visible)
                    .OrderBy(i => dgvMateriales.Columns[i].DisplayIndex).ToList();
                var sb = new System.Text.StringBuilder();
                foreach (DataGridViewRow r in dgvMateriales.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
                    sb.AppendLine(string.Join("\t", cols.Select(i => r.Cells[i].FormattedValue?.ToString() ?? "")));
                if (sb.Length > 0) Clipboard.SetText(sb.ToString().TrimEnd());
            };

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
