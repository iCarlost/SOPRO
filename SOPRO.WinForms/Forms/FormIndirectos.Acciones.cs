using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;
using SOPRO.WinForms.Undo;
using Sopro.Calculation.Pricing;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Estructura predeterminada, cálculo de porcentajes, resumen y transferencia.
    /// </summary>
    public partial class FormIndirectos
    {

        private void btnCrearPredeterminados_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "¿Crear la estructura predeterminada de indirectos según normativa mexicana?",
                "Confirmar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                try
                {
                    IndirectCostStructureService.CrearEstructuraPredeterminada(_context, _proyecto.Id);
                    CargarDatos();
                    MessageBox.Show("Estructura predeterminada creada correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnCalcular_Click(object sender, EventArgs e)
        {
            try
            {
                // Leer valores de configuración
                if (!decimal.TryParse(txtVolumenAnual.Text, out decimal volumenAnual))
                {
                    MessageBox.Show("Ingresa un volumen anual válido.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!decimal.TryParse(txtCostoDirecto.Text, out decimal costoDirecto))
                {
                    MessageBox.Show("Ingresa un costo directo válido.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Calcular totales - IMPORTANTE: ToList() primero para calcular en memoria
                var gruposOC = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.OficinaCentral)
                    .ToList(); // ← Traer a memoria primero

                var totalOC = gruposOC.Sum(g => g.Total); // ← Ahora sí calcular

                var gruposCampo = _context.GruposIndirectos
                    .Include(g => g.Conceptos)
                    .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.Campo)
                    .ToList(); // ← Traer a memoria primero

                var totalCampo = gruposCampo.Sum(g => g.Total); // ← Ahora sí calcular

                // Calcular porcentajes
                var pctResult = IndirectCostPercentageCalculator.Calculate(new IndirectCostPercentageInput
                {
                    OfficeCentralAnnualTotal = totalOC,
                    AnnualWorkVolume = volumenAnual,
                    FieldTotal = totalCampo,
                    DirectCost = costoDirecto
                });
                decimal porcOC = pctResult.OfficeCentralPercentage;
                decimal porcCampo = pctResult.FieldPercentage;

                // Actualizar configuración
                _configuracion.VolumenAnualObra = volumenAnual;
                _configuracion.CostoDirectoObra = costoDirecto;
                _configuracion.TotalOficinaCentralAnual = totalOC;
                _configuracion.TotalCampo = totalCampo;
                _configuracion.PorcentajeOficinaCentral = porcOC;
                _configuracion.PorcentajeCampo = porcCampo;
                _configuracion.FechaActualizacion = DateTime.Now;

                _context.SaveChanges();
                ActualizarResumen();

                tabControl.SelectedTab = tabConfiguracion;
                MessageBox.Show("Porcentajes calculados correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al calcular: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ActualizarResumen()
        {
            lblTotalOficinaCentral.Text = $"Total Oficina Central Anual: ${_configuracion.TotalOficinaCentralAnual:N2}";
            lblTotalCampo.Text = $"Total Campo: ${_configuracion.TotalCampo:N2}";
            lblPorcentajeOC.Text = $"% Oficina Central: {_configuracion.PorcentajeOficinaCentral:N4}%";
            lblPorcentajeCampo.Text = $"% Campo: {_configuracion.PorcentajeCampo:N4}%";
            lblPorcentajeTotal.Text = $"% TOTAL INDIRECTOS: {_configuracion.PorcentajeTotal:N4}%";

            lblStatus.Text = $"Última actualización: {_configuracion.FechaActualizacion:dd/MMM/yyyy HH:mm}";
        }

        private void btnTransferir_Click(object sender, EventArgs e)
        {
            if (_configuracion.PorcentajeTotal == 0)
            {
                var r = MessageBox.Show(
                    "Los porcentajes calculados son 0%.\n¿Deseas transferir de todas formas?",
                    "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            try
            {
                // Guardar en Proyecto
                var proy = _context.Proyectos.Find(_proyecto.Id);
                if (proy == null) return;

                proy.PorcentajeIndirectosCentral = _configuracion.PorcentajeOficinaCentral;
                proy.PorcentajeIndirectosCampo = _configuracion.PorcentajeCampo;
                _context.SaveChanges();

                // Actualizar en memoria
                _proyecto.PorcentajeIndirectosCentral = _configuracion.PorcentajeOficinaCentral;
                _proyecto.PorcentajeIndirectosCampo = _configuracion.PorcentajeCampo;

                MessageBox.Show(
                    $"Transferido al proyecto:\n\n" +
                    $"  Oficina Central: {_configuracion.PorcentajeOficinaCentral:N4}%\n" +
                    $"  Campo:           {_configuracion.PorcentajeCampo:N4}%\n" +
                    $"  TOTAL:           {_configuracion.PorcentajeTotal:N4}%\n\n" +
                    "Usa el módulo de Porcentajes para asignar\n" +
                    "Financiamiento y Utilidad.",
                    "Transferido ✓", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Disparar evento para notificar a FormPresupuesto
                IndirectosTransferidos?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
