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
    /// Exportación y cierre del formulario de explosión.
    /// </summary>
    public partial class FormExplosionInsumos
    {

        private void btnExportar_Click(object sender, EventArgs e)
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
                var plantilla = svc.ObtenerPlantillaSoloLectura(_proyectoId);
                // Snapshot neutral compartido con PDF: misma lista/orden/ancho/
                // formato/estilo. Las columnas se leen sin persistir; si no hay, el
                // resolver usa sus defaults EN MEMORIA (sin SaveChanges).
                var columnas = _context.ColumnasExplosion
                    .Where(c => c.ProyectoId == _proyectoId)
                    .OrderBy(c => c.Orden)
                    .ToList();
                // Snapshot neutral poblado con los overrides de celda/rol persistidos
                // (paso 6.1): misma fuente que PDF.
                var snapshot = ConstruirSnapshotExplosion(columnas);
                string filtro = cmbFiltro.SelectedItem?.ToString() ?? "Todos";

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Explosión de Insumos",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"ExplosionInsumos_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerSoloLectura(_proyectoId, ReportTitleModuleKeys.ExplosionInsumos, lblTitulo.Text);
                var generador = new Services.GeneradorExcelExplosion(svc);
                string ruta = generador.Generar(
                    _proyecto, plantilla, snapshot, filtro,
                    _ultMateriales, _ultManoObra, _ultMaquinaria, _ultHerramientas,
                    _ultCostoDirectoTotal, dlg.FileName, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show($"Reporte generado.\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al generar el reporte:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
