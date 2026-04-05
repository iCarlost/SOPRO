namespace SOPRO.Application.Models.Matrices
{
    public sealed class MatrixSaveValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public MatrixEditorField Field { get; set; } = MatrixEditorField.None;

        public static MatrixSaveValidationResult Valid() => new() { IsValid = true };
    }
}
