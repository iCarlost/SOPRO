namespace SOPRO.Application.UseCases.Catalog;

/// <summary>
/// Tipo de insumo del catálogo que participa en una operación de lote. El lote
/// es homogéneo: una operación elimina un solo tipo (la UI agrupa la selección
/// por catálogo), de modo que un lote mixto no es expresable.
/// </summary>
public enum CatalogItemKind
{
    /// <summary>Material (catálogo del proyecto o copia importada del maestro).</summary>
    Material = 0,

    /// <summary>Mano de obra (catálogo del proyecto o copia importada del maestro).</summary>
    ManoDeObra = 1,

    /// <summary>Herramienta o equipo menor (siempre pertenece a un proyecto).</summary>
    Herramienta = 2,

    /// <summary>Maquinaria (catálogo del proyecto o copia importada del maestro).</summary>
    Maquinaria = 3,
}
