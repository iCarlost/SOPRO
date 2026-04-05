namespace SOPRO.Application.Models
{
    public class MatrixSaveResult
    {
        public int MatrixId { get; set; }
        public bool IsNew { get; set; }
        public int CascadedMatricesUpdated { get; set; }
    }
}
