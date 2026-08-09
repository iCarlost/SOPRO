using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Consolidación de insumos de maquinaria en presupuestos.
    /// </summary>
    public partial class FormCatalogoMaquinaria
    {

        public IReadOnlyList<ConsolidacionInsumoItem> ObtenerSeleccionConsolidable()
        {
            return dgvMaquinaria.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as Maquinaria)
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .Select(x => new ConsolidacionInsumoItem
                {
                    Id = x.Id,
                    Clave = x.Clave ?? string.Empty,
                    Descripcion = x.Descripcion ?? string.Empty,
                    Unidad = "hr",
                    Precio = x.CostoHorario,
                    PrecioEtiqueta = string.Format("Costo horario: {0:N4}", x.CostoHorario)
                })
                .ToList();
        }

        public void EjecutarConsolidacion()
        {
            if (!_proyectoId.HasValue)
            {
                MessageBox.Show("La consolidación solo está disponible dentro de un proyecto.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var seleccion = dgvMaquinaria.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as Maquinaria)
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();

            if (seleccion.Count < 2)
            {
                MessageBox.Show("Seleccione al menos dos registros del proyecto para consolidar.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (seleccion.Any(x => x.Origen == OrigenInsumo.Maestro))
            {
                MessageBox.Show("La consolidación solo admite registros del proyecto actual.\n\nQuite de la selección cualquier insumo del catálogo maestro e intente de nuevo.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var items = ObtenerSeleccionConsolidable();
            using var dlg = new FormConsolidarInsumos(NombreTipoConsolidacion, items, baseId =>
                _consolidationService.ObtenerPreview(_context, _proyectoId.Value, ConsolidacionInsumoTipo.Maquinaria, baseId, seleccion.Select(x => x.Id).ToList()));

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resultado = _consolidationService.Consolidar(_context, _proyectoId.Value, ConsolidacionInsumoTipo.Maquinaria, dlg.InsumoBaseId, seleccion.Select(x => x.Id).ToList());
                CargarMaquinaria();
                InsumosModificados?.Invoke(this, EventArgs.Empty);
                EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
                MessageBox.Show($"Consolidación completada.\n\nRegistros sustituidos: {resultado.RegistrosConsolidados}\nComponentes actualizados: {resultado.ComponentesActualizados}\nMatrices afectadas: {resultado.MatricesAfectadas}\nConceptos impactados: {resultado.ConceptosAfectados}", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No fue posible consolidar los insumos:\n{ex.Message}", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
