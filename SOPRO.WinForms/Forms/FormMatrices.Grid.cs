using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Configuración del grid y formato de celdas del catálogo.
    /// </summary>
    public partial class FormMatrices
    {

        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvMatrices.AutoGenerateColumns = false;
            dgvMatrices.AllowUserToAddRows = false;
            dgvMatrices.AllowUserToDeleteRows = false;
            dgvMatrices.ReadOnly = true;
            dgvMatrices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMatrices.MultiSelect = true;
            dgvMatrices.BackgroundColor = Color.White;
            dgvMatrices.BorderStyle = BorderStyle.None;
            dgvMatrices.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvMatrices.GridColor = Color.FromArgb(230, 230, 230);
            dgvMatrices.ColumnHeadersHeight = 40;
            dgvMatrices.RowTemplate.Height = 35;
            dgvMatrices.EnableHeadersVisualStyles = false;
            dgvMatrices.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvMatrices.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMatrices.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvMatrices.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMatrices.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvMatrices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvMatrices.ScrollBars = ScrollBars.Both;

            DgvCeldaHelper.Aplicar(dgvMatrices, conMenuCopia: false);

            dgvMatrices.Columns.Clear();
            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    string dataProperty = cfg.NombreInterno switch
                    {
                        "Tipo" => nameof(MatrixGridRowDisplay.TipoTexto),
                        "NumInsumos" => nameof(MatrixGridRowDisplay.NumInsumos),
                        _ => cfg.NombreInterno,
                    };

                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = dataProperty,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvMatrices.Columns.Add(col);
                    AplicarEstiloDesdeColMat(col, cfg);
                }
            }
            else
            {
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = nameof(MatrixGridRowDisplay.Clave), Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = nameof(MatrixGridRowDisplay.Descripcion), Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = nameof(MatrixGridRowDisplay.Unidad), Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTipo", HeaderText = "Tipo", DataPropertyName = nameof(MatrixGridRowDisplay.TipoTexto), Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", DataPropertyName = nameof(MatrixGridRowDisplay.OrigenDetalle), Width = 180, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCostoDirecto", HeaderText = "Costo Directo", DataPropertyName = nameof(MatrixGridRowDisplay.CostoDirecto), Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "colComponentes", HeaderText = "# Insumos", DataPropertyName = nameof(MatrixGridRowDisplay.NumInsumos), Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            }

            dgvMatrices.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDummy",
                HeaderText = string.Empty,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });

            _cargandoColumnas = false;
        }

        private void DgvMatrices_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaMatriz cfg) return;

                var columnaDb = _context.ColumnasMatriz.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en matrices: {ex.Message}");
            }
        }

        private static void AplicarEstiloDesdeColMat(DataGridViewTextBoxColumn dgvCol, ColumnaMatriz col)
        {
            try
            {
                FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                string fuente = !string.IsNullOrEmpty(col.NombreFuente) ? col.NombreFuente : "Segoe UI";
                float tam = col.TamanoFuente > 0 ? col.TamanoFuente : 9f;
                dgvCol.DefaultCellStyle.Font = new Font(fuente, tam, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColor(col.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColor(col.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                if (!string.IsNullOrEmpty(col.FormatoNumerico))
                    dgvCol.DefaultCellStyle.Format = col.FormatoNumerico;
            }
            catch
            {
            }
        }

        private static DataGridViewContentAlignment AlineacionADGV(AlineacionColumna alin) =>
            alin switch
            {
                AlineacionColumna.Centro => DataGridViewContentAlignment.MiddleCenter,
                AlineacionColumna.Derecha => DataGridViewContentAlignment.MiddleRight,
                AlineacionColumna.Justificado => DataGridViewContentAlignment.MiddleLeft,
                _ => DataGridViewContentAlignment.MiddleLeft,
            };

        private static Color TryColor(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }
    }
}
