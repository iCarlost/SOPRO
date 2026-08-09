using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Generación del PDF de la explosión de insumos.
    /// </summary>
    public partial class FormExplosionInsumos
    {

        public void GenerarPdfExplosion()
        {
            try
            {
                if (_ultMateriales == null)
                {
                    MessageBox.Show("No hay datos para exportar. Genere la explosión primero.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyectoId);
                var columnas = Helpers.ColumnasExplosionHelper.ObtenerColumnas(_context, _proyectoId);
                string filtro = cmbFiltro.SelectedItem?.ToString() ?? "Todos";

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF de Explosión de Insumos",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"ExplosionInsumos_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId, ReportTitleModuleKeys.ExplosionInsumos, lblTitulo.Text);
                var generador = new Services.GeneradorPdfExplosion(svc);
                string ruta = generador.Generar(
                    _proyecto, plantilla, columnas, filtro,
                    _ultMateriales, _ultManoObra, _ultMaquinaria, _ultHerramientas,
                    _ultCostoDirectoTotal, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte PDF:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
