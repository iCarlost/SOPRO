using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public static class MatrixComponentGridFlowService
    {
        public static bool IsDeleteRequest(int rowIndex, int clickedColumnIndex, int deleteColumnIndex)
        {
            return rowIndex >= 0 && clickedColumnIndex == deleteColumnIndex;
        }

        public static bool ShouldOpenRendimientoDialog(ComponenteMatriz component, string columnName)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            return string.Equals(columnName, "colCantidad", StringComparison.Ordinal)
                && MatrixComponentInteractionService.RequiresRendimientoDialog(component);
        }

        public static bool CanBeginEdit(ComponenteMatriz component, string columnName)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            return columnName switch
            {
                "colCantidad" => MatrixComponentInteractionService.CanEditQuantityInline(component),
                "colPU" => MatrixComponentInteractionService.CanEditUnitPriceInline(component),
                _ => true
            };
        }

        public static MatrixComponentEditResult? ApplyCellEdit(ComponenteMatriz component, string columnName, string value, int decimalesImporte)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            return columnName switch
            {
                "colDescripcion" => MatrixComponentEditingService.UpdateDescription(component, value),
                "colUnidad" => MatrixComponentEditingService.UpdateUnit(component, value),
                "colCantidad" => MatrixComponentEditingService.UpdateQuantity(component, value),
                "colPU" => MatrixComponentEditingService.UpdateUnitPrice(component, value, decimalesImporte),
                _ => null
            };
        }
    }
}
