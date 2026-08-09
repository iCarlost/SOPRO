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
    /// Generación del PDF del programa de insumos.
    /// </summary>
    public partial class FormProgramaInsumos
    {

        public void GenerarPdfProgramaInsumos()
        {
            try
            {
                if (_ganttControl?.RenderModel == null)
                    throw new InvalidOperationException("No hay vista gantt disponible para exportar.");

                var ganttModel = _ganttControl.RenderModel;
                if (ganttModel.Filas.Count == 0 || ganttModel.Escala.Count == 0)
                    throw new InvalidOperationException("El programa de insumos no tiene datos suficientes para generar el PDF.");

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloReporte = ObtenerTituloReporteProgramaInsumos();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaInsumos, lblTitulo.Text);

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF del programa de insumos",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"{tituloReporte.Replace(' ', '_')}_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };

                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                Cursor = Cursors.WaitCursor;
                var generador = new Services.GeneradorPdfProgramaInsumos(svc);
                var ruta = generador.Generar(
                    _proyecto,
                    plantilla,
                    dgvProgramaInsumos,
                    ganttModel,
                    _ganttControl.VisualSettings,
                    tituloReporte,
                    dlg.FileName,
                    tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte generado:" + ruta + "¿Abrir ahora?", "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ruta,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el PDF del programa de insumos:" + ex.Message, "Programa de insumos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
