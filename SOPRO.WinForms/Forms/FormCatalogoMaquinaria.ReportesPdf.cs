using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Generación de reportes PDF del costo horario de maquinaria.
    /// </summary>
    public partial class FormCatalogoMaquinaria
    {

        public bool GenerarReporteExcel()
        {
            ExportarCostoHorario();
            return true;
        }

        public void GenerarPdfCostoHorario()
        {
            if (_listaActual == null || !_listaActual.Any())
            {
                MessageBox.Show("No hay registros en la vista actual para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar análisis de costo horario en PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"CostoHorario_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                DefaultExt = "pdf"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                Proyecto proyecto = _proyectoId.HasValue
                    ? _context.Proyectos.Find(_proyectoId.Value)
                    : new Proyecto { Nombre = "Maquinaria" };

                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoMaquinaria, lblTitulo.Text)
                    : null;
                var generador = new GeneradorPdfCostoHorario(svcRep);
                var ruta = generador.Generar(proyecto, _listaActual, plantilla, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show($"Reporte PDF generado con {_listaActual.Count} análisis de costo horario.\n\n¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte PDF:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
