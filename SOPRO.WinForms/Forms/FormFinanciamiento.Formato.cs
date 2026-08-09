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
    /// Implementación de IGridFormato y configuración del grid de flujo.
    /// </summary>
    public partial class FormFinanciamiento
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colFinRibbon == null) return;

            _colFinRibbon.NombreFuente = fmt.NombreFuente;
            _colFinRibbon.TamanoFuente = fmt.TamanoFuente;
            _colFinRibbon.Negrita = fmt.Negrita;
            _colFinRibbon.Cursiva = fmt.Cursiva;
            _colFinRibbon.Alineacion = fmt.Alineacion;
            _colFinRibbon.ColorFondo = fmt.ColorFondo;
            _colFinRibbon.ColorFuente = fmt.ColorFuente;
            _colFinRibbon.WrapTexto = fmt.WrapTexto;
            _colFinRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colFinRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvFlujo.Columns)
            {
                if (col.Tag == _colFinRibbon && col is DataGridViewTextBoxColumn txtCol)
                {
                    AplicarEstiloDesdeColFin(txtCol, _colFinRibbon);
                    break;
                }
            }

            FormatoHelper.AjustarAutoAlturaFilas(dgvFlujo);
            dgvFlujo.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (var colFin in ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id))
            {
                colFin.NombreFuente = fmt.NombreFuente;
                colFin.TamanoFuente = fmt.TamanoFuente;
                colFin.Negrita = fmt.Negrita;
                colFin.Cursiva = fmt.Cursiva;
                colFin.Alineacion = fmt.Alineacion;
                colFin.ColorFuente = fmt.ColorFuente;
                colFin.WrapTexto = fmt.WrapTexto;
                colFin.AlineacionVertical = fmt.AlineacionVertical;
                colFin.FechaModificacion = DateTime.Now;
            }

            _context.SaveChanges();
            AplicarLayoutColumnas();
            dgvFlujo.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colFinRibbon = null;
            _columnaRibbon = null;

            if (colIndex >= 0 && colIndex < dgvFlujo.Columns.Count)
            {
                var col = dgvFlujo.Columns[colIndex];
                if (col.Tag is ColumnaFinanciamiento cfg)
                {
                    _colFinRibbon = cfg;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = cfg.Nombre,
                        NombreFuente = cfg.NombreFuente,
                        TamanoFuente = cfg.TamanoFuente,
                        Negrita = cfg.Negrita,
                        Cursiva = cfg.Cursiva,
                        Alineacion = cfg.Alineacion,
                        ColorFondo = cfg.ColorFondo,
                        ColorFuente = cfg.ColorFuente,
                        WrapTexto = cfg.WrapTexto,
                        AlineacionVertical = cfg.AlineacionVertical,
                    };
                }
            }

            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        private void ConfigurarGrid()
        {
            dgvFlujo.AplicarEstiloSOPRO();
            dgvFlujo.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvFlujo.AllowUserToAddRows = false;
            dgvFlujo.AllowUserToDeleteRows = false;
            dgvFlujo.AllowUserToResizeRows = false;
            dgvFlujo.ReadOnly = true;
            dgvFlujo.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFlujo.MultiSelect = true;
            dgvFlujo.AllowUserToOrderColumns = false;
            dgvFlujo.ColumnHeadersHeight = 40;
            dgvFlujo.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvFlujo.RowTemplate.Height = 35;
            dgvFlujo.BackgroundColor = Color.White;
            dgvFlujo.BorderStyle = BorderStyle.None;
            dgvFlujo.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvFlujo.GridColor = Color.FromArgb(230, 230, 230);
            dgvFlujo.EnableHeadersVisualStyles = false;
            dgvFlujo.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 51, 76);
            dgvFlujo.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvFlujo.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvFlujo.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvFlujo.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            dgvFlujo.ScrollBars = ScrollBars.Both;
            DgvCeldaHelper.Aplicar(dgvFlujo);
            dgvFlujo.ColumnWidthChanged += dgvFlujo_ColumnWidthChanged;
            dgvFlujo.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);

            foreach (DataGridViewColumn col in dgvFlujo.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
    }
}
