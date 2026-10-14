using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Utilidades de selección para grids en modo
    /// <see cref="DataGridViewSelectionMode.CellSelect"/> (formato por celda).
    ///
    /// Con selección POR CELDA un clic sobre una celda NO puebla
    /// <see cref="DataGridView.SelectedRows"/> (que sólo contiene filas completas).
    /// Las operaciones por FILA (editar, eliminar, consolidar) necesitan resolver
    /// igualmente la(s) fila(s) que el usuario tocó, por lo que este helper combina
    /// ambas fuentes de selección.
    /// </summary>
    public static class SeleccionGridHelper
    {
        /// <summary>
        /// Fuerza en el grid la selección POR CELDAS (tipo Excel): fija
        /// <see cref="DataGridViewSelectionMode.CellSelect"/> y habilita
        /// multi-selección para que un rango (Shift) o varios rangos (Ctrl) se
        /// reporten como celdas concretas. Es requisito del formato por celda: con
        /// <c>FullRowSelect</c> el clic sobre una celda selecciona TODA la fila y el
        /// ribbon aplicaría el override a todas sus columnas (hallazgo H6b).
        /// </summary>
        public static void ConfigurarSeleccionPorCelda(DataGridView grid)
        {
            if (grid is null) return;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.MultiSelect = true;
        }

        /// <summary>
        /// Filas seleccionadas del grid, combinando <see cref="DataGridView.SelectedRows"/>
        /// (filas completas) con las filas distintas de <see cref="DataGridView.SelectedCells"/>
        /// (selección por celda). Deduplica y ordena por índice de fila (ascendente),
        /// de modo que la primera fila devuelta sea siempre la de menor índice.
        /// </summary>
        public static IReadOnlyList<DataGridViewRow> FilasSeleccionadas(DataGridView grid)
        {
            if (grid is null) return new List<DataGridViewRow>();

            var indices = new HashSet<int>();

            foreach (DataGridViewRow fila in grid.SelectedRows)
            {
                if (fila is null || fila.Index < 0) continue;
                indices.Add(fila.Index);
            }

            foreach (DataGridViewCell celda in grid.SelectedCells)
            {
                if (celda is null || celda.RowIndex < 0) continue;
                indices.Add(celda.RowIndex);
            }

            var resultado = new List<DataGridViewRow>(indices.Count);
            foreach (var indice in indices.OrderBy(i => i))
            {
                if (indice >= 0 && indice < grid.Rows.Count)
                    resultado.Add(grid.Rows[indice]);
            }

            return resultado;
        }
    }
}
