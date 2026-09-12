namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Tipo de un elemento libre de la plantilla de PDF
/// (valores legacy "TextoLibre" | "EtiquetaDinamica" | "Imagen").
/// </summary>
public enum MatrixCatalogPageElementKind
{
    TextoLibre = 0,
    EtiquetaDinamica = 1,
    Imagen = 2
}