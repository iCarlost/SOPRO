using SOPRO.Application.Models.Presupuesto;

namespace SOPRO.Application.Services
{
    public static class BudgetGridInteractionService
    {
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
