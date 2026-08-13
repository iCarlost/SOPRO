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
    /// Configuración del grid y formato de celdas del catálogo.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;

            dgvMateriales.AutoGenerateColumns = false;
            dgvMateriales.AllowUserToAddRows = false;
            dgvMateriales.AllowUserToDeleteRows = false;
            dgvMateriales.ReadOnly = true;
            dgvMateriales.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvMateriales, conMenuCopia: false);
            dgvMateriales.CellFormatting += DgvMateriales_CellFormatting;
            dgvMateriales.KeyDown += DgvMateriales_KeyDown;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Cat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Cat;
            dgvMateriales.CellMouseDown += DgvMateriales_CellMouseDown;
            dgvMateriales.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvMateriales.BackgroundColor = Color.White;
            dgvMateriales.BorderStyle = BorderStyle.None;
            dgvMateriales.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvMateriales.GridColor = Color.FromArgb(230, 230, 230);
            dgvMateriales.ColumnHeadersHeight = 40;
            dgvMateriales.RowTemplate.Height = 35;

            dgvMateriales.EnableHeadersVisualStyles = false;
            dgvMateriales.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvMateriales.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMateriales.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvMateriales.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMateriales.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvMateriales.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvMateriales.ScrollBars = ScrollBars.Both;

            dgvMateriales.Columns.Clear();

            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    var alin = cfg.Alineacion switch
                    {
                        AlineacionColumna.Centro => DataGridViewContentAlignment.MiddleCenter,
                        AlineacionColumna.Derecha => DataGridViewContentAlignment.MiddleRight,
                        _ => DataGridViewContentAlignment.MiddleLeft,
                    };

                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = cfg.NombreInterno == "Origen" ? null : cfg.NombreInterno,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                        DefaultCellStyle = new DataGridViewCellStyle
                        {
                            Alignment = alin,
                            Format = cfg.FormatoNumerico ?? "",
                        },
                    };


                    dgvMateriales.Columns.Add(col);
                }

                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn
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
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = "Clave", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = "Unidad", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrecioUnitario", HeaderText = "Precio Unitario", DataPropertyName = "PrecioUnitario", Width = 140, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMateriales.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "colDummy",
                    HeaderText = "",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }

            // Aplicar estilo inicial desde BD
            foreach (DataGridViewColumn col in dgvMateriales.Columns)
                if (col.Tag is ColumnaMaterial cm)
                    AplicarEstiloDesdeColMat((DataGridViewTextBoxColumn)col, cm);

            _cargandoColumnas = false;
        }

        private void DgvMateriales_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaMaterial cfg) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                CatalogColumnLayoutService.GuardarAnchoColumna(_context, cfg.Id, nuevoAncho);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en materiales: {ex.Message}");
            }
        }

        private int DecimalesImporte => _sessionInfo.DecimalesImporte ?? 2;

        private void DgvMateriales_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvMateriales.Rows[e.RowIndex].DataBoundItem is not MaterialListItem mat) return;
            var colName = dgvMateriales.Columns[e.ColumnIndex].Name;
            if (colName == "col_Origen" || colName == "colOrigen")
            {
                e.Value = ImportOriginStampService.BuildOriginDisplay(mat.Notas, mat.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
                e.FormattingApplied = true;
                return;
            }
            if ((colName == "colPrecioUnitario" || colName == "col_PrecioUnitario") && e.Value is decimal pu)
            {
                e.Value = pu.ToString($"C{DecimalesImporte}", System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private static void AplicarEstiloDesdeColMat(DataGridViewTextBoxColumn dgvCol, ColumnaMaterial col)
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

        private static void AplicarEstiloDesdeColumna(DataGridViewTextBoxColumn dgvCol, ColumnaPersonalizada fmt)
        {
            try
            {
                FontStyle fs = (fmt.Negrita ? FontStyle.Bold : FontStyle.Regular)
                             | (fmt.Cursiva ? FontStyle.Italic : FontStyle.Regular);
                string fuente = !string.IsNullOrEmpty(fmt.NombreFuente) ? fmt.NombreFuente : "Segoe UI";
                float tam = fmt.TamanoFuente > 0 ? fmt.TamanoFuente : 9f;
                dgvCol.DefaultCellStyle.Font = new Font(fuente, tam, fs);
                dgvCol.DefaultCellStyle.BackColor = TryColor(fmt.ColorFondo, Color.White);
                dgvCol.DefaultCellStyle.ForeColor = TryColor(fmt.ColorFuente, Color.Black);
                dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(fmt.Alineacion, fmt.AlineacionVertical);
                dgvCol.DefaultCellStyle.WrapMode = fmt.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
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
