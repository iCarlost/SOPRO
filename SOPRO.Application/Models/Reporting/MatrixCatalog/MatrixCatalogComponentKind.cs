namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Tipo de insumo de un componente de matriz, neutral (sin acoplarse a Core).
/// Cada valor tiene un prefijo visual legacy propio (ver MapearPrefijo).
/// </summary>
public enum MatrixCatalogComponentKind
{
    Material = 0,
    ManoDeObra = 1,
    Maquinaria = 2,
    Auxiliar = 3,
    Herramienta = 4
}