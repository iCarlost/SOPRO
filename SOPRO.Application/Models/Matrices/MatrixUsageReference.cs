using SOPRO.Core.Entities;

namespace SOPRO.Application.Models.Matrices
{
    public sealed class MatrixUsageReference
    {
        public int MatrizId { get; set; }
        public string DisplayLabel { get; set; } = string.Empty;
        public Matriz Source { get; set; }
    }
}
