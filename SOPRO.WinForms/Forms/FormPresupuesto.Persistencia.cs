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
    /// Persistencia: guardado de cambios, construcción de filas y estadísticas.
    /// </summary>
    public partial class FormPresupuesto
    {

        private void GuardarCambios()
        {
            try
            {
                var rows = ConstruirFilasPresupuesto(includeOnlyExisting: true);
                BudgetPersistenceService.ApplyAutoSaveChanges(_context, rows);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-guardado: {ex.Message}");
            }
        }

        private List<BudgetConceptRowDto> ConstruirFilasPresupuesto(bool includeOnlyExisting = false)
        {
            var rows = new List<BudgetConceptRowDto>();

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var row = dgvPresupuesto.Rows[i];
                var concepto = row.Tag as ConceptoPresupuesto;
                if (includeOnlyExisting && concepto == null) continue;

                var tipo = ObtenerCeldaTexto(row.Index, "Tipo", "Concepto");
                var descripcion = ObtenerCeldaTexto(row.Index, "Descripcion");
                if (!includeOnlyExisting && string.IsNullOrWhiteSpace(descripcion)) continue;

                var dto = new BudgetConceptRowDto
                {
                    ExistingConceptId = concepto?.Id > 0 ? concepto.Id : null,
                    Tipo = tipo,
                    Clave = ObtenerCeldaTexto(row.Index, "Clave"),
                    Descripcion = descripcion,
                    Unidad = ObtenerCeldaTexto(row.Index, "Unidad"),
                    Cantidad = string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase)
                        ? ObtenerCeldaDecimal(row.Index, "Cantidad")
                        : 0m,
                    MatrizId = concepto?.MatrizId,
                    CostoDirectoUnitario = concepto?.CostoDirectoUnitario ?? 0m,
                    CostoDirectoTotal = concepto?.CostoDirectoTotal ?? 0m,
                    PrecioUnitario = concepto?.PrecioUnitario ?? ObtenerCeldaDecimal(row.Index, "PrecioUnitario"),
                    ImporteTotal = concepto?.ImporteTotal ?? ObtenerCeldaDecimal(row.Index, "Importe"),
                    Orden = i
                };

                rows.Add(dto);
            }

            return rows;
        }

        private string ObtenerCeldaTexto(int rowIndex, string nombreInterno, string defaultValue = "")
        {
            return ObtenerCeldaPorNombreInterno(rowIndex, nombreInterno)?.Value?.ToString() ?? defaultValue;
        }

        private decimal ObtenerCeldaDecimal(int rowIndex, string nombreInterno)
        {
            var cell = ObtenerCeldaPorNombreInterno(rowIndex, nombreInterno);
            if (cell?.Value == null) return 0m;

            if (cell.Value is decimal dec)
                return dec;

            if (cell.Value is int i)
                return i;

            if (cell.Value is double d)
                return (decimal)d;

            var value = cell.Value.ToString();
            if (string.IsNullOrWhiteSpace(value)) return 0m;

            string limpio = value.Replace("$", string.Empty).Trim();
            if (decimal.TryParse(limpio, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out decimal result))
                return result;

            decimal.TryParse(limpio.Replace(",", string.Empty), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result);
            return result;
        }

        private void ActualizarEstadisticas()
        {
            // Contar conceptos directamente del grid (no de BD)
            int totalConceptos = 0;
            decimal totalImporte = 0;

            foreach (DataGridViewRow row in dgvPresupuesto.Rows)
            {
                var tipoCell = ObtenerCeldaPorNombreInterno(row.Index, "Tipo");
                if (tipoCell?.Value?.ToString() == "Concepto")
                {
                    totalConceptos++;

                    // Sumar importe
                    var importeCell = ObtenerCeldaPorNombreInterno(row.Index, "Importe");
                    if (importeCell?.Value != null)
                    {
                        // Limpiar formato de moneda ($, comas, etc.)
                        string valorStr = importeCell.Value.ToString()
                            .Replace("$", "")
                            .Replace(",", "")
                            .Trim();

                        if (decimal.TryParse(valorStr, out decimal importe))
                        {
                            totalImporte += importe;
                        }
                    }
                }
            }

            lblTotalConceptos.Text = $"{totalConceptos} conceptos";
            lblCostoDirecto.Text = totalImporte.ToStringImporte();
        }

        private void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                // Limpiar conceptos existentes
                var conceptosExistentes = _context.ConceptosPresupuesto.Where(c => c.ProyectoId == _proyecto.Id);
                _context.ConceptosPresupuesto.RemoveRange(conceptosExistentes);

                int orden = 0;
                foreach (DataGridViewRow row in dgvPresupuesto.Rows)
                {
                    var tipoCell = ObtenerCeldaPorNombreInterno(row.Index, "Tipo");
                    if (tipoCell?.Value == null) continue;

                    string tipo = tipoCell.Value.ToString();

                    var claveCell = ObtenerCeldaPorNombreInterno(row.Index, "Clave");
                    var descCell = ObtenerCeldaPorNombreInterno(row.Index, "Descripcion");

                    string clave = claveCell?.Value?.ToString() ?? "";
                    string desc = descCell?.Value?.ToString() ?? "";

                    if (string.IsNullOrWhiteSpace(desc)) continue;

                    bool esAgrupador = tipo != "Concepto";
                    int nivel = ObtenerNivelDesdeTipo(tipo);

                    var unidadCell = ObtenerCeldaPorNombreInterno(row.Index, "Unidad");

                    var concepto = new ConceptoPresupuesto
                    {
                        ProyectoId = _proyecto.Id,
                        Clave = clave,
                        Descripcion = desc,
                        EsAgrupador = esAgrupador,
                        Nivel = nivel,
                        Orden = orden++,
                        Unidad = unidadCell?.Value?.ToString() ?? string.Empty,
                        ColumnasPersonalizadasJSON = string.Empty,
                        Notas = string.Empty
                    };

                    if (!esAgrupador)
                    {
                        var cantidadCell = ObtenerCeldaPorNombreInterno(row.Index, "Cantidad");
                        decimal.TryParse(cantidadCell?.Value?.ToString(), out decimal cantidad);
                        concepto.Cantidad = cantidad;

                        // Obtener matriz del Tag si existe
                        if (row.Tag is ConceptoPresupuesto conceptoTemp && conceptoTemp.MatrizId.HasValue)
                        {
                            concepto.MatrizId = conceptoTemp.MatrizId;
                            concepto.CostoDirectoUnitario = conceptoTemp.CostoDirectoUnitario;
                            concepto.CostoDirectoTotal = conceptoTemp.CostoDirectoTotal;
                        }
                        else
                        {
                            // Intentar parsear P.U. e Importe de las celdas
                            var puCell = ObtenerCeldaPorNombreInterno(row.Index, "PrecioUnitario");
                            var impCell = ObtenerCeldaPorNombreInterno(row.Index, "Importe");

                            string puStr = puCell?.Value?.ToString().Replace("$", "").Replace(",", "") ?? "0";
                            string impStr = impCell?.Value?.ToString().Replace("$", "").Replace(",", "") ?? "0";
                            decimal.TryParse(puStr, out decimal pu);
                            decimal.TryParse(impStr, out decimal imp);
                            concepto.CostoDirectoUnitario = pu;
                            concepto.CostoDirectoTotal = imp;
                        }
                    }

                    _context.ConceptosPresupuesto.Add(concepto);
                }

                _context.SaveChanges();
                MessageBox.Show("Presupuesto guardado exitosamente.", "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ActualizarEstadisticas();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
