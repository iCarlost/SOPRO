using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Forms;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Identidad ESTABLE de una fila del grid de explosión para el formato de
    /// celdas: la clave neutral de la fila (<c>FormatoCeldaReporte.ClaveFila</c>) y
    /// el rol funcional de la fila (<see cref="RolFilaReporte"/>). Se guarda en
    /// <see cref="DataGridViewRow.Tag"/> al construir las filas, de modo que el
    /// mapeo selección → dirección no dependa de índices visuales.
    /// </summary>
    internal sealed record ExplosionFilaInfo(string? Clave, string? Rol);

    internal sealed class ExplosionGridRenderService
    {
        private static readonly Color HeaderBaseBackColor = Color.FromArgb(51, 51, 76);
        private static readonly Color HeaderBaseForeColor = Color.White;
        private static readonly Color HeaderBorderColor = Color.FromArgb(200, 200, 200);

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

        public DataGridViewRow[] BuildRows(
            DataGridView dgv,
            IReadOnlyList<ExplosionRowDisplay> rows,
            IReadOnlyDictionary<string, ReportStyleOverlay>? estilosFilaUsuario = null)
        {
            if (dgv == null || rows == null || rows.Count == 0)
                return System.Array.Empty<DataGridViewRow>();

            var gridRows = new List<DataGridViewRow>(rows.Count);
            string? seccion = null;
            int referenciaIndice = 0;
            foreach (var item in rows)
            {
                if (item.Kind == ExplosionRowKind.Encabezado)
                    seccion = item.Descripcion;

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

                row.Tag = ConstruirFilaInfo(item, seccion, ref referenciaIndice);
                ApplyRowStyle(dgv, row, item, estilosFilaUsuario);
                gridRows.Add(row);
            }

            return gridRows.ToArray();
        }

        /// <summary>
        /// Identidad estable de la fila (clave neutral + rol) según su tipo y la
        /// sección vigente. Devuelve una clave <c>null</c> para filas no formateables
        /// (filas vacías).
        /// </summary>
        private static ExplosionFilaInfo ConstruirFilaInfo(
            ExplosionRowDisplay item, string? seccion, ref int referenciaIndice)
        {
            var rol = ExplosionReportSnapshotBuilder.RolFila(item.Kind);
            string? clave = item.Kind switch
            {
                ExplosionRowKind.Encabezado when !string.IsNullOrWhiteSpace(seccion)
                    => ExplosionReportSnapshotBuilder.ClaveFilaSeccion(seccion),
                ExplosionRowKind.Detalle when !string.IsNullOrWhiteSpace(seccion)
                    => ExplosionReportSnapshotBuilder.ClaveFilaDetalle(seccion, item.Clave),
                ExplosionRowKind.Total when !string.IsNullOrWhiteSpace(seccion)
                    => ExplosionReportSnapshotBuilder.ClaveFilaTotalSeccion(seccion),
                ExplosionRowKind.TotalGeneral
                    => ExplosionReportSnapshotBuilder.ClaveFilaTotalGeneral,
                ExplosionRowKind.Referencia
                    => ExplosionReportSnapshotBuilder.ClaveFilaReferencia(referenciaIndice++),
                _ => null,
            };

            return new ExplosionFilaInfo(clave, rol);
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

        private static void ApplyRowStyle(
            DataGridView dgv,
            DataGridViewRow row,
            ExplosionRowDisplay item,
            IReadOnlyDictionary<string, ReportStyleOverlay>? estilosFilaUsuario)
        {
            // Base tipográfica del grid: el overlay de rol sólo pisa las propiedades
            // que define (fuente/tamaño/negrita/colores); las demás heredan la base.
            row.DefaultCellStyle.Font = dgv.Font;

            var rol = ExplosionReportSnapshotBuilder.RolFila(item.Kind);
            var overlay = EstilosFilaPredeterminados.CombinarExplosion(rol, estilosFilaUsuario);
            FormPresupuesto.AplicarOverlayACellStyle(row.DefaultCellStyle, overlay);

            switch (item.Kind)
            {
                case ExplosionRowKind.Encabezado:
                    // La banda de sección alinea su título a la izquierda (el color y
                    // la tipografía los aporta el default del rol Encabezado).
                    row.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    row.Height = 30;
                    row.Frozen = false;
                    break;
                case ExplosionRowKind.Total:
                    SetRightAlignment(row);
                    break;
                case ExplosionRowKind.TotalGeneral:
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
                    // Fondo/texto/cursiva los aporta el default del rol Referencia.
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
