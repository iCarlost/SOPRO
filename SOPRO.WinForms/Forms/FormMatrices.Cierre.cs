using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Exportación del catálogo, cierre del formulario y recalculo.
    /// </summary>
    public partial class FormMatrices
    {

        private void ExportarCatalogoMatrices()
        {
            if (_listaActual == null || !_listaActual.Any())
            {
                MessageBox.Show("No hay matrices en la vista actual para exportar.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string filtroTitulo = "Todos";
            if (rbSoloAPU.Checked) filtroTitulo = "APU";
            else if (rbSoloBasicos.Checked) filtroTitulo = "Básicos";
            else if (rbSoloCuadrillas.Checked) filtroTitulo = "Cuadrillas";

            using var dlg = new SaveFileDialog
            {
                Title = "Guardar catálogo de matrices",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"CatalogoMatrices_{filtroTitulo}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var proyecto = _context.Proyectos.Find(_proyectoId) ?? new Proyecto { Nombre = "Proyecto" };
                var svcRep = new ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyectoId);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyectoId, ReportTitleModuleKeys.CatalogoMatrices, lblTitulo.Text);
                var generador = new GeneradorExcelCatalogoMatrices(svcRep, _context);

                generador.Generar(proyecto, _listaActual, plantilla, filtroTitulo, dlg.FileName, tituloCfg);

                if (MessageBox.Show(
                    $"Catálogo generado con {_listaActual.Count} matrices.\n\n¿Desea abrir el archivo?",
                    "Exportado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el catálogo:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            FormCatalogoMateriales.InsumosModificados -= _onInsumos;
            FormCatalogoManoObra.InsumosModificados -= _onInsumos;
            FormCatalogoHerramientas.InsumosModificados -= _onInsumos;
            FormCatalogoMaquinaria.InsumosModificados -= _onInsumos;
            FormEditarMatriz.MatrizGuardada -= _onInsumos;
            base.OnFormClosed(e);
        }
        // ── IRecalculable ────────────────────────────────────────────────────
        public void RecalcularTodo() => CargarMatrices();

        private void OnDecimalesActualizados_Mat(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) dgvMatrices.Refresh(); }));
        }
    }
}
