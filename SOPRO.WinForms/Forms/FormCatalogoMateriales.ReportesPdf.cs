using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.UseCases.Materials;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Generación de reportes PDF del catálogo de materiales.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        public bool GenerarReporteExcel()
        {
            ExportarCatalogoExcel();
            return true;
        }


        public void GenerarPdfCatalogoMateriales()
        {
            try
            {
                var materiales = dgvMateriales.DataSource as List<MaterialListItem>;
                if (materiales == null || !materiales.Any())
                {
                    MessageBox.Show("No hay materiales para exportar.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar catálogo de materiales en PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Catalogo_Materiales_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
                var proyecto = new Proyecto { Nombre = _proyectoId.HasValue ? _sessionInfo.Project.Name : "Materiales" };
                var colsVis = _columnasConfig.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
                var tituloCfg = _proyectoId.HasValue
                    ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoMateriales, lblTitulo.Text)
                    : null;
                var generador = new GeneradorPdfCatalogoMateriales(svcRep);
                var ruta = generador.Generar(proyecto, materiales, plantilla, colsVis, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Catálogo PDF exportado.¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al exportar PDF:{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
