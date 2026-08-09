using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Construcción del grid, configuración de columnas y estilos.
    /// </summary>
    public partial class FormProgramaInsumos
    {

        private void ConstruirGrid(ProgramaInsumosResultDto dto, ProgramaInsumoTipo tipo)
        {
            var vista = cboVista.ComboBox.SelectedItem is VistaProgramaInsumos v ? v : VistaProgramaInsumos.Cantidades;
            dgvProgramaInsumos.SuspendLayout();
            dgvProgramaInsumos.Columns.Clear();
            dgvProgramaInsumos.Rows.Clear();

            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colClave", HeaderText = "Clave", Width = 110, /*Frozen = true,*/ ReadOnly = true });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescripcion", HeaderText = "Descripción", Width = 420, /*Frozen = true,*/ ReadOnly = true });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnidad", HeaderText = "Unidad", Width = 80, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = CenterStyle() });
            if (tipo == ProgramaInsumoTipo.Maquinaria)
                dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRendimiento", HeaderText = "Rendimiento", Width = 110, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = DecimalStyle() });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colInicio", HeaderText = "Inicio", Width = 95, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = DateStyle() });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTermino", HeaderText = "Término", Width = 95, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = DateStyle() });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDondeSeUsa", HeaderText = "Dónde se usa", Width = 180, /*Frozen = true,*/ ReadOnly = true });
            if (vista != VistaProgramaInsumos.Cantidades)
                dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPU", HeaderText = "P.U.", Width = 90, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = MoneyStyle() });
            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "Total", Width = 90, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = DecimalStyle() });
            if (vista != VistaProgramaInsumos.Cantidades)
                dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = "colImporteTotal", HeaderText = "Importe total", Width = 110, /*Frozen = true,*/ ReadOnly = true, DefaultCellStyle = MoneyStyle(FontStyle.Bold) });

            foreach (var p in dto.Periodos.OrderBy(x => x.Orden))
            {
                if (vista != VistaProgramaInsumos.Importes)
                {
                    dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = $"per_{p.PeriodoId}", HeaderText = p.Etiqueta, Width = 105, ReadOnly = true, DefaultCellStyle = DecimalStyle() });
                    dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = $"acu_{p.PeriodoId}", HeaderText = $"Acum {p.Orden:00}", Width = 110, ReadOnly = true, DefaultCellStyle = DecimalStyle(FontStyle.Bold) });
                }
                if (vista != VistaProgramaInsumos.Cantidades)
                {
                    dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = $"imp_{p.PeriodoId}", HeaderText = $"Imp. {p.Orden:00}", Width = 105, ReadOnly = true, DefaultCellStyle = MoneyStyle() });
                    dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn { Name = $"iacu_{p.PeriodoId}", HeaderText = $"Imp. acum {p.Orden:00}", Width = 115, ReadOnly = true, DefaultCellStyle = MoneyStyle(FontStyle.Bold) });
                }
            }

            dgvProgramaInsumos.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDummy",
                HeaderText = string.Empty,
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False,
                MinimumWidth = 20
            });

            foreach (var row in dto.Rows)
            {
                var cells = new List<object?>
                {
                    row.Clave,
                    row.Descripcion,
                    row.Unidad,
                };
                if (tipo == ProgramaInsumoTipo.Maquinaria)
                    cells.Add(row.Rendimiento > 0 ? FormatDecimal(row.Rendimiento) : string.Empty);
                cells.AddRange(new object?[]
                {
                    row.FechaInicio?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? string.Empty,
                    row.FechaFin?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? string.Empty,
                    row.DondeSeUsa
                });
                if (vista != VistaProgramaInsumos.Cantidades)
                    cells.Add(FormatMoney(row.PrecioUnitario));
                cells.Add(FormatDecimal(row.Total));
                if (vista != VistaProgramaInsumos.Cantidades)
                    cells.Add(FormatMoney(row.ImporteTotal));
                foreach (var p in dto.Periodos.OrderBy(x => x.Orden))
                {
                    row.CantidadesPorPeriodo.TryGetValue(p.PeriodoId, out var cant);
                    row.AcumuladosPorPeriodo.TryGetValue(p.PeriodoId, out var acum);
                    row.ImportesPorPeriodo.TryGetValue(p.PeriodoId, out var imp);
                    row.ImportesAcumuladosPorPeriodo.TryGetValue(p.PeriodoId, out var iacu);
                    if (vista != VistaProgramaInsumos.Importes)
                    {
                        cells.Add(cant == 0m ? string.Empty : FormatDecimal(cant));
                        cells.Add(acum == 0m ? string.Empty : FormatDecimal(acum));
                    }
                    if (vista != VistaProgramaInsumos.Cantidades)
                    {
                        cells.Add(imp == 0m ? string.Empty : FormatMoney(imp));
                        cells.Add(iacu == 0m ? string.Empty : FormatMoney(iacu));
                    }
                }
                int idx = dgvProgramaInsumos.Rows.Add(cells.ToArray());
                dgvProgramaInsumos.Rows[idx].Tag = row.InsumoId;
            }

            foreach (DataGridViewColumn col in dgvProgramaInsumos.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            if (dgvProgramaInsumos.Columns.Contains("colDummy"))
            {
                var dummy = dgvProgramaInsumos.Columns["colDummy"];
                dummy.DefaultCellStyle.BackColor = dgvProgramaInsumos.BackgroundColor;
                dummy.HeaderCell.Style.BackColor = dgvProgramaInsumos.ColumnHeadersDefaultCellStyle.BackColor;
            }
            dgvProgramaInsumos.ResumeLayout();
        }

        private void AplicarConfiguracionColumnas()
        {
            _cargandoColumnas = true;
            try
            {
                var columnas = dgvProgramaInsumos.Columns.Cast<DataGridViewColumn>()
                    .Where(c => c.Name != "colDummy")
                    .ToList();

                foreach (var col in columnas)
                {
                    var cfg = _columnasConfig.FirstOrDefault(c => c.NombreInterno == col.Name);
                    if (cfg == null) continue;
                    col.HeaderText = cfg.Nombre;
                    col.Visible = cfg.Visible;
                    if (cfg.AnchoColumna > 20) col.Width = cfg.AnchoColumna;
                    col.Tag = cfg;
                    AplicarEstiloInsumos(col, cfg);
                }

                var visiblesFijas = columnas.Where(c => c.Visible && c.Frozen)
                    .OrderBy(c => (_columnasConfig.FirstOrDefault(x => x.NombreInterno == c.Name)?.Orden) ?? int.MaxValue).ToList();
                var visiblesMoviles = columnas.Where(c => c.Visible && !c.Frozen)
                    .OrderBy(c => (_columnasConfig.FirstOrDefault(x => x.NombreInterno == c.Name)?.Orden) ?? int.MaxValue).ToList();

                int idx = 0;
                foreach (var col in visiblesFijas) col.DisplayIndex = idx++;
                foreach (var col in visiblesMoviles) col.DisplayIndex = idx++;
            }
            finally
            {
                _cargandoColumnas = false;
            }
        }

        private void GuardarLayoutPersistido()
        {
            if (!_layoutPersistenceReady)
                return;

            var state = new FormProgramaInsumosLayoutState
            {
                GanttPanelWidth = Math.Max(180, splitPrincipal.Panel2.Width),
                GanttCellWidth = _ganttControl?.TimelineCellWidth,
                TipoInsumo = cboTipo.ComboBox.SelectedItem is ProgramaInsumoTipo tipo ? (int)tipo : null,
                Vista = cboVista.ComboBox.SelectedItem?.ToString(),
                GanttFontFamily = _ganttControl?.VisualSettings.FontFamilyName,
                GanttFontStyle = _ganttControl == null ? null : (int?)_ganttControl.VisualSettings.FontStyle,
                GanttTextColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.TextColor.ToArgb(),
                GanttOutlineColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.OutlineColor.ToArgb(),
                GanttNormalBarColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.NormalBarColor.ToArgb(),
                GanttCriticalBarColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.CriticalBarColor.ToArgb(),
                GanttSummaryBarColorArgb = _ganttControl == null ? null : _ganttControl.VisualSettings.SummaryBarColor.ToArgb()
            };
            FormProgramaInsumosLayoutStateStore.Save(_proyecto.Id, state);
        }

        private static void AplicarEstiloInsumos(DataGridViewColumn col, ColumnaProgramaInsumos cfg)
        {
            var font = new Font(cfg.NombreFuente ?? "Segoe UI", cfg.TamanoFuente > 0 ? cfg.TamanoFuente : 9,
                (cfg.Negrita ? FontStyle.Bold : FontStyle.Regular) | (cfg.Cursiva ? FontStyle.Italic : FontStyle.Regular));
            col.DefaultCellStyle.Font = font;
            col.HeaderCell.Style.Font = font;
            try { col.DefaultCellStyle.BackColor = ColorTranslator.FromHtml(cfg.ColorFondo ?? "#FFFFFF"); } catch { }
            try { col.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml(cfg.ColorFuente ?? "#000000"); } catch { }
            try { col.HeaderCell.Style.BackColor = ColorTranslator.FromHtml(cfg.ColorFondo ?? "#FFFFFF"); } catch { }
            try { col.HeaderCell.Style.ForeColor = ColorTranslator.FromHtml(cfg.ColorFuente ?? "#000000"); } catch { }
            col.DefaultCellStyle.Alignment = FormatoHelper.ConvertirAlineacionDgv(cfg.Alineacion, cfg.AlineacionVertical);
            col.DefaultCellStyle.WrapMode = cfg.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
        }
    }
}
