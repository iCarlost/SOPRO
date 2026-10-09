using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Generación de reportes (PDF/Excel) del presupuesto y columnas de reporte.
    /// </summary>
    public partial class FormPresupuesto
    {

        public void GenerarPdfPresupuesto()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                // Snapshot neutral desde la configuración persistida (no desde el grid).
                var snapshot = ConstruirSnapshotPresupuesto(svc);

                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show("El presupuesto no tiene conceptos.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Presupuesto_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                decimal factorPU = CalcularFactorPU();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Presupuesto, lblTitulo.Text);
                var gen = new Services.GeneradorPdfPresupuesto(svc);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, snapshot, dlg.FileName, factorPU, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte PDF:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GenerarPdfAPU()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                var conceptosConAPU = conceptos
                    .Where(c => !c.EsAgrupador && c.MatrizId.HasValue)
                    .ToList();

                if (!conceptosConAPU.Any())
                {
                    MessageBox.Show(
                        "No hay conceptos con APU vinculado en este presupuesto." +
                        "Vincula una Matriz APU a cada concepto desde la columna correspondiente.",
                        "Sin APU", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte APU PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = "APU_" + _proyecto.Nombre + "_" + DateTime.Now.ToString("yyyyMMdd") + ".pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var estiloDescripcionApu = ObtenerEstiloDescripcionPresupuestoParaApu(svc);
                var gen = new Services.GeneradorPdfAPU(svc, _context);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, dlg.FileName, estiloDescripcionApu);
                Cursor = Cursors.Default;

                if (MessageBox.Show("APUs PDF generados:" + ruta + "¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar APUs PDF:" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GenerarExcelPresupuesto()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);

                // Snapshot neutral desde la configuración persistida (no desde el grid).
                var snapshot = ConstruirSnapshotPresupuesto(svc);

                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show("El presupuesto no tiene conceptos.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Excel",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Presupuesto_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                decimal factorPU = CalcularFactorPU();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Presupuesto, lblTitulo.Text);
                var gen = new Services.GeneradorExcelPresupuesto(svc);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, snapshot, dlg.FileName, factorPU, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte generado:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        /// <summary>
        /// Construye el snapshot neutral de columnas del reporte de Presupuesto a
        /// partir de la configuración persistida (columnas personalizadas + overlays
        /// de encabezado del reporte). NO lee del <c>DataGridView</c>.
        /// </summary>
        private Application.Models.Reporting.ReportColumns.ReportColumnSnapshot ConstruirSnapshotPresupuesto(Services.ReporteService svc)
        {
            var columnas = _context.ColumnasPersonalizadas
                .Where(c => c.ProyectoId == _proyecto.Id)
                .OrderBy(c => c.Orden)
                .ToList();

            var overlays = svc.ObtenerOCrearColumnas(_proyecto.Id, "Presupuesto");

            return new Application.UseCases.Reporting.PresupuestoReportSnapshotBuilder()
                .Build(_proyecto.Id, lblTitulo.Text, columnas, overlays);
        }

        private Core.Entities.ConfigColumnaReporte ObtenerEstiloDescripcionPresupuestoParaApu(Services.ReporteService svc)
        {
            var columnasReporte = svc.ObtenerOCrearColumnas(_proyecto.Id, "Presupuesto");
            var descripcionReporte = columnasReporte.FirstOrDefault(c => c.NombreInterno == "Descripcion");

            if (descripcionReporte == null)
            {
                descripcionReporte = new Core.Entities.ConfigColumnaReporte
                {
                    NombreInterno = "Descripcion",
                    Encabezado = "Descripción",
                    ConFuente = "Segoe UI",
                    ConTamaño = 9f,
                    ConColorFondo = "#FFFFFF",
                    ConColorTexto = "#000000",
                    ConAlineacion = "Izquierda"
                };
            }

            return descripcionReporte;
        }

        public void GenerarExcelAPU()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id && !c.EsAgrupador && c.MatrizId.HasValue)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show(
                        "No hay conceptos con APU vinculado en este presupuesto.\n\n" +
                        "Vincula una Matriz APU a cada concepto desde la columna correspondiente.",
                        "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnas = svc.ObtenerOCrearColumnas(_proyecto.Id, "APU");

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte APU",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = "APU_" + _proyecto.Nombre + "_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var estiloDescripcionApu = ObtenerEstiloDescripcionPresupuestoParaApu(svc);
                var gen = new Services.GeneradorExcelAPU(svc, _context);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, columnas, dlg.FileName, estiloDescripcionApu);
                Cursor = Cursors.Default;

                if (MessageBox.Show("APUs generados:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar APUs:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnColumnas_Click(object sender, EventArgs e)
        {
            // Guardar la columna actual para insertar nuevas columnas a su derecha
            int columnaActualIndex = dgvPresupuesto.CurrentCell?.ColumnIndex ?? -1;

            using var form = new FormColumnasPersonalizadas(_context, _proyecto.Id, columnaActualIndex);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                try
                {
                    // Solo guardar el orden visual actual de columnas
                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            var columnaDB = _context.ColumnasPersonalizadas.Find(colDef.Id);
                            if (columnaDB != null)
                                columnaDB.Orden = col.DisplayIndex;
                        }
                    }
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al guardar orden de columnas:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Recargar presupuesto con nuevas columnas, preservando fila/scroll/celda cuando sea posible
                RecargarPresupuestoPreservandoEstado();
            }
        }
    }
}
