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
    /// Generación de reportes PDF y Excel de indirectos.
    /// </summary>
    public partial class FormIndirectos
    {

        public void GenerarPdfIndirectos()
        {
            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnas = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);

                var gruposOC = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.OficinaCentral)
                    .OrderBy(g => g.Orden).ToList();

                var gruposCampo = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.Campo)
                    .OrderBy(g => g.Orden).ToList();

                if (!gruposOC.Any() && !gruposCampo.Any())
                {
                    MessageBox.Show("No hay datos de indirectos para exportar.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF de Cálculo de Indirectos",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Indirectos_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Indirectos, lblTitulo.Text);
                var generador = new Services.GeneradorPdfIndirectos(svc);
                string ruta = generador.Generar(_proyecto, gruposOC, gruposCampo,
                    _configuracion, plantilla, columnas, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte PDF:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnas = Helpers.ColumnasIndirectosHelper.ObtenerColumnas(_context, _proyecto.Id);

                var gruposOC = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.OficinaCentral)
                    .OrderBy(g => g.Orden).ToList();

                var gruposCampo = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.Campo)
                    .OrderBy(g => g.Orden).ToList();

                if (!gruposOC.Any() && !gruposCampo.Any())
                {
                    MessageBox.Show("No hay datos de indirectos para exportar.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Cálculo de Indirectos",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Indirectos_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Indirectos, lblTitulo.Text);
                var generador = new Services.GeneradorExcelIndirectos(svc);
                string ruta = generador.Generar(_proyecto, gruposOC, gruposCampo,
                    _configuracion, plantilla, columnas, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show($"Reporte generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
