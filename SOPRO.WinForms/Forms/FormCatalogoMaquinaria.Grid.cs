using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Configuración del grid y formato de celdas del catálogo.
    /// </summary>
    public partial class FormCatalogoMaquinaria
    {

        private void ConfigurarGrid()
        {
            _cargandoColumnas = true;
            dgvMaquinaria.AutoGenerateColumns = false;
            dgvMaquinaria.AllowUserToAddRows = false;
            dgvMaquinaria.AllowUserToDeleteRows = false;
            dgvMaquinaria.ReadOnly = true;
            dgvMaquinaria.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Helpers.DgvCeldaHelper.Aplicar(dgvMaquinaria, conMenuCopia: false);
            dgvMaquinaria.CellFormatting += DgvMaquinaria_CellFormatting;
            dgvMaquinaria.KeyDown += DgvMaquinaria_KeyDown;
            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados_Cat;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados_Cat;
            dgvMaquinaria.CellMouseDown += DgvMaquinaria_CellMouseDown;
            dgvMaquinaria.MultiSelect = true;
            // RowHeadersVisible habilitado por DgvCeldaHelper
            dgvMaquinaria.BackgroundColor = Color.White;
            dgvMaquinaria.BorderStyle = BorderStyle.None;
            dgvMaquinaria.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvMaquinaria.GridColor = Color.FromArgb(230, 230, 230);
            dgvMaquinaria.ColumnHeadersHeight = 40;
            dgvMaquinaria.RowTemplate.Height = 35;

            dgvMaquinaria.EnableHeadersVisualStyles = false;
            dgvMaquinaria.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvMaquinaria.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMaquinaria.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvMaquinaria.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMaquinaria.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvMaquinaria.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvMaquinaria.ScrollBars = ScrollBars.Both;

            dgvMaquinaria.Columns.Clear();

            if (_columnasConfig.Any())
            {
                foreach (var cfg in _columnasConfig.Where(c => c.Visible))
                {
                    // Columnas calculadas/manuales no tienen DataPropertyName directa
                    bool esManual = cfg.NombreInterno == "Combustible"
                                 || cfg.NombreInterno == "TipoCosto"
                                 || cfg.NombreInterno == "Origen";

                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "col_" + cfg.NombreInterno,
                        HeaderText = cfg.Nombre,
                        DataPropertyName = esManual ? null : cfg.NombreInterno,
                        Width = cfg.AnchoColumna,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Tag = cfg,
                    };


                    dgvMaquinaria.Columns.Add(col);
                    AplicarEstiloDesdeColMaq(col, cfg);
                }

                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn
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
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", DataPropertyName = "Clave", Width = 110, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion", Width = 300, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPotencia", HeaderText = "Potencia (HP)", DataPropertyName = "PotenciaNominal", Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCombustible", HeaderText = "Combustible", Width = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCostoHorario", HeaderText = "Costo Horario", DataPropertyName = "CostoHorario", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "" } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTipoCosto", HeaderText = "Tipo", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrigen", HeaderText = "Origen", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
                dgvMaquinaria.Columns.Add(new DataGridViewTextBoxColumn
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


        private void DgvMaquinaria_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_cargandoColumnas || e?.Column == null) return;

            try
            {
                if (e.Column.Tag is not ColumnaMaquinaria cfg) return;

                var columnaDb = _context.ColumnasMaquinaria.Find(cfg.Id);
                if (columnaDb == null) return;

                int nuevoAncho = Math.Max(40, e.Column.Width);
                if (columnaDb.AnchoColumna == nuevoAncho) return;

                columnaDb.AnchoColumna = nuevoAncho;
                columnaDb.FechaModificacion = DateTime.Now;
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando ancho de columna en maquinaria: {ex.Message}");
            }
        }

        private int DecimalesImporte => _proyectoId.HasValue
            ? (_context.Proyectos.Find(_proyectoId.Value)?.DecimalesImporte ?? 2) : 2;

        private void DgvMaquinaria_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvMaquinaria.Rows[e.RowIndex].DataBoundItem is not Maquinaria maq) return;

            var colName = dgvMaquinaria.Columns[e.ColumnIndex].Name;

            if (colName == "col_Combustible" || colName == "colCombustible")
            {
                e.Value = maq.TipoCombustible.ToString();
                e.FormattingApplied = true;
            }
            else if (colName == "col_TipoCosto" || colName == "colTipoCosto")
            {
                e.Value = maq.EsCostoCalculado ? "🧮 Calc" : "✏ Man";
                e.FormattingApplied = true;
            }
            else if (colName == "col_Origen" || colName == "colOrigen")
            {
                e.Value = ImportOriginStampService.BuildOriginDisplay(maq.Notas, maq.Origen == OrigenInsumo.Maestro ? "Maestro" : "Local");
                e.FormattingApplied = true;
            }
            else if ((colName == "colCostoHorario" || colName == "col_CostoHorario") && e.Value is decimal ch)
            {
                e.Value = ch.ToString($"C{DecimalesImporte}", System.Globalization.CultureInfo.CurrentCulture);
                e.FormattingApplied = true;
            }
        }

        private static void AplicarEstiloDesdeColMaq(DataGridViewTextBoxColumn dgvCol, ColumnaMaquinaria col)
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
