using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Helpers;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Aplicación de parámetros y exportación del FSR a Excel.
    /// </summary>
    public partial class FormFSR
    {

        private async void btnAplicar_Click(object sender, EventArgs e)
        {
            var manoObras = _context.ManoDeObra
                .Where(m => m.ProyectoId == _proyecto.Id)
                .ToList();

            var r = MessageBox.Show(
                $"¿Calcular y aplicar FSR individual a {manoObras.Count} insumo(s) de Mano de Obra?\n\n" +
                $"Cada insumo recibirá su propio FSR calculado con su Salario Base.\n" +
                $"Los parámetros de cálculo (días, porcentajes IMSS, etc.) son los actuales.",
                "Aplicar FSR",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (r != DialogResult.Yes) return;

            try
            {
                int actualizados = 0;
                foreach (var mo in manoObras)
                {
                    // Cada insumo usa su propio SalarioBase como Salario Nominal
                    decimal sn = mo.SalarioBase;
                    if (sn <= 0) continue; // Si no tiene salario, saltar

                    mo.FactorSalarioReal = CalcularFSR(sn);
                    mo.CalcularSalarioReal();
                    actualizados++;
                }

                await _context.SaveChangesAsync();

                // Guardar el FSR de referencia del formulario (con nudSalarioNominal) en el proyecto
                _proyecto.FactorSalarioReal = FSR_FSR;
                _proyecto.FechaCalculoFSR   = DateTime.Now;
                _proyecto.ParametrosFSR     = GuardarParametros();
                await _context.SaveChangesAsync();

                MessageBox.Show(
                    $"FSR individual aplicado exitosamente.\n" +
                    $"Se actualizaron {actualizados} insumo(s) de Mano de Obra con su propio FSR.",
                    "Aplicado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al aplicar FSR:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public bool GenerarReporteExcelRibbon()
        {
            if (string.IsNullOrEmpty(_proyecto.ParametrosFSR))
            {
                MessageBox.Show("Primero aplique el FSR al proyecto para guardar los parámetros.",
                    "Sin parámetros", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            using var dlg = new SaveFileDialog
            {
                Title    = "Guardar reporte",
                Filter   = "Excel (*.xlsx)|*.xlsx",
                FileName = $"CalculoFSR_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return false;

            try
            {
                _proyecto.ParametrosFSR = GuardarParametros();

                var svcRep    = new Services.ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.FSR, lblTitulo.Text);

                using var wb = new ClosedXML.Excel.XLWorkbook();
                Services.GeneradorExcelFSR.GenerarAE2A(wb, _proyecto, plantilla, svcRep, tituloCfg);
                wb.SaveAs(dlg.FileName);

                if (MessageBox.Show("Reporte generado exitosamente.\n¿Desea abrirlo?",
                    "Reporte generado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar reporte:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void btnExportarFSR_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_proyecto.ParametrosFSR))
            {
                MessageBox.Show("Primero aplique el FSR al proyecto para guardar los parámetros.",
                    "Sin parámetros", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title    = "Guardar reporte",
                Filter   = "Excel (*.xlsx)|*.xlsx",
                FileName = $"CalculoFSR_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                // Guardar parámetros actuales antes de exportar
                _proyecto.ParametrosFSR = GuardarParametros();

                var svcRep    = new Services.ReporteService(_context);
                var plantilla = svcRep.ObtenerOCrearPlantilla(_proyecto.Id);
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.FSR, lblTitulo.Text);

                using var wb = new ClosedXML.Excel.XLWorkbook();
                Services.GeneradorExcelFSR.GenerarAE2A(wb, _proyecto, plantilla, svcRep, tituloCfg);
                wb.SaveAs(dlg.FileName);

                if (MessageBox.Show("Reporte generado exitosamente.\n¿Desea abrirlo?",
                    "Reporte generado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar reporte:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
