using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Implementación de IGridFormato y aplicación de estilos desde la BD.
    /// </summary>
    public partial class FormIndirectos
    {

        public bool GenerarReporteExcel()
        {
            btnExportar_Click(this, EventArgs.Empty);
            return true;
        }
        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaIndirectos _columnaIndRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _columnaIndRibbon = null;
            _columnaRibbon = null;
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            // Mapear índice visual a NombreInterno
            var mapaIdx = new[] { "Grupo", "ImporteMensual", "Duracion", "ImporteTotal" };
            if (colIndex >= 0 && colIndex < mapaIdx.Length)
            {
                var ni = mapaIdx[colIndex];
                var col = cols.FirstOrDefault(c => c.NombreInterno == ni);
                if (col != null)
                {
                    _columnaIndRibbon = col;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = col.Nombre,
                        NombreFuente = col.NombreFuente,
                        TamanoFuente = col.TamanoFuente,
                        Negrita = col.Negrita,
                        Cursiva = col.Cursiva,
                        Alineacion = col.Alineacion,
                        ColorFondo = col.ColorFondo,
                        ColorFuente = col.ColorFuente,
                        WrapTexto = col.WrapTexto,
                        AlineacionVertical = col.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_columnaIndRibbon == null) return;
            CopiarFormato(fmt, _columnaIndRibbon);
            _context.SaveChanges();
            AplicarEstilosDesdeDB();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            foreach (var col in cols)
            {
                CopiarFormato(fmt, col, excluirFondo: true);
            }
            _context.SaveChanges();
            AplicarEstilosDesdeDB();
        }

        private void CopiarFormato(ColumnaPersonalizada fmt, ColumnaIndirectos dest,
                                    bool excluirFondo = false)
        {
            dest.NombreFuente = fmt.NombreFuente;
            dest.TamanoFuente = fmt.TamanoFuente;
            dest.Negrita = fmt.Negrita;
            dest.Cursiva = fmt.Cursiva;
            dest.Alineacion = fmt.Alineacion;
            dest.ColorFuente = fmt.ColorFuente;
            if (!excluirFondo) dest.ColorFondo = fmt.ColorFondo;
            dest.WrapTexto = fmt.WrapTexto;
            dest.AlineacionVertical = fmt.AlineacionVertical;
            dest.FechaModificacion = DateTime.Now;
        }

        private void AplicarEstilosDesdeDB()
        {
            var cols = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);
            var mapaNames = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Grupo",          "colGrupo"          },
                { "ImporteMensual", "colImporteMensual"  },
                { "Duracion",       "colDuracion"        },
                { "ImporteTotal",   "colImporteTotal"    },
            };
            foreach (var col in cols)
            {
                if (!mapaNames.TryGetValue(col.NombreInterno, out string colName)) continue;
                AplicarEstiloAGrid(dgvOficinaCentral, colName, col);
                AplicarEstiloAGrid(dgvCampo, colName, col);
            }
            dgvOficinaCentral.Invalidate();
            dgvCampo.Invalidate();
        }

        private static void AplicarEstiloAGrid(DataGridView dgv, string colName,
                                                ColumnaIndirectos col)
        {
            if (!dgv.Columns.Contains(colName)) return;
            var dgvCol = dgv.Columns[colName] as System.Windows.Forms.DataGridViewTextBoxColumn;
            if (dgvCol == null) return;
            try
            {
                FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                dgvCol.DefaultCellStyle.Font = new Font(col.NombreFuente ?? "Segoe UI", col.TamanoFuente > 0 ? col.TamanoFuente : 9f, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColorInd(col.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColorInd(col.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                dgvCol.Width = col.AnchoColumna;
                dgvCol.Tag = col;
            }
            catch { }
        }

        private static Color TryColorInd(string hex, Color fallback)
        {
            try { return System.Drawing.ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }
    }
}
