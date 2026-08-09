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
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Configuración del grid y formato de celdas del catálogo.
    /// </summary>
    public partial class FormCatalogoManoObra
    {

        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvManoObra.AutoGenerateColumns = false;
            dgvManoObra.AllowUserToAddRows = false;
            dgvManoObra.AllowUserToDeleteRows = false;
            dgvManoObra.ReadOnly = true;
            dgvManoObra.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvManoObra, conMenuCopia: false);
            dgvManoObra.CellFormatting += DgvManoObra_CellFormatting;
            dgvManoObra.KeyDown += DgvManoObra_KeyDown;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Cat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Cat;
            dgvManoObra.CellMouseDown += DgvManoObra_CellMouseDown;
            dgvManoObra.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvManoObra.BackgroundColor = Color.White;
            dgvManoObra.BorderStyle = BorderStyle.None;
            dgvManoObra.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvManoObra.GridColor = Color.FromArgb(230, 230, 230);
            dgvManoObra.ColumnHeadersHeight = 40;
            dgvManoObra.RowTemplate.Height = 35;

            dgvManoObra.EnableHeadersVisualStyles = false;
            dgvManoObra.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvManoObra.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvManoObra.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvManoObra.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvManoObra.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvManoObra.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvManoObra.ScrollBars = ScrollBars.Both;

            dgvManoObra.Columns.Clear();

            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = cfg.NombreInterno == "Origen" ? null : cfg.NombreInterno,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvManoObra.Columns.Add(col);
                    AplicarEstiloDesdeColMO(col, cfg);
                }

                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn
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
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = "Clave", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", DataPropertyName = "Unidad", Width = 70, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSalarioBase", HeaderText = "Salario Base", DataPropertyName = "SalarioBase", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFSR", HeaderText = "FSR", DataPropertyName = "FactorSalarioReal", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N4" } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSalarioReal", HeaderText = "Salario Real", DataPropertyName = "SalarioReal", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvManoObra.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "colDummy",
                    HeaderText = "",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }

            _cargandoColumnas = false;
        }

        private void DgvManoObra_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaManoObra cfg) return;

                var columnaDb = _context.ColumnasManoObra.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en mano de obra: {ex.Message}");
            }
        }

        private int DecimalesImporte => ProyectoActual?.DecimalesImporte ?? 2;

        private void DgvManoObra_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvManoObra.Rows[e.RowIndex].DataBoundItem is not ManoDeObra mo) return;
            var colName = dgvManoObra.Columns[e.ColumnIndex].Name;
            if (colName == "col_Origen" || colName == "colOrigen")
            {
                e.Value = ImportOriginStampService.BuildOriginDisplay(mo.Notas, mo.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
                e.FormattingApplied = true;
                return;
            }
            if ((colName == "colSalarioBase" || colName == "col_SalarioBase" ||
                 colName == "colSalarioReal" || colName == "col_SalarioReal") && e.Value is decimal sal)
            {
                e.Value = sal.ToString($"C{DecimalesImporte}", System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private static void AplicarEstiloDesdeColMO(DataGridViewTextBoxColumn dgvCol, ColumnaManoObra col)
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
                dgvCol.DefaultCellStyle.Format = col.FormatoNumerico ?? "";
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
