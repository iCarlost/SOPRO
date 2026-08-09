using System.ComponentModel;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Models;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Exportar, PDF, sincronizar, reconstruir y borrar el programa.
    /// </summary>
    public partial class FormProgramaObra
    {

        private void btnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                if (_programaActual == null || _ganttControl == null || _ganttControl.RenderModel == null)
                {
                    MessageBox.Show("No hay un programa cargado para exportar.", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var ganttModel = _ganttControl.RenderModel;
                if (ganttModel.Filas.Count == 0 || ganttModel.Escala.Count == 0)
                {
                    MessageBox.Show("El programa no tiene datos suficientes para generar el reporte Excel.", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                FormatoHelper.EstablecerProyecto(_proyecto);

                var tituloReporte = ObtenerTituloReportePrograma();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaObra, lblTitulo.Text);
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Excel del programa",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"{tituloReporte.Replace(' ', '_')}_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                Cursor = Cursors.WaitCursor;
                var generador = new Services.GeneradorExcelProgramaObra(svc);
                var ruta = generador.Generar(
                    _proyecto,
                    plantilla,
                    dgvActividades,
                    ganttModel,
                    _ganttControl.VisualSettings,
                    _ganttControl.FooterDisplayMode,
                    _ganttControl.TimelineCellWidth,
                    tituloReporte,
                    dlg.FileName,
                    tituloCfg);
                Cursor = Cursors.Default;
                if (MessageBox.Show("Reporte generado:\n" + ruta + "\n\n¿Abrir ahora?", "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
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
                MessageBox.Show("Error al generar el reporte del programa:\n" + ex.Message, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        public void GenerarPdfProgramaObra()
        {
            try
            {
                if (_ganttControl?.RenderModel == null)
                    throw new InvalidOperationException("No hay vista gantt disponible para exportar.");

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloReporte = ObtenerTituloReportePrograma();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaObra, lblTitulo.Text);

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF del programa",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"{tituloReporte.Replace(' ', '_')}_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };

                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                Cursor = Cursors.WaitCursor;
                var generador = new Services.GeneradorPdfProgramaObra(svc);
                var ruta = generador.Generar(
                    _proyecto,
                    plantilla,
                    dgvActividades,
                    _ganttControl.RenderModel,
                    _ganttControl.VisualSettings,
                    tituloReporte,
                    dlg.FileName,
                    tituloCfg);
                Cursor = Cursors.Default;
                if (MessageBox.Show("Reporte generado:\n" + ruta + "\n\n¿Abrir ahora?", "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
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
                MessageBox.Show("Error al generar el PDF del programa:\n" + ex.Message, "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private string ObtenerTituloReportePrograma()
        {
            var baseTitle = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaObra, lblTitulo.Text).TextoTitulo;
            return ObtenerVistaCurvaSeleccionada() switch
            {
                CurvaSViewMode.Financiera => $"{baseTitle} - Erogaciones",
                CurvaSViewMode.Ambas => $"{baseTitle} - Mixto",
                _ => $"{baseTitle} - Cantidades"
            };
        }

        private void btnSincronizar_Click(object sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                var state = DataGridViewStateHelper.Capture(dgvActividades);
                var result = _syncService.SyncFromBudget(_context, _proyecto.Id);
                lblEstado.Text = result.Message;
                CargarPrograma(state);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al sincronizar el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnReconstruir_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Se reconstruirá el programa desde el presupuesto. Esto eliminará dependencias, distribuciones y ajustes manuales del programa actual.\n\n¿Deseas continuar?",
                "Reconstruir programa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                UseWaitCursor = true;
                var result = _syncService.RebuildProgram(_context, _proyecto.Id);
                lblEstado.Text = result.Message;
                CargarPrograma();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al reconstruir el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnBorrarPrograma_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "Se eliminará por completo el programa de obra del proyecto, incluyendo actividades, dependencias, distribuciones y calendarios.\n\n¿Deseas continuar?",
                "Borrar programa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                UseWaitCursor = true;
                var result = _syncService.DeleteProgram(_context, _proyecto.Id);
                lblEstado.Text = result.Message;
                CargarPrograma();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al borrar el programa: {ex.Message}", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void btnToggleDetalle_Click(object sender, EventArgs e)
        {
            AlternarPanelDetalle();
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
