using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Generación del reporte PDF de financiamiento.
    /// </summary>
    public partial class FormFinanciamiento
    {

        public bool GenerarReporteExcel() => ExportarReporteExcel();

        public void GenerarPdfFinanciamiento()
        {
            try
            {
                var filas = _context.FilasFlujoCajaFinanciamiento
                    .AsNoTracking()
                    .Where(f => f.ConfiguracionFinanciamientoId == _config.Id)
                    .OrderBy(f => f.NumeroPeriodo)
                    .ToList();

                if (filas.Count == 0)
                {
                    MessageBox.Show("No hay cálculo de financiamiento para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF de financiamiento",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Financiamiento_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnasCfg = ColumnasFinanciamientoHelper.ObtenerColumnas(_context, _proyecto.Id)
                    .Where(c => c.Visible)
                    .OrderBy(c => c.Orden)
                    .ToList();

                var baseRows = BuildDisplayRows();
                var baseRowsPdf = baseRows.Values
                    .OrderBy(x => x.NumeroPeriodo)
                    .Select(x => new GeneradorPdfFinanciamiento.BaseRowInfo
                    {
                        NumeroPeriodo = x.NumeroPeriodo,
                        CostoDirecto = x.CostoDirecto,
                        CostoIndirecto = x.CostoIndirecto
                    })
                    .ToList();

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Financiamiento, lblTitulo.Text);
                var generador = new GeneradorPdfFinanciamiento(svcRep);
                string ruta = generador.Generar(_proyecto, plantilla, columnasCfg, _config, filas, baseRowsPdf, dlg.FileName, tituloCfg);
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
    }
}
