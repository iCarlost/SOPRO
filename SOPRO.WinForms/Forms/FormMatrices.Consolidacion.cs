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
    /// Consolidación de matrices seleccionadas en presupuestos.
    /// </summary>
    public partial class FormMatrices
    {

        public IReadOnlyList<ConsolidacionInsumoItem> ObtenerSeleccionConsolidable()
        {
            return ObtenerMatricesSeleccionadas()
                .Select(x => new ConsolidacionInsumoItem
                {
                    Id = x.Id,
                    Clave = x.Clave ?? string.Empty,
                    Descripcion = x.Descripcion ?? string.Empty,
                    Unidad = x.Unidad ?? string.Empty,
                    Precio = x.CostoDirecto,
                    PrecioEtiqueta = string.Format("Costo directo: {0:N4}", x.CostoDirecto)
                })
                .ToList();
        }

        public void EjecutarConsolidacion()
        {
            var seleccion = ObtenerMatricesSeleccionadas();
            if (seleccion.Count < 2)
            {
                MessageBox.Show("Seleccione al menos dos matrices del mismo tipo para consolidar.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var tipos = seleccion.Select(x => x.Tipo).Distinct().ToList();
            if (tipos.Count != 1 || tipos[0] == TipoMatriz.APU)
            {
                MessageBox.Show("Solo se pueden consolidar matrices del mismo tipo y únicamente Básicos o Cuadrillas.", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var tipo = tipos[0];
            var items = ObtenerSeleccionConsolidable();
            using var dlg = new FormConsolidarInsumos(NombreTipoConsolidacion, items, baseId =>
                _matrixConsolidationService.ObtenerPreview(_context, _proyectoId, tipo, baseId, seleccion.Select(x => x.Id).ToList()));

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resultado = _matrixConsolidationService.Consolidar(_context, _proyectoId, tipo, dlg.InsumoBaseId, seleccion.Select(x => x.Id).ToList());
                CargarMatrices();
                EstadoConsolidacionCambiado?.Invoke(this, EventArgs.Empty);
                MessageBox.Show($"Consolidación completada.\n\nRegistros sustituidos: {resultado.RegistrosConsolidados}\nComponentes actualizados: {resultado.ComponentesActualizados}\nMatrices afectadas: {resultado.MatricesAfectadas}\nConceptos impactados: {resultado.ConceptosAfectados}", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No fue posible consolidar las matrices:\n{ex.Message}", "Consolidar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private List<Matriz> ObtenerMatricesSeleccionadas()
        {
            return dgvMatrices.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .Select(r => (r.DataBoundItem as MatrixGridRowDisplay)?.Source)
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();
        }
    }
}
