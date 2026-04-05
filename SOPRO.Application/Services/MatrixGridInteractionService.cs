using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    public sealed class MatrixGridInteractionService
    {
        public MatrixContextMenuState BuildContextMenuState(bool clickedRowAlreadySelected, int selectedRowsCount, TipoMatriz? tipo)
        {
            var safeCount = selectedRowsCount < 1 ? 1 : selectedRowsCount;

            return new MatrixContextMenuState
            {
                PreserveMultiSelection = clickedRowAlreadySelected && safeCount > 1,
                ShowUsageLookup = tipo == TipoMatriz.Basico || tipo == TipoMatriz.Cuadrilla,
                CopyRowsLabel = safeCount > 1 ? $"📄  Copiar {safeCount} filas" : "📄  Copiar fila"
            };
        }
    }
}
