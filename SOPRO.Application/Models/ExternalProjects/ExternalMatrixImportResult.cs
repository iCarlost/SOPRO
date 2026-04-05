namespace SOPRO.Application.Models.ExternalProjects
{
    public sealed class ExternalMatrixImportResult
    {
        public int RootMatrixId { get; set; }
        public int ImportedMatrices { get; set; }
        public int ImportedMateriales { get; set; }
        public int ImportedManoDeObra { get; set; }
        public int ImportedMaquinaria { get; set; }
        public int ImportedHerramientas { get; set; }
    }
}
