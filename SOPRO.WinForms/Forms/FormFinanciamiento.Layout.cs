using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Etiquetas de referencia, estilos de columna y layout persistido del grid.
    /// </summary>
    public partial class FormFinanciamiento
    {

        private void EnsureReferenceLabels()
        {
            if (_lblCostoDirectoRef != null)
                return;

            _lblCostoDirectoRef = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(10, 82),
                Name = "lblCostoDirectoRef",
                Text = "Costo Directo:"
            };

            _lblCostoDirectoValor = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 51, 76),
                Location = new Point(140, 82),
                Name = "lblCostoDirectoValor",
                Text = "$0.00"
            };

            _lblCostoIndirectoRef = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(10, 102),
                Name = "lblCostoIndirectoRef",
                Text = "Costo Indirecto:"
            };

            _lblCostoIndirectoValor = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 51, 76),
                Location = new Point(140, 102),
                Name = "lblCostoIndirectoValor",
                Text = "$0.00"
            };

            gbBaseCalculo.Height = 128;
            gbBaseCalculo.Controls.Add(_lblCostoDirectoRef);
            gbBaseCalculo.Controls.Add(_lblCostoDirectoValor);
            gbBaseCalculo.Controls.Add(_lblCostoIndirectoRef);
            gbBaseCalculo.Controls.Add(_lblCostoIndirectoValor);
        }

        private void ActualizarEtiquetasReferencia()
        {
            EnsureReferenceLabels();

            var baseRows = BuildDisplayRows();
            decimal totalCD = baseRows.Values.Sum(x => x.CostoDirecto);
            decimal totalCI = baseRows.Values.Sum(x => x.CostoIndirecto);

            if (_lblCostoDirectoValor != null)
                _lblCostoDirectoValor.Text = totalCD.ToStringImporte();
            if (_lblCostoIndirectoValor != null)
                _lblCostoIndirectoValor.Text = totalCI.ToStringImporte();
        }

        private void AplicarEstiloDesdeColFin(DataGridViewTextBoxColumn dgvCol, ColumnaFinanciamiento col)
        {
            var alineacion = col.Alineacion switch
            {
                AlineacionColumna.Centro => DataGridViewContentAlignment.MiddleCenter,
                AlineacionColumna.Derecha => DataGridViewContentAlignment.MiddleRight,
                _ => DataGridViewContentAlignment.MiddleLeft,
            };

            FontStyle fs = (col.Negrita ? FontStyle.Bold : FontStyle.Regular)
                         | (col.Cursiva ? FontStyle.Italic : FontStyle.Regular);
            string fuente = !string.IsNullOrWhiteSpace(col.NombreFuente) ? col.NombreFuente : "Segoe UI";
            float tam = col.TamanoFuente > 0 ? col.TamanoFuente : 9f;

            dgvCol.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical);
            dgvCol.DefaultCellStyle.WrapMode = col.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
            dgvCol.DefaultCellStyle.Format = col.FormatoNumerico ?? string.Empty;
            dgvCol.DefaultCellStyle.Font = new Font(fuente, tam, fs);
            dgvCol.DefaultCellStyle.ForeColor = TryColor(col.ColorFuente, Color.Black);
            dgvCol.DefaultCellStyle.BackColor = TryColor(col.ColorFondo, Color.White);
        }

        private static Color TryColor(string? hex, Color fallback)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(hex) ? ColorTranslator.FromHtml(hex) : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void AplicarLayoutColumnas()
        {
            _aplicandoLayoutColumnas = true;
            try
            {
                var columnas = ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id);
                foreach (DataGridViewColumn col in dgvFlujo.Columns)
                {
                    if (col.Name == "colDummy")
                        continue;
                    var cfg = columnas.FirstOrDefault(c => c.NombreInterno == col.Name)
                          ?? (col.Name == "colCobroNeto" ? columnas.FirstOrDefault(c => c.NombreInterno == "colCobro") : null);
                    if (cfg == null)
                        continue;
                    col.Tag = cfg;
                    col.HeaderText = cfg.Nombre;
                    col.Visible = cfg.Visible;
                    col.Width = cfg.AnchoColumna;
                    if (col is DataGridViewTextBoxColumn txtCol)
                        AplicarEstiloDesdeColFin(txtCol, cfg);
                    if (cfg.Orden > 0)
                        col.DisplayIndex = Math.Max(0, cfg.Orden - 1);
                }

                FormatoHelper.AjustarAutoAlturaFilas(dgvFlujo);
            }
            finally
            {
                _aplicandoLayoutColumnas = false;
            }
        }

        private void GuardarLayoutColumnas()
        {
            if (_aplicandoLayoutColumnas) return;
            var columnas = ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id);
            foreach (var cfg in columnas)
            {
                var col = dgvFlujo.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Name == cfg.NombreInterno || (cfg.NombreInterno == "colCobro" && c.Name == "colCobroNeto"));
                if (col == null || col.Name == "colDummy")
                    continue;
                cfg.Visible = col.Visible;
                cfg.AnchoColumna = col.Width;
                cfg.Orden = col.DisplayIndex + 1;
                cfg.FechaModificacion = DateTime.Now;
            }
            _context.SaveChanges();
        }

        private void dgvFlujo_ColumnWidthChanged(object? sender, DataGridViewColumnEventArgs e)
        {
            if (_aplicandoLayoutColumnas || e?.Column == null || e.Column.Name == "colDummy")
                return;
            GuardarLayoutColumnas();
        }

        private void btnConfigColumnas_Click(object sender, EventArgs e)
        {
            using var form = new FormColumnasAPU(_context, _proyecto.Id, FormColumnasAPU.ModoColumnas.Financiamiento);
            if (form.ShowDialog(this) == DialogResult.OK || form.CambiosRealizados)
            {
                AplicarLayoutColumnas();
            }
        }
    }
}
