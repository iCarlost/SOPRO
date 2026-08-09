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
    /// Carga inicial, parámetros del modelo y ejecución del cálculo.
    /// </summary>
    public partial class FormFinanciamiento
    {

        private void FormFinanciamiento_Load(object sender, EventArgs e)
        {
            try
            {
                _cargando = true;
                _config = _service.ObtenerOCrear(_context, _proyecto.Id);

                lblProyecto.Text = $"📁 {_proyecto.Nombre}";

                // Cargar parámetros guardados
                nudTIIE.Value = Math.Min(_config.TasaTIIE, nudTIIE.Maximum);
                nudPuntos.Value = Math.Min(_config.PuntosAdicionales, nudPuntos.Maximum);
                nudAnticipo.Value = Math.Min(_config.PorcentajeAnticipo, nudAnticipo.Maximum);
                nudPeriodosAmort.Value = 1;
                nudPeriodosAmort.Enabled = false;
                nudDesfase.Value = Math.Min(_config.DesfaseCobro, nudDesfase.Maximum);
                rbAcumulable.Checked = _config.BaseCalculo == "Acumulable";
                rbSobreCD.Checked = _config.BaseCalculo == "SobreCD";

                // Verificar si hay programa de obra
                bool tienePrograma = _context.ProgramasObra
                    .Any(p => p.ProyectoId == _proyecto.Id && p.Activo);

                if (!tienePrograma)
                {
                    lblSinPrograma.Visible = true;
                    btnCalcular.Enabled = false;
                }

                ActualizarTasaEfectiva();
                MostrarResultados();
                ActualizarEtiquetasReferencia();

                // Cargar flujo si ya fue calculado
                if (_config.FilasFlujo.Count > 0 || _context.FilasFlujoCajaFinanciamiento
                        .Any(f => f.ConfiguracionFinanciamientoId == _config.Id))
                    CargarTablaFlujo();
                AplicarLayoutColumnas();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No fue posible inicializar la configuración de financiamiento para este proyecto." + "El esquema detectado no es compatible o el proyecto fue creado por una versión intermedia/importador en pruebas." +
                    $"Detalle técnico: {ex.Message}",
                    "Financiamiento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
            }
            finally
            {
                _cargando = false;
            }
        }

        // ── Parámetros ────────────────────────────────────────────────────────

        private void ActualizarTasaEfectiva()
        {
            lblTasaEfectiva.Text = $"Tasa efectiva: {nudTIIE.Value + nudPuntos.Value:N4}% anual";
        }

        private void nudTIIE_ValueChanged(object s, EventArgs e) { if (!_cargando) ActualizarTasaEfectiva(); }
        private void nudPuntos_ValueChanged(object s, EventArgs e) { if (!_cargando) ActualizarTasaEfectiva(); }
        private void ConfigurarComboModeloFinanciamiento()
        {
            cboModeloFinanciamiento.Items.Clear();
            cboModeloFinanciamiento.Items.AddRange(new object[] { "Clásico", "Dual" });
            cboModeloFinanciamiento.DropDownStyle = ComboBoxStyle.DropDownList;
            cboModeloFinanciamiento.SelectedIndexChanged -= cboModeloFinanciamiento_SelectedIndexChanged;
            cboModeloFinanciamiento.SelectedIndexChanged += cboModeloFinanciamiento_SelectedIndexChanged;
            cboModeloFinanciamiento.SelectedIndex = 0;
        }

        private void cboModeloFinanciamiento_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cargando || _config == null)
                return;

            RecalcularTodo();
        }

        private bool EsModeloDualSeleccionado()
            => string.Equals(Convert.ToString(cboModeloFinanciamiento.SelectedItem), "Dual", StringComparison.OrdinalIgnoreCase);

        private decimal GetTasaPeriodoLabel(int diasPeriodo, decimal saldoAcumulado)
        {
            decimal tasaAnual = (_config.TasaTIIE + _config.PuntosAdicionales) / 100m;
            if (EsModeloDualSeleccionado() && saldoAcumulado > 0m)
                tasaAnual = _config.TasaTIIE / 100m;

            if (tasaAnual <= 0m || diasPeriodo <= 0)
                return 0m;
            return decimal.Round(tasaAnual * diasPeriodo / 365m * 100m, 4, MidpointRounding.AwayFromZero);
        }


        // ── Calcular ──────────────────────────────────────────────────────────

        private void btnCalcular_Click(object sender, EventArgs e)
        {
            try
            {
                GuardarParametros();
                Cursor = Cursors.WaitCursor;

                decimal porcF = _service.Calcular(_context, _config, _proyecto, EsModeloDualSeleccionado());

                // Recargar entidad con filas
                _context.Entry(_config).Reload();
                _context.Entry(_config).Collection(c => c.FilasFlujo).Load();

                CargarTablaFlujo();
                AplicarLayoutColumnas();
                MostrarResultados();
                ActualizarEtiquetasReferencia();

                Cursor = Cursors.Default;
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Error al calcular:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GuardarParametros()
        {
            if (_config == null)
                return;

            _config.TasaTIIE = nudTIIE.Value;
            _config.PuntosAdicionales = nudPuntos.Value;
            _config.PorcentajeAnticipo = nudAnticipo.Value;
            _config.PeriodosAmortizacionAnticipo = 1;
            _config.DesfaseCobro = (int)nudDesfase.Value;
            _config.BaseCalculo = rbAcumulable.Checked ? "Acumulable" : "SobreCD";
            _context.SaveChanges();
        }
    }
}
