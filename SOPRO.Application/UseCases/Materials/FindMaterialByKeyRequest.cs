namespace SOPRO.Application.UseCases.Materials;

/// <summary>Request de búsqueda de material por clave dentro del alcance de la sesión.</summary>
/// <param name="Clave">Clave a buscar (sin normalizar; el caso de uso la recorta).</param>
public sealed record FindMaterialByKeyRequest(string Clave);