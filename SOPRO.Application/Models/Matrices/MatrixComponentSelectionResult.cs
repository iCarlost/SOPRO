namespace SOPRO.Application.Models.Matrices
{
    public sealed class MatrixComponentSelectionResult
    {
        public bool HasComponents { get; set; }
        public int AddedCount { get; set; }
        public bool RequiresRecalculation => HasComponents && AddedCount > 0;
    }
}
