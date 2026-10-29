using SOPRO.Application.Models.Presupuesto;

namespace SOPRO.Application.Services
{
    public static class BudgetGridInteractionService
    {
        /// <summary>
        /// Bloque de filas que un arrastre moverá como una sola unidad. Cuando la
        /// selección del usuario es multi-fila completa y contigua, parte de esas
        /// filas; al expandir agrupadores seleccionados incorpora sus descendientes
        /// para no dejarlos huérfanos.
        /// </summary>
        public sealed class RowDragBlock
        {
            public static RowDragBlock Empty { get; } = new();

            public int OriginRow { get; init; } = -1;
            public bool IsMultiSelection { get; init; }
            public bool ExpandedHierarchy { get; init; }
            public IReadOnlyList<int> Rows { get; init; } = Array.Empty<int>();

            public int FirstRow => Rows.Count == 0 ? -1 : Rows[0];
            public int LastRow => Rows.Count == 0 ? -1 : Rows[Rows.Count - 1];
            public bool IsValid => Rows.Count > 0;
        }

        /// <summary>
        /// Posición de inserción de un bloque durante el arrastre, ya expresada
        /// sobre la lista sin el bloque (índice donde debe insertarse) y sobre la
        /// lista original (fila donde se dibuja el indicador).
        /// </summary>
        public sealed class RowDragPlacement
        {
            public static RowDragPlacement Invalid { get; } = new();

            public bool IsValid { get; init; }
            public bool IsNoOp { get; init; }
            public int InsertionIndex { get; init; } = -1;
            public int IndicatorRow { get; init; } = -1;
        }

        /// <summary>
        /// Decide qué filas forman el bloque de arrastre a partir de la selección
        /// capturada ANTES del colapso de WinForms. Solo se mueve una multi-selección
        /// de filas completas, contiguas y que contenga el origen; cualquier selección
        /// dispersa o un rango de celdas degrada a la fila de origen (comportamiento
        /// histórico del arrastre simple).
        /// </summary>
        public static RowDragBlock BuildRowDragBlock(
            int originRow,
            IReadOnlyList<int>? selectedWholeRowIndexes,
            IReadOnlyList<BudgetHierarchyRow> hierarchyRows)
        {
            if (hierarchyRows is null || originRow < 0 || originRow >= hierarchyRows.Count)
                return new RowDragBlock { OriginRow = originRow };

            var seleccionadas = new List<int>();
            if (selectedWholeRowIndexes is not null)
            {
                foreach (int indice in selectedWholeRowIndexes)
                {
                    if (indice >= 0 && indice < hierarchyRows.Count && !seleccionadas.Contains(indice))
                        seleccionadas.Add(indice);
                }
            }

            seleccionadas.Sort();
            bool multiSeleccion = seleccionadas.Count > 1
                && seleccionadas.Contains(originRow)
                && EsRangoContiguo(seleccionadas);

            var raices = multiSeleccion ? seleccionadas : new List<int> { originRow };
            var expandidas = ExpandirConJerarquia(hierarchyRows, raices);
            bool expandioJerarquia = expandidas.Count > raices.Count;

            if (multiSeleccion && !SoloContenidoIncluido(hierarchyRows, expandidas))
            {
                multiSeleccion = false;
                raices = new List<int> { originRow };
                expandidas = ExpandirConJerarquia(hierarchyRows, raices);
                expandioJerarquia = expandidas.Count > raices.Count;
            }

            var bloque = RangoFisicoContiguo(expandidas);
            return new RowDragBlock
            {
                OriginRow = originRow,
                IsMultiSelection = multiSeleccion,
                ExpandedHierarchy = expandioJerarquia,
                Rows = bloque
            };
        }

        /// <summary>
        /// Calcula dónde insertar el bloque al soltarlo sobre <paramref name="dropRow"/>.
        /// Hacia arriba inserta antes de la fila apuntada; hacia abajo, después. Un
        /// destino dentro del propio bloque (o que reproduce su posición actual) es
        /// un no-op. El indicador nunca queda dentro del bloque.
        /// </summary>
        public static RowDragPlacement ResolveRowDragPlacement(
            IReadOnlyList<int>? blockRows,
            int dropRow,
            int lastContentRow,
            int rowCount)
        {
            if (blockRows is null || rowCount <= 0 || dropRow < 0)
                return RowDragPlacement.Invalid;

            // Una fila destino fuera del grid no es una posición de inserción: no se
            // recorta a la última fila (eso movería el bloque a un destino que el
            // usuario no apuntó). El "final del contenido" se expresa con una fila
            // válida y el límite de contenido.
            if (dropRow >= rowCount)
                return RowDragPlacement.Invalid;

            var bloque = blockRows
                .Where(i => i >= 0 && i < rowCount)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (bloque.Count == 0)
                return RowDragPlacement.Invalid;

            int primera = bloque[0];
            int ultima = bloque[bloque.Count - 1];

            if (dropRow >= primera && dropRow <= ultima)
            {
                return new RowDragPlacement
                {
                    IsValid = true,
                    IsNoOp = true,
                    InsertionIndex = primera,
                    IndicatorRow = -1
                };
            }

            int limite = lastContentRow >= 0
                ? Math.Min(rowCount, lastContentRow + 1)
                : rowCount;
            int frontera = dropRow < primera ? dropRow : dropRow + 1;
            frontera = Math.Clamp(frontera, 0, limite);

            int indiceInsercion = frontera > primera
                ? frontera - bloque.Count
                : frontera;

            bool sinCambio = indiceInsercion == primera;
            return new RowDragPlacement
            {
                IsValid = true,
                IsNoOp = sinCambio,
                InsertionIndex = indiceInsercion,
                IndicatorRow = sinCambio ? -1 : frontera
            };
        }

