namespace SOPRO.Application.Services
{
    public static class KeySuggestionService
    {
        public static string Generate()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }
    }
}
