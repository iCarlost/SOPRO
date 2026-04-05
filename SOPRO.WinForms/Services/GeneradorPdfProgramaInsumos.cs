
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Models;
using System.Windows.Forms;

namespace SOPRO.WinForms.Services
{
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
            DataGridView grid,
            GanttRenderModel ganttModel,
            GanttVisualSettings ganttVisualSettings,
            string tituloReporte,
            string? rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            return _inner.Generar(proyecto, plantilla, grid, ganttModel, ganttVisualSettings, tituloReporte, rutaDestino, tituloCfg);
        }
    }
}
