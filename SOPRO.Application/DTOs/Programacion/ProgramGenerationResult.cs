namespace SOPRO.Application.DTOs.Programacion
{
    public sealed class ProgramGenerationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? ProgramaObraId { get; set; }
        public int ActividadesGeneradas { get; set; }
        public int PeriodosGenerados { get; set; }
    }
}
