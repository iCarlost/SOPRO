using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Configuración del grid y formato de celdas de la explosión.
    /// </summary>
    public partial class FormExplosionInsumos
    {

        private void FormExplosionInsumos_Load(object sender, EventArgs e)
        {
            ConfigurarGrid();
            CargarFiltros(); // Ya dispara GenerarExplosion() al hacer SelectedIndex = 0
        }
        
        private void ConfigurarGrid()
        {
            dgvExplosion.SuspendLayout();
            dgvExplosion.AutoGenerateColumns = false;
            dgvExplosion.AllowUserToAddRows = false;
            dgvExplosion.AllowUserToDeleteRows = false;
            dgvExplosion.ReadOnly = true;
            dgvExplosion.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvExplosion.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvExplosion.EnableHeadersVisualStyles = false;
            dgvExplosion.BackgroundColor = Color.White;
            dgvExplosion.GridColor = Color.FromArgb(230, 230, 230);
            dgvExplosion.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 247, 255);
            dgvExplosion.DefaultCellStyle.SelectionForeColor = Color.Black;

            dgvExplosion.AplicarEstiloSOPRO();
            Helpers.DgvCeldaHelper.Aplicar(dgvExplosion);
            _gridRenderService.ConfigureGrid(dgvExplosion);

            // Columnas desde BD
            dgvExplosion.Columns.Clear();

            var columnasBD = Helpers.ColumnasExplosionHelper.ObtenerColumnas(_context, _proyectoId);

            // Mapeo NombreInterno -> Name fijo que usa el código de llenado
            var mapaNames = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Clave",          "colClave"         },
                { "Descripcion",    "colDescripcion"   },
                { "Unidad",         "colUnidad"        },
                { "Cantidad",       "colCantidad"      },
                { "PrecioUnitario", "colPrecioUnitario"},
                { "Importe",        "colImporte"       },
                { "Porcentaje",     "colPorcentaje"    },
            };

            foreach (var colExp in columnasBD)
            {
                if (!mapaNames.TryGetValue(colExp.NombreInterno, out string colName)) continue;

                var dgvCol = new DataGridViewTextBoxColumn
                {
                    Name       = colName,
                    HeaderText = colExp.Nombre,
                    Width      = colExp.AnchoColumna,
                    Visible    = colExp.Visible,
                    Tag        = colExp,
                    SortMode   = DataGridViewColumnSortMode.NotSortable,
                    DefaultCellStyle =
                    {
                        Alignment = AlineacionADGV(colExp.Alineacion),
                        Format    = colExp.FormatoNumerico ?? string.Empty,
                    }
                };
                AplicarEstiloDesdeColExp(dgvCol, colExp);
                dgvExplosion.Columns.Add(dgvCol);
            }

            // Columna dummy al final para llenar espacio
            dgvExplosion.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name         = "colDummy",
                HeaderText   = "",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly     = true,
                SortMode     = DataGridViewColumnSortMode.NotSortable,
            });

            // Estilo de encabezados (base — CellPainting lo sobreescribe por columna)
            dgvExplosion.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvExplosion.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvExplosion.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvExplosion.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvExplosion.ColumnHeadersHeight = 35;

            // Suscribir CellPainting para encabezados con formato personalizado
            dgvExplosion.CellPainting -= DgvExplosion_CellPainting;
            dgvExplosion.CellPainting += DgvExplosion_CellPainting;
            dgvExplosion.ResumeLayout();
        }
        

        private void DgvExplosion_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (e?.Column == null) return;
            if (e.Column.Name == "colDummy") return;

            try
            {
                if (e.Column.Tag is not SOPRO.Core.Entities.ColumnaExplosion cfg) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                cfg.AnchoColumna = nuevoAncho;
                cfg.FechaModificacion = DateTime.Now;

                var columnaDb = _context.ColumnasExplosion.Find(cfg.Id);
                if (columnaDb == null)
                {
                    _context.SaveChanges();
                    return;
                }

                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en explosión: {ex.Message}");
            }
        }


        // ── Helpers de formato ────────────────────────────────────────────────

        private static void AplicarEstiloDesdeColExp(DataGridViewTextBoxColumn dgvCol, SOPRO.Core.Entities.ColumnaExplosion col)
        {
            try
            {
                FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                string fuente = !string.IsNullOrEmpty(col.NombreFuente) ? col.NombreFuente : "Segoe UI";
                float tam = col.TamanoFuente > 0 ? col.TamanoFuente : 9f;
                dgvCol.DefaultCellStyle.Font      = new Font(fuente, tam, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColor(col.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColor(col.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
            }
            catch { }
        }

        private static DataGridViewContentAlignment AlineacionADGV(AlineacionColumna alin) =>
            alin switch
            {
                AlineacionColumna.Centro      => DataGridViewContentAlignment.MiddleCenter,
                AlineacionColumna.Derecha     => DataGridViewContentAlignment.MiddleRight,
                AlineacionColumna.Justificado => DataGridViewContentAlignment.MiddleLeft,
                _                             => DataGridViewContentAlignment.MiddleLeft,
            };

        private static Color TryColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }

        private void DgvExplosion_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return;

            var col = dgvExplosion.Columns[e.ColumnIndex];
            if (col.Tag is not SOPRO.Core.Entities.ColumnaExplosion colDef)
            {
                // colDummy u otros sin Tag: pintar normal
                return;
            }

            _gridRenderService.PaintHeader(e, colDef);
        }

                private void CargarFiltros()
        {
            cmbFiltro.Items.Clear();
            cmbFiltro.Items.Add("Todos");
            cmbFiltro.Items.Add("Materiales");
            cmbFiltro.Items.Add("Mano de Obra");
            cmbFiltro.Items.Add("Maquinaria");
            cmbFiltro.Items.Add("Herramientas");
            cmbFiltro.SelectedIndex = 0;
        }
    }
}