        private static bool EsRangoContiguo(IReadOnlyList<int> filas)
        {
            if (filas.Count <= 1) return true;
            return filas[filas.Count - 1] - filas[0] + 1 == filas.Count;
        }

        private static HashSet<int> ExpandirConJerarquia(
            IReadOnlyList<BudgetHierarchyRow> hierarchyRows,
            IReadOnlyList<int> raices)
        {
            var indices = new HashSet<int>();
            foreach (int raiz in raices)
            {
                foreach (int indice in BudgetHierarchyService.CollectHierarchicalBlock(hierarchyRows, raiz))
                    indices.Add(indice);
            }

            return indices;
        }

        private static bool SoloContenidoIncluido(
            IReadOnlyList<BudgetHierarchyRow> hierarchyRows,
            HashSet<int> incluidos)
        {
            if (incluidos.Count == 0) return false;

            int primera = incluidos.Min();
            int ultima = incluidos.Max();
            for (int i = primera; i <= ultima; i++)
            {
                if (!incluidos.Contains(i) && hierarchyRows[i].HasContent)
                    return false;
            }

            return true;
        }

        private static IReadOnlyList<int> RangoFisicoContiguo(HashSet<int> incluidos)
        {
            if (incluidos.Count == 0) return Array.Empty<int>();

            int primera = incluidos.Min();
            int ultima = incluidos.Max();
            var bloque = new List<int>(ultima - primera + 1);
            for (int i = primera; i <= ultima; i++)
                bloque.Add(i);

            return bloque;
        }

        public static BudgetGridInteractionResult HandleCellDoubleClick(
            int rowIndex,
            int columnIndex,
            bool isReadOnly,
            string columnName,
            string? columnInternalName,
            string? tipo)
        {
            var result = new BudgetGridInteractionResult();
            if (rowIndex < 0 || columnIndex < 0)
                return result;

            bool esConcepto = string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase);
            bool esColumnaPu = string.Equals(columnInternalName, "PrecioUnitario", StringComparison.OrdinalIgnoreCase);

            if (esConcepto && esColumnaPu)
            {
                result.ShouldOpenApuSelector = true;
                return result;
            }

            if (!isReadOnly
                && !string.Equals(columnName, "colNumero", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(columnName, "colId", StringComparison.OrdinalIgnoreCase))
            {
                result.ShouldBeginEdit = true;
            }

            return result;
        }

        public static BudgetGridInteractionResult HandleF2(
            int rowIndex,
            string? tipo,
            string? columnInternalName)
        {
            var result = new BudgetGridInteractionResult();

            bool esConcepto = string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase);
            bool esColumnaPu = string.Equals(columnInternalName, "PrecioUnitario", StringComparison.OrdinalIgnoreCase);
            if (rowIndex >= 0 && esConcepto && esColumnaPu)
            {
                result.Handled = true;
                result.SuppressKeyPress = true;
                result.ShouldOpenApuSelector = true;
            }

            return result;
        }

        public static BudgetGridInteractionResult HandleInsert(int currentRowIndex, int firstEmptyRow, int lastDataRow)
        {
            var result = new BudgetGridInteractionResult
            {
                Handled = true,
                SuppressKeyPress = true
            };

            if (currentRowIndex < 0)
                return result;

            int targetRow = Math.Max(firstEmptyRow, lastDataRow + 1);
            if (targetRow < 0)
                targetRow = 0;

            if (currentRowIndex != targetRow)
            {
                result.FocusRowIndex = targetRow;
            }

            result.ShouldInsertConceptRow = true;
            result.InsertRowIndex = targetRow;
            result.FocusRowIndex = targetRow;
            return result;
        }

        public static BudgetGridInteractionResult BuildDeletionPlan(
            int rowIndex,
            bool rowHasContent,
            IReadOnlyList<BudgetHierarchyRow> hierarchyRows)
        {
            var result = new BudgetGridInteractionResult
            {
                Handled = true,
                SuppressKeyPress = true
            };

            if (rowIndex < 0 || !rowHasContent || hierarchyRows == null || rowIndex >= hierarchyRows.Count)
                return result;

            var rowsToDelete = BudgetHierarchyService.CollectHierarchicalBlock(hierarchyRows, rowIndex)
                .OrderBy(i => i)
                .ToList();

            if (rowsToDelete.Count == 0)
                return result;

            result.RowsToDelete = rowsToDelete;
            result.ConfirmationMessage = rowsToDelete.Count == 1
                ? "¿Eliminar esta fila?"
                : $"¿Eliminar esta fila y todas sus {rowsToDelete.Count - 1} sub-filas?\n\nSe eliminarán {rowsToDelete.Count} filas en total.";

            return result;
        }

        public static bool ShouldStartTypingEdit(bool hasCurrentCell, bool isReadOnly, string columnName, char keyChar, bool isCurrentCellInEditMode)
        {
            if (!hasCurrentCell) return false;
            if (isReadOnly) return false;
            if (string.Equals(columnName, "colNumero", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "colId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnName, "colRelleno", StringComparison.OrdinalIgnoreCase))
                return false;
            if (char.IsControl(keyChar)) return false;
            return !isCurrentCellInEditMode;
        }
    }
}
