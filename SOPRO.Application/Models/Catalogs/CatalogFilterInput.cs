namespace SOPRO.Application.Models.Catalogs
{
    public sealed class CatalogFilterInput
    {
        public int? ProyectoId { get; set; }
        // Compatibilidad con nombres existentes de WinForms:
        // SoloProyecto => SoloLocales
        // SoloMaestros => SoloImportados
        public bool SoloProyecto { get; set; }
        public bool SoloMaestros { get; set; }
        public string? SearchText { get; set; }
    }
}
