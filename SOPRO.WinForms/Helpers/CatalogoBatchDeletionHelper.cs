using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.UseCases.Catalog;
using SOPRO.Data.Context;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Lógica compartida por los cuatro catálogos de insumos para la eliminación en
    /// lote: deriva los Ids de la selección real del grid, rotula el comando con el
    /// número exacto de recursos y arma la confirmación única con el impacto
    /// previsualizado. La UI no calcula impacto ni decide el alcance: solo presenta
    /// lo que devuelve <c>PreviewCatalogBatchDeletion</c>.
    /// </summary>
    internal static class CatalogoBatchDeletionHelper
    {
        /// <summary>
        /// Aviso para la guarda de contexto compartido: la eliminación escribe por
        /// un contexto nuevo y no debe convivir con ediciones pendientes del
        /// contexto de la UI.
        /// </summary>
        public const string MensajeCambiosSinGuardar =
            "Hay cambios sin guardar en el proyecto. Guarde o descarte esos cambios antes de eliminar insumos.";

        /// <summary>
        /// Detecta ediciones pendientes en el contexto compartido de la UI. El
        /// llamador debe abortar la operación visiblemente: guardar o limpiar el
        /// tracker aquí perdería o alteraría trabajo del usuario.
        /// </summary>
        public static bool HayCambiosSinGuardar(SOPROContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.ChangeTracker.DetectChanges();
            return context.ChangeTracker.HasChanges();
        }

        /// <summary>
        /// Ids distintos y ordenados de las filas seleccionadas, resolviendo la
        /// selección por celda con <see cref="SeleccionGridHelper.FilasSeleccionadas"/>.
        /// Ignora la fila nueva, las filas sin dato enlazado y los Ids no positivos:
        /// un borrador no participa del lote.
        /// </summary>
        public static IReadOnlyList<int> IdsSeleccionados<T>(DataGridView grid, Func<T, int> idSelector)
        {
            if (grid is null) return Array.Empty<int>();
            ArgumentNullException.ThrowIfNull(idSelector);

            var ids = new HashSet<int>();

            foreach (var fila in SeleccionGridHelper.FilasSeleccionadas(grid))
            {
                if (fila is null || fila.IsNewRow) continue;
                if (fila.DataBoundItem is not T item) continue;

                int id = idSelector(item);
                if (id > 0) ids.Add(id);
            }

            return ids.OrderBy(id => id).ToList();
        }

        /// <summary>
        /// Etiqueta del comando de eliminación. El número siempre coincide con el
        /// lote que se va a previsualizar y ejecutar; con una sola fila se omite el
        /// número para no romper la terminología existente del menú.
        /// </summary>
        public static string EtiquetaEliminar(CatalogItemKind kind, int cantidad)
        {
            int n = Math.Max(0, cantidad);
            string sustantivo = Sustantivo(kind, n);

            return n > 1 ? $"Eliminar {n} {sustantivo}" : $"Eliminar {sustantivo}";
        }

        /// <summary>
        /// Mensaje de la confirmación ÚNICA: nombra el número exacto de recursos
        /// seleccionados, el impacto en componentes/matrices y el recálculo del
        /// presupuesto, y advierte que la acción es irreversible.
        /// </summary>
        public static string ConstruirConfirmacion(CatalogBatchDeletionPreview preview)
        {
            ArgumentNullException.ThrowIfNull(preview);

            string seleccion = preview.SelectedCount == 1
                ? $"1 {Sustantivo(preview.Kind, 1)} seleccionado"
                : $"{preview.SelectedCount} {Sustantivo(preview.Kind, preview.SelectedCount)} seleccionados";

            string impacto = preview.IsInUse
                ? $"Impacto: se eliminarán {preview.AffectedComponentCount} componente(s) de " +
                  $"{preview.AffectedMatrixCount} matriz/matrices."
                : "Impacto: no está en uso en ninguna matriz.";

            string recalculo = preview.TriggeredRecalculation
                ? $"Se recalcularán {preview.RecalculatedMatrixCount} matriz/matrices, " +
                  $"{preview.AffectedConceptCount} concepto(s) del presupuesto y " +
                  $"{preview.AffectedAggregatorCount} agrupador(es)."
                : "No se requiere recálculo del presupuesto.";

            return $"¿Eliminar {seleccion}?\n\n{impacto}\n{recalculo}\n\n" +
                   "Esta acción no se puede deshacer.";
        }

        /// <summary>
        /// Decide si la tecla Supr debe despachar la eliminación. Vale tanto Supr
        /// como Ctrl+Supr; no se despacha mientras la celda está en edición (el
        /// texto debe recibir la tecla) ni sin selección, y se ignoran Shift/Alt.
        /// </summary>
        public static bool DebeDespacharSupr(
            Keys keyCode,
            bool shift,
            bool alt,
            bool enEdicionCelda,
            bool tieneSeleccion)
            => keyCode == Keys.Delete
               && !shift
               && !alt
               && !enEdicionCelda
               && tieneSeleccion;

        private static string Sustantivo(CatalogItemKind kind, int cantidad)
        {
            bool plural = cantidad > 1;

            return kind == CatalogItemKind.Herramienta
                ? (plural ? "elementos" : "elemento")
                : (plural ? "insumos" : "insumo");
        }
    }
}
