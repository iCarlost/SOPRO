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
    /// Configuración de los grids de oficina central y campo.
    /// </summary>
    public partial class FormIndirectos
    {

        private void ConfigurarGrids()
        {
            ConfigurarGrid(dgvOficinaCentral);
            ConfigurarGrid(dgvCampo);
            // Ribbon: click en encabezado dispara notificación
            dgvOficinaCentral.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvCampo.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvOficinaCentral.CellBeginEdit += Dgv_CellBeginEdit;
            dgvCampo.CellBeginEdit += Dgv_CellBeginEdit;
            txtVolumenAnual.Enter += ConfigTextBox_Enter;
            txtCostoDirecto.Enter += ConfigTextBox_Enter;
            txtVolumenAnual.Leave += ConfigTextBox_Leave;
            txtCostoDirecto.Leave += ConfigTextBox_Leave;
            // Aplicar formato guardado en BD
            AplicarEstilosDesdeDB();
        }

        private void ConfigurarGrid(DataGridView dgv)
        {
            dgv.AutoGenerateColumns = false;
            dgv.AllowUserToAddRows = false;
            dgv.RowHeadersVisible = false;

            // Columna Grupo (solo lectura, en negrita)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colGrupo",
                HeaderText = "GRUPO / CONCEPTO",
                Width = 400,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // Columna Importe Mensual (editable)
            var colImporte = new DataGridViewTextBoxColumn
            {
                Name = "colImporteMensual",
                HeaderText = "IMPORTE MENSUAL $",
                Width = 180,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N2"
                }
            };
            dgv.Columns.Add(colImporte);

            // Columna Duración (editable, solo para conceptos de campo)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDuracion",
                HeaderText = "DURACIÓN (MESES)",
                Width = 150,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            // Columna Importe Total (calculado, solo lectura)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colImporteTotal",
                HeaderText = "IMPORTE TOTAL $",
                Width = 180,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N2",
                    BackColor = Color.FromArgb(240, 240, 240)
                }
            });

            // Columna oculta para el ID
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                Visible = false
            });

            // Columna oculta para el tipo (Grupo o Concepto)
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTipo",
                Visible = false
            });
        }
    }
}
