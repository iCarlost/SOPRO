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
    /// Presentación de resultados y transferencia del porcentaje al proyecto.
    /// </summary>
    public partial class FormFinanciamiento
    {

        private void MostrarResultados()
        {
            lblInteresesNeg.Text = _config.InteresesNegativos.ToStringImporte();
            lblInteresesPos.Text = _config.InteresesPositivos.ToStringImporte();
            lblFinanciamientoNeto.Text = _config.FinanciamientoNeto.ToStringImporte();
            lblPorcentaje.Text = $"{_config.PorcentajeCalculado:N5}%";
            lblFechaCalculo.Text = _config.FechaCalculo.HasValue
                ? $"Calculado: {_config.FechaCalculo.Value:dd/MM/yyyy HH:mm}"
                : "Sin calcular";

            ActualizarEtiquetasReferencia();

            bool tieneResultado = _config.PorcentajeCalculado != 0;
            btnTransferir.Enabled = tieneResultado;
        }

        // ── Transferir al proyecto ────────────────────────────────────────────

        private void btnTransferir_Click(object sender, EventArgs e)
        {
            if (_config.PorcentajeCalculado == 0)
            {
                var r = MessageBox.Show(
                    "El porcentaje calculado es 0%.\\n¿Deseas transferir de todas formas?",
                    "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            try
            {
                var proy = _context.Proyectos.Find(_proyecto.Id);
                if (proy == null) return;

                proy.PorcentajeFinanciamiento = _config.PorcentajeCalculado;
                _context.SaveChanges();

                _proyecto.PorcentajeFinanciamiento = _config.PorcentajeCalculado;

                MessageBox.Show(
                    $"Transferido al proyecto:\n\n" +
                    $"  % Financiamiento: {_config.PorcentajeCalculado:N5}%\n\n" +
                    "Usa el módulo de Porcentajes para revisar\n" +
                    "y aplicar al presupuesto.",
                    "Transferido ✓", MessageBoxButtons.OK, MessageBoxIcon.Information);

                FinanciamientoTransferido?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
