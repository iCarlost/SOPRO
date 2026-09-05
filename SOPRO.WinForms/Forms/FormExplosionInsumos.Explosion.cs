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
    /// Generación de la explosión de insumos y renderizado de filas.
    /// </summary>
    public partial class FormExplosionInsumos
    {

        private void GenerarExplosion()
        {
            var estado = DataGridViewStateHelper.Capture(dgvExplosion);

            SuspendLayout();
            dgvExplosion.SuspendLayout();
            try
            {
                dgvExplosion.Rows.Clear();

                string filtro = cmbFiltro.SelectedItem?.ToString() ?? "Todos";
                var data = _explosionService.Calculate(_context, _proyectoId, filtro);

                if (!data.TieneConceptos)
                {
                    MessageBox.Show("No hay conceptos con matrices en el presupuesto.", "Aviso",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _ultMateriales   = ConvertirDatos(data.Materiales);
                _ultManoObra     = ConvertirDatos(data.ManoObra);
                _ultMaquinaria   = ConvertirDatos(data.Maquinaria);
                _ultHerramientas = ConvertirDatos(data.Herramientas);
                _ultCostoDirectoTotal = data.CostoDirectoTotal;

                RenderRows(data.Rows);

                DataGridViewStateHelper.Restore(dgvExplosion, estado);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar la explosión:\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                dgvExplosion.ResumeLayout(true);
                ResumeLayout(true);
            }
        }

        private static Dictionary<int, DatosInsumo> ConvertirDatos(Dictionary<int, ExplosionInsumoAccumulated> source)
        {
            var result = new Dictionary<int, DatosInsumo>();
            foreach (var item in source)
            {
                result[item.Key] = new DatosInsumo
                {
                    Clave = item.Value.Clave,
                    Descripcion = item.Value.Descripcion,
                    Unidad = item.Value.Unidad,
                    Cantidad = item.Value.Cantidad,
                    CantidadFisica = item.Value.CantidadFisica,
                    PrecioUnitario = item.Value.PrecioUnitario,
                    EsPorcentual = item.Value.EsPorcentual
                };
            }
            return result;
        }

        private void RenderRows(List<ExplosionRowDisplay> rows)
        {
            if (rows == null || rows.Count == 0) return;

            var gridRows = _gridRenderService.BuildRows(dgvExplosion, rows);
            if (gridRows.Length > 0)
                dgvExplosion.Rows.AddRange(gridRows);
        }

        private void cmbFiltro_SelectedIndexChanged(object sender, EventArgs e)
        {
            GenerarExplosion();
        }
    }
}
