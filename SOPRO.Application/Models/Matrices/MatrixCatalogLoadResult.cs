namespace SOPRO.Application.Models.Matrices
{
    public sealed class MatrixCatalogLoadResult
    {
        public List<MatrixGridRowDisplay> Rows { get; set; } = new();
        public string StatusText { get; set; } = string.Empty;
    }
}
