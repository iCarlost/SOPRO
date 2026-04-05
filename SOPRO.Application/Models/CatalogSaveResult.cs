namespace SOPRO.Application.Models
{
    public sealed class CatalogSaveResult
    {
        public int EntityId { get; set; }
        public bool IsNew { get; set; }
        public bool TriggeredRecalculation { get; set; }
    }
}
