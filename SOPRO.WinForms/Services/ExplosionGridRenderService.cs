using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.Application.Models.Explosion;
using SOPRO.Core.Entities;

namespace SOPRO.WinForms.Services
{
    internal sealed class ExplosionGridRenderService
    {
        private static readonly Color HeaderBaseBackColor = Color.FromArgb(51, 51, 76);
        private static readonly Color HeaderBaseForeColor = Color.White;
        private static readonly Color HeaderBorderColor = Color.FromArgb(200, 200, 200);
        private static readonly Color TotalBackColor = Color.FromArgb(240, 240, 240);
        private static readonly Color TotalGeneralBackColor = Color.FromArgb(45, 45, 48);
        private static readonly Color ReferenceBackColor = Color.FromArgb(240, 240, 240);
        private static readonly Color ReferenceForeColor = Color.FromArgb(100, 100, 100);
        private static readonly Color SectionBackColor = Color.FromArgb(70, 130, 180);

        public void ConfigureGrid(DataGridView dgv)
        {
            if (dgv == null) return;

            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgv.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToResizeColumns = true;
            dgv.StandardTab = true;
            dgv.RowTemplate.Height = 28;
            dgv.ScrollBars = ScrollBars.Both;
        }

        public DataGridViewRow[] BuildRows(DataGridView dgv, IReadOnlyList<ExplosionRowDisplay> rows)
        {
            if (dgv == null || rows == null || rows.Count == 0)
                return System.Array.Empty<DataGridViewRow>();

            var gridRows = new List<DataGridViewRow>(rows.Count);
            foreach (var item in rows)
            {
                var row = new DataGridViewRow();
                row.CreateCells(dgv,
                    item.Clave,
                    item.Descripcion,
                    item.Unidad,
                    item.CantidadTexto,
                    item.PrecioUnitarioTexto,
                    item.ImporteTexto,
                    item.Porcentaje,
                    string.Empty);

                ApplyRowStyle(row, item);
                gridRows.Add(row);
            }

            return gridRows.ToArray();
        }

        public bool PaintHeader(DataGridViewCellPaintingEventArgs e, ColumnaExplosion colDef)
        {
            if (e == null || colDef == null) return false;

            Color fondo = HeaderBaseBackColor;
            Color texto = HeaderBaseForeColor;

            if (!string.IsNullOrEmpty(colDef.ColorFondo) && colDef.ColorFondo != "#FFFFFF")
                TrySetColor(colDef.ColorFondo, ref fondo);
            if (!string.IsNullOrEmpty(colDef.ColorFuente) && colDef.ColorFuente != "#000000")
                TrySetColor(colDef.ColorFuente, ref texto);

            using var brushFondo = new SolidBrush(fondo);
            using var penBorde = new Pen(HeaderBorderColor);
            using var brushTexto = new SolidBrush(texto);
            using var fuente = CreateHeaderFont(colDef);

            e.Graphics.FillRectangle(brushFondo, e.CellBounds);
            e.Graphics.DrawLine(penBorde,
                e.CellBounds.Left, e.CellBounds.Bottom - 1,
                e.CellBounds.Right, e.CellBounds.Bottom - 1);
            e.Graphics.DrawLine(penBorde,
                e.CellBounds.Right - 1, e.CellBounds.Top,
                e.CellBounds.Right - 1, e.CellBounds.Bottom);

            using var sf = new StringFormat
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

            var rect = new RectangleF(
                e.CellBounds.Left + 4, e.CellBounds.Top,
                e.CellBounds.Width - 8, e.CellBounds.Height);

            e.Graphics.DrawString(e.Value?.ToString() ?? string.Empty, fuente, brushTexto, rect, sf);
            e.Handled = true;
            return true;
        }

        private static Font CreateHeaderFont(ColumnaExplosion colDef)
        {
            string nombreFuente = !string.IsNullOrEmpty(colDef.NombreFuente) ? colDef.NombreFuente : "Segoe UI";
            float tamano = colDef.TamanoFuente > 0 ? colDef.TamanoFuente : 9f;
            FontStyle fs = FontStyle.Bold | (colDef.Cursiva ? FontStyle.Italic : FontStyle.Regular);
            return new Font(nombreFuente, tamano, fs);
        }

        private static void TrySetColor(string html, ref Color target)
        {
            try { target = ColorTranslator.FromHtml(html); } catch { }
        }

        private static void ApplyRowStyle(DataGridViewRow row, ExplosionRowDisplay item)
        {
            switch (item.Kind)
            {
                case ExplosionRowKind.Encabezado:
                    row.DefaultCellStyle.BackColor = SectionBackColor;
                    row.DefaultCellStyle.ForeColor = Color.White;
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                    row.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    row.Height = 30;
                    row.Frozen = false;
                    break;
                case ExplosionRowKind.Total:
                    row.DefaultCellStyle.BackColor = TotalBackColor;
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                    SetRightAlignment(row);
                    break;
                case ExplosionRowKind.TotalGeneral:
                    row.DefaultCellStyle.BackColor = TotalGeneralBackColor;
                    row.DefaultCellStyle.ForeColor = Color.White;
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    SetRightAlignment(row);
                    row.Height = 30;
                    break;
                case ExplosionRowKind.Vacia:
                    row.DefaultCellStyle.BackColor = Color.White;
                    row.DefaultCellStyle.SelectionBackColor = Color.White;
                    row.DefaultCellStyle.SelectionForeColor = Color.Black;
                    row.Height = 10;
                    break;
                case ExplosionRowKind.Referencia:
                    row.DefaultCellStyle.BackColor = ReferenceBackColor;
                    row.DefaultCellStyle.ForeColor = ReferenceForeColor;
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
                    break;
            }
        }

        private static void SetRightAlignment(DataGridViewRow row)
        {
            if (row.Cells.Count > 5)
                row.Cells[5].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (row.Cells.Count > 6)
                row.Cells[6].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }
}
