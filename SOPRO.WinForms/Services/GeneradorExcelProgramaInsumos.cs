using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Models;
using System.Globalization;
using System.Windows.Forms;

namespace SOPRO.WinForms.Services
{
    public sealed class GeneradorExcelProgramaInsumos : GeneradorExcelProgramaBase
    {
        public GeneradorExcelProgramaInsumos(ReporteService svc) : base(svc)
        {
        }

        public string Generar(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            DataGridView grid,
            GanttRenderModel ganttModel,
            GanttVisualSettings ganttVisualSettings,
            GanttFooterDisplayMode footerMode,
            int timelineCellWidth,
            string tituloReporte,
            string? rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            ArgumentNullException.ThrowIfNull(grid);
            ArgumentNullException.ThrowIfNull(ganttModel);

            var columnas = CapturarColumnasVisibles(grid);
            var filas = ObtenerFilas(grid, ganttModel);
            return GenerarCore(proyecto, plantilla, columnas, filas, ganttModel, ganttVisualSettings, footerMode, timelineCellWidth, tituloReporte, "ProgramaInsumos", rutaDestino, tituloCfg);
        }

        private static List<ProgramaExcelRowExport> ObtenerFilas(DataGridView grid, GanttRenderModel gantt)
        {
            var filasGantt = gantt.Filas.ToDictionary(x => x.Id);
            var resultado = new List<ProgramaExcelRowExport>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow || !row.Visible)
                    continue;

                int? id = null;
                if (row.Tag is int tagId)
                    id = tagId;

                filasGantt.TryGetValue(id ?? -1, out var filaGantt);
                var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var rawValores = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DataGridViewCell cell in row.Cells)
                {
                    var col = grid.Columns[cell.ColumnIndex];
                    if (!col.Visible || col.Name == "colDummy")
                        continue;
                    valores[col.Name] = Convert.ToString(cell.FormattedValue, CultureInfo.CurrentCulture) ?? string.Empty;
                    rawValores[col.Name] = cell.Value;
                    rawValores[col.HeaderText] = cell.Value;
                }

                resultado.Add(new ProgramaExcelRowExport
                {
                    ItemId = id,
                    Valores = valores,
                    RawValores = rawValores,
                    Inicio = filaGantt?.Inicio,
                    Fin = filaGantt?.Fin,
                    EsCritica = false,
                    EsResumen = false,
                    SegmentosFinancieros = filaGantt?.SegmentosFinancieros ?? new List<GanttPeriodSegmentDto>()
                });
            }
            return resultado;
        }
    }
}
