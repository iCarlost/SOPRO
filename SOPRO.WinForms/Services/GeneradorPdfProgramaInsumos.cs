using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Reporting.Programa;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Models;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte PDF del Programa de Insumos delegando en
    /// <see cref="GeneradorPdfProgramaObra"/> con el mismo contrato neutral que la
    /// ruta Excel: snapshot de columnas + filas materializadas + GanttRenderModel.
    /// No recibe ni lee la cuadrícula viva.
    /// </summary>
    public sealed class GeneradorPdfProgramaInsumos
    {
        private readonly GeneradorPdfProgramaObra _inner;

        public GeneradorPdfProgramaInsumos(ReporteService svc)
        {
            _inner = new GeneradorPdfProgramaObra(svc);
        }

        public string Generar(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            ProgramaReportData datos,
            GanttRenderModel ganttModel,
            GanttVisualSettings ganttVisualSettings,
            string tituloReporte,
            string? rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(datos);
            ArgumentNullException.ThrowIfNull(ganttModel);

            return _inner.Generar(
                proyecto, plantilla, snapshot, datos, ganttModel, ganttVisualSettings,
                tituloReporte, rutaDestino, tituloCfg);
        }
    }
}
