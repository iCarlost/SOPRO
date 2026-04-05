namespace SOPRO.Application.Models.Matrices
{
    public class MatrixComponentEditResult
    {
        public bool Success { get; set; }
        public bool RequiresRecalculation { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static MatrixComponentEditResult Ok(bool requiresRecalculation = true)
        {
            return new MatrixComponentEditResult
            {
                Success = true,
                RequiresRecalculation = requiresRecalculation
            };
        }

        public static MatrixComponentEditResult Fail(string errorMessage = "")
        {
            return new MatrixComponentEditResult
            {
                Success = false,
                RequiresRecalculation = false,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }
    }
}
