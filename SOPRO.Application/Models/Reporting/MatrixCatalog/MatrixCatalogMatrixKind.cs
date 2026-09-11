namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Clasificación neutral de una matriz: análisis de precio unitario, básico/auxiliar
/// o cuadrilla de mano de obra.
/// </summary>
public enum MatrixCatalogMatrixKind
{
    Apu = 0,
    Basico = 1,
    Cuadrilla = 2
}