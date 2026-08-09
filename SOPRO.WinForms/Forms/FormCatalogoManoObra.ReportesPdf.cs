using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Generación de reportes PDF del catálogo y tabulador FSR.
    /// </summary>
    public partial class FormCatalogoManoObra
    {

        public bool GenerarReporteExcel()
        {
            ExportarCatalogoExcel();
            return true;
        }

        public void GenerarPdfCatalogoManoObra()
        {
            var lista = dgvManoObra.DataSource as List<ManoDeObra>;
            if (lista == null || !lista.Any())
            {
                MessageBox.Show("No hay registros para exportar.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlgTipo = new FormSeleccionReporteMO();
            if (dlgTipo.ShowDialog(this) != DialogResult.OK) return;

            var svcRep = new ReporteService(_context);
            var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId ?? 0);
            Proyecto proyecto = _proyectoId.HasValue ? _context.Proyectos.Find(_proyectoId.Value) : new Proyecto { Nombre = "Mano de Obra" };
            if (proyecto == null)
                proyecto = new Proyecto { Nombre = "Mano de Obra" };

            var tituloCfg = _proyectoId.HasValue
                ? new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId.Value, ReportTitleModuleKeys.CatalogoManoObra, lblTitulo.Text)
                : null;
            var gen = new GeneradorPdfCatalogoManoObra(svcRep);
            string ruta;
            if (dlgTipo.Seleccion == FormSeleccionReporteMO.TipoReporte.TabuladorFSR)
            {
                if (string.IsNullOrEmpty(proyecto.ParametrosFSR))
                {
                    MessageBox.Show(
                        "Este proyecto no tiene parámetros FSR configurados.Configure el FSR en el módulo correspondiente antes de generar este reporte.",
                        "Sin parámetros FSR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dlgPdfFsr = new SaveFileDialog
                {
                    Title = "Guardar tabulador FSR en PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"TabuladorFSR_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                };
                if (dlgPdfFsr.ShowDialog(this) != DialogResult.OK) return;

                ruta = gen.GenerarTabuladorFsr(proyecto, lista, plantilla, dlgPdfFsr.FileName);
            }
            else
            {
                using var dlgPdf = new SaveFileDialog
                {
                    Title = "Guardar catálogo de mano de obra en PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Catalogo_ManoObra_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
                };
                if (dlgPdf.ShowDialog(this) != DialogResult.OK) return;

                ruta = gen.GenerarCatalogo(proyecto, lista, plantilla, _columnasConfig, dlgPdf.FileName, tituloCfg);
            }

            if (MessageBox.Show("Reporte PDF generado exitosamente.¿Desea abrirlo?", "PDF generado",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
            }
        }
    }
}
