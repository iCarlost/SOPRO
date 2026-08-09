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

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Configuración del grid y formato de celdas del catálogo.
    /// </summary>
    public partial class FormCatalogoHerramientas
    {

        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvHerramientas.AutoGenerateColumns = false;
            dgvHerramientas.AllowUserToAddRows = false;
            dgvHerramientas.AllowUserToDeleteRows = false;
            dgvHerramientas.ReadOnly = true;
            dgvHerramientas.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvHerramientas, conMenuCopia: false);
            dgvHerramientas.CellMouseDown += DgvHerramientas_CellMouseDown;
            dgvHerramientas.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvHerramientas.BackgroundColor = Color.White;
            dgvHerramientas.BorderStyle = BorderStyle.None;
            dgvHerramientas.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvHerramientas.GridColor = Color.FromArgb(230, 230, 230);
            dgvHerramientas.ColumnHeadersHeight = 40;
            dgvHerramientas.RowTemplate.Height = 35;

            dgvHerramientas.EnableHeadersVisualStyles = false;
            dgvHerramientas.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvHerramientas.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvHerramientas.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvHerramientas.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvHerramientas.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvHerramientas.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvHerramientas.ScrollBars = ScrollBars.Both;

            dgvHerramientas.Columns.Clear();

            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = (cfg.NombreInterno == "PrecioUnitario" || cfg.NombreInterno == "OrigenDetalle") ? null : cfg.NombreInterno,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvHerramientas.Columns.Add(col);
                    AplicarEstiloDesdeColHer(col, cfg);
                }

                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "colDummy",
                    HeaderText = "",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }
            else
            {
                // Fallback sin proyecto
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = "Clave", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = "Unidad", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrecio", HeaderText = "Precio/Porcentaje", Width = 150, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 180, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
                dgvHerramientas.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "colDummy",
                    HeaderText = "",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }

            // CellFormatting especial: precio vs porcentaje según EsPorcentajeMO
            dgvHerramientas.CellFormatting += DgvHerramientas_CellFormatting;
            dgvHerramientas.KeyDown += DgvHerramientas_KeyDown;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Cat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Cat;

            _cargandoColumnas = false;
        }

        private void DgvHerramientas_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaHerramienta cfg) return;

                var columnaDb = _context.ColumnasHerramienta.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                cfg.AnchoColumna = nuevoAncho;
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en herramientas: {ex.Message}");
            }
        }

        private void DgvHerramientas_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var colName = dgvHerramientas.Columns[e.ColumnIndex].Name;
            if (dgvHerramientas.Rows[e.RowIndex].DataBoundItem is not Herramienta h) return;

            if (colName == "col_OrigenDetalle" || colName == "colOrigen")
            {
                e.Value = ImportOriginStampService.BuildOriginDisplay(h.Notas, h.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
                e.FormattingApplied = true;
                return;
            }

            if (e.Value == null) return;
            if (colName != "col_PrecioUnitario" && colName != "colPrecio") return;

            decimal valor;
            if (e.Value is decimal d)
            {
                valor = d;
            }
            else if (e.Value is IConvertible)
            {
                try
                {
                    valor = Convert.ToDecimal(e.Value);
                }
                catch
                {
                    var texto = e.Value?.ToString()?.Trim() ?? string.Empty;
                    texto = texto.Replace("%", string.Empty)
                                 .Replace("$", string.Empty)
                                 .Replace(",", string.Empty);

                    if (!decimal.TryParse(texto, out valor))
                        return;
                }
            }
            else
            {
                var texto = e.Value?.ToString()?.Trim() ?? string.Empty;
                texto = texto.Replace("%", string.Empty)
                             .Replace("$", string.Empty)
                             .Replace(",", string.Empty);

                if (!decimal.TryParse(texto, out valor))
                    return;
            }

            int _decHer = _proyectoId.HasValue ? (_context.Proyectos.Find(_proyectoId.Value)?.DecimalesImporte ?? 2) : 2;
            e.Value = h.EsPorcentajeMO ? $"{valor:N2}%" : valor.ToString($"C{_decHer}", System.Globalization.CultureInfo.CurrentCulture);
            e.FormattingApplied = true;
        }

        private static void AplicarEstiloDesdeColHer(DataGridViewTextBoxColumn dgvCol, ColumnaHerramienta col)
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
            }
            catch { }
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
            try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }
    }
}
