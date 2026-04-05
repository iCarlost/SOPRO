namespace SOPRO.Application.Models.Matrices
{
    public sealed class MatrixContextMenuState
    {
        public bool PreserveMultiSelection { get; init; }
        public bool ShowUsageLookup { get; init; }
        public string CopyRowsLabel { get; init; } = "📄  Copiar fila";
    }
}
