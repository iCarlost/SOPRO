using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Reporting.Programa;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Models;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte Excel del Programa de Obra a partir del contrato neutral:
    /// el snapshot de columnas (<see cref="ReportColumnSnapshot"/>) y las filas
    /// materializadas (<see cref="ProgramaReportData"/>) más el
    /// <see cref="GanttRenderModel"/>. No recibe ni lee la cuadrícula viva: los
    /// encabezados, anchos, alineaciones y estilos salen del snapshot y los valores
    /// numéricos se formatean con <c>ReportColumnGridFormat</c>.
    /// </summary>
    public sealed class GeneradorExcelProgramaObra : GeneradorExcelProgramaBase
    {
        public GeneradorExcelProgramaObra(ReporteService svc) : base(svc)
        {
        }

        public string Generar(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            ProgramaReportData datos,
            GanttRenderModel ganttModel,
            GanttVisualSettings ganttVisualSettings,
            GanttFooterDisplayMode footerMode,
            int timelineCellWidth,
            string tituloReporte,
            string? rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(datos);
            ArgumentNullException.ThrowIfNull(ganttModel);

            var columnas = MapearColumnas(snapshot);
            var filas = MapearFilas(datos, snapshot);
            return GenerarCore(
                proyecto, plantilla, columnas, filas, ganttModel, ganttVisualSettings,
                footerMode, timelineCellWidth, tituloReporte, "ProgramaObra", rutaDestino, tituloCfg, datos.Periodos);
        }
    }
}
